using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private Button TouchPortraitButton(){return UIChildren<Button>((DependencyObject)Content).Single(b=>AutomationProperties.GetAutomationId(b)=="companion-touch");}
    private void MomentPreview(string root,string name,double seconds=1.8,bool minimum=true)
    {
        Render();foreach(var view in companionViews)view.PreviewSeconds=seconds;foreach(var scene in scenes)scene.HarvestPreviewSeconds=seconds;
        UIPreview(root,"companion-"+name,minimum?484:544,minimum?501:541);
    }
    internal void RenderCompanionFeedbackChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();ResetGuideView();MomentPreview(root,"welcome");
        var firstFeed=GuidePrimary();var touch=TouchPortraitButton();string before=FarmPracticeTests.Xml(G.S);
        GuideClick(touch);MomentPreview(root,"welcome-touched");
        check(FarmPracticeTests.Xml(G.S)==before&&G.S.GuideStep==0&&CurrentCompanionMoment(0).Kind=="touch"&&GuideWords().Contains("蹭了蹭你的手"),"first portrait click responds without advancing guide or changing resources");
        GuideFits(TouchPortraitButton());GuideFits(GuidePrimary());
        for(int i=0;i<7;i++)touch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(FarmPracticeTests.Xml(G.S)==before,"repeated portrait interaction has no economy or progression effect");
        GuideClick(firstFeed);MomentPreview(root,"feeding-berry",.45);
        check(G.S.Berries==8&&G.S.Xp[0]==12&&G.S.Bond==1&&companionMoment.Kind=="feed"&&GuideWords().Contains("莓果真甜"),"feeding feedback follows one real successful transaction");
        string paid=FarmPracticeTests.Xml(G.S);var feedingMoment=companionMoment;
        MomentPreview(root,"feeding-contented",1.8);GuideFits(GuidePrimary());
        firstFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(FarmPracticeTests.Xml(G.S)==paid&&Object.ReferenceEquals(companionMoment,feedingMoment),"animation frames and stale feed control cannot spend again or replay a success");
        G.S.DesktopMotion=false;paid=FarmPracticeTests.Xml(G.S);MomentPreview(root,"feeding-motion-off",.2);
        check(FarmPracticeTests.Xml(G.S)==paid&&GuideWords().Contains("莓果真甜"),"motion-off feedback stays readable without touching progression");
        G.S.DesktopMotion=true;companionMoment.Started=DateTime.UtcNow.AddSeconds(-6);MomentPreview(root,"feeding-rested");
        check(CurrentCompanionMoment(0)==null&&GuideWords().Contains("看看莓果从哪里来"),"temporary response returns to the useful next-action hint");
        G.S.GuideStep=2;G.S.Xp[0]=0;MomentPreview(root,"before-food-shortage");var oldEnabledFeed=GuidePrimary();G.S.Berries=0;companionMoment=null;paid=FarmPracticeTests.Xml(G.S);oldEnabledFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));MomentPreview(root,"food-shortage");
        check(FarmPracticeTests.Xml(G.S)==paid&&companionMoment==null&&GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="去农田看看莓果","failed feeding does not play success or grant growth");
        GuideClick(TouchPortraitButton());MomentPreview(root,"shortage-touched");check(FarmPracticeTests.Xml(G.S)==paid&&companionMoment.Kind=="touch","free interaction stays available without food");

        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;G.S.Xp[0]=24;Go("农场");MomentPreview(root,"basket-waiting");
        check(scenes.Single().ShowBerryBasket&&!scenes.Single().HasRecentBerryHarvest,"basket has a visible destination before the first harvest without inventing a harvest");
        int berries=G.S.Berries;G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(12));MomentPreview(root,"harvest-travelling",.65);
        check(G.RecentBerryHarvest!=null&&G.RecentBerryHarvest.BerryPlots.SequenceEqual(new[]{0})&&G.RecentBerryHarvest.Berries==G.S.Berries-berries&&G.S.GuideHomeStage==HomeGuideStage.FreshBerries,"harvest effect identifies the actual berry plot and actual stored gain");
        var harvest=G.RecentBerryHarvest;paid=FarmPracticeTests.Xml(G.S);MomentPreview(root,"harvest-in-basket",1.9);GuideFits(GuidePrimary());
        check(FarmPracticeTests.Xml(G.S)==paid&&Object.ReferenceEquals(harvest,G.RecentBerryHarvest),"harvest frames do not repeat awards or create new harvest events");
        G.S.DesktopMotion=false;paid=FarmPracticeTests.Xml(G.S);MomentPreview(root,"harvest-motion-off",.3);check(FarmPracticeTests.Xml(G.S)==paid&&scenes.Single().HasRecentBerryHarvest,"motion-off harvest keeps its receipt without flying fruit");
        G.S.DesktopMotion=true;harvest.Started=DateTime.UtcNow.AddSeconds(-10);Go("伙伴");Go("农场");MomentPreview(root,"harvest-revisited");
        check(!scenes.Single().HasRecentBerryHarvest,"returning later does not replay an old harvest animation");
        G.ResetGuideTest();ResetGuideView();touch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));MomentPreview(root,"reset");
        check(G.RecentBerryHarvest==null&&companionMoment==null&&G.S.Berries==10&&G.S.GuideStep==0,"restart discards feedback and old portrait cannot affect the new session");
        for(int i=0;i<G.Plots;i++){G.S.Crops[i]=1;G.S.Growth[i]=G.CropSeconds(1)-1;}berries=G.S.Berries;
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(2));check(G.RecentBerryHarvest==null&&G.S.Berries==berries&&G.S.Grain>4,"grain harvest does not create berry feedback");
        G.ResetGuideTest();G.S.Berries=99998;G.S.Growth[0]=G.CropSeconds(0)-1;G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(2));
        check(G.S.Berries==99999&&G.RecentBerryHarvest.Berries==1,"basket reports actual storage gain at the resource cap");
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=8;G.S.SimpleCourtyard=true;G.S.Xp[0]=1250;G.S.Bond=0;Go("伙伴");MomentPreview(root,"full-level-bond-ready");
        int xp=G.S.Xp[0];GuideClick(GuidePrimary());MomentPreview(root,"full-level-bond-fed");check(G.S.Xp[0]==xp&&G.S.Bond==1&&companionMoment.Kind=="feed"&&!companionMoment.Words.Contains("长大"),"max-level bond feeding responds without claiming extra growth");
        G.EnsureJourneyOffers();check(G.BeginJourney(0,G.S.BlessingOffers[0]),"away fixture starts a real trip");Go("伙伴");MomentPreview(root,"away");paid=FarmPracticeTests.Xml(G.S);TouchPortraitButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(CurrentCompanionMoment(0)==null&&FarmPracticeTests.Xml(G.S)==paid,"absent travelling pet cannot be touched or fed at home");
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"all companion feedback checks remain detached and memory-only");
        File.WriteAllText(Path.Combine(root,"companion-feedback-regression.txt"),"PASS: first portrait response; no-cost repeated interaction; real feed transaction; berry and contented frames; stale and failed feed; expiry and motion-off; actual source plot and stored harvest; no replay or duplicate rewards; grain exclusion; capacity limit; reset/session isolation; max-level bond and absent pets. No visible/native windows or production-save access.");
    }
}
