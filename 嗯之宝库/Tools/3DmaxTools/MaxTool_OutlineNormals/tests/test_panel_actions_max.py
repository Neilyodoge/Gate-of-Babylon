"""Verify UV2 clearing preserves the other mesh data and native panel shortcuts."""
import sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
from tl_outline_max import (bake_node, clear_second_uv, point,
                            map_has_data, read_render_normals, show)
sys.path.append(str(root / 'tests'))
from max_test_export import export_uv_fbx
out = root / 'tests/artifacts'
results = []


def mapping(mesh, channel):
    if not map_has_data(mesh, channel):
        return None
    return ([point(rt.meshop.getMapVert(mesh, channel, i))
             for i in range(1, int(rt.meshop.getNumMapVerts(mesh, channel)) + 1)],
            [point(rt.meshop.getMapFace(mesh, channel, i))
             for i in range(1, int(rt.getNumFaces(mesh)) + 1)])


def check(node, name):
    before = rt.snapshotAsMesh(node)
    vertices = [point(rt.getVert(before, i)) for i in range(1, int(rt.getNumVerts(before)) + 1)]
    faces = [point(rt.getFace(before, i)) for i in range(1, int(rt.getNumFaces(before)) + 1)]
    channels = {c: mapping(before, c) for c in (-2, 0, 1, 3, 4)}
    normals = read_render_normals(node, len(faces))
    original = list(node.modifiers)
    base = node.baseObject
    rt.select(node)
    with pymxs.undo(True, 'Test Clear UV2'):
        results.append(name + ': ' + clear_second_uv(node))
    after = rt.snapshotAsMesh(node)
    assert base == node.baseObject and all(m in list(node.modifiers) for m in original)
    assert vertices == [point(rt.getVert(after, i)) for i in range(1, len(vertices) + 1)]
    assert faces == [point(rt.getFace(after, i)) for i in range(1, len(faces) + 1)]
    assert mapping(after, 2) == ([(0., 0., 0.)], [(1., 1., 1.)] * len(faces))
    assert channels == {c: mapping(after, c) for c in channels}, name
    n2 = read_render_normals(node, len(faces))
    assert max(abs(a[j] - b[j]) for f1, f2 in zip(normals, n2)
               for a, b in zip(f1, f2) for j in range(3)) < 1e-6
    count = len(list(node.modifiers))
    clear_second_uv(node)
    assert len(list(node.modifiers)) == count, 'Repeated clear added modifiers'
    assert list(rt.selection) == [node]
    export_uv_fbx([node], str(out / ('panel_' + name + '.fbx')))
    results.append(name + ': geometry, normals, UV1/3/4, color, modifiers, selection, repeated clear PASS')


try:
    for name, poly in [('mesh', False), ('poly', True)]:
        n = rt.box(mapCoords=True, length=10, width=10, height=10)
        if poly:
            rt.convertToPoly(n)
        else:
            rt.convertToMesh(n)
        # Populate an actual lightmap channel, then bake UV3.
        if not poly:
            rt.meshop.setNumMaps(n, 3, keep=True)
            rt.meshop.setMapSupport(n, 2, True)
            rt.meshop.defaultMapFaces(n, 2)
        bake_node(n)
        check(n, name)
    n = rt.box(mapCoords=True, length=10, width=10, height=10)
    skin = rt.Skin()
    rt.addModifier(n, skin)
    bone = rt.BoneSys.createBone(rt.Point3(0, 0, 0), rt.Point3(0, 0, 20), rt.Point3(0, 1, 0))
    rt.select(n)
    rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin)
    rt.skinOps.addBone(skin, bone, 1)
    for i in range(1, 9):
        rt.skinOps.replaceVertexWeights(skin, i, 1, 1.)
    bake_node(n)
    # Add UV2 data by baking to that channel as well; UV3 must survive clearing.
    bake_node(n, channel=2, overwrite=True)
    check(n, 'skin')
    rt.pluginManager.loadClass(rt.FBXIMP)
    rt.importFile(str(root / 'tests/fixtures/3333.fbx'),
                  rt.Name('noPrompt'), using=rt.FBXIMP)
    n = next(n for n in rt.selection if rt.superClassOf(n) == rt.GeometryClass)
    check(n, '3333')
    dialog = show()
    from PySide2 import QtWidgets
    app = QtWidgets.QApplication.instance()
    app.processEvents()
    assert not hasattr(dialog, 'export_button')
    assert dialog.clear_uv2_button.geometry().y() == dialog.channels_button.geometry().y()
    assert dialog.clear_uv2_button.geometry().x() < dialog.channels_button.geometry().x()
    dialog.grab().save(str(out / 'panel_actions.png'))
    dialog.channels_button.click()
    app.processEvents()
    assert '打开通道信息失败' not in dialog.log.toPlainText()
    results.append('Two action buttons, export removed, native Channel Info opened PASS')
    dialog.close()
except Exception:
    results.append(traceback.format_exc())
finally:
    (out / 'panel_actions_max_result.txt').write_text('\n'.join(results), encoding='utf-8')
