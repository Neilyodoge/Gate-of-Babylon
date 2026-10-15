using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public partial class FarmWindow
{
    internal void BackgroundPetStartChecks(string folder)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int reading=0;reading<3;reading++)foreach(int size in new[]{96,128,176}){
            var game=new FarmGame(null,null,true);game.S.PetSize=size;game.S.ReadingSize=reading;
            PrepareDesktopStart(game);var window=new FarmWindow(game,true);
            check(window.IsCompact&&window.Panel==null&&!game.OpenGuideOnLaunch&&!window.IsVisible,"launch starts with one pet and no automatic panel");
            var surface=(Grid)window.Content;var hover=window.petHover;
            string tag=reading+"-"+size;
            FarmHeadlessTests.Render(window,Path.Combine(folder,"pet-start-"+tag+".png"),window.Width,window.Height);
            check(window.petWelcome!=null&&window.petWelcome.ActualWidth<=window.Width&&!hover.IsOpen,"fresh pet keeps one visible introduction entry");
            string state=FarmPracticeTests.Xml(game.S);
            surface.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});
            FarmHeadlessTests.Render(window,Path.Combine(folder,"pet-toolbar-"+tag+".png"),window.Width,window.Height);
            check(hover.IsOpen&&window.petTools.IsHitTestVisible,"enter shows interactive toolbar");
            var row=(StackPanel)((StackPanel)window.petTools.Child).Children[0];
            var buttons=row.Children.Cast<Button>().ToArray();
            // Inspect the actual composited alpha along each mouse path, including the blank gap.
            window.Content=null;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.Width),(int)Math.Ceiling(window.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(surface);
            var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);window.Content=surface;
            var start=window.petImage.TranslatePoint(new Point(size/2,size/2),surface);
            foreach(var button in buttons){
                var point=button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2),surface);
                check(point.X>=0&&point.X<window.Width&&point.Y>=0&&point.Y<window.Height&&button.ActualHeight>=22,"three toolbar targets remain inside every size");
                for(int n=0;n<=100;n++){
                    int x=(int)(start.X+(point.X-start.X)*n/100),y=(int)(start.Y+(point.Y-start.Y)*n/100);
                    check(pixels[(y*bitmap.PixelWidth+x)*4+3]>0,"continuous alpha hit surface from pet to "+button.Content);
                }
            }
            surface.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseLeaveEvent});
            check(hover.IsOpen&&hover.Pending,"leave retains buttons through 650ms grace period");
            surface.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});check(hover.IsOpen&&!hover.Pending,"re-enter cancels pending hide");
            hover.Leave();hover.Expire(true,false);check(hover.IsOpen,"pointer in shared surface preserves toolbar");
            hover.Leave();hover.Expire(false,true);check(hover.IsOpen,"keyboard focus preserves toolbar");
            hover.SetPopup(true);hover.Leave();hover.Expire(false,false);check(hover.IsOpen,"pet picker keeps originating toolbar available");
            hover.SetPopup(false);hover.Expire(false,false);check(!hover.IsOpen&&!window.petTools.IsHitTestVisible&&((SolidColorBrush)surface.Background).Color.A==0,"after actual exit toolbar hides and desktop gap becomes click-through again");
            check(FarmPracticeTests.Xml(game.S)==state,"hover, leave and picker lifetime spend nothing or change progression");
            hover.Enter();buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.Panel!=null&&window.Panel.Tab=="卡牌冒险"&&game.C.Phase=="home"&&game.S.GuideStep==0&&game.S.Xp[0]==0&&window.desktopSuppressedForPanel,"expand opens card lobby without feeding and suppresses duplicate pet");
            window.ShowCompact();check(window.IsCompact&&!window.Panel.IsVisible&&!hover.IsOpen,"compacting disposes old hover controller");
            buttons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!game.S.DesktopHidden,"old toolbar cannot hide a rebuilt pet");
            window.petHover.Enter();window.HideToTray();check(game.S.DesktopHidden&&!window.IsVisible&&!window.Panel.IsVisible,"explicit hide folds both surfaces without affecting the tutorial");
            check(window.desktopHandle==IntPtr.Zero&&new WindowInteropHelper(window.Panel).Handle==IntPtr.Zero&&window.tray==null&&!window.clock.IsEnabled&&!window.animation.IsEnabled,"compact checks create no native windows, tray or game timers");
        }
        string path=Path.Combine(folder,"pet-start-normal.xml");var normal=new FarmGame(path);normal.S.Xp[0]=12;normal.S.GuideStep=2;normal.S.GuideInWorld=true;normal.S.DesktopMode="farm";normal.S.PetSize=176;normal.S.DesktopHidden=true;normal.Save();
        var reloaded=new FarmGame(path);PrepareDesktopStart(reloaded);var resumed=new FarmWindow(reloaded,true);
        check(resumed.IsCompact&&resumed.Panel==null&&reloaded.S.DesktopHidden&&reloaded.S.PetSize==176&&reloaded.S.Xp[0]==12,"saved full-farm layout starts compact while preserving explicit hiding, size and growth");
        resumed.OpenCompactPanel();check(resumed.Panel.Tab=="卡牌冒险"&&reloaded.S.GuideStep==2&&reloaded.S.Xp[0]==12,"new default leaves old guide progress untouched");
        resumed.Panel.Tab="新手引导";resumed.Panel.Render();check(reloaded.S.GuideStep==3&&reloaded.S.Xp[0]==12,"explicit old guide resumes without a second feed");
        reloaded.Save();var again=new FarmGame(path);check(again.S.Xp[0]==12&&again.S.GuideStep==3&&again.S.DesktopMode=="pet","new presentation and continued lesson survive isolated reload");
        File.WriteAllText(Path.Combine(folder,"pet-start-regression.txt"),"PASS: compact startup with explicit first entry; no automatic panel; three reading scales x three pet sizes; actual alpha corridor to all three buttons; 650ms deferred exit/re-entry; keyboard focus and picker keepalive; actual expand, compact and hide commands; stale toolbar disposal; single-pet panel policy; old once-fed guide continuation; isolated save roundtrip. Detached WPF only. Native pointer routing, physical timing, monitor DPI and Windows focus remain unverified.");
    }
}
