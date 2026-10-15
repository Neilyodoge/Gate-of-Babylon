"""Diagnose native rigid-vertex weight setters in disposable MaxBatch."""
import os,sys,json,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
assert os.environ.get('TL_OUTLINE_BATCH_TEST')=='1'
from pymxs import runtime as rt
import tl_outline_max as tool
out=root/'tests/artifacts/skin_rigid_api_result.json'
rows=[]
try:
 n=rt.box(mapCoords=True);rt.convertToMesh(n);s=rt.Skin();rt.addModifier(n,s)
 rt.select(n);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(s)
 for x in (0,10):
  b=rt.BoneSys.createBone(rt.Point3(x,0,0),rt.Point3(x,0,20),rt.Point3(0,1,0));rt.skinOps.addBone(s,b,1)
 def record(label):
  f=tool.skin_fingerprint(n,s);rows.append({'label':label,'weights':f[4],'unnormalized':f[6],'rigid':f[7]})
 for i in range(1,9):rt.skinOps.replaceVertexWeights(s,i,rt.Array(1,2),rt.Array(.25,.75))
 rt.skinOps.rigidVertex(s,1,True);record('original rigid true after weights')
 rt.skinOps.replaceVertexWeights(s,1,rt.Array(1,2),rt.Array(.25,.75));record('replace while rigid true')
 rt.skinOps.rigidVertex(s,2,False);rt.skinOps.replaceVertexWeights(s,2,rt.Array(1,2),rt.Array(.25,.75));rt.skinOps.rigidVertex(s,2,True);record('off replace on')
 rt.skinOps.rigidVertex(s,3,True);rt.skinOps.SetVertexWeights(s,3,rt.Array(1,2),rt.Array(.25,.75));record('set while rigid true')
 rt.skinOps.rigidVertex(s,4,True);rt.skinOps.unNormalizeVertex(s,4,False);rt.skinOps.replaceVertexWeights(s,4,rt.Array(1,2),rt.Array(.25,.75));record('rigid unnormalizeFalse replace')
 rt.skinOps.rigidVertex(s,5,False);rt.skinOps.unNormalizeVertex(s,5,False);rt.skinOps.replaceVertexWeights(s,5,rt.Array(1,2),rt.Array(.25,.75));rt.skinOps.rigidVertex(s,5,True);record('off unnormalizeFalse replace on')
except Exception:rows.append({'error':traceback.format_exc()})
finally:out.write_text(json.dumps(rows,indent=2),encoding='utf-8')
