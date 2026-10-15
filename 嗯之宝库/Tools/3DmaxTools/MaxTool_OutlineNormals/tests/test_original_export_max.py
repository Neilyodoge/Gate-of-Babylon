"""Bake the untouched original FBX in a separate Max process; preserve its source."""
import sys
import traceback
from pathlib import Path

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node, read_mesh, map_has_data, point
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx

out = root / 'tests/artifacts'
results = []
try:
    source = root / 'tests/fixtures/C_MX_CJ_jindiao_1.fbx'
    rt.pluginManager.loadClass(rt.FBXIMP)
    rt.importFile(str(source), rt.Name('noPrompt'), using=rt.FBXIMP)
    node = next(n for n in rt.objects if rt.superClassOf(n) == rt.GeometryClass)
    mesh, vertices, faces = read_mesh(node)
    assert not map_has_data(mesh, 3), 'Original already contains UV3'
    uv1 = [point(rt.meshop.getMapVert(mesh, 1, i))
           for i in range(1, int(rt.meshop.getNumMapVerts(mesh, 1)) + 1)]
    uv2 = [point(rt.meshop.getMapVert(mesh, 2, i))
           for i in range(1, int(rt.meshop.getNumMapVerts(mesh, 2)) + 1)]
    export_uv_fbx([node], str(out / 'original_baseline.fbx'))
    results.append(bake_node(node))
    after, v2, f2 = read_mesh(node)
    assert vertices == v2 and faces == f2
    assert uv1 == [point(rt.meshop.getMapVert(after, 1, i)) for i in range(1, len(uv1) + 1)]
    assert uv2 == [point(rt.meshop.getMapVert(after, 2, i)) for i in range(1, len(uv2) + 1)]
    count = len(list(node.modifiers))
    results.append(bake_node(node))
    assert len(list(node.modifiers)) == count
    export_uv_fbx([node], str(out / 'original_baked.fbx'))
    results.append('Untouched original: geometry, UV1, UV2, repeat bake PASS')
except Exception:
    results.append(traceback.format_exc())
finally:
    (out / 'original_max_result.txt').write_text('\n'.join(results), encoding='utf-8')
