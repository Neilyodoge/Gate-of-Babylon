using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

public partial class FarmPanel
{
    private void BerryRecoveryPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"berry-recovery-"+name,size,true);
        FarmHeadlessTests.Check(WantsGentleNarrow,"berry recovery stays in the small guide window");
        var viewport=UIChildren<ScrollContentPresenter>(scroller).First();
        foreach(var control in KeyboardTargets().Where(c=>Under(c,scroller))){var at=control.TranslatePoint(new Point(0,0),viewport);FarmHeadlessTests.Check(at.Y>=0&&at.Y+control.ActualHeight<=viewport.ActualHeight,"berry recovery target stays above footer: "+name+" / "+ControlKey(control));}
        FarmHeadlessTests.Check(!GuideWords().Contains("施肥")&&!GuideWords().Contains("分工")&&!GuideWords().Contains("晶石"),"recovery does not reveal the full management dashboard");
        GuidePrimary();
    }
    private void BerryRecoveryFixture(int step=0)
    {
        G.ResetGuideTest();ResetGuideView();G.S.Berries=0;G.S.GuideStep=step;G.S.GuideInWorld=step>0;G.S.Xp[0]=step==3?24:0;
        for(int i=0;i<G.Plots;i++){G.S.Crops[i]=1;G.S.Growth[i]=i+2;}
        G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(1).Ticks;
        if(step>0)Go("伙伴");
    }
    internal void RenderGuideBerriesChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        Button stalePlant=null;
        for(int size=0;size<3;size++){
            BerryRecoveryFixture();BerryRecoveryPreview(root,"entry",size);string before=FarmPracticeTests.Xml(G.S);
            GuideClick(GuidePrimary());BerryRecoveryPreview(root,"missing",size);check(Tab=="找莓果"&&FarmPracticeTests.Xml(G.S)==before,"opening the berry source is read-only and retains step zero");
            GuideClick(GuidePrimary());BerryRecoveryPreview(root,"preview",size);stalePlant=GuidePrimary();int first=guideBerryChoice;
            GuideClick(UIAction("换一块看看"));BerryRecoveryPreview(root,"next-plot",size);check(guideBerryChoice!=first&&FarmPracticeTests.Xml(G.S)==before,"browsing another real plot does not replant or save");
            stalePlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"previous plot button cannot change the current selection");
            KeyPress(Key.Escape);BerryRecoveryPreview(root,"cancelled",size);check(!guideBerryPlanting&&FarmPracticeTests.Xml(G.S)==before,"Escape cancels the crop preview before leaving the source");
            GuideClick(GuidePrimary());BerryRecoveryPreview(root,"chosen",size);int chosen=guideBerryChoice;int[] crops=(int[])G.S.Crops.Clone();double[] growth=(double[])G.S.Growth.Clone();int grain=G.S.Grain;
            stalePlant=GuidePrimary();GuideClick(stalePlant);BerryRecoveryPreview(root,"planted",size);
            check(G.S.Crops[chosen]==0&&G.S.Growth[chosen]==0&&Enumerable.Range(0,G.Plots).Where(i=>i!=chosen).All(i=>G.S.Crops[i]==crops[i]&&G.S.Growth[i]==growth[i])&&G.S.Grain==grain&&G.S.Berries==0&&G.S.Xp[0]==0&&G.S.GuideStep==0,"explicit planting changes only the selected crop and its clock, without charging or completing a lesson");
            before=FarmPracticeTests.Xml(G.S);stalePlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old confirmation cannot reset a growing plot");
            object surface=Content;var back=GuidePrimary();owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(5),false,true,false);
            check(Object.ReferenceEquals(surface,Content)&&LessonProgress("guide-berry-source-progress").Value>0&&scenes.Single().LearningPlot==chosen&&G.S.Xp[0]==0,"real clock updates the same observed crop without rebuilding or feeding");
            UIPreview(root,"berry-recovery-live-"+size,484,501);
            owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(50),false,true,false);
            check(Object.ReferenceEquals(surface,Content)&&Object.ReferenceEquals(back,GuidePrimary())&&Convert.ToString(back.Content)=="回去喂泡泡"&&G.S.Berries>=2&&G.S.Xp[0]==0&&G.S.GuideStep==0,"actual harvest changes the return action in place and never auto-feeds");
            UIPreview(root,"berry-recovery-ready-"+size,484,501);int berries=G.S.Berries;GuideClick(back);BerryRecoveryPreview(root,"returned",size);
            check(Tab=="新手引导"&&G.S.Berries==berries&&G.S.Xp[0]==0,"return to the original first lesson does not consume berries");
            GuideClick(GuidePrimary());BerryRecoveryPreview(root,"fed",size);check(G.S.Berries==berries-2&&G.S.Xp[0]==12&&G.S.GuideStep==3,"the next explicit feeding completes exactly one normal transaction");
        }

        BerryRecoveryFixture(2);BerryRecoveryPreview(root,"second-feed-shortage");check(GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="去农田看看莓果","later shortage offers one reachable non-spending action");
        string unchanged=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());BerryRecoveryPreview(root,"second-source");KeyPress(Key.Escape);BerryRecoveryPreview(root,"second-cancelled");check(Tab=="伙伴"&&G.S.GuideStep==2&&FarmPracticeTests.Xml(G.S)==unchanged,"source Escape returns to an unfinished legacy first feeding without a skipped step");
        GuideClick(GuidePrimary());BerryRecoveryPreview(root,"second-revisited");GuideClick(GuidePrimary());BerryRecoveryPreview(root,"second-preview");stalePlant=GuidePrimary();
        KeyPress(Key.Escape);KeyPress(Key.Escape);BerryRecoveryPreview(root,"second-left");GuideClick(GuidePrimary());BerryRecoveryPreview(root,"second-new-visit");unchanged=FarmPracticeTests.Xml(G.S);stalePlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==unchanged&&!guideBerryPlanting,"returning later does not reactivate an old planting control");

        BerryRecoveryFixture(3);G.S.GuideFarmSeen=true;G.S.GuideFarmLesson=1;Go("农场");BerryRecoveryPreview(root,"empty-introduction");
        check(Convert.ToString(GuidePrimary().Content)=="去种莓果"&&!UIChildren<ProgressBar>((DependencyObject)Content).Any(),"empty farm introduction does not point at an invented berry plot");
        GuideClick(GuidePrimary());BerryRecoveryPreview(root,"intro-source");GuideClick(GuidePrimary());BerryRecoveryPreview(root,"intro-preview");GuideClick(GuidePrimary());BerryRecoveryPreview(root,"intro-growing");
        GuideClick(GuidePrimary());BerryRecoveryPreview(root,"intro-return");check(Tab=="农场"&&FarmIntroduction&&G.S.GuideFarmLesson==1&&scenes.Single().LearningPlot>=0,"planting returns to the still-unacknowledged real farm introduction");
        GuideClick(GuidePrimary());BerryRecoveryPreview(root,"intro-watch");check(G.S.GuideFarmLesson==2,"watching growth remains a separate deliberate step");

        BerryRecoveryFixture(3);G.S.GuideFarmSeen=true;G.S.GuideFarmLesson=2;G.S.GuideHomeStage=HomeGuideStage.FreshBerries;G.S.GuideHarvestBerries=2;G.S.Crops[0]=0;G.S.Growth[0]=G.CropSeconds(0)-2;
        BerryRecoveryPreview(root,"fresh-shortage",2);int keptXp=G.S.Xp[0],keptStock=G.S.Berries;
        GuideClick(GuidePrimary());BerryRecoveryPreview(root,"fresh-acknowledged",2);
        check(G.S.GuideHomeStage==HomeGuideStage.Complete&&G.S.Xp[0]==keptXp&&G.S.Berries==keptStock,"previous harvest can be acknowledged even when stock was spent elsewhere, without another feeding");


        BerryRecoveryFixture();OpenGuideBerries(0);PreviewGuideBerryPlanting();BerryRecoveryPreview(root,"already-berry-preview");int plot=guideBerryChoice;G.S.Crops[plot]=0;G.S.Growth[plot]=15;RefreshCounters();unchanged=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());BerryRecoveryPreview(root,"already-berry-growing");check(FarmPracticeTests.Xml(G.S)==unchanged&&G.S.Growth[plot]==15,"an already replanted berry crop is viewed without resetting its growth");
        OpenGuideBerries(0);PreviewGuideBerryPlanting();BerryRecoveryPreview(root,"before-reset");stalePlant=GuidePrimary();G.ResetGuideTest();ResetGuideView();unchanged=FarmPracticeTests.Xml(G.S);stalePlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==unchanged&&guideBerryState==null,"reset invalidates the entire recovery context and pending planting");

        string path=Path.Combine(root,"guide-berry-normal.xml");var normal=new FarmGame(path);normal.S.GuideStep=2;normal.S.GuideInWorld=true;normal.S.Xp[0]=0;normal.S.Berries=0;normal.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(1).Ticks;for(int i=0;i<normal.Plots;i++)normal.S.Crops[i]=1;normal.Save();
        var window=new FarmWindow(normal,true);var panel=new FarmPanel(window);window.Panel=panel;panel.Go("伙伴");panel.BerryRecoveryPreview(root,"normal-shortage");string saved=File.ReadAllText(path);panel.GuideClick(panel.GuidePrimary());panel.BerryRecoveryPreview(root,"normal-source");panel.GuideClick(panel.GuidePrimary());panel.BerryRecoveryPreview(root,"normal-preview");
        check(File.ReadAllText(path)==saved,"normal-mode source and planting preview do not write the save");int selected=panel.guideBerryChoice;panel.GuideClick(panel.GuidePrimary());panel.BerryRecoveryPreview(root,"normal-planted");var reload=new FarmGame(path);
        check(reload.S.Crops[selected]==0&&reload.S.GuideStep==2&&reload.S.Xp[0]==0&&reload.HomeGuideResumeTab=="伙伴","normal reload keeps confirmed crop and the unfinished feeding, without restoring an unconfirmed action");
        check(G.SavePath==null&&!IsVisible&&!owner.IsVisible&&new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"berry recovery tests use only detached controls and an isolated save");
        G.ResetGuideTest();ResetGuideView();
        File.WriteAllText(Path.Combine(root,"guide-berry-recovery-regression.txt"),"PASS: small one-plot berry recovery; default/115/130% controls fit the actual viewport; read-only source and previews; explicit single-plot planting; real clock/progress/harvest and in-place return; first feeding, unfinished legacy feeding, non-spending harvest acknowledgement and empty farm-introduction continuity; cancel/Escape/revisit/reset guards; already-berry preservation; isolated normal-save read-only browsing and committed-crop reload. No native windows or player-save access.");
    }
}
