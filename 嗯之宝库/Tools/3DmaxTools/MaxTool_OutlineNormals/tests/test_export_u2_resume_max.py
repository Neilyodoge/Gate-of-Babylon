"""FBX regression after fixing Unity's degenerate-UV fallback basis."""
import os,runpy,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1]
assert os.environ.get('TL_OUTLINE_BATCH_TEST')=='1'
out=root/'tests/artifacts/export_u2_resume_result.txt'
rows=['STARTED']
try:
    for script,result,success in (
        ('test_prisoner_stack_max.py','prisoner_stack_max_result.txt','PRISONER STACK TEST PASSED'),
        ('test_uv_channels_max.py','channels_max_result.txt','ALL CHANNEL SELECTION MAX TESTS PASSED'),
        ('test_launcher_reload.py','launcher_reload_result.txt','Repeated Launch.ms refreshes source'),
    ):
        rows.append('Running '+script);out.write_text('\n'.join(rows),encoding='utf-8')
        runpy.run_path(str(root/'tests'/script),run_name='__main__')
        text=(root/'tests/artifacts'/result).read_text(encoding='utf-8')
        assert success in text,text
        rows.append(script+' PASS')
    rows.append('ALL U+2 EXPORT REGRESSIONS PASSED')
except Exception:rows.append(traceback.format_exc())
finally:out.write_text('\n'.join(rows),encoding='utf-8')
