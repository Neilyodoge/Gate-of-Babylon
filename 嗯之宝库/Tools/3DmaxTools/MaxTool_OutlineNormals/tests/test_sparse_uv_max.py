"""Probe FBX channel numbering if the intermediate Max channel is absent."""
import sys, traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
out=root/'tests/artifacts'
try:
 n=rt.box(mapCoords=True,length=10,width=10,height=10)
 bake_node(n);rt.convertToMesh(n);rt.meshop.setMapSupport(n,2,False)
 export_uv_fbx([n],str(out/'channels_sparse.fbx'))
 (out/'sparse_max_result.txt').write_text('Exported UV1 + UV3, no UV2 support',encoding='utf-8')
except Exception:
 (out/'sparse_max_result.txt').write_text(traceback.format_exc(),encoding='utf-8')
