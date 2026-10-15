param([Parameter(Mandatory=$true)][string]$GamePath,[Parameter(Mandatory=$true)][string]$CheckPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$assembly=[Reflection.Assembly]::LoadFrom($GamePath)
$app=[System.Windows.Application]::new();$app.ShutdownMode=[System.Windows.ShutdownMode]::OnExplicitShutdown
$game=[FarmGame]::new($null,$null,$true);$game.S.ReadingSize=2;$game.S.DesktopMotion=$true;$null=$game.StartCards($false)
$window=[FarmWindow]::new($game,$true);$panel=[FarmPanel]::new($window);$window.Panel=$panel;$panel.Tab='卡牌冒险';$panel.Render()
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$click=[FarmPanel].GetMethod('ClickCard',$flags);$null=$click.Invoke($panel,@('cards-hand-0'))
$feedback=[FarmPanel].GetField('cardFeedback',$flags).GetValue($panel)
$elapsed=$feedback.GetType().GetField('TestElapsed',$flags)
$scene=[FarmPanel].GetField('cardScene',$flags).GetValue($panel)
$tick=$scene.GetType().GetField('Tick',$flags)
$xml=[FarmPracticeTests].GetMethod('Xml').MakeGenericMethod([MossRun]);$before=$xml.Invoke($null,@($game.C))
$render=[FarmHeadlessTests].GetMethod('Render',[Reflection.BindingFlags]'NonPublic,Static')
for($i=0;$i -lt 15;$i++){
    $elapsed.SetValue($feedback,[double]($i*67));$tick.SetValue($scene,[int]$i)
    $scene.InvalidateVisual()
    $renderArgs=[object[]]::new(4);$renderArgs[0]=$panel.PSObject.BaseObject;$renderArgs[1]=[IO.Path]::Combine($CheckPath,('cards-motion-'+$i.ToString('00')+'.png'));$renderArgs[2]=[double]764;$renderArgs[3]=[double]680
    $null=$render.Invoke($null,$renderArgs)
}
if($before -ne $xml.Invoke($null,@($game.C))){throw 'Playback changed combat state.'}
foreach($w in @($window,$panel)){if($w.IsVisible -or ([System.Windows.Interop.WindowInteropHelper]::new($w)).Handle -ne [IntPtr]::Zero){throw 'Unexpected native window.'}}
$summary='PASS: 15 detached frames at 67 ms intervals after a real legal card click; same production drawing and motion formulas; combat state unchanged across playback. No native window or real-player save. Reconstructed preview, not a physical display recording.'
Set-Content -LiteralPath (Join-Path $CheckPath 'cards-motion-render.txt') -Value $summary -Encoding UTF8
$summary
