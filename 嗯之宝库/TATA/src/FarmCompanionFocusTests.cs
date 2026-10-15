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
    private Button FocusEvolutionButton(string id="companion-evolution")
    {
        return LogicalNodes((DependencyObject)Content).OfType<Button>().SingleOrDefault(b=>AutomationProperties.GetAutomationId(b)==id);
    }
    private void CompanionFocusPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"companion-focus-"+name,size,true);var action=GuidePrimary();
        if(Under(action,scroller))ReviewTarget(action,"companion focus "+name);else GuideFits(action);
    }
    private void OpenFocusDetails(string root,string id,string name,Button action)
    {
        LaterExpander(id).IsExpanded=true;UIPreview(root,"companion-focus-"+name+"-expanded",484,501);
        ReviewTarget(action,"optional evolution "+name,true);UIPreview(root,"companion-focus-"+name+"-visible",484,501);
    }
    internal void RenderCompanionFocusChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)for(int pet=0;pet<FarmGame.PetCount;pet++){
            EverydayFixture();G.S.CourtyardPet=pet;G.S.Berries=30;Go("伙伴");string tag=pet+"-"+size;
            CompanionFocusPreview(root,"care-"+pet,size);string before=FarmPracticeTests.Xml(G.S);
            var source=FocusEvolutionButton();bool own=pet==0||pet==4;
            check(source!=null&&CanKeyboardReach(source)==own,"default evolution action belongs only to the current companion");
            check(!LaterExpander("life-companion-menu").IsExpanded,"other companion choices start collapsed");
            if(!own){
                source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==before,"hidden optional source cannot activate");
                OpenFocusDetails(root,"life-companion-menu",tag,source);check(FarmPracticeTests.Xml(G.S)==before,"expanding and scrolling never spends or changes companions");
                KeyPress(Key.Escape);CompanionFocusPreview(root,"care-cancelled-"+pet,size);
                source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==before,"collapsed old optional source stays inert");
                source=FocusEvolutionButton();OpenFocusDetails(root,"life-companion-menu",tag+"-reopened",source);
            }else ReviewTarget(source,"current companion evolution");
            FocusByTab(source);KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);CompanionFocusPreview(root,"preview-from-"+pet,size);
            int expected=pet==4?4:0;check(Tab=="进化"&&smallGrowthPet==expected&&smallGrowthStep==0&&G.CourtyardCompanion==pet&&FarmPracticeTests.Xml(G.S)==before,"explicit preview names the intended creature without changing cared-for pet or saved plans");
            var another=FocusEvolutionButton("evolution-other-companion");check(another!=null&&!CanKeyboardReach(another)&&!LaterExpander("small-evolution-others").IsExpanded,"alternate form remains optional in the preview");
            another.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(smallGrowthPet==expected&&FarmPracticeTests.Xml(G.S)==before,"hidden alternate cannot switch target");
            OpenFocusDetails(root,"small-evolution-others",tag+"-alternate",another);KeyPress(Key.Escape);CompanionFocusPreview(root,"preview-cancelled-"+pet,size);
            check(Tab=="进化"&&smallGrowthPet==expected&&!CanKeyboardReach(FocusEvolutionButton("evolution-other-companion")),"Escape first closes the alternate area without leaving current preview");
            another=FocusEvolutionButton("evolution-other-companion");OpenFocusDetails(root,"small-evolution-others",tag+"-alternate-again",another);GuideClick(another);CompanionFocusPreview(root,"other-from-"+pet,size);
            int alternate=expected==0?4:0;check(smallGrowthPet==alternate&&smallGrowthStep==0&&FarmPracticeTests.Xml(G.S)==before,"explicit alternate is a read-only preview with no automatic evolution or care switch");
            another.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(smallGrowthPet==alternate&&FarmPracticeTests.Xml(G.S)==before,"old alternate cannot reopen a former target");
            GuideClick(UIAction("回去陪伙伴"));CompanionFocusPreview(root,"care-returned-"+pet,size);check(G.CourtyardCompanion==pet&&FarmPracticeTests.Xml(G.S)==before&&!LaterExpander("life-companion-menu").IsExpanded,"return restores the actual care companion with optional choices collapsed");
        }

        for(int size=0;size<3;size++){
            EverydayFixture();G.S.Evolved=true;Go("伙伴");CompanionFocusPreview(root,"bubble-finished",size);var source=FocusEvolutionButton();
            check(source!=null&&!CanKeyboardReach(source)&&Convert.ToString(source.Content).Contains(G.Name(4)),"finished bubble offers seed only inside optional choices");
            OpenFocusDetails(root,"life-companion-menu","finished-bubble-"+size,source);string before=FarmPracticeTests.Xml(G.S);GuideClick(source);CompanionFocusPreview(root,"seed-after-bubble",size);check(smallGrowthPet==4&&FarmPracticeTests.Xml(G.S)==before,"seed remains reachable after bubble is fully evolved");
            EverydayFixture();G.S.SeedStage=3;G.S.CourtyardPet=4;Go("伙伴");CompanionFocusPreview(root,"seed-finished",size);source=FocusEvolutionButton();check(source!=null&&!CanKeyboardReach(source),"finished seed does not push another companion into default care");
            G.S.Evolved=true;CompanionFocusPreview(root,"all-finished",size);check(FocusEvolutionButton()==null,"no obsolete evolution entry when all forms are complete");
            EverydayFixture();G.S.Xp[0]=55;G.S.Bond=3;G.S.Crystals=4;Go("伙伴");CompanionFocusPreview(root,"ready-current",size);source=FocusEvolutionButton();check(CanKeyboardReach(source)&&Convert.ToString(source.Content)=="可以进化啦 · 看看新样子","current companion ready state keeps its direct entry");
        }

        EverydayFixture();G.S.CourtyardPet=2;G.S.Berries=30;Go("伙伴");CompanionFocusPreview(root,"feed-origin");var old=FocusEvolutionButton();OpenFocusDetails(root,"life-companion-menu","feed-origin",old);GuideClick(old);CompanionFocusPreview(root,"feed-preview");GuideClick(GuidePrimary());CompanionFocusPreview(root,"feed-requirement");int xp=G.S.Xp[0],berries=G.S.Berries;GuideClick(GuidePrimary());CompanionFocusPreview(root,"feed-applied");
        check(G.S.Xp[0]==xp+12&&G.S.Berries==berries-2&&G.CourtyardCompanion==2,"optional preview feeding still explicitly acts on the previewed companion only");GuideClick(UIAction("回去陪伙伴"));CompanionFocusPreview(root,"feed-back");string saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴"&&G.CourtyardCompanion==2&&FarmPracticeTests.Xml(G.S)==saved,"old optional entry cannot reopen after returning");
        EverydayFixture();Go("伙伴");CompanionFocusPreview(root,"stale-before");old=FocusEvolutionButton();Go("农场");Go("伙伴");CompanionFocusPreview(root,"stale-revisited");saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==saved,"same-pet revisit invalidates the earlier direct preview");
        old=FocusEvolutionButton();G.ChooseCourtyardCompanion(2);CompanionFocusPreview(root,"stale-switched");saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴"&&FarmPracticeTests.Xml(G.S)==saved,"changing care pet invalidates the old direct preview");
        G.EnsureJourneyOffers();check(G.BeginJourney(0,G.S.BlessingOffers[0],0),"paused fixture starts a real trip");G.ToggleJourneyPause();CompanionFocusPreview(root,"away-paused");saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.R.JourneyPaused&&!G.AtHome&&FarmPracticeTests.Xml(G.S)==saved,"old preview cannot alter an active paused trip");

        ChapterGuideFixture(1);CompanionFocusPreview(root,"chapter");GuideClick(GuidePrimary());CompanionFocusPreview(root,"chapter-choice");GuideClick(GuidePrimary());CompanionFocusPreview(root,"chapter-care");
        check(G.CourtyardCompanion==2&&HasChapterReturn&&!CanKeyboardReach(FocusEvolutionButton()),"chapter growth keeps its chosen companion as the only default care target");GuideClick(GuidePrimary());CompanionFocusPreview(root,"chapter-fed");GuideClick(ChapterBack());CompanionFocusPreview(root,"chapter-done");check(lifeChapter==1&&G.ChapterDone(1)&&!G.S.ChapterClaimed[1],"feeding then visible chapter return keeps actual progress and explicit gift claiming");

        EverydayFixture();G.S.CourtyardPet=2;G.S.ReadingSize=2;string path=Path.Combine(root,"companion-focus-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.Go("伙伴");panel.CompanionFocusPreview(root,"normal-care",2);string disk=File.ReadAllText(path);
        var action=panel.FocusEvolutionButton();panel.OpenFocusDetails(root,"life-companion-menu","normal",action);panel.GuideClick(action);panel.CompanionFocusPreview(root,"normal-preview",2);panel.KeyPress(Key.Escape);panel.CompanionFocusPreview(root,"normal-back",2);check(File.ReadAllText(path)==disk&&game.CourtyardCompanion==2,"normal saved progress is untouched by optional preview and cancellation");
        var loaded=new FarmGame(path);var reload=new FarmWindow(loaded,true);reload.Panel=new FarmPanel(reload);reload.Panel.Go("伙伴");reload.Panel.CompanionFocusPreview(root,"normal-reloaded",2);check(loaded.CourtyardCompanion==2&&!reload.Panel.LaterExpander("life-companion-menu").IsExpanded,"reload retains selected companion and starts optional choices folded");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero&&new WindowInteropHelper(reload).Handle==IntPtr.Zero,"normal checks remain detached");
        old=FocusEvolutionButton();G.ResetGuideTest();ResetGuideView();CompanionFocusPreview(root,"fresh");saved=FarmPracticeTests.Xml(G.S);if(old!=null)old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.SavePath==null&&FarmPracticeTests.Xml(G.S)==saved&&!IsVisible&&!owner.IsVisible,"fresh guide has no old optional entry or player save access");
        File.WriteAllText(Path.Combine(root,"companion-focus-regression.txt"),"PASS: five current companions at 100/115/130%; only own evolution shown by default, other companions in collapsed existing menu; preview alternatives explicitly expanded and chosen; hidden and stale controls inert; Escape closes disclosure, keyboard and held Enter; confirmed feeding targets previewed creature without changing care pet; finished forms and direct ready entry; exact chapter return; isolated normal-save browsing and reload; paused travel and memory-only reset. Actual WPF viewport and scroll reachability, no native windows or physical input claim.");
    }
}
