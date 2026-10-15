import sys,json,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node,point,read_mesh
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
out=root/'tests/artifacts';results=[]
try:
 rt.pluginManager.loadClass(rt.FBXIMP)
 rt.importFile(str(root/'tests/fixtures/3333.fbx'),rt.Name('noPrompt'),using=rt.FBXIMP)
 n=next(n for n in rt.objects if rt.superClassOf(n)==rt.GeometryClass)
 export_uv_fbx([n],str(out/'real_baseline.fbx'))
 results.append(bake_node(n))
 before_mods=len(list(n.modifiers))
 results.append(bake_node(n))
 assert len(list(n.modifiers))==before_mods
 from tl_outline_max import preview_rows
 a,_=preview_rows([n]);b,_=preview_rows([n],mode=1)
 from outline_math import dot
 assert min(dot(x[6:9],y[6:9]) for x,y in zip(a,b))>.99999
 results.append('rebake modifier count and Max preview PASS')
 results.append('mods='+str([(str(m.name),str(rt.classOf(m))) for m in n.modifiers]))
 for m in n.modifiers:
  if str(rt.classOf(m))=='Edit_Normals':
   rt.select(n);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(m)
   results.append('explicit='+str(m.GetNumNormals())+' unique='+str(len(set(point(m.GetNormal(i)) for i in range(1,int(m.GetNumNormals())+1)))))
 export_uv_fbx([n],str(out/'real_rebaked.fbx'))
 results.append('exported fresh bake')
except Exception:results.append(traceback.format_exc())
finally:(out/'real_max_result.txt').write_text('\n'.join(results),encoding='utf-8')
