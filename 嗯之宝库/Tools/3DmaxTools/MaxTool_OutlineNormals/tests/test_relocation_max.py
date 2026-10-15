"""Run Launch.ms from its moved Unicode path in an independent Max process."""
import sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
from pymxs import runtime as rt
from PySide2 import QtWidgets
out = root / 'tests/artifacts/relocation_max_result.txt'
rows = []
try:
    assert (root / 'Unity/TL_RenderFeature_Outline.mat').is_file()
    assert (root / 'Unity/TL_RF_Outline.shader').is_file()
    for name in ('3333.fbx', 'C_MX_CJ_jindiao_1.fbx', 'Hero_Prisoner.fbx'):
        assert (root / 'tests/fixtures' / name).is_file()
    rt.fileIn(str(root / 'Launch.ms'))
    app = QtWidgets.QApplication.instance()
    app.processEvents()
    dialogs = [w for w in app.topLevelWidgets() if w.isVisible()
               and w.windowTitle() == 'TL 描边 · 平滑法线写入 UV']
    assert len(dialogs) == 1
    dialog = dialogs[0]
    assert hasattr(dialog, 'status') and not hasattr(dialog, 'export_button')
    node = rt.box(mapCoords=True)
    skin = rt.Skin()
    rt.addModifier(node, skin)
    bone = rt.BoneSys.createBone(rt.Point3(0,0,0), rt.Point3(0,0,20), rt.Point3(0,1,0))
    rt.select(node); rt.setCommandPanelTaskMode(rt.Name('modify'))
    rt.modPanel.setCurrentObject(skin)
    rt.skinOps.addBone(skin, bone, 1)
    for i in range(1,9):
        rt.skinOps.replaceVertexWeights(skin, i, 1, 1.)
    dialog.bake()
    assert '#63d58a' in dialog.status.styleSheet(), dialog.log.toPlainText()
    assert skin in list(node.modifiers) and bool(skin.enabled)
    assert rt.modPanel.getCurrentObject() == skin
    assert int(rt.skinOps.GetNumberVertices(skin)) == 8
    import tl_outline_max as tool
    tool.verify_uv_channel(node, 3)
    rt.clearSelection(); dialog.bake()
    assert '#ff5b5b' in dialog.status.styleSheet()
    rows.append('Launch from Unicode destination, MikkTSpace DLL, UV3 bake, retained editable Skin, green/red status PASS')
    rows.append('Local Unity resources and independent FBX fixtures PASS')
    rows.append('ALL RELOCATION TESTS PASSED')
    dialog.close()
except Exception:
    rows.append(traceback.format_exc())
finally:
    out.write_text('\n'.join(rows), encoding='utf-8')
