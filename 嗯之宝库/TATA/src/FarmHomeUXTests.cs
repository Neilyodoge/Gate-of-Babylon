using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

public partial class FarmPanel
{
    private Button HomePrimary(){return UIChildren<Button>((DependencyObject)Content).First(b=>AutomationProperties.GetAutomationId(b)=="home-next-action");}
    private void HomeUXPreview(string root,string name,double width=764,double height=571){Render();UIPreview(root,"homeux-"+name,width,height);}
    internal void RenderHomeUXChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;
        string initial=FarmPracticeTests.Xml(G.S);Go("农场");HomeUXPreview(root,"first-open");
        check(initial==FarmPracticeTests.Xml(G.S),"home guide render has no gameplay or save effects");
        check(FarmLoopHint.Current(G).Kind=="feed"&&FarmLoopHint.Current(G).Pet==0,"new farm suggests feeding an active companion");
        UIVisible(HomePrimary(),"first recommended action visible at minimum size");UIVisible(UIAction("管理农田"),"field manager visible at minimum size");UIVisible(UIAction("布置小院"),"courtyard visible at minimum size");UIVisible(UIAction("喂养 · 莓果 2"),"companion feeding visible at minimum size");
        check(!UIChildren<ComboBox>((DependencyObject)Content).Any(),"first home does not show plot configuration");
        var nav=UIChildren<Button>((DependencyObject)Content).Select(AutomationProperties.GetName).ToArray();check(nav.Contains("农场")&&nav.Contains("伙伴")&&nav.Contains("冒险")&&nav.Contains("更多")&&!nav.Contains("配队")&&!nav.Contains("手册"),"primary navigation shows three categories and more");
        HomeUXPreview(root,"first-open-large",924,721);
        int berries=G.S.Berries;HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"first-feed");check(G.S.Xp[0]==12&&G.S.Berries==berries-2&&FarmLoopHint.Current(G).Kind=="feed","first feed costs exactly two berries and keeps clear next action");
        var lastFeed=HomePrimary();lastFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"ready");check(G.Level(0)==2&&FarmLoopHint.Current(G).Kind=="prepare","level two advances to departure preparation");
        string ready=FarmPracticeTests.Xml(G.S);lastFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==ready&&G.AtHome,"stale feed cannot become a departure action");
        HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"prepare");check(Tab=="冒险"&&G.AtHome&&journeyDifficulty==0&&journeyDepth==0,"recommended adventure only opens first-route preparation");UIVisible(UIAction("出发 · 自动走完整趟"),"real departure visible at minimum size");
        G.S.Xp=Enumerable.Repeat(55,FarmGame.PetCount).ToArray();UIAction("出发 · 自动走完整趟").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Go("农场");HomeUXPreview(root,"travel");check(G.JourneyRunning&&FarmLoopHint.Current(G).Kind=="journey"&&!UIAction("喂养 · 莓果 2").IsEnabled,"travel home shows active trip and disables care");HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="冒险"&&G.JourneyRunning,"progress shortcut does not interrupt trip");
        G.ToggleJourneyPause();Go("农场");HomeUXPreview(root,"paused");long next=G.R.NextJourneyTicks;HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="冒险"&&G.R.JourneyPaused&&G.R.NextJourneyTicks==next,"paused trip stays paused through home shortcut");
        G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(2));Go("农场");HomeUXPreview(root,"returned");check(G.R.Phase=="result","sample first trip finished");int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"result");check(Tab=="冒险"&&G.R.Phase=="result"&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals,"receipt opens without duplicate awards or dismissing result");
        UIAction("返家 · 升级农田").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"soil");check(G.AtHome&&FarmLoopHint.Current(G).Kind=="soil","first clear leads back to fertilizer step");UIVisible(HomePrimary(),"fertilizer action visible at minimum size");int soil=G.S.SoilLevel,cost=G.SoilCost;var soilButton=HomePrimary();soilButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"grown");check(G.S.SoilLevel==soil+1&&G.S.Fertilizer==fertilizer-cost,"fertilizer action spends exact cost once");string grown=FarmPracticeTests.Xml(G.S);soilButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==grown,"old soil button cannot pay a new price");
        Go("农场");HomeUXPreview(root,"mature",924,721);UIAction("更多").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"more");check(moreNavigationOpen,"more menu expands inline");UIVisible(UIAction("配队"),"more team entry visible");UIVisible(UIAction("手册"),"more handbook entry visible");UIAction("配队").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"team");check(Tab=="配队"&&!moreNavigationOpen&&MainCategory()=="伙伴","secondary team page highlights companion category and closes menu");
        Go("农场");HomeUXPreview(root,"manager-entry");UIAction("管理农田").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"fields",924,721);check(Tab=="农田"&&UIChildren<ComboBox>((DependencyObject)Content).Count()==G.Plots,"real unlocked crop controls retained in manager");UIAction("返回农场首页").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HomeUXPreview(root,"manager-back");check(Tab=="农场","manager returns directly to guided home");
        Go("怎么玩");HomeUXPreview(root,"help",924,721);UIAction("回首页，看看现在做什么").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="农场","short help connects to current action");
        G.S.SoilLevel=5;G.S.Wins=0;G.S.Xp=new int[FarmGame.PetCount];G.S.Berries=0;G.S.DisplayPet=4;HomeUXPreview(root,"shortage");check(FarmLoopHint.Current(G).Pet==G.S.Team[0]&&!HomePrimary().IsEnabled,"display-only pet does not redirect first-trip training away from party");UIVisible(UIAction("管理农田 · 种莓果"),"shortage provides visible crop source");G.S.Berries=2;RefreshCounters();check(HomePrimary().IsEnabled,"harvest enables current home action without navigating");HomeUXPreview(root,"harvest-ready");
        G.S.Berries=0;for(int i=0;i<G.Plots;i++)G.S.Crops[i]=1;RefreshCounters();check(FarmLoopHint.Current(G).Detail.Contains("没种莓果"),"unplanted crop is distinguished from a timed harvest");
        G.S.Wins=1;G.S.Xp[4]=55;G.S.SeedBond=3;G.S.Crystals=8;HomeUXPreview(root,"evolution");check(FarmLoopHint.Current(G).Kind=="evolve"&&FarmLoopHint.Current(G).Pet==4,"mature farm suggests actual available seed evolution");HomePrimary().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="进化"&&evolutionFocusPet==4&&!G.S.Evolved&&G.S.SeedStage==0,"evolution shortcut previews correct species without evolving");
        check(!owner.IsVisible&&!IsVisible,"UX checks remain detached without visible windows");
        File.WriteAllText(Path.Combine(root,"homeux-regression.txt"),"PASS: three primary categories; inline more and parent highlight; new-player action, manager and care visible at minimum size; read-only guidance; exact feed and soil costs; stale click protection; real first-loop transitions; preparation versus explicit departure; no automatic resume; receipt without repeat awards; all plot controls and direct back path; live shortage recovery; correct party and evolution targets; short help. Isolated saves and detached WPF rendering only.");
    }
}
