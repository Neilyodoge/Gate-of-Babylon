"""Focused review diagnostics: UI bake undo and Skin baking after a bone rotation."""
import json
import sys
import traceback
from pathlib import Path

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from tl_outline_max import show, bake_node, point, read_render_normals, read_mesh, preview_rows
from outline_math import dot

rows = []
try:
    node = rt.box(name='ReviewBakeUndo', mapCoords=True)
    rt.convertToMesh(node)
    before = list(node.modifiers)
    rt.clearUndoBuffer()
    rt.select(node)
    dialog = show()
    dialog.bake()
    rows.append({'case': 'UI bake before undo', 'log': dialog.log.toPlainText(),
                 'modifiers': [str(m.name) for m in node.modifiers]})
    rt.execute('max undo')
    rows.append({'case': 'UI bake after one undo',
                 'restored': list(node.modifiers) == before,
                 'modifiers': [str(m.name) for m in node.modifiers]})
    dialog.close()

    # The original-normal preview should not need texture UVs, but currently does.
    node = rt.box(name='ReviewNoTextureUV', mapCoords=False)
    try:
        preview_rows([node], mode=2)
        rows.append({'case': 'Original-normal preview without UV1', 'success': True})
    except Exception as error:
        rows.append({'case': 'Original-normal preview without UV1', 'success': False, 'error': str(error)})

    from PySide2 import QtWidgets
    title = 'TL 描边 · 平滑法线写入 UV'
    windows = lambda: [w for w in QtWidgets.QApplication.topLevelWidgets()
                      if w.windowTitle() == title and w.isVisible()]
    for w in windows():
        w.close()
    rt.fileIn(str(root / 'Launch.ms'))
    first = len(windows())
    rt.fileIn(str(root / 'Launch.ms'))
    rows.append({'case': 'Repeat Launch.ms', 'afterFirstLaunch': first,
                 'afterSecondLaunch': len(windows())})
    for w in windows():
        w.close()

    for posed in (False, True):
        node = rt.box(name='ReviewSkin' + str(posed), mapCoords=True)
        bone = rt.BoneSys.createBone(rt.Point3(0, 0, 0), rt.Point3(0, 0, 20), rt.Point3(0, 1, 0))
        skin = rt.Skin()
        rt.addModifier(node, skin)
        rt.select(node)
        rt.setCommandPanelTaskMode(rt.Name('modify'))
        rt.modPanel.setCurrentObject(skin)
        rt.skinOps.addBone(skin, bone, 1)
        for i in range(1, 9):
            rt.skinOps.replaceVertexWeights(skin, i, 1, 1.0)
        initial = bone.rotation
        _, _, faces = read_mesh(node)
        original = read_render_normals(node, len(faces))
        if posed:
            rt.rotate(bone, rt.eulerAngles(0, 0, 90))
        before = read_render_normals(node, len(faces))
        posed_dot = min(dot(a, b) for fa, fb in zip(original, before) for a, b in zip(fa, fb))
        message = bake_node(node)
        after = read_render_normals(node, len(faces))
        current_dot = min(dot(a, b) for fa, fb in zip(before, after) for a, b in zip(fa, fb))
        bone.rotation = initial
        restored = read_render_normals(node, len(faces))
        bind_dot = min(dot(a, b) for fa, fb in zip(original, restored) for a, b in zip(fa, fb))
        rows.append({'case': 'Skin posed=' + str(posed), 'message': message,
                     'poseNormalMinDot': posed_dot,
                     'samePoseNormalMinDot': current_dot, 'returnToBindNormalMinDot': bind_dot,
                     'modifiers': [str(m.name) for m in node.modifiers]})
except Exception:
    rows.append({'error': traceback.format_exc()})
finally:
    (root / 'tests/artifacts/review_edges_max.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
