"""在同一 Max Python 会话模拟旧模块缓存，验证重复运行 Launch.ms。"""
import sys
import types
import traceback
from pathlib import Path
from pymxs import runtime as rt

root = Path(__file__).resolve().parents[1]
out = root / 'tests/artifacts/launcher_reload_result.txt'
results = []
try:
    count = len(list(rt.objects))
    stale = types.ModuleType('outline_math')
    stale.__file__ = str(root / 'outline_math.py')
    stale.smooth_normals = lambda *args: 'OLD'
    sys.modules['outline_math'] = stale
    rt.fileIn(str(root / 'Launch.ms'))
    current = sys.modules['outline_math']
    assert current is not stale
    assert current.oct_encode((0, 0, 1)) == (2.5, .5)
    assert current.oct_decode((2.5, .5)) == (0, 0, 1)
    results.append('Old cached module replaced with current source PASS')
    del current.oct_encode
    rt.fileIn(str(root / 'Launch.ms'))
    assert sys.modules['outline_math'].oct_encode((0, 0, 1)) == (2.5, .5)
    assert len(list(rt.objects)) == count
    results.append('Repeated Launch.ms refreshes source without restarting Max or changing scene PASS')
except Exception:
    results.append(traceback.format_exc())
finally:
    out.parent.mkdir(exist_ok=True)
    out.write_text('\n'.join(results), encoding='utf-8')
