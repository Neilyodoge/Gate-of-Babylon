"""One-click geometric-degenerate cleanup and accepted warnings in disposable MaxBatch."""
import os,sys,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
assert os.environ.get('TL_OUTLINE_BATCH_TEST')=='1'
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
from outline_math import is_degenerate_triangle
out=root/'tests/artifacts/degenerate_cleanup_result.txt';rows=['STARTED'];out.write_text('\n'.join(rows),encoding='utf-8')
def passed(s):rows.append(s+' PASS');out.write_text('\n'.join(rows),encoding='utf-8')
def fingerprint(node):
    with pymxs.undo(False):return tool.collapse_fingerprint(node)
def bad_count(node):
    mesh=node.mesh;v=[tool.point(rt.getVert(mesh,i)) for i in range(1,int(rt.getNumVerts(mesh))+1)]
    return sum(is_degenerate_triangle(v,tuple(int(x)-1 for x in tool.point(rt.getFace(mesh,i)))) for i in range(1,int(rt.getNumFaces(mesh))+1))
def fixture(disconnected=False):
    box=rt.box(mapCoords=True);mesh=rt.snapshotAsMesh(box);rt.delete(box)
    rt.setNumFaces(mesh,13,True)
    if disconnected:
        rt.meshop.setNumVerts(mesh,11)
        for i in range(9,12):rt.setVert(mesh,i,rt.Point3(i,0,0))
        rt.setFace(mesh,13,rt.Point3(9,10,11))
    else:
        first=rt.getFace(mesh,1);rt.setFace(mesh,13,rt.Point3(first.x,first.x,first.y))
        rt.setFaceSmoothGroup(mesh,13,rt.getFaceSmoothGroup(mesh,1))
    for channel in range(1,int(rt.meshop.getNumMaps(mesh))):
        if rt.meshop.getMapSupport(mesh,channel):
            rt.meshop.setNumMapFaces(mesh,channel,13,keep=True)
            first_uv=rt.meshop.getMapFace(mesh,channel,1)
            rt.meshop.setMapFace(mesh,channel,13,rt.Point3(first_uv.x,first_uv.x,first_uv.y))
    node=rt.mesh(mesh=mesh);rt.update(node)
    rt.meshop.setNumMaps(node,3,keep=True);rt.meshop.setMapSupport(node,2,True);rt.meshop.defaultMapFaces(node,2)
    return node
def idle():
    rt.subObjectLevel=0
    if rt.theHold.Holding():rt.theHold.Accept('Disposable test setup')
    rt.clearUndoBuffer()
try:
    if rt.theHold.Holding():rt.theHold.Accept('Batch startup')
    dialog=tool.show()
    node=fixture();rt.select(node);idle();original_uv=tool.uv_corner_values(node.mesh,2)
    dialog.bake()
    assert '全部成功' in dialog.status.text() and '已忽略 1 个' in dialog.status.text(),dialog.log.toPlainText()
    assert int(rt.getNumFaces(node.mesh))==13 and tool.uv_corner_values(node.mesh,2)==original_uv
    passed('Default ignore retains 13 faces and reports accepted count')
    before=fingerprint(node);idle();dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
    assert '全部成功' in dialog.status.text(),dialog.log.toPlainText()
    assert int(rt.getNumFaces(node.mesh))==12 and bad_count(node)==0
    after=fingerprint(node)
    rt.execute('max undo');assert fingerprint(node)==before
    rt.execute('max redo');assert fingerprint(node)==after
    rt.execute('max undo');assert fingerprint(node)==before
    passed('Mesh cleanup alone deletes only bad face; UVs/materials/normals and one Undo/Redo/Undo')
    node=rt.box(mapCoords=True);rt.convertToPoly(node)
    rt.polyop.setVert(node.baseObject,2,rt.polyop.getVert(node.baseObject,1));rt.select(node);idle()
    before=fingerprint(node);initial_bad=bad_count(node);assert initial_bad>0
    dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
    assert '全部成功' in dialog.status.text(),dialog.log.toPlainText()
    assert bad_count(node)==0 and int(rt.getNumFaces(node.mesh))==len(before['faces'])-initial_bad
    rt.execute('max undo');assert fingerprint(node)==before
    passed('Poly partial-degenerate polygons are triangulated and cleaned with Undo')
    node=fixture(True);skin=rt.Skin();rt.addModifier(node,skin)
    rt.select(node);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(skin)
    bones=[rt.BoneSys.createBone(rt.Point3(x,0,0),rt.Point3(x,0,20),rt.Point3(0,1,0)) for x in (0,10)]
    for bone in bones:rt.skinOps.addBone(skin,bone,1)
    for i in range(1,12):
        rt.skinOps.unNormalizeVertex(skin,i,i%2==0)
        rt.skinOps.replaceVertexWeights(skin,i,rt.Array(1,2),rt.Array(.4,.8) if i%2==0 else rt.Array(i/12.,1-i/12.))
        rt.skinOps.setVertexDQWeight(skin,i,i/12.)
        if i%3==0:rt.skinOps.rigidVertex(skin,i,True)
    rt.notifyDependents(node);rt.snapshotAsMesh(node)
    skin.enabled=False;skin.enabledInViews=False
    before_weights=tool.skin_fingerprint(node,skin);before=fingerprint(node);handle=rt.getHandleByAnim(skin)
    idle();dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
    assert '全部成功' in dialog.status.text(),dialog.log.toPlainText()
    assert int(rt.getNumVerts(node.mesh))==8 and int(rt.getNumFaces(node.mesh))==12
    assert list(node.modifiers)==[skin] and rt.getHandleByAnim(skin)==handle
    weights=tool.skin_fingerprint(node,skin)
    assert weights[:4]==before_weights[:4]
    for field in range(4,len(weights)):
        if field==4:
            for a,b in zip(weights[field],before_weights[field][:8]):
                assert {bone:w for bone,w in a if w>1e-6}=={bone:w for bone,w in b if w>1e-6}
        else:assert weights[field]==before_weights[field][:8]
    after=fingerprint(node)
    rt.execute('max undo');assert fingerprint(node)==before and tool.skin_fingerprint(node,skin)==before_weights
    rt.execute('max redo');assert fingerprint(node)==after and tool.skin_fingerprint(node,skin)==weights
    rt.execute('max undo');assert fingerprint(node)==before and tool.skin_fingerprint(node,skin)==before_weights
    passed('Skin disconnected bad face + 3 orphan vertices: same Skin, raw weights/DQ/flags and Undo/Redo/Undo')
    node=fixture();rt.meshop.setNumMaps(node,4,keep=True);rt.meshop.setMapSupport(node,3,True);rt.meshop.defaultMapFaces(node,3)
    rt.select(node);before=fingerprint(node);idle();dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
    assert '全部成功' in dialog.status.text() and bad_count(node)==0,dialog.log.toPlainText()
    after=fingerprint(node);dialog.bake()
    assert '存在问题' in dialog.status.text(),dialog.log.toPlainText()
    assert fingerprint(node)==after
    rt.execute('max undo');assert fingerprint(node)==before
    passed('Cleanup is separate from baking: occupied UV blocks bake, cleanup remains independently undoable')
    node=rt.box(mapCoords=True);rt.convertToMesh(node);rt.meshop.setMapFace(node,1,1,rt.Point3(1,1,1))
    rt.select(node);before=fingerprint(node);idle();dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
    assert '全部成功' in dialog.status.text() and fingerprint(node)==before
    passed('UV-only degenerate face with valid geometry is preserved')
    node=fixture();rt.select(node);idle();before=fingerprint(node)
    with pymxs.undo(True,'Simulated user edit'):
        dialog.run_batch('清理退化面', 'TL Clean Degenerate Faces', tool.clean_degenerate_faces)
        assert '进行中的编辑操作' in dialog.status.text() and fingerprint(node)==before
    passed('Active user Undo rejects cleanup without changes')
    dialog.close();rows.append('ALL DEGENERATE CLEANUP TESTS PASSED')
except Exception:rows.append(traceback.format_exc())
finally:out.write_text('\n'.join(rows),encoding='utf-8')
