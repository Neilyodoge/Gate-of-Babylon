from pathlib import Path
import sys
import pymxs
from pymxs import runtime as rt

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import tl_outline_max as tool

out = root / 'tests' / 'artifacts' / 'skin_bake_hold.txt'
rows = []

if rt.theHold.Holding():
    rt.theHold.Accept('MaxBatch test setup')

node = rt.box(mapCoords=True)
rt.convertToMesh(node)
rt.meshop.setNumMaps(node, 3, keep=True)
rt.meshop.setMapSupport(node, 2, True)
rt.meshop.defaultMapFaces(node, 2)

skin = rt.Skin()
rt.addModifier(node, skin)
rt.select(node)
rt.setCommandPanelTaskMode(rt.Name('modify'))
rt.modPanel.setCurrentObject(skin)
bones = [
    rt.BoneSys.createBone(
        rt.Point3(x, 0, 0),
        rt.Point3(x, 0, 20),
        rt.Point3(0, 1, 0))
    for x in (0, 10)
]
for bone in bones:
    rt.skinOps.addBone(skin, bone, 1)
for i in range(1, int(rt.skinOps.GetNumberVertices(skin)) + 1):
    rt.skinOps.replaceVertexWeights(
        skin, i, rt.Array(1, 2), rt.Array(i / 12.0, 1.0 - i / 12.0))
if rt.theHold.Holding():
    rt.theHold.Accept('Test Skin setup')

dialog = tool.show()
rt.select(node)
rt.clearUndoBuffer()
rt.modPanel.setCurrentObject(skin)
rt.subObjectLevel = 1
if rt.theHold.Holding():
    rt.theHold.Accept('Test Skin panel setup')
rt.modPanel.setCurrentObject(skin)
rt.subObjectLevel = 1
rows.append(
    'before: holding=%s current=%s class=%s level=%s mode=%s' % (
        rt.theHold.Holding(),
        rt.modPanel.getCurrentObject(),
        rt.classOf(rt.modPanel.getCurrentObject()),
        int(rt.subObjectLevel or 0),
        rt.getCommandPanelTaskMode()))

dialog.character_mode.setChecked(True)
dialog.bake()
rows.append(
    'after: holding=%s current=%s class=%s level=%s mode=%s' % (
        rt.theHold.Holding(),
        rt.modPanel.getCurrentObject(),
        rt.classOf(rt.modPanel.getCurrentObject()),
        int(rt.subObjectLevel or 0),
        rt.getCommandPanelTaskMode()))
rows.append('status=' + dialog.status.text())
rows.append('log=' + dialog.log.toPlainText())
out.write_text('\n'.join(rows), encoding='utf-8')
