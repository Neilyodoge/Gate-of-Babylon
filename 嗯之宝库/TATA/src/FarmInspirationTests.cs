using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;

public static class FarmInspirationTests
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception("Inspiration: "+label);}
    private static FarmGame New(string root){var g=new FarmGame(Path.Combine(root,"inspiration-"+Guid.NewGuid().ToString("N")+".xml"));g.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;g.S.BlessingOffers=new[]{0,1,2};return g;}
    private static void FirstChoice(FarmGame g){Check(g.BeginJourney(0,0),"fixture departs");g.RouteChoice(0);int turns=0;while(g.R.Phase=="battle"&&turns++<60)g.Step();Check(g.R.Phase=="inspiration","first victory offers a choice");}
    private static FarmGame EffectGame(string root,int id,bool selected)
    {
        var g=New(root);g.S.Xp=Enumerable.Repeat(20,5).ToArray();g.S.Team=id==4||id==10?new[]{1,4,3}:id==9?new[]{0,4,3}:new[]{0,2,3};if(id==10)g.S.Skills[4]=1;g.BeginJourney(0,0);
        if(selected){g.R.Phase="inspiration";g.R.InspirationOffers=new[]{id};Check(g.ChooseInspiration(id,0),"effect fixture acquires ability "+id);}else g.R.Node=1;
        g.RouteChoice(0);g.R.EnemyCount=2;g.R.EnemyHp=new[]{1000,1000,0};g.R.EnemyMax=new[]{1000,1000,0};g.R.EnemyKind=new int[3];g.R.ActiveBattle.EnemyStart=2000;g.R.Hp[g.S.Team[0]]=20;g.R.ActiveBattle.StartHp[0]=20;return g;
    }
    private static void Effects(string root)
    {
        for(int id=0;id<FarmGame.InspirationCount;id++){
            var baseline=EffectGame(root,id,false);var enhanced=EffectGame(root,id,true);
            if(id==8)foreach(var g in new[]{baseline,enhanced}){g.R.EnemyCount=1;g.R.EnemyHp=new[]{1,0,0};g.R.EnemyMax=new[]{1,0,0};g.R.ActiveBattle.EnemyStart=1;}
            int rounds=id==7?3:1;for(int i=0;i<rounds;i++){baseline.Step();enhanced.Step();}var a=baseline.CurrentBattleNotes().Last();var b=enhanced.CurrentBattleNotes().Last();
            Check(enhanced.R.InspirationTriggers[id]>0,"ability produces a trigger "+id);
            if(id==0)Check(b.Direct-a.Direct==5,"wet attack gains five actual damage");
            if(id==1)Check(b.Direct-a.Direct==7,"lightning jump gains seven actual damage");
            if(id==2||id==11)Check(b.Blocked.Sum()+enhanced.R.Shield.Sum()>a.Blocked.Sum()+baseline.R.Shield.Sum(),"shield ability adds actual protection "+id);
            if(id==3)Check(b.Healing-a.Healing==5,"shell gains five actual healing");
            if(id==4)Check(b.Burn-a.Burn==3,"fire adds three actual ongoing damage");
            if(id==5)Check(b.MaxHp.Sum()-a.MaxHp.Sum()==36,"whole party gains twelve max hp each");
            if(id==6)Check(b.Direct-a.Direct==15,"opening grants five attack per living pet");
            if(id==7)Check(b.Direct-a.Direct==10,"third-round team attack adds ten damage");
            if(id==8)Check(b.Healing-a.Healing==10,"kill recovery is real and excluded from post-victory recovery");
            if(id==9)Check(b.Poison-a.Poison==2,"seed applies two extra poison");
            if(id==10)Check(b.Direct-a.Direct==4,"two consumed poison stacks gain two damage each");
            Check(b.Direct+b.Burn+b.Poison==b.EnemyStart+b.EnemyHealing-b.EnemyRemaining&&b.StartHp.Sum()+b.Healing-b.EndHp.Sum()==b.Taken.Sum(),"new effects conserve battle health "+id);
        }
    }
    public static string Run(string root)
    {
        var g=New(root);Check(g.S.InspirationStyle==0&&g.S.DiscoveredInspirations.All(v=>!v)&&!g.SetInspirationStyle(3),"safe defaults and invalid preference");g.SetInspirationStyle(2);g.SaveParty(0);g.SetInspirationStyle(1);g.LoadParty(0);Check(g.S.InspirationStyle==2,"saved party restores travel preference");g.S.Xp=Enumerable.Repeat(55,5).ToArray();FirstChoice(g);
        Check(g.R.InspirationsEnabled&&g.R.InspirationStyle==2&&g.R.InspirationOffers.Length==3&&g.R.InspirationOffers.Distinct().Count()==3&&g.R.InspirationOffers.All(g.InspirationEligible)&&g.R.RewardFertilizer==1&&g.S.Xp.All(x=>x==55),"new journey offers three relevant abilities after normal rewards");
        Check(!g.SetInspirationStyle(0),"preference locked during trip");g.ToggleJourneyPause();g.Save();int[] offers=(int[])g.R.InspirationOffers.Clone();int seed=g.S.Seed;var reload=new FarmGame(g.SavePath);Check(reload.R.JourneyPaused&&reload.R.Phase=="inspiration"&&reload.R.InspirationOffers.SequenceEqual(offers)&&reload.S.Seed==seed,"pending choice persists without reroll or unpausing");reload.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(reload.R.InspirationPicks.Length==0,"paused choice never auto selects");
        int chosen=reload.RecommendedInspiration,fertilizer=reload.S.Fertilizer;Check(reload.ChooseInspiration(chosen,0)&&reload.R.JourneyPaused&&reload.R.Node==1&&reload.R.InspirationPicks.Single().Manual&&reload.S.DiscoveredInspirations[chosen]&&reload.S.Fertilizer==fertilizer&&!reload.ChooseInspiration(chosen,0),"manual choice is atomic, once only and preserves pause");
        reload.ToggleJourneyPause();reload.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(reload.R.Outcome=="victory"&&reload.R.InspirationPicks.Select(p=>p.Station).SequenceEqual(new[]{0,1,3})&&reload.R.Inspirations.Sum()==3&&reload.R.Inspirations.All(x=>x<=2)&&reload.R.InspirationPicks.Skip(1).All(p=>!p.Manual)&&reload.S.Fertilizer==7,"full trip autonomously chooses three times without changing base rewards");
        var memory=reload.S.JourneyMemories.Single();Check(memory.InspirationStyle==2&&memory.Inspirations.Length==3&&memory.Battles.Length==4&&memory.Battles[0].Inspirations.Sum()==0&&memory.Battles.Last().Inspirations.Sum()==3,"history and per-fight snapshots show only acquired abilities");string reason=memory.Inspirations[0].Reason;reload.R.InspirationPicks[0].Reason="changed";Check(memory.Inspirations[0].Reason==reason,"history selection is detached");reload.Home();Check(reload.R.Perks.All(x=>x==0)&&reload.R.Inspirations.All(x=>x==0)&&reload.S.Xp.All(x=>x==55)&&reload.S.DiscoveredInspirations.Any(x=>x),"temporary growth clears at home while collection remains");
        var preference=New(root);preference.BeginJourney(0,0);preference.R.Phase="inspiration";preference.R.InspirationOffers=new[]{5,6,7};foreach(int p in preference.S.Team)preference.R.Hp[p]=1;
        preference.R.InspirationStyle=0;Check(preference.RecommendedInspiration==5&&preference.InspirationReason(5).Contains("体力偏低"),"balanced preference protects a weak party");preference.R.InspirationStyle=1;Check(preference.RecommendedInspiration==6,"attack preference favors opening damage");preference.R.InspirationStyle=2;Check(preference.RecommendedInspiration==5,"safe preference favors maximum health");
        preference.R.InspirationOffers=new[]{5};preference.ChooseInspiration(5,0);preference.R.Phase="inspiration";preference.R.InspirationOffers=new[]{5};Check(!preference.ChooseInspiration(5,0)&&preference.ChooseInspiration(5,1),"stale station cannot select a later choice");preference.R.Phase="inspiration";preference.R.Node=3;preference.R.InspirationOffers=new[]{5};Check(!preference.ChooseInspiration(5,3)&&preference.R.Inspirations[5]==2,"stack cap cannot be bypassed");
        var offline=New(root);offline.S.Xp=Enumerable.Repeat(55,5).ToArray();FirstChoice(offline);offline.R.NextJourneyTicks=DateTime.UtcNow.AddHours(-1).Ticks;offline.Save();var settled=new FarmGame(offline.SavePath);Check(settled.R.Outcome=="victory"&&settled.R.InspirationPicks.Length==3&&settled.S.JourneyMemories.Length==1,"offline choice continues through current journey");var settledAgain=new FarmGame(offline.SavePath);Check(settledAgain.S.Fertilizer==settled.S.Fertilizer&&settledAgain.S.JourneyMemories.Length==1,"offline rewards and choices are idempotent");
        var old=New(root);old.S.Xp=Enumerable.Repeat(55,5).ToArray();old.BeginJourney(0,0);old.RouteChoice(0);old.ToggleJourneyPause();old.Save();var xml=XDocument.Load(old.SavePath);foreach(string field in new[]{"InspirationStyle","DiscoveredInspirations"})xml.Root.Element(field).Remove();foreach(string field in new[]{"InspirationsEnabled","InspirationStyle","Inspirations","InspirationTriggers","InspirationOffers","InspirationPicks"})xml.Root.Element("Run").Element(field).Remove();string oldPath=Path.Combine(root,"pre-inspiration-save.xml");xml.Save(oldPath);var migrated=new FarmGame(oldPath);Check(migrated.R.JourneyPaused&&!migrated.R.InspirationsEnabled&&migrated.R.Phase=="battle","old active run stays on original rules");migrated.ToggleJourneyPause();migrated.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(migrated.R.Outcome=="victory"&&migrated.R.InspirationPicks.Length==0&&migrated.S.JourneyMemories.Single().InspirationStyle==-1,"no retroactive abilities for old runs");
        var paced=New(root);var caughtUp=New(root);foreach(var sample in new[]{paced,caughtUp}){sample.S.Xp=Enumerable.Repeat(55,5).ToArray();sample.BeginJourney(0,0);}
        int steps=0;while(paced.R.Phase!="inspiration"&&steps++<65){long deadline=paced.R.NextJourneyTicks;paced.AdvanceJourney(new DateTime(deadline,DateTimeKind.Utc));if(paced.R.Phase=="inspiration")Check(paced.R.NextJourneyTicks-deadline==TimeSpan.FromSeconds(18).Ticks,"a new choice has eighteen seconds before auto-selection");}
        Check(paced.R.Phase=="inspiration","paced journey reaches first choice");long choiceAt=paced.R.NextJourneyTicks;paced.AdvanceJourney(new DateTime(choiceAt-1,DateTimeKind.Utc));Check(paced.R.InspirationPicks.Length==0,"automatic selection does not happen before deadline");paced.AdvanceJourney(new DateTime(choiceAt,DateTimeKind.Utc));Check(paced.R.InspirationPicks.Length==1&&paced.R.NextJourneyTicks==choiceAt+TimeSpan.FromSeconds(8).Ticks,"automatic selection schedules the next route once");
        while(paced.JourneyRunning&&steps++<400)paced.AdvanceJourney(new DateTime(paced.R.NextJourneyTicks,DateTimeKind.Utc));caughtUp.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(FarmPracticeTests.Xml(paced.R.InspirationPicks)==FarmPracticeTests.Xml(caughtUp.R.InspirationPicks)&&paced.R.Hp.SequenceEqual(caughtUp.R.Hp)&&paced.S.Seed==caughtUp.S.Seed&&paced.R.RewardFertilizer==caughtUp.R.RewardFertilizer,"online steps and offline catch-up choose identically and award identically");
        Effects(root);return "PASS: 12 real ability effects and health conservation; relevant stable offers; three preference policies; manual/automatic choices; three opportunities and two-stack cap; stale/double-click protection; exact auto-choice timing; identical online/offline choices and outcomes; paused/offline persistence; unchanged rewards and permanent growth; temporary reset; collection, saved preference, history and battle snapshots; pre-version journey compatibility.";
    }
}

public partial class FarmPanel
{
    private void InspirationRender(string root,string name,double width=924,double height=721){Render();UIPreview(root,name,width,height);}
    internal void RenderInspirationChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.S.BlessingOffers=new[]{0,1,2};Tab="冒险";inspirationPreferencesOpen=true;InspirationRender(root,"inspiration-preference");UIAction("稳健续航").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));InspirationRender(root,"inspiration-preference-small",764,571);check(G.S.InspirationStyle==2&&G.S.BlessingOffers.SequenceEqual(new[]{0,1,2}),"preference does not reroll offers");UIVisible(UIAction("出发 · 自动走完整趟"),"departure preserved at minimum size");
        G.BeginJourney(0,0);G.RouteChoice(0);while(G.R.Phase=="battle")G.Step();G.R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(18).Ticks;Tab="冒险";InspirationRender(root,"inspiration-choice");InspirationRender(root,"inspiration-choice-small",764,571);foreach(int id in G.R.InspirationOffers)UIVisible(UIAction("选「"+FarmGame.InspirationNames[id]+"」"),"all inspiration choices visible at minimum size");UIVisible(UIAction("暂停旅程"),"pause visible while choosing");
        var oldButton=UIAction("选「"+FarmGame.InspirationNames[G.R.InspirationOffers[0]]+"」");UIAction("暂停旅程").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));int random=G.S.Seed;G.AdvanceJourney(DateTime.UtcNow.AddHours(2));InspirationRender(root,"inspiration-paused-small",764,571);check(G.R.JourneyPaused&&G.R.InspirationPicks.Length==0&&G.S.Seed==random,"paused screen waits without reroll");int chosen=G.R.InspirationOffers[0];UIAction("选「"+FarmGame.InspirationNames[chosen]+"」").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));InspirationRender(root,"inspiration-manual");check(G.R.InspirationPicks.Single().Manual&&G.R.JourneyPaused&&G.R.Node==1,"manual choice keeps explicit pause");
        G.RouteChoice(0);G.Step();InspirationRender(root,"inspiration-battle");check(G.R.ActiveBattle.Inspirations[chosen]==1&&UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="这趟学会的小配合"),"battle has acquired ability and visible run card");while(G.R.Phase=="battle")G.Step();InspirationRender(root,"inspiration-second-choice");int count=G.R.InspirationPicks.Length;oldButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.R.InspirationPicks.Length==count,"old card event cannot pick the next encounter");
        G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(2));InspirationRender(root,"inspiration-result");check(G.R.Outcome=="victory"&&G.R.InspirationPicks.Length==3,"full automatic continuation completes");scroller.ScrollToVerticalOffset(520);scroller.UpdateLayout();UIPreview(root,"inspiration-result-details");
        var memory=G.S.JourneyMemories[0];OpenBattleReview(memory);InspirationRender(root,"inspiration-review");check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="当场携带的灵感"),"review shows historical ability loadout");scroller.ScrollToBottom();scroller.UpdateLayout();UIPreview(root,"inspiration-review-details");
        OpenHandbook(3);historyExpanded=memory.RecordedTicks;InspirationRender(root,"inspiration-history");check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text.Contains("第 1 站 · ")),"travel history stores selection reasons");
        OpenHandbook(4);InspirationRender(root,"inspiration-book");InspirationRender(root,"inspiration-book-small",764,571);check(UIChildren<InspirationView>((DependencyObject)Content).Count()==12,"all twelve abilities can be previewed");scroller.ScrollToBottom();scroller.UpdateLayout();UIPreview(root,"inspiration-book-lower",764,571);UIAction("只看已领悟").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));InspirationRender(root,"inspiration-book-owned");check(UIChildren<InspirationView>((DependencyObject)Content).Count()==G.S.DiscoveredInspirations.Count(v=>v),"book filter shows exactly discovered abilities");
        check(!IsVisible&&!owner.IsVisible,"all new screens remain offscreen");File.WriteAllText(Path.Combine(root,"inspiration-ui-regression.txt"),"PASS: preference editing without reroll; minimum-window departure, three choice cards and pause; paused manual selection; stale controls rejected; automatic continuation; active effects; historical review and selection reasons; twelve-card collection and owned filter. Isolated saves and no native windows.");
    }
}
