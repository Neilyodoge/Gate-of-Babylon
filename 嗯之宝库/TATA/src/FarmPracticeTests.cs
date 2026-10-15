using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public static class FarmPracticeTests
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception("Practice: "+label);}
    public static string Xml<T>(T value){using(var text=new StringWriter()){new XmlSerializer(typeof(T)).Serialize(text,value);return text.ToString();}}
    private static FarmGame New(string root){var g=new FarmGame(Path.Combine(root,"practice-"+Guid.NewGuid().ToString("N")+".xml"));g.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;g.S.BlessingOffers=new[]{1,0,2};g.Save();return g;}
    public static string Run(string root)
    {
        var g=New(root);g.S.Team=new[]{2,0,3};g.S.DesktopHidden=true;g.S.DisplayPet=4;g.Save();var draft=g.PracticePreset(0);string original=Xml(g.S),disk=File.ReadAllText(g.SavePath);int files=Directory.GetFiles(root).Length;
        var result=g.Practice(draft,0,0,1);Check(result!=null&&result.Current.Rounds.Length<=3&&result.Trial.Rounds.Length<=3,"both trials run at most three rounds");
        Check(Xml(g.S)==original&&File.ReadAllText(g.SavePath)==disk&&Directory.GetFiles(root).Length==files,"practice performs no writes and changes no source state, including rewards and offers");
        Check(result.Current.Rounds[0].Combos[0]==0&&result.Trial.Rounds[0].Combos[0]==1,"changing order activates the wet combo in round one");
        draft.Team[0]=4;Check(result.Draft.Team[0]==0,"comparison holds a detached draft");int initial=result.Trial.Rounds[0].Direct;result.Trial.Final.Direct++;Check(result.Trial.Rounds.Length==1||result.Trial.Rounds[0].Direct==initial,"round snapshots are independent");result=g.Practice(g.PracticePreset(0),0,0,1);
        var actual=New(root);actual.S.Team=new[]{0,2,3};actual.BeginJourney(0,1);actual.RouteChoice(0);for(int i=0;i<3&&actual.R.Phase=="battle";i++)actual.Step();Check(Xml(actual.CurrentBattleNotes().Last())==Xml(result.Trial.Final),"practice exactly matches real opening battle rules");
        var duplicate=g.CurrentPracticeDraft();duplicate.Team=new[]{0,0,3};Check(g.Practice(duplicate,0,0,1)==null&&g.Practice(g.CurrentPracticeDraft(),1,0,1)==null&&g.Practice(g.CurrentPracticeDraft(),0,1,1)==null&&g.Practice(g.CurrentPracticeDraft(),0,0,5)==null,"invalid teams, locked regions/depths and unavailable blessings rejected");
        var locked=g.CurrentPracticeDraft();locked.Skills[3]=1;Check(g.Practice(locked,0,0,1)==null&&g.PracticePreset(1)==null&&g.PracticePreset(2)==null&&g.PracticePreset(4)==null,"unearned secondary skills cannot be practiced");
        g.S.Berries++;Check(g.PracticeIsCurrent(result),"passive resource growth does not stale battle comparisons");g.S.Xp[0]=20;string changed=Xml(g.S);Check(!g.AdoptPractice(result)&&Xml(g.S)==changed,"growth changes invalidate adoption without partial changes");
        var apply=New(root);apply.S.FarmCaretaker=1;apply.S.Xp=Enumerable.Repeat(55,5).ToArray();apply.S.Skills[2]=1;apply.S.EquippedRelics=new[]{-1,-1};apply.S.DisplayPet=4;apply.S.DesktopHidden=true;apply.SaveParty(0);var plan=apply.Practice(apply.PracticePreset(4),0,0,2);int berries=apply.S.Berries,grain=apply.S.Grain,crystals=apply.S.Crystals,seed=apply.S.Seed;string saved=Xml(apply.S.SavedParties);int helper=apply.PracticeCaretaker(plan.Draft);
        Check(apply.AdoptPractice(plan)&&apply.S.Team.SequenceEqual(new[]{1,4,3})&&apply.S.Skills[4]==1&&apply.S.Skills[2]==1&&apply.Caretaker==helper,"adoption applies only active team skills and resolves farm helper");
        Check(apply.AtHome&&apply.S.Berries==berries&&apply.S.Grain==grain&&apply.S.Crystals==crystals&&apply.S.Seed==seed&&apply.S.JourneyMemories.Length==0&&apply.S.ExpeditionClears.All(x=>x==0)&&Xml(apply.S.SavedParties)==saved&&apply.S.DisplayPet==4&&apply.S.DesktopHidden,"adoption spends no resources, starts no trip and preserves saved parties and desktop");
        Check(new FarmGame(apply.SavePath).S.Team.SequenceEqual(apply.S.Team),"adopted team persists through reload");apply.BeginJourney(0,1);apply.ToggleJourneyPause();string away=Xml(apply.S);Check(apply.Practice(apply.CurrentPracticeDraft(),0,0,1)==null&&!apply.AdoptPractice(plan)&&Xml(apply.S)==away,"active and paused journey cannot be changed by practice");
        var matrix=New(root);matrix.S.Xp=Enumerable.Repeat(110,5).ToArray();matrix.S.Evolved=true;matrix.S.SeedStage=3;matrix.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();matrix.S.EquippedRelics=new[]{3,5};
        for(int route=0;route<6;route++)for(int preset=0;preset<5;preset++){
            int blessing=(route+preset)%6;matrix.S.BlessingOffers=new[]{blessing,(blessing+1)%6,(blessing+2)%6};string before=Xml(matrix.S);var check=matrix.Practice(matrix.PracticePreset(preset),route/3,route%3,blessing);Check(before==Xml(matrix.S),"matrix trial leaves source untouched");
            foreach(var side in new[]{check.Current,check.Trial})foreach(var note in side.Rounds){Check(note.Rounds>=1&&note.Rounds<=3&&note.Direct+note.Burn+note.Poison==note.EnemyStart+note.EnemyHealing-note.EnemyRemaining,"all route and combo samples conserve enemy health");Check(note.StartHp.Sum()+note.Healing-note.EndHp.Sum()==note.Taken.Sum(),"all samples conserve party health");}
        }
        return "PASS: zero source-state and file writes; exact real-battle equivalence; three-round cap and early clearing; same-blessing comparison; swap-order combo difference; detached snapshots; unavailable route/skill/blessing guards; stale-adoption guards; explicit team adoption, helper and save persistence; no rewards, history, progression or desktop changes; paused-trip locks; health conservation across 30 route/build samples.";
    }
}

public partial class FarmPanel
{
    private void PracticeRender(string root,string name,double width=924,double height=721){Render();UIPreview(root,name,width,height);}
    internal void RenderPracticeChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;G.S.BlessingOffers=new[]{1,0,2};G.S.Team=new[]{2,0,3};G.S.DesktopHidden=true;G.S.DisplayPet=4;G.Save();Tab="配队";PracticeRender(root,"practice-entry");UIAction("搭配试练 · 免费试打").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-editor-small",764,571);
        UIVisible(UIAction("开始试练 · 免费"),"practice button visible in minimum window");check(Tab=="试练"&&!UIAction("火毒催化").IsEnabled,"workshop opens practice and locks unavailable skills");string original=FarmPracticeTests.Xml(G.S);
        var member=UIChildren<ComboBox>((DependencyObject)Content).First(c=>AutomationProperties.GetName(c)=="试练第 1 位伙伴");member.SelectedIndex=0;PracticeRender(root,"practice-swapped");check(practiceDraft.Team.SequenceEqual(new[]{0,2,3})&&FarmPracticeTests.Xml(G.S)==original,"editor swaps duplicate pets only in draft");
        UIAction("开始试练 · 免费").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-comparison");PracticeRender(root,"practice-comparison-small",764,571);UIVisible(UIAction("采用搭配并去准备"),"practice adopt visible at minimum size");UIVisible(UIAction("再调整一下"),"practice revise visible at minimum size");check(practiceResult!=null&&FarmPracticeTests.Xml(G.S)==original,"trial result leaves real game unchanged");
        UIAction("第 1 回合").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-first-round");check(practiceTurn==1&&practiceResult.Current.Rounds[0].Combos[0]==0&&practiceResult.Trial.Rounds[0].Combos[0]==1,"round selector exposes actual combo difference");
        var details=UIChildren<Expander>((DependencyObject)Content).First();details.IsExpanded=true;UIPreview(root,"practice-actions");check(UIChildren<TextBlock>(details).All(t=>!t.Text.Contains("已自动入库")),"practice log never claims real rewards");
        UIAction("再调整一下").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-revise");check(practiceResult==null&&practiceDraft.Team.SequenceEqual(new[]{0,2,3})&&G.S.Team.SequenceEqual(new[]{2,0,3}),"revise retains draft without adoption");UIAction("开始试练 · 免费").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));G.S.Xp[0]=20;PracticeRender(root,"practice-stale");check(!UIAction("采用搭配并去准备").IsEnabled,"growth change disables old comparison adoption");
        UIAction("再调整一下").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-rerun");UIAction("开始试练 · 免费").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-ready");UIAction("采用搭配并去准备").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-adopted-small",764,571);check(Tab=="冒险"&&G.AtHome&&G.S.Team.SequenceEqual(new[]{0,2,3})&&selectedBlessing==1&&G.S.BlessingOffers.SequenceEqual(new[]{1,0,2})&&G.S.JourneyMemories.Length==0&&G.S.DesktopHidden&&G.S.DisplayPet==4,"adoption stops at preparation with chosen blessing and desktop preserved");UIVisible(UIAction("出发 · 自动走完整趟"),"real departure remains visible after practice adoption");
        G.S.Xp=Enumerable.Repeat(55,5).ToArray();OpenPractice();PracticeRender(root,"practice-unlocked");UIAction("火毒催化").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PracticeRender(root,"practice-fire-poison");var skill=UIChildren<ComboBox>((DependencyObject)Content).First(c=>AutomationProperties.GetName(c)=="试练暗影种子技能");check(skill.SelectedIndex==1&&G.S.Team.SequenceEqual(new[]{0,2,3}),"fire poison preset changes draft only");skill.SelectedIndex=0;PracticeRender(root,"practice-skill-change");check(practiceDraft.Skills[4]==0&&G.S.Skills[4]==0,"skill picker changes draft");
        G.BeginJourney(0,1);G.ToggleJourneyPause();Tab="试练";PracticeRender(root,"practice-away");check(G.R.JourneyPaused&&UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text.Contains("等伙伴回来")),"practice away guard preserves pause");check(!IsVisible&&!owner.IsVisible,"practice renders without native windows");
        File.WriteAllText(Path.Combine(root,"practice-ui-regression.txt"),"PASS: workshop and adventure integration, isolated draft editing and preset gates, real opening-round comparison, minimum-window controls, round selection and sanitized logs, revision, stale results, explicit adoption and real departure, unlocked fire-poison preset, active-trip guard. No native windows or production save access.");
    }
}
