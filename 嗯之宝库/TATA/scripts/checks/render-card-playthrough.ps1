param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$CheckPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$assembly=[Reflection.Assembly]::LoadFrom($GamePath)
$app=[System.Windows.Application]::new()
$app.ShutdownMode=[System.Windows.ShutdownMode]::OnExplicitShutdown
$save=Get-ChildItem -LiteralPath $CheckPath -Filter 'cards-all-phases-*.xml' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$game=[FarmGame]::new($save.FullName,$null,$false)
if($game.C.Phase -ne 'result' -or $game.C.Victories -ne 5){throw 'Expected a completed legal playthrough.'}
$window=[FarmWindow]::new($game,$true)
$panel=[FarmPanel]::new($window);$window.Panel=$panel;$panel.Tab='卡牌冒险'
$render=[FarmHeadlessTests].GetMethod('Render',[Reflection.BindingFlags]'NonPublic,Static')
foreach($scale in 0..2){
    $game.S.ReadingSize=$scale;$panel.Render()
    $argsForRender=New-Object 'object[]' 4
    $argsForRender[0]=$panel.PSObject.BaseObject;$argsForRender[1]=[System.IO.Path]::Combine($CheckPath,"cards-playthrough-$scale.png");$argsForRender[2]=[double]764;$argsForRender[3]=[double]680
    $null=$render.Invoke($null,$argsForRender)
}
foreach($w in @($window,$panel)){if($w.IsVisible -or ([System.Windows.Interop.WindowInteropHelper]::new($w)).Handle -ne [IntPtr]::Zero){throw 'Unexpected native window.'}}
$summary="PASS: detached screenshots from a real legal simulated playthrough: $($game.C.Victories) victories, $($game.C.Played) cards, $($game.C.Combos) combos, $($game.C.Hp) remaining HP. No edited combat counters; not a human playtest."
Set-Content -LiteralPath (Join-Path $CheckPath 'cards-playthrough-render.txt') -Value $summary -Encoding UTF8
$summary

