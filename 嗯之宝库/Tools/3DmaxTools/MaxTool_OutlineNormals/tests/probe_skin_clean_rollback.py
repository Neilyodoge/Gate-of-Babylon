import os
import sys
import traceback
from pathlib import Path

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool

out = root / 'tests/artifacts/skin_clean_rollback.txt'
rows = ['STARTED']

try:
    assert os.environ.get('TL_OUTLINE_BATCH_TEST') == '1'
    if rt.theHold.Holding():
        rt.theHold.Accept('MaxBatch test setup')

    box = rt.box(mapCoords=True)
    mesh = rt.snapshotAsMesh(box)
    rt.delete(box)
    points = [rt.getVert(mesh, i) for i in range(1, 9)]
    faces = [rt.getFace(mesh, i) for i in range(1, 13)]
    rt.meshop.setNumVerts(mesh, 9)
    rt.setVert(mesh, 1, rt.Point3(-20, -20, -20))
    for i, point in enumerate(points, 2):
        rt.setVert(mesh, i, point)
    for i, face in enumerate(faces, 1):
        rt.setFace(mesh, i, rt.Point3(face.x + 1, face.y + 1, face.z + 1))
    node = rt.mesh(mesh=mesh)

    skin = rt.Skin()
    rt.addModifier(node, skin)
    rt.select(node)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin)
    bones = [rt.BoneSys.createBone(rt.Point3(x, 0, 0), rt.Point3(x, 0, 20), rt.Point3(0, 1, 0))
             for x in (0, 10)]
    for bone in bones:
        rt.skinOps.addBone(skin, bone, 1)
    for i in range(1, int(rt.skinOps.GetNumberVertices(skin)) + 1):
        rt.skinOps.replaceVertexWeights(skin, i, rt.Array(1, 2), rt.Array(i / 12., 1 - i / 12.))
    if rt.theHold.Holding():
        rt.theHold.Accept('Test Skin setup')

    before = tool.skin_fingerprint(node, skin)
    real_verify = tool.verify_collapse

    def fail(*args):
        raise tool.CollapseError('Injected collapse data mismatch')

    tool.verify_collapse = fail
    dialog = tool.show()
    rt.select(node)
    dialog.clean_isolated()
    tool.verify_collapse = real_verify
    after = tool.skin_fingerprint(node, skin)
    rows.extend([
        'status=' + dialog.status.text(),
        'verts=' + str(int(rt.getNumVerts(node.mesh))),
        'holding=' + str(bool(rt.theHold.Holding())),
        'weights_equal=' + str(after == before),
    ])
except Exception:
    rows.append(traceback.format_exc())
finally:
    out.write_text('\n'.join(rows), encoding='utf-8')
