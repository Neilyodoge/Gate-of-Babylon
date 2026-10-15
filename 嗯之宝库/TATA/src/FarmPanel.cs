using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel:Window
{
    readonly FarmWindow owner;FarmGame G{get{return owner.G;}}
    public string Tab="农场";bool autoplay=false,confirmRetreat=false;int speed=1,meal=0;
    private TextBlock feedback;private Grid body;private ScrollViewer scroller;private bool resetScroll;
    private string lastTab="",lastPhase="",flash="",flashTab="",flashPhase="";private DateTime flashUntil;
    private HomeGuideStage renderedHomeGuideStage;
    private List<FarmScene> scenes=new List<FarmScene>();private List<Tuple<int,Image>> portraits=new List<Tuple<int,Image>>();
    private List<Action> updates=new List<Action>();
    public bool IsBattleRunning{get{return G.R.Casual?G.JourneyRunning:autoplay&&IsVisible&&WindowState!=WindowState.Minimized&&Tab=="冒险"&&G.R.Phase=="battle";}}
    public FarmPanel(FarmWindow w)
    {
        owner=w;journeyDifficulty=G.S.PlannedRegion;journeyDepth=G.S.PlannedDepth;Title="苔间 · 农场与冒险";Width=940;Height=760;MinWidth=780;MinHeight=610;Background=U.Cream;FontFamily=new FontFamily("Microsoft YaHei");Foreground=U.Ink;UseLayoutRounding=true;WindowStartupLocation=WindowStartupLocation.Manual;Topmost=G.S.DesktopTopmost;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);FarmTheme.Apply(this);InitializePanelInput();
        if(!G.AtHome)Tab="冒险";
        Closing+=(s,e)=>{if(!owner.Exiting){e.Cancel=true;owner.ShowCompact();}};
        StateChanged+=(s,e)=>{if(WindowState==WindowState.Minimized)PauseForBackground();owner.SyncPanelPresence();};
        IsVisibleChanged+=(s,e)=>{if(!IsVisible)PauseForBackground();owner.SyncPanelPresence();};
    }
    public void PauseForBackground(){ClearCardFeedback();autoplay=false;CloseSmallRetreat();}
    private void Go(string tab){CheckSkillCareScope(tab);CheckChapterReturnScope(tab);if(tab=="农场"||tab=="伙伴"||tab=="进化"||tab=="新手引导")ClearCrystalPreparation();CloseSmallRetreat();if(Tab!=tab){ClearLifeCareResult();courtyardVisit++;if(tab=="莓果田")berryForEvolution=SimpleCourtyard&&Tab=="进化";if(tab=="进化"&&Tab!="莓果田"){smallGrowthStep=0;smallGrowthBefore="";smallGrowthRevision++;}if(tab=="施肥")courtyardSoilReceipt=null;}if(Tab=="冒险"&&tab!="冒险"&&G.AtHome)departureSession++;RememberPage(tab);GuideNavigated(tab);if(tab!="冒险")PauseForBackground();if(tab=="农场")careFarmResource=-1;else homeSoilReceipt="";if(tab=="农田"&&Tab!="农田")farmReturnTab="农场";moreNavigationOpen=false;confirmRetreat=false;Tab=tab;}
    private void Do(Action action)
    {
        try{string before=G.S.Notice;int[] levels=Enumerable.Range(0,FarmGame.PetCount).Select(p=>G.Level(p)).ToArray();action();
            if(G.S.Notice!=before){flash=G.S.Notice;for(int p=0;p<FarmGame.PetCount;p++)if(G.Level(p)>levels[p]&&!G.S.Notice.Contains(G.Name(p)+"升至 Lv."+G.Level(p)))flash+="  "+G.Name(p)+"升至 Lv."+G.Level(p)+"！";flashTab=Tab;flashPhase=G.R.Phase;flashUntil=DateTime.UtcNow.AddSeconds(6);}
            owner.Refresh();
        }catch(Exception ex){MessageBox.Show("这次操作未完成："+ex.Message,"苔间");}
    }
    private Button Btn(string label,Action action,bool enabled=true,bool primary=false){return U.Button(label,()=>Do(action),enabled,primary);}
    public void RefreshCounters()
    {
        int[] values={G.S.Berries,G.S.Grain,G.S.Mint,G.S.Fertilizer,G.S.Crystals};for(int i=0;i<5;i++)if(resourceNumbers[i]!=null){resourceNumbers[i].Text=FarmTheme.ResourceCount(values[i]);resourceNumbers[i].ToolTip=values[i].ToString("N0");System.Windows.Automation.AutomationProperties.SetName(resourceNumbers[i],values[i].ToString());}
        if(feedback!=null){bool showFlash=DateTime.UtcNow<flashUntil&&flashTab==Tab&&flashPhase==G.R.Phase;feedback.Text=showFlash?flash:WorldGuide?WorldGuideHint():Tab=="新手引导"?(G.GuideTest?"引导测试 · 所有操作仅在本次会话生效。":"新手引导 · 随时可跳过，也能离开后接着完成。"):FarmUX.Activity(G,IsBattleRunning);feedback.Foreground=showFlash?U.Green:U.Muted;feedback.ToolTip=feedback.Text;}
        foreach(var update in updates)update();foreach(var view in companionViews)view.InvalidateVisual();foreach(var farmScene in scenes)if(farmScene.ShowBerryBasket||farmScene.SmallTravel)farmScene.InvalidateVisual();
    }
    public void Animate(int tick){if(!IsVisible||WindowState==WindowState.Minimized)return;TickCardFeedback();if(cardScene!=null){cardScene.Tick=tick;cardScene.InvalidateVisual();}foreach(var view in companionViews){if(G.S.DesktopMotion)view.Tick=tick;view.InvalidateVisual();}foreach(var s in scenes){s.Tick=tick;s.InvalidateVisual();}foreach(var item in portraits)item.Item2.Source=Art.Frame(item.Item1,tick,G.S.Evolved,G.S.SeedStage);foreach(var item in seedPreviews)item.Item2.Source=Art.Frame(4,tick,false,item.Item1);}
    public void Pulse(){Pulse(IsVisible,WindowState==WindowState.Minimized);}
    internal void Pulse(bool visible,bool minimized)
    {
        if(!visible||minimized)return;
        if(Tab=="卡牌冒险")return;
        if(WorldGuide){int step=G.S.GuideStep;G.SyncGuide();if(step!=G.S.GuideStep||renderedHomeGuideStage!=G.S.GuideHomeStage){Render();return;}}
        if(Tab=="新手引导"){int step=G.S.GuideStep;G.SyncGuide();if(step!=G.S.GuideStep||lastPhase!=G.R.Phase||journeyNode!=G.R.Node)Render();else RefreshCounters();return;}
        if(G.AtHome){RefreshCounters();return;}
        if(G.R.Casual){if(lastPhase!=G.R.Phase||journeyNode!=G.R.Node||(Tab=="复盘"&&reviewMemory==null&&reviewRound!=G.R.Round))Render();else RefreshCounters();return;}
        if(!IsBattleRunning)return;string phase=G.R.Phase;
        for(int i=0;i<speed&&G.R.Phase=="battle";i++)G.Step();
        if(G.R.Phase!=phase){autoplay=false;Render();}else RefreshCounters();
    }
    private Image Portrait(int p,double height=84){var im=new Image{Source=Art.Frame(p,owner.Tick,G.S.Evolved,G.S.SeedStage),Height=height,Stretch=Stretch.Uniform};portraits.Add(Tuple.Create(p,im));return im;}
    private FarmScene Scene(bool battle=false){var s=new FarmScene(G,battle){Tick=owner.Tick,Height=battle?242:168,MinHeight=battle?242:168,Margin=new Thickness(0,0,0,10)};scenes.Add(s);return s;}
    private TextBlock Live(Func<string> get,double size=13,Brush color=null){var t=U.Text(get(),size,color);updates.Add(()=>t.Text=get());return t;}
    public void Render()
    {
        if(Tab=="卡牌冒险"){RenderCards();return;}
        CheckSkillCareScope(Tab);CheckChapterReturnScope(Tab);
        if(!SmallRetreatVisible&&smallRetreatRun!=null)CloseSmallRetreat();
        RouteGuide();
        renderedHomeGuideStage=G.S.GuideHomeStage;
        Title=QuietShell||GentleGuide&&G.S.GuideStep<=3&&!G.S.GuideOutingAccepted?"苔间 · 泡泡的小院":"苔间 · 农场与冒险";
        ApplyGentleWindowSize();
        if(Tab=="新手引导")G.SyncGuide();
        double offset=resetScroll?0:navigationOffset.HasValue?navigationOffset.Value:scroller!=null&&lastTab==Tab&&lastPhase==G.R.Phase?pendingScrollOffset??scroller.VerticalOffset:0;navigationOffset=null;resetScroll=false;lastTab=Tab;lastPhase=G.R.Phase;journeyNode=G.R.Node;
        scenes.Clear();portraits.Clear();companionViews.Clear();seedPreviews.Clear();updates.Clear();
        readingSettingsCard=null;BuildPanelShell();
        if(Tab=="找莓果")GuideBerriesPage();else if(SimpleCourtyard&&LifeTab)LifePage();else if(Tab=="新手引导")GuidePage();else if(Tab=="农场"){if(careFarmResource>=0)CarePlantingPage();else HomeOverview();}else if(Tab=="目的地")SmallDestinationPage();else if(Tab=="施肥")CourtyardSoilPage();else if(Tab=="小纪念")CourtyardKeepsakePage();else if(Tab=="莓果田")QuietBerryPage();else if(Tab=="农田")FarmDetails();else if(Tab=="归家")HomecomingPage();else if(Tab=="便当")PicnicPage();else if(Tab=="培养")CarePage();else if(Tab=="小院")GardenStudio();else if(Tab=="伙伴")Pets();else if(Tab=="伙伴图鉴")PetCatalogue();else if(Tab=="配队")PartyWorkshop();else if(Tab=="冒险")Adventure();else if(Tab=="复盘")BattleReviewPage();else if(Tab=="试练")PracticePage();else if(Tab=="进化")EvolutionPage();else if(Tab=="手册")Handbook();else if(Tab=="详细规则")DetailedHelp();else Help();
        ApplyReadingSize();RefreshCounters();Animate(owner.Tick);RestorePanelScroll(offset);FinishPanelInput();
    }
    private StackPanel[] Columns(double right=236){body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(right)});var left=new StackPanel{Margin=new Thickness(0,0,14,0)};var aside=new StackPanel();Grid.SetColumn(aside,1);body.Children.Add(left);body.Children.Add(aside);return new[]{left,aside};}
    private void Heading(Panel p,string title,string description){p.Children.Add(U.Text(title,21));p.Children.Add(U.Text(description,13,U.Muted));}
    private UIElement Notice(string title,string description){var p=new StackPanel();p.Children.Add(U.Text(title,16,U.Green));p.Children.Add(U.Text(description,13));var c=U.Card(p,12);c.Background=U.Pale;return c;}
    private void Farm()
    {
        var cols=Columns();var left=cols[0];var right=cols[1];Heading(left,"农场正在替你积攒资源","作物成熟会自动收获、补种。选择种什么即可，无需手动浇水。");left.Children.Add(Scene());
        left.Children.Add(Live(()=>G.LastHarvest,12,U.Green));var grid=new UniformGrid{Columns=3};left.Children.Add(grid);
        for(int i=0;i<6;i++){
            int plot=i;var p=new StackPanel();p.Children.Add(U.Text("田地 "+(i+1),14));
            if(i>=G.Plots){p.Children.Add(U.Text("尚未解锁",14,U.Muted));p.Children.Add(U.Text("击败小溪领主\n自动增加 2 块田",12,U.Muted));}
            else{
                var combo=new ComboBox{ItemsSource=FarmGame.CropNames.Take(G.S.GardenUnlocked?3:2).ToArray(),SelectedIndex=G.S.Crops[i],FontSize=13,Padding=new Thickness(5),Margin=new Thickness(0,0,0,7)};combo.SelectionChanged+=(s,e)=>{if(combo.SelectedIndex>=0&&combo.SelectedIndex!=G.S.Crops[plot])Do(()=>G.Plant(plot,combo.SelectedIndex));};p.Children.Add(combo);
                p.Children.Add(Live(()=>G.SecondsLeft(plot)+" 秒后 +"+G.CropYield(G.S.Crops[plot]),13,U.Green));
                var bar=U.Bar(0,100);updates.Add(()=>bar.Value=100*G.S.Growth[plot]/G.CropSeconds(G.S.Crops[plot]));p.Children.Add(bar);
                p.Children.Add(U.Text(new[]{"用途：喂养伙伴","用途：训练","用途：薄荷点心"}[G.S.Crops[plot]],11,U.Muted));
            }
            var c=U.Card(p,10);c.Margin=new Thickness(0,0,7,8);grid.Children.Add(c);
        }
        left.Children.Add(U.Text("改种会重新计时；离线最多结算 4 小时收成。",12,U.Muted));
        FarmImprovements(left,right);
        var task=new StackPanel();task.Children.Add(U.Text("下一步",18));task.Children.Add(Live(()=>!G.AtHome?FarmUX.Activity(G,IsBattleRunning):G.NextChapterHint,14));
        task.Children.Add(Btn("成长目标与奖励",()=>OpenHandbook(0),true,true));task.Children.Add(Btn(G.AtHome?"准备下一趟冒险":FarmUX.ActionLabel(G),()=>Go("冒险")));right.Children.Add(U.Card(task));
        var jobs=new StackPanel();jobs.Children.Add(U.Text("留守照料",16));jobs.Children.Add(Portrait(G.Caretaker,78));jobs.Children.Add(U.Text(G.Name(G.Caretaker)+"："+G.CaretakerEffect,13));if(G.S.Evolved)jobs.Children.Add(U.Text("潮汐雨露：生长额外 +15%",12,U.Green));jobs.Children.Add(Btn("查看队伍与分工",()=>Go("伙伴")));right.Children.Add(U.Card(jobs));
        right.Children.Add(U.Text("资源怎么花？",15));right.Children.Add(U.Text("莓果 → 喂养、增加亲密\n麦穗 → 训练\n薄荷 → 点心、成长 +24\n晶石 → 永久进化",12,U.Muted));if(G.LoadWarning!="")right.Children.Add(U.Text(G.LoadWarning,12,U.Gold));
    }
    private UIElement EvolutionCard()
    {
        var p=new StackPanel();var row=new DockPanel();p.Children.Add(row);var art=Portrait(0,64);art.Width=76;DockPanel.SetDock(art,Dock.Left);row.Children.Add(art);var info=new StackPanel{Margin=new Thickness(8,0,0,0)};row.Children.Add(info);
        info.Children.Add(U.Text(G.S.Evolved?"潮汐兽 · 永久进化已完成":"下一次进化：泡泡 → 潮汐兽",19));
        if(!G.S.Evolved)info.Children.Add(U.Text((G.Level(0)>=3?"✓ ":"○ ")+"等级 "+G.Level(0)+" / 3     "+(G.S.Bond>=3?"✓ ":"○ ")+"亲密 "+G.S.Bond+" / 3     "+(G.S.Crystals>=2?"✓ ":"○ ")+"晶石 "+G.S.Crystals+" / 2",13));
        info.Children.Add(U.Text(FarmUX.EvolutionHint(G),13,U.Green));
        if(!G.S.Evolved){
            var actions=new StackPanel{Orientation=Orientation.Horizontal};p.Children.Add(actions);
            if(G.CanEvolve){actions.Children.Add(Btn("进化 · 消耗晶石 2",()=>G.Evolve(),true,true));}
            else if(!G.AtHome){actions.Children.Add(Btn("继续冒险，返家后培养",()=>Go("冒险"),true,true));}
            else if(FarmUX.FeedsNeeded(G)>0){actions.Children.Add(FeedButton(0,true));actions.Children.Add(Btn("去小溪获取晶石",()=>Go("冒险")));}
            else actions.Children.Add(Btn("去冒险获取晶石",()=>Go("冒险"),true,true));
            p.Children.Add(U.Text("进化后：开场群体潮湿 + 农场生长速度 15% + 新冒险区域。",12,U.Muted));
            p.Children.Add(Btn("准备泡泡进化材料",()=>OpenCare(0,true)));
        }
        return U.Card(p,13);
    }
    private void PartySummary(StackPanel aside)
    {
        var team=new StackPanel();team.Children.Add(U.Text("队伍状态",16));for(int i=0;i<3;i++){int p=G.S.Team[i];team.Children.Add(Live(()=>G.Name(p)+"  "+G.R.Hp[p]+" / "+G.MaxHp(p)+(G.R.Hp[p]==0?"  暂时倒下":""),12));var b=U.Bar(G.R.Hp[p],G.MaxHp(p));updates.Add(()=>{b.Maximum=G.MaxHp(p);b.Value=Math.Max(0,G.R.Hp[p]);});team.Children.Add(b);}aside.Children.Add(U.Card(team,12));
        var perks=new StackPanel();perks.Children.Add(U.Text("已获得的本局能力",16));bool any=false;
        for(int i=0;i<12;i++)if(G.R.Perks[i]>0){any=true;perks.Children.Add(U.Text(FarmGame.PerkNames[i]+(FarmGame.PerkLimit(i)>1?" · "+G.R.Perks[i]+"级":""),13,U.Green));perks.Children.Add(U.Text(FarmUX.PerkEffect(i,G.R.Perks[i]),12,U.Muted));}
        if(!any)perks.Children.Add(U.Text(G.R.Phase=="draft"?"在左侧选一项，这里就会显示当前效果。":"战斗胜利后，三选一获得新能力。",12,U.Muted));
        if(G.R.Meal>0)perks.Children.Add(U.Text(G.R.Meal==1?"便当：体力上限 +12":"薄荷汤：战后恢复 +9",12,U.Gold));aside.Children.Add(U.Card(perks,12));
        aside.Children.Add(Btn("回农场看看 · 保留冒险",()=>Go("农场")));
        if(G.R.Phase!="result"){
            if(!confirmRetreat){var b=Btn("提前结束这趟冒险…",()=>{confirmRetreat=true;autoplay=false;});b.Background=U.Cream;aside.Children.Add(b);}
            else{var confirm=new StackPanel();confirm.Children.Add(U.Text("现在返家？",15));confirm.Children.Add(U.Text("已经获得的成长和资源保留。本次临时能力将在下次出发时重新选择。",12,U.Muted));U.Row(confirm,Btn("继续这趟",()=>confirmRetreat=false),Btn("确认返家",()=>{G.Retreat();confirmRetreat=false;},true,true));aside.Children.Add(U.Card(confirm,10));}
        }
    }
    private void Adventure()
    {
        if(SmallDeparture){FirstDeparturePage();return;}
        if(SmallRepeatDeparture){RepeatDeparturePage();return;}
        if(SmallJourney){SmallJourneyPage();return;}
        if(FirstSmallJourney){FirstSmallJourneyPage();return;}
        if(SmallHomecoming&&G.R.Phase=="result"){FirstHomecomingPage();return;}
        var cols=Columns();var left=cols[0];var aside=cols[1];var r=G.R;
        if(G.AtHome){PrepareJourney(left,aside);return;}
        if(r.Casual){JourneyPage(left,aside);return;}
        left.Children.Add(Notice("旧版旅程已保留", "原节点、体力与能力不变。可让小队自动走完剩余路程，之后使用新玩法。"));left.Children.Add(Btn("接着自动走完这趟",()=>G.AdoptLegacyJourney(),true,true));
        var nodes=new UniformGrid{Columns=5,Margin=new Thickness(0,0,0,12)};
        for(int i=0;i<5;i++){string name=new[]{"草地战斗","分岔路","营地","关口战斗","首领"}[i];bool done=i<r.Node||(i==r.Node&&(r.Phase=="draft"||r.Phase=="result"));var box=new StackPanel{Margin=new Thickness(3,0,3,0)};var number=U.Text(done?"✓":""+(i+1),17,i==r.Node?U.Green:U.Muted);number.TextAlignment=TextAlignment.Center;box.Children.Add(number);var text=U.Text(name,11,i==r.Node?U.Ink:U.Muted);text.TextAlignment=TextAlignment.Center;box.Children.Add(text);nodes.Children.Add(box);}left.Children.Add(nodes);
        PartySummary(aside);
        if(r.Phase=="battle")Battle(left);
        else if(r.Phase=="route")Route(left);
        else if(r.Phase=="draft")Draft(left);
        else Result(left);
    }
    private void Prepare(StackPanel left,StackPanel aside)
    {
        Heading(left,"出发前，只需确认队伍","伙伴会自动战斗；你负责选择路线和奖励。随时能暂停，冒险不消耗体力门票。");
        var party=new UniformGrid{Columns=3};for(int i=0;i<3;i++){int p=G.S.Team[i];var card=new StackPanel();card.Children.Add(U.Text((i+1)+"号 · "+(i==0?"前排":"后排"),12,U.Muted));card.Children.Add(Portrait(p,83));card.Children.Add(U.Text(G.Name(p)+"  Lv."+G.Level(p),16));card.Children.Add(U.Text(FarmGame.Roles[p],12,U.Muted));var c=U.Card(card,10);c.Margin=new Thickness(0,0,7,10);party.Children.Add(c);}left.Children.Add(party);
        left.Children.Add(Btn("调整站位 / 更换伙伴",()=>Go("伙伴")));
        left.Children.Add(Notice("这支队伍怎么配合",G.S.Team.Contains(0)&&G.S.Team.Contains(2)?"让泡泡先出手挂潮湿，电团随后追击。苔壳提供治疗护盾，赤钳提供持续灼烧。":"队伍按站位顺序行动。前排承担主要伤害，苔壳负责续航，赤钳适合持续输出。"));
        left.Children.Add(Notice("这一趟会得到什么","5 个节点：战斗 → 选路 → 营地 → 关口 → 首领。\n途中获得成长、莓果与临时能力；首领奖励晶石 2，首次通关解锁薄荷和两块田。"));
        var plan=new StackPanel();plan.Children.Add(U.Text("料理 · 可选",17));var combo=new ComboBox{ItemsSource=new[]{"轻装出发 · 免费","莓果便当","薄荷汤"},SelectedIndex=meal,FontSize=13,Padding=new Thickness(7),Margin=new Thickness(0,3,0,9)};
        combo.SelectionChanged+=(s,e)=>{if(combo.SelectedIndex>=0){meal=combo.SelectedIndex;RefreshCounters();}};plan.Children.Add(combo);plan.Children.Add(Live(()=>FarmUX.MissingMeal(G,meal),13,U.Muted));
        var start=Btn("开始小溪远足",()=>Start(0),true,true);updates.Add(()=>start.IsEnabled=FarmUX.CanCook(G,meal));plan.Children.Add(start);
        plan.Children.Add(U.Text("推荐第一次先走普通路线，在营地根据体力选择休息或额外能力。",12,U.Muted));
        var cave=Btn(G.CaveUnlocked?"挑战潮汐洞穴":"潮汐洞穴 · 尚未解锁",()=>Start(1),G.CaveUnlocked);updates.Add(()=>cave.IsEnabled=G.CaveUnlocked&&FarmUX.CanCook(G,meal));plan.Children.Add(cave);if(!G.CaveUnlocked)plan.Children.Add(U.Text("解锁条件：泡泡或种子完成首次进化。",12,U.Muted));aside.Children.Add(U.Card(plan));
        aside.Children.Add(U.Text("点击开始不会扣除普通出发费用；选择料理才会消耗对应材料。",12,U.Muted));
    }
    private void Battle(StackPanel left)
    {
        left.Children.Add(Live(()=>G.NodeName+" · 第 "+G.R.Round+" 回合",20));
        left.Children.Add(Live(()=>autoplay?"自动战斗中 · 不需要手动攻击":"已暂停 · 点击下方「继续自动战斗」",14,U.Green));
        var toggle=U.Button("",()=>{autoplay=!autoplay;RefreshCounters();},true,true);updates.Add(()=>toggle.Content=autoplay?"暂停自动战斗":"继续自动战斗");
        var step=U.Button("只前进 1 回合",()=>{autoplay=false;string phase=G.R.Phase;G.Step();if(G.R.Phase!=phase)Render();else RefreshCounters();});
        var rate=U.Button("",()=>{speed=speed==1?2:1;RefreshCounters();});updates.Add(()=>rate.Content="速度 ×"+speed);U.Row(left,toggle,step,rate);
        left.Children.Add(Live(()=>G.Intent,13,U.Gold));left.Children.Add(Scene(true));
        var log=new StackPanel();log.Children.Add(U.Text("本回合反馈",15));log.Children.Add(Live(()=>G.R.Log,13));left.Children.Add(U.Card(log,12));
        left.Children.Add(U.Text("绿色条是伙伴体力，橙色条是敌人体力；「+盾」会先抵挡伤害。每场胜利后会停止，等你选奖励。",12,U.Muted));
    }
    private void Route(StackPanel left)
    {
        var r=G.R;Heading(left,"第 "+(r.Node+1)+" / 5 站 · "+G.NodeName,"队伍正在等待你的决定。点击下面的选项，才会继续前进。");
        if(!String.IsNullOrEmpty(r.Log))left.Children.Add(Notice("上一步",r.Log));
        if(r.Node==2){
            int missing=G.S.Team.Sum(p=>G.MaxHp(p)-r.Hp[p]);int healed=G.S.Team.Sum(p=>Math.Min(G.MaxHp(p)-r.Hp[p],(int)Math.Ceiling(G.MaxHp(p)*.4)));
            left.Children.Add(U.Text(missing==0?"全队体力已满，可以探索古树获得额外能力。":"全队共缺 "+missing+" 体力；休息预计恢复 "+healed+"。",14,U.Green));
            U.Row(left,Choice("围炉休息",missing==0?"体力已满，这个选项会直接前往关口。":"全队各恢复 40% 最大体力，再前往关口。",missing==0?"直接前往关口":"休息并前进",()=>G.RouteChoice(0),false),Choice("探索古树","不恢复体力。额外获得一次能力三选一，之后前往关口。","挑选额外能力",()=>G.RouteChoice(1),missing==0));
        }else if(r.Node==0||r.Node==4){
            var info=new StackPanel();info.Children.Add(U.Text(r.Node==4?"首领战 · "+G.NodeName:"第一场 · 溪口草地",18));info.Children.Add(U.Text(r.Node==4?"领主每三回合横扫全队。打赢获得晶石 "+(r.Difficulty==0?2:3)+"，首次胜利解锁新田地和薄荷。":"伙伴会按 1 → 2 → 3 的顺序自动出手。胜利后获得成长、莓果，并挑选一项能力。",14));
            info.Children.Add(Btn(r.Node==4?"开始首领战":"开始第一场战斗",()=>{autoplay=true;G.RouteChoice(0);},true,true));left.Children.Add(U.Card(info));
        }else{
            U.Row(left,Choice("沿溪小路 · 普通","敌人较弱。胜利后每位伙伴成长 +6、莓果 +2，并选择能力。","走普通路线",()=>{autoplay=true;G.RouteChoice(0);},true),Choice("精英巢穴 · 较难","敌人体力与攻击更高。胜利后成长 +10、莓果 +2，额外晶石 +1，并选择能力。","挑战精英",()=>{autoplay=true;G.RouteChoice(1);},false));
            left.Children.Add(U.Text("莓果奖励会受到本局「甜蜜战利品」加成；战斗后的体力会带入下一场。",12,U.Muted));
        }
    }
    private void Draft(StackPanel left)
    {
        var r=G.R;Heading(left,r.Node==2?"找到古树秘藏 · 选 1 项能力":"战斗胜利！选 1 项能力继续","三选一，点击后立即生效，并进入下一站。能力仅在这趟冒险中有效。");
        if(r.Node!=2)left.Children.Add(U.Text(String.IsNullOrEmpty(r.LastReward)?"本场成长与资源已经自动入账，无需另行领取。":r.LastReward,13,U.Green));
        var grid=new UniformGrid{Columns=3};left.Children.Add(grid);
        foreach(int id in r.Offers){int p=id;int level=r.Perks[p]+1;bool available=r.Perks[p]<FarmGame.PerkLimit(p);var box=new StackPanel();
            box.Children.Add(U.Text(FarmUX.PerkRole(p),12,U.Muted));box.Children.Add(U.Text(FarmGame.PerkNames[p],18,U.Green));box.Children.Add(U.Text(r.Perks[p]>0?"升级："+r.Perks[p]+" → "+level+" 级":"获得新能力",12,U.Gold));
            box.Children.Add(U.Text(FarmUX.PerkEffect(p,level),14));
            box.Children.Add(Btn(available?"选择这个能力":"已达到上限",()=>G.Pick(p),available,true));
            box.Children.Add(new Expander{Header="搭配建议",Content=U.Text(FarmUX.PerkAdvice(G,p),12,U.Muted),FontSize=12,Margin=new Thickness(0,6,0,0)});
            var card=U.Card(box,11);card.Margin=new Thickness(0,0,7,10);grid.Children.Add(card);
        }
        left.Children.Add(U.Text("选完后无需再点领取。已获得的能力可以在右侧查看；队伍会等你选择下一站。",13,U.Muted));
    }
    private void Result(StackPanel left)
    {
        var r=G.R;Heading(left,r.Outcome=="victory"?"冒险完成，收获已入库":"小队平安回来，途中收获保留",r.Outcome=="victory"?"接下来回家培养伙伴，或看看刚解锁的新内容。":"可以调整队伍、喂养或准备料理，再来挑战。");
        if(r.RewardBerries>=0){var stats=new UniformGrid{Columns=3};foreach(string item in new[]{"莓果 +"+r.RewardBerries,"晶石 +"+r.RewardCrystals,"每位成长 +"+r.RewardXp}){var t=U.Text(item,16,U.Green);var c=U.Card(t,10);c.Margin=new Thickness(0,0,7,10);stats.Children.Add(c);}left.Children.Add(stats);}else left.Children.Add(U.Text(r.LastReward,14,U.Green));
        if(r.UnlockedGarden)left.Children.Add(Notice("首次通关解锁","新增田地 5、6，以及月光薄荷。回农场后可在田地菜单中改种薄荷，用于制作恢复料理。"));
        left.Children.Add(Notice("泡泡的进化进度",FarmUX.EvolutionHint(G).Replace(" 冒险结束返家后即可培养或进化。","")));
        left.Children.Add(Btn("返家 · 查看成长与进化",()=>{G.Home();Go("伙伴");},true,true));left.Children.Add(Btn("返家 · 查看农田",()=>{G.Home();Go("农场");}));
        var detail=new Expander{Header="查看最后一场战斗记录",Content=U.Text(r.Log,13),Margin=new Thickness(0,10,0,0)};left.Children.Add(detail);
    }
    private UIElement Choice(string title,string description,string actionLabel,Action action,bool primary){var p=new StackPanel();p.Children.Add(U.Text(title,17));p.Children.Add(U.Text(description,14,U.Muted));p.Children.Add(Btn(actionLabel,action,true,primary));return U.Card(p,12);}
    private void Start(int difficulty){if(!G.Begin(difficulty,meal)){flash=FarmUX.MissingMeal(G,meal);flashTab=Tab;flashPhase=G.R.Phase;flashUntil=DateTime.UtcNow.AddSeconds(7);return;}autoplay=false;confirmRetreat=false;}
    private CheckBox DesktopSetting(string label,string preference,Func<bool> get)
    {
        var setting=new FarmCheckBox{Content=label,IsChecked=get(),FontSize=13,Margin=new Thickness(0,5,10,5),Foreground=U.Ink};
        setting.Click+=(s,e)=>owner.SetDesktopPreference(preference,setting.IsChecked==true);updates.Add(()=>setting.IsChecked=get());return setting;
    }
    private void DetailedHelp()
    {
        var p=new StackPanel();body.Children.Add(p);Heading(p,"先玩完这一个小循环","种田 → 喂养 / 配技能 → 自动冒险 → 肥料升级田地 + 装饰家园。");
        p.Children.Add(Notice("让旅途中也慢慢形成组合", "新旅程在第 1、2、4 站胜利后，会各遇见一组三选一的旅途灵感。小队约 18 秒后按偏好自动选，也能当场亲自挑；隐藏不会停，主动暂停会停。十二种能力强化潮湿、连锁、护盾、治疗、灼烧和毒种，每种最多 2 层，返家后清空。出发前可选「顺势搭配 / 进攻优先 / 稳健续航」，也会随常用队伍保存。手册里的灵感图鉴、旅行手记和逐场复盘能查看发现、选择和触发情况。"));
        p.Children.Add(Notice("回家后，一起核对这次安排", "在冒险结算或农场打开「归家安排」，勾选领取已有成长奖励、土壤升一级、出战伙伴各升一级。先看总花费与成长变化，再确认执行；任一材料不足就整份不执行。奖励可用于本次安排，新完成的目标留待下次领取。确认后保存实际花费与永久成长记录，可从回执重走原路线或准备下一层，不会自动出发。"));
        p.Children.Add(Notice("把农场收成，装进出门便当", "从农场或冒险准备页打开「出门便当」，六种食谱分别帮助体力、开场护盾、续航、水雷、灼烧与中毒。前两种直接可用，四种薄荷食谱首通小溪后开放。每趟最多带一份，选食谱不扣材料，点「制作便当并出发」时才扣；不足会显示差额，也可直接不带便当免费出发。材料页能预览改种与收齐时间。便当只在当前旅程生效，出发后计划清空；失败和提前返家不会退回已经制作的材料。"));
        p.Children.Add(Notice("出发前，免费试试新搭配", "在「配队」或冒险准备页点击「搭配试练 · 免费试打」。临时换伙伴、站位、技能和便当，与当前队伍使用相同路线、祝福和信物，试打第一场的前 3 回合；打完会提前结束。已开放的便当食谱即使缺材料也能免费模拟。试练不扣资源、不发奖励或推进通关；采用时只保存队伍、技能与便当计划，再确认实际费用和出发。逐场复盘、结算与旅行手记可查看真实便当效果。"));
        p.Children.Add(Notice("看看组合有没有真正生效", "在冒险途中、归来后点击「逐场复盘 · 伤害与配合」，选择一场战斗，查看每位伙伴的主动伤害、实际治疗、承伤和护盾挡伤，以及中毒、灼烧和连招次数。复盘会说明耗尽体力的回合、敌人剩余体力和未触发的配合；返家后可直接去调整配队或培养。最近 10 趟旅行手记也保留逐场记录，升级前的旧旅程不补写不存在的数据。查看复盘不会改变旅程暂停状态。"));
        p.Children.Add(Notice("让下一次成长更明确", "首页「培养详情」或伙伴卡片的「培养计划与材料」可设置升一级、下一次进化的目标。计划先喂到所需亲密，再算补等级的训练，总材料清楚列出；材料不足不扣费，培养与进化分别点击。缺作物时可直达种植预览，缺晶石则去冒险准备页。目标会保存，完成后由你选择是否继续下一步。"));
        p.Children.Add(Notice("把冒险带回来的装饰摆起来", "「农场 → 布置小院」可收藏、试摆六件装饰，小院有三个位置，桌宠另选一件随身摆件。未收藏的也能预览，选为目标后会显示还差几次通关；各地区任意深度都计数。选择装饰后会回到预览，点「放到…」才保存，也可「结束试摆」。装饰不加战力，旅途中可以布置，不会改变暂停状态。"));
        p.Children.Add(Notice("少点几下，也能照顾好小队", "「农场 → 种植计划」预览产量，再一次安排田地；只有改种的田地重新计时。「配队」可调整三位伙伴、技能和农场帮手，保存 3 套含信物的常用配置；全队升级先显示总麦穗费用，材料不足不会部分扣除，也不增加亲密。「手册 → 旅行手记」保留最近 10 趟新自动旅程的配置和战报，通关、失败与提前返家都会记录。"));
        p.Children.Add(Notice("成长手册与旅行信物", "到「手册」领取 8 项一次性成长奖励、查看 6 档探索进度、搭配旅行信物。每处首通发现一件新信物，最多带 2 件，与伙伴技能和祝福一起自动生效。深入敌人有硬壳，秘境敌人会再生；准备页会说明规则与建议等级。"));
        p.Children.Add(Notice("种子的四阶进化", "种子是第五位可培养和出战的宠物。到「进化」查看四阶形态、条件和新增能力；喂养提升成长与亲密，冒险首领带回进化晶石。满级后如果进化所需亲密不足，仍能用莓果补齐亲密。伙伴页可按水、火、草、土、雷与六种职责筛选。"));
        p.Children.Add(Notice("桌面太挤？缩成一只宠物", "右上角「缩成宠物」收起所有大面板。鼠标移上宠物后点「换宠」，直接选择头像；按鼠标中键可轮换下一只。换宠不影响出战与农田分工。宠物可拖动，滚轮调大小，双击打开玩法。\n隐藏后点击系统托盘里的宠物图标，或再次启动程序即可找回。托盘右键也可选择展示伙伴。新旅程隐藏后会继续；可在冒险页主动暂停。右键「退出并保存」才会结束程序。"));
        var settings=new StackPanel();settings.Children.Add(U.Text("工作时的显示设置",17,U.Green));
        settings.Children.Add(U.Text("取消置顶可让工作窗口盖住桌宠；暂停动作后农场继续收获。锁定后仍可双击打开玩法。设置也能在宠物或托盘的右键菜单里找到。",13,U.Muted));
        var switches=new UniformGrid{Columns=2};settings.Children.Add(switches);
        switches.Children.Add(DesktopSetting("保持置顶","topmost",()=>G.S.DesktopTopmost));switches.Children.Add(DesktopSetting("播放桌宠动作","motion",()=>G.S.DesktopMotion));
        switches.Children.Add(DesktopSetting("锁定拖动与滚轮","locked",()=>G.S.DesktopLocked));switches.Children.Add(DesktopSetting("启用全局快捷键","hotkeys",()=>G.S.DesktopHotkeys));
        switches.Children.Add(DesktopSetting("显示桌宠状态角标","hints",()=>G.S.DesktopStatusHints));
        settings.Children.Add(U.Text("角标显示旅行站数、暂停、归来、进化与成长奖励。点击查看详情；鼠标移上宠物也能查看状态。没有声音或自动弹窗，可随时关闭角标。",12,U.Muted));
        settings.Children.Add(Live(()=>owner.DesktopShortcuts,12,U.Muted));p.Children.Add(U.Card(settings));
        var grid=new UniformGrid{Columns=2};p.Children.Add(grid);
        string[] titles={"1. 农场会自动收获","2. 培养与进化是永久的","3. 选择三位出战伙伴","4. 战斗自动进行","5. 肥料与装饰自动入库","6. 让组合自己运转"};
        string[] details={"选择作物后，它会自己生长、收获、补种。莓果用于喂养，麦穗用于训练；材料不够时可先去冒险。","喂养消耗莓果 2，成长 +12；喂泡泡和种子还增加亲密 1。训练消耗麦穗 3，成长 +18。泡泡 Lv.3、亲密 3、晶石 2 即可进化。","伙伴按站位 1 → 2 → 3 出手，敌人优先攻击前排。泡泡先挂潮湿，电团随后追击；苔壳提供治疗护盾。从未出战的伙伴中选一位照料农田，其余休息。","出发前配队、选择一个祝福。小队自动战斗、休息、走完整趟。隐藏和关闭面板都会继续，主动点击暂停才会停下。退出重开只结算当前这趟。","每场胜利获得肥料，升级土壤永久加快生长。每个地区前三次通关各带回一件新装饰；集齐后重复奖励转为肥料。失败保留途中收获。","Lv.2 解锁第二个可替换技能。试试水雷连锁、护盾重击、灼烧蔓延、潮露毒种；战报告诉你触发了几次。小溪首领仍奖励进化晶石 2。"};
        for(int i=0;i<6;i++){var box=new StackPanel();box.Children.Add(U.Text(titles[i],17,U.Green));box.Children.Add(U.Text(details[i],14));var c=U.Card(box,14);c.Margin=new Thickness(0,0,10,10);grid.Children.Add(c);}
        p.Children.Add(Btn(G.AtHome?"明白了，去准备冒险":"回到我的冒险",()=>Go("冒险"),true,true));
    }
}







