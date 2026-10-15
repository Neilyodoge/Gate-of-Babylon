"""Use the Prisoner source asset to verify the screenshot's Skin/UV-clear stack."""
import os, sys, traceback, json
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node, uv_corner_values, read_render_normals, verify_uv_channel, read_mesh, local_bake_data, point
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
from outline_math import dot, smooth_normals
out = root / 'tests/artifacts'
assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1', 'Disposable MaxBatch only'
rows = []
try:
    rt.pluginManager.loadClass(rt.FBXIMP)
    source = root / 'tests/fixtures/Hero_Prisoner.fbx'
    rt.importFile(str(source), rt.Name('noPrompt'), using=rt.FBXIMP)
    nodes = [n for n in rt.objects if rt.superClassOf(n) == rt.GeometryClass and 'Prisoner' in str(n.name)]
    assert nodes, 'Prisoner mesh missing'
    export_nodes = [n for n in rt.objects if rt.superClassOf(n) != rt.GeometryClass or n in nodes]
    export_uv_fbx(export_nodes,str(out/'prisoner_baseline.fbx'))
    for node in nodes:
        skin = next((m for m in node.modifiers if str(rt.classOf(m)) == 'Skin'), None)
        assert skin is not None, 'Source Prisoner has no Skin'
        rt.select(node);rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(skin)
        rows.append('Skin bones=%s exportSkin=%s' % (rt.skinOps.getNumberBones(skin),rt.FBXExporterGetParam('Skin')))
        skin.enabled = False
        mesh = rt.snapshotAsMesh(node)
        rows.append('%s: source vertices=%s triangles=%s' % (node.name, rt.getNumVerts(mesh), rt.getNumFaces(mesh)))
        original_uv1 = uv_corner_values(mesh, 1)
        skin.enabled = True
        rows.append(bake_node(node))
        skin.enabled = False
        original_normals = read_render_normals(node, int(rt.getNumFaces(rt.snapshotAsMesh(node))))
        skin.enabled = True
        before_count = len(list(node.modifiers))
        before_uv3 = uv_corner_values(rt.snapshotAsMesh(node), 3)
        # Match screenshot: UVW clears above Skin, existing baked layers below it.
        rt.ChannelInfo.ClearChannel(node,2)
        rt.ChannelInfo.ClearChannel(node,3)
        clears = list(node.modifiers)[:2]
        skin.enabled = False
        rows.append(bake_node(node))
        assert not bool(skin.enabled), 'User Skin state changed'
        assert all(not bool(m.enabled) for m in clears)
        assert len(list(node.modifiers)) == before_count + 2, 'Rebake added redundant layers'
        verify_uv_channel(node,3)
        after = rt.snapshotAsMesh(node)
        assert original_uv1 == uv_corner_values(after,1)
        assert before_uv3 == uv_corner_values(after,3)
        normals = read_render_normals(node,int(rt.getNumFaces(after)))
        normal_min = min(dot(a,b) for fa,fb in zip(original_normals,normals) for a,b in zip(fa,fb))
        rows.append('Normal diagnostic minDot=%s base=%s' % (normal_min,rt.classOf(node.baseObject)))
        assert normal_min > .99999
        local_mesh, verts, faces = read_mesh(node)
        local, frames, reference_normals = local_bake_data(node,local_mesh,verts,faces)
        (out/'prisoner_max_reference.json').write_text(json.dumps({'vertices':local,'faces':faces,
          'smooth':smooth_normals(local,faces)[0], 'normals':reference_normals,
          'uv3':uv_corner_values(local_mesh,3)}),encoding='utf-8')
        skin.enabled = True
        rows.append('%s: screenshot stack, disabled Skin, UV1/UV3, repeat modifier count PASS' % node.name)
    export_uv_fbx(export_nodes,str(out/'prisoner_stack.fbx'))
    rows.append('PRISONER STACK TEST PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    (out/'prisoner_stack_max_result.txt').write_text('\n'.join(rows),encoding='utf-8')
