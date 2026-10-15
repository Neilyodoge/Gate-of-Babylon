using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void EverydayFixture()
    {
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=8;G.S.SimpleCourtyard=true;G.S.Xp[0]=36;G.S.GuideHomeStage=HomeGuideStage.FreshBerries;
        G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(2).Ticks;for(int i=0;i<G.Plots;i++)G.S.Growth[i]=0;
        Go("农场");
    }
    private void EverydayPreview(string root,string name,int size)
    {
        ReadingPreview(root,"everyday-"+name,size,true);GuideFits(GuidePrimary());
    }
    internal void RenderEverydayChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;EverydayFixture();GuidePreview(root,"everyday-home");
        string before=FarmPracticeTests.Xml(G.S);check(Convert.ToString(GuidePrimary().Content)=="去喂泡泡","home action says it opens feeding");
        GuideClick(GuidePrimary());GuidePreview(root,"everyday-companion");check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==before,"going to feed spends nothing");
        var feed=GuidePrimary();GuideClick(feed);GuidePreview(root,"everyday-fed");check(G.S.Xp[0]==48&&G.S.Berries==8,"everyday feeding uses the real transaction");
        before=FarmPracticeTests.Xml(G.S);feed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale feeding cannot spend twice");
        G.S.Berries=0;GuidePreview(root,"everyday-shortage");check(Convert.ToString(GuidePrimary().Content)=="去看看莓果"&&GuideWords().Contains("篮子里有 0 颗"),"shortage offers one clear food-source action");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());GuidePreview(root,"everyday-berry-wait");int plot=scenes.Single().LearningPlot;
        check(Tab=="莓果田"&&QuietShell&&WantsGentleNarrow&&plot>=0&&!UIChildren<ComboBox>((DependencyObject)Content).Any()&&!HasGuideButton("管理农田")&&FarmPracticeTests.Xml(G.S)==before,"food source stays small, watches one actual plot and does not alter crops");
        var progress=LessonProgress("quiet-berry-progress");check(progress.Value==0,"previous completed harvest lesson cannot fake a full progress bar");
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(7));RefreshCounters();
        check(Math.Abs(progress.Value-100*G.S.Growth[plot]/G.CropSeconds(0))<.001&&G.S.Berries==0,"clock refresh updates the visible actual progress without reopening the page");
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(G.SecondsLeft(plot)+.1));RefreshCounters();
        check(G.S.Berries>=2&&Convert.ToString(GuidePrimary().Content)=="回去喂泡泡"&&GuideWords().Contains("莓果够喂一口啦")&&progress.Value<10,"real automatic harvest enables returning to feed and retains the next batch's actual progress");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());GuidePreview(root,"everyday-berry-return");check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==before,"returning with berries does not feed or award them twice");
        int berries=G.S.Berries,xp=G.S.Xp[0];GuideClick(GuidePrimary());GuidePreview(root,"everyday-berry-fed");check(G.S.Berries==berries-2&&G.S.Xp[0]==xp+12,"returned berries feed exactly once on explicit action");

        EverydayFixture();G.S.Berries=0;for(int i=0;i<G.Plots;i++)G.S.Crops[i]=1;Go("莓果田");GuidePreview(root,"everyday-no-berry-crop");
        before=FarmPracticeTests.Xml(G.S);check(Convert.ToString(GuidePrimary().Content)=="去选择一块田"&&GuideWords().Contains("改种会重新计时")&&scenes.Single().LearningPlot==-1,"no planted berries has an honest recovery action and no fabricated countdown");
        GuideClick(GuidePrimary());GuidePreview(root,"everyday-manage-choice");check(Tab=="农田"&&FarmPracticeTests.Xml(G.S)==before&&G.S.Crops.Take(G.Plots).All(c=>c==1),"only explicit request opens management and never automatically replants");
        G.Plant(1,0);Go("莓果田");GuidePreview(root,"everyday-replanted");check(scenes.Single().LearningPlot==1&&LessonProgress("quiet-berry-progress").Value==0,"actual chosen berry plot becomes the source");
        G.S.Xp[0]=1250;G.S.Bond=3;Go("伙伴");GuidePreview(root,"everyday-grown");before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());
        check(Convert.ToString(GuidePrimary().Content)=="摸摸泡泡"&&FarmPracticeTests.Xml(G.S)==before,"fully grown companion offers a no-cost action even without berries");

        EverydayFixture();before=FarmPracticeTests.Xml(G.S);Go("冒险");GuidePreview(root,"everyday-departure");
        check(SmallRepeatDeparture&&WantsGentleNarrow&&FarmPracticeTests.Xml(G.S)==before&&!GuideWords().Contains("攻击")&&!HasGuideButton("看看要去的地方"),"repeat outing is one small confirmation without another introduction or random roll");
        var oldStart=GuidePrimary();GuideClick(UIAction("先待在家里"));GuidePreview(root,"everyday-cancelled");Go("冒险");GuidePreview(root,"everyday-reopened");
        oldStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"cancelled repeat departure cannot revive after reopening");
        oldStart=GuidePrimary();GuideClick(UIAction("农场"));GuidePreview(root,"everyday-nav-away");Go("冒险");GuidePreview(root,"everyday-nav-return");oldStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"ordinary navigation also invalidates old departure controls");
        var details=UIChildren<Expander>((DependencyObject)Content).Single();details.IsExpanded=true;GuidePreview(root,"everyday-preparation-details");
        KeyPress(Key.Escape);GuidePreview(root,"everyday-folded");check(!repeatDetailsOpen&&SmallRepeatDeparture&&FarmPracticeTests.Xml(G.S)==before,"Escape first closes optional settings without changing the journey");
        KeyPress(Key.Escape);GuidePreview(root,"everyday-escape-home");check(Tab=="农场"&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"second Escape returns home without cost or random changes");
        Go("冒险");ResetPanelInput();GuidePreview(root,"everyday-free-ready");KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"Tab selects the repeat departure action first");
        KeyPress(Key.Enter);GuidePreview(root,"everyday-free-started");check(G.JourneyRunning&&G.R.Picnic==-1&&G.S.Berries==10&&G.S.Grain==4,"only explicit departure starts a free real journey");

        EverydayFixture();G.PlanPicnic(0);Go("冒险");GuidePreview(root,"everyday-paid-ready");check(GuideWords().Contains("出发消耗：莓果 2 · 麦穗 2"),"existing picnic's exact cost is visible before departure");
        var paid=GuidePrimary();GuideClick(paid);GuidePreview(root,"everyday-paid-started");check(G.R.Picnic==0&&G.S.Berries==8&&G.S.Grain==2,"planned picnic is charged exactly once");
        before=FarmPracticeTests.Xml(G.S);paid.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old paid departure cannot double charge");
        EverydayFixture();G.PlanPicnic(0);G.S.Berries=0;Go("冒险");GuidePreview(root,"everyday-picnic-shortage");paid=GuidePrimary();check(paid.IsEnabled&&Convert.ToString(paid.Content)=="先看看缺的材料","missing picnic ingredients offer a material source instead of spending");
        GuideClick(UIAction("不带便当"));GuidePreview(root,"everyday-picnic-cancelled");before=FarmPracticeTests.Xml(G.S);paid.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&G.S.PlannedPicnic==-1&&FarmPracticeTests.Xml(G.S)==before&&GuidePrimary().IsEnabled,"dropping the picnic gives a free departure and invalidates the old price");
        GuideClick(GuidePrimary());check(G.JourneyRunning&&G.S.Berries==0&&G.S.Grain==4&&G.R.Picnic==-1,"free departure remains possible with no berries");

        EverydayFixture();G.S.Evolved=true;G.S.ExpeditionClears[3]=1;journeyDifficulty=1;journeyDepth=1;Go("冒险");GuidePreview(root,"everyday-cave");
        check(GuideWords().Contains("洞穴 · 深入")&&Convert.ToString(GuidePrimary().Content).Contains("洞穴"),"existing selected destination is named accurately");GuideClick(GuidePrimary());check(G.R.Difficulty==1&&G.R.Depth==1,"confirmed destination matches the actual trip");
        EverydayFixture();Go("冒险");GuidePreview(root,"everyday-expand");UIChildren<Expander>((DependencyObject)Content).Single().IsExpanded=true;UIPreview(root,"everyday-expand-visible",484,501);GuideClick(UIAction("查看完整准备"));GuidePreview(root,"everyday-full-preparation");
        check(!G.S.SimpleCourtyard&&G.AtHome&&HasGuideButton("回简洁小院"),"advanced preparation remains available by explicit request");

        EverydayFixture();G.S.GuideStep=-1;string path=Path.Combine(root,"everyday-old-save.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var normal=new FarmGame(path);var window=new FarmWindow(normal,true);var panel=new FarmPanel(window);window.Panel=panel;panel.Go("冒险");panel.GuidePreview(root,"everyday-normal-resume");
        check(panel.SmallRepeatDeparture&&normal.AtHome&&!normal.OpenGuideOnLaunch,"normal saved simple mode and older guide state resume without replaying lessons or departing");
        for(int size=0;size<3;size++){
            EverydayFixture();Go("伙伴");EverydayPreview(root,"companion",size);GuideFits(UIAction("回小院"));
            G.S.Berries=0;EverydayPreview(root,"shortage",size);GuideFits(UIAction("回小院"));GuideClick(GuidePrimary());EverydayPreview(root,"waiting",size);GuideFits(UIAction("回小院"));
            G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(46));EverydayPreview(root,"ready",size);GuideFits(UIAction("回小院"));
            G.S.Berries=0;for(int i=0;i<G.Plots;i++)G.S.Crops[i]=1;EverydayPreview(root,"no-crop",size);GuideFits(UIAction("回小院"));
            Go("冒险");EverydayPreview(root,"departure",size);GuideFits(UIAction("先待在家里"));
            G.PlanPicnic(0);EverydayPreview(root,"picnic-shortage",size);GuideFits(UIAction("先待在家里"));GuideFits(UIAction("不带便当"));
            G.S.Berries=10;EverydayPreview(root,"picnic-ready",size);GuideFits(UIAction("先待在家里"));
        }
        var stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();GuidePreview(root,"everyday-reset");before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.S.GuideStep==0&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before&&!IsVisible&&!owner.IsVisible&&G.SavePath==null&&!window.IsVisible&&!panel.IsVisible,"old everyday controls cannot affect a fresh isolated tutorial");
        File.WriteAllText(Path.Combine(root,"everyday-regression.txt"),"PASS: ordinary feed and stale transaction; shortage opens one actual berry plot; live clock and real harvest; no fake completed progress or automatic feed; missing crop requires explicit management; max-level no-cost companionship; narrow repeat preparation with exact destination and picnic cost; cancellation, navigation and stale prices; keyboard and Escape; explicit full preparation; old simple-mode save; three font sizes including all main/return actions; fresh reset. Detached WPF and isolated saves only.");
    }
}
