using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static class FarmReadingTests
{
    private static void Fixture(string path,FarmGame game){XDocument.Parse(FarmPracticeTests.Xml(game.S)).Save(path);}
    private static void FirstChoice(FarmGame game)
    {
        game.S.BlessingOffers=new[]{0,1,2};FarmHeadlessTests.Check(game.BeginJourney(0,0),"idle fixture departs");int turns=0;
        while(game.R.Phase!="inspiration"&&game.JourneyRunning&&turns++<80)game.AdvanceJourney(new DateTime(game.R.NextJourneyTicks,DateTimeKind.Utc));
        FarmHeadlessTests.Check(game.R.Phase=="inspiration","idle fixture reaches a genuine inspiration");
    }
    public static string Run(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;string path=Path.Combine(root,"reading-normal.xml");var game=new FarmGame(path);
        game.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;game.Feed(0);int berries=game.S.Berries,xp=game.S.Xp[0];
        check(game.SetReadingSize(2)&&!game.SetReadingSize(2)&&!game.SetReadingSize(-1)&&!game.SetReadingSize(3),"reading choices are bounded and idempotent");
        var reload=new FarmGame(path);check(reload.S.ReadingSize==2&&reload.S.Berries==berries&&reload.S.Xp[0]==xp&&reload.LoadWarning=="","reading preference survives reload without resetting progress");
        var xml=XDocument.Load(path);xml.Root.Element("ReadingSize").Remove();string old=Path.Combine(root,"reading-v129.xml");xml.Save(old);var oldGame=new FarmGame(old);check(oldGame.S.ReadingSize==0&&oldGame.S.Xp[0]==xp,"old saves get the familiar default text size");
        xml=XDocument.Load(path);xml.Root.Element("ReadingSize").Value="999";string invalid=Path.Combine(root,"reading-invalid.xml");xml.Save(invalid);var repaired=new FarmGame(invalid);check(repaired.S.ReadingSize==2&&repaired.S.Xp[0]==xp&&repaired.SavePath==invalid,"invalid presentation setting is repaired without replacing the save");
        var staying=new FarmGame(null,null,true);staying.S.GuideStep=3;staying.S.GuideInWorld=true;staying.S.GuideFarmSeen=true;staying.S.GuideHomeStage=HomeGuideStage.StayingHome;staying.S.Xp[0]=36;staying.S.ReadingSize=2;
        var expected=new FarmGame(null,null,true);expected.S.Xp[0]=36;DateTime start=DateTime.UtcNow.AddHours(-10);staying.S.LastFarmTicks=start.Ticks;expected.S.LastFarmTicks=start.Ticks;expected.TickFarm(start.AddHours(4));
        string idle=Path.Combine(root,"reading-idle-home.xml");Fixture(idle,staying);var idleHome=new FarmGame(idle);
        check(idleHome.AtHome&&idleHome.S.GuideHomeStage==HomeGuideStage.StayingHome&&!idleHome.OpenGuideOnLaunch&&idleHome.S.ReadingSize==2&&idleHome.S.Xp[0]==36,"long absence keeps the deferred invitation and reading preference");
        check(idleHome.S.Berries==expected.S.Berries&&idleHome.S.Grain==expected.S.Grain&&idleHome.S.Harvests==expected.S.Harvests,"ten-hour absence settles the existing four-hour farm cap once");
        var paused=new FarmGame(null,null,true);paused.S.Xp=Enumerable.Repeat(55,5).ToArray();paused.S.GuideStep=4;paused.S.GuideInWorld=true;FirstChoice(paused);paused.ToggleJourneyPause();
        string runBefore=FarmPracticeTests.Xml(paused.R);paused.SetReadingSize(2);check(FarmPracticeTests.Xml(paused.R)==runBefore,"changing reading size never touches journey state or timing");
        int[] hp=(int[])paused.R.Hp.Clone(),offers=(int[])paused.R.InspirationOffers.Clone();paused.S.LastFarmTicks=DateTime.UtcNow.AddHours(-6).Ticks;paused.R.NextJourneyTicks=DateTime.UtcNow.AddHours(-6).Ticks;
        string pausedPath=Path.Combine(root,"reading-idle-paused.xml");Fixture(pausedPath,paused);var pausedReload=new FarmGame(pausedPath);
        check(pausedReload.R.JourneyPaused&&pausedReload.R.Phase=="inspiration"&&pausedReload.R.Hp.SequenceEqual(hp)&&pausedReload.R.InspirationOffers.SequenceEqual(offers)&&pausedReload.R.InspirationPicks.Length==0&&pausedReload.S.Harvests>0,"long absence grows crops while preserving paused choices and health");
        var auto=new FarmGame(null,null,true);auto.S.Xp=Enumerable.Repeat(55,5).ToArray();auto.S.GuideStep=4;auto.S.GuideInWorld=true;auto.S.ReadingSize=1;FirstChoice(auto);auto.S.LastFarmTicks=DateTime.UtcNow.AddHours(-6).Ticks;auto.R.NextJourneyTicks=DateTime.UtcNow.AddHours(-6).Ticks;
        string autoPath=Path.Combine(root,"reading-idle-auto.xml");Fixture(autoPath,auto);var settled=new FarmGame(autoPath);settled.SyncGuide();
        check(settled.R.Phase=="result"&&settled.R.Outcome=="victory"&&settled.R.InspirationPicks.Length==3&&settled.S.JourneyMemories.Length==1&&settled.S.GuideStep==5,"unpaused absence completes only the current trip and resumes on the return receipt");
        var settledAgain=new FarmGame(autoPath);check(settledAgain.S.Wins==settled.S.Wins&&settledAgain.S.Fertilizer==settled.S.Fertilizer&&settledAgain.S.Crystals==settled.S.Crystals&&settledAgain.S.JourneyMemories.Length==1,"reopening again cannot duplicate trip rewards or start another trip");
        var test=new FarmGame(path,null,true);test.SetReadingSize(2);test.ResetGuideTest();check(test.S.ReadingSize==0&&new FarmGame(path).S.ReadingSize==2,"tutorial reset clears its preference without touching normal progress");
        return "PASS: saved/default/bounded reading preferences; original save compatibility; ten-hour home absence and four-hour farm cap; persistent deferral; paused offline inspiration; single automatic trip settlement; no repeated rewards; reading changes do not alter journeys; fresh test isolation.";
    }
}
