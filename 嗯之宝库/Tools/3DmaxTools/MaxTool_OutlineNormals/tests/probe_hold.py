from pathlib import Path
import pymxs
from pymxs import runtime as rt

out = Path(__file__).resolve().parent / 'artifacts' / 'hold_probe.txt'
rows = []
def state(label):
    rows.append('%s: holding=%s undo=%s' % (label, rt.theHold.Holding(), rt.theHold.IsSuspended()))
state('entry')
with pymxs.undo(False):
    state('undo off')
state('after off')
with pymxs.undo(True, 'Probe'):
    state('undo on')
    b = rt.box()
    state('after box')
state('after undo on')
# This is only MaxBatch's disposable test scene. End its startup hold so button
# callbacks are tested under the same idle state as a normal interactive click.
if rt.theHold.Holding():
    rt.theHold.Accept('MaxBatch test setup')
state('after batch setup accept')
with pymxs.undo(True, 'Probe idle'):
    state('idle undo on')
    b = rt.box()
    state('idle after box')
state('idle after context')
try:
    with pymxs.undo(True, 'Probe failure'):
        failed = rt.box(name='ProbeRollback')
        raise RuntimeError('injected failure')
except RuntimeError:
    rows.append('Exception propagated')
rows.append('undo context rollback object exists=%s' % (rt.getNodeByName('ProbeRollback') is not None))
out.write_text('\n'.join(rows), encoding='utf-8')
