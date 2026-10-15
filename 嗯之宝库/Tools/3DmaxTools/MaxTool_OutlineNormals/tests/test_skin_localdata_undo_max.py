"""Prove same Skin identity and native Undo with separate LocalModData contexts."""
import os, sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1'
out = root / 'tests/artifacts/localdata_same_skin_result.txt'
rows = ['STARTED']
if rt.theHold.Holding(): rt.theHold.Accept('MaxBatch setup')
try:
    for poly in (False, True):
        box = rt.box(mapCoords=True); mesh = rt.snapshotAsMesh(box); rt.delete(box)
        points = [rt.getVert(mesh, i) for i in range(1,9)]
        faces = [rt.getFace(mesh, i) for i in range(1,13)]
        rt.meshop.setNumVerts(mesh, 9); rt.setVert(mesh,1,rt.Point3(-20,-20,-20))
        for i,p in enumerate(points,2): rt.setVert(mesh,i,p)
        for i,f in enumerate(faces,1): rt.setFace(mesh,i,rt.Point3(f.x+1,f.y+1,f.z+1))
        node = rt.mesh(mesh=mesh)
        if poly: rt.convertToPoly(node)
        original = rt.Skin(); rt.addModifier(node,original)
        rt.select(node); rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(original)
        bones = [rt.BoneSys.createBone(rt.Point3(x,0,0),rt.Point3(x,0,20),rt.Point3(0,1,0)) for x in (0,10)]
        for bone in bones: rt.skinOps.addBone(original,bone,1)
        for i in range(1,10):
            rt.skinOps.replaceVertexWeights(original,i,rt.Array(1,2),rt.Array(i/12.,1-i/12.))
            rt.skinOps.setVertexDQWeight(original,i,i/10.)
        if rt.theHold.Holding(): rt.theHold.Accept('Skin setup')
        before = tool.skin_fingerprint(node,original)
        handle = rt.getHandleByAnim(original)
        rt.clearUndoBuffer(); rt.subObjectLevel=0
        with tool.undo_transaction('LocalData original Skin test'):
            work = rt.copy(original)
            rt.addModifierWithLocalData(node,work,node,original,before=1)
            assert tool.skin_fingerprint(node,work) == before
            work.enabled=False
            rt.deleteModifier(node,original)
            if poly: rt.polyop.deleteIsoVerts(node.baseObject); rt.polyop.collapseDeadStructs(node.baseObject)
            else: rt.meshop.deleteIsoVerts(node.baseObject)
            work.enabled=True; rt.notifyDependents(node); rt.snapshotAsMesh(node)
            rt.modPanel.setCurrentObject(work,node=node)
            for new,old in enumerate(range(1,9),1):
                weights=before[4][old]
                rt.skinOps.replaceVertexWeights(work,new,rt.Array(*(b for b,w in weights)),rt.Array(*(w for b,w in weights)))
                rt.skinOps.setVertexDQWeight(work,new,before[5][old])
            clean = tool.skin_fingerprint(node,work)
            # The deleted original context is held by native Undo. Reattach the
            # SAME modifier instance with a new context copied from the worker.
            rt.addModifierWithLocalData(node,original,node,work,before=1)
            rt.deleteModifier(node,work)
            assert list(node.modifiers)==[original]
            assert rt.getHandleByAnim(original)==handle
            assert tool.skin_fingerprint(node,original)==clean
        rt.execute('max undo')
        assert int(rt.getNumVerts(node.mesh))==9
        assert list(node.modifiers)==[original] and rt.getHandleByAnim(original)==handle
        assert tool.skin_fingerprint(node,original)==before
        rt.execute('max redo')
        assert int(rt.getNumVerts(node.mesh))==8
        assert list(node.modifiers)==[original]
        assert tool.skin_fingerprint(node,original)==clean
        rt.execute('max undo')
        assert tool.skin_fingerprint(node,original)==before
        rows.append('Poly=%s: original Skin handle, full weights, DQ and native Undo/Redo/Undo PASS' % poly)
        out.write_text('\n'.join(rows),encoding='utf-8')
    rows.append('ALL PASSED')
except Exception: rows.append(traceback.format_exc())
finally: out.write_text('\n'.join(rows),encoding='utf-8')
