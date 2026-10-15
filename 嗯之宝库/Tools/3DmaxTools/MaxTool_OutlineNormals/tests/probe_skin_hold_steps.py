from pathlib import Path
import pymxs
from pymxs import runtime as rt

out = Path(__file__).resolve().parent / 'artifacts' / 'skin_hold_steps.txt'
rows = []


def state(label):
    rows.append(
        '%s: holding=%s suspended=%s level=%s current=%s' % (
            label,
            rt.theHold.Holding(),
            rt.theHold.IsSuspended(),
            int(rt.subObjectLevel or 0),
            str(rt.modPanel.getCurrentObject())))


if rt.theHold.Holding():
    rt.theHold.Accept('MaxBatch test setup')
state('start')

node = rt.box(mapCoords=True)
rt.convertToMesh(node)
state('box')

skin = rt.Skin()
rt.addModifier(node, skin)
state('add skin')

rt.select(node)
rt.setCommandPanelTaskMode(rt.Name('modify'))
rt.modPanel.setCurrentObject(skin)
state('set current')

bones = [
    rt.BoneSys.createBone(
        rt.Point3(x, 0, 0),
        rt.Point3(x, 0, 20),
        rt.Point3(0, 1, 0))
    for x in (0, 10)
]
state('create bones')

for bone in bones:
    rt.skinOps.addBone(skin, bone, 1)
state('add bones')

for i in range(1, int(rt.skinOps.GetNumberVertices(skin)) + 1):
    rt.skinOps.replaceVertexWeights(
        skin,
        i,
        rt.Array(1, 2),
        rt.Array(i / 12.0, 1.0 - i / 12.0))
state('replace weights')

if rt.theHold.Holding():
    rt.theHold.Accept('Test Skin setup')
state('accept setup')

rt.subObjectLevel = 1
state('subobject 1')

rt.clearUndoBuffer()
state('clear undo')

rt.subObjectLevel = 0
state('subobject 0')

if rt.theHold.Holding():
    rt.theHold.Accept('Finish Skin edit')
state('accept finish')

out.write_text('\n'.join(rows), encoding='utf-8')
