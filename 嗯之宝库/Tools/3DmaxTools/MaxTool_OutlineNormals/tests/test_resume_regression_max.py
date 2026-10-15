"""Resume regression using the authoritative tool directory in disposable MaxBatch."""
import os, runpy, traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1]
assert os.environ.get('TL_OUTLINE_BATCH_TEST')=='1'
out=root/'tests/artifacts/resume_regression_result.txt'
rows=['STARTED']
try:
    for script,result,success in (
        ('test_modes_collapse_clean_max.py','modes_collapse_clean_result.txt','ALL MODE/COLLAPSE/CLEANUP TESTS PASSED'),
        ('test_skin_vertex_remap_max.py','skin_vertex_remap_result.txt','ALL SKIN VERTEX REMAP TESTS PASSED'),
        ('test_uv_channels_max.py','channels_max_result.txt','ALL CHANNEL SELECTION MAX TESTS PASSED'),
    ):
        rows.append('Running '+script);out.write_text('\n'.join(rows),encoding='utf-8')
        runpy.run_path(str(root/'tests'/script),run_name='__main__')
        text=(root/'tests/artifacts'/result).read_text(encoding='utf-8')
        assert success in text,text
        rows.append(script+' PASS')
    rows.append('ALL RESUMED REGRESSIONS PASSED')
except Exception: rows.append(traceback.format_exc())
finally: out.write_text('\n'.join(rows),encoding='utf-8')
