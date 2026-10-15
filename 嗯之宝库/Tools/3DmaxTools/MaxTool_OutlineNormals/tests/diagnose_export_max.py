"""Read an autosave in an isolated Max batch process; never save over it."""
import sys, json, traceback, collections
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
sys.path.append(str(root / 'tests'))
from pymxs import runtime as rt
from tl_outline_max import read_mesh, point, map_has_data, mesh_frames
from outline_math import smooth_normals, dot, oct_decode
from tangent_space import from_tangent
out = root / 'tests/artifacts'
result = []
try:
    rt.loadMaxFile('C:/Users/admin/Documents/3ds Max 2023/autoback/AutoBackup08.max', quiet=True)
    result.append({'file': str(rt.maxFilePath) + str(rt.maxFileName), 'selected': [str(n.name) for n in rt.selection]})
    for node in rt.objects:
        if rt.superClassOf(node) != rt.GeometryClass:
            continue
        row = {'name': str(node.name), 'base': str(rt.classOf(node.baseObject)),
               'mods': [{'name': str(m.name), 'type': str(rt.classOf(m))} for m in node.modifiers]}
        try:
            mesh, vertices, faces = read_mesh(node)
            row.update(verts=len(vertices), triangles=len(faces), unique_positions=len(set(vertices)))
            normals, skipped = smooth_normals(vertices, faces)
            corner_normals = [[point(n) for n in rt.meshop.getFaceRNormals(mesh, fi)] for fi in range(1,len(faces)+1)]
            (out / 'diagnose_max_normals.json').write_text(json.dumps(corner_normals), encoding='utf-8')
            row['skipped'] = skipped
            row['maps'] = {str(c): int(rt.meshop.getNumMapVerts(mesh, c)) for c in range(1, 9) if map_has_data(mesh, c)}
            if map_has_data(mesh, 3):
                frames = mesh_frames(mesh, vertices, faces)
                dots = []
                for fi, face in enumerate(faces):
                    mf = point(rt.meshop.getMapFace(mesh, 3, fi+1))
                    for ci, vi in enumerate(face):
                        uv = point(rt.meshop.getMapVert(mesh, 3, int(mf[ci])))
                        if 1.9999 <= uv[0] <= 3.0001:
                            dots.append(dot(normals[vi], from_tangent(oct_decode(uv), frames[fi*3+ci])))
                row['decoded_min_dot'] = min(dots) if dots else None
                row['decoded_count'] = len(dots)
            # Export this autosave object into the test folder for an independent Unity import.
            from max_test_export import export_uv_fbx
            export_uv_fbx([node], str(out / ('diagnose_' + str(node.handle) + '.fbx')))
            row['export'] = 'diagnose_' + str(node.handle) + '.fbx'
            rt.FBXExporterSetParam('SmoothingGroups',False)
            rt.select(node)
            rt.exportFile(str(out/'diagnose_no_sg.fbx'),rt.Name('noPrompt'),selectedOnly=True,using=rt.FBXEXP)
            en=rt.Edit_Normals();rt.addModifier(node,en)
            rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(en)
            en.MakeExplicit(selection=rt.execute('#{1..'+str(en.GetNumNormals())+'}'))
            row['explicit_normal_count']=en.GetNumNormals()
            rt.FBXExporterSetParam('SmoothingGroups',True)
            rt.exportFile(str(out/'diagnose_explicit.fbx'),rt.Name('noPrompt'),selectedOnly=True,using=rt.FBXEXP)
            (out / ('diagnose_' + str(node.handle) + '.json')).write_text(json.dumps({'positions': vertices, 'faces': faces, 'smooth': normals}), encoding='utf-8')
        except Exception:
            row['error'] = traceback.format_exc()
        result.append(row)
except Exception:
    result.append({'error': traceback.format_exc()})
finally:
    (out / 'diagnose_max.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
