using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private void ReachGentleInspiration()
    {
        int turns=0;while(G.R.Phase!="inspiration"&&G.JourneyRunning&&turns++<80)G.AdvanceJourney(new DateTime(G.R.NextJourneyTicks,DateTimeKind.Utc));
        FarmHeadlessTests.Check(G.R.Phase=="inspiration","normal trip reaches inspiration");G.R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(18).Ticks;G.SyncGuide();
    }
    private int InspirationButtons(){return UIChildren<Button>((DependencyObject)Content).Count(b=>AutomationProperties.GetAutomationId(b).StartsWith("inspiration-choice-"));}
    internal void RenderQuietUXChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;G.S.GuideHomeStage=HomeGuideStage.StayingHome;G.S.Xp[0]=36;
        Go("冒险");GuidePreview(root,"quiet-inspiration-party");GuideClick(GuidePrimary());GuidePreview(root,"quiet-inspiration-departure");GuideClick(GuidePrimary());ReachGentleInspiration();GuidePreview(root,"inspiration-gentle");
        check(InspirationButtons()==1&&HasGuideButton("我想换一个")&&GuideWords().Contains("伙伴会自己挑")&&!GuideWords().Contains("每种最多"),"guided choice starts with one recommendation and prominent automatic continuation");
        GuideFits(UIAction("就选这个，继续走"));GuideFits(UIAction("我想换一个"));GuidePreview(root,"inspiration-gentle-minimum",764,571);GuideFits(UIAction("就选这个，继续走"));GuideFits(UIAction("暂停旅程"));
        var oldRun=G.R;var stalePick=UIAction("就选这个，继续走");int recommendation=G.RecommendedInspiration;long deadline=G.R.NextJourneyTicks;
        string before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("我想换一个"));GuidePreview(root,"inspiration-expanded-minimum",764,571);
        check(InspirationButtons()==3&&FarmPracticeTests.Xml(G.S)==before,"expansion preserves all three original choices, random state and deadline");
        foreach(int id in G.R.InspirationOffers){var choice=UIAction("选「"+FarmGame.InspirationNames[id]+"」");choice.BringIntoView();scroller.UpdateLayout();GuideFits(choice);}
        GuideClick(UIAction("收起，交给伙伴选"));GuidePreview(root,"inspiration-folded");check(InspirationButtons()==1&&G.R.NextJourneyTicks==deadline,"collapsing does not restart automatic timer");
        G.AdvanceJourney(new DateTime(deadline-1,DateTimeKind.Utc));check(G.R.InspirationPicks.Length==0,"recommendation is not applied early");
        G.AdvanceJourney(new DateTime(deadline,DateTimeKind.Utc));GuidePreview(root,"inspiration-auto-continued");check(G.R.InspirationPicks.Length==1&&G.R.InspirationPicks[0].Id==recommendation&&!G.R.InspirationPicks[0].Manual,"folded screen automatically applies the genuine recommendation once");
        stalePick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.R.InspirationPicks.Length==1,"stale recommended button cannot duplicate selection");
        ReachGentleInspiration();GuidePreview(root,"inspiration-next-folded");check(InspirationButtons()==1,"next encounter starts folded independently");
        GuideClick(UIAction("暂停旅程"));GuidePreview(root,"inspiration-gentle-paused");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));check(G.R.Phase=="inspiration"&&G.R.InspirationPicks.Length==1,"paused recommendation never auto-selects");
        GuideClick(UIAction("我想换一个"));GuidePreview(root,"inspiration-paused-expanded");int chosen=G.R.InspirationOffers.Last();GuideClick(UIAction("选「"+FarmGame.InspirationNames[chosen]+"」"));GuidePreview(root,"inspiration-paused-chosen");
        check(G.R.JourneyPaused&&G.R.InspirationPicks.Length==2&&G.R.InspirationPicks[1].Id==chosen&&G.R.InspirationPicks[1].Manual,"manual alternative preserves explicit pause");
        GuideClick(UIAction("继续这趟旅程"));G.AdvanceJourney(DateTime.UtcNow.AddHours(1));check(G.R.Phase=="result"&&G.R.InspirationPicks.Length==3,"remaining trip finishes automatically with normal inspiration rules");
        G.Home();G.S.GuideStep=8;G.S.SimpleCourtyard=true;Go("冒险");GuidePreview(root,"quiet-repeat-departure");GuideClick(GuidePrimary());ReachGentleInspiration();GuidePreview(root,"quiet-repeat-inspiration");
        check(InspirationButtons()==1,"simple home keeps gentle choices for later journeys");before=FarmPracticeTests.Xml(G.S);stalePick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&!Object.ReferenceEquals(oldRun,G.R),"old trip control cannot pick a matching station in a new trip");
        G.Retreat();G.Home();G.S.GuideStep=-1;G.S.SimpleCourtyard=true;Go("农场");GuidePreview(root,"legacy-quiet-home");check(HasGuideButton("出门")&&HasGuideButton("伙伴"),"legacy opt-in simple home retains navigation");
        GuideClick(UIAction("出门"));GuidePreview(root,"legacy-quiet-departure");GuideClick(GuidePrimary());check(G.JourneyRunning,"legacy opt-in simple mode can depart");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));GuidePreview(root,"quiet-repeat-result");GuideClick(UIAction("返家 · 回小院"));GuidePreview(root,"quiet-repeat-home");check(G.AtHome&&G.S.SimpleCourtyard&&!HasGuideButton("更多"),"later journey returns to simple home");
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"quiet UX checks remain detached and memory-only");
        File.WriteAllText(Path.Combine(root,"quiet-ux-regression.txt"),"PASS: genuine first-trip recommended choice; original three options on expansion; no reroll, clock reset or early choice; automatic selection; paused manual alternative; later encounters reset folding; stale controls across encounters and runs; simple repeated journey and return; legacy simple-mode opt-in. No native windows or production-save access.");
    }
}
