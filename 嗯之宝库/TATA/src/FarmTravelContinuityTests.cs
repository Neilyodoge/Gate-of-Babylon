using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void TravelContinuityPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"travel-continuity-"+name,size,true);GuideFits(GuidePrimary());
        if(SmallRetreatVisible)GuideFits(UIAction("确认返家"));
    }
    private void OpenRetreatThroughDetails(string root)
    {
        Render();UIPreview(root,"travel-continuity-before-open",484,501);
        var details=LaterExpander(G.R.Phase=="inspiration"?"small-inspiration-return":FirstSmallJourney?"guide-combo-details":"small-trip-details");
        details.IsExpanded=true;UIPreview(root,"travel-continuity-open-details",484,501);GuideClick(UIAction("提前回家…"));
    }
    internal void RenderTravelContinuityChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        EverydayFixture();G.S.Evolved=true;G.S.ExpeditionClears[3]=1;G.S.BlessingOffers=new[]{0,1,2};G.PlanPicnic(0);G.Save();
        OpenSmallDestination(1,1);TravelContinuityPreview(root,"destination");string before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());TravelContinuityPreview(root,"depth");
        check(FarmPracticeTests.Xml(G.S)==before,"destination and depth browsing never changes saved plans, resources or random offers");GuideClick(GuidePrimary());TravelContinuityPreview(root,"saved-preparation");
        var expected=XDocument.Parse(before);expected.Root.Element("PlannedRegion").Value="1";expected.Root.Element("PlannedDepth").Value="1";
        check(G.AtHome&&G.S.PlannedRegion==1&&G.S.PlannedDepth==1&&XNode.DeepEquals(expected,XDocument.Parse(FarmPracticeTests.Xml(G.S))),"explicit destination confirmation saves only route and depth");
        before=FarmPracticeTests.Xml(G.S);OpenSmallDestination(0,0);TravelContinuityPreview(root,"cancel-preview");GuideClick(UIAction("返回出发准备"));TravelContinuityPreview(root,"cancelled-preview");check(FarmPracticeTests.Xml(G.S)==before&&journeyDifficulty==1&&journeyDepth==1,"cancelling a different destination preserves selected route and picnic");
        string path=Path.Combine(root,"travel-continuity-normal.xml");XDocument.Parse(before).Save(path);var loaded=new FarmGame(path);var window=new FarmWindow(loaded,true);window.Panel=new FarmPanel(window);var panel=window.Panel;
        panel.PrepareFromHome();panel.TravelContinuityPreview(root,"reload-preparation");
        check(panel.journeyDifficulty==1&&panel.journeyDepth==1&&loaded.S.PlannedPicnic==0&&loaded.AtHome&&loaded.S.BlessingOffers.SequenceEqual(new[]{0,1,2})&&loaded.S.Seed==G.S.Seed,"reopening restores selected cave depth and picnic, even before first victory, without reroll or departure");
        int berries=loaded.S.Berries,grain=loaded.S.Grain;panel.GuideClick(panel.GuidePrimary());check(loaded.R.Difficulty==1&&loaded.R.Depth==1&&loaded.R.Picnic==0&&loaded.S.Berries==berries-2&&loaded.S.Grain==grain-2,"restored preparation starts the chosen route and cooks once");
        loaded.ToggleJourneyPause();loaded.Save();var paused=new FarmGame(path);check(paused.R.JourneyPaused&&paused.R.Difficulty==1&&paused.R.Depth==1&&paused.S.Berries==loaded.S.Berries&&paused.S.Grain==loaded.S.Grain,"reload resumes exact paused journey without repacking food");
        before=FarmPracticeTests.Xml(paused.S);check(!paused.PlanDestination(0,0)&&FarmPracticeTests.Xml(paused.S)==before,"active journey cannot be replaced by destination planning");
        var old=XDocument.Parse(before);old.Root.Element("PlannedRegion").Remove();old.Root.Element("PlannedDepth").Remove();string oldPath=Path.Combine(root,"travel-continuity-old.xml");old.Save(oldPath);var migrated=new FarmGame(oldPath);
        check(migrated.SavePath==oldPath&&migrated.S.PlannedRegion==0&&migrated.S.PlannedDepth==0&&migrated.R.JourneyPaused&&migrated.R.Difficulty==1&&migrated.R.Depth==1,"old save gains default future route while preserving current paused cave trip");
        var broken=XDocument.Parse(before);broken.Root.Element("PlannedRegion").Value="99";broken.Root.Element("PlannedDepth").Value="99";string brokenPath=Path.Combine(root,"travel-continuity-invalid.xml");broken.Save(brokenPath);var repaired=new FarmGame(brokenPath);
        check(repaired.SavePath==brokenPath&&repaired.S.PlannedRegion==0&&repaired.S.PlannedDepth==0&&repaired.S.Berries==paused.S.Berries&&repaired.R.JourneyPaused,"invalid route preferences repair without recovering or replacing player progress");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"normal persistence fixtures remain detached without native windows");
        EverydayFixture();before=FarmPracticeTests.Xml(G.S);check(!G.PlanDestination(1,0)&&!G.PlanDestination(0,1)&&FarmPracticeTests.Xml(G.S)==before,"locked routes cannot be saved or unlocked by planning");
        G.S.PlannedRegion=1;G.S.PlannedDepth=2;G.Save();check(G.S.PlannedRegion==0&&G.S.PlannedDepth==0,"locked persisted destination normalizes to a real available route");

        LaterJourneyFixture();TravelContinuityPreview(root,"journey");var oldPause=UIAction("暂停旅程");before=FarmPracticeTests.Xml(G.S);OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"confirm");
        check(SmallRetreatVisible&&FarmPracticeTests.Xml(G.S)==before&&G.JourneyRunning,"return confirmation is a separate screen and keeps original running state and deadline");
        var retreat=UIAction("确认返家");oldPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old pause control cannot silently pause while return decision is open");
        KeyPress(Key.Escape);TravelContinuityPreview(root,"escape-cancelled");check(!SmallRetreatVisible&&FarmPracticeTests.Xml(G.S)==before,"Escape cancels return without time, economy or pause changes");
        OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"reopened");retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.JourneyRunning&&!G.R.Withdrew,"old confirm is inert after cancellation and opening a new decision");
        retreat=UIAction("确认返家");Go("农场");Go("冒险");TravelContinuityPreview(root,"navigation-return");before=FarmPracticeTests.Xml(G.S);retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));oldPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&!SmallRetreatVisible,"leaving and returning invalidates both confirm and pause controls");
        OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"before-inspiration");retreat=UIAction("确认返家");ReachGentleInspiration();before=FarmPracticeTests.Xml(G.S);retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"confirm drawn for earlier battle cannot act after automatic phase change");
        Pulse(true,false);UIPreview(root,"travel-continuity-pulse-inspiration",484,501);check(SmallRetreatVisible&&G.R.Phase=="inspiration"&&GuideWords().Contains("这次先回家吗？")&&!HasGuideButton("就选这个，继续走"),"real panel pulse keeps return decision visible across inspiration transition");
        TravelContinuityPreview(root,"confirm-inspiration");int picks=G.R.InspirationPicks.Length;long deadline=G.R.NextJourneyTicks;owner.TickClock(new DateTime(deadline,DateTimeKind.Utc),false,true,false);
        check(SmallRetreatVisible&&G.R.InspirationPicks.Length==picks+1&&!G.R.InspirationPicks.Last().Manual,"waiting on confirmation preserves automatic inspiration choice and original deadline");
        TravelContinuityPreview(root,"confirm-after-choice");GuideClick(GuidePrimary());TravelContinuityPreview(root,"continued");check(G.JourneyRunning&&!SmallRetreatVisible,"cancelling returns to the actual current phase");
        OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"before-background");retreat=UIAction("确认返家");before=FarmPracticeTests.Xml(G.S);PauseForBackground();TravelContinuityPreview(root,"after-background");retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&!SmallRetreatVisible&&G.JourneyRunning,"hiding clears a transient decision without pausing or acting on old confirmation");

        LaterJourneyFixture();OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"before-completion");retreat=UIAction("确认返家");G.AdvanceJourney(DateTime.UtcNow.AddHours(1));Pulse(true,false);TravelContinuityPreview(root,"completed-while-deciding");
        before=FarmPracticeTests.Xml(G.S);retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!SmallRetreatVisible&&G.R.Phase=="result"&&!G.R.Withdrew&&FarmPracticeTests.Xml(G.S)==before,"completed trip shows actual reward receipt and cannot become a retreat or reward twice");

        for(int size=0;size<3;size++){
            LaterJourneyFixture(true);TravelContinuityPreview(root,"font-travel",size);OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"font-confirm",size);
            ResetPanelInput();Render();UIPreview(root,"travel-continuity-keyboard",484,501);KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"confirmation keyboard defaults to keep travelling");KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);check(G.JourneyRunning&&!SmallRetreatVisible,"held Enter cancels safely without returning home");
            TravelContinuityPreview(root,"font-travel-again",size);GuideClick(UIAction("暂停旅程"));OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"font-paused-confirm",size);before=FarmPracticeTests.Xml(G.S);G.AdvanceJourney(DateTime.UtcNow.AddHours(1));GuideClick(GuidePrimary());TravelContinuityPreview(root,"font-pause-preserved",size);
            check(G.R.JourneyPaused&&FarmPracticeTests.Xml(G.S)==before,"cancel while paused preserves run and deadline, without resuming");
            GuideClick(GuidePrimary());ReachGentleInspiration();TravelContinuityPreview(root,"font-inspiration",size);GuideFits(LaterExpander("small-inspiration-return"));OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"font-inspiration-confirm",size);
            retreat=UIAction("确认返家");int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;GuideClick(retreat);TravelContinuityPreview(root,"font-retreat-result",size);
            check(G.R.Phase=="result"&&G.R.Withdrew&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals&&G.S.ExpeditionClears.All(n=>n==0),"explicit return preserves earned crops and does not grant unfinished boss rewards");before=FarmPracticeTests.Xml(G.S);retreat.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"confirmed return runs only once");
            EverydayFixture();G.S.Evolved=true;SelectJourneyDestination(1,0);Go("冒险");TravelContinuityPreview(root,"font-saved-preparation",size);
        }
        LaterJourneyFixture();G.S.SimpleCourtyard=false;G.S.GuideInWorld=true;G.S.GuideStep=4;Render();UIPreview(root,"travel-continuity-first-trip",484,501);
        check(FirstSmallJourney&&!SmallJourney,"first-trip fixture uses original gradual guide");OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"first-confirm");before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());Render();UIPreview(root,"travel-continuity-first-cancelled",484,501);check(FirstSmallJourney&&!SmallRetreatVisible&&FarmPracticeTests.Xml(G.S)==before,"first trip uses same non-spending confirmation and cancellation");
        LaterJourneyFixture();ReachGentleInspiration();TravelContinuityPreview(root,"stale-inspiration");var oldChoice=GuidePrimary();OpenRetreatThroughDetails(root);TravelContinuityPreview(root,"stale-inspiration-confirm");before=FarmPracticeTests.Xml(G.S);oldChoice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old inspiration choice cannot act behind return confirmation");
        EverydayFixture();check(G.S.PlannedRegion==0&&G.S.PlannedDepth==0&&!SmallRetreatVisible&&G.SavePath==null,"fresh tutorial clears saved preparation and transient decision in memory only");
        File.WriteAllText(Path.Combine(root,"travel-continuity-regression.txt"),"PASS: explicit destination/depth persistence; read-only browsing and cancel; isolated normal reload with picnic; paused journey reload; old and malformed preferences; locked destinations; stable narrow return decision through inspiration and automatic completion; pause preservation; stale controls across cancel/navigation/hiding/phase changes; Escape and held Enter; actual retreat rewards once; three reading sizes; fresh tutorial; detached windows.");
    }
}
