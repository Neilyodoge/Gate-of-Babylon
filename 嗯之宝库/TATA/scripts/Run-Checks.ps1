param([string]$PythonPath='python',[string]$NodePath='node',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'Build.ps1')}
$game=Join-Path $projectRoot 'artifacts/build/TataFarmAdventure.exe'
if(!(Test-Path -LiteralPath $game)){throw 'Build the game first.'}
$test=Start-Process -FilePath $game -ArgumentList '--headless-test' -WorkingDirectory (Split-Path $game) -WindowStyle Hidden -PassThru -Wait
if($test.ExitCode -ne 0){throw 'Headless checks failed. Inspect artifacts/build/farm-checks/latest-background-check.txt.'}
$test.Dispose()
$check=(Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts/build/farm-checks/latest-background-check.txt') -Raw -Encoding UTF8).Trim()
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'checks/check-card-save-phases.ps1') -GamePath $game -CheckPath $check
if($LASTEXITCODE -ne 0){throw 'Save phase checks failed.'}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'checks/check-launcher.ps1') -GamePath $game -CheckPath $check
if($LASTEXITCODE -ne 0){throw 'Launcher checks failed.'}
foreach($script in @('render-card-playthrough.ps1','render-card-motion.ps1')){
    & powershell -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot ('checks/'+$script)) -GamePath $game -CheckPath $check
    if($LASTEXITCODE -ne 0){throw "Detached rendering failed: $script"}
}
& $PythonPath (Join-Path $PSScriptRoot 'make-overview.py') $check
if($LASTEXITCODE -ne 0){throw 'Preview generation failed; Python and Pillow are required.'}
& $NodePath (Join-Path $PSScriptRoot 'measure-copy.js') $check
if($LASTEXITCODE -ne 0){throw 'Copy measurement failed; Node.js is required.'}
$hash=(Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $check 'tested-executable-sha256.txt') -Value $hash -Encoding UTF8
$relative=$check.Substring($projectRoot.Length+1).Replace('\','/')
@{path=$relative;executableSha256=$hash;checkedAt=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/latest-check.json') -Encoding UTF8
"PASS: $relative"
