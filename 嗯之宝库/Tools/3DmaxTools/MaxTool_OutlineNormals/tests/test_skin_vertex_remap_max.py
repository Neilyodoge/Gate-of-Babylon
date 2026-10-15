"""Skin LocalData preservation across orphan locations, flags and Undo/Redo."""
import os, sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1'
out = root / 'tests/artifacts/skin_vertex_remap_result.txt'
rows = ['STARTED']
try:
    dialog = tool.show()
    for poly in (False, True):
        for orphan_ids in ((1,), (5,), (9,), (1,6,10)):
            for enabled in (False, True):
                box=rt.box(mapCoords=True); mesh=rt.snapshotAsMesh(box); rt.delete(box)
                positions=[rt.getVert(mesh,i) for i in range(1,9)]
                faces=[rt.getFace(mesh,i) for i in range(1,13)]
                count=8+len(orphan_ids); used=[i for i in range(1,count+1) if i not in orphan_ids]
                rt.meshop.setNumVerts(mesh,count)
                for i in orphan_ids: rt.setVert(mesh,i,rt.Point3(-20,-20,-20))
                for i,p in zip(used,positions): rt.setVert(mesh,i,p)
                for i,f in enumerate(faces,1): rt.setFace(mesh,i,rt.Point3(used[int(f.x)-1],used[int(f.y)-1],used[int(f.z)-1]))
                node=rt.mesh(mesh=mesh)
                if poly: rt.convertToPoly(node)
                skin=rt.Skin(); rt.addModifier(node,skin)
                rt.select(node); rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(skin)
                bones=[rt.BoneSys.createBone(rt.Point3(x,0,0),rt.Point3(x,0,20),rt.Point3(0,1,0)) for x in (0,10)]
                for bone in bones: rt.skinOps.addBone(skin,bone,1)
                for i in range(1,count+1):
                    non_normalized=i%2==0
                    rt.skinOps.unNormalizeVertex(skin,i,non_normalized)
                    rt.skinOps.replaceVertexWeights(skin,i,rt.Array(1,2),rt.Array(.4,.8) if non_normalized else rt.Array(i/12.,1-i/12.))
                    rt.skinOps.setVertexDQWeight(skin,i,i/(count+1.))
                    if i%3==0: rt.skinOps.rigidVertex(skin,i,True)
                # Let native Skin finish applying rigid mode (single effective
                # bone) before capturing the fixture. Otherwise disabled Skin
                # keeps pending mixed weights that change on its first evaluation.
                skin.enabled=True;skin.enabledInViews=True
                rt.notifyDependents(node);rt.snapshotAsMesh(node)
                skin.enabled=enabled; skin.enabledInViews=not enabled
                if rt.theHold.Holding(): rt.theHold.Accept('Batch Skin setup')
                before=tool.skin_fingerprint(node,skin); handle=rt.getHandleByAnim(skin)
                rt.subObjectLevel=0; rt.clearUndoBuffer()
                dialog.clean_isolated()
                assert '#63d58a' in dialog.status.styleSheet(),dialog.log.toPlainText()
                assert list(node.modifiers)==[skin] and rt.getHandleByAnim(skin)==handle
                expected=before[:4]+tuple(tuple(field[i-1] for i in used) for field in before[4:])
                after=tool.skin_fingerprint(node,skin)
                assert after[:4]==expected[:4]
                for field in range(4,len(after)):
                    for actual,wanted in zip(after[field],expected[field]):
                        if field==4:
                            a={b:w for b,w in actual if w>1e-6};b={b:w for b,w in wanted if w>1e-6}
                            assert a.keys()==b.keys() and all(abs(a[k]-b[k])<1e-6 for k in a),(field,actual,wanted)
                        else: assert abs(float(actual)-float(wanted))<1e-6,(field,actual,wanted)
                rt.execute('max undo')
                assert list(node.modifiers)==[skin] and tool.skin_fingerprint(node,skin)==before
                rt.execute('max redo')
                assert list(node.modifiers)==[skin] and tool.skin_fingerprint(node,skin)==after
                rt.execute('max undo')
                assert tool.skin_fingerprint(node,skin)==before
                rows.append('Poly=%s orphan=%s enabled=%s: same Skin, weights/DQ/flags/switches and Undo/Redo/Undo PASS' % (poly,orphan_ids,enabled))
                out.write_text('\n'.join(rows),encoding='utf-8')
    rows.append('ALL SKIN VERTEX REMAP TESTS PASSED')
except Exception: rows.append(traceback.format_exc())
finally: out.write_text('\n'.join(rows),encoding='utf-8')
