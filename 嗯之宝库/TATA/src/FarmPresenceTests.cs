using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

public partial class FarmWindow
{
    private void PresencePreview(string root,string name,bool hover=false)
    {
        RefreshCounters();petTools.Opacity=hover?1:0;FarmHeadlessTests.Render(this,Path.Combine(root,"presence-"+name+".png"),Width,Height);
    }
    public void BackgroundPresenceChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.S.GuideStep=9; // Full-feature presence checks; beginner companionship is covered by RenderGuideChecks.
        G.S.DesktopMode="pet";G.S.PetSize=128;G.S.DesktopMotion=false;G.S.DesktopLocked=true;BuildDesktop();PlaceDesktop();
        double x=Left,y=Top,w=Width,h=Height;var team=(int[])G.S.Team.Clone();
        check(FarmAttention.Current(G).Kind=="farm"&&compactBadge.Visibility==Visibility.Collapsed,"quiet new farm has no persistent corner badge");PresencePreview(root,"idle");
        G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.S.BlessingOffers=new[]{0,1,3};G.BeginJourney(0,0);G.RouteChoice(0);PresencePreview(root,"travel");
        check(compactBadge.Visibility==Visibility.Visible&&Convert.ToString(compactBadge.Content)=="1","compact badge reports journey station");PresencePreview(root,"hover",true);
        G.ToggleJourneyPause();PresencePreview(root,"paused");compactBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.R.JourneyPaused&&Panel.Tab=="冒险","pause badge opens adventure without silently resuming");
        SetDesktopPreference("hints",false);check(compactBadge.Visibility==Visibility.Collapsed,"corner indicator can be disabled");G.Save();var reload=new FarmGame(G.SavePath);check(!reload.S.DesktopStatusHints&&reload.R.JourneyPaused,"hint preference and paused journey survive reload");PresencePreview(root,"no-hints");
        string legacy=Path.Combine(root,"before-status-hints.xml");var old=XDocument.Load(G.SavePath);old.Root.Element("DesktopStatusHints").Remove();old.Save(legacy);var migrated=new FarmGame(legacy);check(migrated.SavePath==legacy&&migrated.S.DesktopStatusHints&&migrated.R.JourneyPaused&&migrated.S.Team.SequenceEqual(team),"old saves default hints on without resetting pets or journey");
        SetDesktopPreference("hints",true);G.S.DesktopHidden=true;G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(2));RefreshPresence();PresencePreview(root,"returned");
        check(G.R.Phase=="result"&&FarmAttention.Current(G).Kind=="result"&&G.S.DesktopHidden&&!IsVisible,"hidden trip completion updates state without restoring desktop");int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;compactBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.R.Phase=="result"&&Panel.Tab=="冒险"&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals,"return badge views receipt without claiming twice or leaving it");
        G.Home();G.S.Bond=3;G.S.Crystals=2;PresencePreview(root,"evolution");check(FarmAttention.Current(G).Kind=="evolve"&&FarmAttention.Current(G).Pet==0,"ready bubble is prioritized over chapter rewards");compactBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));FarmHeadlessTests.Render(Panel,Path.Combine(root,"presence-bubble-focus.png"),764,571);
        var evolve=FindButton((DependencyObject)Panel.Content,"进化 · 消耗晶石 2");check(evolve!=null&&evolve.TranslatePoint(new Point(0,0),(UIElement)Panel.Content).Y+evolve.ActualHeight<400,"bubble evolution shortcut reveals the correct pet above the fold");
        G.S.SeedBond=2;G.S.Crystals=3;check(FarmAttention.Current(G).Pet==4,"ready seed shortcut identifies seed");
        G.S.SeedBond=G.S.Bond=0;PresencePreview(root,"rewards");check(FarmAttention.Current(G).Kind=="chapter","unclaimed growth rewards are discoverable at home");compactBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Panel.Tab=="手册","reward badge opens the handbook");
        G.S.Run=new FarmRun();G.Begin(0,0);RefreshPresence();check(FarmAttention.Current(G).Kind=="legacy","legacy trip is not described as automatically running");G.Retreat();G.Home();
        foreach(int size in new[]{96,128,176}){G.S.PetSize=size;BuildDesktop();PresencePreview(root,"size-"+size,true);check(petTools.ActualHeight<=42&&petTools.ActualWidth<=Width&&Height==size+42,"presence toolbar fits existing compact bounds");}
        G.S.PetSize=128;BuildDesktop();check(Width==w&&Height==h&&Left==x&&Top==y&&G.S.Team.SequenceEqual(team),"status changes preserve size position and team");
        check(!IsVisible&&!Panel.IsVisible&&desktopHandle==IntPtr.Zero&&tray==null&&!clock.IsEnabled&&!animation.IsEnabled,"presence creates no windows tray icons timers or hotkeys");
        PresenceSheet(root);Panel.RenderHomeCareChecks(root);
        File.WriteAllText(Path.Combine(root,"presence-regression.txt"),"PASS: quiet idle; journey station; explicit pause; silent hidden completion; receipt routing without duplicate rewards; correct evolution target; reward routing; legacy state; hint toggle/reload/old-save migration; unchanged compact bounds, placement and team; inline home care and live next actions. No native resources or player-save access.");
    }
    private void PresenceSheet(string root)
    {
        var sheet=new StackPanel{Background=U.Cream,Margin=new Thickness(0)};sheet.Children.Add(U.Text("缩成一只宠物，也能知道伙伴在做什么",21));sheet.Children.Add(U.Text("角标不闪烁、不弹窗 · 鼠标移上查看文字 · 右键可以关闭角标",12,U.Muted));
        var row=new StackPanel{Orientation=Orientation.Horizontal};sheet.Children.Add(row);string[] files={"idle","travel","paused","returned","hover"},names={"日常陪伴","第 1 站旅行中","旅程暂停","伙伴归来","鼠标移上时"};
        for(int i=0;i<files.Length;i++){var p=new StackPanel{Width=164,Margin=new Thickness(0,8,6,0)};var title=U.Text(names[i],13);title.TextAlignment=TextAlignment.Center;p.Children.Add(title);p.Children.Add(new Image{Source=new BitmapImage(new Uri(Path.Combine(root,"presence-"+files[i]+".png"))),Width=156,Height=170});row.Children.Add(p);}
        FarmHeadlessTests.RenderContent(new Border{Child=sheet,Padding=new Thickness(14),Background=U.Cream},Path.Combine(root,"presence-overview.png"),878,300);
    }
}

public partial class FarmPanel
{
    internal void RenderHomeCareChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.DisplayPet=4;G.S.Berries=20;G.S.Grain=20;G.S.Xp[4]=0;G.S.SeedBond=0;Tab="农场";Render();UIPreview(root,"home-care");
        check(FarmAttention.Current(G).Kind=="chapter"&&FarmAttention.Current(G).Detail.Contains(FarmGame.ChapterNames[1]),"reward prompt names an actually completed chapter");
        int xp=G.S.Xp[4],berries=G.S.Berries;UIAction("喂养 · 莓果 2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.Xp[4]==xp+12&&G.S.Berries==berries-2&&G.S.SeedBond==1,"home feeding trains the displayed companion");Render();UIPreview(root,"home-fed-small",764,571);
        int grain=G.S.Grain,cost=G.TrainToNextCost(4);UIAction("升至 Lv.2 · 麦穗 "+cost).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.Level(4)==2&&G.S.Grain==grain-cost,"home level action spends exact training cost");
        G.S.SeedBond=G.S.Bond=0;G.ClaimReadyChapters();G.S.Harvests=0;G.S.Fertilizer=G.SoilCost;Render();UIPreview(root,"home-soil");int soil=G.S.SoilLevel;UIAction("施肥升级 · 肥料 "+G.SoilCost).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.SoilLevel==soil+1,"explicit home fertilizer action upgrades soil");
        G.S.BlessingOffers=new[]{0,1,3};G.BeginJourney(0,0);Render();UIPreview(root,"home-away");check(!UIAction("喂养 · 莓果 2").IsEnabled,"home feeding stays locked during travel");UIAction("看看旅行进度").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="冒险"&&G.JourneyRunning,"home travel action routes without interrupting travel");
        G.Retreat();Tab="农场";Render();UIPreview(root,"home-return");UIAction("查看本趟收获").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="冒险"&&G.R.Phase=="result","home receipt prompt retains result until player returns");
    }
}
