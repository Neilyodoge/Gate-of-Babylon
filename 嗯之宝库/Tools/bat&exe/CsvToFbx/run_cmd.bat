@echo off
chcp 65001 >nul
title RenderDoc CSV to FBX (CMD)
cd /d "%~dp0"

if "%~1"=="" (
    echo Usage: Drag .csv file onto this bat, or:
    echo   run_cmd.bat input.csv [output.fbx] [mesh_name]
    echo.
    echo Parameters:
    echo   input.csv   - RenderDoc exported CSV file
    echo   output.fbx  - Output FBX path (optional, defaults to same name)
    echo   mesh_name   - Mesh name (optional, defaults to "Mesh")
    pause
    exit /b
)

set "INPUT=%~1"
set "OUTPUT=%~2"
set "MESHNAME=%~3"

if "%OUTPUT%"=="" set "OUTPUT=%~dpn1.fbx"
if "%MESHNAME%"=="" set "MESHNAME=Mesh"

echo Input:  %INPUT%
echo Output: %OUTPUT%
echo Mesh:   %MESHNAME%
echo.

python csv_to_fbx.py "%INPUT%" "%OUTPUT%" "%MESHNAME%"

if errorlevel 1 (
    echo.
    echo [Error] Conversion failed.
    pause
) else (
    echo.
    echo [Done] Output: %OUTPUT%
    timeout /t 3
)
