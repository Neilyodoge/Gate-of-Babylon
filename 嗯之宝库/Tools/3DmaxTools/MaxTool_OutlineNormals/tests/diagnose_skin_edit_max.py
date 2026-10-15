"""Read autobackups in a separate Max process; never save the source scenes."""
import sys, json, traceback, hashlib
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node, point
out = root / 'tests/artifacts/skin_edit_diagnosis.json'
results = []

def skin_info(node, skin):
    # Saved scenes may hide the mesh or pin another modifier stack.
    rt.unhide(node)
    rt.unfreeze(node)
    rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin, node=node)
    rt.windows.processPostedMessages()
    weights = []
    for i in range(1, int(rt.skinOps.GetNumberVertices(skin)) + 1):
        weights.append([(int(rt.skinOps.GetVertexWeightBoneID(skin, i, j)),
                         float(rt.skinOps.GetVertexWeight(skin, i, j)))
                        for j in range(1, int(rt.skinOps.GetVertexWeightCount(skin, i)) + 1)])
    mesh = node.mesh
    return {'vertices': len(weights), 'bones': int(rt.skinOps.GetNumberBones(skin)),
            'weight_hash': hashlib.sha256(json.dumps(weights).encode()).hexdigest(),
            'enabled': bool(skin.enabled),
            'positions': [point(rt.getVert(mesh, i)) for i in range(1, int(rt.getNumVerts(mesh)) + 1)]}

try:
    for name in ('AutoBackup04.max', 'AutoBackup03.max', 'AutoBackup02.max', 'AutoBackup01.max'):
        rt.loadMaxFile('C:/Users/admin/Documents/3ds Max 2023/autoback/' + name, quiet=True)
        scene = {'file': name, 'meshes': []}
        results.append(scene)
        for node in list(rt.objects):
            if rt.superClassOf(node) != rt.GeometryClass:
                continue
            skins = [m for m in node.modifiers if str(rt.classOf(m)) == 'Skin']
            item = {'name': str(node.name), 'base': str(rt.classOf(node.baseObject)),
                    'stack': [{'name': str(m.name), 'class': str(rt.classOf(m)), 'enabled': bool(m.enabled)}
                              for m in node.modifiers]}
            scene['meshes'].append(item)
            if not skins:
                continue
            skin = skins[0]
            try:
                before = skin_info(node, skin)
                item['message'] = bake_node(node)
                item['panel_after_bake'] = str(rt.classOf(rt.modPanel.getCurrentObject()))
                item['skin_exists'] = skin in list(node.modifiers)
                after = skin_info(node, skin)
                item['weights_equal'] = before['weight_hash'] == after['weight_hash']
                item['max_position_delta'] = max((max(abs(x-y) for x,y in zip(a,b))
                                                  for a,b in zip(before['positions'], after['positions'])), default=0)
                before.pop('positions'); after.pop('positions')
                item['before'], item['after'] = before, after
            except Exception:
                item['error'] = traceback.format_exc()
        out.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
except Exception:
    results.append({'error': traceback.format_exc()})
finally:
    out.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
