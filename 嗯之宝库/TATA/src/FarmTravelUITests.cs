using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public partial class FarmPanel
{
    private void TravelTestRender(string root){Render();UIPreview(root,"travel-layout",764,571);}
    internal void RenderTravelUIChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.SetPoisonPreset();G.S.BlessingOffers=new[]{0,1,3};check(G.BeginJourney(0,0),"travel UI fixture departs");G.RouteChoice(0);Tab="冒险";TravelTestRender(root);UIPreview(root,"travel-small",764,571);
        var pause=UIAction("暂停旅程");UIVisible(pause,"pause stays above the fold");UIVisible(UIAction("回农场看看"),"farm return stays above the fold");
        object content=Content;pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));int node=G.R.Node,round=G.R.Round;
        G.AdvanceJourney(DateTime.UtcNow.AddHours(2));check(G.R.JourneyPaused&&G.R.Node==node&&G.R.Round==round&&Object.ReferenceEquals(content,Content)&&TravelStatus()=="旅程已暂停","pause updates in place and really stops simulation");UIPreview(root,"travel-paused-small",764,571);
        UIAction("继续这趟旅程").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!G.R.JourneyPaused,"continue resumes the trip");G.AdvanceJourney(new DateTime(G.R.NextJourneyTicks,DateTimeKind.Utc));check(G.R.Node>node||G.R.Round>round,"resumed trip advances");TravelTestRender(root);UIPreview(root,"travel");
        var report=UIChildren<Expander>((DependencyObject)Content).First();report.IsExpanded=true;TravelTestRender(root);check(travelReportOpen,"battle report stays expanded across updates");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(2));check(G.R.Outcome=="victory","travel fixture completes with loot");TravelTestRender(root);UIPreview(root,"return");UIPreview(root,"return-small",764,571);
        UIVisible(UIAction("返家 · 升级农田"),"soil return visible in minimum window");UIVisible(UIAction("返家 · 培养 / 调整技能"),"care return visible in minimum window");UIVisible(UIAction("准备再去一趟"),"repeat preparation visible in minimum window");
        int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals,clears=G.S.ExpeditionClears.Sum();UIAction("准备再去一趟").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TravelTestRender(root);
        check(G.AtHome&&Tab=="冒险"&&journeyDifficulty==0&&journeyDepth==0&&G.S.BlessingOffers.Length==3&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals&&G.S.ExpeditionClears.Sum()==clears,"prepare again returns home without starting a run or duplicating loot");
        G.S.Harvests=1;G.ClaimChapter(0);OpenHandbook(0);TravelTestRender(root);UIPreview(root,"journal-ready");
        check(!UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="01  "+FarmGame.ChapterNames[0]),"claimed chapter cards are collapsed by default");
        var single=UIAction("领取奖励");int grain=G.S.Grain;single.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.ChapterClaimed[1]&&G.S.Grain==grain+6,"first ready card claims the correct reward");single.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.Grain==grain+6,"old reward button cannot grant twice");TravelTestRender(root);
        int ready=G.ClaimableChapters;UIAction("一键领取 "+ready+" 项奖励").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TravelTestRender(root);check(G.ClaimableChapters==0,"all ready rewards claimed through summary action");
        UIAction("查看已领取（"+G.S.ChapterClaimed.Count(x=>x)+"）").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TravelTestRender(root);UIPreview(root,"journal-claimed-small",764,571);check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="01  "+FarmGame.ChapterNames[0]),"claimed history can be reopened");
        scroller.ScrollToVerticalOffset(170);scroller.UpdateLayout();check(scroller.VerticalOffset>0,"handbook fixture is scrolled");UIAction("探索地图 · "+G.S.ExpeditionClears.Count(x=>x>0)+" / 6").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TravelTestRender(root);UIPreview(root,"map");check(scroller.VerticalOffset==0,"switching handbook sections starts at the top");
        var locked=UIChildren<Button>((DependencyObject)Content).Where(b=>Convert.ToString(b.Content)=="尚未开放").ToArray();check(locked.Length>0&&locked.All(b=>!b.IsEnabled),"unopened destinations are disabled");
        var deeper=UIChildren<Button>((DependencyObject)Content).First(b=>Convert.ToString(b.ToolTip)==FarmGame.ExpeditionName(0,1));deeper.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TravelTestRender(root);check(G.AtHome&&Tab=="冒险"&&journeyDifficulty==0&&journeyDepth==1,"map prepares the selected destination without departure");
        G.S.SoilLevel=1;G.S.SeedStage=2;G.S.RelicVictories=1;G.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();G.ClaimReadyChapters();showClaimedChapters=false;OpenHandbook(0);TravelTestRender(root);UIPreview(root,"journal-complete");check(G.S.ChapterClaimed.All(x=>x)&&!UIChildren<Button>((DependencyObject)Content).Any(b=>Convert.ToString(b.Content)=="领取奖励"),"finished handbook has no remaining reward buttons");
        G.S.Xp=Enumerable.Repeat(0,5).ToArray();G.S.BlessingOffers=new[]{0,1,3};check(G.BeginJourney(1,0,2),"failure fixture departs");G.RouteChoice(0);foreach(int pet in G.S.Team)G.R.Hp[pet]=1;G.R.EnemyCount=3;G.R.EnemyKind=Enumerable.Repeat(3,3).ToArray();G.R.EnemyHp=Enumerable.Repeat(999,3).ToArray();G.R.EnemyMax=Enumerable.Repeat(999,3).ToArray();G.R.Round=2;G.Step();check(G.R.Outcome=="defeat","failure fixture ends");Tab="冒险";TravelTestRender(root);UIPreview(root,"return-defeat-small",764,571);UIVisible(UIAction("返家 · 培养 / 调整技能"),"failed trip has visible care return");UIAction("返家 · 培养 / 调整技能").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&Tab=="伙伴","failure returns to actionable pet care");
        check(!IsVisible&&!owner.IsVisible,"travel UI checks never open a window");
        File.WriteAllText(Path.Combine(root,"travel-ui-regression.txt"),"PASS: minimum-window pause and return actions; in-place pause/resume; real simulation stop/resume; report state; victory/failure receipts; repeated preparation preserves rewards; single/all chapter claims without duplicates; claimed history; map scroll reset; locked and selected destinations; completed handbook. Isolated save and offscreen input only.");
    }
}
