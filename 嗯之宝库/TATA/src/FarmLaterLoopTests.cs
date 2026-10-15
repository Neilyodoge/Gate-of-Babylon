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
    private void LaterJourneyFixture(bool cave=false)
    {
        EverydayFixture();G.S.Evolved=cave;G.S.Xp=Enumerable.Repeat(55,FarmGame.PetCount).ToArray();
        G.EnsureJourneyOffers();FarmHeadlessTests.Check(G.BeginJourney(cave?1:0,G.S.BlessingOffers[0]),"later-loop fixture starts a genuine trip");G.AdvanceJourney(new DateTime(G.R.NextJourneyTicks,DateTimeKind.Utc));FarmHeadlessTests.Check(G.R.Phase=="battle","later-loop fixture reaches a real battle");Go("冒险");
    }
    private void LaterPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"later-"+name,size,true);GuideFits(GuidePrimary());
    }
    private Expander LaterExpander(string id){return UIChildren<Expander>((DependencyObject)Content).Single(e=>AutomationProperties.GetAutomationId(e)==id);}
    internal void RenderLaterLoopChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;LaterJourneyFixture();LaterPreview(root,"travel");
        check(SmallJourney&&WantsGentleNarrow&&scenes.Single().SmallTravel&&scenes.Single().Journey,"later trip stays small and depicts the actual encounter");
        check(LessonProgress("gentle-trip-progress").Value==G.R.Node&&!GuideWords().Contains("第 0 回合")&&!GuideWords().Contains("每场"),"default trip exposes real progress without battle-report density");
        var run=G.R;var oldPause=UIAction("暂停旅程");string before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());LaterPreview(root,"away-home");
        check(Tab=="农场"&&G.JourneyRunning&&scenes.Single().HideFarmPets&&FarmPracticeTests.Xml(G.S)==before,"returning to the courtyard neither pauses nor fast-forwards the trip or depicts travelling friends at home");
        GuideClick(GuidePrimary());LaterPreview(root,"travel-reopened");GuideClick(UIAction("暂停旅程"));LaterPreview(root,"paused");
        long deadline=run.NextJourneyTicks;int node=run.Node,round=run.Round;G.AdvanceJourney(DateTime.UtcNow.AddHours(1));
        check(run.JourneyPaused&&run.NextJourneyTicks==deadline&&run.Node==node&&run.Round==round&&GuideWords().Contains("已暂停")&&HasGuideButton("继续这趟旅程")&&!GuideWords().Contains("自动交战中"),"pause has honest wording, an explicit resume action, and survives elapsed time");
        GuideClick(UIAction("回小院等他们"));LaterPreview(root,"paused-home");GuideClick(GuidePrimary());LaterPreview(root,"paused-return");check(run.JourneyPaused,"home navigation preserves explicit pause");
        GuideClick(GuidePrimary());ReachGentleInspiration();LaterPreview(root,"inspiration");before=FarmPracticeTests.Xml(G.S);oldPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(InspirationButtons()==1&&FarmPracticeTests.Xml(G.S)==before&&WantsGentleNarrow,"old battle pause cannot affect a later choice and inspiration remains narrow");
        var staleChoice=GuidePrimary();int recommendation=G.RecommendedInspiration;deadline=run.NextJourneyTicks;
        GuideClick(UIAction("我想换一个"));LaterPreview(root,"inspiration-expanded");check(InspirationButtons()==3&&FarmPracticeTests.Xml(G.S)==before,"expanding alternatives preserves original offers, timers and resources");
        KeyPress(Key.Escape);LaterPreview(root,"inspiration-folded");check(!inspirationChoicesOpen&&run.NextJourneyTicks==deadline,"Escape folds choices without resetting their timer");
        GuideClick(UIAction("暂停旅程"));LaterPreview(root,"inspiration-paused");check(Convert.ToString(GuidePrimary().Content)=="选这个，继续暂停","paused choice does not promise to restart travel");
        ResetPanelInput();LaterPreview(root,"keyboard-choice");KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"first Tab selects the small inspiration recommendation");KeyPress(Key.Enter);LaterPreview(root,"chosen-paused");check(run.JourneyPaused&&run.InspirationPicks.Length==1&&run.InspirationPicks[0].Id==recommendation&&run.InspirationPicks[0].Manual,"keyboard choice applies once and preserves pause");
        before=FarmPracticeTests.Xml(G.S);staleChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old choice cannot duplicate an applied inspiration");
        GuideClick(GuidePrimary());ReachGentleInspiration();LaterPreview(root,"next-inspiration");check(!inspirationChoicesOpen&&InspirationButtons()==1,"each encounter begins with one recommendation");
        deadline=run.NextJourneyTicks;recommendation=G.RecommendedInspiration;G.AdvanceJourney(new DateTime(deadline,DateTimeKind.Utc));
        check(run.InspirationPicks.Length==2&&!run.InspirationPicks[1].Manual&&run.InspirationPicks[1].Id==recommendation,"unattended choice follows the original deadline and recommendation");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));LaterPreview(root,"return");check(run.Phase=="result"&&WantsGentleNarrow&&GuideWords().Contains("肥料")&&!GuideWords().Contains("晶石"),"return first shows the actual fertilizer in the same small window");
        int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;var returnButton=GuidePrimary();UIChildren<Expander>((DependencyObject)Content).Single().IsExpanded=true;UIPreview(root,"later-return-details",484,501);
        check(G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals&&GuideWords().Contains("晶石"),"optional actual rewards do not grant them again");
        GuideClick(returnButton);LaterPreview(root,"returned-home");before=FarmPracticeTests.Xml(G.S);returnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.AtHome&&Tab=="农场"&&G.S.SimpleCourtyard&&FarmPracticeTests.Xml(G.S)==before,"return ends the trip once and keeps the quiet courtyard");
        var things=LaterExpander("courtyard-things");check(!things.IsExpanded&&!HasGuideButton("给田施肥"),"returned home keeps optional uses folded");things.IsExpanded=true;UIPreview(root,"later-home-things",484,501);GuideClick(UIAction("给田施肥"));LaterPreview(root,"soil-ready");
        check(Tab=="施肥"&&QuietShell&&WantsGentleNarrow&&G.S.Fertilizer==fertilizer,"opening the simple soil page spends nothing");
        var growth=(double[])G.S.Growth.Clone();var crops=(int[])G.S.Crops.Clone();int cost=G.SoilCost,level=G.S.SoilLevel;var use=GuidePrimary();GuideClick(use);LaterPreview(root,"soil-applied");
        check(G.S.SoilLevel==level+1&&G.S.Fertilizer==fertilizer-cost&&G.ValidSoilReceipt!=null&&G.ValidSoilReceipt.Cost==cost&&G.S.Growth.SequenceEqual(growth)&&G.S.Crops.SequenceEqual(crops),"repeat soil upgrade uses real cost and preserves crops");
        check(LessonProgress("soil-time-after").Value==G.ValidSoilReceipt.AfterBerrySeconds,"repeat upgrade displays the actual saved duration comparison");
        before=FarmPracticeTests.Xml(G.S);use.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old soil action cannot spend twice");
        string path=Path.Combine(root,"later-loop-saved.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var loaded=new FarmGame(path);
        check(loaded.S.SimpleCourtyard&&loaded.ValidSoilReceipt!=null&&loaded.S.Fertilizer==G.S.Fertilizer&&loaded.S.SoilLevel==G.S.SoilLevel,"normal save retains simple mode, fertilizer and the actual upgrade receipt");
        GuideClick(GuidePrimary());Go("施肥");LaterPreview(root,"soil-reopened");check(courtyardSoilReceipt==null&&!GuideWords().Contains("本次施肥"),"reopening does not replay a previous purchase as new");
        G.S.Fertilizer=0;LaterPreview(root,"soil-shortage");check(Convert.ToString(GuidePrimary().Content)=="去小溪找点肥料"&&!G.UpgradeSoil(),"missing fertilizer offers recovery instead of a dead purchase action");GuideClick(UIAction("去小溪找点肥料"));LaterPreview(root,"soil-preparation");check(G.AtHome&&SmallRepeatDeparture,"shortage recovery only opens preparation");
        Go("施肥");G.S.SoilLevel=5;LaterPreview(root,"soil-max");before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());check(G.S.Fertilizer==0&&FarmPracticeTests.Xml(G.S)==before,"max-level soil only returns without purchase");

        EverydayFixture();G.S.Decorations[0]=G.S.Decorations[1]=true;G.S.DisplayDecoration=0;G.S.GardenDecorations=new[]{0,-1,1};G.S.DesktopHidden=true;G.S.DesktopLocked=true;
        Go("小纪念");LaterPreview(root,"keepsake-current");before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("下一件"));LaterPreview(root,"keepsake-preview");
        check(keepsakeSelection==1&&FarmPracticeTests.Xml(G.S)==before&&GuideWords().Contains("先看看样子"),"browsing owned keepsakes is a preview without applying it");
        var equip=GuidePrimary();GuideClick(equip);LaterPreview(root,"keepsake-equipped");check(G.S.DisplayDecoration==1&&G.S.GardenDecorations.SequenceEqual(new[]{0,-1,1})&&G.S.DesktopHidden&&G.S.DesktopLocked,"keepsake changes only the desktop decoration and preserves garden layout and hidden/locked preferences");
        before=FarmPracticeTests.Xml(G.S);equip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old keepsake action is inert after applying");
        XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);loaded=new FarmGame(path);check(loaded.S.DisplayDecoration==1&&loaded.S.GardenDecorations.SequenceEqual(new[]{0,-1,1}),"chosen desktop keepsake persists normally");
        GuideClick(UIAction("先收起桌宠摆件"));LaterPreview(root,"keepsake-stored");check(G.S.DisplayDecoration==-1&&G.S.Decorations[1],"storing the accessory preserves ownership");
        equip=GuidePrimary();Go("农场");Go("小纪念");LaterPreview(root,"keepsake-revisited");before=FarmPracticeTests.Xml(G.S);equip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"leaving and returning invalidates an old keepsake control");
        EverydayFixture();Go("小纪念");LaterPreview(root,"keepsake-empty");GuideClick(GuidePrimary());check(G.AtHome&&SmallRepeatDeparture&&G.S.Decorations.All(v=>!v),"empty collection routes to preparation without giving a free souvenir");

        LaterJourneyFixture();LaterPreview(root,"retreat-start");LaterExpander("small-trip-details").IsExpanded=true;UIPreview(root,"later-retreat-details",484,501);GuideClick(UIAction("提前回家…"));LaterPreview(root,"retreat-confirm");
        before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("再待一会儿"));LaterPreview(root,"retreat-cancelled");check(G.JourneyRunning&&FarmPracticeTests.Xml(G.S)==before,"cancelling early return changes no game state");
        GuideClick(UIAction("提前回家…"));LaterPreview(root,"retreat-confirm-again");var retreat=UIAction("确认返家");GuideClick(retreat);LaterPreview(root,"retreat-result");
        check(G.R.Withdrew&&G.R.Phase=="result"&&G.S.ExpeditionClears.All(n=>n==0)&&G.S.Decorations.All(v=>!v),"explicit retreat preserves normal non-boss reward rules");before=FarmPracticeTests.Xml(G.S);retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old retreat cannot alter a completed result");
        LaterJourneyFixture(true);LaterPreview(root,"cave-travel");check(G.R.Difficulty==1&&scenes.Single().SmallTravel,"cave travel renders from the actual region");
        G.R.Hp[G.S.Team[0]]=0;G.R.EnemyHp[0]=0;LaterPreview(root,"cave-injured");check(G.R.Hp[G.S.Team[0]]==0&&G.R.EnemyHp[0]==0,"encounter drawing never heals or damages units");

        for(int size=0;size<3;size++){
            LaterJourneyFixture(size==2);LaterPreview(root,"travel",size);GuideFits(UIAction("暂停旅程"));GuideClick(UIAction("暂停旅程"));LaterPreview(root,"paused",size);GuideFits(UIAction("回小院等他们"));
            GuideClick(GuidePrimary());ReachGentleInspiration();LaterPreview(root,"inspiration",size);GuideFits(UIAction("我想换一个"));GuideFits(UIAction("暂停旅程"));GuideFits(UIAction("回小院等他们"));
            GuideClick(UIAction("暂停旅程"));LaterPreview(root,"inspiration-paused",size);GuideFits(UIAction("继续这趟旅程"));GuideClick(UIAction("我想换一个"));LaterPreview(root,"inspiration-expanded",size);
            check(InspirationButtons()==3,"three actual alternatives remain available as a scrollable list");GuideClick(UIAction("继续这趟旅程"));G.AdvanceJourney(DateTime.UtcNow.AddHours(1));LaterPreview(root,"return",size);GuideClick(GuidePrimary());LaterPreview(root,"home",size);GuideFits(LaterExpander("courtyard-things"));
            G.S.Fertilizer=40;G.S.SoilLevel=1;Go("施肥");LaterPreview(root,"soil",size);GuideFits(UIAction("回小院"));GuideClick(GuidePrimary());LaterPreview(root,"soil-applied",size);
            G.S.Fertilizer=0;Go("农场");Go("施肥");LaterPreview(root,"soil-shortage",size);GuideFits(UIAction("去小溪找点肥料"));GuideFits(UIAction("回小院"));
            G.S.Decorations[0]=G.S.Decorations[1]=true;G.S.DisplayDecoration=-1;Go("小纪念");LaterPreview(root,"keepsake",size);GuideFits(UIAction("回小院"));GuideFits(UIAction("下一件"));GuideClick(GuidePrimary());LaterPreview(root,"keepsake-equipped",size);GuideFits(UIAction("先收起桌宠摆件"));
        }
        var stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();GuidePreview(root,"later-reset");before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.S.GuideStep==0&&FarmPracticeTests.Xml(G.S)==before&&!IsVisible&&!owner.IsVisible&&G.SavePath==null,"new loop actions cannot affect a fresh tutorial and all validation remains detached");
        File.WriteAllText(Path.Combine(root,"later-loop-regression.txt"),"PASS: narrow live journey/encounter and real station progress; home navigation; pause across time and reopening; stale stage controls; automatic and manual inspiration with persistent pause; original alternatives without reroll; return and optional rewards without duplicate credit; repeat soil transaction, exact receipt, no crop reset, normal reload, shortage and max level; owned keepsake preview/equip/store and persisted desktop-only effect; hidden/locked preferences; explicit/cancelled/stale early retreat; cave and injured scenes; three font sizes for full later loop. Detached WPF and isolated saves only.");
    }
}
