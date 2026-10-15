using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows;

public static class PetTests
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception("Pet regression: "+label);}
    private static FarmGame New(string root){return new FarmGame(Path.Combine(root,"pet-"+Guid.NewGuid().ToString("N")+".xml"));}
    public static string Run(string root)
    {
        var report=new StringBuilder();var game=New(root);Check(game.S.Xp.Length==5&&game.R.Hp.Length==5,"five pets start with complete state");
        game.S.DisplayPet=4;game.Save();var reload=new FarmGame(game.SavePath);Check(reload.S.DisplayPet==4,"seed can be the desktop pet");
        var old=New(root);old.Begin(0,0);old.RouteChoice(0);old.Step();old.Save();var xml=XDocument.Load(old.SavePath);
        foreach(string name in new[]{"Xp","Skills"})xml.Root.Element(name).Elements().Last().Remove();
        foreach(string name in new[]{"Hp","Shield","Skills"})xml.Root.Element("Run").Element(name).Elements().Last().Remove();
        foreach(string name in new[]{"SeedStage","SeedBond","FarmCaretaker"})xml.Root.Element(name).Remove();
        string legacy=Path.Combine(root,"pet-legacy-four.xml");xml.Save(legacy);var migrated=new FarmGame(legacy);
        Check(File.Exists(legacy+".before-pets-v1.6.bak")&&XDocument.Load(legacy+".before-pets-v1.6.bak").Root.Element("Xp").Elements().Count()==4,"migration preserves a permanent original-format backup");
        Check(migrated.SavePath==legacy&&migrated.S.Xp.Length==5&&migrated.R.Hp.Length==5&&migrated.S.Xp.Take(4).SequenceEqual(old.S.Xp.Take(4))&&migrated.R.Hp.Take(4).SequenceEqual(old.R.Hp.Take(4))&&migrated.R.EnemyHp.SequenceEqual(old.R.EnemyHp)&&migrated.R.Round==old.R.Round&&!migrated.R.Casual,"four-pet save preserves existing combat");
        Check(game.JoinTeam(4,1)&&game.S.Team.SequenceEqual(new[]{0,4,3}),"seed joins a selected slot");Check(!game.JoinTeam(4,0)&&!game.JoinTeam(5,0),"duplicate and invalid pets rejected");
        Check(game.SetCaretaker(2)&&game.Caretaker==2&&!game.SetCaretaker(4),"only a resting pet can tend crops");
        game.S.Crystals=0;int xp=game.S.Xp[4];Check(!game.EvolveSeed()&&game.S.Xp[4]==xp&&game.S.SeedStage==0,"locked evolution spends nothing");game.S.Berries=2;Check(game.Feed(4)&&game.S.SeedBond==1&&game.S.Xp[4]==12,"seed care raises growth and bond");
        for(int stage=0;stage<3;stage++){
            game.S.Xp[4]=new[]{20,110,300}[stage];game.S.SeedBond=game.SeedRequiredBond;int cost=game.SeedEvolutionCost;game.S.Crystals=cost-1;
            Check(!game.EvolveSeed()&&game.S.Crystals==cost-1,"insufficient crystals leave stage intact");game.S.Crystals=cost;int previousXp=game.S.Xp[4],hp=game.MaxHp(4),attack=game.Attack(4);
            Check(game.EvolveSeed()&&game.S.SeedStage==stage+1&&game.S.Crystals==0&&game.S.Xp[4]==previousXp&&game.MaxHp(4)==hp+6&&game.Attack(4)==attack+2,"evolution consumes crystals once and improves stats");
        }
        Check(!game.EvolveSeed(),"final stage cannot evolve again");var saved=new FarmGame(game.SavePath);Check(saved.S.SeedStage==3&&saved.S.SeedBond==6,"evolution persists");
        for(int stage=0;stage<4;stage++){
            var combat=New(root);combat.S.Xp=Enumerable.Repeat(55,5).ToArray();combat.S.SeedStage=stage;combat.SetPoisonPreset();JourneyTests.Start(combat,0,3);
            Check(!combat.JoinTeam(1,0)&&!combat.SetCaretaker(2)&&!combat.Feed(4)&&!combat.EvolveSeed(),"party and growth locked during journey");
            combat.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(combat.R.Phase=="result"&&combat.R.Outcome=="victory"&&combat.R.PoisonTriggers>0&&combat.R.PoisonDamage>0,"every seed form can use poison in a full trip");
            if(stage==3)Check(combat.R.PoisonSpreads>0,"final form transfers poison");
            report.AppendLine("Seed stage "+(stage+1)+": "+combat.R.Outcome+", poison damage="+combat.R.PoisonDamage+", applications="+combat.R.PoisonTriggers+", transfers="+combat.R.PoisonSpreads);
        }
        var seedCave=New(root);seedCave.S.SeedStage=1;Check(seedCave.CaveUnlocked&&!seedCave.S.Evolved,"seed evolution independently unlocks the cave");JourneyTests.Start(seedCave,1,3);
        var fire=New(root);fire.S.Xp=Enumerable.Repeat(55,5).ToArray();fire.S.Team=new[]{1,4,3};fire.SetSkill(4,1);JourneyTests.Start(fire,0,2);fire.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(fire.R.CatalystTriggers>0,"fire and poison produce catalytic attacks");
        var pause=New(root);pause.SetPoisonPreset();JourneyTests.Start(pause,0,0);pause.RouteChoice(0);pause.Step();pause.ToggleJourneyPause();pause.Save();var paused=new FarmGame(pause.SavePath);Check(paused.R.JourneyPaused&&paused.R.Poison.SequenceEqual(pause.R.Poison)&&paused.R.PoisonDamage==pause.R.PoisonDamage,"poison and pause survive reload");
        var maxed=New(root);maxed.S.Xp=Enumerable.Repeat(1250,5).ToArray();maxed.S.Berries=30;maxed.S.Crystals=11;
        Check(!maxed.CanFeed(-1)&&!maxed.Feed(5)&&!maxed.Feed(1),"invalid and fully grown pets cannot consume berries");
        maxed.S.Berries=1;Check(!maxed.Feed(4)&&maxed.S.SeedBond==0,"bond feeding still requires two berries");maxed.S.Berries=30;
        for(int i=0;i<3;i++)Check(maxed.Feed(0)&&maxed.S.Xp[0]==1250,"max-level bubble gains bond without XP");
        Check(!maxed.CanFeed(0)&&maxed.Evolve(),"bubble can finish evolution after leveling only through training");
        for(int i=0;i<6;i++)Check(maxed.Feed(4)&&maxed.S.Xp[4]==1250,"max-level seed gains all required evolution bond");
        Check(maxed.S.Berries==12&&!maxed.Feed(4)&&maxed.S.SeedBond==6,"bond feeding stops when final evolution requirement is met");
        for(int i=0;i<3;i++)Check(maxed.EvolveSeed(),"max-level seed can complete each evolution");
        Check(maxed.S.Crystals==0&&maxed.S.SeedStage==3&&!maxed.Train(4)&&!maxed.Feed(4),"completed pet cannot waste training or feeding resources");
        var maxReload=new FarmGame(maxed.SavePath);Check(maxReload.S.Xp[4]==1250&&maxReload.S.SeedBond==6&&maxReload.S.SeedStage==3&&maxReload.S.Evolved,"max-level evolution and bond persist");
        var away=New(root);away.S.Xp[4]=1250;JourneyTests.Start(away,0,0);Check(!away.CanFeed(4)&&!away.Feed(4)&&away.S.SeedBond==0,"max-level bond feeding remains locked during a trip");
        report.AppendLine("PASS: fifth pet migration, party/caretaker assignment, staged evolution, growth locks, four forms, poison combinations, max-level bond recovery and persistence.");return report.ToString();
    }
}

public partial class FarmWindow
{
    public void BackgroundPetChecks(string root)
    {
        G.Home();Panel=new FarmPanel(this);Panel.Tab="伙伴";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"pets-roster.png"),924,721);
        Panel.RenderSeedRosterCheck(root);Panel.Tab="进化";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"pets-evolution-small.png"),764,571);FarmHeadlessTests.Render(Panel,Path.Combine(root,"pets-evolution.png"),924,721);
        for(int stage=0;stage<4;stage++){G.S.SeedStage=stage;G.S.DisplayPet=4;G.S.DesktopMode="pet";BuildDesktop();FarmHeadlessTests.Render(this,Path.Combine(root,"seed-desktop-"+(stage+1)+".png"),Width,Height);}
        G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.SetPoisonPreset();JourneyTests.Start(G,0,0);G.RouteChoice(0);G.Step();Panel.Tab="冒险";Panel.Render();FarmHeadlessTests.Render(Panel,Path.Combine(root,"pets-poison-battle.png"),924,721);
        BackgroundPetSwitcherChecks(root);
        FarmHeadlessTests.Check(!IsVisible&&!Panel.IsVisible,"new pet tests never show windows");
    }
}
