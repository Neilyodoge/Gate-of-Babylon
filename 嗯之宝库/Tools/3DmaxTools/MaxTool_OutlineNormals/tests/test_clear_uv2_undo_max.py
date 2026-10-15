import sys, traceback
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root))
import pymxs
from pymxs import runtime as rt
from tl_outline_max import show, point
out = root / 'tests/artifacts/clear_uv2_undo_result.txt'
try:
    n = rt.box(mapCoords=True)
    rt.convertToMesh(n)
    rt.meshop.setNumMaps(n, 3, keep=True)
    rt.meshop.setMapSupport(n, 2, True)
    rt.meshop.defaultMapFaces(n, 2)
    before = [point(rt.meshop.getMapVert(n, 2, i)) for i in range(1, int(rt.meshop.getNumMapVerts(n, 2)) + 1)]
    rt.clearUndoBuffer()
    rt.select(n)
    dialog = show()
    dialog.clear_uv2_button.click()
    assert '清空失败' not in dialog.log.toPlainText()
    rt.execute('max undo')
    mesh = rt.snapshotAsMesh(n)
    after = [point(rt.meshop.getMapVert(mesh, 2, i)) for i in range(1, int(rt.meshop.getNumMapVerts(mesh, 2)) + 1)]
    assert after == before and len(list(n.modifiers)) == 0
    dialog.close()
    out.write_text('Undo restores original UV2 and removes generated modifiers PASS', encoding='utf-8')
except Exception:
    out.write_text(traceback.format_exc(), encoding='utf-8')
