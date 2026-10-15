"""Verify retained Skin weights/deformation/editing and red/green batch results."""
import sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import tl_outline_max as tool
from pymxs import runtime as rt
from PySide2 import QtWidgets
out = root / 'tests/artifacts'
rows = []

def skin_weights(skin):
    return tuple(tuple((int(rt.skinOps.GetVertexWeightBoneID(skin, i, j)),
                        float(rt.skinOps.GetVertexWeight(skin, i, j)))
                       for j in range(1, int(rt.skinOps.GetVertexWeightCount(skin, i)) + 1))
                 for i in range(1, int(rt.skinOps.GetNumberVertices(skin)) + 1))

def positions(node):
    mesh = node.mesh
    return tuple(tool.point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1))

def positive_weights(weights):
    return tuple((bone, weight) for bone, weight in weights if weight > 1e-6)

def close_positions(a, b):
    return len(a) == len(b) and max(abs(x-y) for p,q in zip(a,b) for x,y in zip(p,q)) < 1e-4

def new_skin(poly, enabled):
    node = rt.box(name='EditableSkin%s%s' % (poly, enabled), mapCoords=True)
    if poly:
        rt.convertToPoly(node)
    skin = rt.Skin()
    rt.addModifier(node, skin)
    bones = [rt.BoneSys.createBone(rt.Point3(x,0,0), rt.Point3(x,0,20), rt.Point3(0,1,0))
             for x in (0, 10)]
    rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin)
    for bone in bones:
        rt.skinOps.addBone(skin, bone, 1)
    for i in range(1,9):
        rt.skinOps.replaceVertexWeights(skin, i, rt.Array(1,2), rt.Array(i/10., 1-i/10.))
    bones[0].rotation = rt.eulerAngles(17,31,43)
    bones[1].pos = rt.Point3(20,5,7)
    skin.enabled = enabled
    return node, skin, bones

try:
    for poly in (False, True):
        for enabled in (False, True):
            node, skin, bones = new_skin(poly, enabled)
            initial = list(node.modifiers)
            before_weights, before_pos = skin_weights(skin), positions(node)
            rt.subObjectLevel = 1
            dialog = tool.show()
            rt.clearUndoBuffer()
            dialog.bake()
            assert '完成：1 成功' in dialog.log.toPlainText(), dialog.log.toPlainText()
            assert '#63d58a' in dialog.status.styleSheet(), dialog.status.styleSheet()
            assert skin in list(node.modifiers) and bool(skin.enabled) == enabled
            assert rt.modPanel.getCurrentObject() == skin, 'Skin editing panel lost'
            assert int(rt.subObjectLevel) == 1, 'Skin subobject mode lost'
            assert skin_weights(skin) == before_weights, 'Original weights changed'
            assert close_positions(positions(node), before_pos), 'Skin deformation changed'
            assert int(rt.skinOps.GetNumberBones(skin)) == 2
            # The original Skin instance must support edits immediately, without reselection.
            rt.skinOps.replaceVertexWeights(skin, 1, 2, 1.)
            edited_weights = skin_weights(skin)[0]
            rows.append('Edited weights: %s' % (edited_weights,))
            assert positive_weights(edited_weights) == ((2, 1.),), 'Skin cannot edit weights after bake'
            skin.enabled = True
            edited_pos = positions(node)
            bones[1].pos = bones[1].pos + rt.Point3(5,0,0)
            moved_pos = positions(node)
            assert abs(moved_pos[0][0] - edited_pos[0][0] - 5) < 1e-4
            skin.enabled = enabled
            changed_weights = skin_weights(skin)
            dialog.clear_uv2()
            assert rt.modPanel.getCurrentObject() == skin and skin_weights(skin) == changed_weights
            tool.preview_rows([node])
            assert rt.modPanel.getCurrentObject() == skin and int(rt.subObjectLevel) == 1
            assert bool(skin.enabled) == enabled
            rt.subObjectLevel = 0
            rows.append('Mesh/Poly=%s Skin enabled=%s: weights, bones, pose, editing, preview panel PASS' % (poly,enabled))
            dialog.close()

    # Persist a generated scene, reload, and edit the same Skin after reading from disk.
    rt.saveMaxFile(str(out/'skin_edit_retained.max'), quiet=True)
    rt.loadMaxFile(str(out/'skin_edit_retained.max'), quiet=True)
    node = rt.getNodeByName('EditableSkinTrueTrue')
    rt.select(node); rt.setCommandPanelTaskMode(rt.Name('modify'))
    skin = next(m for m in node.modifiers if str(rt.classOf(m)) == 'Skin')
    rt.modPanel.setCurrentObject(skin)
    assert positive_weights(skin_weights(skin)[0]) == ((2, 1.),)
    rt.skinOps.replaceVertexWeights(skin, 1, 1, 1.)
    assert positive_weights(skin_weights(skin)[0]) == ((1, 1.),)
    rows.append('Saved/reloaded Max scene retains editable Skin PASS')

    dialog = tool.show()
    rt.clearSelection(); dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet() and '失败' in dialog.status.text()
    good = rt.box(name='<Success & mesh>', mapCoords=True)
    helper = rt.Point(name='SkippedHelper')
    rt.select(rt.Array(good, helper)); dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet()
    assert '1 成功，1 跳过，0 失败' in dialog.status.text()
    assert '<Success & mesh>' in dialog.log.toPlainText(), 'Node name interpreted as HTML'
    rows.append('No selection and mixed success/skip show red; node names escaped PASS')

    real_bake = tool.bake_node
    def fail(*args, **kwargs):
        raise RuntimeError('Injected UV verification failure')
    tool.bake_node = fail
    rt.select(good); dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet() and '1 失败' in dialog.status.text()
    tool.bake_node = lambda *args, **kwargs: tool.BakeResult('Skipped 2 degenerate faces', skipped_faces=2)
    dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet() and '2 个退化面' in dialog.status.text()
    tool.bake_node = real_bake
    dialog.bake()
    assert '#63d58a' in dialog.status.styleSheet() and '全部成功' in dialog.status.text()
    QtWidgets.QApplication.instance().processEvents()
    dialog.grab().save(str(out/'panel_skin_status_green.png'))
    rt.select(rt.Array(good,helper)); dialog.bake()
    QtWidgets.QApplication.instance().processEvents()
    dialog.grab().save(str(out/'panel_skin_status_red.png'))
    rows.append('Failed UV verification and face-level skips red; next full success green PASS')
    rt.select(rt.Array(good,helper)); dialog.clear_uv2()
    assert '#ff5b5b' in dialog.status.styleSheet() and '1 跳过' in dialog.status.text()
    rt.select(good); dialog.clear_uv2()
    assert '#63d58a' in dialog.status.styleSheet()
    rows.append('Clear UV2 also reports red/green consistently PASS')
    dialog.close()
    rows.append('ALL SKIN EDIT AND STATUS TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    (out/'skin_edit_status_max_result.txt').write_text('\n'.join(rows), encoding='utf-8')
