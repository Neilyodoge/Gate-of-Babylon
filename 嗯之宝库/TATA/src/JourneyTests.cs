using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;

public static class JourneyTests
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Journey test: "+message);}
    private static FarmGame New(string root){return new FarmGame(Path.Combine(root,"journey-"+Guid.NewGuid().ToString("N")+".xml"));}
    public static void Start(FarmGame g,int difficulty,int blessing){g.S.BlessingOffers=new[]{blessing,(blessing+1)%6,(blessing+2)%6};Check(g.BeginJourney(difficulty,blessing),"valid departure");}
    public static string Run(string root)
    {
        var report=new StringBuilder();var stable=New(root);stable.EnsureJourneyOffers();int[] offered=(int[])stable.S.BlessingOffers.Clone();stable.EnsureJourneyOffers();stable.SetPreset(0);var reload=new FarmGame(stable.SavePath);Check(reload.S.BlessingOffers.SequenceEqual(offered),"offers survive reopen and team changes");
        Check(!reload.BeginJourney(0,99)&&reload.AtHome,"invalid blessing rejected");Check(reload.BeginJourney(0,offered[0])&&!reload.BeginJourney(0,offered[0]),"cannot start twice");Check(!reload.SetSkill(0,1)&&!reload.SetPreset(0),"loadout locked while away");
        var paused=New(root);Start(paused,0,3);paused.ToggleJourneyPause();int node=paused.R.Node;paused.AdvanceJourney(DateTime.UtcNow.AddHours(3));Check(paused.R.Node==node&&paused.R.Round==0,"explicit pause survives elapsed time");paused.ToggleJourneyPause();Check(paused.R.NextJourneyTicks>DateTime.UtcNow.Ticks,"resume starts from now");
        var progress=New(root);progress.S.Xp=new[]{55,55,55,55};Start(progress,0,3);int[] xp=(int[])progress.S.Xp.Clone();progress.AdvanceJourney(DateTime.UtcNow.AddHours(2));
        Check(progress.R.Outcome=="victory"&&progress.R.RewardFertilizer==7&&progress.S.Fertilizer==7&&progress.R.RewardCrystals==2,"whole journey earns expected fertilizer and evolution crystals");Check(progress.S.Xp.SequenceEqual(xp),"growth comes from farm-funded care");Check(progress.S.Decorations[0]&&progress.S.DisplayDecoration==0,"first decoration earned and displayed");
        int fert=progress.S.Fertilizer,crystals=progress.S.Crystals;progress.AdvanceJourney(DateTime.UtcNow.AddDays(1));Check(progress.S.Fertilizer==fert&&progress.S.Crystals==crystals,"finished journey never rewards or restarts twice");
        double before=progress.CropSeconds(0);Check(progress.UpgradeSoil()&&progress.S.SoilLevel==1&&progress.S.Fertilizer==3&&progress.CropSeconds(0)<before,"fertilizer funds permanent crop speed");Check(!progress.UpgradeSoil(),"insufficient fertilizer is atomic");
        progress.Home();for(int n=1;n<4;n++){Start(progress,0,3);progress.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(progress.R.Outcome=="victory","repeat collection journey clears");if(n<3)Check(progress.R.DecorationReward==n,"new decoration before duplicates");else Check(progress.R.DecorationReward==-1&&progress.R.RewardFertilizer==9,"completed collection converts duplicate to fertilizer");progress.Home();}
        Check(progress.S.Decorations.Take(3).All(x=>x)&&!progress.EquipDecoration(5)&&progress.EquipDecoration(2),"only owned decoration can be equipped");
        var stored=new FarmGame(progress.SavePath);Check(stored.S.SoilLevel==1&&stored.S.Decorations.Take(3).All(x=>x)&&stored.S.DisplayDecoration==2,"farm and cosmetics persist");
        stored.S.Mint=0;int beforeXp=stored.S.Xp[0];Check(!stored.HerbalFeed(0)&&stored.S.Xp[0]==beforeXp,"mint care needs material");stored.S.Mint=1;Check(stored.HerbalFeed(0)&&stored.S.Mint==0&&stored.S.Xp[0]==beforeXp+24,"mint remains useful for growth");
        var unlock=New(root);Check(!unlock.SetSkill(0,1)&&!unlock.CanUsePreset(1),"alternative skill needs level two");unlock.S.Xp[0]=unlock.S.Xp[3]=24;Check(unlock.SetSkill(0,1)&&unlock.SetPreset(1),"level two unlocks free build changes");
        for(int preset=0;preset<3;preset++)for(int blessing=0;blessing<6;blessing++){
            var g=New(root);g.S.Xp=new[]{55,55,55,55};g.SetPreset(preset);Start(g,0,blessing);int steps=g.AdvanceJourney(DateTime.UtcNow.AddHours(2));
            Check(g.R.Phase=="result"&&steps<400,"auto journey terminates");Check(g.R.Outcome=="victory","level-three build can clear creek: "+preset+" / "+blessing);
            if(preset==0)Check(g.R.ChainTriggers>0,"water build produces chain lightning");if(preset==1)Check(g.R.ShieldTriggers>0,"shield build consumes shields for damage");if(preset==2)Check(g.R.SpreadTriggers>0&&g.R.BurnDamage>0,"fire build spreads damage over time");
            report.AppendLine("Build "+preset+", blessing "+blessing+": "+g.R.Outcome+"; water="+g.R.WaterTriggers+", chain="+g.R.ChainTriggers+", shield="+g.R.ShieldTriggers+", spread="+g.R.SpreadTriggers+", burn="+g.R.BurnDamage);
        }
        var hard=New(root);hard.S.Evolved=true;hard.S.Xp=new[]{190,190,190,190};Start(hard,1,3);hard.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(hard.R.Outcome=="victory"&&hard.R.RewardFertilizer==14&&hard.R.DecorationReward==3,"grown team earns cave rewards");
        var offline=New(root);offline.S.Xp=new[]{55,55,55,55};Start(offline,0,3);offline.R.NextJourneyTicks=DateTime.UtcNow.AddHours(-1).Ticks;offline.Save();var caughtUp=new FarmGame(offline.SavePath);Check(caughtUp.R.Phase=="result"&&caughtUp.S.Fertilizer==7,"restart settles one offline journey");var again=new FarmGame(offline.SavePath);Check(again.S.Fertilizer==7,"offline settlement is idempotent");
        var death=New(root);Start(death,0,5);death.RouteChoice(0);foreach(int p in death.S.Team)death.R.Hp[p]=1;for(int i=0;i<death.R.EnemyCount;i++){death.R.EnemyHp[i]=death.R.EnemyMax[i]=1000;death.R.EnemyKind[i]=3;}death.R.Round=2;death.Step();Check(death.R.Outcome=="defeat"&&death.R.RewardFertilizer==0,"failed first encounter grants no unearned reward");
        foreach(int experience in new[]{0,24,55,110,190}){var strength=New(root);strength.S.Evolved=true;strength.S.Xp=new[]{experience,experience,experience,experience};Start(strength,1,3);strength.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(strength.Level(0)==1?strength.R.Outcome=="defeat":strength.Level(0)>=4?strength.R.Outcome=="victory":strength.R.Phase=="result","cave growth endpoints with temporary choices at level "+strength.Level(0));report.AppendLine("Cave Lv."+strength.Level(0)+": "+strength.R.Outcome+"; fertilizer="+strength.R.RewardFertilizer);}
        var legacy=New(root);legacy.Begin(0,0);legacy.R.Node=2;legacy.RouteChoice(1);legacy.R.Perks[3]=1;legacy.Save();var legacyReload=new FarmGame(legacy.SavePath);Check(!legacyReload.R.Casual&&legacyReload.R.Phase=="draft"&&legacyReload.R.Perks[3]==1,"old pending choice is untouched on load");int[] originalOffers=(int[])legacyReload.R.Offers.Clone();Check(legacyReload.AdoptLegacyJourney(),"explicitly adopt old journey");legacyReload.AdvanceJourney(new DateTime(legacyReload.R.NextJourneyTicks,DateTimeKind.Utc));Check(legacyReload.R.Node==3&&legacyReload.R.Phase=="route"&&originalOffers.Any(i=>legacyReload.R.Perks[i]>(i==3?1:0))&&legacyReload.R.RewardFertilizer==0,"adoption chooses original offer without retroactive loot");
        report.AppendLine("PASS: offer stability, pause/resume, farm economy, decoration collection, level locks, three builds, cave, offline settlement, failure and legacy continuation.");return report.ToString();
    }
}

public partial class FarmWindow
{
    public void BackgroundJourneyChecks(string root)
    {
        Panel=new FarmPanel(this);G.S.Xp=new[]{55,55,55,55};G.SetPreset(0);
        foreach(string tab in new[]{"冒险","伙伴","农场","怎么玩"}){Panel.Tab=tab;Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"journey-"+tab+".png"),924,721);}
        Panel.Tab="冒险";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"journey-small-prepare.png"),764,571);
        JourneyTests.Start(G,0,0);G.RouteChoice(0);G.Step();Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"journey-battle.png"),924,721);
        int advanced=G.AdvanceJourney(DateTime.UtcNow.AddSeconds(20));FarmHeadlessTests.Check(advanced>0&&!Panel.IsVisible,"invisible panel does not block game advancement");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(2));Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"journey-result.png"),924,721);
        G.Home();Panel.Tab="农场";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"journey-farm-rewards.png"),924,721);
        G.S.DesktopMode="pet";G.S.DisplayDecoration=0;BuildDesktop();FarmHeadlessTests.Render(this,Path.Combine(root,"journey-compact-decoration.png"),Width,Height);
        FarmHeadlessTests.Check(!IsVisible&&!Panel.IsVisible&&new WindowInteropHelper(this).Handle==IntPtr.Zero&&new WindowInteropHelper(Panel).Handle==IntPtr.Zero,"journey visual checks are windowless");
    }
}
