param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$CheckPath)
$ErrorActionPreference='Stop'
$game=[IO.Path]::GetFullPath($GamePath)
$launcher=Join-Path (Split-Path $game) '卡牌试玩.exe'
$root=Join-Path ([IO.Path]::GetFullPath($CheckPath)) 'launcher-cases'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$results=@()
foreach($case in @('current','current-with-legacy','missing-current','legacy-only','wrong-current','corrupt-current-with-legacy')){
    $folder=Join-Path $root $case
    New-Item -ItemType Directory -Path $folder | Out-Null
    $entry=Join-Path $folder '卡牌试玩.exe'
    Copy-Item -LiteralPath $launcher -Destination $entry
    $primary=Join-Path $folder 'TataFarmAdventure.exe'
    $legacy=Join-Path $folder 'TataFarmAdventure-v4.7.exe'
    if($case -in @('current','current-with-legacy')){Copy-Item -LiteralPath $game -Destination $primary}
    if($case -eq 'current-with-legacy'){Copy-Item -LiteralPath $launcher -Destination $legacy}
    if($case -in @('legacy-only','corrupt-current-with-legacy')){Copy-Item -LiteralPath $game -Destination $legacy}
    if($case -eq 'wrong-current'){Copy-Item -LiteralPath $launcher -Destination $primary}
    if($case -eq 'corrupt-current-with-legacy'){Set-Content -LiteralPath $primary -Value 'not a game assembly' -Encoding ASCII}
    $process=Start-Process -FilePath $entry -ArgumentList '--verify' -WorkingDirectory $folder -WindowStyle Hidden -Wait -PassThru
    $code=$process.ExitCode
    $process.Dispose()
    $expected=if($case -in @('current','current-with-legacy')){0}else{1}
    if($code -ne $expected){throw "Launcher case $case returned $code, expected $expected"}
    $results+="$case=$code"
}
$summary='PASS: launcher verifies current canonical game; legacy filename cannot mask valid current game; missing, legacy-only, non-game and corrupt current files rejected. Six isolated --verify cases; no game launch, message box, native window or player save.'
Set-Content -LiteralPath (Join-Path $CheckPath 'launcher-regression.txt') -Value ($summary+[Environment]::NewLine+($results -join ', ')) -Encoding UTF8
$summary
