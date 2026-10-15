"""One-click bake with unused vertices and retained geometric-degenerate faces."""
import os, sys, traceback
from pathlib import Path
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1'
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
import tangent_space
from outline_math import smooth_normals, dot, oct_decode, oct_encode, is_degenerate_triangle, unit, sub, mul, cross
try:
    from PySide6 import QtWidgets
except ImportError:
    from PySide2 import QtWidgets
artifacts = root / 'tests' / 'artifacts'
out = artifacts / 'oneclick_bake_result.txt'
rows = ['STARTED']
out.write_text('\n'.join(rows), encoding='utf-8')
def passed(s):
    rows.append(s + ' PASS')
    out.write_text('\n'.join(rows), encoding='utf-8')
def idle():
    rt.subObjectLevel = 0
    if rt.theHold.Holding(): rt.theHold.Accept('Disposable setup')
    rt.clearUndoBuffer()
def geometry(node):
    mesh = node.mesh
    return {'vertices': tuple(tool.point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)),
            'faces': tuple(tool.point(rt.getFace(mesh, i)) for i in range(1, int(rt.getNumFaces(mesh)) + 1)),
            'uv1': tool.uv_corner_values(mesh, 1),
            'materials': tuple(int(rt.getFaceMatID(mesh, i)) for i in range(1, int(rt.getNumFaces(mesh)) + 1)),
            'smoothing': tuple(int(rt.getFaceSmoothGroup(mesh, i)) for i in range(1, int(rt.getNumFaces(mesh)) + 1))}
def fixture():
    box = rt.box(mapCoords=True)
    mesh = rt.snapshotAsMesh(box); rt.delete(box)
    rt.meshop.setNumVerts(mesh, 12)
    for i, p in enumerate(((100, 200, 300), (40, 0, 0), (41, 0, 0), (42, 0, 0)), 9):
        rt.setVert(mesh, i, rt.Point3(*p))
    rt.setNumFaces(mesh, 14, True)
    first = rt.getFace(mesh, 1)
    rt.setFace(mesh, 13, rt.Point3(first.x, first.x, first.y))
    rt.setFaceSmoothGroup(mesh, 13, rt.getFaceSmoothGroup(mesh, 1))
    rt.setFace(mesh, 14, rt.Point3(10, 11, 12))
    for channel in range(1, int(rt.meshop.getNumMaps(mesh))):
        if rt.meshop.getMapSupport(mesh, channel):
            rt.meshop.setNumMapFaces(mesh, channel, 14, keep=True)
            uv = rt.meshop.getMapFace(mesh, channel, 1)
            rt.meshop.setMapFace(mesh, channel, 13, rt.Point3(uv.x, uv.x, uv.y))
            rt.meshop.setMapFace(mesh, channel, 14, uv)
    node = rt.mesh(mesh=mesh); rt.update(node)
    return node
def native_full_frames(local, faces, normals, uv_faces, uv_vertices):
    # Read official Mikk output before per-corner validation. Unity still has
    # degenerate triangles, so valid corners must match full-mesh generation.
    import ctypes
    flip = lambda p: (-p[0], p[1], p[2])
    reverse = lambda f: (f[0], f[2], f[1])
    imported_faces = [reverse(f) for f in faces]
    imported_normals = [[flip(n) for n in reverse(face)] for face in normals]
    imported_uv = [reverse(f) for f in uv_faces]
    count = len(faces) * 3
    source = (ctypes.c_float * (count * 8))(); target = (ctypes.c_float * (count * 4))()
    for fi, face in enumerate(imported_faces):
        for ci, vi in enumerate(face):
            values = flip(local[vi]) + imported_normals[fi][ci] + uv_vertices[imported_uv[fi][ci]][:2]
            for j, value in enumerate(values): source[(fi * 3 + ci) * 8 + j] = value
    assert tangent_space._library.tl_mikk_generate(len(faces), source, target)
    result = []
    for fi in range(len(faces)):
        for ci in (0, 2, 1):
            i = fi * 3 + ci; n = imported_normals[fi][ci]
            t = unit(sub(tuple(target[i * 4 + j] for j in range(3)), mul(n, dot(n, tuple(target[i * 4 + j] for j in range(3))))))
            b = mul(cross(n, t), target[i * 4 + 3])
            result.append(tuple(flip(axis) for axis in (t, b, n)))
    return result
try:
    idle(); dialog = tool.show()
    buttons = dialog.findChildren(QtWidgets.QPushButton)
    assert sorted(b.text() for b in buttons) == sorted(['烘焙选中模型', '清空日志', '查看通道', '预览描边 / 顶点色 A']), [b.text() for b in buttons]
    passed('Bake action plus manual clear log; no check/geometry cleanup/ignore buttons')
    node = fixture(); node.name = '一键烘焙 · 孤立点和退化面'; rt.select(node); idle()
    before = geometry(node)
    mesh, vertices, faces = tool.read_mesh(node)
    local, frames, normals = tool.local_bake_data(node, mesh, vertices, faces, skip_degenerate=True)
    smooth, skips = smooth_normals(local, faces, skip_unused=True)
    assert skips == 2 and frames[36:] == [None] * 6 and smooth[8] == (0., 0., 0.)
    uv_faces = [tuple(int(v) - 1 for v in tool.point(rt.meshop.getMapFace(mesh, 1, fi))) for fi in range(1, len(faces) + 1)]
    uv_vertices = [tool.point(rt.meshop.getMapVert(mesh, 1, i)) for i in range(1, int(rt.meshop.getNumMapVerts(mesh, 1)) + 1)]
    full = native_full_frames(local, faces, normals, uv_faces, uv_vertices)
    for i in range(36):
        for a, b in zip(frames[i], full[i]): assert dot(a, b) > .999999, (i, a, b)
    passed('Effective corner frames match official full-mesh Mikk output, even with retained degenerate triangles')
    dialog.bake()
    assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
    assert '孤立点' in dialog.status.text() and '2 个几何退化面' in dialog.status.text(), dialog.status.text()
    assert geometry(node) == before
    baked = tool.uv_corner_values(node.mesh, 3)
    assert all(v == (2.5, .5, 0.) for v in baked[36:]), baked[36:]
    for fi, face in enumerate(faces[:12]):
        for ci, vi in enumerate(face):
            expected = oct_encode(tangent_space.to_tangent(smooth[vi], frames[fi * 3 + ci]))
            assert dot(oct_decode(expected), oct_decode(baked[fi * 3 + ci])) > .999999
    after = geometry(node)
    rt.execute('max undo'); assert geometry(node) == before and not tool.map_has_data(node.mesh, 3)
    rt.execute('max redo'); assert geometry(node) == after and tool.map_has_data(node.mesh, 3)
    dialog.bake(); assert '数据未变化' in dialog.log.toPlainText() and '全部成功' in dialog.status.text()
    passed('Mesh: 1 orphan + 2 bad faces retained, valid UV3 directions, degenerate original-normal encoding, Undo/Redo/rebake')

    node = rt.box(mapCoords=True); rt.convertToPoly(node)
    rt.polyop.setVert(node.baseObject, 2, rt.polyop.getVert(node.baseObject, 1)); rt.select(node); idle()
    before = geometry(node); dialog.bake()
    assert '全部成功' in dialog.status.text() and geometry(node) == before, dialog.log.toPlainText()
    passed('Poly partial geometric degeneracy bakes without cleaning or topology loss')

    node = fixture(); skin = rt.Skin(); rt.addModifier(node, skin); rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(skin, node=node)
    bone = rt.BoneSys.createBone(rt.Point3(0, 0, 0), rt.Point3(0, 0, 20), rt.Point3(0, 1, 0))
    rt.skinOps.addBone(skin, bone, 1)
    for i in range(1, 13):
        rt.skinOps.replaceVertexWeights(skin, i, rt.Array(1), rt.Array(1.))
        rt.skinOps.setVertexDQWeight(skin, i, i / 20.)
    rt.notifyDependents(node); skin.enabled = False
    extra = rt.XForm(); rt.addModifier(node, extra)
    before = geometry(node); weights = tool.skin_fingerprint(node, skin); handle = rt.getHandleByAnim(skin)
    idle(); dialog.bake()
    assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
    assert geometry(node) == before and tool.skin_fingerprint(node, skin) == weights
    assert rt.getHandleByAnim(skin) == handle and extra in list(node.modifiers)
    rt.execute('max undo'); assert geometry(node) == before and tool.skin_fingerprint(node, skin) == weights
    passed('Skin + user XForm: no cleanup requirement, same vertices/faces/Skin/weights/DQ/switches, native Undo')

    node = rt.box(mapCoords=True); rt.convertToMesh(node); rt.select(node); idle()
    node.scale = rt.Point3(1, 2, 1); before = geometry(node); dialog.bake()
    assert '非均匀缩放' in dialog.log.toPlainText() and geometry(node) == before
    node.scale = rt.Point3(1, 1, 1)
    rt.meshop.setMapSupport(node, 1, False); before_vertices = tuple(tool.point(rt.getVert(node.mesh, i)) for i in range(1, 9)); dialog.bake()
    assert '缺少第一套' in dialog.log.toPlainText() and not tool.map_has_data(node.mesh, 3)
    passed('Nonuniform transforms and missing UV still block baking without producing output')
    try: smooth_normals([(0, 0, 0), (1, 0, 0), (0, 1, 0), (10, 10, 10)], [(0, 1, 2), (0, 2, 1)], skip_unused=True)
    except ValueError: pass
    else: raise AssertionError('Opposite effective face cancellation was ignored')
    passed('Only unused/degenerate-only vertices skipped; cancellation on effective faces remains an error')

    rt.select(rt.box(mapCoords=True)); dialog.bake()
    dialog.resize(590, 600); QtWidgets.QApplication.processEvents()
    assert dialog.grab().save(str(artifacts / 'oneclick_bake_ui.png'))
    dialog.close(); rows.append('ALL ONECLICK BAKE TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    out.write_text('\n'.join(rows), encoding='utf-8')
