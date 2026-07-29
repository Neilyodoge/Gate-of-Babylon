@echo off
chcp 65001 >nul
title 安装 FBX Mesh Exporter (改) 到 RenderDoc

set "exporter=%APPDATA%\qrenderdoc\extensions"
if not exist "%exporter%" mkdir "%exporter%"

echo 正在安装扩展到: %exporter%
xcopy "%~dp0timmyliang\*" "%exporter%\timmyliang\" /i /e /Y /C

if errorlevel 1 (
    echo.
    echo [失败] 复制文件出错。
    pause
    exit /b 1
)

echo.
echo [完成] 已安装。请重启 RenderDoc，在 Tools -^> Manage Extensions 中启用 "FBX Mesh Exporter"。
pause
