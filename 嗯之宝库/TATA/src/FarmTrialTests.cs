using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

// Acceptance uses the real new-game stock and public player actions. Only the
// clock advances faster; no XP, currencies, wins or unlocks are assigned.
public static class FarmTrialTests
{
    private static void Check(bool ok,string message){FarmHeadlessTests.Check(ok,"Trial: "+message);}
    private static string Stamp(DateTime start,DateTime now){return (now-start).TotalMinutes.ToString("0.00")+" 分钟";}
    private static void Tick(FarmGame g,ref DateTime now){now=now.AddSeconds(2);g.TickFarm(now);g.AdvanceJourney(now);}
    private static void WaitFor(FarmGame g,ref DateTime now,Func<bool> ready)
    {
        DateTime deadline=now.AddMinutes(30);while(!ready()&&now<deadline)Tick(g,ref now);Check(ready(),"resources arrive within 30 simulated minutes");
    }
    private static void Snapshot(FarmGame g,string root,string name,string tab)
    {
        var w=new FarmWindow(g,true);w.Panel=new FarmPanel(w);w.Panel.Tab=tab;w.Panel.Render();FarmHeadlessTests.Render(w.Panel,Path.Combine(root,"ui-trial-play-"+name+".png"),924,721);
    }
    private static bool Trip(ref FarmGame g,ref DateTime now,DateTime start,int region,int depth,StringBuilder report,bool pause)
    {
        g.EnsureJourneyOffers();int blessing=g.S.BlessingOffers[0];DateTime departed=now;int[] xp=(int[])g.S.Xp.Clone();
        Check(g.BeginJourney(region,blessing,depth),"explicit departure");g.R.NextJourneyTicks=now.AddSeconds(6).Ticks;g.Save();
        if(pause){
            for(int i=0;i<8;i++)Tick(g,ref now);g.ToggleJourneyPause();g.R.NextJourneyTicks=now.AddSeconds(4).Ticks;g.Save();
            int node=g.R.Node,round=g.R.Round,harvests=g.S.Harvests;var hp=(int[])g.R.Hp.Clone();
            g=new FarmGame(g.SavePath);Check(g.LoadWarning==""&&g.R.JourneyPaused,"paused journey survives reopen");
            for(int i=0;i<30;i++)Tick(g,ref now);
            Check(g.R.Node==node&&g.R.Round==round&&g.R.Hp.SequenceEqual(hp)&&g.S.Harvests>harvests,"pause freezes journey while farm keeps producing");
            g.ToggleJourneyPause();g.R.NextJourneyTicks=now.AddSeconds(4).Ticks;g.Save();
        }
        DateTime limit=now.AddMinutes(12);while(g.R.Phase!="result"&&now<limit)Tick(g,ref now);Check(g.R.Phase=="result","automatic trip finishes without input");
        Check(g.S.Xp.SequenceEqual(xp),"automatic combat does not invent permanent XP");
        report.AppendLine("- "+Stamp(start,now)+"："+FarmGame.ExpeditionName(region,depth)+"，"+(g.R.Outcome=="victory"?"通关":"未通关")+"；本趟 "+(now-departed).TotalSeconds.ToString("0")+" 秒"+(pause?"（含暂停 60 秒）":"")+"；肥料 +"+g.R.RewardFertilizer+"、晶石 +"+g.R.RewardCrystals+"；祝福「"+FarmGame.BlessingNames[blessing]+"」。");
        bool won=g.R.Outcome=="victory";g.Save();string saved=FarmPracticeTests.Xml(g.S);g=new FarmGame(g.SavePath);Check(g.LoadWarning==""&&FarmPracticeTests.Xml(g.S)==saved,"finished journey reload preserves rewards exactly");return won;
    }
    private static void GrowParty(FarmGame g,ref DateTime now,int level)
    {
        foreach(int pet in g.S.Team){while(g.Level(pet)<level){int cost=g.TrainToNextCost(pet);WaitFor(g,ref now,()=>g.S.Grain>=cost);Check(g.TrainToNext(pet),"earned grain upgrades teammate");}}
    }
    public static string Run(string root)
    {
        var report=new StringBuilder();report.AppendLine("# 第一版试玩验收 · v"+FarmProgram.Version);report.AppendLine();
        report.AppendLine("本记录使用全新隔离存档，以 2 秒步长推进游戏时间。资源、等级、通关和进化均通过正常玩法获得；没有直接加材料或等级。时间为自动验收中的模拟时间，不代表真人试玩时长。每次返家立即安排下一步，不计玩家阅读与思考时间。祝福使用正常生成的第一项，旅途灵感由小队自动选择，不带便当。");report.AppendLine();
        var g=new FarmGame(Path.Combine(root,"trial-fresh-save.xml"));DateTime start=new DateTime(g.S.LastFarmTicks,DateTimeKind.Utc),now=start;
        Check(g.S.Berries==10&&g.S.Grain==4&&g.S.Crystals==0&&g.S.Xp.All(x=>x==0)&&g.S.Wins==0,"genuine default save");
        Snapshot(g,root,"start","农场");Check(g.Feed(0)&&g.Feed(0)&&g.Level(0)==2,"two starting feeds reach level two");
        report.AppendLine("- 0.00 分钟：初始莓果 10、麦穗 4；喂泡泡两次，花莓果 4，升至 Lv.2。默认队伍为泡泡、电团、苔壳。");
        int attempts=0;bool win=false;while(!win&&attempts++<3){win=Trip(ref g,ref now,start,0,0,report,false);if(!win){g.Home();GrowParty(g,ref now,Math.Min(4,attempts+1));}}
        Check(win&&g.S.GardenUnlocked&&g.S.Crystals>=2&&g.S.Decorations.Any(x=>x),"first creek clear opens crops and grants evolution/decor resources");Snapshot(g,root,"first-return","冒险");
        int fertilizer=g.S.Fertilizer,soilCost=g.SoilCost;Check(g.Home()&&g.UpgradeSoil()&&g.S.SoilLevel==1&&g.S.Fertilizer==fertilizer-soilCost,"first rewards upgrade actual soil");g.ApplyPlanting(3);
        report.AppendLine("- "+Stamp(start,now)+"：返家，花肥料 "+soilCost+" 升土壤 Lv.1；6 块田已开放，安排莓果、麦穗、薄荷各 2 块。小循环完成。");
        for(int stage=1;stage<=3;stage++){
            Check(g.SetCareTarget(4,true),"set next seed evolution target");var plan=g.CurrentCarePlan;int berryCost=plan.Berries,grainCost=plan.Grain,crystalCost=plan.Crystals;
            WaitFor(g,ref now,()=>g.S.Berries>=berryCost&&g.S.Grain>=grainCost);Check(!plan.HasTraining||g.ApplyCareTraining(),"real farm supplies seed growth");
            int runs=0;while(g.S.Crystals<crystalCost&&runs++<8){bool cleared=Trip(ref g,ref now,start,0,0,report,stage==2&&runs==1);g.Home();if(!cleared)GrowParty(g,ref now,Math.Min(5,runs+2));}
            Check(g.S.Crystals>=crystalCost&&g.EvolveSeed()&&g.S.SeedStage==stage,"evolution acquired through real crops and trips");
            report.AppendLine("- "+Stamp(start,now)+"：种子第 "+(stage+1)+" 形态「"+g.Name(4)+"」，Lv."+g.Level(4)+" / 亲密 "+g.S.SeedBond+"；本阶培养花莓果 "+berryCost+"、麦穗 "+grainCost+"，进化花晶石 "+crystalCost+"。");
        }
        Snapshot(g,root,"seed-four-forms","进化");Check(g.CaveUnlocked,"evolution opens cave");
        Check(g.SetCareTarget(0,true),"bubble evolution target");var bubble=g.CurrentCarePlan;WaitFor(g,ref now,()=>g.S.Berries>=bubble.Berries&&g.S.Grain>=bubble.Grain);if(bubble.HasTraining)Check(g.ApplyCareTraining(),"bubble cultivation");
        while(g.S.Crystals<2){Check(Trip(ref g,ref now,start,0,0,report,false),"repeat creek for bubble crystals");g.Home();}Check(g.Evolve(),"bubble evolution uses earned crystals");
        report.AppendLine("- "+Stamp(start,now)+"：泡泡进化为潮汐兽；全田生长额外加速 15%。");
        int[] targets={2,4,6,5,7,9};
        for(int route=1;route<6;route++){
            GrowParty(g,ref now,targets[route]);bool cleared=false;for(int attempt=0;attempt<3&&!cleared;attempt++){
                cleared=Trip(ref g,ref now,start,route/3,route%3,report,false);g.Home();if(!cleared)GrowParty(g,ref now,Math.Min(10,targets[route]+attempt+1));
            }Check(cleared,"all six destinations accessible using earned growth");
        }
        g.ClaimReadyChapters();Check(g.S.ExpeditionClears.All(x=>x>0)&&g.S.SeedStage==3&&g.S.Evolved,"first playtest progression complete");g.Save();var reload=new FarmGame(g.SavePath);Check(FarmPracticeTests.Xml(g.S)==FarmPracticeTests.Xml(reload.S),"final progress reload is exact");
        report.AppendLine();report.AppendLine("验收结果：PASS。两条进化线、六条路线、自动种植与奖励回流均可完成。暂停/重开保留战况，暂停期间农田继续生产，完成后重开不会重复发奖。所有成长目标奖励仅在最后领取，前述进度不依赖这些额外奖励。");
        report.AppendLine("本次终点：模拟 "+Stamp(start,now)+"；土壤 Lv."+g.S.SoilLevel+"；种子四阶、泡泡已进化；6/6 路线通关；队伍 "+String.Join(" / ",g.S.Team.Select(p=>g.Name(p)+" Lv."+g.Level(p)).ToArray())+"。本条验收路线只升了一次土壤；继续施肥、调整种植比例或提前领取成长奖励，可以缩短后期等材料的时间。");
        report.AppendLine("独立回归另外覆盖：缩小与隐藏、鼠标中键切宠、三档尺寸、多屏位置、暂停动画、输入锁定、旧存档兼容，以及种植、配队、便当、灵感、战报与重复操作。全部使用后台检查与离屏绘制，不创建可见窗口，不访问正式存档。");
        return report.ToString();
    }
}

public partial class FarmPanel
{
    private void TrialPreview(string root,string name,double width=764,double height=571){resetScroll=true;Render();UIPreview(root,"trial-"+name,width,height);}
    internal void RenderTrialChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;Go("农田");TrialPreview(root,"farm");
        var controls=UIChildren<ComboBox>((DependencyObject)Content).Where(c=>AutomationProperties.GetName(c).StartsWith("田地 ")).ToArray();check(controls.Length==4,"fresh farm exposes four open crop controls");foreach(var c in controls)UIVisible(c,"farm control visible at minimum size");UIVisible(UIAction("返回农场首页"),"farm return visible");
        G.S.GardenUnlocked=true;TrialPreview(root,"farm-six");controls=UIChildren<ComboBox>((DependencyObject)Content).Where(c=>AutomationProperties.GetName(c).StartsWith("田地 ")).ToArray();check(controls.Length==6,"unlocked farm exposes six plots");foreach(var c in controls)UIVisible(c,"six plot controls visible at minimum size");G.S.GardenUnlocked=false;
        Go("配队");TrialPreview(root,"workshop");UIVisible(UIAction("去冒险验证搭配"),"workshop primary visible");UIVisible(UIAction("缺麦穗？安排种植"),"shortage action visible");check(!workshopOptionsOpen,"advanced workshop options start collapsed");string before=FarmPracticeTests.Xml(G.S);
        UIAction("缺麦穗？安排种植").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"farm-linked");check(Tab=="农田"&&selectedPlanting==2&&plantingOpen&&FarmPracticeTests.Xml(G.S)==before,"shortage routes to preview without spending or replanting");UIVisible(UIAction("返回配队"),"contextual return visible");
        UIAction("返回配队").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"workshop-back");check(Tab=="配队"&&FarmPracticeTests.Xml(G.S)==before,"return leaves state intact");
        int previousHelper=G.Caretaker;var helper=UIChildren<ComboBox>((DependencyObject)Content).First(c=>AutomationProperties.GetName(c)=="农场帮手");UIVisible(helper,"helper selection visible at minimum size");helper.SelectedIndex=helper.SelectedIndex==0?1:0;TrialPreview(root,"helper");check(G.Caretaker!=previousHelper&&!G.S.Team.Contains(G.Caretaker)&&G.S.Grain==4,"helper changes freely and stays off party");
        G.S.Grain=100;TrialPreview(root,"training-ready");int cost=G.PartyTrainingCost,grain=G.S.Grain;var train=UIAction("全队升一级 · 麦穗 "+cost);train.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"trained");check(G.S.Grain==grain-cost&&G.S.Team.All(p=>G.Level(p)==2),"batch training spends displayed price");before=FarmPracticeTests.Xml(G.S);train.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale team training button cannot buy another level");
        G.S.Xp[4]=300;G.S.SeedBond=6;G.S.Crystals=20;Go("进化");SelectEvolutionFamily(0);before=FarmPracticeTests.Xml(G.S);TrialPreview(root,"bubble");UIAction("种子进化").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"seed");check(FarmPracticeTests.Xml(G.S)==before&&evolutionFocusPet==4,"family switch only previews");UIVisible(UIAction("进化为「"+FarmGame.SeedNames[1]+"」"),"seed evolve visible");
        var evolve=UIAction("进化为「"+FarmGame.SeedNames[1]+"」");evolve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"seed-evolved");check(G.S.SeedStage==1&&G.S.Crystals==19&&evolutionReceipt.Contains("晶石 −1"),"seed evolution receipt matches actual spending");before=FarmPracticeTests.Xml(G.S);evolve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale evolution action cannot spend for second stage");
        UIAction("泡泡进化").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));TrialPreview(root,"bubble-return");check(evolutionReceipt==""&&evolutionFocusPet==0,"other family clears prior receipt");G.S.Xp[0]=55;G.S.Bond=3;TrialPreview(root,"bubble-ready");UIVisible(UIAction("进化 · 消耗晶石 2"),"bubble actual cost visible");
        G.EnsureJourneyOffers();G.BeginJourney(0,G.S.BlessingOffers[0]);G.ToggleJourneyPause();Go("配队");TrialPreview(root,"workshop-away");check(G.R.JourneyPaused&&UIChildren<ComboBox>((DependencyObject)Content).All(c=>!c.IsEnabled)&&!UIAction("全队升一级 · 麦穗 "+G.PartyTrainingCost).IsEnabled,"paused journey locks team edits and training");
        Go("进化");SelectEvolutionFamily(4);TrialPreview(root,"seed-away");check(G.R.JourneyPaused&&!UIAction("进化为「"+FarmGame.SeedNames[2]+"」").IsEnabled,"paused travel cannot evolve");
        G.Retreat();G.Home();G.S.SeedStage=3;TrialPreview(root,"seed-complete");check(!UIChildren<Button>((DependencyObject)Content).Any(b=>Convert.ToString(b.Content)=="准备种子进化材料"),"final seed form does not suggest another evolution");UIAction("继续培养种子").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="培养"&&G.CurrentCarePlan.Pet==4&&!G.CurrentCarePlan.Evolution,"final form links to ordinary growth");
        check(!owner.IsVisible&&!IsVisible,"trial checks remain detached");File.WriteAllText(Path.Combine(root,"trial-ux-regression.txt"),"PASS: farm controls and returns at minimum size; shortage-plan-return path without spending; free helper selection; exact batch cost and stale-click guard; family preview versus evolution; exact evolution receipt and stale-stage guard; paused trip locks; final form routes to ordinary cultivation.");
    }
}
