using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Xml.Linq;

public partial class FarmPanel
{
    private ProgressBar LessonProgress(string id)
    {
        return UIChildren<ProgressBar>((DependencyObject)Content).Single(b=>AutomationProperties.GetAutomationId(b)==id);
    }
    internal void RenderSmallStepsChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"small-start");
        var bar=LessonProgress("guide-growth-progress");check(bar.Value==0&&bar.Maximum==20,"first growth card starts at actual zero of twenty");
        GuideClick(GuidePrimary());GuidePreview(root,"small-fed");bar=LessonProgress("guide-growth-progress");
        check(bar.Value==12&&bar.Maximum==20&&CurrentGrowthReceipt(0).AfterXp==12,"one real feeding visibly fills twelve of twenty growth");
        // Level-up art still has its own optional ordinary-feeding regression.
        CareForFocusedPet(0,()=>G.Feed(0));GuidePreview(root,"small-levelled");bar=LessonProgress("guide-growth-progress");
        var detail=UIChildren<Expander>((DependencyObject)Content).Single();
        check(bar.Value==bar.Maximum&&!detail.IsExpanded&&!GuideWords().Contains("攻击"),"level milestone fills the bar and keeps combat stats collapsed");
        var details=String.Join(" ",LogicalNodes((DependencyObject)detail.Content).OfType<TextBlock>().Select(t=>t.Text));
        check(details.Contains("攻击  9 → 11")&&details.Contains("体力  48 → 54"),"optional original-icon details show real before and after stats");
        GuideClick(GuidePrimary());GuidePreview(root,"small-intro");
        int watched=scenes.Single().LearningPlot;
        check(watched==0&&G.S.Crops[watched]==0&&!scenes.Single().ShowBerryBasket&&!UIChildren<ProgressBar>((DependencyObject)Content).Any(),"first farm screen teaches only one real berry plot");
        string pendingPath=Path.Combine(root,"small-steps-pending.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(pendingPath);
        var reload=new FarmGame(pendingPath);check(reload.S.GuideFarmLesson==1&&reload.HomeGuideResumeTab=="农场","unacknowledged farm introduction resumes at the farm");
        var old=XDocument.Load(pendingPath);old.Root.Element("GuideFarmLesson").Remove();string oldPath=Path.Combine(root,"small-steps-old.xml");old.Save(oldPath);
        var oldGame=new FarmGame(oldPath);check(oldGame.S.GuideFarmLesson==-1&&oldGame.S.Xp[0]==24&&oldGame.S.GuideStep==3,"older saves keep progress and do not force the new introduction");
        var introButton=GuidePrimary();string before=FarmPracticeTests.Xml(G.S);GuideClick(introButton);
        check(G.S.GuideFarmLesson==2,"acknowledgement advances just the farm lesson");G.S.GuideFarmLesson=1;
        check(FarmPracticeTests.Xml(G.S)==before,"acknowledging a lesson spends nothing and never fast-forwards crops");G.S.GuideFarmLesson=2;
        GuidePreview(root,"small-watching");bar=LessonProgress("guide-harvest-progress");
        check(scenes.Single().LearningPlot==watched&&Math.Abs(bar.Value-100*G.S.Growth[watched]/G.CropSeconds(0))<.001,"watching shows the exact selected plot's growth");
        int berries=G.S.Berries;G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(12));MomentPreview(root,"small-harvest-source",.65);
        check(G.S.GuideHomeStage==HomeGuideStage.FreshBerries&&G.RecentBerryHarvest.BerryPlots.Contains(watched)&&scenes.Single().LearningPlot==watched,"harvest keeps the observed source plot after replanting");
        check(G.S.GuideHarvestBerries==G.S.Berries-berries&&LessonProgress("guide-harvest-progress").Value==100,"full harvest bar reports only actual stored berries");
        before=FarmPracticeTests.Xml(G.S);introButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old introduction control cannot repeat acknowledgement or rewards");
        int keptXp=G.S.Xp[0];berries=G.S.Berries;GuideClick(GuidePrimary());GuidePreview(root,"small-complete");bar=LessonProgress("guide-growth-progress");
        check(G.S.GuideHomeStage==HomeGuideStage.Complete&&G.S.Xp[0]==keptXp&&G.S.Berries==berries,"harvest receipt acknowledges existing stock without feeding or giving extra rewards");

        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.Xp[0]=24;Go("农场");GuidePreview(root,"small-slow-intro");
        var slowIntro=GuidePrimary();G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(12));GuidePreview(root,"small-intro-already-harvested");
        check(FarmIntroduction&&Convert.ToString(GuidePrimary().Content)=="看看它怎么长"&&G.S.GuideHomeStage==HomeGuideStage.FreshBerries,"real harvest during reading does not dismiss the first lesson");
        XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(pendingPath);reload=new FarmGame(pendingPath);
        check(reload.HomeGuideResumeTab=="农场"&&reload.S.GuideFarmLesson==1,"reload after early harvest still resumes the unacknowledged lesson");
        berries=G.S.Berries;GuideClick(slowIntro);GuidePreview(root,"small-caught-up");
        check(G.S.Berries==berries&&LessonProgress("guide-harvest-progress").Value==100&&Convert.ToString(GuidePrimary().Content)=="收好，回去陪泡泡","late acknowledgement shows the real completed harvest without a fake wait");
        XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(pendingPath);reload=new FarmGame(pendingPath);
        check(reload.S.GuideFarmLesson==2&&reload.HomeGuideResumeTab=="农场","acknowledged farm introduction resumes at the actual harvest receipt");
        G.S.Crops[lessonPlot]=1;GuidePreview(root,"small-replanted");check(scenes.Single().LearningPlot>=0&&G.S.Crops[scenes.Single().LearningPlot]==0,"if the watched plot is changed manually, select a remaining actual berry plot");
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"small-reset");
        check(G.S.GuideFarmLesson==0&&growthReceipt==null&&lessonPlot==-1&&G.RecentBerryHarvest==null,"restart clears transient art and the entire small-step lesson");
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"small-step rendering remains detached and memory-only");
        File.WriteAllText(Path.Combine(root,"small-steps-regression.txt"),"PASS: one-concept farm introduction; actual XP and level art; collapsed real stat details; stable observed harvest source and actual stored gain; no invented wait or rewards; current progress before fresh feeding; old controls inert; acknowledgement, early harvest and normal reload; old-save compatibility; crop changes; fresh reset. Detached WPF checks, no native windows or production saves.");
    }
}

public partial class FarmWindow
{
    internal void BackgroundPanelPresenceChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();G.S.PetLeft=170;G.S.PetTop=240;G.S.Left=300;G.S.Top=400;G.S.DesktopMode="pet";
        G.S.Run.JourneyPaused=true;string before=FarmPracticeTests.Xml(G.S);
        ApplyPanelPresence(true);check(desktopSuppressedForPanel&&!DesktopNeedsCompanion(false,true,false),"visible main panel suppresses the desktop companion");
        ApplyPanelPresence(true);ApplyPanelPresence(true,true);check(!desktopSuppressedForPanel&&DesktopNeedsCompanion(false,true,true),"minimizing restores the companion exactly once");
        ApplyPanelPresence(true);ApplyPanelPresence(false);check(!desktopSuppressedForPanel&&DesktopNeedsCompanion(false,false,false),"folding returns to the desktop presentation");
        check(FarmPracticeTests.Xml(G.S)==before,"temporary presentation changes preserve hidden preference, saved positions, display mode and journey pause");
        G.S.DesktopHidden=true;before=FarmPracticeTests.Xml(G.S);ApplyPanelPresence(true);ApplyPanelPresence(false);
        check(!DesktopNeedsCompanion(true,false,false)&&!DesktopNeedsCompanion(true,true,true)&&FarmPracticeTests.Xml(G.S)==before,"explicit hide survives panel disappearance and minimization");
        ApplyPanelPresence(true);foldingPanel=true;SyncPanelPresence();check(desktopSuppressedForPanel,"folding cannot trigger a competing restore during a hide operation");foldingPanel=false;ApplyPanelPresence(false);
        check(!IsVisible&&!Panel.IsVisible&&new WindowInteropHelper(this).Handle==IntPtr.Zero&&new WindowInteropHelper(Panel).Handle==IntPtr.Zero&&tray==null&&!animation.IsEnabled&&!clock.IsEnabled,"presence checks do not create windows, tray, timers or hotkeys");
        G.ResetGuideTest();Panel.ResetGuideView();
        File.WriteAllText(Path.Combine(root,"panel-presence-regression.txt"),"PASS: panel/companion exclusivity policy; minimized and folded return; repeated transition; explicit hide; folding guard; preserved save preferences, positions and journey pause. Native Show/Hide was suppressed in testing; actual desktop window-manager behavior was not exercised.");
    }
}
