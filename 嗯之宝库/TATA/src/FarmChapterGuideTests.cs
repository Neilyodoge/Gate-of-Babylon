using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void ChapterGuideFixture(int chapter)
    {
        EverydayFixture();G.S.Xp=Enumerable.Repeat(0,FarmGame.PetCount).ToArray();G.S.Xp[2]=12;
        G.S.Berries=G.S.Grain=G.S.Mint=30;G.S.Fertilizer=30;G.S.Crystals=10;
        G.S.CourtyardPet=4;G.S.Harvests=0;G.S.SoilLevel=0;G.S.Evolved=false;G.S.SeedStage=0;G.S.RelicVictories=0;
        G.S.ExpeditionClears=new int[6];G.S.ChapterClaimed=new bool[8];G.S.EquippedRelics=new[]{-1,-1};G.S.BlessingOffers=new[]{0,1,2};G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;
        if(chapter==6)G.S.SeedStage=1;
        if(chapter==7){G.S.ExpeditionClears[0]=5;G.S.ExpeditionClears[1]=2;}
        OpenLife("成长手册");lifeChapter=chapter;
    }
    private void CompleteChapterFixture(int chapter)
    {
        switch(chapter){case 0:G.S.Harvests=1;break;case 1:G.S.Xp[2]=20;break;case 2:G.S.ExpeditionClears[0]=1;break;case 3:G.S.SoilLevel=1;break;case 4:G.S.Evolved=true;break;case 5:G.S.RelicVictories=1;break;case 6:G.S.SeedStage=2;break;case 7:G.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();break;}
    }
    private void ChapterGuidePreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"chapter-guide-"+name,size,true);ReviewTarget(GuidePrimary(),"chapter primary "+name+" "+size);
    }
    internal void RenderChapterGuideChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;int[] values={0,12,0,0,0,0,1,2},totals={1,20,1,1,1,1,2,6};
        string[] pages={"农田","伙伴册","目的地","施肥","进化","目的地","进化","目的地"};
        for(int size=0;size<3;size++)for(int chapter=0;chapter<8;chapter++){
            ChapterGuideFixture(chapter);G.S.ReadingSize=size;string before=FarmPracticeTests.Xml(G.S);ChapterGuidePreview(root,"waiting-"+chapter,size);
            var bar=LessonProgress("chapter-guide-progress");
            check(bar.Value==values[chapter]&&bar.Maximum==totals[chapter]&&!G.ChapterDone(chapter)&&FarmPracticeTests.Xml(G.S)==before,"eight incomplete chapters display real capped progress without changing state");
            check(AutomationProperties.GetName(bar).Contains(FarmGame.ChapterNames[chapter])&&AutomationProperties.GetName(bar).Contains(G.ChapterGuideProgress(chapter)),"progress has a current readable chapter name");
            ReviewTarget(UIAction("先回小院"),"chapter home "+chapter+" "+size);
            if(chapter>0)ReviewTarget(UIAction("前一页"),"previous chapter "+chapter+" "+size);else check(!UIAction("前一页").IsEnabled,"first page has no previous action");
            if(chapter<7)ReviewTarget(UIAction("后一页"),"next chapter "+chapter+" "+size);else check(!UIAction("后一页").IsEnabled,"last page has no next action");
            var source=GuidePrimary();GuideClick(source);ChapterGuidePreview(root,"source-"+chapter,size);
            check(Tab==pages[chapter]&&FarmPracticeTests.Xml(G.S)==before&&G.AtHome,"next step opens the corresponding real page without spending, changing preparation, or starting a trip");
            if(chapter==2||chapter==5||chapter==7)check(smallDestinationStep==1&&smallDestinationRegion==0&&smallDestinationDepth==(chapter==7?2:0),"route suggestion opens the actual required accessible segment as preview only");
            if(chapter==1)check(lifePet==2&&G.CourtyardCompanion==4&&G.S.Xp[2]==12&&G.S.Xp[4]==0,"growth shortcut previews the named progress leader without changing the cared-for companion or feeding");
            if(chapter==4||chapter==6)check(smallGrowthPet==4&&smallGrowthStep==0,"evolution shortcut previews the correct companion without evolving");
            source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old chapter action is inert away from the chapter");
            KeyPress(Key.Escape);ChapterGuidePreview(root,"returned-"+chapter,size);check(Tab=="成长手册"&&lifeChapter==chapter&&FarmPracticeTests.Xml(G.S)==before,"Escape returns to the same chapter with no transaction");
            source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="成长手册"&&FarmPracticeTests.Xml(G.S)==before,"returning does not reactivate an old source control");
            var sameRoot=Content;CompleteChapterFixture(chapter);before=FarmPracticeTests.Xml(G.S);RefreshCounters();
            check(Object.ReferenceEquals(Content,sameRoot)&&Convert.ToString(GuidePrimary().Content)=="收好这份成长礼物"&&LessonProgress("chapter-guide-progress").Value==totals[chapter]&&FarmPracticeTests.Xml(G.S)==before,"newly complete chapters update the same page to full progress and explicit claim without auto-claiming");
            ChapterGuidePreview(root,"ready-"+chapter,size);int[] stocks=ChapterStocks();var claim=GuidePrimary();GuideClick(claim);ChapterGuidePreview(root,"claimed-"+chapter,size);
            check(G.S.ChapterClaimed[chapter]&&ChapterStocks().SequenceEqual(stocks.Select((n,i)=>n+FarmGame.ChapterRewards[chapter][i]))&&GuideWords().Contains("这次收好："),"claim adds exactly the existing reward and shows its actual receipt");
            before=FarmPracticeTests.Xml(G.S);claim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old reward button cannot pay twice");
            if(chapter<7){GuideClick(UIAction("后一页"));ChapterGuidePreview(root,"next-"+chapter,size);check(lifeChapter==chapter+1&&FarmPracticeTests.Xml(G.S)==before,"browsing next chapter has no mutation");GuideClick(UIAction("前一页"));ChapterGuidePreview(root,"back-"+chapter,size);check(lifeChapter==chapter&&!GuideWords().Contains("这次收好："),"returning to an old claimed page never replays a fresh receipt");}
        }

        ChapterGuideFixture(0);ChapterGuidePreview(root,"live-harvest-before",2);var surface=Content;var liveClaim=GuidePrimary();
        owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(90),false,true,false);
        check(Object.ReferenceEquals(Content,surface)&&G.S.Harvests>0&&!G.S.ChapterClaimed[0]&&LessonProgress("chapter-guide-progress").Value==1&&Convert.ToString(liveClaim.Content)=="收好这份成长礼物","production farm clock changes the existing waiting action into an explicit reward claim");
        UIPreview(root,"chapter-guide-live-harvest-ready",484,501);int[] harvested=ChapterStocks();GuideClick(liveClaim);ChapterGuidePreview(root,"live-harvest-claimed",2);check(G.S.Berries==harvested[0]+4&&G.S.Grain==harvested[1]+3,"actual farm harvest and subsequent gift are separate amounts");

        ChapterGuideFixture(5);G.S.ExpeditionClears[0]=1;ChapterGuidePreview(root,"relic-owned");string state=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ChapterGuidePreview(root,"relic-preview");check(Tab=="行囊"&&FarmPracticeTests.Xml(G.S)==state&&G.S.EquippedRelics.All(i=>i<0),"owned but unequipped relic opens a read-only bag choice");
        GuideClick(GuidePrimary());ChapterGuidePreview(root,"relic-equipped");check(G.S.EquippedRelics[0]==0&&G.AtHome&&!G.ChapterDone(5),"explicitly equipping a relic does not complete a victory chapter");KeyPress(Key.Escape);ChapterGuidePreview(root,"relic-prepare");check(Tab=="成长手册"&&Convert.ToString(GuidePrimary().Content)=="准备带信物去小溪","chapter advances its next-step hint after real equipment");
        state=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ChapterGuidePreview(root,"relic-route");check(Tab=="目的地"&&FarmPracticeTests.Xml(G.S)==state,"equipped relic route remains preparation only");

        ChapterGuideFixture(7);G.S.ExpeditionClears=new[]{1,1,1,0,0,0};ChapterGuidePreview(root,"cave-locked",2);state=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ChapterGuidePreview(root,"cave-evolution",2);check(Tab=="进化"&&smallGrowthPet==4&&FarmPracticeTests.Xml(G.S)==state,"when only locked cave routes remain, first show evolution instead of a locked or completed route");
        KeyPress(Key.Escape);G.S.Evolved=true;ChapterGuidePreview(root,"cave-open",2);GuideClick(GuidePrimary());ChapterGuidePreview(root,"cave-route",2);check(Tab=="目的地"&&smallDestinationRegion==1&&smallDestinationDepth==0&&G.S.PlannedRegion==0,"opening cave preview does not silently replace saved destination");

        ChapterGuideFixture(0);CompleteChapterFixture(0);G.S.Berries=99998;G.S.Grain=99999;ChapterGuidePreview(root,"storage-limited",2);check(GuideWords().Contains("这次能收下：莓果 +1")&&!GuideWords().Contains("麦穗 +3"),"limited warehouse shows actual available reward before claiming");
        GuideClick(GuidePrimary());ChapterGuidePreview(root,"storage-receipt",2);check(G.S.Berries==99999&&G.S.Grain==99999&&GuideWords().Contains("这次收好：莓果 +1")&&!GuideWords().Contains("麦穗 +3"),"receipt reports actual stock delta rather than fabricating overflow rewards");
        ChapterGuideFixture(0);CompleteChapterFixture(0);G.S.Berries=G.S.Grain=99999;ChapterGuidePreview(root,"storage-full");check(GuideWords().Contains("领取不会增加材料"),"full storage is explained before the existing claim action");
        state=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("先回小院"));check(FarmPracticeTests.Xml(G.S)==state&&!G.S.ChapterClaimed[0],"full-storage gift can be left unclaimed without cost");

        ChapterGuideFixture(1);ChapterGuidePreview(root,"growth-leader");check(GuideWords().Contains("电团成长 12 / 20")&&Convert.ToString(GuidePrimary().Content)=="先看看电团"&&portraits.Single().Item1==2,"growth name, portrait and next action refer to the same actual companion");
        GuideClick(GuidePrimary());ChapterGuidePreview(root,"growth-preview");int berries=G.S.Berries;GuideClick(GuidePrimary());ChapterGuidePreview(root,"growth-chosen");check(G.CourtyardCompanion==2&&G.S.Berries==berries&&G.S.Xp[2]==12,"only explicit companion choice changes whom to care for, without feeding");GuideClick(GuidePrimary());check(G.S.Berries==berries-2&&G.S.Xp[2]==24&&G.ChapterDone(1),"separate explicit feeding completes actual growth at its original price");
        ChapterGuideFixture(1);G.S.Xp[4]=12;check(G.ChapterGrowthPet==4,"equal growth prefers the existing cared-for companion");
        ChapterGuideFixture(1);G.S.Xp[2]=1250;ChapterGuidePreview(root,"above-goal");check(LessonProgress("chapter-guide-progress").Value==20&&GuideWords().Contains("电团 Lv.10")&&G.ChapterDone(1),"completed growth bar is capped while text retains actual higher level");
        ChapterGuideFixture(6);G.S.SeedStage=3;ChapterGuidePreview(root,"final-form");check(LessonProgress("chapter-guide-progress").Value==2&&GuideWords().Contains("当前第 4 形态"),"final seed stage retains actual form without overfilling the goal");
        ChapterGuideFixture(0);CompleteChapterFixture(0);ChapterGuidePreview(root,"keyboard");ResetPanelInput();Render();UIPreview(root,"chapter-guide-keyboard",484,501);FocusByTab(GuidePrimary());KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);check(G.S.ChapterClaimed[0]&&G.S.Berries==34&&G.S.Grain==33,"keyboard claim and held Enter pay one reward");

        ChapterGuideFixture(7);string path=Path.Combine(root,"chapter-guide-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.OpenLife("成长手册");panel.lifeChapter=7;panel.ChapterGuidePreview(root,"normal-browse");string disk=File.ReadAllText(path);panel.GuideClick(panel.GuidePrimary());panel.ChapterGuidePreview(root,"normal-source");panel.KeyPress(Key.Escape);panel.ChapterGuidePreview(root,"normal-return");check(File.ReadAllText(path)==disk,"normal-save browsing and source cancellation write nothing");
        game.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();panel.ChapterGuidePreview(root,"normal-ready");panel.GuideClick(panel.GuidePrimary());panel.ChapterGuidePreview(root,"normal-claimed");var loaded=new FarmGame(path);check(loaded.S.ChapterClaimed[7]&&loaded.S.Fertilizer==42&&loaded.S.Crystals==14,"confirmed reward and actual progress survive isolated normal reload");state=FarmPracticeTests.Xml(loaded.S);check(!loaded.ClaimChapter(7)&&FarmPracticeTests.Xml(loaded.S)==state,"reloaded chapter cannot be claimed twice");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"normal chapter validation creates no native windows");
        ChapterGuideFixture(0);ChapterGuidePreview(root,"before-away");var old=GuidePrimary();G.BeginJourney(0,0);G.ToggleJourneyPause();state=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ChapterGuidePreview(root,"away-paused");GuideClick(GuidePrimary());check(G.R.JourneyPaused&&Tab=="冒险"&&FarmPracticeTests.Xml(G.S)==state,"old chapter and away page cannot claim rewards or resume a paused trip");
        ChapterGuideFixture(0);ChapterGuidePreview(root,"before-reset");old=GuidePrimary();G.ResetGuideTest();ResetGuideView();state=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ReadingPreview(root,"chapter-guide-fresh",0,true);check(FarmPracticeTests.Xml(G.S)==state&&G.SavePath==null&&!IsVisible&&!owner.IsVisible,"fresh guide rejects old chapter actions and remains detached and memory-only");
        File.WriteAllText(Path.Combine(root,"chapter-guide-regression.txt"),"PASS: eight chapters show actual progress with accessible names and one concrete next step; live production harvest changes the existing action to explicit claim; routes/evolution/farm/feeding/soil/relic previews spend nothing; same-chapter Escape and stale actions; no equipment-only victory or duplicate-route progress; exact reward and warehouse-cap receipts; explicit single keyboard claim; isolated normal save browsing/reload; paused journey and fresh guide; actual viewport at 100/115/130%. Detached WPF, not physical input or player comprehension testing.");
    }
}
