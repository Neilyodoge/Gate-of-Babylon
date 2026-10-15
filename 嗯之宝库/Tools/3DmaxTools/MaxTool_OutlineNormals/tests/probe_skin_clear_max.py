"""Inspect ChannelInfo clear modifiers in an isolated Max scene."""
import sys, json, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node, map_has_data, point
rows = []
def props(modifier):
    values = {}
    for p in ('enabled', 'enabledInViews', 'enabledInRenders'):
        try:
            values[p] = str(getattr(modifier, p))
        except Exception:
            pass
    for p in rt.getPropNames(modifier):
        try:
            values[str(p)] = str(rt.getProperty(modifier, p))
        except RuntimeError:
            pass
    return values
def report(node, label):
    mesh = rt.snapshotAsMesh(node)
    rows.append({'label': label,
                 'maps': {c: int(rt.meshop.getNumMapVerts(mesh, c)) for c in range(1, 5) if map_has_data(mesh, c)},
                 'modifiers': [{'name': str(m.name), 'type': str(rt.classOf(m)),
                                'properties': props(m)}
                               for m in node.modifiers]})
try:
    n = rt.box(mapCoords=True)
    skin = rt.Skin()
    rt.addModifier(n, skin)
    bone = rt.BoneSys.createBone(rt.Point3(0, 0, 0), rt.Point3(0, 0, 20), rt.Point3(0, 1, 0))
    rt.select(n)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin)
    rt.skinOps.addBone(skin, bone, 1)
    for i in range(1, 9):
        rt.skinOps.replaceVertexWeights(skin, i, 1, 1.0)
    report(n, 'before')
    rows.append({'bake': bake_node(n)})
    rt.ChannelInfo.ClearChannel(n, 2)
    rt.ChannelInfo.ClearChannel(n, 3)
    report(n, 'after UV2 and UV3 clears above Skin')
    skin.enabled = False
    try:
        rows.append({'rebakeDisabledSkin': bake_node(n)})
    except Exception:
        rows.append({'rebakeDisabledSkinError': traceback.format_exc()})
    report(n, 'after rebake with Skin disabled')
    skin.enabled = True
    for m in n.modifiers:
        if str(rt.classOf(m)) == 'UVW_Mapping_Clear':
            m.enabled = False
    report(n, 'clears disabled')
except Exception:
    rows.append({'error': traceback.format_exc()})
finally:
    (root / 'tests/artifacts/probe_skin_clear_max.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
