@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b 1
cd /d "%~dp0"
cl /nologo /O2 /MD /LD mikktspace.c tl_mikktspace.c /link /OUT:../tl_mikktspace.dll
exit /b %errorlevel%
