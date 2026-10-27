"""External supervisor: one reusable Max worker, crash recovery and scoped stopping."""
import ctypes
from ctypes import wintypes as wt
from datetime import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import threading
import time
import uuid
import batch_core as core

ROOT = Path(__file__).resolve().parent


class ProcessTree:
    """A Windows Job Object owns only the worker process and its descendants."""
    def __init__(self, process):
        self.process, self.handle = process, None
        if os.name != 'nt':
            return
        size = ctypes.c_size_t
        class Basic(ctypes.Structure):
            _fields_ = [('process_time', ctypes.c_longlong), ('job_time', ctypes.c_longlong),
                        ('flags', wt.DWORD), ('min_ws', size), ('max_ws', size),
                        ('active', wt.DWORD), ('affinity', size), ('priority', wt.DWORD), ('scheduling', wt.DWORD)]
        class IO(ctypes.Structure):
            _fields_ = [(name, ctypes.c_ulonglong) for name in ('read_ops', 'write_ops', 'other_ops', 'read_bytes', 'write_bytes', 'other_bytes')]
        class Extended(ctypes.Structure):
            _fields_ = [('basic', Basic), ('io', IO), ('process_mem', size), ('job_mem', size), ('peak_process', size), ('peak_job', size)]
        api = ctypes.WinDLL('kernel32', use_last_error=True)
        api.CreateJobObjectW.argtypes = [ctypes.c_void_p, wt.LPCWSTR]
        api.CreateJobObjectW.restype = wt.HANDLE
        api.SetInformationJobObject.argtypes = [wt.HANDLE, ctypes.c_int, ctypes.c_void_p, wt.DWORD]
        api.AssignProcessToJobObject.argtypes = [wt.HANDLE, wt.HANDLE]
        api.TerminateJobObject.argtypes = [wt.HANDLE, wt.UINT]
        api.CloseHandle.argtypes = [wt.HANDLE]
        handle = api.CreateJobObjectW(None, None)
        limits = Extended()
        limits.basic.flags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if (not handle or not api.SetInformationJobObject(handle, 9, ctypes.byref(limits), ctypes.sizeof(limits))
                or not api.AssignProcessToJobObject(handle, wt.HANDLE(int(process._handle)))):
            if handle:
                api.CloseHandle(handle)
            self.kill()
            raise OSError('无法为后台 Max 建立进程隔离，已停止本次启动。')
        self.api, self.handle = api, handle

    def kill(self):
        if self.handle:
            self.api.TerminateJobObject(self.handle, 1)
        elif self.process.poll() is None:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/PID', str(self.process.pid), '/T', '/F'],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                               creationflags=subprocess.CREATE_NO_WINDOW, timeout=20)
            else:
                self.process.kill()

    def close(self):
        if self.handle:
            self.api.CloseHandle(self.handle)
            self.handle = None


class JsonTail:
    def __init__(self, path):
        self.path, self.offset, self.pending = Path(path), 0, b''

    def read(self):
        if not self.path.exists():
            return []
        with self.path.open('rb') as stream:
            stream.seek(self.offset)
            self.pending += stream.read()
            self.offset = stream.tell()
        events = []
        while b'\n' in self.pending:
            line, self.pending = self.pending.split(b'\n', 1)
            if line.strip():
                events.append(json.loads(line.decode('utf-8')))
        return events


class BatchRunner:
    def __init__(self, config, jobs, callback, command_factory=None):
        self.config, self.jobs, self.callback = dict(config), list(jobs), callback
        self.command_factory = command_factory
        self.stop_event, self.kill_event = threading.Event(), threading.Event()
        self.results, self.run_dir, self.log = {}, None, None

    def stop(self, immediate=False):
        self.stop_event.set()
        if immediate:
            self.kill_event.set()

    def emit(self, event, **fields):
        record = dict(fields, event=event, time=time.time())
        if self.log:
            try:
                self.log.write(json.dumps(record, ensure_ascii=False) + '\n')
                self.log.flush()
            except OSError as exc:
                try:
                    self.log.close()
                except OSError:
                    pass
                self.log = None
                raise RuntimeError('日志写入失败，已停止：' + str(exc)) from exc
        self.callback(record)

    def finish_file(self, source, status, detail):
        self.results[source] = {'status': status, 'detail': detail}
        self.emit('file_end', source=source, status=status, detail=detail)

    def command(self, manifest, index):
        if self.command_factory:
            return self.command_factory(manifest)
        if Path(self.config['max_exe']).name.lower() != '3dsmax.exe':
            raise ValueError('请重新选择普通 Max 快捷方式，本版使用 3dsmax.exe 导出。')
        bootstrap = self.run_dir / ('start_%02d.ms' % index)
        bootstrap.write_text('python.Execute "import os, runpy; runpy.run_path(os.environ[\'MT_BATCH_WORKER\'], run_name=\'__main__\')"\n', encoding='utf-8-sig')
        return [self.config['max_exe'], *self.config.get('max_arguments', []), '-U', 'MAXScript', str(bootstrap)]

    def run(self):
        tree = None
        failure_reason = ''
        try:
            for kind in {j['kind'] for j in self.jobs if not j.get('issue')}:
                core.validate_preset(self.config[kind + '_preset'])
            run_id = datetime.now().strftime('%Y%m%d_%H%M%S_') + uuid.uuid4().hex[:6]
            self.run_dir = Path(self.config['output']) / '_MT_logs' / run_id
            self.run_dir.mkdir(parents=True)
            self.log = (self.run_dir / 'events.jsonl').open('x', encoding='utf-8')
            config = dict(self.config)
            for kind in {j['kind'] for j in self.jobs if not j.get('issue')}:
                src = Path(config[kind + '_preset'])
                dst = self.run_dir / (kind + '.fbxexportpreset')
                shutil.copyfile(src, dst)
                config[kind + '_preset'] = str(dst)
            (self.run_dir / 'plan.json').write_text(json.dumps({'config': self.config, 'jobs': self.jobs}, ensure_ascii=False, indent=2), encoding='utf-8')
            self.emit('run_start', log_dir=str(self.run_dir), total=len(self.jobs))
            pending = []
            for job in self.jobs:
                if job.get('issue'):
                    self.finish_file(job['source'], 'failed', job['issue'])
                elif Path(job['target']).exists():
                    self.finish_file(job['source'], 'skipped', 'FBX 已存在，未覆盖')
                else:
                    pending.append(job)
            launches, idle_failures = 0, 0
            while pending and not self.stop_event.is_set():
                launches += 1
                event_path = self.run_dir / ('worker_%02d.jsonl' % launches)
                stop_path = self.run_dir / ('stop_%02d.request' % launches)
                manifest = self.run_dir / ('manifest_%02d.json' % launches)
                permit = self.run_dir / ('permit_%02d.json' % launches)
                launch_token = uuid.uuid4().hex
                manifest.write_text(json.dumps({'jobs': pending, 'config': config,
                                                'events': str(event_path), 'stop_file': str(stop_path),
                                                'launch_permit': str(permit), 'launch_token': launch_token}, ensure_ascii=False), encoding='utf-8')
                environment = dict(os.environ, MT_BATCH_MANIFEST=str(manifest), MT_BATCH_TOKEN=launch_token,
                                   MT_BATCH_WORKER=str(ROOT / 'max_worker.py'))
                self.emit('launching', attempt=launches, remaining=len(pending))
                with (self.run_dir / ('console_%02d.log' % launches)).open('wb') as console:
                    startup = None
                    if os.name == 'nt' and config.get('max_launch_mode') == 'desktop':
                        startup = subprocess.STARTUPINFO()
                        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                        startup.wShowWindow = 0  # Start the tool-owned Max without taking focus.
                    command = self.command(manifest, launches)
                    working_dir = config.get('max_working_directory') or str(ROOT)
                    (self.run_dir / ('launch_%02d.json' % launches)).write_text(
                        json.dumps({'command': command, 'cwd': working_dir, 'mode': config.get('max_launch_mode', 'batch')},
                                   ensure_ascii=False, indent=2), encoding='utf-8')
                    process = subprocess.Popen(command, cwd=working_dir, env=environment, startupinfo=startup,
                                               stdout=console, stderr=subprocess.STDOUT,
                                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
                    tree = ProcessTree(process)
                    permit_data = {'pid': process.pid, 'token': launch_token}
                    core.save_config(permit, permit_data)
                    tail = JsonTail(event_path)
                    active, ready, done, fatal = None, False, False, ''
                    last_progress = time.monotonic()
                    timeout = float(config.get('timeout_minutes', 10)) * 60
                    completion_time = None
                    def consume(events):
                        nonlocal active, ready, done, fatal, last_progress, completion_time
                        for event in events:
                            name = event['event']
                            if name == 'ready':
                                ready = True
                                last_progress = time.monotonic()
                                self.emit('ready')
                            elif name == 'file_start':
                                active = event['source']
                                last_progress = time.monotonic()
                                self.emit('file_start', source=active)
                            elif name == 'file_end':
                                self.finish_file(event['source'], event['status'], event['detail'])
                                active = None
                                last_progress = time.monotonic()
                            elif name == 'fatal':
                                fatal = event['detail']
                            elif name == 'worker_done':
                                done = True
                                completion_time = time.monotonic()
                    while True:
                        consume(tail.read())
                        if self.stop_event.is_set():
                            stop_path.touch(exist_ok=True)
                        if self.kill_event.is_set():
                            fatal = '用户终止后台处理'
                            tree.kill()
                            break
                        if process.poll() is not None:
                            # One final read catches a record flushed just before exit.
                            consume(tail.read())
                            break
                        if done and completion_time and time.monotonic() - completion_time > 15:
                            tree.kill()  # Finished worker stalled during Max shutdown.
                            break
                        if not done and time.monotonic() - last_progress > timeout:
                            fatal = '文件处理超时' if active else 'Max 启动或响应超时'
                            tree.kill()
                            break
                        time.sleep(0.15)
                    tree.close()
                    tree = None
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        raise RuntimeError('后台进程未退出，请查看日志。')
                if active and active not in self.results:
                    self.finish_file(active, 'canceled' if self.stop_event.is_set() else 'failed', fatal or 'Max 意外退出')
                remaining = [j for j in pending if j['source'] not in self.results]
                if not remaining or self.stop_event.is_set():
                    break
                if done:
                    raise RuntimeError('后台提前结束，仍有未完成文件。')
                if not active:
                    idle_failures += 1
                else:
                    idle_failures = 0
                if not ready or idle_failures > 1:
                    reason = fatal or 'Max 未能启动或工作脚本未执行；请检查版本、授权及后台日志。'
                    for job in remaining:
                        self.finish_file(job['source'], 'failed', reason)
                    break
                self.emit('restarting', detail=fatal or 'Max 意外退出，重启后继续剩余文件')
                pending = remaining
        except Exception as exc:
            failure_reason = str(exc)
            self.emit('error', detail=failure_reason)
        finally:
            if tree:
                tree.close()
            for job in self.jobs:
                if job['source'] not in self.results:
                    self.finish_file(job['source'], 'canceled' if self.stop_event.is_set() else 'failed',
                                     '未处理，已停止' if self.stop_event.is_set() else (failure_reason or '批处理未完成，请查看日志'))
            self.emit('run_done', results=self.results, stopped=self.stop_event.is_set())
            if self.log:
                self.log.close()
                self.log = None
