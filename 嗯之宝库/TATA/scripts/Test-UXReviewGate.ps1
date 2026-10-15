param([Parameter(Mandatory=$true)][string]$Version,[Parameter(Mandatory=$true)][string]$CheckPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$ledger=Get-Content -LiteralPath (Join-Path $projectRoot 'docs/releases.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$last=[version]$ledger.lastFullReview;$current=[version]$Version
$published=@($ledger.releases | Where-Object {[version]$_.version -gt $last -and [version]$_.version -le $current})
$count=$published.Count
if(!(@($ledger.releases | Where-Object {$_.version -eq $Version}).Count)){$count++}
$report=Join-Path $projectRoot ('docs/reviews/ux-review-v'+$Version+'.md')
if($count -ge 3 -or (Test-Path -LiteralPath $report)){
    if(!(Test-Path -LiteralPath $report)){throw 'A complete game-ux-review is due before this release.'}
    $body=Get-Content -LiteralPath $report -Raw -Encoding UTF8
    foreach($line in @(('Version: '+$Version),('EvidenceRun: '+(Split-Path $CheckPath -Leaf)),'Status: COMPLETE','OpenBlockingIssues: 0')){
        if($body -notmatch ('(?m)^'+[regex]::Escape($line)+'\r?$')){throw "Review does not match this build: $line"}
    }
    "PASS: full review $Version matches this evidence."
}else{"PASS: routine release $count of 3 since full review $last."}
