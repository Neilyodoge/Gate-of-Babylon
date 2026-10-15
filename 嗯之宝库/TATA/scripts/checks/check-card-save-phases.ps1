param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$CheckPath)
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom($GamePath)
$bot=[FarmCardsTests].GetMethod('BestCard',[Reflection.BindingFlags]'Static,NonPublic')
$xml=[FarmPracticeTests].GetMethod('Xml').MakeGenericMethod([MossRun])
$winning=Import-Csv -LiteralPath (Join-Path $CheckPath 'cards-simulation.csv') | Where-Object {$_.assisted -eq 'True' -and $_.victories -eq '5'} | Select-Object -First 1
$savePath=Join-Path $CheckPath ('cards-all-phases-'+[guid]::NewGuid().ToString('N')+'.xml')
$game=[FarmGame]::new($savePath,$null,$false)
$game.C.Seed=[int]$winning.seed*7919
if(!$game.StartCards($true)){throw 'Cannot start isolated journey.'}
$seen=@{}
for($step=0;$step -lt 350;$step++){
    $phase=$game.C.Phase
    if(!$seen.ContainsKey($phase)){
        $before=$xml.Invoke($null,@($game.C));$game.Save()
        $game=[FarmGame]::new($savePath,$null,$false)
        if($xml.Invoke($null,@($game.C)) -ne $before){throw "Save/reload changed $phase"}
        $seen[$phase]=$true
    }
    if($phase -eq 'result'){break}
    if($phase -eq 'battle'){
        $slot=[int]$bot.Invoke($null,@($game.PSObject.BaseObject))
        if($slot -ge 0){$null=$game.PlayCard($slot,$game.C.Revision)}else{$null=$game.EndCardTurn($game.C.Revision)}
    }elseif($phase -eq 'reward'){$null=$game.PickCardReward(0,$game.C.Revision)}
    elseif($phase -eq 'branch'){$null=$game.PickShieldBranch(1,$game.C.Revision)}
    elseif($phase -eq 'route'){$null=$game.ChooseCardRoute($false,$game.C.Revision)}
    elseif($phase -eq 'camp'){$null=$game.CardCamp(0,-1,$game.C.Revision)}
}
foreach($phase in @('battle','reward','branch','route','camp','result')){if(!$seen.ContainsKey($phase)){throw "Phase not exercised: $phase"}}
$result='PASS: exact card state roundtrip at battle, reward, branch, route, camp and result using an isolated normal save; continued real legal actions after each reload; no UI, real-player progress or native window.'
Set-Content -LiteralPath (Join-Path $CheckPath 'cards-save-phases.txt') -Value $result -Encoding UTF8
$result

