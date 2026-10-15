import sys,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node,point
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
out=root/'tests/artifacts';r=[]
try:
 n=rt.box(mapCoords=True);rt.convertToPoly(n);bake_node(n)
 rt.showEndResult=True;rt.update(n);rt.redrawViews()
 export_uv_fbx([n],str(out/'probe_poly_update.fbx'))
 bake_node(n);rt.showEndResult=True;rt.update(n);rt.redrawViews()
 export_uv_fbx([n],str(out/'probe_poly_rebake.fbx'))
 # Enable preserve mapping and specified normals on Turn to Mesh if available.
 for m in n.modifiers:
  r.append(str(m.name)+' '+str(list(rt.getPropNames(m))))
 r.append('node properties '+str(list(rt.getPropNames(n))))
 rt.collapseStack(n)
 export_uv_fbx([n],str(out/'probe_poly_collapse.fbx'))
except Exception:r.append(traceback.format_exc())
finally:(out/'probe_modifier_result.txt').write_text('\n'.join(r),encoding='utf-8')
