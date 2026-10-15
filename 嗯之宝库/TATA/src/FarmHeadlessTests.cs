using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class FarmHeadlessTests
{
    public static int Run()
    {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"farm-checks","background-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        try{
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"rules.txt"),FarmTests.Run(root));
            File.WriteAllText(Path.Combine(root,"journey-rules.txt"),JourneyTests.Run(root));
            File.WriteAllText(Path.Combine(root,"pet-rules.txt"),PetTests.Run(root));
            File.WriteAllText(Path.Combine(root,"progression-rules.txt"),ProgressionTests.Run(root));
            File.WriteAllText(Path.Combine(root,"management-rules.txt"),FarmManagementTests.Run(root));
            File.WriteAllText(Path.Combine(root,"decorating-rules.txt"),FarmDecoratingTests.Run(root));
            File.WriteAllText(Path.Combine(root,"care-rules.txt"),FarmCareTests.Run(root));
            File.WriteAllText(Path.Combine(root,"battle-review-rules.txt"),FarmBattleReviewTests.Run(root));
            File.WriteAllText(Path.Combine(root,"practice-rules.txt"),FarmPracticeTests.Run(root));
            File.WriteAllText(Path.Combine(root,"inspiration-rules.txt"),FarmInspirationTests.Run(root));
            File.WriteAllText(Path.Combine(root,"picnic-rules.txt"),FarmPicnicTests.Run(root));
            File.WriteAllText(Path.Combine(root,"homecoming-rules.txt"),FarmHomecomingTests.Run(root));
            File.WriteAllText(Path.Combine(root,"cards-rules.txt"),FarmCardsTests.Run(root));
            File.WriteAllText(Path.Combine(root,"branch-rules.txt"),FarmCardBranchTests.Run(root));
            File.WriteAllText(Path.Combine(root,"cards-forecast-rules.txt"),FarmCardForecastTests.Run(root));
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            File.WriteAllText(Path.Combine(root,"guide-rules.txt"),FarmGuideTests.Run(root));
            File.WriteAllText(Path.Combine(root,"reading-idle-rules.txt"),FarmReadingTests.Run(root));
            var guideGame=new FarmGame(null,null,true);var guideWindow=new FarmWindow(guideGame,true);guideWindow.Panel=new FarmPanel(guideWindow);guideWindow.Panel.RenderBranchChecks(root);guideWindow.Panel.RenderCardChecks(root);guideWindow.Panel.RenderGuideChecks(root);
            guideWindow.Panel.RenderCardFeedbackChecks(root);
            guideWindow.Panel.RenderCardForecastChecks(root);
            guideWindow.Panel.RenderCardDamageChecks(root);
            guideWindow.Panel.RenderCardPileChecks(root);
            guideWindow.Panel.RenderRewardFitChecks(root);
            guideWindow.Panel.RenderCardBrowseChecks(root);
            guideWindow.Panel.RenderCardCampChecks(root);
            guideWindow.Panel.RenderCardLessonChecks(root);
            guideWindow.Panel.RenderCardReceiptChecks(root);
            guideWindow.Panel.RenderCardTurnReceiptChecks(root);
            guideWindow.Panel.RenderCardInspectChecks(root);
            guideWindow.Panel.RenderCardStatusChecks(root);
            guideWindow.Panel.RenderQuietUXChecks(root);
            guideWindow.Panel.RenderCompanionFeedbackChecks(root);
            guideWindow.Panel.RenderFirstFeedingChecks(root);
            guideWindow.Panel.RenderGuideBerriesChecks(root);
            guideWindow.Panel.RenderKeyboardChecks(root);
            guideWindow.Panel.RenderReadingChecks(root);
            guideWindow.Panel.RenderFinalUXChecks(root);
            guideWindow.Panel.RenderSmallStepsChecks(root);
            guideWindow.Panel.RenderFirstOutingChecks(root);
            guideWindow.Panel.RenderFirstHomecomingChecks(root);
            guideWindow.Panel.RenderEverydayChecks(root);
            guideWindow.Panel.RenderLaterLoopChecks(root);
            guideWindow.Panel.RenderSmallProgressionChecks(root);
            guideWindow.Panel.RenderLifeChecks(root);
            guideWindow.Panel.RenderSmallResourceChecks(root);
            guideWindow.Panel.RenderContinuityChecks(root);
            guideWindow.Panel.RenderTravelContinuityChecks(root);
            guideWindow.Panel.RenderPreparationChecks(root);
            guideWindow.Panel.RenderUXReviewChecks(root);
            guideWindow.Panel.RenderCropRecoveryChecks(root);
            guideWindow.Panel.RenderCrystalJourneyChecks(root);
            guideWindow.Panel.RenderJourneyHealthChecks(root);
            guideWindow.Panel.RenderPicnicCompanionChecks(root);
            guideWindow.Panel.RenderChapterGuideChecks(root);
            guideWindow.Panel.RenderChapterReturnChecks(root);
            guideWindow.Panel.RenderCompanionFocusChecks(root);
            guideWindow.Panel.RenderSkillCareChecks(root);
            guideWindow.BackgroundPanelPresenceChecks(root);
            guideWindow.BackgroundPetStartChecks(root);
            guideWindow.BackgroundGuideLaunchChecks();var guideNormalWindow=new FarmWindow(new FarmGame(Path.Combine(root,"guide-message-normal.xml")),true);guideNormalWindow.BackgroundGuideLaunchChecks();
            File.WriteAllText(Path.Combine(root,"试玩验收.md"),FarmTrialTests.Run(root));
            var trialGame=new FarmGame(Path.Combine(root,"trial-ui-save.xml"));var trialWindow=new FarmWindow(trialGame,true);trialWindow.Panel=new FarmPanel(trialWindow);trialWindow.Panel.RenderTrialChecks(root);
            var returnGame=new FarmGame(Path.Combine(root,"return-ux-save.xml"));var returnWindow=new FarmWindow(returnGame,true);returnWindow.Panel=new FarmPanel(returnWindow);returnWindow.Panel.RenderReturnUXChecks(root);
            var focusGame=new FarmGame(Path.Combine(root,"focus-ux-save.xml"));var focusWindow=new FarmWindow(focusGame,true);focusWindow.Panel=new FarmPanel(focusWindow);focusWindow.Panel.RenderFocusUXChecks(root);
            var homeUXGame=new FarmGame(Path.Combine(root,"home-ux-save.xml"));var homeUXWindow=new FarmWindow(homeUXGame,true);homeUXWindow.Panel=new FarmPanel(homeUXWindow);homeUXWindow.Panel.RenderHomeUXChecks(root);
            var game=new FarmGame(Path.Combine(root,"background-save.xml"));
            var window=new FarmWindow(game,true);
            window.BackgroundChecks(root);
            var journeyGame=new FarmGame(Path.Combine(root,"journey-visual-save.xml"));var journeyWindow=new FarmWindow(journeyGame,true);journeyWindow.BackgroundJourneyChecks(root);
            var petGame=new FarmGame(Path.Combine(root,"pet-visual-save.xml"));var petWindow=new FarmWindow(petGame,true);petWindow.BackgroundPetChecks(root);
            var progressionGame=new FarmGame(Path.Combine(root,"progression-visual-save.xml"));var progressionWindow=new FarmWindow(progressionGame,true);progressionWindow.BackgroundProgressionChecks(root);
            var uiGame=new FarmGame(Path.Combine(root,"ui-visual-save.xml"));var uiWindow=new FarmWindow(uiGame,true);uiWindow.Panel=new FarmPanel(uiWindow);uiWindow.Panel.RenderUIChecks(root);
            var travelGame=new FarmGame(Path.Combine(root,"travel-ui-save.xml"));var travelWindow=new FarmWindow(travelGame,true);travelWindow.Panel=new FarmPanel(travelWindow);travelWindow.Panel.RenderTravelUIChecks(root);
            var presenceGame=new FarmGame(Path.Combine(root,"presence-save.xml"));var presenceWindow=new FarmWindow(presenceGame,true);presenceWindow.BackgroundPresenceChecks(root);
            var workshopGame=new FarmGame(Path.Combine(root,"workshop-ui-save.xml"));var workshopWindow=new FarmWindow(workshopGame,true);workshopWindow.Panel=new FarmPanel(workshopWindow);workshopWindow.Panel.RenderWorkshopChecks(root);
            var decoratingGame=new FarmGame(Path.Combine(root,"decorating-ui-save.xml"));var decoratingWindow=new FarmWindow(decoratingGame,true);decoratingWindow.Panel=new FarmPanel(decoratingWindow);decoratingWindow.Panel.RenderDecoratingChecks(root);decoratingWindow.BackgroundDecorationDisplayChecks(root);
            var careGame=new FarmGame(Path.Combine(root,"care-ui-save.xml"));var careWindow=new FarmWindow(careGame,true);careWindow.Panel=new FarmPanel(careWindow);careWindow.Panel.RenderCareChecks(root);
            var reviewGame=new FarmGame(Path.Combine(root,"battle-review-ui-save.xml"));var reviewWindow=new FarmWindow(reviewGame,true);reviewWindow.Panel=new FarmPanel(reviewWindow);reviewWindow.Panel.RenderBattleReviewChecks(root);
            var practiceGame=new FarmGame(Path.Combine(root,"practice-ui-save.xml"));var practiceWindow=new FarmWindow(practiceGame,true);practiceWindow.Panel=new FarmPanel(practiceWindow);practiceWindow.Panel.RenderPracticeChecks(root);
            var inspirationGame=new FarmGame(Path.Combine(root,"inspiration-ui-save.xml"));var inspirationWindow=new FarmWindow(inspirationGame,true);inspirationWindow.Panel=new FarmPanel(inspirationWindow);inspirationWindow.Panel.RenderInspirationChecks(root);
            var picnicGame=new FarmGame(Path.Combine(root,"picnic-ui-save.xml"));var picnicWindow=new FarmWindow(picnicGame,true);picnicWindow.Panel=new FarmPanel(picnicWindow);picnicWindow.Panel.RenderPicnicChecks(root);
            var homecomingGame=new FarmGame(Path.Combine(root,"homecoming-ui-save.xml"));var homecomingWindow=new FarmWindow(homecomingGame,true);homecomingWindow.Panel=new FarmPanel(homecomingWindow);homecomingWindow.Panel.RenderHomecomingChecks(root);
            foreach(Window w in app.Windows)Check(!w.IsVisible&&new WindowInteropHelper(w).Handle==IntPtr.Zero,"test created a native window");
            File.WriteAllText(Path.Combine(root,"result.txt"),"PASS: no native windows, tray icons, global hotkeys or player-save access; gameplay regression; multi-monitor placement; work preferences; frozen/hidden animation policy; locked input; old-save migration; detached rendering of desktop and all gameplay pages.");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"farm-checks","latest-background-check.txt"),root);return 0;
        }catch(Exception ex){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"error.txt"),ex.ToString());File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"farm-checks","latest-background-check.txt"),root);return 1;}
    }
    internal static void Check(bool ok,string label){if(!ok)throw new Exception("Background test: "+label);}
    internal static void Render(Window window,string path,double width,double height)
    {
        var content=(FrameworkElement)window.Content;window.Content=null;
        try{RenderContent(content,path,width,height);}finally{window.Content=content;}
    }
    internal static void RenderContent(FrameworkElement content,string path,double width,double height)
    {
            content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);bitmap.Render(content);
            var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);int visible=0;for(int i=3;i<pixels.Length;i+=4)if(pixels[i]>0)visible++;
            Check(visible>200,"empty offscreen render: "+Path.GetFileName(path));
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    }
}

public partial class FarmWindow
{
    public void BackgroundPetSwitcherChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.S.DesktopMotion=false;G.S.DesktopLocked=true;G.S.DesktopHidden=true;G.S.DesktopMode="pet";
        G.S.SeedStage=3;G.S.DisplayPet=0;BuildDesktop();PlaceDesktop();
        double x=Left,y=Top;var team=(int[])G.S.Team.Clone();int caretaker=G.Caretaker,round=G.R.Round;long next=G.R.NextJourneyTicks;var hp=(int[])G.R.Hp.Clone();
        for(int i=1;i<=FarmGame.PetCount;i++){
            petImage.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Middle){RoutedEvent=UIElement.MouseDownEvent});
            check(G.S.DisplayPet==i%FarmGame.PetCount&&Object.ReferenceEquals(petImage.Source,Art.Frame(G.S.DisplayPet,Tick,G.S.Evolved,G.S.SeedStage)),"middle-click cycles and refreshes frozen artwork");
        }
        var picker=DisplayPetPicker();((MenuItem)picker.Items[5]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(G.S.DisplayPet==4&&G.S.Team.SequenceEqual(team)&&G.Caretaker==caretaker&&G.R.Round==round&&G.R.NextJourneyTicks==next&&G.R.Hp.SequenceEqual(hp),"picker leaves party and active journey untouched");
        check(Left==x&&Top==y&&G.S.DesktopHidden&&!IsVisible&&!animation.IsEnabled&&G.S.DesktopLocked,"pet selection preserves placement, hidden and work settings");
        var reload=new FarmGame(G.SavePath);check(reload.S.DisplayPet==4&&reload.S.SeedStage==3&&reload.S.DesktopHidden,"selected evolved pet persists");
        SelectDisplayPet(-1);SelectDisplayPet(FarmGame.PetCount);check(G.S.DisplayPet==4,"invalid display pet is ignored");
        foreach(int size in new[]{96,128,176}){
            G.S.PetSize=size;BuildDesktop();petTools.Opacity=1;
            FarmHeadlessTests.Render(this,Path.Combine(root,"switcher-compact-"+size+".png"),Width,Height);
            check(petTools.ActualWidth<=Width&&petTools.ActualHeight<=42*new[]{1d,1.15,1.3}[G.S.ReadingSize],"hover controls fit without enlarging compact window");
        }
        FarmHeadlessTests.RenderContent(DisplayPetPicker(),Path.Combine(root,"switcher-pet-picker.png"),300,328);
        G.Retreat();G.Home();G.S.Xp[4]=1250;G.S.SeedStage=0;G.S.SeedBond=0;G.S.Berries=10;Panel.Tab="进化";Panel.Render();
        FarmHeadlessTests.Render(Panel,Path.Combine(root,"evolution-max-level-bond.png"),764,571);
        var feed=FindButton((DependencyObject)Panel.Content,"亲密喂养 · 莓果 2");check(feed!=null&&feed.IsEnabled,"max-level evolution page exposes bond feeding");
        G.S.DesktopMode="farm";BuildDesktop();SelectDisplayPet(2);check(!IsCompact&&!IsVisible,"tray/panel display selection does not force a desktop mode change");
        check(desktopHandle==IntPtr.Zero&&tray==null&&!animation.IsEnabled&&!clock.IsEnabled,"switcher checks create no native resources");
        File.WriteAllText(Path.Combine(root,"pet-switcher.txt"),"PASS: middle-click wraparound; portrait picker; evolved frame while frozen; party, journey, hidden mode and position preserved; reload; three compact sizes; max-level bond UI; no native windows.");
    }
    public void BackgroundChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        check(!ShowActivated&&!IsVisible&&desktopHandle==IntPtr.Zero&&tray==null&&!compactKey&&!hideKey,"startup stays passive without native resources");
        check(!animation.IsEnabled&&!clock.IsEnabled,"background test has no running game timers");
        var primary=new Rect(0,0,1920,1040);var left=new Rect(-1280,0,1280,984);var right=new Rect(1920,-200,2560,1400);testWorkAreas=new[]{primary,left,right};
        check(DesktopLayout.ChooseArea(new Rect(-1000,100,156,170),testWorkAreas)==left,"negative-coordinate monitor detected");
        check(DesktopLayout.ChooseArea(new Rect(1940,-120,156,170),testWorkAreas)==right,"upper/right monitor detected");
        check(DesktopLayout.ChooseArea(new Rect(1850,80,156,170),testWorkAreas)==right,"monitor with greatest overlap selected");
        var recovered=DesktopLayout.Clamp(new Rect(-1900,1300,156,170),primary);
        check(recovered.Left==0&&recovered.Bottom==primary.Bottom,"disconnected monitor recovers on screen above taskbar");
        check(DesktopLayout.Clamp(new Rect(0,0,800,284),new Rect(0,0,640,240)).Width==640,"oversized bounds remain reachable");
        check(DesktopLayout.ShouldAnimate(true,true,false)&&!DesktopLayout.ShouldAnimate(true,false,false)&&!DesktopLayout.ShouldAnimate(false,true,false)&&DesktopLayout.ShouldAnimate(true,false,true),"pause and visibility animation policy");

        G.S.PetLeft=-1000;G.S.PetTop=730;PlaceDesktop();check(Left==-1000&&Top==730,"saved pet stays on left monitor");
        FarmHeadlessTests.Render(this,Path.Combine(root,"01-compact.png"),Width,Height);
        SetDesktopPreference("topmost",false);SetDesktopPreference("motion",false);SetDesktopPreference("locked",true);SetDesktopPreference("hotkeys",false);
        check(!Topmost&&!G.S.DesktopMotion&&G.S.DesktopLocked&&!G.S.DesktopHotkeys&&!animation.IsEnabled,"work preferences apply without animation");
        int size=G.S.PetSize;
        ((UIElement)Content).RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.MouseWheelEvent});
        check(G.S.PetSize==size,"locked pet ignores scroll");
        SetDesktopPreference("locked",false);
        ((UIElement)Content).RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.MouseWheelEvent});
        check(G.S.PetSize==size+16,"unlocked pet accepts scroll without rebuilding a window");
        bool handled=false;DesktopMessage(IntPtr.Zero,0x0312,new IntPtr(CompactHotkey),IntPtr.Zero,ref handled);
        check(!handled&&IsCompact&&!IsVisible,"disabled hotkeys ignore queued messages");
        SetDesktopPreference("locked",true);G.Save();var reload=new FarmGame(G.SavePath);
        check(!reload.S.DesktopTopmost&&!reload.S.DesktopMotion&&reload.S.DesktopLocked&&!reload.S.DesktopHotkeys,"work preferences persist");
        string oldPath=Path.Combine(root,"pre-work-settings.xml");
        File.WriteAllText(oldPath,System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(G.SavePath),@"\s*<(DesktopTopmost|DesktopMotion|DesktopLocked|DesktopHotkeys)>.*?</\1>",""));
        var old=new FarmGame(oldPath);check(old.SavePath==oldPath&&old.S.DesktopTopmost&&old.S.DesktopMotion&&!old.S.DesktopLocked&&old.S.DesktopHotkeys&&old.S.PetSize==G.S.PetSize,"older saves receive safe work-setting defaults");

        var previousPet=(UIElement)Content;G.S.DesktopMode="farm";G.S.Left=-1200;G.S.Top=660;BuildDesktop();PlaceDesktop();
        previousPet.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseLeaveEvent});
        check(Left==-1200&&Top==660,"full farm keeps independent position on secondary monitor");
        FarmHeadlessTests.Render(this,Path.Combine(root,"02-farm-strip.png"),Width,Height);
        testWorkAreas=new[]{primary};PlaceDesktop();check(Left>=0&&Left+Width<=primary.Right,"removed monitor brings saved farm back");
        testWorkAreas=new[]{primary,left,right};G.S.DesktopMode="pet";G.S.PetSize=96;BuildDesktop();PlaceDesktop();
        FarmHeadlessTests.Render(this,Path.Combine(root,"03-small-locked.png"),Width,Height);
        G.S.PetSize=176;G.S.DisplayPet=3;BuildDesktop();petTools.Opacity=1;
        FarmHeadlessTests.Render(this,Path.Combine(root,"04-large-controls.png"),Width,Height);

        Panel=new FarmPanel(this);check(!Panel.Topmost,"gameplay panel follows non-topmost setting");
        foreach(string tab in new[]{"农场","伙伴","冒险","怎么玩"}){Panel.Tab=tab;Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"panel-"+tab+".png"),924,721);}
        G.Feed(0);G.Feed(0);G.Feed(0);G.Begin(0,0);G.RouteChoice(0);Panel.Tab="冒险";Panel.Render();
        FarmHeadlessTests.Render(Panel,Path.Combine(root,"05-battle.png"),924,721);
        while(G.R.Phase=="battle")G.Step();Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"06-small-draft.png"),764,571);
        FarmTests.Clear(G);Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"07-result.png"),924,721);
        G.Home();G.Evolve();Panel.Tab="伙伴";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"08-evolution.png"),924,721);
        check(!IsVisible&&!Panel.IsVisible&&desktopHandle==IntPtr.Zero&&new WindowInteropHelper(Panel).Handle==IntPtr.Zero&&tray==null&&!animation.IsEnabled&&!clock.IsEnabled,"all rendering and event tests remain windowless");
        testWorkAreas=null;
    }
}



