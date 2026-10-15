import sys,traceback,math
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node,read_mesh,preview_rows,point
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
from outline_math import oct_encode,oct_decode,dot,unit
out=root/'tests/artifacts';results=[]
def uv(m,c):
 return ([point(rt.meshop.getMapVert(m,c,i)) for i in range(1,int(rt.meshop.getNumMapVerts(m,c))+1)],
         [point(rt.meshop.getMapFace(m,c,i)) for i in range(1,int(rt.getNumFaces(m))+1)])
def check(n,name):
 before,v,f=read_mesh(n);uv1=uv(before,1);base=n.baseObject;mods=list(n.modifiers)
 groups=[rt.getFaceSmoothGroup(before,i) for i in range(1,len(f)+1)]
 results.append(name+': '+bake_node(n,2,overwrite=True))
 after,v2,f2=read_mesh(n)
 assert (v,f)==(v2,f2) and uv(after,1)==uv1 and n.baseObject==base
 assert all(m in list(n.modifiers) for m in mods)
 assert [rt.getFaceSmoothGroup(after,i) for i in range(1,len(f)+1)]==groups
 baked=uv(after,2)
 assert all(4<=x<=5 and 0<=y<=1 for x,y,z in baked[0])
 bake_node(n,2);assert uv(rt.snapshotAsMesh(n),2)==baked
 rows,_=preview_rows([n],units_per_meter=1);smooth,_=preview_rows([n],mode=1,units_per_meter=1)
 assert all(dot(a[6:9],b[6:9])>.99999 for a,b in zip(rows,smooth)),name+' preview decode mismatch'
 rt.FBXExporterSetParam('ASCII',False);rt.FBXExporterSetParam('Animation',False);rt.FBXExporterSetParam('TangentSpaceExport',True)
 results.append(export_uv_fbx([n],str(out/('uv2_'+name+'.fbx'))))
 results.append(name+': UV0 preserved; UV2 smooth normals; topology/smoothing/modifiers/preview/rebake PASS')
try:
 rt.pluginManager.loadClass(rt.FBXEXP)
 for x in range(-5,6):
  for y in range(-5,6):
   for z in range(-5,6):
    if x or y or z:
     n=unit((x,y,z));assert dot(n,oct_decode(oct_encode(n)))>.999999
 results.append('octahedral full sphere 1330 directions PASS')
 n=rt.box(name='UV2Cube',length=10,width=10,height=10,mapCoords=True);check(n,'cube')
 n=rt.box(name='UV2Poly',length=10,width=10,height=10,mapCoords=True);rt.convertToPoly(n);check(n,'poly')
 n=rt.box(name='UV2Rotate',length=10,width=10,height=10,mapCoords=True);n.rotation=rt.eulerAngles(25,30,40);n.scale=rt.Point3(2,2,2);check(n,'rotated')
 n=rt.box(name='UV2Mirror',length=10,width=10,height=10,mapCoords=True);n.scale=rt.Point3(-1,1,1);rt.ResetXForm(n);check(n,'mirror')
 n=rt.box(name='UV2Skin',length=10,width=10,height=10,mapCoords=True)
 bone=rt.BoneSys.createBone(rt.Point3(0,0,0),rt.Point3(0,0,20),rt.Point3(0,1,0));skin=rt.Skin();rt.addModifier(n,skin);rt.select(n)
 rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(skin);rt.skinOps.addBone(skin,bone,1)
 for i in range(1,9):rt.skinOps.replaceVertexWeights(skin,i,1,1.)
 check(n,'skin')
 n=rt.box(name='UV2Existing',length=10,width=10,height=10,mapCoords=True);rt.convertToMesh(n)
 rt.meshop.setNumMaps(n,3,keep=True);rt.meshop.setMapSupport(n,2,True);rt.meshop.setNumMapVerts(n,2,1);rt.meshop.setMapVert(n,2,1,rt.Point3(.25,.75,0))
 for i in range(1,13):rt.meshop.setMapFace(n,2,i,rt.Point3(1,1,1))
 check(n,'existing')
 results.append('ALL UV2 MAX TESTS PASSED')
except Exception:results.append(traceback.format_exc())
finally:(out/'uv2_max_result.txt').write_text('\n'.join(results),encoding='utf-8')
