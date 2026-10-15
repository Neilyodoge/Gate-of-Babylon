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
    private void SkillCareFixture(int pet,int xp=12)
    {
        EverydayFixture();G.S.CourtyardPet=pet;G.S.Xp[pet]=xp;G.S.Grain=9;G.S.Mint=1;G.S.Berries=30;G.S.DisplayPet=4;G.S.GardenUnlocked=true;Go("伙伴");OpenLife("技能");lifeSkill=1;
    }
    private void SkillCarePreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"skill-care-"+name,size,true);var action=GuidePrimary();
        if(Under(action,scroller))ReviewTarget(action,"skill care "+name);else GuideFits(action);
    }
    internal void RenderSkillCareChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)for(int pet=0;pet<FarmGame.PetCount;pet++){
            SkillCareFixture(pet);SkillCarePreview(root,"locked-"+pet,size);string before=FarmPracticeTests.Xml(G.S);var bar=LessonProgress("skill-learning-progress");
            check(bar.Value==12&&bar.Maximum==20&&AutomationProperties.GetName(bar).Contains(FarmGame.SkillNames[pet][1])&&GuideWords().Contains("成长 12 / 20"),"locked skill shows actual progress and correct companion/skill");
            ReviewTarget(UIAction("看看另一招"),"skill alternative fits");ReviewTarget(UIAction("返回伙伴"),"skill back fits without scrolling");
            var source=GuidePrimary();GuideClick(source);SkillCarePreview(root,"training-"+pet,size);check(HasSkillCare&&skillCarePet==pet&&skillCareSkill==1&&Tab=="照料"&&FarmPracticeTests.Xml(G.S)==before,"source opens exact skill training without spending or equipment changes");
            ReviewTarget(UIAction("回去看这招"),"skill cancellation");GuideClick(UIAction("回去看这招"));SkillCarePreview(root,"cancelled-"+pet,size);check(Tab=="技能"&&lifeSkill==1&&!HasSkillCare&&FarmPracticeTests.Xml(G.S)==before,"visible cancellation returns to original skill without cost");
            source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="技能"&&FarmPracticeTests.Xml(G.S)==before,"old source cannot reopen a cancelled training detour");
            GuideClick(GuidePrimary());SkillCarePreview(root,"reopened-"+pet,size);var purchase=GuidePrimary();int grain=G.S.Grain,xp=G.S.Xp[pet];GuideClick(purchase);SkillCarePreview(root,"trained-"+pet,size);
            check(G.S.Xp[pet]==xp+18&&G.S.Grain==grain-3&&G.S.Skills[pet]==0&&HasSkillCare&&Convert.ToString(GuidePrimary().Content)=="回去看这招","real training costs once, retains goal and offers a return instead of another purchase");
            check(!LogicalNodes((DependencyObject)Content).OfType<Button>().Any(b=>Convert.ToString(b.Content)=="看看刚学会的一招"),"goal completion does not duplicate the learned-skill return");
            before=FarmPracticeTests.Xml(G.S);purchase.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old training cannot spend twice");
            FocusByTab(GuidePrimary());KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);SkillCarePreview(root,"learned-"+pet,size);
            check(Tab=="技能"&&lifeSkill==1&&Convert.ToString(GuidePrimary().Content)=="换上这一招"&&FarmPracticeTests.Xml(G.S)==before&&!HasSkillCare,"return previews learned target without auto-equipping even with held Enter");
            ReviewTarget(UIAction("看看另一招"),"learned alternative fits");ReviewTarget(UIAction("返回伙伴"),"learned back fits without scrolling");
            var equip=GuidePrimary();GuideClick(equip);SkillCarePreview(root,"equipped-"+pet,size);check(G.S.Skills[pet]==1&&G.S.Grain==grain-3&&G.S.Xp[pet]==xp+18&&G.CourtyardCompanion==pet&&G.S.DisplayPet==4,"separate explicit equipment is free and preserves care/display/experience");
            before=FarmPracticeTests.Xml(G.S);equip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old equipment button is inert");
            KeyPress(Key.Escape);SkillCarePreview(root,"left-"+pet,size);check(Tab=="伙伴","return removes training detour from back history");
        }
        for(int size=0;size<3;size++){
            SkillCareFixture(2,0);SkillCarePreview(root,"zero",size);GuideClick(GuidePrimary());SkillCarePreview(root,"zero-training",size);GuideClick(GuidePrimary());SkillCarePreview(root,"not-yet",size);GuideClick(GuidePrimary());SkillCarePreview(root,"still-missing",size);
            check(LessonProgress("skill-learning-progress").Value==18&&!G.SkillUnlocked(2,1)&&G.S.Skills[2]==0&&Convert.ToString(GuidePrimary().Content)=="先陪它长大","one insufficient training returns to honest 18 of 20 with next action");
            GuideClick(GuidePrimary());SkillCarePreview(root,"last-training",size);GuideClick(GuidePrimary());SkillCarePreview(root,"enough-now",size);GuideClick(GuidePrimary());SkillCarePreview(root,"ready-now",size);check(G.S.Xp[2]==36&&G.S.Grain==3&&G.S.Skills[2]==0,"second explicit training completes real progress without selecting the skill");
        }

        SkillCareFixture(4);G.S.Grain=0;for(int i=0;i<G.Plots;i++)G.S.Crops[i]=0;SkillCarePreview(root,"grain-start",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-shortage",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-source",2);string saved=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-plant-preview",2);KeyPress(Key.Escape);SkillCarePreview(root,"grain-plant-cancelled",2);check(HasSkillCare&&Tab=="材料"&&FarmPracticeTests.Xml(G.S)==saved,"crop cancellation preserves skill origin without spending");
        GuideClick(GuidePrimary());SkillCarePreview(root,"grain-plant-again",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-planted",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-growing",2);owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(100),false,true,false);UIPreview(root,"skill-care-grain-real-harvest",484,501);
        check(G.S.Grain>=3&&HasSkillCare&&G.S.Skills[4]==0,"real farm harvest retains goal and never equips");GuideClick(GuidePrimary());SkillCarePreview(root,"grain-returned",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-trained",2);GuideClick(GuidePrimary());SkillCarePreview(root,"grain-learned",2);check(Tab=="技能"&&lifeSkill==1&&G.SkillUnlocked(4,1)&&G.S.Skills[4]==0,"crop recovery completes at the original skill preview");

        SkillCareFixture(1);G.S.Mint=0;SkillCarePreview(root,"mint-start",2);GuideClick(GuidePrimary());SkillCarePreview(root,"mint-training",2);LaterExpander("life-mint-details").IsExpanded=true;UIPreview(root,"skill-care-mint-option",484,501);GuideClick(UIAction("去看看薄荷田"));SkillCarePreview(root,"mint-source",2);check(HasSkillCare&&Tab=="材料","optional snack shortage keeps skill goal");G.S.Mint=1;RefreshCounters();GuideClick(GuidePrimary());SkillCarePreview(root,"mint-ready",2);check(Tab=="点心"&&G.S.Mint==1,"material return only previews the snack");GuideClick(GuidePrimary());SkillCarePreview(root,"mint-eaten",2);GuideClick(GuidePrimary());SkillCarePreview(root,"mint-learned",2);check(lifeSkill==1&&G.S.Mint==0&&G.S.Grain==9&&G.S.Xp[1]==36&&G.S.Skills[1]==0,"explicit snack charges one mint then returns to exact skill without equipping");

        SkillCareFixture(2);SkillCarePreview(root,"cancel-keyboard");GuideClick(GuidePrimary());SkillCarePreview(root,"cancel-training");var oldBack=UIAction("回去看这招");saved=FarmPracticeTests.Xml(G.S);KeyPress(Key.Escape);SkillCarePreview(root,"cancel-keyboard-back");oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="技能"&&lifeSkill==1&&!HasSkillCare&&FarmPracticeTests.Xml(G.S)==saved,"Escape and stale visible return preserve exact source and no costs");
        GuideClick(GuidePrimary());SkillCarePreview(root,"independent-before");GuideClick(UIAction("伙伴"));SkillCarePreview(root,"independent-care");OpenLife("照料");SkillCarePreview(root,"independent-training");check(!HasSkillCare&&UIAction("返回伙伴")!=null,"independent top navigation clears temporary goal");
        SkillCareFixture(2);SkillCarePreview(root,"change-before");GuideClick(GuidePrimary());SkillCarePreview(root,"change-training");oldBack=UIAction("回去看这招");G.ChooseCourtyardCompanion(3);SkillCarePreview(root,"change-companion");saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!HasSkillCare&&G.CourtyardCompanion==3&&Tab=="照料"&&FarmPracticeTests.Xml(G.S)==saved,"changed companion clears goal; old return cannot switch targets");
        SkillCareFixture(2);SkillCarePreview(root,"pause-before");GuideClick(GuidePrimary());SkillCarePreview(root,"pause-training");oldBack=UIAction("回去看这招");G.EnsureJourneyOffers();check(G.BeginJourney(0,G.S.BlessingOffers[0]),"paused fixture starts real trip");G.ToggleJourneyPause();SkillCarePreview(root,"paused");saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!HasSkillCare&&G.R.JourneyPaused&&FarmPracticeTests.Xml(G.S)==saved,"travel clears home goal and stale return cannot resume pause");
        SkillCareFixture(2);SkillCarePreview(root,"external-growth");var locked=GuidePrimary();G.S.Xp[2]=20;saved=FarmPracticeTests.Xml(G.S);locked.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="技能"&&FarmPracticeTests.Xml(G.S)==saved&&!HasSkillCare,"outdated locked action cannot train after the skill unlock condition changes");

        ChapterGuideFixture(1);SkillCarePreview(root,"chapter-start");GuideClick(GuidePrimary());SkillCarePreview(root,"chapter-friend");GuideClick(GuidePrimary());SkillCarePreview(root,"chapter-care");OpenLife("技能");lifeSkill=1;SkillCarePreview(root,"chapter-skill");GuideClick(GuidePrimary());SkillCarePreview(root,"chapter-training");GuideClick(GuidePrimary());SkillCarePreview(root,"chapter-trained");GuideClick(GuidePrimary());SkillCarePreview(root,"chapter-learned");check(HasChapterReturn&&lifeSkill==1&&!HasSkillCare,"skill detour retains the independent chapter return");GuideClick(ChapterBack());SkillCarePreview(root,"chapter-back");check(lifeChapter==1&&G.ChapterDone(1)&&!G.S.ChapterClaimed[1]&&G.S.Skills[2]==0,"chapter completion neither equips skill nor claims gift");

        SkillCareFixture(2);G.S.ReadingSize=2;string path=Path.Combine(root,"skill-care-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.OpenLife("技能");panel.lifeSkill=1;panel.SkillCarePreview(root,"normal-start",2);string disk=File.ReadAllText(path);
        panel.GuideClick(panel.GuidePrimary());panel.SkillCarePreview(root,"normal-source",2);panel.GuideClick(panel.UIAction("回去看这招"));panel.SkillCarePreview(root,"normal-cancelled",2);check(File.ReadAllText(path)==disk,"normal preview and return write nothing");panel.GuideClick(panel.GuidePrimary());panel.SkillCarePreview(root,"normal-training",2);panel.GuideClick(panel.GuidePrimary());panel.SkillCarePreview(root,"normal-trained",2);panel.GuideClick(panel.GuidePrimary());panel.SkillCarePreview(root,"normal-learned",2);
        var loaded=new FarmGame(path);check(loaded.S.Xp[2]==30&&loaded.S.Grain==6&&loaded.S.Skills[2]==0,"normal reload retains confirmed growth and charge without equipping");var reload=new FarmWindow(loaded,true);reload.Panel=new FarmPanel(reload);reload.Panel.OpenLife("照料");reload.Panel.SkillCarePreview(root,"normal-reopened",2);check(!reload.Panel.HasSkillCare,"temporary navigation is not invented on reload");
        SkillCareFixture(2);G.S.Grain=0;SkillCarePreview(root,"reminder-start");GuideClick(GuidePrimary());SkillCarePreview(root,"reminder-training");GuideClick(GuidePrimary());SkillCarePreview(root,"reminder-source");GuideClick(UIAction("回小院，记着这件事"));SkillCarePreview(root,"reminder-home");check(!HasSkillCare&&G.S.SmallReminder!=null&&G.S.SmallReminder.Activity=="train"&&G.S.SmallReminder.Pet==2,"explicit reminder preserves original training activity while leaving temporary skill navigation");
        SkillCareFixture(2);SkillCarePreview(root,"reset-before");GuideClick(GuidePrimary());SkillCarePreview(root,"reset-training");oldBack=UIAction("回去看这招");G.ResetGuideTest();ResetGuideView();SkillCarePreview(root,"fresh");saved=FarmPracticeTests.Xml(G.S);oldBack.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!HasSkillCare&&G.SavePath==null&&FarmPracticeTests.Xml(G.S)==saved,"fresh memory-only guide rejects old goal return");
        check(!IsVisible&&!owner.IsVisible&&new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero&&new WindowInteropHelper(reload).Handle==IntPtr.Zero,"skill checks use no visible or native windows");
        File.WriteAllText(Path.Combine(root,"skill-care-regression.txt"),"PASS: all five skills and 100/115/130% actual viewport; truthful locked XP and 0-to-18-to-36 learning; exact return after explicit training or optional snack; no preview costs, auto-equipment or duplicate return action; actual grain harvest/crop cancellation; keyboard, held Enter and stale controls; independent navigation, care change, pause and reset; chapter origin and explicit reminder preserved; isolated normal browsing and saved growth without equipment; no persistent temporary navigation. Detached WPF, not physical input or player understanding tests.");
    }
}
