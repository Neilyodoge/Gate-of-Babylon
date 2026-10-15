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
    private void ProgressionPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"progression-"+name,size,true);GuideFits(GuidePrimary());
    }
    internal void RenderSmallProgressionChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        EverydayFixture();Go("伙伴");ProgressionPreview(root,"companion");GuideFits(UIAction("看看长大后的样子"));
        string before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("看看长大后的样子"));ProgressionPreview(root,"preview");
        check(Tab=="进化"&&WantsGentleNarrow&&smallGrowthStep==0&&FarmPracticeTests.Xml(G.S)==before&&!GuideWords().Contains("亲密")&&!GuideWords().Contains("晶石"),"preview introduces one future form without mutation or all requirements");
        GuideClick(GuidePrimary());ProgressionPreview(root,"level");check(GuideWords().Contains("先长到 Lv.3")&&!GuideWords().Contains("晶石")&&LessonProgress("small-evolution-progress").Value==36,"first need shows actual growth alone");
        var feed=GuidePrimary();int berries=G.S.Berries;GuideClick(feed);ProgressionPreview(root,"fed");check(G.S.Xp[0]==48&&G.S.Berries==berries-2,"growth step performs exactly one real feed");before=FarmPracticeTests.Xml(G.S);feed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale growth feed cannot spend twice");
        GuideClick(GuidePrimary());ProgressionPreview(root,"bond");check(G.Level(0)==3&&GuideWords().Contains("再熟悉彼此一点"),"level completion naturally reveals the next unmet requirement");
        G.S.Berries=0;ProgressionPreview(root,"berry-shortage");feed=GuidePrimary();G.S.Berries=2;before=FarmPracticeTests.Xml(G.S);GuideClick(feed);ProgressionPreview(root,"berry-ready");
        check(Tab=="莓果田"&&FarmPracticeTests.Xml(G.S)==before,"food-source button never changes into a spending action when berries arrive");
        GuideClick(GuidePrimary());ProgressionPreview(root,"berry-return");check(Tab=="进化"&&smallGrowthStep==1&&smallGrowthPet==0,"berry detour returns to the exact growth step");
        GuideClick(GuidePrimary());ProgressionPreview(root,"crystals");check(G.S.Bond==3&&GuideWords().Contains("还差 2 颗晶石")&&!G.S.Evolved,"feeding does not evolve automatically");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ProgressionPreview(root,"crystal-preparation");check(SmallRepeatDeparture&&journeyDifficulty==0&&journeyDepth==0&&FarmPracticeTests.Xml(G.S)==before,"crystal source opens preparation without departure, costs or reroll");
        G.S.Crystals=4;OpenSmallEvolution(0);ProgressionPreview(root,"ready-preview");GuideClick(GuidePrimary());ProgressionPreview(root,"ready");
        int xp=G.S.Xp[0],bond=G.S.Bond;int[] team=(int[])G.S.Team.Clone();double growth=G.S.Growth[0],seconds=G.CropSeconds(0);var evolve=GuidePrimary();
        GuideClick(evolve);ProgressionPreview(root,"evolved");check(G.S.Evolved&&G.S.Crystals==2&&G.CaveUnlocked&&G.CropSeconds(0)<seconds&&G.S.Growth[0]==growth&&G.S.Xp[0]==xp&&G.S.Bond==bond&&G.S.Team.SequenceEqual(team),"confirmed bubble evolution uses exact cost and effect while preserving growth, XP and team");
        check(smallGrowthStep==2&&GuideWords().Contains("用了 2 颗晶石"),"receipt comes from this actual evolution");before=FarmPracticeTests.Xml(G.S);evolve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale evolution cannot charge again");
        string path=Path.Combine(root,"small-progression-save.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var loaded=new FarmGame(path);check(loaded.S.Evolved&&loaded.S.Crystals==2&&loaded.S.Team.SequenceEqual(team),"normal save reload preserves actual permanent evolution");
        GuideClick(GuidePrimary());ProgressionPreview(root,"new-cave");check(Tab=="目的地"&&smallDestinationRegion==1&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"evolution receipt previews new cave without changing destination or starting travel");
        GuideClick(GuidePrimary());ProgressionPreview(root,"cave-depth");check(GuideWords().Contains("Lv.4～5"),"unlocked cave still states the actual suggested level");
        int selectedRegion=journeyDifficulty,selectedDepth=journeyDepth;var select=GuidePrimary();GuideClick(UIAction("下一段"));ProgressionPreview(root,"locked-depth");check(!G.ExpeditionUnlocked(1,1)&&GuideWords().Contains("先通关")&&journeyDifficulty==selectedRegion&&journeyDepth==selectedDepth,"previewing a locked depth never commits it");
        select.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="目的地"&&journeyDifficulty==selectedRegion,"old destination selection is inert after changing preview");GuideClick(GuidePrimary());ProgressionPreview(root,"back-to-first-depth");GuideClick(GuidePrimary());ProgressionPreview(root,"cave-preparation");
        var expected=XDocument.Parse(before);expected.Root.Element("PlannedRegion").Value="1";
        check(SmallRepeatDeparture&&journeyDifficulty==1&&journeyDepth==0&&G.AtHome&&XNode.DeepEquals(XDocument.Parse(FarmPracticeTests.Xml(G.S)),expected),"selection saves only the destination and returns to explicit departure");before=FarmPracticeTests.Xml(G.S);
        var staleStart=GuidePrimary();GuideClick(UIAction("换个地方看看"));ProgressionPreview(root,"reopen-destination");GuideClick(UIAction("也看看小溪"));ProgressionPreview(root,"preview-creek");GuideClick(UIAction("返回出发准备"));ProgressionPreview(root,"cancel-preview");
        check(journeyDifficulty==1&&journeyDepth==0&&G.AtHome,"cancelling browsing preserves the previously selected place");staleStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"departure control from before browsing cannot unexpectedly start travel");
        GuideClick(GuidePrimary());ProgressionPreview(root,"cave-started");check(!G.AtHome&&G.R.Difficulty==1&&G.R.Depth==0,"only explicit departure starts the selected cave");before=FarmPracticeTests.Xml(G.S);evolve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old evolution control cannot act during travel");

        EverydayFixture();G.S.Evolved=true;Go("伙伴");ProgressionPreview(root,"seed-entry");before=FarmPracticeTests.Xml(G.S);LaterExpander("life-companion-menu").IsExpanded=true;UIPreview(root,"progression-seed-entry-expanded",484,501);GuideClick(UIAction("看看暗影种子长大后"));ProgressionPreview(root,"seed-entry-preview");check(smallGrowthPet==4&&FarmPracticeTests.Xml(G.S)==before,"optional seed progression remains discoverable after bubble evolution without changing the party");
        EverydayFixture();OpenSmallDestination(1,0);ProgressionPreview(root,"locked-cave");before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ProgressionPreview(root,"locked-cave-evolution");check(Tab=="进化"&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"locked cave leads to evolution preview, without unlocking or evolving");
        Go("农场");OpenSmallDestination(0,0);ProgressionPreview(root,"unlock-fixture");G.S.ExpeditionClears[0]=1;GuideClick(GuidePrimary());ProgressionPreview(root,"creek-depth");GuideClick(UIAction("下一段"));ProgressionPreview(root,"unlocked-depth");GuideClick(GuidePrimary());ProgressionPreview(root,"deeper-preparation");check(journeyDepth==1&&journeyDifficulty==0&&G.AtHome,"an actually cleared depth enables the next route");

        EverydayFixture();G.S.Xp[4]=300;G.S.SeedBond=6;G.S.Crystals=12;G.S.Team=new[]{4,1,3};G.S.DesktopHidden=true;G.S.DesktopLocked=true;
        for(int stage=0;stage<3;stage++){
            OpenSmallEvolution(4);ProgressionPreview(root,"seed-preview-"+stage);GuideClick(GuidePrimary());ProgressionPreview(root,"seed-ready-"+stage);int cost=G.SeedEvolutionCost,crystals=G.S.Crystals;var action=GuidePrimary();GuideClick(action);ProgressionPreview(root,"seed-evolved-"+stage);
            check(G.S.SeedStage==stage+1&&G.S.Crystals==crystals-cost&&G.S.Xp[4]==300&&G.S.SeedBond==6&&G.S.Team.SequenceEqual(new[]{4,1,3})&&G.S.DesktopHidden&&G.S.DesktopLocked,"seed stage "+stage+" uses real rules and preserves roster and desktop preferences");
            before=FarmPracticeTests.Xml(G.S);action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old seed confirmation cannot buy another stage");
        }
        OpenSmallEvolution(4);ProgressionPreview(root,"seed-complete");check(!GuideWords().Contains("用了")&&Convert.ToString(GuidePrimary().Content)=="回小院","reopening cannot replay a previous completion receipt or sell a fourth evolution");
        XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);loaded=new FarmGame(path);check(loaded.S.SeedStage==3&&loaded.S.Crystals==3&&loaded.S.DesktopHidden&&loaded.S.DesktopLocked,"seed evolution and exact crystal balance persist normally");
        EverydayFixture();G.S.Team=new[]{4,1,3};G.S.Xp[4]=20;G.S.SeedBond=0;G.S.Berries=0;OpenSmallEvolution(4);ProgressionPreview(root,"seed-bond-start");GuideClick(GuidePrimary());ProgressionPreview(root,"seed-bond-shortage");GuideClick(GuidePrimary());ProgressionPreview(root,"seed-berries");check(Tab=="莓果田"&&scenes.Single().FocusedFarmPet==4,"seed berry detour keeps the correct companion");G.S.Berries=2;RefreshCounters();GuideClick(GuidePrimary());ProgressionPreview(root,"seed-bond-return");GuideClick(GuidePrimary());ProgressionPreview(root,"seed-fed");check(G.S.SeedBond==1&&G.S.Bond==0&&G.S.Xp[4]==32,"returning feeds the seed rather than the default bubble");

        for(int size=0;size<3;size++){
            EverydayFixture();Go("伙伴");ProgressionPreview(root,"font-companion",size);GuideFits(UIAction("看看长大后的样子"));
            OpenSmallEvolution(0);ProgressionPreview(root,"font-preview",size);GuideFits(UIAction("回去陪伙伴"));GuideClick(GuidePrimary());ProgressionPreview(root,"font-level",size);GuideFits(UIAction("回去陪伙伴"));
            G.S.Xp[0]=55;G.S.Bond=1;ProgressionPreview(root,"font-bond",size);G.S.Bond=3;ProgressionPreview(root,"font-crystals",size);
            G.S.Crystals=3;ProgressionPreview(root,"font-ready",size);GuideFits(UIAction("回去陪伙伴"));GuideClick(GuidePrimary());ProgressionPreview(root,"font-evolved",size);GuideFits(UIAction("先回小院"));
            GuideClick(GuidePrimary());ProgressionPreview(root,"font-cave",size);GuideFits(UIAction("返回出发准备"));GuideClick(GuidePrimary());ProgressionPreview(root,"font-depth",size);GuideFits(UIAction("返回出发准备"));
            GuideClick(UIAction("下一段"));ProgressionPreview(root,"font-locked",size);GuideFits(UIAction("返回出发准备"));GuideClick(UIAction("返回出发准备"));G.S.PlannedPicnic=-1;ProgressionPreview(root,"font-departure",size);GuideFits(UIAction("换个地方看看"));
            G.S.PlannedPicnic=0;G.S.Berries=99;ProgressionPreview(root,"font-picnic",size);GuideFits(UIAction("换个地方看看"));GuideFits(UIAction("先待在家里"));
        }
        EverydayFixture();G.S.Xp[0]=55;G.S.Bond=3;G.S.Crystals=2;OpenSmallEvolution(0);ProgressionPreview(root,"keyboard-preview");ResetPanelInput();Render();ProgressionPreview(root,"keyboard-preview-layout");FocusByTab(GuidePrimary());KeyPress(Key.Enter);ProgressionPreview(root,"keyboard-ready");check(smallGrowthStep==1&&!G.S.Evolved,"first Enter only opens the requirements");KeyPress(Key.Enter,ModifierKeys.None,true);check(!G.S.Evolved,"held key cannot evolve on the new step");KeyPress(Key.Enter);ProgressionPreview(root,"keyboard-evolved");check(G.S.Evolved&&G.S.Crystals==0,"new deliberate keyboard action confirms evolution exactly once");
        var stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();GuidePreview(root,"progression-reset");before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&G.SavePath==null&&!IsVisible&&!owner.IsVisible,"fresh tutorial invalidates progression controls and all checks remain detached from production saves and native windows");
        File.WriteAllText(Path.Combine(root,"small-progression-regression.txt"),"PASS: progressive form preview, one actual unmet requirement at a time, berry return to correct pet and step, exact guarded feed and permanent evolution transactions, seed stages and final form, actual receipt with no replay, normal save reload, cave and depth gates, preview cancellation, no roll/spend/departure until explicit start, stale control protection, keyboard repeat suppression, three reading sizes at minimum window including picnic preparation. Detached WPF and isolated saves only.");
    }
}

