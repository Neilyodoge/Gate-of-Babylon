@echo off
chcp 65001 >nul
title 传政今天牙掉了没 (ಡωಡ)
setlocal
set PYTHONDONTWRITEBYTECODE=1
where pythonw.exe >nul 2>nul
if not errorlevel 1 (
  start "" pythonw.exe "%~dp0mt_batch_export.py"
  exit /b
)
where pyw.exe >nul 2>nul
if not errorlevel 1 (
  start "" pyw.exe -3 "%~dp0mt_batch_export.py"
  exit /b
)
for /d %%P in ("%LOCALAPPDATA%\Programs\Python\Python3*") do (
  if exist "%%~P\pythonw.exe" (
    start "" "%%~P\pythonw.exe" "%~dp0mt_batch_export.py"
    exit /b
  )
)
echo 未找到可用的 Python 环境。
echo 请安装 Python 3.9 或更新版本，并勾选 Tcl/Tk 支持，然后重新打开本工具。
echo 下载地址：https://www.python.org/downloads/
pause
