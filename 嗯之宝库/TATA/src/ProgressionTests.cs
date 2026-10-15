using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Windows;

public static class ProgressionTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Progression: "+message);}
    private static FarmGame New(string root){return new FarmGame(Path.Combine(root,"progression-"+Guid.NewGuid().ToString("N")+".xml"));}
    private static readonly int[] XpByLevel={0,0,20,55,110,190,300,450,650,900,1250};
    private static void Start(FarmGame g,int region,int depth,int blessing=3){g.S.BlessingOffers=new[]{blessing,(blessing+1)%6,(blessing+2)%6};Check(g.BeginJourney(region,blessing,depth),"departure "+region+"/"+depth);}
    private static FarmGame Unlocked(string root,int level=5){var g=New(root);g.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();g.S.Evolved=true;g.S.Xp=Enumerable.Repeat(XpByLevel[level],5).ToArray();return g;}
    public static string Run(string root)
    {
        var report=new StringBuilder();var fresh=New(root);fresh.S.BlessingOffers=new[]{0,1,2};
        Check(fresh.ExpeditionUnlocked(0,0)&&!fresh.ExpeditionUnlocked(0,1)&&!fresh.ExpeditionUnlocked(1,0)&&fresh.S.EquippedRelics.All(x=>x==-1),"new game opens only creek first tier");
        Check(!fresh.BeginJourney(0,0,1)&&!fresh.BeginJourney(1,0,0)&&!fresh.BeginJourney(-1,0,0)&&!fresh.BeginJourney(0,0,3)&&fresh.AtHome&&fresh.S.BlessingOffers.Length==3,"locked/invalid departures do not consume offers");
        Check(!fresh.EquipRelic(0,0)&&!fresh.EquipRelic(-2,0)&&!fresh.EquipRelic(-1,2),"unowned and invalid relic choices rejected");
        fresh.S.Grain=5;Check(fresh.TrainToNextCost(4)==6&&!fresh.TrainToNext(4)&&fresh.S.Grain==5&&fresh.S.Xp[4]==0,"batch training shows full cost and is atomic");fresh.S.Grain=6;
        Check(fresh.TrainToNext(4)&&fresh.S.Xp[4]==36&&fresh.Level(4)==2&&fresh.S.SeedBond==0&&fresh.S.Grain==0,"batch training follows existing +18 growth costs");
        fresh.S.Xp[4]=1249;fresh.S.Grain=3;Check(fresh.TrainToNext(4)&&fresh.Level(4)==10&&!fresh.TrainToNext(4)&&fresh.TrainToNextCost(4)==0,"training stops at max level");
        var trip=New(root);trip.S.Xp=Enumerable.Repeat(1250,5).ToArray();trip.S.Evolved=true;trip.S.SeedStage=3;trip.SetPoisonPreset();
        for(int region=0;region<2;region++)for(int depth=0;depth<3;depth++){
            Start(trip,region,depth);Check(!trip.EquipRelic(-1,0)&&!trip.TrainToNext(0),"trip locks loadout and training");
            trip.AdvanceJourney(DateTime.UtcNow.AddHours(2));int id=region*3+depth;
            Check(trip.R.Outcome=="victory"&&trip.R.NewRelic==id&&trip.S.ExpeditionClears[id]==1,"each first clear unlocks its unique relic: "+id);
            Check(trip.R.RewardCrystals==FarmGame.ExpeditionCrystals(region,depth)&&trip.R.RewardFertilizer>=FarmGame.ExpeditionFertilizer(region,depth),"depth reward scaling");
            int fertilizer=trip.S.Fertilizer;trip.AdvanceJourney(DateTime.UtcNow.AddHours(4));Check(trip.S.Fertilizer==fertilizer&&trip.S.ExpeditionClears[id]==1,"settled trip cannot reward twice");
            if(depth<2)Check(trip.ExpeditionUnlocked(region,depth+1),"next depth opens on victory");
            trip.Home();
        }
        Check(trip.S.EquippedRelics.SequenceEqual(new[]{0,1})&&trip.S.ExpeditionClears.All(x=>x==1),"first two finds fill empty slots, later finds preserve chosen loadout");
        Check(trip.EquipRelic(0,1)&&trip.S.EquippedRelics.SequenceEqual(new[]{1,0}),"moving an equipped relic swaps slots without duplicates");
        trip.EquipRelic(2,0);trip.EquipRelic(-1,1);Start(trip,0,0);var snapshot=(int[])trip.R.Relics.Clone();trip.S.EquippedRelics=new[]{3,5};trip.Save();Check(trip.R.Relics.SequenceEqual(snapshot),"running trip carries a snapshot of its relics");
        trip.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(trip.R.RelicTriggers[2]==4&&trip.R.RewardFertilizer==13,"harvest bag adds four rewards, plus collected-decoration duplicate bonus");trip.Home();
        for(int relic=0;relic<6;relic++){
            var g=Unlocked(root,4);if(relic==3)g.SetPreset(2);else if(relic==4)g.SetPreset(1);else if(relic==5)g.SetPoisonPreset();else g.SetPreset(0);
            g.EquipRelic(relic,0);Start(g,1,0,relic==1?0:relic==4?1:3);g.RouteChoice(0);if(relic==4)g.R.Hp[g.S.Team[0]]=g.MaxHp(g.S.Team[0])-12;
            g.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(g.R.RelicTriggers[relic]>0,"relic has observable triggers: "+relic);report.AppendLine("Relic "+relic+": triggers="+g.R.RelicTriggers[relic]+", outcome="+g.R.Outcome);
        }
        var paired=Unlocked(root,5);paired.SetPreset(2);paired.EquipRelic(3,0);paired.EquipRelic(5,1);Start(paired,1,1);paired.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(paired.R.RelicTriggers[3]>0&&paired.R.RelicTriggers[5]>0&&paired.R.PoisonDamage>0,"fire produces amplified poison without seed");
        var plain=SoloSeed(root,0);var armor=SoloSeed(root,1);plain.Step();armor.Step();Check(armor.R.EnemyHp[0]-plain.R.EnemyHp[0]==2&&plain.R.PoisonDamage==armor.R.PoisonDamage,"hard shell reduces direct damage but leaves poison damage unchanged");
        var regen=SoloSeed(root,2);var baseline=SoloSeed(root,0);regen.R.EnemyHp[0]=baseline.R.EnemyHp[0]=900;regen.Step();baseline.Step();Check(regen.R.EnemyHp[0]-baseline.R.EnemyHp[0]==4,"secret creek enemies regenerate before actions");
        var paused=Unlocked(root);paused.EquipRelic(3,0);paused.SetPreset(2);Start(paused,1,2);paused.RouteChoice(0);paused.Step();paused.ToggleJourneyPause();paused.Save();var loaded=new FarmGame(paused.SavePath);int round=loaded.R.Round;loaded.AdvanceJourney(DateTime.UtcNow.AddHours(3));Check(loaded.R.Depth==2&&loaded.R.Relics[0]==3&&loaded.R.RelicTriggers.SequenceEqual(paused.R.RelicTriggers)&&loaded.R.Round==round,"depth, relics, counters and explicit pause survive reload");
        var offline=Unlocked(root);offline.EquipRelic(0,0);Start(offline,0,1);offline.R.NextJourneyTicks=DateTime.UtcNow.AddHours(-1).Ticks;offline.Save();var settled=new FarmGame(offline.SavePath);Check(settled.R.Outcome=="victory"&&settled.S.ExpeditionClears[1]==2,"offline settlement advances only the current depth trip");int cleared=settled.S.ExpeditionClears.Sum();var twice=new FarmGame(offline.SavePath);Check(twice.S.ExpeditionClears.Sum()==cleared,"offline depth completion is idempotent");
        var defeat=Unlocked(root,1);Start(defeat,1,2);defeat.RouteChoice(0);foreach(int p in defeat.S.Team)defeat.R.Hp[p]=1;defeat.R.EnemyCount=3;defeat.R.EnemyKind=new[]{3,3,3};defeat.R.Round=2;defeat.R.EnemyHp=new[]{1000,1000,1000};defeat.Step();Check(defeat.R.Outcome=="defeat"&&defeat.S.ExpeditionClears[5]==1&&defeat.R.NewRelic==-1,"defeat cannot grant a new clear or relic");
        var chapters=Unlocked(root,10);chapters.S.Harvests=1;chapters.S.SoilLevel=1;chapters.S.SeedStage=2;chapters.S.RelicVictories=1;chapters.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(10).Ticks;
        Check(chapters.ClaimableChapters==8&&!chapters.ClaimChapter(-1)&&!chapters.ClaimChapter(8),"eight one-time goals and invalid claim handling");chapters.ClaimReadyChapters();int[] after={chapters.S.Berries,chapters.S.Grain,chapters.S.Mint,chapters.S.Fertilizer,chapters.S.Crystals};chapters.ClaimReadyChapters();var rewards=new FarmGame(chapters.SavePath);Check(rewards.S.ChapterClaimed.All(x=>x)&&rewards.ClaimableChapters==0&&after.SequenceEqual(new[]{rewards.S.Berries,rewards.S.Grain,rewards.S.Mint,rewards.S.Fertilizer,rewards.S.Crystals}),"rewards persist and cannot be claimed twice");
        Migration(root);PlayLoop(root);Balance(root,report);
        report.AppendLine("PASS: depth gates and rewards, six relics, slot snapshots, hard-shell/regen rules, batch training, one-time chapters, failure, offline/pause and old-save migration.");return report.ToString();
    }
    private static FarmGame SoloSeed(string root,int depth)
    {
        var g=Unlocked(root,3);g.S.Evolved=false;g.S.Team=new[]{4,0,3};Start(g,0,depth);g.RouteChoice(0);g.R.Hp[0]=g.R.Hp[3]=0;g.R.EnemyCount=1;g.R.EnemyKind[0]=0;g.R.EnemyHp[0]=g.R.EnemyMax[0]=1000;g.R.Wet[0]=0;return g;
    }
    private static void Migration(string root)
    {
        var old=New(root);old.S.GardenUnlocked=true;old.S.Evolved=true;old.S.Decorations[0]=old.S.Decorations[3]=true;old.S.Xp=Enumerable.Repeat(55,5).ToArray();Start(old,1,0);old.RouteChoice(0);old.Step();old.ToggleJourneyPause();old.Save();
        var xml=XDocument.Load(old.SavePath);foreach(string name in new[]{"ProgressionVersion","RelicVictories","ExpeditionClears","EquippedRelics","ChapterClaimed"})xml.Root.Element(name).Remove();foreach(string name in new[]{"Depth","NewRelic","Relics","RelicTriggers"})xml.Root.Element("Run").Element(name).Remove();
        string path=Path.Combine(root,"pre-v1.8-save.xml");xml.Save(path);string original=File.ReadAllText(path);var migrated=new FarmGame(path);
        Check(File.ReadAllText(path+".before-progression-v1.8.bak")==original,"original format migration backup retained");
        Check(migrated.S.ExpeditionClears.SequenceEqual(new[]{1,0,0,1,0,0})&&migrated.S.EquippedRelics.All(x=>x==-1)&&migrated.S.ChapterClaimed.All(x=>!x),"old victories infer only supported base depths, without changing loadout or claiming rewards");
        Check(migrated.R.Phase==old.R.Phase&&migrated.R.Round==old.R.Round&&migrated.R.Hp.SequenceEqual(old.R.Hp)&&migrated.R.JourneyPaused&&migrated.R.Relics.All(x=>x==-1)&&migrated.S.Fertilizer==old.S.Fertilizer,"old adventure and economy remain intact");
        var reread=new FarmGame(path);Check(File.ReadAllText(path+".before-progression-v1.8.bak")==original&&reread.S.ExpeditionClears.SequenceEqual(migrated.S.ExpeditionClears),"migration backup and unlocks are stable on reload");
    }
    private static void Balance(string root,StringBuilder report)
    {
        foreach(int region in new[]{0,1})foreach(int depth in new[]{0,1,2})foreach(int level in new[]{2,4,6,8,10}){
            int wins=0;
            for(int build=0;build<4;build++){
                var g=Unlocked(root,level);g.S.SeedStage=level>=6?3:level>=4?2:0;if(build==3)g.SetPoisonPreset();else g.SetPreset(build);
                int[] relics=build==0?new[]{0,1}:build==1?new[]{0,4}:build==2?new[]{3,5}:new[]{0,5};g.EquipRelic(relics[0],0);g.EquipRelic(relics[1],1);
                Start(g,region,depth,3);g.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(g.R.Phase=="result","balanced run terminates");if(g.R.Outcome=="victory")wins++;
            }
            report.AppendLine("Balance "+region+"/"+depth+" Lv."+level+": "+wins+"/4 builds clear (evolved pets, all relics available)");
            if(level==10)Check(wins>=2,"at least two fully grown builds clear every tier");
            if(region==1&&depth==2&&level==2)Check(wins==0,"deepest cave needs growth even with endgame relics");
        }
    }
    private static void PlayLoop(string root)
    {
        var g=New(root);g.TickFarm(DateTime.UtcNow.AddMinutes(6));
        foreach(int pet in g.S.Team)while(g.Level(pet)<3)Check(g.TrainToNext(pet),"farm harvest can fund level-three training");
        for(int i=0;i<3;i++)Check(g.Feed(0),"farm berries supply evolution bond");
        Start(g,0,0);g.AdvanceJourney(DateTime.UtcNow.AddHours(1));Check(g.R.Outcome=="victory"&&g.R.NewRelic==0,"starter loop earns first relic");
        g.Home();Check(g.UpgradeSoil()&&g.Evolve(),"adventure loot improves the farm and evolves a pet");
        g.ClaimReadyChapters();Check(g.S.ChapterClaimed.Take(5).All(x=>x),"first five chapter goals follow the normal play loop");
        Start(g,0,1);g.AdvanceJourney(DateTime.UtcNow.AddHours(1));Check(g.R.Outcome=="victory"&&g.R.NewRelic==1&&g.R.RelicTriggers[0]>0&&g.ChapterDone(5),"grown team and first relic unlock the second depth and relic goal");
    }
}

public partial class FarmWindow
{
    public void BackgroundProgressionChecks(string root)
    {
        Panel=new FarmPanel(this);Panel.RenderHandbookCheck(root,"handbook-new");
        G.S.Xp=Enumerable.Repeat(300,5).ToArray();G.S.SeedStage=2;G.S.ExpeditionClears=Enumerable.Repeat(1,6).ToArray();G.S.EquippedRelics=new[]{3,5};G.S.Harvests=20;G.S.SoilLevel=2;G.S.RelicVictories=2;G.S.Grain=200;G.SetPoisonPreset();
        Panel.RenderHandbookCheck(root);Panel.Tab="冒险";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"progression-prepare-small.png"),764,571);
        Panel.RenderDeepDepartureCheck(root);var departure=FindButton((DependencyObject)Panel.Content,"出发 · 自动走完整趟");FarmHeadlessTests.Check(departure!=null&&departure.TranslatePoint(new Point(0,0),(UIElement)Panel.Content).Y+departure.ActualHeight<521,"departure button remains visible in the smallest window at deepest destination");
        Panel.Tab="伙伴";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"progression-care-small.png"),764,571);
        G.S.ExpeditionClears[5]=0;G.S.BlessingOffers=new[]{2,3,4};G.BeginJourney(1,2,2);G.RouteChoice(0);G.Step();Panel.Tab="冒险";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"progression-battle-small.png"),764,571);
        G.AdvanceJourney(DateTime.UtcNow.AddHours(2));Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"progression-result-small.png"),764,571);
        G.Retreat();G.Home();G.S.ExpeditionClears[5]=0;G.S.Xp=Enumerable.Repeat(1250,5).ToArray();G.S.SeedStage=3;G.S.BlessingOffers=new[]{0,1,3};G.BeginJourney(1,3,2);G.AdvanceJourney(DateTime.UtcNow.AddHours(2));Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"progression-first-clear.png"),924,721);
        FarmHeadlessTests.Check(G.R.Outcome=="victory"&&G.R.NewRelic==5,"first-clear reward view exercises a new relic");
        FarmHeadlessTests.Check(!IsVisible&&!Panel.IsVisible,"progression rendering does not show native windows");
    }
}
