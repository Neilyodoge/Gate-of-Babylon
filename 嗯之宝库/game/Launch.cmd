@echo off
setlocal
cd /d "%~dp0"
if not exist "build\Stormfield.exe" (
    echo Building Stormfield...
    dotnet publish "Stormfield.csproj" -c Release -o "build"
    if errorlevel 1 (
        echo Build failed. Install the .NET 9 SDK or see README.md.
        pause
        exit /b 1
    )
)
copy /y "tuning.json" "build\tuning.json" >nul
start "" "build\Stormfield.exe"
