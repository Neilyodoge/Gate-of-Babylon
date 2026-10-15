param()
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$game=Join-Path $projectRoot 'artifacts/build/TataFarmAdventure.exe'
$stamp=Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts/latest-check.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$check=Join-Path $projectRoot $stamp.path
$hash=(Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash
if($hash -ne $stamp.executableSha256){throw 'Game changed after verification; run checks again.'}
$source=Get-Content -LiteralPath (Join-Path $projectRoot 'src/FarmApp.cs') -Raw -Encoding UTF8
if($source -notmatch 'const string Version="([0-9.]+)"'){throw 'Cannot identify game version.'}
$version=$Matches[1]
& (Join-Path $PSScriptRoot 'Test-UXReviewGate.ps1') -Version $version -CheckPath $check
$reports=@('cards-upgraded-fit-regression.txt','launcher-regression.txt','cards-relic-regression.txt','cards-camp-regression.txt','cards-damage-regression.txt','cards-turn-receipt-regression.txt','cards-status-regression.txt','cards-inspect-regression.txt','cards-receipt-regression.txt','cards-lesson-regression.txt','cards-browse-regression.txt','cards-reward-fit-regression.txt','cards-piles-regression.txt','cards-forecast-rules.txt','cards-forecast-ui.txt','result.txt','cards-rules.txt','cards-ui-regression.txt','branch-rules.txt','branch-ui-regression.txt','cards-feedback-regression.txt','cards-motion-render.txt','cards-save-phases.txt','cards-playthrough-render.txt','pet-start-regression.txt','panel-presence-regression.txt','keyboard-regression.txt','reading-ui-regression.txt','first-feeding-regression.txt')
foreach($name in $reports){if((Get-Content -LiteralPath (Join-Path $check $name) -Raw -Encoding UTF8) -notmatch '^PASS:'){throw "Missing pass: $name"}}
$candidate=Join-Path $projectRoot ('artifacts/package-staging-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $candidate | Out-Null
foreach($dir in @('previews','evidence')){New-Item -ItemType Directory -Path (Join-Path $candidate $dir) | Out-Null}
Copy-Item -LiteralPath $game -Destination (Join-Path $candidate 'TataFarmAdventure.exe')
$launcher=Join-Path $projectRoot 'artifacts/build/卡牌试玩.exe'
Copy-Item -LiteralPath $launcher -Destination (Join-Path $candidate '卡牌试玩.exe')
$verify=Start-Process -FilePath (Join-Path $candidate '卡牌试玩.exe') -ArgumentList '--verify' -WorkingDirectory $candidate -WindowStyle Hidden -PassThru -Wait
if($verify.ExitCode -ne 0){throw 'Launcher does not accept the current build.'};$verify.Dispose()
$invalid=Join-Path $projectRoot ('artifacts/launcher-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $invalid | Out-Null
Copy-Item -LiteralPath $launcher -Destination (Join-Path $invalid '卡牌试玩.exe')
Copy-Item -LiteralPath $launcher -Destination (Join-Path $invalid 'TataFarmAdventure.exe')
$verify=Start-Process -FilePath (Join-Path $invalid '卡牌试玩.exe') -ArgumentList '--verify' -WorkingDirectory $invalid -WindowStyle Hidden -PassThru -Wait
if($verify.ExitCode -eq 0){throw 'Launcher incorrectly accepts a non-game executable.'};$verify.Dispose()
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/试玩说明.md') -Destination $candidate
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/GAMEPLAY.md') -Destination (Join-Path $candidate 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/design/口袋远征-玩法演进.md') -Destination (Join-Path $candidate '玩法演进.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/RELEASE_CHECKS.md') -Destination $candidate
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs/reviews') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $candidate 'evidence')}
foreach($name in ($reports+@('cards-lesson-copy.txt','cards-balance.txt','cards-simulation.csv','cards-ux-metrics.json','cards-contrast.json','tested-executable-sha256.txt'))){Copy-Item -LiteralPath (Join-Path $check $name) -Destination (Join-Path $candidate 'evidence')}
foreach($name in @('cards-upgraded-fit-overview.png','cards-upgraded-fit-overview-gray.png','cards-fit-upgraded-sources-2.png','cards-relic-overview.png','cards-relic-overview-gray.png','cards-relic-2-2.png','cards-overview.png','cards-camp-overview.png','cards-camp-overview-gray.png','cards-camp-full-2.png','cards-damage-overview.png','cards-damage-overview-gray.png','cards-damage-snail-2.png','cards-turn-receipt-overview.png','cards-turn-receipt-overview-gray.png','cards-turn-counter-2.png','cards-status-overview.png','cards-status-overview-gray.png','cards-status-combined-2.png','cards-inspect-overview.png','cards-inspect-overview-gray.png','cards-inspect-focus-return-2.png','cards-receipt-overview.png','cards-receipt-overview-gray.png','cards-receipt-breath-2.png','cards-lesson-overview.png','cards-lesson-overview-gray.png','cards-lesson-detour-2.png','cards-browse-overview.png','cards-browse-overview-gray.png','cards-browse-upgrade-end-2.png','cards-reward-fit-overview.png','cards-reward-fit-overview-gray.png','cards-fit-wet-2.png','cards-fit-missing-2.png','cards-flow-overview.png','cards-piles-overview.png','cards-piles-overview-gray.png','cards-piles-draw-2.png','cards-piles-exhaust-2.png','cards-reading-overview.png','cards-reading-overview-gray.png','cards-attack-preview.gif','cards-forecast-danger-2.png','cards-forecast-shield-2.png','cards-forecast-lethal-2.png','cards-energy-full-2.png','cards-energy-empty-2.png','cards-playthrough-2.png')){Copy-Item -LiteralPath (Join-Path $check $name) -Destination (Join-Path $candidate 'previews')}
Set-Content -LiteralPath (Join-Path $candidate 'Launch.cmd') -Value '@start "" "%~dp0TataFarmAdventure.exe"' -Encoding ASCII
$summary="TATA $version`r`nEvidenceRun: $(Split-Path $check -Leaf)`r`nExecutable SHA256: $hash`r`nDetached WPF and isolated saves only. Physical display and human understanding remain untested."
Set-Content -LiteralPath (Join-Path $candidate '引导验收.txt') -Value $summary -Encoding UTF8
$hashes=Get-ChildItem -LiteralPath $candidate -Recurse -File | Sort-Object FullName | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash+'  '+$_.FullName.Substring($candidate.Length+1).Replace('\','/')}
Set-Content -LiteralPath (Join-Path $candidate 'SHA256.txt') -Value $hashes -Encoding UTF8
$archive=$candidate+'.zip';Compress-Archive -Path (Join-Path $candidate '*') -DestinationPath $archive -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$files=@{};Get-ChildItem -LiteralPath $candidate -Recurse -File | ForEach-Object {$files[$_.FullName.Substring($candidate.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{
    $entries=@($zip.Entries | Where-Object {$_.Name -ne ''});if($entries.Count -ne $files.Count){throw 'ZIP file count mismatch.'}
    foreach($entry in $entries){
        $name=$entry.FullName.Replace('\','/');if(!$files.ContainsKey($name) -or $name -match '(?i)(\.xml$|\.bak$|farm-save)'){throw "Unexpected entry: $name"}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if($actual -ne $files[$name]){throw "ZIP hash mismatch: $name"}
    }
}finally{$zip.Dispose()}
# Only replace the two explicit latest-delivery paths, after verifying the candidate.
$dist=[IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
foreach($name in @('TATA','TATA.zip')){
    $path=[IO.Path]::GetFullPath((Join-Path $dist $name))
    if(!$path.StartsWith($dist+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe delivery path.'}
    if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Recurse -Force}
}
Move-Item -LiteralPath $candidate -Destination (Join-Path $dist 'TATA')
Move-Item -LiteralPath $archive -Destination (Join-Path $dist 'TATA.zip')
$ledgerPath=Join-Path $projectRoot 'docs/releases.json';$ledger=Get-Content -LiteralPath $ledgerPath -Raw -Encoding UTF8 | ConvertFrom-Json
if(!(@($ledger.releases | Where-Object {$_.version -eq $version}).Count)){
    $full=Test-Path -LiteralPath (Join-Path $projectRoot ('docs/reviews/ux-review-v'+$version+'.md'))
    $ledger.releases+=@{version=$version;fullReview=$full;report=if($full){'docs/reviews/ux-review-v'+$version+'.md'}else{''}}
    if($full){$ledger.lastFullReview=$version}
    $ledger | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ledgerPath -Encoding UTF8
}
$result="PASS: $version; $($files.Count) ZIP entries match SHA256; launcher accepts current build and rejects a non-game executable; no save files."
Set-Content -LiteralPath (Join-Path $check 'package-verification.txt') -Value $result -Encoding UTF8
$result
