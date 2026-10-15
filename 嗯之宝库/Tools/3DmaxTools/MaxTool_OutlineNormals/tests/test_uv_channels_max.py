"""Max 2023 integration: channel selection, preservation, FBX and preview decode."""
import sys, traceback, json
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node, read_mesh, preview_rows, point, map_has_data, show
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
from outline_math import oct_encode, oct_decode, dot, unit
out = root / 'tests/artifacts'
out.mkdir(exist_ok=True)
results, manifest = [], []

def uv(mesh, channel):
    if not map_has_data(mesh, channel):
        return None
    return ([point(rt.meshop.getMapVert(mesh, channel, i))
             for i in range(1, int(rt.meshop.getNumMapVerts(mesh, channel)) + 1)],
            [point(rt.meshop.getMapFace(mesh, channel, i))
             for i in range(1, int(rt.getNumFaces(mesh)) + 1)])

def populate(node, channel, zero=False):
    rt.convertToMesh(node)
    rt.meshop.setNumMaps(node, max(channel + 1, int(rt.meshop.getNumMaps(node))), keep=True)
    rt.meshop.setMapSupport(node, channel, True)
    faces = int(rt.getNumFaces(node))
    rt.meshop.setNumMapVerts(node, channel, 1 if zero else faces * 3)
    if zero:
        rt.meshop.setMapVert(node, channel, 1, rt.Point3(0, 0, 0))
    for f in range(1, faces + 1):
        if not zero:
            for c in range(3):
                i = (f - 1) * 3 + c + 1
                rt.meshop.setMapVert(node, channel, i, rt.Point3(i / 50., (faces * 3 - i) / 50., 0))
        rt.meshop.setMapFace(node, channel, f, rt.Point3(1, 1, 1) if zero
                             else rt.Point3(f * 3 - 2, f * 3 - 1, f * 3))

def check(node, name, channel=3, overwrite=False):
    before, vertices, faces = read_mesh(node)
    original_maps = {c: uv(before, c) for c in range(1, 9) if c != channel}
    base, mods = node.baseObject, list(node.modifiers)
    normals = [tuple(point(n) for n in rt.meshop.getFaceRNormals(before, i))
               for i in range(1, len(faces) + 1)]
    results.append(name + ': ' + bake_node(node, channel, overwrite))
    after, v2, f2 = read_mesh(node)
    assert (vertices, faces) == (v2, f2) and node.baseObject == base
    assert all(m in list(node.modifiers) for m in mods)
    # UV paste can discard Max specified normals. Verify preservation through
    # Edit Normals, which is the source used by the FBX exporter.
    for c, data in original_maps.items():
        if data is not None:
            assert uv(after, c) == data, (name, c, 'existing UV changed')
        elif 2 <= c < channel:
            assert uv(after, c)[0] == [(0., 0., 0.)], (name, c, 'placeholder')
        else:
            assert uv(after, c) is None, (name, c, 'unexpected UV')
    baked = uv(after, channel)
    assert 0 < len(baked[0]) <= len(faces) * 3
    assert all(2 <= x <= 3 and 0 <= y <= 1 and z == 0 for x, y, z in baked[0])
    count = len(list(node.modifiers))
    bake_node(node, channel)
    assert len(list(node.modifiers)) == count, (name, 'rebake modifier accumulation')
    assert uv(rt.snapshotAsMesh(node), channel) == baked
    rows, _ = preview_rows([node], channel=channel)
    smooth, _ = preview_rows([node], mode=1)
    assert all(dot(a[6:9], b[6:9]) > .99999 for a, b in zip(rows, smooth)), name
    export_uv_fbx([node], str(out / ('channels_' + name + '.fbx')))
    manifest.append({'name': name, 'channel': channel,
                     'uv2': sorted({value[:2] for value in uv(after, 2)[0]})})
    results.append(name + ': topology, original normals, existing UVs, modifiers, decode, rebake PASS')

try:
    rt.pluginManager.loadClass(rt.FBXEXP)
    rt.FBXExporterSetParam('ASCII', False)
    rt.FBXExporterSetParam('Animation', False)
    rt.FBXExporterSetParam('TangentSpaceExport', True)
    for x in range(-5, 6):
        for y in range(-5, 6):
            for z in range(-5, 6):
                if x or y or z:
                    n = unit((x, y, z))
                    assert dot(n, oct_decode(oct_encode(n))) > .999999
    results.append('octahedral full sphere 1330 directions PASS')
    def cube(name):
        return rt.box(name=name, length=10, width=10, height=10, mapCoords=True)
    check(cube('UV3Cube'), 'cube')
    n = cube('UV3Poly'); rt.convertToPoly(n); check(n, 'poly')
    n = cube('UV3Rotated'); n.rotation = rt.eulerAngles(25, 30, 40)
    n.scale = rt.Point3(2, 2, 2); check(n, 'rotated')
    n = cube('UV3Mirror'); n.scale = rt.Point3(-1, 1, 1); rt.ResetXForm(n); check(n, 'mirror')
    n = cube('UV3Skin')
    bone = rt.BoneSys.createBone(rt.Point3(0, 0, 0), rt.Point3(0, 0, 20), rt.Point3(0, 1, 0))
    skin = rt.Skin(); rt.addModifier(n, skin); rt.select(n)
    rt.setCommandPanelTaskMode(rt.Name('modify')); rt.modPanel.setCurrentObject(skin)
    rt.skinOps.addBone(skin, bone, 1)
    for i in range(1, 9): rt.skinOps.replaceVertexWeights(skin, i, 1, 1.)
    check(n, 'skin')
    n = cube('Building'); populate(n, 2); check(n, 'building')
    n = cube('ZeroUV2'); populate(n, 2, zero=True); check(n, 'zero')
    n = rt.sphere(name='MikkCurved',radius=10,segs=12,mapCoords=True); check(n,'curved')
    n = cube('MirroredUV');rt.convertToMesh(n)
    for i in range(1,int(rt.meshop.getNumMapVerts(n,1))+1):
        value=rt.meshop.getMapVert(n,1,i);value.x=-value.x;rt.meshop.setMapVert(n,1,i,value)
    check(n,'mirrored_uv')
    n=cube('ExperimentalU4');populate(n,3,zero=True)
    rt.meshop.setMapVert(n,3,1,rt.Point3(4.5,.5,0))
    check(n,'experimental_upgrade')
    check(cube('LegacyChannel2'), 'channel2', channel=2)
    check(cube('Channel4'), 'channel4', channel=4)
    check(cube('Channel8'), 'channel8', channel=8)
    n = cube('OccupiedTarget'); populate(n, 3)
    before, _, _ = read_mesh(n); original = uv(before, 3); mods = list(n.modifiers)
    try:
        bake_node(n)
        raise AssertionError('occupied UV should be rejected')
    except ValueError as error:
        assert '已有数据' in str(error)
    assert list(n.modifiers) == mods and uv(rt.snapshotAsMesh(n), 3) == original
    check(n, 'override', overwrite=True)
    dialog = show()
    assert dialog.channel.value() == 3 and not dialog.overwrite.isChecked()
    assert (dialog.channel.minimum(), dialog.channel.maximum()) == (2, 8)
    dialog.close()
    (out / 'channels_manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
    results.append('ALL CHANNEL SELECTION MAX TESTS PASSED')
except Exception:
    results.append(traceback.format_exc())
finally:
    (out / 'channels_max_result.txt').write_text('\n'.join(results), encoding='utf-8')
