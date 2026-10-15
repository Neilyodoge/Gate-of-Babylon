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
    private void PicnicCompanionFixture(int recipe)
    {
        PreparationFixture();G.S.GardenUnlocked=true;G.S.Berries=G.S.Grain=G.S.Mint=40;
        G.S.Team=recipe==3?new[]{0,1,3}:new[]{0,2,3};G.S.CourtyardPet=3;
        G.PlanJourneyPicnic(recipe,1,1);Go("冒险");
    }
    private void PicnicCompanionPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"picnic-friend-"+name,size,true);ReviewTarget(GuidePrimary(),"picnic companion "+name+" "+size);
    }
    internal void RenderPicnicCompanionChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)foreach(int recipe in new[]{3,4,5}){
            PicnicCompanionFixture(recipe);G.S.ReadingSize=size;int pet=PicnicRequiredFriend(recipe);string tag=recipe.ToString();
            PicnicCompanionPreview(root,"missing-"+tag,size);string before=FarmPracticeTests.Xml(G.S);var source=GuidePrimary();
            check(Convert.ToString(source.Content)=="先看看同行伙伴"&&GuideWords().Contains("不在这队"),"departure explains missing required companion before cooking");
            ReviewTarget(UIAction("不带便当"),"free no-picnic alternative "+size);GuideClick(source);PicnicCompanionPreview(root,"position-"+tag,size);
            check(Tab=="便当伙伴"&&GuideWords().Contains(G.Name(pet))&&FarmPracticeTests.Xml(G.S)==before,"opening recovery has no spending, reroll, party or save changes");
            ReviewTarget(UIAction("换个位置看看"),"browse position "+size);ReviewTarget(UIAction("先不换，回准备"),"cancel position "+size);
            var stale=GuidePrimary();KeyPress(Key.Escape);PicnicCompanionPreview(root,"cancel-"+tag,size);
            stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(Tab=="冒险"&&journeyDifficulty==1&&journeyDepth==1&&FarmPracticeTests.Xml(G.S)==before,"Escape preserves exact recipe and route; old recovery and departure controls are inert");
            GuideClick(GuidePrimary());PicnicCompanionPreview(root,"reopen-"+tag,size);
            for(int slot=0;slot<3;slot++){
                check(picnicPartySlot==slot&&GuideWords().Contains("第 "+(slot+1)+" 位")&&GuideWords().Contains(G.Name(G.S.Team[slot])),"preview identifies exact position and outgoing friend");
                stale=GuidePrimary();var cycle=UIAction("换个位置看看");GuideClick(cycle);PicnicCompanionPreview(root,"slot-"+slot+"-"+tag,size);
                stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));cycle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(picnicPartySlot==(slot+1)%3&&FarmPracticeTests.Xml(G.S)==before,"cycling has no transaction and invalidates stale slot controls");
            }
            for(int slot=0;slot<size;slot++){GuideClick(UIAction("换个位置看看"));PicnicCompanionPreview(root,"choose-"+slot+"-"+tag,size);}
            var expected=(int[])G.S.Team.Clone();expected[size]=pet;int display=G.S.DisplayPet;var xp=(int[])G.S.Xp.Clone();stale=GuidePrimary();
            ResetPanelInput();Render();UIPreview(root,"picnic-friend-keyboard-"+recipe+"-"+size,484,501);FocusByTab(GuidePrimary());KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);
            PicnicCompanionPreview(root,"ready-"+tag,size);
            check(G.AtHome&&Tab=="冒险"&&G.S.Team.SequenceEqual(expected)&&G.S.PlannedPicnic==recipe&&journeyDifficulty==1&&journeyDepth==1,"explicit keyboard confirmation changes only selected slot and returns to the same preparation");
            check(G.S.Berries==40&&G.S.Grain==40&&G.S.Mint==40&&G.S.CourtyardPet==3&&G.S.DisplayPet==display&&G.S.Xp.SequenceEqual(xp),"assignment costs no ingredients and preserves cared-for pet, display pet and growth");
            before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"repeat confirmation cannot replace twice");
            int[] cost=FarmGame.PicnicCost(recipe);var depart=GuidePrimary();GuideClick(depart);
            check(G.JourneyRunning&&G.R.Difficulty==1&&G.R.Depth==1&&G.R.Picnic==recipe&&G.S.Berries==40-cost[0]&&G.S.Grain==40-cost[1]&&G.S.Mint==40-cost[2],"only separate departure cooks once at the displayed cost");
            before=FarmPracticeTests.Xml(G.S);depart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"repeated departure cannot charge again");
        }

        PicnicCompanionFixture(5);G.S.Berries=0;PicnicCompanionPreview(root,"both-missing",2);GuideClick(GuidePrimary());PicnicCompanionPreview(root,"both-position",2);GuideClick(GuidePrimary());PicnicCompanionPreview(root,"materials-next",2);
        check(Convert.ToString(GuidePrimary().Content)=="先看看缺的材料"&&G.S.PlannedPicnic==5&&G.S.Berries==0,"after companion recovery, material shortage is taught separately");
        GuideClick(GuidePrimary());PicnicCompanionPreview(root,"material-source",2);check(Tab=="材料"&&G.AtHome&&G.S.PlannedPicnic==5,"missing materials open the existing resource recovery without departure");
        PicnicCompanionFixture(5);PicnicCompanionPreview(root,"no-meal-before");var old=GuidePrimary();GuideClick(UIAction("不带便当"));PicnicCompanionPreview(root,"no-meal");string saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.S.PlannedPicnic==-1&&G.AtHome&&FarmPracticeTests.Xml(G.S)==saved,"no-picnic alternative clears plan and invalidates old companion action");GuideClick(GuidePrimary());check(G.R.Picnic==-1&&G.S.Berries==40&&G.S.Grain==40&&G.S.Mint==40,"free departure remains available without replacing anyone");

        PicnicCompanionFixture(3);PicnicCompanionPreview(root,"stale-roster");old=GuidePrimary();G.AssignPartySlot(1,2);saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&FarmPracticeTests.Xml(G.S)==saved,"old missing-companion action cannot become a paid departure after a party change");
        PicnicCompanionFixture(4);PicnicCompanionPreview(root,"edge-entry");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"stale-recipe");old=GuidePrimary();G.PlanPicnic(0);saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==saved,"changed recipe invalidates pending party assignment");PicnicCompanionPreview(root,"changed-recipe");GuideClick(GuidePrimary());check(Tab=="冒险"&&G.S.PlannedPicnic==0,"changed recipe has a safe return");
        PicnicCompanionFixture(4);PicnicCompanionPreview(root,"edge-entry");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"button-cancel");saved=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("先不换，回准备"));PicnicCompanionPreview(root,"button-cancelled");check(G.AtHome&&FarmPracticeTests.Xml(G.S)==saved,"visible cancel returns without assignment");

        CrystalFixture(4,2);G.S.GardenUnlocked=true;G.S.Berries=G.S.Grain=G.S.Mint=40;G.PlanPicnic(5);CrystalPreview(root,"picnic-friend-source");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"crystal-missing");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"crystal-position");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"crystal-ready");
        check(HasCrystalPreparation&&crystalPreparationPet==4&&crystalPreparationStage==2,"companion recovery retains exact crystal evolution purpose");GuideClick(GuidePrimary());check(G.R.CrystalTargetPet==4&&G.R.CrystalTargetStage==2&&G.R.Picnic==5,"final journey carries original evolution purpose and explicit meal");

        PicnicCompanionFixture(5);string path=Path.Combine(root,"picnic-friend-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.PrepareFromHome();panel.PicnicCompanionPreview(root,"normal-start");
        string disk=File.ReadAllText(path);panel.GuideClick(panel.GuidePrimary());panel.PicnicCompanionPreview(root,"normal-position");panel.GuideClick(panel.UIAction("换个位置看看"));panel.PicnicCompanionPreview(root,"normal-browse");check(File.ReadAllText(path)==disk,"normal-save browsing never writes the save");panel.GuideClick(panel.GuidePrimary());panel.PicnicCompanionPreview(root,"normal-confirmed");
        var loaded=new FarmGame(path);check(loaded.S.Team.SequenceEqual(new[]{0,4,3})&&loaded.S.PlannedPicnic==5&&loaded.S.PlannedRegion==1&&loaded.S.PlannedDepth==1&&loaded.S.Berries==40&&loaded.S.CourtyardPet==3,"confirmed companion, recipe and route survive normal isolated reload");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"normal save test creates no native windows");
        PicnicCompanionFixture(5);PicnicCompanionPreview(root,"edge-entry");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"before-away");old=GuidePrimary();G.BeginJourney(1,0,1);G.ToggleJourneyPause();saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicCompanionPreview(root,"away-paused");GuideClick(GuidePrimary());check(G.R.JourneyPaused&&Tab=="冒险"&&FarmPracticeTests.Xml(G.S)==saved,"away and paused states prevent pending party edits and retain pause");
        PicnicCompanionFixture(5);PicnicCompanionPreview(root,"edge-entry");GuideClick(GuidePrimary());PicnicCompanionPreview(root,"before-reset");old=GuidePrimary();G.ResetGuideTest();ResetGuideView();saved=FarmPracticeTests.Xml(G.S);old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ReadingPreview(root,"picnic-friend-fresh",0,true);
        check(FarmPracticeTests.Xml(G.S)==saved&&G.SavePath==null&&!IsVisible&&!owner.IsVisible,"fresh guide rejects old assignment and remains memory-only and detached");
        File.WriteAllText(Path.Combine(root,"picnic-companion-regression.txt"),"PASS: three companion-specific meals recover missing party before spending; each actual slot, explicit assignment, read-only browsing, cancel/Escape/keyboard/held Enter and stale events; separate exact cooking charge; no-food alternative; sequential material shortage; exact route, recipe, courtyard/display pet and crystal purpose; isolated save browsing and reload; away/pause/fresh guide; actual viewport at 100/115/130%. Detached WPF only, no physical input or player comprehension claim.");
    }

}

