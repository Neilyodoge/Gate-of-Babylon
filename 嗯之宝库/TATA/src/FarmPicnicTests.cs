using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public static class FarmPicnicTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Picnic: "+message);}
    private static FarmGame New(string root)
    {
        var g=new FarmGame(Path.Combine(root,"picnic-"+Guid.NewGuid().ToString("N")+".xml"));g.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;g.S.BlessingOffers=new[]{0,1,2};g.S.Berries=g.S.Grain=g.S.Mint=30;return g;
    }
    private static FarmGame EffectsGame(string root,int id)
    {
        var g=New(root);g.S.GardenUnlocked=true;g.S.Xp=Enumerable.Repeat(55,5).ToArray();g.S.Team=new[]{0,2,3};g.S.Skills[3]=1;
        if(id==4)g.S.Team=new[]{0,1,3};if(id==5)g.S.Team=new[]{0,4,3};return g;
    }
    private static void StartEffect(FarmGame g,int id)
    {
        Check(g.PlanPicnic(id)&&g.BeginJourney(0,0)&&g.RouteChoice(0),"effect fixture starts");
        g.R.EnemyCount=2;g.R.EnemyHp=new[]{2000,2000,0};g.R.EnemyMax=new[]{2000,2000,0};g.R.EnemyKind=new int[3];g.R.ActiveBattle.EnemyStart=4000;
        g.R.Hp[g.S.Team[0]]=20;g.R.ActiveBattle.StartHp[0]=20;
    }
    private static void Conserve(BattleNote b)
    {
        Check(b.Direct+b.Burn+b.Poison==b.EnemyStart+b.EnemyHealing-b.EnemyRemaining,"actual enemy health conserved");
        Check(b.StartHp.Sum()+b.Healing-b.EndHp.Sum()==b.Taken.Sum(),"actual party health conserved");
    }
    public static string Run(string root)
    {
        var g=New(root);Check(g.S.PlannedPicnic==-1&&g.R.Picnic==-1&&!g.PlanPicnic(-2)&&!g.PlanPicnic(6)&&!g.PlanPicnic(2),"defaults, invalid recipes and locked mint recipes");
        g.S.GardenUnlocked=true;
        for(int id=0;id<6;id++){
            Check(g.PlanPicnic(id),"all recipes can be planned after unlock");var cost=FarmGame.PicnicCost(id);
            for(int k=0;k<3;k++)if(cost[k]>0){g.S.Berries=g.S.Grain=g.S.Mint=30;if(k==0)g.S.Berries=cost[k]-1;else if(k==1)g.S.Grain=cost[k]-1;else g.S.Mint=cost[k]-1;string before=FarmPracticeTests.Xml(g.S);Check(!g.BeginJourney(0,0)&&FarmPracticeTests.Xml(g.S)==before,"any missing ingredient rejects entire departure without charge "+id+"/"+k);}
        }
        g.S.Berries=g.S.Grain=g.S.Mint=30;g.PlanPicnic(0);g.Save();var restored=new FarmGame(g.SavePath);Check(restored.AtHome&&restored.S.PlannedPicnic==0&&restored.S.Berries==30,"plan persists without cooking");
        string state=FarmPracticeTests.Xml(restored.S);Check(!restored.BeginJourney(1,0)&&!restored.BeginJourney(0,5)&&FarmPracticeTests.Xml(restored.S)==state,"unavailable route and blessing do not charge");
        int hp=restored.PermanentHp(0);Check(restored.BeginJourney(0,0)&&restored.S.Berries==28&&restored.S.Grain==28&&restored.S.Mint==30&&restored.R.Picnic==0&&restored.S.PlannedPicnic==-1,"one charge on departure and next plan cleared");
        Check(restored.MaxHp(0)==hp+8&&restored.PermanentHp(0)==hp&&restored.R.Hp[0]==hp+8,"temporary health starts full and never leaks into permanent growth");
        state=FarmPracticeTests.Xml(restored.S);Check(!restored.BeginJourney(0,0)&&!restored.PlanPicnic(1)&&FarmPracticeTests.Xml(restored.S)==state,"repeat departure and away plan cannot charge or change trip");
        restored.ToggleJourneyPause();restored.Save();var paused=new FarmGame(restored.SavePath);paused.AdvanceJourney(DateTime.UtcNow.AddHours(1));Check(paused.R.JourneyPaused&&paused.R.Picnic==0&&paused.R.Node==0&&paused.S.Berries==28,"pause and reload preserve meal without charging again");paused.Retreat();paused.Home();Check(paused.R.Picnic==-1&&paused.MaxHp(0)==hp&&paused.S.PlannedPicnic==-1&&paused.S.Berries==28,"return clears meal without refund or repeat cooking");
        paused.S.Berries=paused.S.Grain=paused.S.Mint=0;paused.EnsureJourneyOffers();Check(paused.BeginJourney(0,paused.S.BlessingOffers[0]),"empty pantry can always depart without food");
        for(int id=0;id<6;id++){
            var baseline=EffectsGame(root,id);var enhanced=EffectsGame(root,id);StartEffect(baseline,-1);StartEffect(enhanced,id);
            int rounds=id==2?3:1;for(int i=0;i<rounds;i++){baseline.Step();enhanced.Step();}
            var a=baseline.CurrentBattleNotes().Last();var b=enhanced.CurrentBattleNotes().Last();Conserve(b);
            if(id==0)Check(b.MaxHp.Sum()-a.MaxHp.Sum()==24,"rice adds eight maximum health per party member");
            if(id==1)Check(b.Blocked.Sum()+enhanced.R.Shield.Sum()>a.Blocked.Sum()+baseline.R.Shield.Sum()&&b.PicnicTriggers==1,"bread produces real opening protection");
            if(id==2)Check(b.Healing-a.Healing==8&&b.PicnicHealing==8&&b.PicnicTriggers==1,"tea produces eight actual recovery on third round");
            if(id==3)Check(b.Direct-a.Direct==3&&b.PicnicTriggers==1,"wet electric attack gains three damage");
            if(id==4)Check(b.Burn-a.Burn==2&&b.PicnicTriggers==1,"fire gets two additional burn");
            if(id==5)Check(b.Poison-a.Poison==1&&b.PicnicTriggers==1,"seed gets one additional poison");
        }
        var trial=EffectsGame(root,3);trial.S.Berries=trial.S.Grain=trial.S.Mint=0;trial.Save();var draft=trial.CurrentPracticeDraft();draft.Picnic=3;string original=FarmPracticeTests.Xml(trial.S),disk=File.ReadAllText(trial.SavePath);int count=Directory.GetFiles(root).Length;
        var comparison=trial.Practice(draft,0,0,0);Check(comparison!=null&&comparison.Current.Final.Picnic==-1&&comparison.Trial.Final.Picnic==3&&FarmPracticeTests.Xml(trial.S)==original&&File.ReadAllText(trial.SavePath)==disk&&Directory.GetFiles(root).Length==count,"practice can compare food with no food without ingredients or any real writes");
        var actual=EffectsGame(root,3);actual.PlanPicnic(3);actual.BeginJourney(0,0);actual.RouteChoice(0);for(int n=0;n<3&&actual.R.Phase=="battle";n++)actual.Step();Check(FarmPracticeTests.Xml(actual.CurrentBattleNotes().Last())==FarmPracticeTests.Xml(comparison.Trial.Final),"picnic trial exactly matches actual battle");
        Check(trial.AdoptPractice(comparison)&&trial.S.PlannedPicnic==3&&trial.S.Berries==0&&trial.AtHome&&!trial.BeginJourney(0,0),"adoption plans only and actual departure still checks materials");var fresh=trial.Practice(trial.CurrentPracticeDraft(),0,0,0);trial.PlanPicnic(1);Check(!trial.AdoptPractice(fresh),"changed picnic invalidates old comparison");
        var paced=EffectsGame(root,3);var offline=EffectsGame(root,3);foreach(var item in new[]{paced,offline}){item.PlanPicnic(3);item.BeginJourney(0,0);}int steps=0;while(paced.JourneyRunning&&steps++<400)paced.AdvanceJourney(new DateTime(paced.R.NextJourneyTicks,DateTimeKind.Utc));offline.AdvanceJourney(DateTime.UtcNow.AddHours(2));
        Check(paced.R.Outcome=="victory"&&paced.R.PicnicTriggers>0&&paced.R.PicnicTriggers==offline.R.PicnicTriggers&&paced.R.Hp.SequenceEqual(offline.R.Hp)&&paced.S.Seed==offline.S.Seed&&paced.R.RewardFertilizer==offline.R.RewardFertilizer&&paced.S.Berries==offline.S.Berries,"online and offline meal effects and costs agree");
        var memory=offline.S.JourneyMemories.Single();Check(memory.Picnic==3&&memory.PicnicTriggers==offline.R.PicnicTriggers&&memory.Battles.Sum(b=>b.PicnicTriggers)==memory.PicnicTriggers&&memory.Battles.All(b=>b.Picnic==3),"per-fight and trip snapshots agree");foreach(var note in memory.Battles)Conserve(note);offline.Home();offline.PlanPicnic(1);Check(memory.Picnic==3&&memory.Battles.All(b=>b.Picnic==3),"history never changes with current picnic");
        var reload=new FarmGame(offline.SavePath);Check(reload.S.JourneyMemories.Single().Picnic==3&&reload.S.PlannedPicnic==1,"history and pending plan persist independently");
        var tea=EffectsGame(root,2);StartEffect(tea,2);tea.R.Round=2;foreach(int p in tea.S.Team)tea.R.Hp[p]=tea.MaxHp(p);tea.R.Hp[tea.S.Team[0]]=0;tea.R.Hp[tea.S.Team[1]]-=3;tea.Step();Check(tea.R.PicnicHealing==3&&tea.R.Hp[tea.S.Team[0]]==0,"tea records only actual recovery and never revives fallen pets");
        var cap=EffectsGame(root,5);StartEffect(cap,5);cap.R.Poison[0]=40;cap.Step();Check(cap.R.PicnicTriggers==0&&cap.R.Poison.All(n=>n<=40),"poison already capped does not falsely trigger extra stack");
        var shield=EffectsGame(root,1);shield.S.Team=new[]{3,0,1};shield.S.Skills[3]=1;StartEffect(shield,1);shield.Step();Check(shield.R.ShieldTriggers>0,"opening bread shield enables first-action shield strike");
        var old=New(root);old.BeginJourney(0,0);old.RouteChoice(0);old.ToggleJourneyPause();old.Save();var xml=XDocument.Load(old.SavePath);foreach(var field in xml.Descendants().Where(x=>new[]{"PlannedPicnic","Picnic","PicnicTriggers","PicnicHealing","PicnicStart","PicnicHealingStart"}.Contains(x.Name.LocalName)).ToArray())field.Remove();string oldPath=Path.Combine(root,"before-picnic.xml");xml.Save(oldPath);var migrated=new FarmGame(oldPath);Check(migrated.R.Picnic==-1&&migrated.S.PlannedPicnic==-1&&migrated.R.JourneyPaused&&migrated.R.ActiveBattle.Picnic==-1,"old active journeys default to no food without changing phase");
        var matrix=EffectsGame(root,0);matrix.S.Evolved=true;matrix.S.SeedStage=3;matrix.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();matrix.S.EquippedRelics=new[]{3,5};
        for(int id=0;id<6;id++)for(int build=0;build<5;build++){var plan=matrix.PracticePreset(build);plan.Picnic=id;var result=matrix.Practice(plan,1,2,0);Check(result!=null,"all recipe/build combinations simulate");foreach(var note in result.Trial.Rounds)Conserve(note);}
        return "PASS: six actual effects; HP conservation across 30 recipe/build combinations; atomic ingredient checks; one charge on departure; optional food with empty pantry; saved plans; temporary versus permanent HP; pause/reload; identical online/offline outcome; old-run compatibility; exact practice equivalence and zero real writes; independent historical snapshots.";
    }
}

public partial class FarmPanel
{
    private void PicnicRender(string root,string name,double width=924,double height=721){Render();UIPreview(root,"picnic-"+name,width,height);}
    internal void RenderPicnicChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.S.BlessingOffers=new[]{0,1,2};Tab="冒险";PicnicRender(root,"entry");UIAction("出门便当 · 可选").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"recipes");foreach(string name in FarmGame.PicnicNames){var button=UIAction("查看便当 "+name);check(button.TranslatePoint(new Point(0,button.ActualHeight),(UIElement)Content).Y<=682,"six recipe actions fit default window");}PicnicRender(root,"recipes-small",764,571);UIVisible(UIAction("选好，返回冒险准备"),"picnic confirm fits minimum window");UIVisible(UIAction("这趟不带便当"),"no-food choice fits minimum window");
        var stocks=new[]{G.S.Berries,G.S.Grain,G.S.Mint};UIAction("查看便当 月光薄荷茶").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"locked");check(!UIAction("选好，返回冒险准备").IsEnabled&&stocks.SequenceEqual(new[]{G.S.Berries,G.S.Grain,G.S.Mint}),"locked recipe previews without charge");
        G.S.GardenUnlocked=true;G.S.Berries=G.S.Grain=G.S.Mint=0;picnicPreview=5;PicnicRender(root,"missing-small",764,571);UIAction("看看材料怎么种").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"planting");check(Tab=="便当"&&picnicPlanting,"ingredients open dedicated planting preview");var crops=(int[])G.S.Crops.Clone();UIAction("多种莓果").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"planting-preview");check(G.S.Crops.SequenceEqual(crops),"preview does not replant");var apply=UIChildren<Button>((DependencyObject)Content).First(b=>Convert.ToString(b.Content).StartsWith("应用方案"));apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"planting-applied");check(G.S.Crops.SequenceEqual(G.PlantingPlan(1))&&G.S.PlannedPicnic==-1&&G.S.Berries==0,"planting changes only crops");
        UIAction("返回便当食谱").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"back-to-recipe");UIAction("选好，返回冒险准备").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"departure-missing",764,571);check(Tab=="冒险"&&G.S.PlannedPicnic==5&&!UIAction("制作便当并出发").IsEnabled,"missing ingredients disable real departure");UIVisible(UIAction("制作便当并出发"),"paid departure visible at minimum size");UIVisible(UIAction("不带便当"),"cancel meal visible before departure");UIAction("不带便当").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"cancelled");check(G.S.PlannedPicnic==-1&&G.AtHome&&G.S.Berries==0,"direct cancellation clears plan without charge or departure");G.PlanPicnic(5);PicnicRender(root,"replanned");
        UIAction("更换 / 不带便当").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"reopen");UIAction("这趟不带便当").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"no-food");check(G.S.PlannedPicnic==-1&&UIAction("出发 · 自动走完整趟").IsEnabled,"empty pantry can use free departure");
        OpenPicnic();picnicPreview=3;PicnicRender(root,"trial-entry");UIAction("先免费试打看看").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"practice-small",764,571);check(practiceDraft.Picnic==3&&G.S.PlannedPicnic==-1,"recipe becomes a trial-only meal");UIVisible(UIAction("开始试练 · 免费"),"trial meal still fits start button");UIAction("开始试练 · 免费").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"comparison");check(practiceResult.Trial.Final.Picnic==3&&G.S.Mint==0,"trial uses selected meal without charging");UIAction("采用搭配并去准备").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"adopted");check(G.S.PlannedPicnic==3&&G.AtHome&&G.S.Mint==0,"adoption requires later paid departure");
        G.S.Berries=20;G.S.Mint=5;RefreshCounters();check(UIAction("制作便当并出发").IsEnabled,"new harvest enables departure without reopening page");PicnicRender(root,"ready-small",764,571);UIVisible(UIAction("制作便当并出发"),"ready paid departure fits");UIAction("制作便当并出发").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PicnicRender(root,"travelling");check(G.R.Picnic==3&&G.S.PlannedPicnic==-1&&G.S.Berries==17&&G.S.Mint==4,"actual UI departure pays exact displayed cost");
        G.RouteChoice(0);G.Step();G.ToggleJourneyPause();OpenPicnic();PicnicRender(root,"away");check(!UIAction("选好，返回冒险准备").IsEnabled&&G.R.JourneyPaused,"viewing recipes during paused trip cannot change it");Go("冒险");G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(2));PicnicRender(root,"result");scroller.ScrollToBottom();scroller.UpdateLayout();UIPreview(root,"picnic-result-details");
        var memory=G.S.JourneyMemories.Single();check(memory.Picnic==3,"completed trip records food");OpenBattleReview(memory);PicnicRender(root,"review");check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="当场便当效果"),"battle review exposes meal");scroller.ScrollToBottom();scroller.UpdateLayout();UIPreview(root,"picnic-review-details");OpenHandbook(3);historyExpanded=memory.RecordedTicks;PicnicRender(root,"history");check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text.Contains("便当 · 酥脆雷莓")),"history includes picnic snapshot");
        check(!IsVisible&&!owner.IsVisible,"all picnic screens remain offscreen");File.WriteAllText(Path.Combine(root,"picnic-ui-regression.txt"),"PASS: six-recipe preview, lock and cost clarity, minimum-window selection and departure, planting preview/apply/return, no-food escape, free recipe trial and adoption, live resource readiness, exact charge, paused-trip lock, result/review/history. No native windows or player-save access.");
    }
}
