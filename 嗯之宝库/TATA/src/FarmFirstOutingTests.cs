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
    private void FirstOutingFixture(string root,string name)
    {
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;G.S.GuideHomeStage=HomeGuideStage.StayingHome;G.S.Xp[0]=36;
        Go("冒险");GuidePreview(root,name);
    }
    internal void RenderFirstOutingChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;FirstOutingFixture(root,"outing-friends");
        check(SmallDeparture&&WantsGentleNarrow&&G.S.GuideDepartureLesson==0&&G.AtHome,"first invitation opens only the small companion introduction");
        check(G.S.Team.All(p=>GuideWords().Contains(G.Name(p)))&&!GuideWords().Contains("分钟")&&!GuideWords().Contains("放电")&&!GuideWords().Contains("肥料"),"actual companions appear before destination, abilities or rewards");
        var intro=GuidePrimary();var before=FarmPracticeTests.Xml(G.S);GuideClick(intro);GuidePreview(root,"outing-destination");
        check(G.S.GuideDepartureLesson==1&&G.AtHome&&G.S.BlessingOffers.Length==0,"reading the next page neither launches nor rolls a blessing");
        G.S.GuideDepartureLesson=0;check(FarmPracticeTests.Xml(G.S)==before,"acknowledgement changes no resources, seed, clock, party or run");G.S.GuideDepartureLesson=1;
        check(scenes.Single().JourneyPreview&&GuideWords().Contains("不用准备材料")&&!GuideWords().Contains("Lv."),"destination preview uses real companions without adding combat stats");
        string path=Path.Combine(root,"first-outing-ready.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var resumed=new FarmGame(path);
        check(resumed.S.GuideDepartureLesson==1&&resumed.HomeGuideResumeTab=="冒险"&&resumed.AtHome,"normal save resumes the destination without automatically departing");
        var xml=XDocument.Load(path);xml.Root.Element("GuideDepartureLesson").Remove();string oldPath=Path.Combine(root,"first-outing-old.xml");xml.Save(oldPath);var oldGame=new FarmGame(oldPath);
        check(oldGame.S.GuideDepartureLesson==-1&&oldGame.S.Xp[0]==36&&oldGame.S.GuideOutingAccepted,"previous saves keep their ready preparation and existing growth");
        var ready=GuidePrimary();GuideClick(UIAction("先待在家里"));GuidePreview(root,"outing-cancelled");
        check(G.AtHome&&!G.S.GuideOutingAccepted&&G.S.GuideHomeStage==HomeGuideStage.StayingHome,"cancelling preparation returns to quiet home");
        Go("冒险");GuidePreview(root,"outing-reopened");check(G.S.GuideDepartureLesson==1,"reopening does not repeat the already-read companion introduction");
        before=FarmPracticeTests.Xml(G.S);ready.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));intro.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(FarmPracticeTests.Xml(G.S)==before&&G.AtHome,"cancelled preparation controls cannot revive after the page is reopened");
        int berries=G.S.Berries,grain=G.S.Grain;var start=GuidePrimary();GuideClick(start);GuidePreview(root,"outing-started");
        check(G.JourneyRunning&&G.S.GuideStep==4&&G.S.Berries==berries&&G.S.Grain==grain&&G.R.Picnic==-1,"only the explicit final action starts the original free trip");
        before=FarmPracticeTests.Xml(G.S);start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"departure cannot repeat");
        check(LessonProgress("gentle-trip-progress").Value==G.R.Node&&LessonProgress("gentle-trip-progress").Maximum==5&&!GuideWords().Contains("潮湿追击"),"travel shows actual completed stations with optional combination details");
        GuideClick(UIAction("暂停旅程"));GuidePreview(root,"outing-paused");check(G.R.JourneyPaused&&GuideWords().Contains("旅程已暂停，等你点继续")&&!GuideWords().Contains("不用操作，也会继续"),"paused journey never claims to keep moving");
        string runBefore=FarmPracticeTests.Xml(G.R);var combo=UIChildren<Expander>((DependencyObject)Content).Single(e=>AutomationProperties.GetAutomationId(e)=="guide-combo-details");combo.IsExpanded=true;GuidePreview(root,"outing-combo-open");
        check(guideComboOpen&&FarmPracticeTests.Xml(G.R)==runBefore,"reading combination details does not advance or unpause");KeyPress(Key.Escape);GuidePreview(root,"outing-combo-closed");check(!guideComboOpen&&G.R.JourneyPaused,"Escape folds details before navigating or resuming");
        GuideClick(GuidePrimary());ReachGentleInspiration();GuidePreview(root,"outing-first-choice");
        check(Object.ReferenceEquals(GuidePrimary(),UIAction("就选这个，继续走"))&&InspirationButtons()==1,"the visible recommendation is the single primary guide action");
        ResetPanelInput();GuidePreview(root,"outing-choice-keyboard");KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"first Tab selects the recommended card rather than a window utility");
        long deadline=G.R.NextJourneyTicks;int recommended=G.RecommendedInspiration;KeyPress(Key.Enter);GuidePreview(root,"outing-choice-made");
        check(G.R.InspirationPicks.Length==1&&G.R.InspirationPicks[0].Id==recommended&&G.R.InspirationPicks[0].Manual&&G.JourneyRunning,"keyboard applies the real recommendation once and continues");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));G.SyncGuide();GuidePreview(root,"outing-return");
        check(G.R.DecorationReward>=0&&!GuideWords().Contains(FarmGame.DecorationNames[G.R.DecorationReward])&&GuideWords().Contains("肥料"),"return initially teaches fertilizer while the real souvenir remains in optional details");
        int fertilizer=G.S.Fertilizer;UIChildren<Expander>((DependencyObject)Content).Single(e=>Convert.ToString(e.Header)=="还有哪些收获").IsExpanded=true;
        UIPreview(root,"outing-return-details",764,571);check(GuideWords().Contains(FarmGame.DecorationNames[G.R.DecorationReward])&&G.S.Fertilizer==fertilizer,"expanding actual return rewards does not claim them twice");
        FirstOutingFixture(root,"outing-paid-friends");GuideClick(GuidePrimary());G.PlanPicnic(0);GuidePreview(root,"outing-paid-ready");berries=G.S.Berries;grain=G.S.Grain;
        var paidStart=GuidePrimary();GuideClick(paidStart);GuidePreview(root,"outing-paid-started");check(G.R.Picnic==0&&G.S.Berries==berries-2&&G.S.Grain==grain-2,"an existing picnic plan is disclosed and charged exactly once on explicit departure");
        before=FarmPracticeTests.Xml(G.S);paidStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"paid departure cannot double charge");
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"outing-reset");check(G.S.GuideDepartureLesson==0&&G.S.BlessingOffers.Length==0&&!IsVisible&&!owner.IsVisible&&G.SavePath==null,"fresh tutorial clears the lesson and stays detached from native windows and player saves");
        File.WriteAllText(Path.Combine(root,"first-outing-regression.txt"),"PASS: companion introduction then destination; acknowledgement has no costs, clock or random changes; explicit departure; normal resume and old saves; cancellation and stale controls after reopening; actual station progress; explicit pause; optional combination details and Escape; recommendation primary action and keyboard focus; actual manual choice; optional souvenir; real picnic cost charged once; isolated fresh reset. Detached WPF verification only.");
    }
}
