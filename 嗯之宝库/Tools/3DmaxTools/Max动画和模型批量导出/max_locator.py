"""Resolve only the Max program/shortcut explicitly chosen by the user."""
import base64
import ctypes
import json
import os
from pathlib import Path
import subprocess


def clean_path(value):
    return os.path.expandvars(str(value or '').strip().strip('"'))


def program_from_path(value):
    """Accept a real install folder or Max EXE, never an unrelated sibling."""
    if not clean_path(value):
        return None
    path = Path(clean_path(value))
    if path.is_dir():
        program = path / '3dsmax.exe'
    elif path.is_file() and path.name.lower() in ('3dsmax.exe', '3dsmaxbatch.exe'):
        program = path.parent / '3dsmax.exe'
    else:
        return None
    return program.resolve() if program.is_file() else None


def read_shortcuts(paths):
    """WScript.Shell reads .lnk metadata only. No shortcut target is executed."""
    paths = [str(Path(p).resolve()) for p in paths]
    if not paths:
        return []
    if os.name != 'nt':
        raise ValueError('快捷方式解析仅支持 Windows。')
    script = r'''
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding
$paths = @([Console]::In.ReadToEnd() | ConvertFrom-Json)
$shell = New-Object -ComObject WScript.Shell
$items = @()
foreach ($path in $paths) {
    try {
        $shortcut = $shell.CreateShortcut($path)
        $items += [pscustomobject]@{path=$path; target=$shortcut.TargetPath; arguments=$shortcut.Arguments; directory=$shortcut.WorkingDirectory}
    } catch { }
}
ConvertTo-Json -InputObject @($items) -Compress
'''
    encoded = base64.b64encode(script.encode('utf-16-le')).decode('ascii')
    executable = Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
    try:
        result = subprocess.run([str(executable), '-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', encoded],
                                input=json.dumps(paths, ensure_ascii=False), encoding='utf-8', errors='replace',
                                capture_output=True, timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
        if result.returncode:
            raise ValueError('Windows 未能读取快捷方式，请直接选择 Max 安装目录中的 3dsmax.exe 或 3dsmaxbatch.exe。')
        rows = json.loads(result.stdout.lstrip('\ufeff').strip() or '[]')
        return rows if isinstance(rows, list) else [rows]
    except (OSError, subprocess.TimeoutExpired, json.JSONDecodeError) as exc:
        raise ValueError('快捷方式读取失败，请直接选择 Max 程序。') from exc


def split_windows_args(value):
    """Use Windows quoting rules; arguments are never evaluated by a shell."""
    if not value or not value.strip():
        return []
    from ctypes import wintypes as wt
    api = ctypes.WinDLL('shell32', use_last_error=True)
    api.CommandLineToArgvW.argtypes = [wt.LPCWSTR, ctypes.POINTER(ctypes.c_int)]
    api.CommandLineToArgvW.restype = ctypes.POINTER(wt.LPWSTR)
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    count = ctypes.c_int()
    argv = api.CommandLineToArgvW('3dsmax.exe ' + value, ctypes.byref(count))
    if not argv:
        raise ValueError('无法读取快捷方式的启动参数。')
    try:
        return [argv[i] for i in range(1, count.value)]
    finally:
        kernel.LocalFree(argv)


def resolve_launch(value):
    if not clean_path(value):
        raise ValueError('请在“高级设置”中选择要使用的 Max 快捷方式。')
    path = Path(clean_path(value))
    arguments, directory = [], ''
    if path.suffix.lower() == '.lnk':
        if not path.is_file():
            raise ValueError('这个快捷方式不存在，请在“高级设置”中重新选择。')
        rows = read_shortcuts([path])
        target = Path(clean_path(rows[0].get('target'))) if rows else None
        if (not target or not target.is_file()
                or target.name.lower() not in ('3dsmax.exe', '3dsmaxbatch.exe')):
            raise ValueError('快捷方式目标已失效，或没有直接指向 3dsmax.exe / 3dsmaxbatch.exe。请重新选择 Max 快捷方式。')
        arguments = split_windows_args(rows[0].get('arguments', ''))
        directory = clean_path(rows[0].get('directory', ''))
        found = program_from_path(target)
    else:
        found = program_from_path(path)
    if not found:
        raise ValueError('指定版本中没有找到 3dsmax.exe，请检查该版本的安装。工具不会改用其他版本。')
    if any(arg.casefold() in ('-u', '/u', '-mxs', '/mxs', '-batch', '/batch') for arg in arguments):
        raise ValueError('这个快捷方式已指定启动脚本或 Batch 模式，与自动导出冲突。请选择该版本普通启动 Max 的快捷方式。')
    if directory and not Path(directory).is_dir():
        raise ValueError('快捷方式的“起始位置”不存在，请在快捷方式属性中修正。')
    return {'max_exe': str(found), 'max_arguments': arguments,
            'max_working_directory': directory or str(found.parent), 'max_launch_mode': 'desktop'}


def resolve_max(value):
    return Path(resolve_launch(value)['max_exe'])


def pick_max_launcher(owner, initial=''):
    """Keep the selected .lnk path using the Windows NODEREFERENCELINKS flag."""
    from ctypes import wintypes as wt

    class OpenFileName(ctypes.Structure):
        _fields_ = [
            ('lStructSize', wt.DWORD), ('hwndOwner', wt.HWND), ('hInstance', wt.HINSTANCE),
            ('lpstrFilter', wt.LPCWSTR), ('lpstrCustomFilter', wt.LPWSTR),
            ('nMaxCustFilter', wt.DWORD), ('nFilterIndex', wt.DWORD),
            ('lpstrFile', wt.LPWSTR), ('nMaxFile', wt.DWORD),
            ('lpstrFileTitle', wt.LPWSTR), ('nMaxFileTitle', wt.DWORD),
            ('lpstrInitialDir', wt.LPCWSTR), ('lpstrTitle', wt.LPCWSTR),
            ('Flags', wt.DWORD), ('nFileOffset', wt.WORD), ('nFileExtension', wt.WORD),
            ('lpstrDefExt', wt.LPCWSTR), ('lCustData', ctypes.c_ssize_t),
            ('lpfnHook', ctypes.c_void_p), ('lpTemplateName', wt.LPCWSTR),
            ('pvReserved', ctypes.c_void_p), ('dwReserved', wt.DWORD), ('FlagsEx', wt.DWORD)]

    buffer = ctypes.create_unicode_buffer(32768)
    initial_path = Path(clean_path(initial)) if clean_path(initial) else None
    initial_dir = initial_path if initial_path and initial_path.is_dir() else (initial_path.parent if initial_path else None)
    dialog = OpenFileName()
    dialog.lStructSize = ctypes.sizeof(dialog)
    dialog.hwndOwner = owner
    dialog.lpstrFilter = 'Max 快捷方式或程序\0*.lnk;*.exe\0Windows 快捷方式\0*.lnk\0Max 程序\0*.exe\0\0'
    dialog.nFilterIndex = 1
    dialog.lpstrFile = ctypes.cast(buffer, wt.LPWSTR)
    dialog.nMaxFile = len(buffer)
    dialog.lpstrInitialDir = str(initial_dir) if initial_dir and initial_dir.is_dir() else None
    dialog.lpstrTitle = '选择要使用的 Max 快捷方式'
    dialog.Flags = 0x00100000 | 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008
    api = ctypes.WinDLL('comdlg32', use_last_error=True)
    api.GetOpenFileNameW.argtypes = [ctypes.POINTER(OpenFileName)]
    api.GetOpenFileNameW.restype = wt.BOOL
    api.CommDlgExtendedError.restype = wt.DWORD
    if api.GetOpenFileNameW(ctypes.byref(dialog)):
        return buffer.value
    error = api.CommDlgExtendedError()
    if error:
        raise OSError('无法打开文件选择窗口（{}），可以把快捷方式的完整路径粘贴到输入框。'.format(error))
    return ''
