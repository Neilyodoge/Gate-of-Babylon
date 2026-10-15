"""Skin-preserving collapse, UV2 model modes and native orphan cleanup regression."""
import os, sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
from PySide2 import QtWidgets
import tl_outline_max as tool
out = root / 'tests/artifacts'
class ProgressRows(list):
    def append(self, value):
        super().append(value)
        (out/'modes_collapse_clean_result.txt').write_text('\n'.join(self),encoding='utf-8')
rows = ProgressRows()
rows.append('STARTED')
# MaxBatch evaluates its entry script inside a startup undo hold. This disposable
# process has no user operation: accept only that setup hold before simulating
# normal, idle button clicks. Keep the production busy guard unchanged.
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1', 'Run with TL_OUTLINE_BATCH_TEST=1 in 3dsmaxbatch, never a live scene.'
if rt.theHold.Holding():
    rt.theHold.Accept('MaxBatch test setup')

def weighted_skin(node):
    skin=rt.Skin();rt.addModifier(node,skin)
    rt.select(node);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(skin)
    bones=[rt.BoneSys.createBone(rt.Point3(x,0,0),rt.Point3(x,0,20),rt.Point3(0,1,0)) for x in (0,10)]
    for bone in bones:rt.skinOps.addBone(skin,bone,1)
    for i in range(1,int(rt.skinOps.GetNumberVertices(skin))+1):
        rt.skinOps.replaceVertexWeights(skin,i,rt.Array(1,2),rt.Array(i/12.,1-i/12.))
    bones[0].rotation=rt.eulerAngles(17,31,43);bones[1].pos=rt.Point3(18,5,7)
    return skin,bones

def box_uv2():
    node=rt.box(mapCoords=True);rt.convertToMesh(node)
    rt.meshop.setNumMaps(node,3,keep=True);rt.meshop.setMapSupport(node,2,True)
    rt.meshop.defaultMapFaces(node,2)
    return node

def orphan(poly=False):
    box=rt.box(mapCoords=True);mesh=rt.snapshotAsMesh(box);rt.delete(box)
    points=[rt.getVert(mesh,i) for i in range(1,9)]
    faces=[rt.getFace(mesh,i) for i in range(1,13)]
    rt.meshop.setNumVerts(mesh,9);rt.setVert(mesh,1,rt.Point3(-20,-20,-20))
    for i,p in enumerate(points,2):rt.setVert(mesh,i,p)
    for i,f in enumerate(faces,1):rt.setFace(mesh,i,rt.Point3(f.x+1,f.y+1,f.z+1))
    node=rt.mesh(mesh=mesh)
    if poly:rt.convertToPoly(node)
    return node

try:
    dialog=tool.show()
    assert not dialog.character_mode.isChecked() and not dialog.scene_mode.isChecked()
    assert dialog.auto_collapse.isChecked()
    node=box_uv2();rt.select(node)
    with pymxs.undo(True, 'Simulated user edit'):
        dialog.bake()
        assert '进行中的编辑操作' in dialog.status.text()
        assert not tool.map_has_data(node.mesh,3) and not list(node.modifiers)
    rows.append('Active user undo operation refuses the button without changes PASS')
    node=box_uv2();original_uv2=tool.uv_corner_values(node.mesh,2)
    rt.select(node);rt.clearUndoBuffer();dialog.bake()
    assert '#63d58a' in dialog.status.styleSheet(),dialog.log.toPlainText()
    assert not list(node.modifiers),list(node.modifiers)
    assert tool.uv_corner_values(node.mesh,2)==original_uv2
    base=node.baseObject
    dialog.bake()
    assert node.baseObject==base and not list(node.modifiers)
    assert '数据未变化' in dialog.log.toPlainText()
    rows.append('Default preserves existing UV2; automatic collapse and repeated no-op PASS')

    node=box_uv2();rt.select(node)
    dialog.character_mode.setChecked(True);dialog.bake()
    assert '#63d58a' in dialog.status.styleSheet(),dialog.log.toPlainText()
    tool.verify_uv_channel(node,2,zero=True);tool.verify_uv_channel(node,3)
    assert not list(node.modifiers) and dialog.channel.minimum()==3
    dialog.scene_mode.setChecked(True)
    assert not dialog.character_mode.isChecked() and dialog.channel.minimum()==2
    node=box_uv2();uv2=tool.uv_corner_values(node.mesh,2);rt.select(node);dialog.bake()
    assert tool.uv_corner_values(node.mesh,2)==uv2
    node=rt.box(mapCoords=True);rt.select(node);dialog.bake();tool.verify_uv_channel(node,2,zero=True)
    dialog.scene_mode.setChecked(False)
    assert not dialog.character_mode.isChecked() and not dialog.scene_mode.isChecked()
    rows.append('Character clears UV2 to one zero; scene preserves or fills; exclusive/unchecked modes PASS')

    for enabled in (False,True):
        node=box_uv2();skin,bones=weighted_skin(node);skin.enabled=enabled
        # 批处理直接调用 skinOps 不会像交互 UI 那样在操作结束时自动提交 Hold。
        if rt.theHold.Holding():
            rt.theHold.Accept('Test Skin setup')
        before=tool.skin_fingerprint(node,skin)
        rt.select(node);rt.clearUndoBuffer()
        rt.modPanel.setCurrentObject(skin);rt.subObjectLevel=1
        dialog.character_mode.setChecked(True)
        # 3dsmaxbatch 的 Skin 面板偶尔延迟关闭初始化 Hold；正常交互点击
        # 按钮前事件循环已经提交。这里只清理测试搭建产生的事务。
        if rt.theHold.Holding():
            rt.theHold.Accept('Test Skin panel setup')
        rt.modPanel.setCurrentObject(skin);rt.subObjectLevel=1
        assert rt.modPanel.getCurrentObject()==skin and int(rt.subObjectLevel)==1
        dialog.bake()
        assert '#63d58a' in dialog.status.styleSheet(),dialog.log.toPlainText()
        assert list(node.modifiers)==[skin] and tool.skin_fingerprint(node,skin)==before
        assert rt.modPanel.getCurrentObject()==skin and int(rt.subObjectLevel)==1
        rt.execute('max undo')
        assert list(node.modifiers)==[skin] and tool.skin_fingerprint(node,skin)==before
        assert not tool.map_has_data(node.mesh,3)
        dialog.bake()
        assert list(node.modifiers)==[skin]
        rt.modPanel.setCurrentObject(skin);rt.skinOps.replaceVertexWeights(skin,1,1,1.)
        assert all(w==0 or b==1 for b,w in tool.skin_fingerprint(node,skin)[4][0])
        # skinOps 在 3dsmaxbatch 中会留下模拟编辑的 Hold；真实 UI 会在用户
        # 松开编辑操作时提交。测试进程主动提交，避免污染下一轮按钮点击。
        if rt.theHold.Holding():
            rt.theHold.Accept('Simulated Skin edit')
        rows.append('Skin enabled=%s: pose, raw weights, original modifier, UV2, editing mode and one undo PASS' % enabled)
    rt.subObjectLevel=0

    # Failure after native collapse must restore the pre-bake base, modifiers and Skin.
    node=box_uv2();skin,bones=weighted_skin(node)
    original_base=node.baseObject;before=tool.skin_fingerprint(node,skin)
    real_verify=tool.verify_collapse
    def fail(*args):raise tool.CollapseError('Injected collapse data mismatch')
    tool.verify_collapse=fail
    rt.select(node);dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet() and '全部撤销' in dialog.status.text()
    assert node.baseObject==original_base and list(node.modifiers)==[skin]
    assert tool.skin_fingerprint(node,skin)==before and not tool.map_has_data(node.mesh,3)
    tool.verify_collapse=real_verify
    rows.append('Injected collapse verification failure cancels whole bake and preserves Skin PASS')

    for poly in (False,True):
        for with_skin in (False,True):
            node=orphan(poly);skin=None
            if with_skin:skin,bones=weighted_skin(node)
            weights=tool.skin_fingerprint(node,skin) if skin else None
            original_base=node.baseObject
            rt.select(node);rt.clearUndoBuffer();dialog.clean_isolated()
            assert '#63d58a' in dialog.status.styleSheet(),dialog.log.toPlainText()
            assert int(rt.getNumVerts(node.mesh))==8 and int(rt.getNumFaces(node.mesh))==12
            if skin:
                actual=tool.skin_fingerprint(node,skin)
                assert actual[:4]==weights[:4]
                for new,old in enumerate(range(1,9)):
                    assert {b:w for b,w in actual[4][new] if w>1e-6}=={b:w for b,w in weights[4][old] if w>1e-6}
            rt.execute('max undo')
            assert int(rt.getNumVerts(node.mesh))==9 and node.baseObject==original_base
            if skin:
                assert tool.skin_fingerprint(node,skin)==weights
                rt.execute('max redo')
                assert int(rt.getNumVerts(node.mesh))==8
                redo_weights=tool.skin_fingerprint(node,skin)
                for new,old in enumerate(range(1,9)):
                    assert {b:w for b,w in redo_weights[4][new] if w>1e-6}=={b:w for b,w in weights[4][old] if w>1e-6}
                assert not rt.theHold.Holding()
                rt.execute('max undo')
                assert int(rt.getNumVerts(node.mesh))==9 and tool.skin_fingerprint(node,skin)==weights
            rows.append('Orphan at index 1: Poly=%s Skin=%s native cleanup and undo/redo PASS' % (poly,with_skin))

    node=orphan();skin,bones=weighted_skin(node);before=tool.skin_fingerprint(node,skin)
    tool.verify_collapse=fail;rt.select(node);dialog.clean_isolated()
    assert '#ff5b5b' in dialog.status.styleSheet()
    assert int(rt.getNumVerts(node.mesh))==9 and tool.skin_fingerprint(node,skin)==before
    tool.verify_collapse=real_verify
    rows.append('Injected orphan cleanup verification failure restores vertices and Skin weights PASS')

    node=box_uv2();rt.addModifier(node,rt.Bend());rt.select(node)
    dialog.character_mode.setChecked(False);dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet() and '保留修改器栈' in dialog.status.text()
    assert any(str(rt.classOf(m))=='Bend' for m in node.modifiers)
    rows.append('Other user modifiers are preserved with red skip-collapse warning PASS')

    # A real saved 43-bone actor is inspected in a separate process, never saved over.
    rt.loadMaxFile('C:/Users/admin/Documents/3ds Max 2023/autoback/AutoBackup01.max',quiet=True)
    node=rt.getNodeByName('SM_TL_Hero_Prisoner');node.isFrozen=False;node.isHidden=False
    rt.select(node);skin=next(m for m in node.modifiers if str(rt.classOf(m))=='Skin')
    before=tool.skin_fingerprint(node,skin)
    rows.append(tool.collapse_tool_modifiers(node))
    assert tool.skin_fingerprint(node,skin)==before
    assert not any(tool.is_tool_modifier(m) for m in node.modifiers)
    assert len(before[3])==43 and len(before[4])==1709
    rt.saveMaxFile(str(out/'collapsed_actor_test.max'),quiet=True)
    rt.loadMaxFile(str(out/'collapsed_actor_test.max'),quiet=True)
    node=rt.getNodeByName('SM_TL_Hero_Prisoner');rt.select(node)
    skin=next(m for m in node.modifiers if str(rt.classOf(m))=='Skin')
    assert tool.skin_fingerprint(node,skin)==before
    dialog=tool.show();dialog.character_mode.setChecked(True)
    QtWidgets.QApplication.instance().processEvents()
    dialog.grab().save(str(out/'panel_modes_collapse_clean.png'))
    dialog.close()
    rows.append('Actual actor: all 43 bones/1709 weights preserved; saved/reloaded editable Skin PASS')
    rows.append('ALL MODE/COLLAPSE/CLEANUP TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    (out/'modes_collapse_clean_result.txt').write_text('\n'.join(rows),encoding='utf-8')
