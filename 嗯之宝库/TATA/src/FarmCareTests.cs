using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

public static class FarmCareTests
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception("Care: "+label);}
    private static FarmGame New(string root){var g=new FarmGame(Path.Combine(root,"care-"+Guid.NewGuid().ToString("N")+".xml"));g.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;return g;}
    public static string Run(string root)
    {
        var g=New(root);Check(g.CurrentCarePlan==null&&!g.SetCareTarget(9,false)&&!g.SetCareTarget(2,true),"invalid and nonexistent evolution targets rejected");
        g.SetCareTarget(0,true);var p=g.CurrentCarePlan;Check(p.Feeds==3&&p.Trainings==2&&p.Berries==6&&p.Grain==6&&p.Crystals==2&&p.AfterXp==72&&p.AfterBond==3&&p.AfterLevel==3,"bubble plan includes bond feeding before remaining training");
        g.S.Grain=5;g.S.Berries=10;Check(!g.ApplyCareTraining()&&g.S.Grain==5&&g.S.Berries==10&&g.S.Xp[0]==0&&g.S.Bond==0,"one missing material prevents all spending and growth");
        g.S.Grain=6;int[] team=(int[])g.S.Team.Clone();Check(g.ApplyCareTraining()&&g.S.Grain==0&&g.S.Berries==4&&g.S.Xp[0]==72&&g.S.Bond==3&&g.S.Crystals==0&&g.S.Team.SequenceEqual(team),"training exactly follows quote without spending crystals or changing party");
        Check(!g.ApplyCareTraining()&&!g.CompleteCareEvolution()&&!g.CurrentCarePlan.Done,"prepared evolution waits for crystals and cannot retrain");g.S.Crystals=2;Check(g.CompleteCareEvolution()&&g.S.Evolved&&g.S.Crystals==0&&g.CurrentCarePlan.Done&&!g.CompleteCareEvolution(),"evolution is explicit and cannot repeat");
        g.SetCareTarget(1,false);g.S.Grain=6;p=g.CurrentCarePlan;Check(p.Feeds==0&&p.Grain==6&&p.AfterXp==36&&p.AfterLevel==2,"ordinary next-level plan matches existing training batches");g.ApplyCareTraining();Check(g.CurrentCarePlan.Done&&g.CurrentCarePlan.Level==2&&!g.ApplyCareTraining(),"completed level target stays fixed instead of rolling forward");
        g.SetCareTarget(4,true);g.S.Berries=4;g.S.Grain=0;p=g.CurrentCarePlan;Check(p.Feeds==2&&p.Trainings==0&&p.AfterXp==24,"seed first evolution is satisfied by bond feeding alone");g.ApplyCareTraining();g.S.Crystals=1;g.CompleteCareEvolution();g.SetCareTarget(4,true);p=g.CurrentCarePlan;Check(p.Level==4&&p.Stage==2&&p.Feeds==2&&p.Trainings==4&&p.AfterXp==120&&p.Crystals==3,"seed next stage uses current experience and next-stage conditions");
        g.S.Xp[4]=1250;g.S.SeedBond=2;g.S.Berries=4;p=g.CurrentCarePlan;Check(p.Feeds==2&&p.Trainings==0&&p.AfterXp==1250&&p.AfterLevel==10,"full level can prepare bond without wasting growth");g.ApplyCareTraining();Check(g.S.SeedBond==4&&g.S.Xp[4]==1250,"max level batch feeds only the missing bond");
        g.S.Crystals=3;g.CompleteCareEvolution();g.SetCareTarget(4,true);p=g.CurrentCarePlan;Check(p.Stage==3&&p.Level==6&&p.RequiredBond==6&&p.Feeds==2&&p.Grain==0&&p.Crystals==5,"final seed target uses level six, bond six and five crystals");
        g.S.SeedStage=0;g.S.SeedBond=0;g.S.Xp[4]=1240;g.SetCareTarget(4,true);p=g.CurrentCarePlan;Check(p.AfterXp==1252&&p.AfterBond==2,"feeding stops growth at the same cap as single feeds");
        g.SetCareTarget(2,false);g.S.Grain=100;g.S.Berries=100;g.S.BlessingOffers=new[]{0,1,3};g.BeginJourney(0,0);g.ToggleJourneyPause();int grain=g.S.Grain;long next=g.R.NextJourneyTicks;Check(!g.ApplyCareTraining()&&!g.CompleteCareEvolution()&&g.S.Grain==grain&&g.R.JourneyPaused&&g.R.NextJourneyTicks==next,"away cultivation preserves paused trip and materials");g.Retreat();g.Home();
        g.R.Perks=new int[12];
        return FinishChecks(root,g);
    }
    private static string FinishChecks(string root,FarmGame g)
    {
        g.R.Perks[6]=2;g.R.Meal=1;Check(g.PermanentHp(2)==42+g.Level(2)*6,"permanent preview excludes legacy temporary buffs");g.R.Perks[6]=0;g.R.Meal=0;
        var crops=New(root);crops.S.Crops=new[]{0,0,1,1,0,0};crops.S.Growth=new[]{30.0,0,0,0,0,0};
        Check(Math.Abs(crops.CareResourceWait(0,2)-15)<.01&&Math.Abs(crops.CareResourceWait(0,4)-45)<.01&&crops.CareResourceWait(2,1)<0&&crops.CareResourceWait(0,0)==0,"wait estimate respects staggered batches and unplanted resources");
        crops.S.SoilLevel=2;crops.SetCaretaker(4);double wait=crops.CareResourceWait(0,20);var now=DateTime.UtcNow;crops.S.LastFarmTicks=now.Ticks;crops.S.Berries=0;crops.TickFarm(now.AddSeconds(Math.Max(0,wait-.1)));Check(crops.S.Berries<20,"forecast does not claim crop resources before harvest");crops.TickFarm(now.AddSeconds(wait+.1));Check(crops.S.Berries>=20,"forecast matches actual farm production after soil and helper bonuses");
        g.S.DesktopHidden=true;g.S.DisplayPet=3;g.Save();var reloaded=new FarmGame(g.SavePath);Check(reloaded.CurrentCarePlan.Pet==2&&!reloaded.CurrentCarePlan.Evolution&&reloaded.CurrentCarePlan.Level==2&&reloaded.S.DesktopHidden&&reloaded.S.DisplayPet==3,"fixed care target survives reload without altering display");
        var xml=XDocument.Load(g.SavePath);xml.Root.Element("CareTarget").Remove();string old=Path.Combine(root,"pre-care.xml");xml.Save(old);var migrated=new FarmGame(old);Check(migrated.SavePath==old&&migrated.CurrentCarePlan==null&&migrated.S.Xp.SequenceEqual(g.S.Xp)&&migrated.S.GardenDecorations.SequenceEqual(g.S.GardenDecorations),"old save gets no invented target and preserves progress");
        g.S.CareTarget=new CareTarget{Pet=4,Evolution=true,Stage=99,Level=6};g.Save();Check(g.CurrentCarePlan==null,"malformed optional care target repaired without discarding save");
        return "PASS: exact bond-first feeding and training quote; atomic insufficient resources; separate evolution spending; fixed completed goals; all seed stage requirements; max-level feeding cap; active-trip locks; permanent stat preview; staggered harvest estimates; save/reload and old-save compatibility.";
    }
}

public partial class FarmPanel
{
    private void CareRender(string root,string name,double width=924,double height=721){Render();UIPreview(root,name,width,height);}
    internal void RenderCareChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;G.S.DisplayPet=4;
        Tab="农场";CareRender(root,"care-home-entry");UIAction("培养详情").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-seed-small",764,571);check(Tab=="培养"&&G.CurrentCarePlan.Pet==4&&G.CurrentCarePlan.Evolution,"home plans care for displayed pet");UIVisible(UIAction("一次完成这步培养"),"training visible in minimum care page");UIVisible(UIAction("确认进化 · 晶石 1"),"separate evolution action visible in minimum care page");
        UIAction("培养泡泡").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-bubble");check(G.CurrentCarePlan.Pet==0&&G.CurrentCarePlan.Berries==6&&G.CurrentCarePlan.Grain==6&&!UIAction("一次完成这步培养").IsEnabled,"pet selection changes real quote and disables unaffordable action");
        UIAction("安排麦穗种植").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-planting-small",764,571);check(Tab=="农场"&&careFarmResource==1&&plantingOpen&&selectedPlanting==2,"shortage opens grain planting preview directly");var before=(int[])G.S.Crops.Clone();int changed=Enumerable.Range(0,G.Plots).Count(i=>G.S.Crops[i]!=G.PlantingPlan(2)[i]);UIVisible(UIAction("应用方案 · 调整 "+changed+" 块田"),"planting apply visible in small linked planner");check(G.S.Crops.SequenceEqual(before),"opening resource source never replants automatically");
        UIAction("应用方案 · 调整 "+changed+" 块田").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-planting-applied");check(G.S.Crops.Take(G.Plots).SequenceEqual(G.PlantingPlan(2))&&G.S.Crops.Skip(G.Plots).SequenceEqual(before.Skip(G.Plots)),"explicit crop plan applies only to unlocked plots");UIAction("返回培养计划").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-returned");check(Tab=="培养"&&G.CurrentCarePlan.Pet==0,"return resumes original care target");
        G.S.Grain=6;G.S.Berries=6;G.S.Crystals=0;CareRender(root,"care-ready-small",764,571);UIVisible(UIAction("一次完成这步培养"),"bubble care action visible at minimum size");UIAction("一次完成这步培养").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-trained");check(G.Level(0)==3&&G.S.Bond==3&&G.S.Berries==0&&G.S.Grain==0&&!G.S.Evolved&&!UIAction("确认进化 · 晶石 2").IsEnabled&&careReceipt.Contains("攻击"),"training receipt shows results while evolution waits for crystals");
        UIAction("准备去小溪找晶石").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-route-small",764,571);check(Tab=="冒险"&&G.AtHome&&journeyDifficulty==0&&journeyDepth==0,"crystal route stops at preparation");
        G.S.Crystals=2;OpenCare();CareRender(root,"care-evolution-ready-small",764,571);UIVisible(UIAction("确认进化 · 晶石 2"),"ready evolution visible in minimum window");UIAction("确认进化 · 晶石 2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-evolved");check(G.S.Evolved&&G.S.Crystals==0&&G.CurrentCarePlan.Done&&careReceipt.Contains("泡泡 → 潮汐兽"),"explicit evolution records before and after and completes fixed goal");
        UIAction("继续下个成长目标").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-next-level");check(!G.CurrentCarePlan.Evolution&&G.CurrentCarePlan.Level==4,"next target only advances on explicit action");
        G.S.BlessingOffers=new[]{0,1,3};G.BeginJourney(0,0);G.ToggleJourneyPause();OpenCare(4,true);CareRender(root,"care-away");int round=G.R.Round;long next=G.R.NextJourneyTicks;check(!UIAction("一次完成这步培养").IsEnabled&&!UIAction("准备去小溪找晶石").IsEnabled&&G.R.JourneyPaused&&G.R.Round==round&&G.R.NextJourneyTicks==next,"viewing another target cannot resume travel or permit cultivation");
        G.Retreat();G.Home();G.S.SeedStage=2;G.S.Xp[4]=1250;G.S.SeedBond=4;G.S.Berries=4;OpenCare(4,true);CareRender(root,"care-max-bond");UIAction("一次完成这步培养").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CareRender(root,"care-max-bond-done");check(G.S.SeedBond==6&&G.S.Xp[4]==1250,"max-level bond preparation works through actual UI");
        Go("农场");CareRender(root,"care-home-target");check(careFarmResource==-1&&UIChildren<Button>((DependencyObject)Content).Any(b=>Convert.ToString(b.Content)=="查看培养计划"),"normal farm navigation clears resource subpage and exposes saved goal");check(!IsVisible&&!owner.IsVisible,"care checks never show windows");
        File.WriteAllText(Path.Combine(root,"care-ui-regression.txt"),"PASS: displayed-pet entry, live quote, minimum-window training/evolution, resource planner preview and explicit application, return to fixed goal, receipts, crystal route without departure, completed goal, active-trip locks, max-level bond and home goal routing. Offscreen controls and isolated saves only.");
    }
}
