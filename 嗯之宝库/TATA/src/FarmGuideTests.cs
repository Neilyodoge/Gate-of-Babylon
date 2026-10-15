using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Xml.Linq;

public static class FarmGuideTests
{
    public static string Run(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        check(FarmProgram.GuideMutexName.EndsWith("-v"+FarmProgram.Version)&&FarmProgram.GuideWindowTitle.EndsWith("v"+FarmProgram.Version),"test instance identity is version-specific and cannot reset the older test");
        string folder=Path.Combine(root,"guide-isolation");Directory.CreateDirectory(folder);string target=Path.Combine(folder,"farm-save.xml"),legacy=Path.Combine(folder,"save.xml");
        File.WriteAllText(target,"Do not read or overwrite this existing progress.");File.WriteAllText(legacy,"Legacy progress sentinel.");
        string before=File.ReadAllText(target);var g=new FarmGame(target,legacy,true);
        check(g.GuideTest&&g.SavePath==null&&g.LoadWarning==""&&g.S.GuideStep==0&&g.S.Berries==10&&g.S.Grain==4,"guide test ignores both supplied saves and starts in memory");
        g.Feed(0);g.AdvanceGuideTest();g.Save();check(g.S.Harvests>0&&g.S.Xp[0]==12,"fast forward uses real crop production");
        g.S.GuideFarmSeen=true;g.ResetGuideTest();check(g.S.GuideStep==0&&!g.S.GuideFarmSeen&&g.S.Harvests==0&&g.S.Xp.All(x=>x==0)&&g.S.Berries==10&&g.S.Grain==4&&g.AtHome,"reset clears growth harvest journey and revealed pages");
        var second=new FarmGame(target,legacy,true);check(second.S.Xp.All(x=>x==0)&&second.S.GuideStep==0&&File.ReadAllText(target)==before&&Directory.GetFiles(folder).Length==2&&File.ReadAllText(legacy)=="Legacy progress sentinel.","relaunch is fresh and creates no save or backup");
        string normal=Path.Combine(root,"guide-normal.xml");var normalGame=new FarmGame(normal);check(normalGame.S.GuideStep==0,"new normal game offers guide");normalGame.S.GuideStep=2;normalGame.S.GuideInWorld=true;normalGame.Feed(0);normalGame.Save();var resumed=new FarmGame(normal);check(resumed.S.GuideStep==2&&resumed.S.GuideInWorld&&resumed.S.Xp[0]==12&&resumed.GuideActive,"normal mode saves and resumes in-world guide progress");
        string unchanged=FarmPracticeTests.Xml(resumed.S);check(!resumed.ResetGuideTest()&&!resumed.AdvanceGuideTest()&&FarmPracticeTests.Xml(resumed.S)==unchanged,"test reset and fast forward cannot change normal progress");
        var xml=XDocument.Load(normal);xml.Root.Element("GuideStep").Remove();string old=Path.Combine(root,"guide-old.xml");xml.Save(old);var oldGame=new FarmGame(old);check(oldGame.S.GuideStep==-1&&!oldGame.GuideActive&&oldGame.S.Xp[0]==12,"old saves retain progress without forced onboarding");
        normalGame.S.GuideStep=9;normalGame.Save();check(!new FarmGame(normal).GuideActive,"skipped guide stays skipped in normal mode");
        normalGame.S.GuideStep=8;normalGame.S.SimpleCourtyard=true;normalGame.Save();var quietReload=new FarmGame(normal);
        check(quietReload.S.SimpleCourtyard&&!quietReload.OpenGuideOnLaunch&&quietReload.S.Xp[0]==12,"completed simple courtyard and growth survive normal reload without reopening guide");
        quietReload.S.SimpleCourtyard=false;quietReload.Save();check(!new FarmGame(normal).S.SimpleCourtyard,"explicit full interface preference survives reload");
        var prior=XDocument.Load(normal);prior.Root.Element("SimpleCourtyard").Remove();prior.Save(old);check(!new FarmGame(old).S.SimpleCourtyard,"v1.27 and earlier saves keep their familiar full interface");
        var home=new FarmGame(null,null,true);home.S.GuideInWorld=true;home.S.GuideStep=2;home.Feed(0);home.Feed(0);home.SyncGuide();
        home.AdvanceGuideTest();check(home.S.GuideHomeStage==HomeGuideStage.Harvest,"harvests before discovering the farm do not finish the food-source lesson");
        home.S.GuideFarmSeen=true;for(int i=0;i<home.Plots;i++)home.Plant(i,1);
        home.AdvanceGuideTest();check(home.S.GuideHomeStage==HomeGuideStage.Harvest,"grain harvests cannot count as fresh berries");
        home.Plant(0,0);home.AdvanceGuideTest();check(home.S.GuideHomeStage==HomeGuideStage.Harvest,"existing berries cannot stand in for a new harvest");
        home.AdvanceGuideTest();check(home.S.GuideHomeStage==HomeGuideStage.FreshBerries&&home.S.GuideHarvestBerries==home.CropYield(0),"new harvest is recorded from actual berry production");
        home.Feed(2);check(home.S.GuideHomeStage==HomeGuideStage.FreshBerries,"another companion's feeding does not finish this lesson");
        home.Feed(0);check(home.S.GuideHomeStage==HomeGuideStage.Complete&&home.AtHome,"real companion feeding completes basic care without an outing");
        check(home.RestAfterHomeGuide()&&!home.RestAfterHomeGuide(),"basic completion starts the companionship interval exactly once");
        home.TickFarm(DateTime.UtcNow.AddSeconds(10));check(home.S.GuideHomeStage==HomeGuideStage.Resting,"invitation does not interrupt the quiet interval");
        home.TickFarm(DateTime.UtcNow.AddSeconds(31));check(home.S.GuideHomeStage==HomeGuideStage.Invitation&&home.AtHome,"later invitation does not start a journey");
        check(home.DeferGuideOuting()&&!home.DeferGuideOuting(),"invitation can be declined once without repeated prompts");
        string homeSave=Path.Combine(root,"guide-staying-home.xml");XDocument.Parse(FarmPracticeTests.Xml(home.S)).Save(homeSave);var staying=new FarmGame(homeSave);
        staying.TickFarm(DateTime.UtcNow.AddHours(2));staying.Save();var stayingAgain=new FarmGame(homeSave);
        check(stayingAgain.AtHome&&stayingAgain.S.GuideHomeStage==HomeGuideStage.StayingHome&&!stayingAgain.S.GuideOutingAccepted&&stayingAgain.HomeGuideResumeTab=="农场"&&!stayingAgain.OpenGuideOnLaunch,"declining survives time, save and reload without forced adventure or reopening the tutorial window");
        check(!FarmUX.Activity(stayingAgain,false).Contains("冒险"),"compact tooltip keeps the quiet companionship wording");
        var oldGuide=XDocument.Load(homeSave);foreach(string field in new[]{"GuideHomeStage","GuideHarvestBerries","GuideRestUntilTicks","GuideOutingAccepted"})oldGuide.Root.Element(field).Remove();
        string oldGuidePath=Path.Combine(root,"guide-v126.xml");oldGuide.Save(oldGuidePath);var compatible=new FarmGame(oldGuidePath);
        check(compatible.S.Xp.SequenceEqual(stayingAgain.S.Xp)&&compatible.HomeGuideResumeTab=="农场"&&compatible.S.GuideHomeStage==HomeGuideStage.Harvest,"v1.26 guide continues at the food source without losing growth or forcing adventure");
        return "PASS: in-memory sessions ignore supplied saves; reset/relaunch are fresh and never save; actual new berry harvest required; fresh companion feeding closes basic care; quiet interval and optional invitation; deferral persists without repeats; normal and v1.26 guide persistence; old-save opt-in; skip persistence.";
    }
}

public partial class FarmWindow
{
    internal void BackgroundGuideLaunchChecks()
    {
        string before=FarmPracticeTests.Xml(G.S);bool handled=false;DesktopMessage(IntPtr.Zero,0x8002,IntPtr.Zero,IntPtr.Zero,ref handled);
        if(G.GuideTest)FarmHeadlessTests.Check(handled&&G.S.GuideStep==0&&G.AtHome&&G.S.Berries==10&&G.S.Xp.All(x=>x==0)&&G.SavePath==null&&!IsVisible,"second test launch resets only memory without native resources");
        else FarmHeadlessTests.Check(!handled&&FarmPracticeTests.Xml(G.S)==before,"normal window ignores guide-reset messages");
    }
}
