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
    private Button ChapterBack(){return UIChildren<Button>((DependencyObject)Content).Single(b=>AutomationProperties.GetAutomationId(b)=="chapter-return");}
    private bool ChapterBackVisible(){return UIChildren<Button>((DependencyObject)Content).Any(b=>AutomationProperties.GetAutomationId(b)=="chapter-return");}
    private void ChapterReturnPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"ux33-"+name,size,true);var primary=GuidePrimary();if(Under(primary,scroller))ReviewTarget(primary,"review 3.3 primary "+name);else GuideFits(primary);
        if(!HasChapterReturn)return;
        var back=ChapterBack();GuideFits(back);var stock=(FrameworkElement)resourceNumbers[0].Parent;
        var area=back.TranslatePoint(new Point(back.ActualWidth,back.ActualHeight),(UIElement)Content);var resource=stock.TranslatePoint(new Point(0,0),(UIElement)Content);var bodyStart=body.TranslatePoint(new Point(0,0),(UIElement)Content);
        FarmHeadlessTests.Check(area.X<=resource.X+.5&&area.Y<=bodyStart.Y+.5&&CanKeyboardReach(back),"contextual return fits header without covering stock or body at "+size);
    }
    private void ChapterReturnSource(string root,int chapter,int size=0)
    {
        ChapterGuideFixture(chapter);ChapterReturnPreview(root,"chapter-"+chapter,size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"source-"+chapter,size);
    }
    internal void RenderChapterReturnChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)for(int chapter=0;chapter<8;chapter++){
            ChapterGuideFixture(chapter);G.S.Berries=99999;G.S.ReadingSize=size;ChapterReturnPreview(root,"chapter-"+chapter,size);string before=FarmPracticeTests.Xml(G.S);
            check(!ChapterBackVisible(),"chapter itself does not duplicate its return action");GuideClick(GuidePrimary());ChapterReturnPreview(root,"source-"+chapter,size);
            var back=ChapterBack();check(HasChapterReturn&&chapterReturnIndex==chapter&&AutomationProperties.GetName(back).Contains(FarmGame.ChapterNames[chapter])&&FarmPracticeTests.Xml(G.S)==before,"eight source pages carry the precise origin without writing or spending");
            GuideClick(back);ChapterReturnPreview(root,"returned-"+chapter,size);
            check(Tab=="成长手册"&&lifeChapter==chapter&&!ChapterBackVisible()&&!HasChapterReturn&&FarmPracticeTests.Xml(G.S)==before,"visible return reaches the original chapter with no automatic claim");
            back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&Tab=="成长手册","old return does not navigate or mutate after use");
            KeyPress(Key.Escape);ChapterReturnPreview(root,"left-chapter-"+chapter,size);check(Tab=="农场"&&!ChapterBackVisible(),"return removes the detour from history; Escape leaves the chapter rather than reopening the source");
        }

        for(int size=0;size<3;size++){
            ChapterReturnSource(root,1,size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"growth-chosen",size);var preFeedBack=ChapterBack();int berries=G.S.Berries;
            GuideClick(GuidePrimary());ChapterReturnPreview(root,"growth-fed",size);check(G.S.Xp[2]==24&&G.S.Berries==berries-2&&HasChapterReturn,"explicit care and feeding retain the current chapter return");
            int[] stocks=ChapterStocks();GuideClick(ChapterBack());ChapterReturnPreview(root,"growth-done",size);check(lifeChapter==1&&G.ChapterDone(1)&&!G.S.ChapterClaimed[1]&&ChapterStocks().SequenceEqual(stocks)&&Convert.ToString(GuidePrimary().Content)=="收好这份成长礼物","return shows real completion without refunding care or claiming its gift");
            string before=FarmPracticeTests.Xml(G.S);preFeedBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"earlier same-visit header cannot reenter a finished detour");

            ChapterReturnSource(root,3,size);int cost=G.SoilCost,soil=G.S.SoilLevel;GuideClick(GuidePrimary());ChapterReturnPreview(root,"soil-applied",size);stocks=ChapterStocks();
            GuideClick(ChapterBack());ChapterReturnPreview(root,"soil-done",size);check(lifeChapter==3&&G.S.SoilLevel==soil+1&&G.S.Fertilizer==30-cost&&!G.S.ChapterClaimed[3]&&ChapterStocks().SequenceEqual(stocks),"soil receipt can return to exact chapter with its real charge retained");

            ChapterGuideFixture(4);G.S.Xp[4]=1250;G.S.SeedBond=99;G.S.Crystals=40;ChapterReturnPreview(root,"evolution-chapter",size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"evolution-preview",size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"evolution-ready",size);
            int crystals=G.S.Crystals,evolutionCost=G.SeedEvolutionCost;GuideClick(GuidePrimary());ChapterReturnPreview(root,"evolution-applied",size);GuideClick(ChapterBack());ChapterReturnPreview(root,"evolution-done",size);
            check(lifeChapter==4&&G.S.SeedStage==1&&G.S.Crystals==crystals-evolutionCost&&!G.S.ChapterClaimed[4],"evolution return retains confirmed form and cost without granting chapter gift");

            ChapterGuideFixture(5);G.S.ExpeditionClears[0]=1;ChapterReturnPreview(root,"relic-chapter",size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"relic-preview",size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"relic-equipped",size);GuideClick(ChapterBack());ChapterReturnPreview(root,"relic-returned",size);
            check(lifeChapter==5&&G.S.EquippedRelics[0]==0&&!G.ChapterDone(5)&&Convert.ToString(GuidePrimary().Content)=="准备带信物去小溪","return retains explicit equipment but does not pretend it was a victory");

            ChapterReturnSource(root,7,size);GuideClick(GuidePrimary());ChapterReturnPreview(root,"route-selected",size);before=FarmPracticeTests.Xml(G.S);check(G.S.PlannedRegion==0&&G.S.PlannedDepth==2&&G.AtHome,"route choice is explicit preparation");
            GuideClick(ChapterBack());ChapterReturnPreview(root,"route-returned",size);check(lifeChapter==7&&FarmPracticeTests.Xml(G.S)==before&&G.AtHome,"return retains an explicitly selected route and never starts it");
        }

        ChapterReturnSource(root,1);GuideClick(GuidePrimary());ChapterReturnPreview(root,"berry-before");G.S.Berries=0;ChapterReturnPreview(root,"berry-shortage");GuideClick(GuidePrimary());ChapterReturnPreview(root,"berry-source");string saved=FarmPracticeTests.Xml(G.S);GuideClick(ChapterBack());ChapterReturnPreview(root,"berry-returned");check(lifeChapter==1&&G.S.Berries==0&&FarmPracticeTests.Xml(G.S)==saved,"missing berries remain a read-only detour back to the original growth goal");
        ChapterGuideFixture(4);G.S.Xp[4]=1250;G.S.SeedBond=99;G.S.Crystals=0;ChapterReturnPreview(root,"crystal-chapter");GuideClick(GuidePrimary());ChapterReturnPreview(root,"crystal-evolution");GuideClick(GuidePrimary());ChapterReturnPreview(root,"crystal-shortage");GuideClick(GuidePrimary());ChapterReturnPreview(root,"crystal-preparation");check(HasCrystalPreparation&&HasChapterReturn,"crystal preparation still knows both its evolution target and chapter origin");var oldStart=GuidePrimary();saved=FarmPracticeTests.Xml(G.S);GuideClick(ChapterBack());ChapterReturnPreview(root,"crystal-cancelled");oldStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(lifeChapter==4&&!HasCrystalPreparation&&G.AtHome&&FarmPracticeTests.Xml(G.S)==saved,"return cancels uncommitted crystal purpose without changing saved resources or starting from an old control");

        ChapterReturnSource(root,3);var oldBack=ChapterBack();GuideClick(UIAction("伙伴"));ChapterReturnPreview(root,"independent-companion");saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!HasChapterReturn&&!ChapterBackVisible()&&Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==saved,"independent top navigation drops the old origin and invalidates old return");
        ChapterReturnSource(root,1);GuideClick(UIAction("返回伙伴"));ChapterReturnPreview(root,"ordinary-parent");GuideClick(UIAction("回小院"));ChapterReturnPreview(root,"abandoned-home");Go("伙伴");ChapterReturnPreview(root,"ordinary-reopened");check(!ChapterBackVisible(),"leaving for home cannot resurrect a stale chapter origin later");
        ChapterReturnSource(root,0);var same=Content;owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(5),false,true,false);check(Object.ReferenceEquals(Content,same)&&Tab=="农田"&&HasChapterReturn&&!G.S.ChapterClaimed[0],"five seconds without input preserves the source and never claims rewards");
        FocusByTab(ChapterBack());KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);ChapterReturnPreview(root,"keyboard-return");check(Tab=="成长手册"&&lifeChapter==0&&!HasChapterReturn,"keyboard and held Enter use the same one-time return");

        ChapterReturnSource(root,2);GuideClick(GuidePrimary());ChapterReturnPreview(root,"before-travel");oldBack=ChapterBack();GuideClick(GuidePrimary());G.ToggleJourneyPause();ChapterReturnPreview(root,"travel-paused");saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!HasChapterReturn&&!ChapterBackVisible()&&G.R.JourneyPaused&&FarmPracticeTests.Xml(G.S)==saved,"actual departure clears the home-only origin and stale return cannot affect pause or travel");
        G.Retreat();G.Home();Go("伙伴");ChapterReturnPreview(root,"after-travel");check(!ChapterBackVisible(),"finished travel does not revive abandoned home navigation");

        ChapterGuideFixture(3);string path=Path.Combine(root,"chapter-return-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.OpenLife("成长手册");panel.lifeChapter=3;panel.ChapterReturnPreview(root,"normal-chapter");string disk=File.ReadAllText(path);panel.GuideClick(panel.GuidePrimary());panel.ChapterReturnPreview(root,"normal-source");panel.GuideClick(panel.ChapterBack());panel.ChapterReturnPreview(root,"normal-cancelled");check(File.ReadAllText(path)==disk,"normal-save contextual return writes nothing");
        panel.GuideClick(panel.GuidePrimary());panel.ChapterReturnPreview(root,"normal-apply");panel.GuideClick(panel.GuidePrimary());panel.ChapterReturnPreview(root,"normal-applied");panel.GuideClick(panel.ChapterBack());panel.ChapterReturnPreview(root,"normal-done");var loaded=new FarmGame(path);check(loaded.S.SoilLevel==1&&loaded.S.Fertilizer==26&&!loaded.S.ChapterClaimed[3],"normal reload preserves actual confirmed work without auto-claiming the goal");
        var reloadWindow=new FarmWindow(loaded,true);var reloadPanel=new FarmPanel(reloadWindow);reloadWindow.Panel=reloadPanel;reloadPanel.Go("施肥");reloadPanel.ChapterReturnPreview(root,"normal-reopened");check(!reloadPanel.HasChapterReturn&&!reloadPanel.ChapterBackVisible(),"new window starts ordinary navigation instead of inventing a persisted unfinished view");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero&&new WindowInteropHelper(reloadWindow).Handle==IntPtr.Zero,"normal checks do not create native windows");
        ChapterReturnSource(root,3);oldBack=ChapterBack();G.ResetGuideTest();ResetGuideView();saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ReadingPreview(root,"ux33-fresh",0,true);check(!HasChapterReturn&&!ChapterBackVisible()&&G.SavePath==null&&FarmPracticeTests.Xml(G.S)==saved&&!IsVisible&&!owner.IsVisible,"fresh guide clears origin and rejects old return with no production saves or windows");
        File.WriteAllText(Path.Combine(root,"chapter-return-regression.txt"),"PASS: visible header return from all eight chapter sources; exact origin, no preview writes or costs, stale controls and cleaned back history; actual feeding/soil/evolution/equipment/route changes retained without auto-claim; berry/crystal detours; explicit main navigation and departure clear origin; pause preserved; keyboard/held Enter and five-second idle; isolated normal browsing/save/reload; fresh memory-only guide; 100/115/130% header bounds and no stock/body overlap. Detached WPF events, not physical mouse or player comprehension tests.");
    }
}
