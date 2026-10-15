using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void PreparationPreview(string root,string name,int size=0){ReadingPreview(root,"preparation-"+name,size,true);GuideFits(GuidePrimary());}
    private void PreparationFixture()
    {
        EverydayFixture();G.S.Evolved=true;G.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();G.S.Xp=Enumerable.Repeat(110,FarmGame.PetCount).ToArray();G.S.Skills[0]=1;G.S.EquippedRelics=new[]{0,3};G.S.BlessingOffers=new[]{0,1,2};G.PlanJourneyPicnic(0,1,1);journeyDifficulty=1;journeyDepth=1;Go("冒险");
    }
    internal void RenderPreparationChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        PreparationFixture();PreparationPreview(root,"departure");string before=FarmPracticeTests.Xml(G.S);var oldStart=GuidePrimary();
        LaterExpander("repeat-departure-details").IsExpanded=true;UIPreview(root,"preparation-menu",484,501);GuideClick(UIAction("看看这次准备"));PreparationPreview(root,"route");
        check(Tab=="准备小卡"&&WantsGentleNarrow&&preparationCard==0&&PreparationCards().Length==7&&FarmPracticeTests.Xml(G.S)==before,"optional review opens actual seven-card loadout without any save, reroll, spending or departure");oldStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome,"pre-review start control cannot depart after navigation");
        for(int index=0;index<7;index++){
            PreparationPreview(root,"card-"+index);check(preparationCard==index&&FarmPracticeTests.Xml(G.S)==before,"each review card is strictly read-only");
            if(index>=1&&index<=3){int pet=G.S.Team[index-1];check(GuideWords().Contains(G.Name(pet))&&GuideWords().Contains(FarmGame.SkillNames[pet][G.S.Skills[pet]])&&!GuideWords().Contains("成长 +"),"companion card shows actual standing order and equipped skill");}
            if(index==4)check(GuideWords().Contains("莓果 2")&&GuideWords().Contains("麦穗 2")&&G.S.PlannedPicnic==0,"picnic review shows actual selected recipe cost without cooking");
            if(index>=5)check(GuideWords().Contains(FarmGame.RelicNames[G.S.EquippedRelics[index-5]]),"relic card shows equipment in its real bag slot");
            var next=GuidePrimary();GuideClick(next);Render();UIPreview(root,"preparation-next",484,501);int currentCard=preparationCard;next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(preparationCard==currentCard&&G.AtHome,"old next button cannot skip another card or depart");
        }
        PreparationPreview(root,"reviewed-departure");check(SmallRepeatDeparture&&FarmPracticeTests.Xml(G.S)==before,"last review action returns to explicit departure with no mutation");
        int berries=G.S.Berries,grain=G.S.Grain;GuideClick(GuidePrimary());check(G.R.Difficulty==1&&G.R.Depth==1&&G.R.Picnic==0&&G.S.Berries==berries-2&&G.S.Grain==grain-2,"only final departure charges selected meal once on reviewed route");
        G.ToggleJourneyPause();Go("准备小卡");PreparationPreview(root,"away-paused");before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());check(G.R.JourneyPaused&&Tab=="冒险"&&FarmPracticeTests.Xml(G.S)==before,"review opened while travelling routes to current trip without changing pause");

        EverydayFixture();OpenPreparationCards();PreparationPreview(root,"basic-route");check(PreparationCards().SequenceEqual(new[]{0,1,2,3})&&!GuideWords().Contains("便当")&&!GuideWords().Contains("信物"),"empty meal and bag do not introduce unavailable systems or filler cards");
        var stale=GuidePrimary();GuideClick(stale);PreparationPreview(root,"basic-friend");KeyPress(Key.Escape);PreparationPreview(root,"back-to-preparation");before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="冒险"&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"Escape leaves optional review and stale navigation is inert");
        OpenPreparationCards();PreparationPreview(root,"keyboard");ResetPanelInput();Render();UIPreview(root,"preparation-keyboard",484,501);KeyPress(Key.Tab);KeyPress(Key.Enter);KeyPress(Key.Enter,ModifierKeys.None,true);check(preparationCard==1&&G.AtHome,"held Enter advances one read-only card");
        G.S.Team=new[]{4,1,3};G.S.Xp[4]=110;G.S.Skills[4]=1;Go("农场");OpenPreparationCards();PreparationPreview(root,"changed-route");GuideClick(GuidePrimary());PreparationPreview(root,"changed-friend");check(GuideWords().Contains(G.Name(4))&&GuideWords().Contains(FarmGame.SkillNames[4][1]),"reopened cards read current equipment rather than a stale snapshot");

        for(int size=0;size<3;size++){
            PreparationFixture();G.S.GardenUnlocked=true;G.S.PlannedPicnic=5;G.S.Berries=G.S.Grain=G.S.Mint=0;OpenPreparationCards();
            for(int index=0;index<7;index++){
                PreparationPreview(root,"font-card-"+index,size);if(index>0)GuideFits(UIAction("上一张"));if(index<6)GuideFits(UIAction("先回出发准备"));
                if(index==4){check(GuideWords().Contains("种子不在这队"),"review states when a character-specific meal cannot work with the actual party");var surface=Content;owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(85),false,true,false);check(Object.ReferenceEquals(surface,Content)&&GuideWords().Contains("现在有：莓果 "+G.S.Berries)&&G.S.PlannedPicnic==5&&G.AtHome,"review inventory updates with real harvest without rebuilding cards or cooking");}
                GuideClick(GuidePrimary());
            }
            PreparationPreview(root,"font-final",size);check(G.AtHome&&G.S.PlannedPicnic==5,"review never substitutes for confirmation at any reading size");
        }

        OpenLife("便当");PreparationPreview(root,"meal-without-friend");check(GuideWords().Contains("种子不在这队")&&G.S.PlannedPicnic==5,"recipe preview explains missing companion without silently replacing chosen meal");G.S.Team=new[]{4,1,3};PreparationPreview(root,"meal-with-friend");check(GuideWords().Contains(FarmGame.PicnicDetails[5])&&!GuideWords().Contains("暂时用不上"),"meal explanation follows actual party when the required companion joins");
        PreparationFixture();G.S.Grain=0;var remembered=new FarmSmallReminder{Activity="picnic",Recipe=1,Region=0,Depth=1};G.RememberSmallActivity(remembered);OpenSmallResources(remembered);PreparationPreview(root,"reminder-source");GuideClick(GuidePrimary());PreparationPreview(root,"reminder-recipe");
        check(journeyDifficulty==0&&journeyDepth==1&&G.S.PlannedRegion==1&&G.S.PlannedDepth==1,"reading old reminder temporarily restores its context without changing confirmed destination");
        berries=G.S.Berries;grain=G.S.Grain;int seed=G.S.Seed;GuideClick(GuidePrimary());PreparationPreview(root,"reminder-confirmed");
        check(G.AtHome&&G.S.PlannedRegion==0&&G.S.PlannedDepth==1&&G.S.PlannedPicnic==1&&G.S.Berries==berries&&G.S.Grain==grain&&G.S.Seed==seed,"recipe confirmation saves its shown route and meal together without costs or reroll");
        string path=Path.Combine(root,"preparation-normal.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var loaded=new FarmGame(path);var window=new FarmWindow(loaded,true);window.Panel=new FarmPanel(window);var panel=window.Panel;panel.PrepareFromHome();panel.PreparationPreview(root,"reloaded-recipe");
        check(panel.journeyDifficulty==0&&panel.journeyDepth==1&&loaded.S.PlannedPicnic==1&&loaded.S.Grain==grain,"normal reload keeps the confirmed reminder route with its recipe");
        panel.OpenPreparationCards();panel.PreparationPreview(root,"normal-review");before=FarmPracticeTests.Xml(loaded.S);string disk=File.ReadAllText(path);panel.GuideClick(panel.GuidePrimary());panel.PreparationPreview(root,"normal-friend");check(FarmPracticeTests.Xml(loaded.S)==before&&File.ReadAllText(path)==disk,"normal review has no save-file writes");
        check(new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"normal review validation creates no native windows");
        before=FarmPracticeTests.Xml(loaded.S);check(!loaded.PlanJourneyPicnic(99,0,0)&&!loaded.PlanJourneyPicnic(0,9,0)&&FarmPracticeTests.Xml(loaded.S)==before,"invalid combined plan cannot partially change route or recipe");

        LaterJourneyFixture();G.AdvanceJourney(DateTime.UtcNow.AddHours(1));G.Home();G.S.Evolved=true;SelectJourneyDestination(1,0);OpenLife("旅途记忆");PreparationPreview(root,"memory");LaterExpander("life-memory-details").IsExpanded=true;UIPreview(root,"preparation-memory-details",484,501);before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("再准备去一次"));PreparationPreview(root,"memory-confirmed");
        var expected=XDocument.Parse(before);expected.Root.Element("PlannedRegion").Value="0";check(G.S.PlannedRegion==0&&G.S.PlannedDepth==0&&G.AtHome&&XNode.DeepEquals(expected,XDocument.Parse(FarmPracticeTests.Xml(G.S))),"journal retry saves only the explicitly selected route without replaying rewards");
        XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);loaded=new FarmGame(path);check(loaded.S.PlannedRegion==0&&loaded.S.PlannedDepth==0,"journal route survives reload");
        HomecomingRoute(0,1);check(G.S.PlannedRegion==0&&G.S.PlannedDepth==1,"return arrangement route uses same saved planning contract");
        G.S.BlessingOffers=new[]{0,1,2};G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(5).Ticks;var trial=G.Practice(G.CurrentPracticeDraft(),1,0,0);check(G.AdoptPractice(trial)&&G.S.PlannedRegion==1&&G.S.PlannedDepth==0&&G.AtHome,"explicit practice adoption remembers its route without travel");
        G.BeginJourney(1,0);before=FarmPracticeTests.Xml(G.S);check(!G.PlanJourneyPicnic(-1,0,0)&&FarmPracticeTests.Xml(G.S)==before,"active trip rejects combined plan without changing resources or current run");
        EverydayFixture();OpenPreparationCards();PreparationPreview(root,"before-reset");stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();PreparationPreview(root,"reset");before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.SavePath==null&&FarmPracticeTests.Xml(G.S)==before,"fresh guide rejects old review controls without touching production saves");
        CaptureUXBaseline(root);
        File.WriteAllText(Path.Combine(root,"preparation-regression.txt"),"PASS: optional read-only route/three companions/equipped skills/selected meal/equipped relic cards; hidden unused concepts; actual costs and live inventory; no saves, rerolls, resources or departure while browsing; explicit final departure charges once; Escape, held Enter, stale controls, current equipment and reset; font 100/115/130 minimum-window actions; exact route+recipe confirmation after reminder; normal reload; journal/return/practice route persistence; invalid plan atomic rejection and active trip lock; detached UI and fresh in-memory tutorial.");
    }
    private void CaptureUXBaseline(string root)
    {
        var copy=new List<string>();G.ResetGuideTest();ResetGuideView();copy.Add("SCREEN: desktop-start\n点这里，认识泡泡");
        Action<string> capture=name=>{PreparationPreview(root,"ux-"+name);copy.Add("SCREEN: "+name+"\n"+String.Join("\n",UIChildren<TextBlock>(body).Select(t=>t.Text)));};
        capture("welcome");GuideClick(GuidePrimary());capture("first-feed");GuideClick(GuidePrimary());capture("farm-intro");GuideClick(GuidePrimary());capture("growing");
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(12));capture("harvest");GuideClick(GuidePrimary());capture("basics-complete");
        FirstOutingFixture(root,"ux-first-outing");capture("first-companions");GuideClick(GuidePrimary());capture("first-destination");
        File.WriteAllText(Path.Combine(root,"ux-baseline-copy.txt"),String.Join("\n\n",copy));
    }
}
