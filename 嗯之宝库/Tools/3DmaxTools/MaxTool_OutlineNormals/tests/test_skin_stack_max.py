"""Regression for disabled Skin, top UV clears, failure rollback and removed export."""
import sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
from tl_outline_max import (bake_node, clear_second_uv, map_has_data, uv_corner_values,
                            read_render_normals, show, verify_uv_channel)
from outline_math import dot
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
out = root / 'tests/artifacts'
rows = []

def new_skin(name):
    node = rt.box(name=name, mapCoords=True, length=10, width=10, height=10)
    skin = rt.Skin()
    rt.addModifier(node, skin)
    bone = rt.BoneSys.createBone(rt.Point3(0,0,0),rt.Point3(0,0,20),rt.Point3(0,1,0))
    rt.select(node);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(skin)
    rt.skinOps.addBone(skin,bone,1)
    for i in range(1,9):rt.skinOps.replaceVertexWeights(skin,i,1,1.)
    return node, skin, bone

try:
    reference = None
    for enabled in (True, False):
        node, skin, bone = new_skin('ClearAboveSkin' + str(enabled))
        skin.enabled = enabled
        initial = list(node.modifiers)
        bind_normals = read_render_normals(node, 12)
        rt.ChannelInfo.ClearChannel(node,2);rt.ChannelInfo.ClearChannel(node,3)
        clears = list(node.modifiers)[:2]
        bone.rotation = rt.eulerAngles(17,31,43)
        rt.select(node)
        dialog = show()
        rt.clearUndoBuffer()
        dialog.bake()
        assert '完成：1 成功' in dialog.log.toPlainText(), dialog.log.toPlainText()
        assert bool(skin.enabled) == enabled
        assert all(not bool(m.enabled) for m in clears)
        verify_uv_channel(node,3);verify_uv_channel(node,2,zero=True)
        skin.enabled = False
        normal_after = read_render_normals(node,12)
        assert min(dot(a,b) for fa,fb in zip(bind_normals,normal_after) for a,b in zip(fa,fb)) > .99999
        current = uv_corner_values(rt.snapshotAsMesh(node),3)
        if reference is None:reference=current
        else:assert current==reference, 'Skin on/off altered baked UV'
        skin.enabled = enabled
        count = len(list(node.modifiers))
        bake_node(node)
        assert count == len(list(node.modifiers)), 'Repeated bake added modifiers'
        # Undo the original UI bake, not the subsequent direct no-op call.
        rt.execute('max undo')
        assert all(bool(m.enabled) for m in clears), 'Undo did not restore clears'
        assert list(node.modifiers) == clears + initial
        assert bool(skin.enabled) == enabled
        rows.append('Skin enabled=%s: top UV2/3 clears, posed bake, normals, repeat and UI undo PASS' % enabled)
        rows.append(bake_node(node))
        skin.enabled = True
        export_uv_fbx([node],str(out / ('stack_skin_' + str(enabled) + '.fbx')))
        dialog.close()

    node, skin, bone = new_skin('ClearUV2AfterBake')
    bake_node(node)
    rt.ChannelInfo.ClearChannel(node,2)
    clear = node.modifiers[0]
    u3 = uv_corner_values(rt.snapshotAsMesh(node),3)
    rows.append(clear_second_uv(node))
    assert not bool(clear.enabled) and bool(skin.enabled)
    verify_uv_channel(node,2,zero=True)
    assert u3 == uv_corner_values(rt.snapshotAsMesh(node),3)
    rows.append('Clear UV2 under Skin preserves UV3 and disables top clear PASS')

    node, skin, bone = new_skin('MissingIntermediateOnly')
    bake_node(node)
    rt.ChannelInfo.ClearChannel(node,2)
    # Disable the clear, but explicitly remove its input placeholder with a new
    # clear below Skin. An unchanged UV3 must still cause the placeholder repair.
    clear = node.modifiers[0]
    rt.deleteModifier(node,clear)
    rt.addModifier(node,clear,before=list(node.modifiers).index(skin)+1)
    assert not map_has_data(rt.snapshotAsMesh(node),2)
    bake_node(node)
    verify_uv_channel(node,2,zero=True);verify_uv_channel(node,3)
    rows.append('Unchanged UV3 repairs missing intermediate UV2 PASS')

    node, skin, bone = new_skin('FailedBakeRollback')
    rt.ChannelInfo.ClearChannel(node,2)
    clear = node.modifiers[0]
    rt.ChannelInfo.ClearChannel(node,1)
    original = list(node.modifiers)
    try:
        bake_node(node)
        raise AssertionError('Missing UV1 was accepted')
    except ValueError as error:
        assert '第一套' in str(error)
    assert list(node.modifiers) == original and bool(clear.enabled) and bool(skin.enabled)
    rows.append('Failed bake restores clears and Skin and adds no modifiers PASS')

    rt.fileIn(str(root/'Launch.ms'));rt.fileIn(str(root/'Launch.ms'))
    from PySide2 import QtWidgets
    app = QtWidgets.QApplication.instance();app.processEvents()
    windows = [w for w in app.topLevelWidgets() if w.isVisible() and w.windowTitle()=='TL 描边 · 平滑法线写入 UV']
    assert len(windows)==1, len(windows)
    window=windows[0]
    assert not hasattr(window,'export_button') and not hasattr(window,'export_fbx')
    window.grab().save(str(out/'panel_no_export.png'))
    window.close()
    rows.append('Repeated Launch one window and export feature removed PASS')
    rows.append('ALL SKIN STACK TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    (out/'skin_stack_max_result.txt').write_text('\n'.join(rows),encoding='utf-8')
