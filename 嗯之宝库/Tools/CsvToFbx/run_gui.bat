@echo off
chcp 65001 >nul
title RenderDoc CSV to FBX Converter
cd /d "%~dp0"
python csv_to_fbx_gui.py
if errorlevel 1 (
    echo.
    echo [Error] Python not found. Please install Python 3.6+ and add to PATH.
    echo         https://www.python.org/downloads/
    pause
)
