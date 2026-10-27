"""Private worker: only an explicitly permitted process owned by the supervisor."""
import json
import os
from pathlib import Path
import sys
import time
import traceback

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))


def authorized_manifest():
    manifest_path = Path(os.environ.get('MT_BATCH_MANIFEST', ''))
    token = os.environ.get('MT_BATCH_TOKEN', '')
    if not token or not manifest_path.is_file():
        raise RuntimeError('只能由外部导出工具启动本脚本。请勿在已打开的 Max 中运行。')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('launch_token') != token:
        raise RuntimeError('导出任务启动标记不匹配。')
    permit_path = Path(manifest['launch_permit'])
    deadline = time.monotonic() + 20
    while not permit_path.is_file() and time.monotonic() < deadline:
        time.sleep(0.05)
    permit = json.loads(permit_path.read_text(encoding='utf-8'))
    if permit.get('token') != token or permit.get('pid') != os.getpid():
        raise RuntimeError('当前 Max 不是本工具启动的进程，已拒绝处理场景。')
    return manifest


def run(manifest, rt):
    event_path = Path(manifest['events'])
    stop_path = Path(manifest['stop_file'])
    with event_path.open('a', encoding='utf-8', buffering=1) as stream:
        def emit(event, **fields):
            stream.write(json.dumps(dict(fields, event=event, time=time.time()), ensure_ascii=False) + '\n')
            stream.flush()
        try:
            from max_export import MaxExporter
            exporter = MaxExporter(rt)
            emit('ready', pid=os.getpid())
            for job in manifest['jobs']:
                if stop_path.exists():
                    break
                emit('file_start', source=job['source'])
                try:
                    status, detail = exporter.export(job, manifest['config'])
                except Exception as exc:
                    status, detail = 'failed', str(exc)
                emit('file_end', source=job['source'], status=status, detail=detail)
            emit('worker_done', stopped=stop_path.exists())
        except Exception:
            emit('fatal', detail=traceback.format_exc())


def main():
    # Guard before touching the scene or registering any automatic exit.
    manifest = authorized_manifest()
    from pymxs import runtime as rt
    try:
        rt.setQuietMode(True)
        run(manifest, rt)
    finally:
        rt.quitMax(rt.name('noPrompt'))


if __name__ == '__main__':
    main()
