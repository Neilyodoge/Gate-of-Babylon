@echo off
chcp 65001 >nul
cd /d "%~dp0"

rem 优先使用无窗口的 VBS 启动器（完全不弹 cmd 窗口，失败会弹窗提示）
if exist "%~dp0启动GUI.vbs" (
    start "" wscript.exe "%~dp0启动GUI.vbs"
    exit /b
)

rem 退化方案：直接用 pythonw 启动（无控制台窗口）
where pythonw >nul 2>nul
if errorlevel 1 (
    powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('未找到 Python (pythonw)。请安装 Python 3.6+ 并勾选 Add Python to PATH。' + [char]13 + [char]10 + 'https://www.python.org/downloads/', 'CSV -> FBX 启动失败', 'OK', 'Error')" >nul 2>nul
    exit /b 1
)
start "" pythonw "%~dp0csv_to_fbx_gui.py"
exit /b
