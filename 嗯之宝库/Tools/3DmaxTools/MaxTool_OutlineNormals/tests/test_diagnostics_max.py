"""Detailed failure reporting, clear log and static/Skin bake paths in disposable Max."""
import os, sys, traceback, copy, runpy
from pathlib import Path
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1'
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
import tangent_space
try:
    from PySide6 import QtWidgets
except ImportError:
    from PySide2 import QtWidgets
artifacts = root / 'tests/artifacts'
out = artifacts / 'diagnostics_result.txt'
rows = ['STARTED']; out.write_text('\n'.join(rows), encoding='utf-8')
def passed(message):
    rows.append(message + ' PASS'); out.write_text('\n'.join(rows), encoding='utf-8')
def idle():
    rt.subObjectLevel = 0
    if rt.theHold.Holding(): rt.theHold.Accept('Disposable setup')
    rt.clearUndoBuffer()
def fingerprint(node):
    with pymxs.undo(False): return tool.collapse_fingerprint(node)
def static_mesh(poly=False, extended=False):
    node = rt.box(mapCoords=True)
    if poly: rt.convertToPoly(node)
    else: rt.convertToMesh(node)
    if extended:
        rt.meshop.setNumMaps(node, 5, keep=True)
        for channel in (2, 4, 0, -2):
            rt.meshop.setMapSupport(node, channel, True)
            rt.meshop.setNumMapVerts(node, channel, 8)
            rt.meshop.setNumMapFaces(node, channel, 12, keep=False)
            for i in range(1, 9):
                value = (.123456789 + i / 17., .765432109 - i / 29., 0.) if channel > 0 else (.3 + i / 20., .5, .7)
                if channel == -2: value = (.2 + i / 16., 0., 0.)
                rt.meshop.setMapVert(node, channel, i, rt.Point3(*value))
            for fi in range(1, 13): rt.meshop.setMapFace(node, channel, fi, rt.getFace(node, fi))
        rt.meshop.setMapSupport(node, 3, False)
        rt.update(node)
    return node

try:
    idle(); dialog = tool.show()
    assert sorted(button.text() for button in dialog.findChildren(QtWidgets.QPushButton)) == sorted(['烘焙选中模型', '清空日志', '查看通道', '预览描边 / 顶点色 A'])
    sample = {'vertices': ((0.,0.,0.), (1.,0.,0.), (0.,1.,0.)), 'faces': ((1.,2.,3.),),
        'materials': (1,), 'smoothing': (0,), 'channels': {3: ((2.5,.5,0.),)*3}, 'normals': (((0.,0.,1.),)*3,)}
    altered = copy.deepcopy(sample); altered['channels'][3] = ((2.5,.5,0.), (2.51,.5,0.), (2.5,.5,0.))
    try: tool.verify_collapse(sample, altered)
    except tool.CollapseError as error:
        message = str(error)
        assert all(token in message for token in ('第 3 套 UV', '三角面 #1', '面角 2', '几何顶点 #2', '2.51', '最大差值')), message
    else: raise AssertionError('Real channel change was accepted')
    noise = copy.deepcopy(sample); noise['channels'][3] = ((2.5000004,.5,0.),)*3
    tool.verify_collapse(sample, noise)
    invalid = copy.deepcopy(sample); invalid['channels'][3] = ((float('nan'),.5,0.),)*3
    try: tool.verify_collapse(sample, invalid)
    except tool.CollapseError as error: assert 'NaN/Infinity' in str(error)
    else: raise AssertionError('NaN was accepted')
    lost = copy.deepcopy(sample); del lost['channels'][3]
    try: tool.verify_collapse(sample, lost)
    except tool.CollapseError as error: assert '第 3 套 UV' in str(error) and '丢失' in str(error)
    else: raise AssertionError('Channel loss accepted')
    color = copy.deepcopy(sample); color['channels'][0] = ((.5,.5,.5),)*3
    color_after = copy.deepcopy(color); color_after['channels'][0] = ((.6,.5,.5),)*3
    try: tool.verify_collapse(color, color_after)
    except tool.CollapseError as error: assert '顶点色 RGB' in str(error)
    passed('Channel/triangle/corner/geometry vertex/value diagnostics; genuine changes/NaN/channel loss blocked; float noise accepted')

    try:
        tangent_space.unity_corner_frames([(0.,0.,0.),(0.,1.,0.),(0.,0.,1.)], [(0,1,2)],
            [[(1.,0.,0.)]*3], [(0,0,0)], [(0.,0.,0.)], face_ids=[12])
    except ValueError as error:
        assert '三角面 #13' in str(error) and 'UV 顶点编号' in str(error), str(error)
    else: raise AssertionError('Invalid tangent accepted')
    try:
        tangent_space.unity_corner_frames([(0.,0.,0.),(1.,0.,0.),(0.,1.,0.)], [(0,1,2)],
            [[(0.,0.,1.)]*3], [(0,10,1)], [(0.,0.,0.),(1.,0.,0.)], face_ids=[21])
    except ValueError as error:
        assert '三角面 #22' in str(error) and '面角 2' in str(error) and '索引' in str(error), str(error)
    else: raise AssertionError('Invalid UV index accepted')
    passed('Tangent failures use original triangle/corner numbers after Unity winding reversal and degenerate-face filtering')

    native_verify = tool.verify_collapse
    pairs = []
    def recorded(before, after):
        pairs.append((before, after))
        return native_verify(before, after)
    tool.verify_collapse = recorded
    for label, node in [('Editable Mesh', static_mesh()), ('Editable Poly', static_mesh(True)),
                        ('Mesh + UV2/UV4/RGB/Alpha', static_mesh(extended=True)), ('Primitive Box', rt.box(mapCoords=True))]:
        assert not any(str(rt.classOf(m)) == 'Skin' for m in node.modifiers)
        node.name = label + ' · 无 Skin'; rt.select(node); idle(); before = fingerprint(node)
        pairs.clear(); dialog.bake()
        assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
        assert '无 Skin 静态网格路径' in dialog.log.toPlainText()
        after = fingerprint(node)
        assert before['vertices'] == after['vertices'] and before['faces'] == after['faces']
        for c, values in before['channels'].items():
            assert tool.channel_difference(c, values, after['channels'][c], before['faces']) is None
        strict_differences = sum(a['channels'] != b['channels'] for a, b in pairs)
        passed('%s without Skin: bake + native collapse succeeds; original channels retained; strict-float differences %s' % (label, strict_differences))

    node = static_mesh(extended=True); skin = rt.Skin(); rt.addModifier(node, skin); rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(skin, node=node)
    bone = rt.BoneSys.createBone(rt.Point3(0,0,0), rt.Point3(0,0,20), rt.Point3(0,1,0)); rt.skinOps.addBone(skin,bone,1)
    for i in range(1,9): rt.skinOps.replaceVertexWeights(skin,i,rt.Array(1),rt.Array(1.))
    weights = tool.skin_fingerprint(node, skin); handle = rt.getHandleByAnim(skin); idle(); dialog.bake()
    assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
    assert tool.skin_fingerprint(node, skin) == weights and rt.getHandleByAnim(skin) == handle
    passed('Same extended UV/color mesh with Skin: bake/collapse preserves Skin identity and weights')

    # Reproduce the screenshot's static-base panel state after editing Skin.
    node = static_mesh(); node.name = '静态底层显示回归'; rt.select(node); idle()
    rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(node.baseObject, node=node)
    rt.showEndResult = False
    original = fingerprint(node); assert not rt.showEndResult
    dialog.clear_log(); dialog.bake()
    assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
    with pymxs.undo(False): actual = fingerprint(node)
    assert 2 in actual['channels'] and 3 in actual['channels']
    assert original['vertices'] == actual['vertices'] and original['faces'] == actual['faces']
    assert tool.channel_difference(1, original['channels'][1], actual['channels'][1], original['faces']) is None
    passed('No Skin, static base selected with Show End Result OFF after Skin editing: bake/collapse retains full channels, no false channel-added error')

    # Confirm a substantive channel delta still cancels the complete native transaction.
    node = static_mesh(); node.name = '诊断测试 · 无 Skin'; rt.select(node); idle(); before = fingerprint(node)
    def changed_channel(before, after):
        altered = copy.deepcopy(after)
        values = list(altered['channels'][3]); x,y,z = values[1]; values[1] = (x+.01,y,z)
        altered['channels'][3] = tuple(values)
        return native_verify(before, altered)
    rt.modPanel.setCurrentObject(node.baseObject, node=node); rt.showEndResult = False
    tool.verify_collapse = changed_channel
    dialog.clear_log(); dialog.bake()
    text = dialog.log.toPlainText()
    assert all(token in text for token in ('阶段：自动塌陷', '无（静态网格路径）', '第 3 套 UV', '三角面 #1', '面角 2', '全部撤销')), text
    assert fingerprint(node) == before and not tool.map_has_data(node.mesh, 3)
    tool.verify_collapse = native_verify
    passed('Injected substantive channel mismatch names static path, UV channel and corner; complete rollback preserves original model')
    selected = list(rt.selection); before = fingerprint(node); holding = rt.theHold.Holding()
    dialog.clear_log_button.click()
    assert not dialog.log.toPlainText() and dialog.log_sequence == 0 and dialog.log_start == 0
    assert fingerprint(node) == before and list(rt.selection) == selected and rt.theHold.Holding() == holding
    dialog.bake(); assert '#01' in dialog.log.toPlainText() and '全部成功' in dialog.status.text()
    passed('Manual clear resets log only, preserves model/selection/Undo hold, numbering restarts at 01')

    # Related project Bard asset, tested as a static mesh. Its internal model name
    # is SM_TH_Hero3000_Bard; it is not claimed to be the exact screenshot file.
    source = Path(r'D:\TL\topheroes-client\client\Assets\ArtAssets\_TopLegendArt\Characters\Hero_Bard\Models\Hero_Bard.fbx')
    previous = set(int(rt.getHandleByAnim(n)) for n in rt.objects)
    rt.FBXImporterSetParam('Animation', False)
    rt.importFile(str(source), rt.Name('noPrompt'), using=rt.FBXIMP)
    imported = [n for n in rt.objects if int(rt.getHandleByAnim(n)) not in previous and rt.superClassOf(n) == rt.GeometryClass]
    bard = next(n for n in imported if str(n.name) == 'SM_TH_Hero3000_Bard')
    for modifier in list(bard.modifiers):
        if str(rt.classOf(modifier)) == 'Skin': modifier.enabled = False
    rt.convertToMesh(bard)
    assert not any(str(rt.classOf(m)) == 'Skin' for m in bard.modifiers)
    rt.select(bard); idle(); before = fingerprint(bard); dialog.clear_log(); dialog.bake()
    assert '全部成功' in dialog.status.text(), dialog.log.toPlainText()
    after = fingerprint(bard)
    assert before['vertices'] == after['vertices'] and before['faces'] == after['faces']
    for c, values in before['channels'].items(): assert tool.channel_difference(c,values,after['channels'][c],before['faces']) is None
    passed('Related project Bard FBX, converted to static/no Skin: bake + collapse succeeds and original UV/color retained')
    dialog.resize(620,640); QtWidgets.QApplication.processEvents()
    assert dialog.grab().save(str(artifacts/'diagnostics_clear_log_ui.png'))
    dialog.close()
    rows.append('ALL DIAGNOSTICS AND NO-SKIN TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    out.write_text('\n'.join(rows), encoding='utf-8')
