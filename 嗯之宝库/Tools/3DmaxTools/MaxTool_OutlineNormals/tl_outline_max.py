"""3ds Max 平滑法线 UV 工具。由 Launch.ms 启动；无需安装外部依赖。"""
import os
import sys
import traceback
import importlib.util
import html
import math
from datetime import datetime
from contextlib import contextmanager

ROOT = os.path.dirname(os.path.abspath(__file__))
if ROOT in sys.path:
    sys.path.remove(ROOT)
sys.path.insert(0, ROOT)

# Max 长时间保留 Python 模块缓存；更新脚本后从当前目录源码重新载入。
# 直接编译源码也避免同秒替换文件时沿用旧 .pyc。
_math_path = os.path.join(ROOT, 'outline_math.py')
_math_spec = importlib.util.spec_from_file_location('outline_math', _math_path)
_math_module = importlib.util.module_from_spec(_math_spec)
with open(_math_path, 'r', encoding='utf-8-sig') as _math_file:
    exec(compile(_math_file.read(), _math_path, 'exec'), _math_module.__dict__)
if not all(hasattr(_math_module, name) for name in ('oct_encode', 'oct_decode')):
    raise ImportError('outline_math.py 版本过旧，请将新版工具包完整解压覆盖后重新运行 Launch.ms。')
sys.modules['outline_math'] = _math_module

import pymxs
from pymxs import runtime as rt
from outline_math import dot, cross, unit, mul, smooth_normals, oct_encode, oct_decode, is_degenerate_triangle
import importlib
import tangent_space
importlib.reload(tangent_space)
from tangent_space import corner_frames, unity_corner_frames, to_tangent, from_tangent

_dialog = None
COLLAPSED_NORMALS_ID = 0x544C554E


class CollapseError(RuntimeError):
    """A changed collapse result requires cancelling the whole UI transaction."""


@contextmanager
def undo_transaction(label):
    """Join our batch hold, or own one that rolls back and propagates errors.

    pymxs.undo(True) starts a hold and may swallow exceptions after rollback.
    Keep the exception so UI callers can accurately report the failed action.
    """
    if rt.theHold.Holding():
        yield
        return
    failure = None
    with pymxs.undo(True, label):
        try:
            yield
        except Exception as error:
            failure = error
            raise
    if failure is not None:
        raise failure


class BakeResult(str):
    """Keep face-level skips separate from the human-readable message."""
    def __new__(cls, message, skipped_faces=0, skipped_vertices=0):
        result = super().__new__(cls, message)
        result.skipped_faces = skipped_faces
        result.skipped_vertices = skipped_vertices
        return result


@contextmanager
def preserve_modifier_panel():
    """Temporary normal readers must not leave Skin's editing panel behind."""
    selected = list(rt.selection)
    mode = rt.getCommandPanelTaskMode()
    current = rt.modPanel.getCurrentObject() if str(mode).lower() == 'modify' else None
    current_handle = rt.getHandleByAnim(current) if current is not None else None
    owner = next((node for node in selected
                  if current == node.baseObject or current in list(node.modifiers)), None)
    level = int(rt.subObjectLevel or 0)
    try:
        yield
    finally:
        remaining = [node for node in selected if rt.isValidNode(node)]
        if list(rt.selection) != remaining:
            if remaining:
                rt.select(rt.Array(*remaining))
            else:
                rt.clearSelection()
        rt.setCommandPanelTaskMode(mode)
        if current_handle is not None:
            if owner is not None and rt.isValidNode(owner):
                # Collapse deletes the old base: comparing its stale pymxs wrapper
                # can raise a native "access to deleted object" error. Resolve by
                # the handle captured while it was still alive instead.
                available = [owner.baseObject] + list(owner.modifiers)
                current = next((m for m in available if rt.getHandleByAnim(m) == current_handle), None)
                if current is None:
                    current = next((m for m in owner.modifiers if str(rt.classOf(m)) == 'Skin'), owner.baseObject)
                    level = 0
                rt.modPanel.setCurrentObject(current, node=owner)
            else:
                current = rt.getAnimByHandle(current_handle)
                if current is not None:
                    rt.modPanel.setCurrentObject(current)
            if current is not None:
                rt.subObjectLevel = level


def focus_selected_skin(nodes):
    """A single skinned mesh returns to its existing, editable Skin modifier."""
    if len(nodes) == 1 and rt.isValidNode(nodes[0]):
        skin = next((m for m in nodes[0].modifiers if str(rt.classOf(m)) == 'Skin'), None)
        if skin is not None:
            if list(rt.selection) != nodes:
                rt.select(nodes[0])
            # 重设已经处于激活状态的 Modify 面板会把 Skin 子对象层级清零。
            # 仅在确实需要切换面板时调用，保留用户原来的权重编辑层级。
            if str(rt.getCommandPanelTaskMode()).lower() != 'modify':
                rt.setCommandPanelTaskMode(rt.Name('modify'))
            if rt.modPanel.getCurrentObject() != skin:
                rt.modPanel.setCurrentObject(skin, node=nodes[0])


def point(value):
    return (float(value.x), float(value.y), float(value.z))


def read_mesh(node):
    if rt.superClassOf(node) != rt.GeometryClass:
        raise ValueError('请选择网格对象。')
    # 公共坐标基可以自动处理旋转、轴转换和等比缩放。
    # 非均匀缩放/错切会改变角度权重和基向量，要求先 Reset XForm。
    rows = [point(node.objectTransform.row1), point(node.objectTransform.row2),
            point(node.objectTransform.row3)]
    lengths = [dot(v, v) for v in rows]
    maximum = max(lengths)
    minimum = min(lengths)
    invalid = minimum <= 1e-20 or maximum / minimum > 1.0001
    invalid = invalid or any(abs(dot(rows[a], rows[b])) > maximum * 1e-5
                             for a, b in ((0, 1), (0, 2), (1, 2)))
    invalid = invalid or dot(cross(rows[0], rows[1]), rows[2]) < 0
    if invalid:
        reasons = []
        if minimum <= 1e-20: reasons.append('零缩放')
        elif maximum / minimum > 1.0001: reasons.append('非均匀缩放')
        if any(abs(dot(rows[a], rows[b])) > maximum * 1e-5 for a, b in ((0, 1), (0, 2), (1, 2))): reasons.append('错切')
        if dot(cross(rows[0], rows[1]), rows[2]) < 0: reasons.append('镜像负缩放')
        raise ValueError('对象变换无效：%s；X/Y/Z 轴倍率 %s。请先 Reset XForm 后烘焙。' % ('、'.join(reasons), diagnostic_value(tuple(math.sqrt(v) for v in lengths))))
    mesh = rt.snapshotAsMesh(node)
    vertices = [point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)]
    faces = [tuple(int(v) - 1 for v in point(rt.getFace(mesh, i)))
             for i in range(1, int(rt.getNumFaces(mesh)) + 1)]
    if not vertices or not faces:
        raise ValueError('网格没有有效顶点/三角面。')
    return mesh, vertices, faces


def map_has_data(mesh, channel):
    return (channel < int(rt.meshop.getNumMaps(mesh)) and rt.meshop.getMapSupport(mesh, channel)
            and int(rt.meshop.getNumMapVerts(mesh, channel)) > 0)


def is_outline_channel(mesh, channel, include_experimental=False):
    if not map_has_data(mesh, channel):
        return False
    values = [point(rt.meshop.getMapVert(mesh, channel, i))
              for i in range(1, int(rt.meshop.getNumMapVerts(mesh, channel)) + 1)]
    def marked(offset):
        return all(offset - .0001 <= uv[0] <= offset + 1.0001
                   and -.0001 <= uv[1] <= 1.0001 and abs(uv[2]) < .0001 for uv in values)
    return marked(2) or (include_experimental and marked(4))


def mesh_frames(mesh, vertices, faces):
    """按面角读取原 NORMAL 和 UV1；只计算普通 MikkTSpace 帧，不写入切线。"""
    normals = [[unit(point(n)) for n in rt.meshop.getFaceRNormals(mesh, fi)]
               for fi in range(1, len(faces) + 1)]
    uv_faces = [tuple(int(v) - 1 for v in point(rt.meshop.getMapFace(mesh, 1, fi)))
                for fi in range(1, len(faces) + 1)]
    uv_vertices = [point(rt.meshop.getMapVert(mesh, 1, vi))
                   for vi in range(1, int(rt.meshop.getNumMapVerts(mesh, 1)) + 1)]
    return corner_frames(vertices, faces, normals, uv_faces, uv_vertices)


def read_render_normals(node, face_count):
    """Read the specified normals used by FBX without depending on texture UVs."""
    with preserve_modifier_panel():
        reader = rt.Edit_Normals()
        rt.addModifier(node, reader)
        try:
            if list(rt.selection) != [node]:
                rt.select(node)
            rt.setCommandPanelTaskMode(rt.Name('modify'))
            rt.modPanel.setCurrentObject(reader, node=node)
            normals = [[unit(point(reader.GetNormal(reader.GetNormalID(fi, ci)))) for ci in range(1,4)]
                       for fi in range(1,face_count+1)]
        finally:
            rt.deleteModifier(node, reader)
    return normals


def local_bake_data(node, mesh, vertices, faces, skip_degenerate=False):
    """Bake before the object/pivot transform, matching exported mesh precision."""
    # node.mesh is the evaluated object-local mesh; snapshotAsMesh transforms to world
    # and perturbs equal normals enough to change MikkTSpace's exact welding groups.
    local_mesh = node.mesh
    local = [point(rt.getVert(local_mesh, i)) for i in range(1,len(vertices)+1)]
    normals = read_render_normals(node, len(faces))
    normals = [[unit(tuple(round(x, 5) for x in n)) for n in face] for face in normals]
    uv_faces = [tuple(int(v) - 1 for v in point(rt.meshop.getMapFace(mesh, 1, fi)))
                for fi in range(1, len(faces) + 1)]
    uv_vertices = [point(rt.meshop.getMapVert(mesh, 1, vi))
                   for vi in range(1, int(rt.meshop.getNumMapVerts(mesh, 1)) + 1)]
    if not skip_degenerate:
        return local, unity_corner_frames(local, faces, normals, uv_faces, uv_vertices), normals
    valid = [i for i, face in enumerate(faces) if not is_degenerate_triangle(local, face)]
    if not valid:
        raise ValueError('没有可烘焙的有效三角面，模型已保留。')
    for fi in valid:
        for ci, n in enumerate(normals[fi]):
            if not .5 <= dot(n, n) <= 1.5:
                raise ValueError('有效面的原始法线无效：三角面 #%s / 面角 %s / 几何顶点 #%s，法线 %s。' % (fi + 1, ci + 1, faces[fi][ci] + 1, diagnostic_value(n)))
    # Mikk and smooth directions use only effective geometry. Degenerate faces
    # remain in the mesh; their UV encodes tangent-space +Z (original normal).
    calculated = unity_corner_frames(local, [faces[i] for i in valid],
                                     [normals[i] for i in valid], [uv_faces[i] for i in valid], uv_vertices, face_ids=valid)
    frames = [None] * (len(faces) * 3)
    for index, fi in enumerate(valid):
        frames[fi * 3:fi * 3 + 3] = calculated[index * 3:index * 3 + 3]
    return local, frames, normals


def freeze_render_normals(node, normals=None, faces=None):
    """Pin the render normals used by TBN; remove float noise within 1e-5."""
    with preserve_modifier_panel():
        return _freeze_render_normals(node, normals, faces)


def _freeze_render_normals(node, normals, faces):
    modifier = rt.Edit_Normals()
    rt.addModifier(node, modifier)
    modifier.name = 'TL Outline Render Normals'
    if list(rt.selection) != [node]:
        rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(modifier)
    count = int(modifier.GetNumNormals())
    modifier.MakeExplicit(selection=rt.execute('#{1..%s}' % count))
    if normals is not None:
        if faces is not None:
            groups = {}
            for fi, face in enumerate(normals,1):
                for ci, normal in enumerate(face,1):
                    key = (faces[fi-1][ci-1], tuple(round(x,5) for x in normal))
                    groups.setdefault(key, set()).add(int(modifier.GetNormalID(fi,ci)))
            for ids in groups.values():
                if len(ids)>1:
                    modifier.Unify(selection=rt.execute('#{'+','.join(str(i) for i in ids)+'}'))
        modifier.MakeExplicit(selection=rt.execute('#{1..%s}' % int(modifier.GetNumNormals())))
        for fi, face in enumerate(normals,1):
            for ci, normal in enumerate(face,1):
                i = modifier.GetNormalID(fi,ci)
                modifier.SetNormal(i, rt.Point3(*normal))
    return modifier


def disable_conflicting_uv_clears(node, channels):
    """Keep clear modifiers, disabling only channels written below Skin."""
    modifiers = list(node.modifiers)
    skin_index = next((i for i, m in enumerate(modifiers) if str(rt.classOf(m)) == 'Skin'), None)
    changed = []
    if skin_index is not None:
        for modifier in modifiers[:skin_index]:
            if (str(rt.classOf(modifier)) == 'UVW_Mapping_Clear'
                    and int(modifier.mapID) in channels and bool(modifier.enabled)):
                changed.append((modifier, bool(modifier.enabled)))
                modifier.enabled = False
    return changed


def uv_corner_values(mesh, channel):
    return tuple(point(rt.meshop.getMapVert(mesh, channel, int(index)))
                 for fi in range(1, int(rt.getNumFaces(mesh)) + 1)
                 for index in point(rt.meshop.getMapFace(mesh, channel, fi)))


def verify_uv_channel(node, channel, zero=False, expected=None):
    """Check the evaluated stack, including modifiers above Skin."""
    mesh = rt.snapshotAsMesh(node)
    if not map_has_data(mesh, channel):
        raise RuntimeError('第 %s 套 UV 在最终修改器结果中丢失，请检查上方修改器。' % channel)
    if zero:
        valid = (int(rt.meshop.getNumMapVerts(mesh, channel)) == 1
                 and point(rt.meshop.getMapVert(mesh, channel, 1)) == (0., 0., 0.))
    else:
        valid = is_outline_channel(mesh, channel)
    if not valid:
        raise RuntimeError('第 %s 套 UV 被后续修改器覆盖，请检查上方修改器。' % channel)
    count = int(rt.meshop.getNumMapVerts(mesh, channel))
    for fi in range(1, int(rt.getNumFaces(mesh)) + 1):
        if any(not 1 <= int(v) <= count for v in point(rt.meshop.getMapFace(mesh, channel, fi))):
            raise RuntimeError('第 %s 套 UV 的面索引无效：三角面 #%s，引用 %s，共 %s 个 UV 顶点。' % (channel, fi, point(rt.meshop.getMapFace(mesh, channel, fi)), count))
    if expected is not None:
        actual = uv_corner_values(mesh, channel)
        faces = [point(rt.getFace(mesh, i)) for i in range(1, int(rt.getNumFaces(mesh)) + 1)]
        difference = channel_difference(channel, expected, actual, faces)
        if difference:
            raise RuntimeError('阶段：最终通道校验；上方修改器改变了烘焙数据。\n' + difference)


def has_pinned_render_normals(node):
    return (any(str(m.name) == 'TL Outline Render Normals' for m in node.modifiers)
            or (str(rt.classOf(node.baseObject)).lower() == 'editable_mesh'
                and str(rt.getAppData(node, COLLAPSED_NORMALS_ID)) == 'specified-v1'))


def is_tool_modifier(modifier):
    return (str(modifier.name).startswith(('TL Outline ', 'TL Clear UV2 '))
            and str(rt.classOf(modifier)) in ('Edit_Normals', 'UVW_Mapping_Paste', 'Turn_to_Mesh'))


def collapse_fingerprint(node):
    """Compare the complete evaluated stack, independent of the selected layer.

    When the base is selected and Show End Result is off, both node.mesh and
    snapshotAsMesh omit the UV Paste layers. Collapsing moves their data into
    the base, causing a false channel-added error. Normal-reader panel restore
    can turn Show End Result off again, so enable it again before mesh capture.
    Keep local coordinates for exact geometry comparisons.
    """
    show_end = bool(rt.showEndResult)
    try:
        rt.showEndResult = True
        evaluated = rt.snapshotAsMesh(node)
        count = int(rt.getNumFaces(evaluated))
        normals = read_render_normals(node, count)
        rt.showEndResult = True
        mesh = node.mesh
        if int(rt.getNumFaces(mesh)) != count:
            raise CollapseError('校验读取期间三角面数发生变化，请检查会改变拓扑的修改器。')
        channels = {c: uv_corner_values(mesh, c) for c in range(-2, int(rt.meshop.getNumMaps(mesh)))
                    if map_has_data(mesh, c)}
        return {
            'vertices': tuple(point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)),
            'faces': tuple(point(rt.getFace(mesh, i)) for i in range(1, count + 1)),
            'materials': tuple(int(rt.getFaceMatID(mesh, i)) for i in range(1, count + 1)),
            'smoothing': tuple(int(rt.getFaceSmoothGroup(mesh, i)) for i in range(1, count + 1)),
            'channels': channels,
            'normals': normals,
        }
    finally:
        rt.showEndResult = show_end


def skin_fingerprint(node, skin):
    frozen, hidden = bool(node.isFrozen), bool(node.isHidden)
    with preserve_modifier_panel():
        try:
            node.isFrozen, node.isHidden = False, False
            if list(rt.selection) != [node]:
                rt.select(node)
            rt.setCommandPanelTaskMode(rt.Name('modify'))
            rt.modPanel.setCurrentObject(skin, node=node)
            count = int(rt.skinOps.GetNumberVertices(skin))
            bones = int(rt.skinOps.GetNumberBones(skin))
            return (bool(skin.enabled), bool(skin.enabledInViews), bool(skin.enabledInRenders),
                    tuple(str(rt.skinOps.GetBoneName(skin, i, 0)) for i in range(1, bones + 1)),
                    tuple(tuple((int(rt.skinOps.GetVertexWeightBoneID(skin, i, j)),
                                 float(rt.skinOps.GetVertexWeight(skin, i, j)))
                                for j in range(1, int(rt.skinOps.GetVertexWeightCount(skin, i)) + 1))
                          for i in range(1, count + 1)),
                    tuple(float(rt.skinOps.getVertexDQWeight(skin, i)) for i in range(1, count + 1)),
                    tuple(bool(rt.skinOps.isUnNormalizeVertex(skin, i)) for i in range(1, count + 1)),
                    tuple(bool(rt.skinOps.isRigidVertex(skin, i)) for i in range(1, count + 1)))
        finally:
            node.isFrozen, node.isHidden = frozen, hidden


CHANNEL_VALUE_TOLERANCE = 1e-6


def channel_label(channel):
    return {-2: '顶点 Alpha（通道 -2）', -1: '顶点光照色（通道 -1）',
            0: '顶点色 RGB（通道 0）'}.get(channel, '第 %s 套 UV（Max Channel %s）' % (channel, channel))


def diagnostic_value(value):
    if isinstance(value, (tuple, list)):
        return '(' + ', '.join(diagnostic_value(v) for v in value) + ')'
    return '%.9g' % value if isinstance(value, (float, int)) else str(value)


def corner_location(index, faces):
    fi, ci = divmod(index, 3)
    vertex = int(faces[fi][ci]) if fi < len(faces) else None
    return '三角面 #%s / 面角 %s%s' % (fi + 1, ci + 1, ' / 几何顶点 #%s' % vertex if vertex else '')


def channel_difference(channel, before, after, faces, tolerance=CHANNEL_VALUE_TOLERANCE):
    label = channel_label(channel)
    if len(before) != len(after):
        return '%s：面角数据数量 %s → %s。' % (label, len(before), len(after))
    differences, samples = 0, []
    maximum = 0.
    nonfinite = False
    for index, (a, b) in enumerate(zip(before, after)):
        finite = all(math.isfinite(v) for v in a + b)
        delta = max(abs(x - y) for x, y in zip(a, b)) if finite else float('inf')
        if not finite or delta > tolerance:
            differences += 1
            maximum = max(maximum, delta)
            nonfinite = nonfinite or not finite
            if len(samples) < 3:
                samples.append('%s：前 %s → 后 %s%s' % (
                    corner_location(index, faces), diagnostic_value(a), diagnostic_value(b),
                    '（包含 NaN/Infinity）' if not finite else ''))
    if not differences:
        return None
    return '%s：%s 个面角不一致；最大差值 %s，允许误差 %.1g。\n%s' % (
        label, differences, '非有限数值' if nonfinite else diagnostic_value(maximum), tolerance, '\n'.join(samples))


def verify_collapse(before, after):
    labels = {'vertices': '几何顶点', 'faces': '三角面顶点索引', 'materials': '材质 ID', 'smoothing': '平滑组'}
    for key, label in labels.items():
        a, b = before[key], after[key]
        if len(a) != len(b):
            raise CollapseError('%s数量改变：%s → %s。' % (label, len(a), len(b)))
        for index, (x, y) in enumerate(zip(a, b), 1):
            if x != y:
                raise CollapseError('%s #%s 改变：前 %s → 后 %s。' % (label, index, diagnostic_value(x), diagnostic_value(y)))
    old, new = before['channels'], after['channels']
    messages = []
    for channel in sorted(set(old) | set(new)):
        if channel not in old:
            messages.append('%s：塌陷后新增了此通道。' % channel_label(channel))
        elif channel not in new:
            messages.append('%s：塌陷后丢失了此通道。' % channel_label(channel))
        else:
            difference = channel_difference(channel, old[channel], new[channel], before['faces'])
            if difference:
                messages.append(difference)
    if messages:
        raise CollapseError('通道校验失败：\n' + '\n'.join(messages[:3]))
    if len(before['normals']) != len(after['normals']):
        raise CollapseError('显式法线面数改变：%s → %s。' % (len(before['normals']), len(after['normals'])))
    for fi, (a, b) in enumerate(zip(before['normals'], after['normals'])):
        for ci, (x, y) in enumerate(zip(a, b)):
            if not all(math.isfinite(v) for v in x + y) or any(abs(u - v) > 1e-6 for u, v in zip(x, y)):
                raise CollapseError('显式法线改变，%s：前 %s → 后 %s。' % (
                    corner_location(fi * 3 + ci, before['faces']), diagnostic_value(x), diagnostic_value(y)))


def collapse_tool_modifiers(node):
    """Collapse only a bottom run of tool layers; never include Skin or user layers."""
    modifiers = list(node.modifiers)
    indices = [i for i, m in enumerate(modifiers) if is_tool_modifier(m)]
    if not indices:
        return '工具修改器已整理，无需塌陷。'
    head = indices[0]
    skins = [m for m in modifiers if str(rt.classOf(m)) == 'Skin']
    if len(skins) > 1:
        raise ValueError('存在 %s 个 Skin：%s；请先检查绑定流程。' % (len(skins), ', '.join(str(m.name) for m in skins)))
    if skins and modifiers.index(skins[0]) >= head:
        raise ValueError('工具层位于 Skin 上方，不能把 Skin 包含在塌陷范围内。')
    if any(not is_tool_modifier(m) for m in modifiers[head:]):
        raise ValueError('工具层下方有其它修改器：%s；已保留原栈。' % ', '.join('%s (%s)' % (m.name, rt.classOf(m)) for m in modifiers[head:] if not is_tool_modifier(m)))
    if any(not bool(m.enabled) or not bool(m.enabledInViews) or not bool(m.enabledInRenders)
           for m in modifiers[head:]):
        raise ValueError('工具层存在关闭或视口/渲染开关不一致，不能安全塌陷。')
    if len(list(rt.refs.dependentNodes(node.baseObject))) > 1:
        raise ValueError('底层网格被多个对象引用，保留实例关系，未自动塌陷。')
    protected = modifiers[:head]
    skin = skins[0] if skins else None
    diagnostic_context = '阶段：自动塌陷；Skin：%s；底层：%s。\n' % ('无（静态网格路径）' if skin is None else str(skin.name), str(rt.classOf(node.baseObject)))
    with preserve_modifier_panel(), undo_transaction('TL Collapse Tool Modifiers'):
        skin_enabled = bool(skin.enabled) if skin is not None else None
        weights = skin_fingerprint(node, skin) if skin is not None else None
        try:
            if skin is not None:
                skin.enabled = False
                rt.notifyDependents(node)
            with pymxs.undo(False):
                before = collapse_fingerprint(node)
            rt.subObjectLevel = 0
            rt.maxOps.CollapseNodeTo(node, head + 1, True)
            if list(node.modifiers) != protected:
                raise CollapseError('塌陷范围不正确，原 Skin 或上方修改器发生变化。')
            rt.modPanel.setCurrentObject(skin if skin is not None else node.baseObject, node=node)
            with pymxs.undo(False):
                verify_collapse(before, collapse_fingerprint(node))
            if skin is not None:
                skin.enabled = skin_enabled
                rt.notifyDependents(node)
                if skin_fingerprint(node, skin) != weights:
                    raise CollapseError('塌陷改变了 Skin 的骨骼、权重或开关状态。')
            rt.setAppData(node, COLLAPSED_NORMALS_ID, 'specified-v1')
        except Exception as error:
            raise CollapseError(diagnostic_context + str(error)) from error
        finally:
            if skin is not None:
                skin.enabled = skin_enabled
                rt.notifyDependents(node)
    return '已塌陷 %s 个工具修改器，UV 和显式法线已验证保留%s。' % (
        len(indices), '，原 Skin、骨骼和权重保留' if skin is not None else '，无 Skin 静态网格路径')


def clean_isolated_vertices(node):
    """Clean topology with native Undo, preserving the original Skin instance and LocalData."""
    if rt.superClassOf(node) != rt.GeometryClass:
        raise ValueError('请选择网格对象。')
    mesh = node.mesh
    used = sorted({int(v) for i in range(1, int(rt.getNumFaces(mesh)) + 1)
                   for v in point(rt.getFace(mesh, i))})
    count = int(rt.getNumVerts(mesh))
    removed = count - len(used)
    if not removed:
        return '没有孤立点，无需清理。'
    if not used:
        raise ValueError('网格没有有效面，保留原对象，请手动检查。')
    modifiers = list(node.modifiers)
    skins = [m for m in modifiers if str(rt.classOf(m)) == 'Skin']
    if len(skins) > 1 or any(not is_tool_modifier(m) and m not in skins for m in modifiers):
        raise ValueError('存在其它修改器或多个 Skin，不能安全重排顶点编号，已保留原网格。')
    if len(list(rt.refs.dependentNodes(node.baseObject))) > 1:
        raise ValueError('底层网格被多个对象引用，未清理，避免改变其它实例。')
    skin = skins[0] if skins else None
    had_skin = skin is not None
    with preserve_modifier_panel(), undo_transaction('TL Clean Isolated Vertices'):
        skin_enabled = bool(skin.enabled) if skin is not None else None
        skin_enabled_in_views = bool(skin.enabledInViews) if skin is not None else None
        try:
            collapse_tool_modifiers(node)
            base_class = str(rt.classOf(node.baseObject)).lower()
            if base_class not in ('editable_mesh', 'editable_poly'):
                raise CollapseError('请先将底层对象转换为可编辑网格或多边形。')
            weights = skin_fingerprint(node, skin) if skin is not None else None
            if skin is not None:
                if len(weights[4]) != count:
                    raise CollapseError('Skin 输入顶点数不一致，不能安全重排权重。')
                original_skin = skin
                skin_index = list(node.modifiers).index(original_skin) + 1
                working_skin = rt.copy(original_skin)
                working_skin.enabled = original_skin.enabled
                working_skin.enabledInViews = original_skin.enabledInViews
                working_skin.enabledInRenders = original_skin.enabledInRenders
                rt.addModifierWithLocalData(
                    node, working_skin, node, original_skin, before=skin_index)
                if skin_fingerprint(node, working_skin) != weights:
                    raise CollapseError('复制 Skin LocalData 失败，已保留原修改器。')
                skin = working_skin
                skin.enabled = False
                rt.deleteModifier(node, original_skin)
                rt.modPanel.setCurrentObject(skin, node=node)
                # 几何保真应比较 Skin 的输入网格，避免蒙皮后的顶点位置造成误报。
                rt.notifyDependents(node)
            with pymxs.undo(False):
                before = collapse_fingerprint(node)
            rt.subObjectLevel = 0
            if base_class == 'editable_poly':
                if int(rt.polyop.getNumVerts(node.baseObject)) != count:
                    raise CollapseError('底层多边形含未压缩的废弃结构，不能安全映射顶点编号。')
                rt.polyop.deleteIsoVerts(node.baseObject)
                rt.polyop.collapseDeadStructs(node.baseObject)
            else:
                rt.meshop.deleteIsoVerts(node.baseObject)
            rt.notifyDependents(node)
            if skin is not None:
                # Skin 关闭时不会立即接收底层拓扑变化；临时启用并强制求值，
                # 让其内部顶点表先压缩，再按旧索引重写保留下来的权重。
                skin.enabled = True
                skin.enabledInViews = True
                rt.notifyDependents(node)
                rt.snapshotAsMesh(node)
                rt.modPanel.setCurrentObject(skin, node=node)
                if int(rt.skinOps.GetNumberVertices(skin)) != len(used):
                    raise CollapseError('清理后 Skin 顶点数不一致。')
                for new, old in enumerate(used, 1):
                    # Temporarily unlock rigid mode. Native ReplaceVertexWeights
                    # needs effective influences; restore modes and evaluate below.
                    rt.skinOps.rigidVertex(skin, new, False)
                    rt.skinOps.unNormalizeVertex(skin, new, weights[6][old - 1])
                    influences = [(b, w) for b, w in weights[4][old - 1] if w > 0.0]
                    if influences:
                        rt.skinOps.replaceVertexWeights(skin, new, rt.Array(*(b for b, w in influences)),
                                                       rt.Array(*(w for b, w in influences)))
                    rt.skinOps.setVertexDQWeight(skin, new, weights[5][old - 1])
                    rt.skinOps.rigidVertex(skin, new, weights[7][old - 1])
                # Resolve native rigid-mode weights before disabling evaluation;
                # Skin keeps pending values until the next mesh evaluation.
                rt.notifyDependents(node)
                rt.snapshotAsMesh(node)
                skin.enabled = False
                rt.notifyDependents(node)
            with pymxs.undo(False):
                after = collapse_fingerprint(node)
                remap = {old: new for new, old in enumerate(used, 1)}
                expected = dict(before)
                expected['vertices'] = tuple(before['vertices'][old - 1] for old in used)
                expected['faces'] = tuple(tuple(float(remap[int(v)]) for v in f) for f in before['faces'])
                verify_collapse(expected, after)
            if skin is not None:
                skin.enabled = skin_enabled
                skin.enabledInViews = skin_enabled_in_views
                rt.notifyDependents(node)
                actual = skin_fingerprint(node, skin)
                if actual[:4] != weights[:4]:
                    raise CollapseError('清理改变了 Skin 骨骼或开关。')
                for new, old in enumerate(used):
                    original = {b: w for b, w in weights[4][old - 1] if w > 1e-6}
                    current = {b: w for b, w in actual[4][new] if w > 1e-6}
                    if original.keys() != current.keys() or any(abs(w - current[b]) > 1e-6 for b, w in original.items()):
                        raise CollapseError('清理后 Skin 权重映射不一致：旧点 %s -> 新点 %s，%s -> %s，非归一化 %s，刚性 %s。' % (old, new + 1, original, current, actual[6][new], actual[7][new]))
                    if abs(actual[5][new] - weights[5][old - 1]) > 1e-6:
                        raise CollapseError('清理后 Skin DQ 权重映射不一致。')
                    if actual[6][new] != weights[6][old - 1] or actual[7][new] != weights[7][old - 1]:
                        raise CollapseError('清理后 Skin 顶点属性映射不一致。')
                # The old context stays detached and intact in Max's native Undo.
                # Reattach the SAME Skin with a new context copied from the cleaned
                # worker, then remove the worker. Undo restores the untouched old
                # context after topology restoration; no callbacks or repair queue.
                rt.addModifierWithLocalData(node, original_skin, node, skin, before=skin_index)
                rt.deleteModifier(node, skin)
                skin = original_skin
                if list(node.modifiers) != [original_skin] or skin_fingerprint(node, skin) != actual:
                    raise CollapseError('清理后原 Skin 或 LocalData 未正确保留。')
        except Exception as error:
            if isinstance(error, CollapseError):
                raise
            raise CollapseError('清理孤立点失败：' + str(error)) from error
        finally:
            if skin is not None and skin in list(node.modifiers):
                skin.enabled = skin_enabled
                skin.enabledInViews = skin_enabled_in_views
                rt.notifyDependents(node)
    return '已清理 %s 个孤立点：%s → %s 顶点；面、UV 和法线保留%s。' % (
        removed, count, len(used), '，Skin 权重已按保留顶点重排并验证' if had_skin else '')



def inspect_geometry(node):
    """Count input-mesh issues without changing topology, UVs or Skin state."""
    if rt.superClassOf(node) != rt.GeometryClass:
        raise ValueError('请选择网格对象。')
    with preserve_modifier_panel(), pymxs.undo(False):
        skins = [(m, bool(m.enabled)) for m in node.modifiers if str(rt.classOf(m)) == 'Skin']
        try:
            for skin, _ in skins:
                skin.enabled = False
            mesh = node.mesh
            vertices = [point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)]
            faces = [tuple(int(v) - 1 for v in point(rt.getFace(mesh, i)))
                     for i in range(1, int(rt.getNumFaces(mesh)) + 1)]
            used = {v for face in faces for v in face}
            return {'vertices': len(vertices), 'triangles': len(faces),
                    'isolated': len(vertices) - len(used),
                    'degenerate': sum(is_degenerate_triangle(vertices, face) for face in faces)}
        finally:
            for skin, enabled in skins:
                skin.enabled = enabled


def clean_degenerate_faces(node):
    """Delete geometric zero-area triangles; retain valid faces, channels and Skin.

    UV-degenerate triangles with nonzero geometry are intentionally untouched.
    Edits and optional orphan remapping share one native Undo transaction.
    """
    if rt.superClassOf(node) != rt.GeometryClass:
        raise ValueError('请选择网格对象。')
    modifiers = list(node.modifiers)
    skins = [m for m in modifiers if str(rt.classOf(m)) == 'Skin']
    if len(skins) > 1 or any(not is_tool_modifier(m) and m not in skins for m in modifiers):
        raise ValueError('存在其它修改器或多个 Skin，请先整理修改器栈；未删除任何面。')
    if len(list(rt.refs.dependentNodes(node.baseObject))) > 1:
        raise ValueError('底层网格被多个对象引用，未清理，避免改变其它实例。')
    skin = skins[0] if skins else None
    with preserve_modifier_panel(), undo_transaction('TL Clean Degenerate Faces'):
        enabled = bool(skin.enabled) if skin is not None else None
        try:
            if skin is not None:
                skin.enabled = False
                rt.notifyDependents(node)
            mesh = node.mesh
            vertices = [point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)]
            faces = [tuple(int(v) - 1 for v in point(rt.getFace(mesh, i)))
                     for i in range(1, int(rt.getNumFaces(mesh)) + 1)]
            bad = [i for i, face in enumerate(faces) if is_degenerate_triangle(vertices, face)]
            if not bad:
                return '没有几何退化面，无需清理。'
            if len(bad) == len(faces):
                raise ValueError('所有三角面均退化，保留原对象，请手动检查。')
            collapse_tool_modifiers(node)
            # Polygon triangulation is explicit and remains below Skin. It makes
            # deletion IDs match the baker's evaluated triangles, including n-gons.
            if str(rt.classOf(node.baseObject)).lower() == 'editable_poly':
                tri = rt.Turn_to_Mesh()
                tri.name = 'TL Outline Cleanup Triangulate'
                rt.addModifier(node, tri, before=len(list(node.modifiers)))
                collapse_tool_modifiers(node)
            base_class = str(rt.classOf(node.baseObject)).lower()
            if base_class not in ('editable_mesh', 'editable_poly'):
                raise CollapseError('底层对象不是可编辑网格或多边形，本次清理已撤销。')
            with pymxs.undo(False):
                before = collapse_fingerprint(node)
                vertices = before['vertices']
                faces = [tuple(int(v) - 1 for v in face) for face in before['faces']]
                bad = [i for i, face in enumerate(faces) if is_degenerate_triangle(vertices, face)]
                bad_set = set(bad)
                kept = [i for i in range(len(faces)) if i not in bad_set]
            weights = skin_fingerprint(node, skin) if skin is not None else None
            # For a Poly retained by CollapseNodeTo, verify all cage faces are
            # actual triangles before translating snapshot IDs to cage IDs.
            if base_class == 'editable_poly':
                if int(rt.polyop.getNumFaces(node.baseObject)) != len(faces):
                    raise CollapseError('多边形三角化未完成，本次清理已撤销。')
                cage = {}
                for i in range(1, len(faces) + 1):
                    ids = tuple(int(v) - 1 for v in rt.polyop.getFaceVerts(node.baseObject, i))
                    if len(ids) != 3:
                        raise CollapseError('存在未三角化多边形，本次清理已撤销。')
                    cage.setdefault(tuple(sorted(ids)), []).append(i)
                to_delete = sorted({i for fi in bad for i in cage[tuple(sorted(faces[fi]))]})
            else:
                to_delete = [i + 1 for i in bad]
            bits = rt.execute('#{' + ','.join(str(i) for i in to_delete) + '}')
            rt.subObjectLevel = 0
            if base_class == 'editable_poly':
                rt.polyop.deleteFaces(node.baseObject, bits, delIsoVerts=False)
                rt.polyop.collapseDeadStructs(node.baseObject)
            else:
                rt.meshop.deleteFaces(node.baseObject, bits, delIsoVerts=False)
                rt.update(node)
            rt.notifyDependents(node)
            with pymxs.undo(False):
                expected = dict(before)
                for key in ('faces', 'materials', 'smoothing', 'normals'):
                    expected[key] = tuple(before[key][i] for i in kept)
                expected['channels'] = {c: tuple(v for i in kept for v in values[i * 3:i * 3 + 3])
                                        for c, values in before['channels'].items()}
                verify_collapse(expected, collapse_fingerprint(node))
            if skin is not None and skin_fingerprint(node, skin) != weights:
                raise CollapseError('删除退化面改变了 Skin 权重，本次清理已撤销。')
            # Deleting a disconnected zero-area face may orphan its vertices.
            # Use the already validated native LocalData remapping rather than
            # asking DeleteFaces to renumber Skin input vertices implicitly.
            isolated = clean_isolated_vertices(node)
            return '已删除 %s 个几何退化面，保留 %s 个有效三角面；%s' % (len(bad), len(kept), isolated)
        except Exception as error:
            if isinstance(error, CollapseError):
                raise
            raise CollapseError('退化面清理验证失败，本次处理已撤销：' + str(error)) from error
        finally:
            if skin is not None and skin in list(node.modifiers):
                skin.enabled = enabled
                rt.notifyDependents(node)


def bake_node(node, channel=3, overwrite=False, clear_uv2=False):
    """Skip unchanged bakes and roll back all newly added modifiers on failure."""
    with preserve_modifier_panel():
        return _bake_preserving_skin(node, channel, overwrite, clear_uv2)


def _bake_preserving_skin(node, channel, overwrite, clear_uv2):
    if not 2 <= channel <= 8:
        raise ValueError('法线 UV 通道必须为 2~8。')
    if clear_uv2 and channel == 2:
        raise ValueError('角色模式会清空第二套 UV，描边法线请选择第三套或更高通道。')
    original = list(node.modifiers)
    clears, skin_states = [], []
    try:
        clears = disable_conflicting_uv_clears(node, range(2, channel + 1))
        # Read Skin's input pose; restore the user's enabled state after writing.
        for modifier in original:
            if str(rt.classOf(modifier)) == 'Skin':
                skin_states.append((modifier, bool(modifier.enabled)))
                modifier.enabled = False
        mesh, _, _ = read_mesh(node)
        if map_has_data(mesh, channel) and not is_outline_channel(mesh, channel, include_experimental=True) and not overwrite:
            raise ValueError('第 %s 套 UV 已有数据；请选择空闲通道，或勾选允许覆盖目标通道。' % channel)
        triangulate = None
        if not has_pinned_render_normals(node):
            triangulate = rt.Turn_to_Mesh()
            rt.addModifier(node, triangulate)
            triangulate.name = 'TL Outline UV%s Triangulate' % channel
        result = _bake_node(node, channel, overwrite, clear_uv2)
        expected = uv_corner_values(rt.snapshotAsMesh(node), channel)
        if result.startswith('数据未变化') and triangulate is not None:
            rt.deleteModifier(node, triangulate)
        skin = next((m for m in node.modifiers if str(rt.classOf(m)) == 'Skin'), None)
        if skin is not None:
            generated = [m for m in node.modifiers if m not in original]
            for m in reversed(generated):
                rt.deleteModifier(node,m)
                index = list(node.modifiers).index(skin) + 1
                rt.addModifier(node,m,before=index)
        rt.showEndResult = True
        rt.notifyDependents(node)
        for modifier, enabled in skin_states:
            modifier.enabled = enabled
        verify_uv_channel(node, channel, expected=expected)
        if clear_uv2:
            verify_uv_channel(node, 2, zero=True)
        evaluated = rt.snapshotAsMesh(node)
        for intermediate in range(2, channel):
            if not map_has_data(evaluated, intermediate):
                raise RuntimeError('第 %s 套中间 UV 被后续修改器清除。' % intermediate)
        if clears:
            result = BakeResult(result + ' 已停用 %s 个冲突的 UVW 贴图清除修改器，保留修改器供撤销。' % len(clears),
                                skipped_faces=getattr(result, 'skipped_faces', 0))
        if any(modifier not in list(node.modifiers) for modifier, _ in skin_states):
            raise RuntimeError('Skin 修改器未保留，烘焙失败。')
        return result
    except Exception:
        for m in list(node.modifiers):
            if m not in original:
                rt.deleteModifier(node, m)
        for modifier, enabled in clears:
            modifier.enabled = enabled
        raise
    finally:
        for modifier, enabled in skin_states:
            modifier.enabled = enabled


def _bake_node(node, channel=3, overwrite=False, clear_uv2=False):
    """写入可选 UV 通道，默认第三套；只给缺失的中间通道添加最小占位。"""
    if not 2 <= channel <= 8:
        raise ValueError('法线 UV 通道必须为 2~8。')
    mesh, vertices, faces = read_mesh(node)
    if not rt.meshop.getMapSupport(mesh, 1) or not rt.meshop.getNumMapVerts(mesh, 1):
        raise ValueError('缺少第一套贴图 UV。')
    if map_has_data(mesh, channel) and not is_outline_channel(mesh, channel, include_experimental=True) and not overwrite:
        raise ValueError('第 %s 套 UV 已有数据；请选择空闲通道，或勾选允许覆盖目标通道。' % channel)
    original = list(node.modifiers)
    local, frames, render_normals = local_bake_data(node, mesh, vertices, faces, skip_degenerate=True)
    normals, skipped = smooth_normals(local, faces, skip_unused=True)
    isolated = len(vertices) - len({v for face in faces for v in face})
    # 硬边/UV 接缝的各面角有独立切线帧，不能按几何顶点共用一份切线空间值。
    # 同时变换平滑方向和 TBN 后，切线空间分量不依赖 Max/FBX 的坐标轴转换。
    encoded = []
    map_faces = []
    lookup = {}
    for fi, face in enumerate(faces):
        ids = []
        for corner, vi in enumerate(face):
            frame = frames[fi * 3 + corner]
            uv = oct_encode((0., 0., 1.) if frame is None else to_tangent(normals[vi], frame))
            # 1e-5 UV quantization removes harmless float noise, <0.01 degree error.
            uv = tuple(round(x, 5) for x in uv)
            if uv not in lookup:
                lookup[uv] = len(encoded) + 1
                encoded.append(uv)
            ids.append(lookup[uv])
        map_faces.append(ids)
    source = None
    if (has_pinned_render_normals(node)
            and is_outline_channel(mesh, channel)
            and all(map_has_data(mesh, c) for c in range(2, channel))
            and (not clear_uv2 or (int(rt.meshop.getNumMapVerts(mesh, 2)) == 1
                                  and point(rt.meshop.getMapVert(mesh, 2, 1)) == (0., 0., 0.)))):
        unchanged = True
        for fi, ids in enumerate(map_faces, 1):
            current = point(rt.meshop.getMapFace(mesh, channel, fi))
            for ci, target in enumerate(ids):
                value = point(rt.meshop.getMapVert(mesh, channel, int(current[ci])))
                if dot(oct_decode(value), oct_decode(encoded[target-1])) < .99999999:
                    unchanged = False
                    break
            if not unchanged:
                break
        if unchanged:
            warning = (' 已忽略 %s 个几何退化面（面保留）。' % skipped if skipped else '') + (' 已跳过 %s 个孤立点（顶点保留）。' % isolated if isolated else '')
            return BakeResult('数据未变化：第 %s 套法线 UV 已正确烘焙，未重复添加修改器。%s' % (channel, warning),
                              skipped_faces=skipped, skipped_vertices=isolated)
    try:
        # setNumMaps 会在快照里初始化中间通道；必须在扩展前判断原模型是否缺失。
        placeholders = [c for c in range(2, channel) if not map_has_data(mesh, c)]
        if clear_uv2 and 2 not in placeholders:
            placeholders.insert(0, 2)
        rt.meshop.setNumMaps(mesh, max(channel + 1, int(rt.meshop.getNumMaps(mesh))), keep=True)
        for intermediate in placeholders:
            rt.meshop.setMapSupport(mesh, intermediate, True)
            rt.meshop.setNumMapVerts(mesh, intermediate, 1)
            rt.meshop.setMapVert(mesh, intermediate, 1, rt.Point3(0, 0, 0))
            for fi in range(1, len(faces) + 1):
                rt.meshop.setMapFace(mesh, intermediate, fi, rt.Point3(1, 1, 1))
        rt.meshop.setMapSupport(mesh, channel, True)
        rt.meshop.setNumMapVerts(mesh, channel, len(encoded))
        for i, uv in enumerate(encoded, 1):
            rt.meshop.setMapVert(mesh, channel, i, rt.Point3(uv[0], uv[1], 0))
        for i, ids in enumerate(map_faces, 1):
            rt.meshop.setMapFace(mesh, channel, i, rt.Point3(*ids))
        source = rt.mesh(mesh=mesh)
        for target in placeholders + [channel]:
            rt.ChannelInfo.CopyChannel(source, 3, target)
            rt.ChannelInfo.PasteChannel(node, 3, target)
            node.modifiers[0].name = 'TL Outline UV%s %s' % (target, 'Smooth Normals' if target == channel else 'Placeholder')
        freeze_render_normals(node, render_normals, faces)
        verify = rt.snapshotAsMesh(node)
        if int(rt.meshop.getNumMapVerts(verify, channel)) != len(encoded):
            raise RuntimeError('第 %s 套 UV 写入失败。' % channel)
        warning = (' 注意：当前 Unity 描边 Shader 固定读取第三套 UV，本次所选通道不会被它使用。'
                   if channel != 3 else '')
        return BakeResult('第 %s 套 UV 已写入切线空间平滑法线：%s 几何顶点 / %s 三角面%s；%s 个法线 UV 点；补齐 %s 个中间占位通道。%s' % (
            channel, len(vertices), len(faces), ('，忽略 %s 个几何退化面' % skipped if skipped else '') + ('，跳过 %s 个孤立点（顶点保留）' % isolated if isolated else ''), len(encoded), len(placeholders), warning),
            skipped_faces=skipped, skipped_vertices=isolated)
    except Exception:
        for modifier in list(node.modifiers):
            if modifier not in original:
                rt.deleteModifier(node, modifier)
        raise
    finally:
        if source is not None and rt.isValidNode(source):
            rt.delete(source)


def clear_second_uv(node):
    """Clear UV2 to one zero placeholder, preserving UV3's FBX channel index."""
    with preserve_modifier_panel():
        return _clear_second_uv_preserving_skin(node)


def _clear_second_uv_preserving_skin(node):
    if rt.superClassOf(node) != rt.GeometryClass:
        raise ValueError('请选择网格对象。')
    original, selected = list(node.modifiers), list(rt.selection)
    clears, skin_states = [], []
    source = None
    try:
        clears = disable_conflicting_uv_clears(node, [2])
        for modifier in original:
            if str(rt.classOf(modifier)) == 'Skin':
                skin_states.append((modifier, bool(modifier.enabled)))
                modifier.enabled = False
        mesh = rt.snapshotAsMesh(node)
        count = int(rt.getNumFaces(mesh))
        if not count:
            raise ValueError('网格没有有效三角面。')
        if (map_has_data(mesh, 2) and int(rt.meshop.getNumMapVerts(mesh, 2)) == 1
                and point(rt.meshop.getMapVert(mesh, 2, 1)) == (0.0, 0.0, 0.0)):
            for modifier, enabled in skin_states:
                modifier.enabled = enabled
            verify_uv_channel(node, 2, zero=True)
            return '第二套 UV 已是全零占位，无需清空。' + (' 已停用冲突的 UVW 贴图清除。' if clears else '')
        triangulate = rt.Turn_to_Mesh()
        rt.addModifier(node, triangulate)
        triangulate.name = 'TL Clear UV2 Triangulate'
        mesh = rt.snapshotAsMesh(node)
        count = int(rt.getNumFaces(mesh))
        normals = read_render_normals(node, count)
        rt.meshop.setNumMaps(mesh, max(3, int(rt.meshop.getNumMaps(mesh))), keep=True)
        rt.meshop.setMapSupport(mesh, 2, True)
        rt.meshop.setNumMapVerts(mesh, 2, 1)
        rt.meshop.setMapVert(mesh, 2, 1, rt.Point3(0, 0, 0))
        for fi in range(1, count + 1):
            rt.meshop.setMapFace(mesh, 2, fi, rt.Point3(1, 1, 1))
        source = rt.mesh(mesh=mesh)
        rt.ChannelInfo.CopyChannel(source, 3, 2)
        rt.ChannelInfo.PasteChannel(node, 3, 2)
        node.modifiers[0].name = 'TL Clear UV2 Placeholder'
        freeze_render_normals(node, normals).name = 'TL Clear UV2 Render Normals'
        skin = next((m for m in node.modifiers if str(rt.classOf(m)) == 'Skin'), None)
        if skin is not None:
            for modifier in reversed([m for m in node.modifiers if m not in original]):
                rt.deleteModifier(node, modifier)
                rt.addModifier(node, modifier, before=list(node.modifiers).index(skin) + 1)
        rt.showEndResult = True
        rt.notifyDependents(node)
        for modifier, enabled in skin_states:
            modifier.enabled = enabled
        verify_uv_channel(node, 2, zero=True)
        return '第二套 UV 已清空，保留全零占位；其它 UV 和普通法线保留。' + (' 已停用冲突的 UVW 贴图清除。' if clears else '')
    except Exception:
        for modifier in list(node.modifiers):
            if modifier not in original:
                rt.deleteModifier(node, modifier)
        for modifier, enabled in clears:
            modifier.enabled = enabled
        raise
    finally:
        for modifier, enabled in skin_states:
            modifier.enabled = enabled
        if source is not None and rt.isValidNode(source):
            rt.delete(source)
        if selected:
            rt.select(rt.Array(*selected))
        else:
            rt.clearSelection()


def open_channel_info():
    """Open Max's native Channel Info window for the current selection."""
    rt.ChannelInfo.Dialog()
    rt.ChannelInfo.Update()


def preview_rows(nodes, mode=0, units_per_meter=1.0, channel=3):
    """Read full-stack snapshots without changing selection, panel or Undo."""
    show_end = bool(rt.showEndResult)
    try:
        with preserve_modifier_panel(), pymxs.undo(False):
            rt.showEndResult = True
            return _preview_rows(nodes, mode, units_per_meter, channel)
    finally:
        rt.showEndResult = show_end


def _preview_rows(nodes, mode=0, units_per_meter=1.0, channel=3):
    """预览导出时的平滑法线，保留原始法线、逐面 RGB 和 Alpha，不添加 UV。"""
    rows, messages = [], []
    for node in nodes:
        if not rt.isValidNode(node):
            continue
        rt.showEndResult = True
        mesh, vertices, faces = read_mesh(node)
        # Alpha and original-normal display do not need texture UV or Mikk.
        marked_channel = channel if mode == 0 and is_outline_channel(mesh, channel) else None
        marked = marked_channel is not None
        if marked:
            local, frames, render_normals = local_bake_data(node, mesh, vertices, faces, skip_degenerate=True)
        else:
            local_mesh = node.mesh
            local = [point(rt.getVert(local_mesh, i)) for i in range(1, len(vertices) + 1)]
            render_normals = read_render_normals(node, len(faces))
            frames = None
        transform_rows = [point(node.objectTransform.row1), point(node.objectTransform.row2), point(node.objectTransform.row3)]
        to_world = lambda n: unit(tuple(sum(n[j] * transform_rows[j][i] for j in range(3)) for i in range(3)))
        smooth = [to_world(n) for n in smooth_normals(local, faces, skip_unused=True)[0]] if mode == 1 else None
        has_alpha = rt.meshop.getMapSupport(mesh, -2) and int(rt.meshop.getNumMapVerts(mesh, -2)) > 0
        has_color = rt.meshop.getMapSupport(mesh, 0) and int(rt.meshop.getNumMapVerts(mesh, 0)) > 0
        for fi, face in enumerate(faces, 1):
            surface = [to_world(n) for n in render_normals[fi-1]]
            alpha_face = point(rt.meshop.getMapFace(mesh, -2, fi)) if has_alpha else None
            color_face = point(rt.meshop.getMapFace(mesh, 0, fi)) if has_color else None
            uv_face = point(rt.meshop.getMapFace(mesh, marked_channel, fi)) if marked else None
            for corner, vi in enumerate(face):
                normal = surface[corner]
                outline = smooth[vi] if smooth else normal
                if marked and frames[(fi - 1) * 3 + corner] is not None:
                    uv = point(rt.meshop.getMapVert(mesh, marked_channel, int(uv_face[corner])))
                    outline = to_world(from_tangent(oct_decode(uv), frames[(fi - 1) * 3 + corner]))
                alpha = point(rt.meshop.getMapVert(mesh, -2, int(alpha_face[corner])))[0] if has_alpha else 1.0
                rgb = point(rt.meshop.getMapVert(mesh, 0, int(color_face[corner]))) if has_color else (1.0, 1.0, 1.0)
                def rotate(v):
                    return (v[0], v[2], -v[1])
                rows.append(rotate(mul(vertices[vi], 1.0 / units_per_meter)) + rotate(normal) + rotate(outline) + (alpha,) + rgb)
        source = '已烘焙第 %s 套 UV' % marked_channel if marked else ('实时平滑' if smooth else '原始法线')
        messages.append('%s：%s，%s 三角面%s' % (
            node.name, source, len(faces), '' if has_alpha else '（无顶点 Alpha，按 1 预览）'))
    return rows, messages


def show():
    global _dialog
    try:
        from PySide6 import QtWidgets, QtCore
    except ImportError:
        from PySide2 import QtWidgets, QtCore
    import qtmax

    # Launch.ms uses a fresh runpy dictionary; close previous Qt windows explicitly.
    for widget in QtWidgets.QApplication.topLevelWidgets():
        if widget.windowTitle() == 'TL 描边 · 平滑法线写入 UV':
            widget.close()

    class OutlineDialog(QtWidgets.QDialog):
        def __init__(self):
            super().__init__(qtmax.GetQMaxMainWindow())
            self.setWindowTitle('TL 描边 · 平滑法线写入 UV')
            self.setMinimumWidth(560)
            self.setAttribute(QtCore.Qt.WA_DeleteOnClose)
            layout = QtWidgets.QVBoxLayout(self)
            layout.setSpacing(7)
            help_text = QtWidgets.QLabel(
                '默认写第三套 UV（U+2）。孤立点和几何退化面自动跳过，保留原几何。\n'
                'Unity：Normals Import / Tangents Calculate Mikktspace，关闭 Bake Axis Conversion。')
            help_text.setWordWrap(True)
            layout.addWidget(help_text)
            form = QtWidgets.QFormLayout()
            self.channel = QtWidgets.QSpinBox()
            self.channel.setRange(2, 8)
            self.channel.setValue(3)
            form.addRow('法线 UV（第几套，Max Channel）：', self.channel)
            layout.addLayout(form)
            self.overwrite = QtWidgets.QCheckBox('允许覆盖目标 UV 已有数据（其它通道保留）')
            layout.addWidget(self.overwrite)
            self.auto_collapse = QtWidgets.QCheckBox('处理后塌陷工具修改器（保留 Skin）')
            self.auto_collapse.setChecked(True)
            layout.addWidget(self.auto_collapse)
            modes = QtWidgets.QHBoxLayout()
            self.character_mode = QtWidgets.QCheckBox('角色：第二套 UV 设为最小')
            self.scene_mode = QtWidgets.QCheckBox('场景：保留已有第二套 UV')
            self.character_mode.setToolTip('清空第二套 UV 为一个全零占位，描边法线使用第三套或更高通道。')
            self.scene_mode.setToolTip('第二套有数据就保留，没有才补最小全零占位；都不选时也是默认保留策略。')
            self.character_mode.toggled.connect(self.select_character_mode)
            self.scene_mode.toggled.connect(self.select_scene_mode)
            modes.addWidget(self.character_mode, 1)
            modes.addWidget(self.scene_mode, 1)
            layout.addLayout(modes)
            self.update_mode_colors()

            self.bake_button = QtWidgets.QPushButton('烘焙选中模型')
            self.bake_button.setMinimumHeight(38)
            self.bake_button.setStyleSheet('QPushButton { color: #d8edff; background-color: #285780; border: 1px solid #4383b5; border-radius: 3px; padding: 6px; font-weight: bold; } QPushButton:hover { background-color: #326a99; }')
            self.bake_button.clicked.connect(self.bake)
            layout.addWidget(self.bake_button)
            actions = QtWidgets.QHBoxLayout()
            self.channels_button = QtWidgets.QPushButton('查看通道')
            self.channels_button.setToolTip('打开 Max 通道信息，查看 UV、顶点色 RGB 和 Alpha。')
            self.channels_button.clicked.connect(self.open_channels)
            actions.addWidget(self.channels_button, 1)
            self.preview_button = QtWidgets.QPushButton('预览描边 / 顶点色 A')
            self.preview_button.setToolTip('预览描边效果；窗口内可切换顶点色 A 灰度。')
            self.preview_button.clicked.connect(self.open_preview)
            actions.addWidget(self.preview_button, 1)
            layout.addLayout(actions)
            self.status = QtWidgets.QLabel('', self)
            self.status.hide()
            self.log = QtWidgets.QTextEdit()
            self.log.setReadOnly(True)
            self.log.setMinimumHeight(180)
            self.log.setStyleSheet('QTextEdit { background: #1d2025; border: 1px solid #4b5058; padding: 4px; }')
            self.log_sequence = 0
            self.log_start = 0
            log_actions = QtWidgets.QHBoxLayout()
            log_actions.addWidget(QtWidgets.QLabel('日志'))
            log_actions.addStretch(1)
            self.clear_log_button = QtWidgets.QPushButton('清空日志')
            self.clear_log_button.setToolTip('只清空日志记录，不修改模型、UV 或撤销记录。')
            self.clear_log_button.clicked.connect(self.clear_log)
            log_actions.addWidget(self.clear_log_button)
            layout.addLayout(log_actions)
            layout.addWidget(self.log, 1)
            self.preview = None
            layout.addWidget(QtWidgets.QLabel('可 Ctrl+Z 撤销。修改拓扑、UV 或变换后请重新烘焙。'))

        def clear_log(self):
            self.log.clear()
            self.status.clear()
            self.log_sequence = 0
            self.log_start = 0

        @staticmethod
        def log_html(message):
            return html.escape(str(message)).replace('\n', '<br>')

        def begin_log(self, action, nodes):
            self.log_sequence += 1
            self.log_start = self.log.document().characterCount() - 1
            self.log.append('<table width="100%%" cellspacing="0" cellpadding="7" bgcolor="#363636">'
                            '<tr><td><font color="#eeeeee"><b>#%02d · %s</b></font></td>'
                            '<td align="right"><font color="#cccccc">%s · %s 个模型</font></td></tr></table>'
                            % (self.log_sequence, self.log_html(action), datetime.now().strftime('%H:%M:%S'), len(nodes)))
            header = self.log.document().find('#%02d ·' % self.log_sequence, self.log_start)
            if not header.isNull():
                self.log_start = header.selectionStart()

        def write_log(self, message, problem=False, title=None, level=None):
            level = level or ('error' if problem else 'success')
            color, background, label = {
                'success': ('#63d58a', '#282828', '成功'),
                'warning': ('#ff5b5b', '#282828', '需处理'),
                'error': ('#ff5b5b', '#282828', '失败'),
                'info': ('#63d58a', '#282828', '提示'),
            }[level]
            self.log.append('<table width="100%%" cellspacing="0" cellpadding="7" bgcolor="%s">'
                            '<tr><td><font color="%s"><b>[%s] %s</b></font></td></tr>'
                            '<tr><td><font color="%s">%s</font></td></tr></table>'
                            % (background, color, label, self.log_html(title or '操作提示'), color, self.log_html(message)))

        def set_result(self, message, problem=False, level=None):
            self.status.setText(message)
            self.status.setStyleSheet('color: #dedede; background: #303030; border: 1px solid #555555; font-size: 14px; font-weight: bold; padding: 8px;')
            self.write_log(message, problem, title='本次汇总', level=level)
            cursor = self.log.textCursor()
            cursor.setPosition(min(self.log_start, self.log.document().characterCount() - 1))
            self.log.setTextCursor(cursor)
            self.log.ensureCursorVisible()
            scrollbar = self.log.verticalScrollBar()
            scrollbar.setValue(scrollbar.value() + max(0, self.log.cursorRect().top() - 8))

        def update_mode_colors(self):
            for button in (self.character_mode, self.scene_mode):
                checked = button.isChecked()
                button.setStyleSheet('QCheckBox { color: #dedede; background: %s; border: 1px solid %s; padding: 9px; font-weight: bold; }'
                                     % ('#454545' if checked else '#303030', '#aaaaaa' if checked else '#555555'))

        def select_character_mode(self, checked):
            if checked:
                self.scene_mode.setChecked(False)
            self.channel.setMinimum(3 if checked else 2)
            self.update_mode_colors()

        def select_scene_mode(self, checked):
            if checked:
                self.character_mode.setChecked(False)

            self.update_mode_colors()

        def open_channels(self):
            try:
                open_channel_info()
            except Exception as error:
                self.set_result('打开通道信息失败：' + str(error), True)
                print(traceback.format_exc())

        def open_preview(self):
            try:
                if self.preview is None:
                    import importlib
                    import outline_preview
                    OutlinePreview = importlib.reload(outline_preview).OutlinePreview
                    units_per_meter = float(rt.units.decodeValue('1m'))
                    material_path = os.path.join(ROOT, 'Unity', 'TL_RenderFeature_Outline.mat')
                    self.preview = OutlinePreview(
                        self, lambda nodes, mode, units: preview_rows(nodes, mode, units, self.channel.value()),
                        material_path, units_per_meter)
                self.preview.show()
                self.preview.raise_()
                self.preview.load_selection()
            except Exception as error:
                self.set_result('打开效果预览失败：' + str(error), True)
                print(traceback.format_exc())

        def closeEvent(self, event):
            if self.preview is not None:
                self.preview.close()
            super().closeEvent(event)

        def bake(self):
            self.run_batch('烘焙', 'TL Outline UV Bake',
                           lambda node: bake_node(node, self.channel.value(), self.overwrite.isChecked(),
                                                  clear_uv2=self.character_mode.isChecked()), collapse=True)

        def run_batch(self, action, undo_label, operation, collapse=False):
            nodes = list(rt.selection)
            self.begin_log(action, nodes)
            if not nodes:
                self.set_result('%s失败：请先选择要处理的模型。' % action, True)
                return
            # Skin 子对象编辑层级可能保留内部 Hold。按钮点击时先临时退出该
            # 层级；仅当 Hold 确实随之结束才继续，普通用户事务仍保持拒绝。
            with preserve_modifier_panel():
                if rt.theHold.Holding():
                    current = (rt.modPanel.getCurrentObject()
                               if str(rt.getCommandPanelTaskMode()).lower() == 'modify'
                               else None)
                    if (current is not None and str(rt.classOf(current)) == 'Skin'
                            and int(rt.subObjectLevel or 0) > 0):
                        rt.subObjectLevel = 0
                        # Max 的 Skin 编辑层级在部分场景会保留一个内部 Hold；
                        # 点击本工具按钮即表示结束该次 Skin 编辑，先提交它，
                        # 后续工具操作再建立独立且可完整撤销的事务。
                        if rt.theHold.Holding():
                            rt.theHold.Accept('TL Finish Skin Edit')
                if rt.theHold.Holding():
                    self.set_result('%s未执行：Max 当前有进行中的编辑操作，请结束该操作后重试。' % action, True)
                    return
                self.run_batch_idle(nodes, action, undo_label, operation, collapse)

        def run_batch_idle(self, nodes, action, undo_label, operation, collapse):
            success, skipped, failed, skipped_faces = 0, 0, 0, 0
            collapse_skipped, ignored_faces, isolated_vertices = 0, 0, 0
            try:
                with undo_transaction(undo_label):
                    for index, node in enumerate(nodes, 1):
                        title = "%s/%s · %s" % (index, len(nodes), node.name)
                        level = "success"
                        try:
                            message = operation(node)
                            face_skips = getattr(message, 'skipped_faces', 0)
                            isolated_vertices += getattr(message, 'skipped_vertices', 0)
                            accepted = action == '烘焙'
                            if accepted:
                                ignored_faces += face_skips
                            else:
                                skipped_faces += face_skips
                            problem = bool(face_skips) and not accepted
                            level = "warning" if problem else "success"
                            if collapse and self.auto_collapse.isChecked():
                                try:
                                    message = str(message) + ' ' + collapse_tool_modifiers(node)
                                except ValueError as error:
                                    collapse_skipped += 1
                                    level = "warning"
                                    message, problem = str(message) + ' 未塌陷：' + str(error), True
                            success += 1
                        except CollapseError as error:
                            self.write_log(str(error), True, title=title)
                            raise
                        except ValueError as error:
                            skipped += 1
                            message, problem = '跳过：' + str(error), True
                            level = 'warning'
                        except Exception as error:
                            failed += 1
                            source = traceback.extract_tb(error.__traceback__)[-1]
                            message, problem = '失败：%s：%s\n代码位置：%s (%s:%s)' % (type(error).__name__, error, source.name, os.path.basename(source.filename), source.lineno), True
                            level = 'error'
                            print(traceback.format_exc())
                        self.write_log(message, problem, title=title, level=level)
            except CollapseError as error:
                self.set_result(action + '验证失败，本次处理已全部撤销（上方成功记录也已撤销）：' + str(error), True)
                self.finish_processing(nodes)
                return
            problem = bool(skipped or failed or skipped_faces or collapse_skipped)
            summary = ('完成，存在问题：' if problem else '全部成功，完成：') + '%s 成功，%s 跳过，%s 失败。' % (
                success, skipped, failed)
            if skipped_faces:
                summary += ' 跳过 %s 个退化面，请检查模型。' % skipped_faces
            if isolated_vertices:
                summary += ' 已跳过 %s 个孤立点（顶点保留）。' % isolated_vertices
            if ignored_faces:
                summary += ' 已忽略 %s 个几何退化面提示（面保留）。' % ignored_faces
            if collapse_skipped:
                summary += ' %s 个模型保留修改器栈，详见日志。' % collapse_skipped
            self.set_result(action + '：' + summary, problem, level='error' if failed else ('warning' if problem else 'success'))
            self.finish_processing(nodes)

        def finish_processing(self, nodes):
            if self.preview is not None and self.preview.isVisible():
                self.preview.refresh()
            focus_selected_skin(nodes)


    if _dialog is not None:
        try:
            _dialog.close()
        except RuntimeError:
            pass
    _dialog = OutlineDialog()
    _dialog.show()
    return _dialog


if __name__ == '__main__':
    show()
