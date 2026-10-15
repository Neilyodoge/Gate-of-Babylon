using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmRun resultDetailsSource;
    private bool resultDetailsOpen;
    private string homeSoilReceipt="";
    private int homeSoilReceiptLevel=-1;
    private void UpgradeFromHome()
    {
        if(!G.AtHome||G.S.SoilLevel>=5||G.S.Fertilizer<G.SoilCost)return;
        int level=G.S.SoilLevel,cost=G.SoilCost;double before=G.CropSeconds(0);
        if(G.UpgradeSoil()){
            homeSoilReceiptLevel=G.S.SoilLevel;
            homeSoilReceipt="本次施肥：土壤 Lv."+level+" → "+G.S.SoilLevel+" · 肥料 −"+cost+" · 莓果一轮 "+before.ToString("0.#")+" → "+G.CropSeconds(0).ToString("0.#")+" 秒";
        }
    }
    private int ResultCarePet()
    {
        var last=G.R.Battles.LastOrDefault();
        if(last!=null&&last.Outcome=="defeat"&&last.Team.Any(p=>last.FellRound[p]>0))return FarmGame.ReviewCarePet(last);
        return G.S.Team.OrderBy(p=>G.Level(p)).ThenBy(p=>G.S.Xp[p]).First();
    }
    private bool StillViewingResult(FarmRun source){return Object.ReferenceEquals(G.R,source)&&G.R.Phase=="result";}
    private void ReturnFromReceipt(FarmRun source,string tab,int pet=-1)
    {
        if(!StillViewingResult(source)||!G.Home())return;
        if(pet>=0)SelectFocusedPet(pet);if(tab=="手册")OpenHandbook(0);else Go(tab);resetScroll=true;
    }
    private void PrepareFromReceipt(FarmRun source,int depth)
    {
        if(!StillViewingResult(source)||!G.ExpeditionUnlocked(source.Difficulty,depth))return;
        int region=source.Difficulty;if(!G.Home())return;SelectJourneyDestination(region,depth);Go("冒险");resetScroll=true;
    }
    private UIElement DiscoveryLine(UIElement icon,string title,string detail,Button action=null)
    {
        var row=new DockPanel{Margin=new Thickness(0,0,0,7)};
        if(action!=null){action.Padding=new Thickness(6,4,6,4);action.FontSize=10;action.Margin=new Thickness(6,0,0,0);action.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(action,Dock.Right);row.Children.Add(action);}
        var picture=new Border{Child=icon,Width=35,Height=35,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(picture,Dock.Left);row.Children.Add(picture);
        var words=new StackPanel();words.Children.Add(ReviewText(title,13,U.Green));words.Children.Add(ReviewText(detail,11,U.Muted));row.Children.Add(words);return row;
    }
    private void ReturnOverview(StackPanel left,StackPanel aside)
    {
        if(GentleJourney){GentleGuideReturn(left,aside);return;}
        var run=G.R;bool won=run.Outcome=="victory",withdrew=run.Withdrew;
        if(!Object.ReferenceEquals(resultDetailsSource,run)){resultDetailsSource=run;resultDetailsOpen=false;}
        Heading(left,won?"这一趟，有收获。":withdrew?"提前回来，也没关系。":"先歇一会儿，再变强。",WorldGuide&&won?"伙伴带回了纪念，也带回让农田长得更快的肥料。":"奖励已自动入库。先看收获，再决定怎么用。");
        left.Children.Add(FarmTheme.Badge(G.RouteName+" · "+(won?"已通关":withdrew?"主动返家":"第 "+(run.Node+1)+" 站止步")));
        left.Children.Add(SectionTitle("本趟带回","农田收成另计"));var rewards=new UniformGrid{Columns=2};
        rewards.Children.Add(RewardTile("fertilizer","肥料",run.RewardFertilizer,"让农田长得更快"));
        if(run.RewardCrystals>=0)rewards.Children.Add(RewardTile("crystal","晶石",run.RewardCrystals,"用于伙伴进化"));else rewards.Children.Add(U.Card(U.Text("晶石\n旧旅程未记录总量",12,U.Muted),12));left.Children.Add(rewards);
        var discoveries=new StackPanel();bool found=false;
        if(run.DecorationReward>=0){int id=run.DecorationReward;found=true;var preview=Btn("去小院预览与摆放",()=>OpenGarden(id));discoveries.Children.Add(DiscoveryLine(new DecorationView{Decoration=id},"新装饰 · "+FarmGame.DecorationNames[id],"已收藏，可以放在小院或桌宠旁。",preview));}
        if(run.NewRelic>=0){int id=run.NewRelic;found=true;discoveries.Children.Add(DiscoveryLine(new RelicView{Relic=id},"新信物 · "+FarmGame.RelicNames[id],G.S.EquippedRelics.Contains(id)?"已放入行囊，下次出发生效。":"已收藏，返家后可在手册中装备。"));}
        if(run.UnlockedGarden){found=true;discoveries.Children.Add(DiscoveryLine(new FarmGlyph{Kind="farm"},"农场扩大了","新增 2 块田地与薄荷种子，已经开放。"));}
        if(found){var box=new StackPanel();box.Children.Add(U.Text("本趟新发现",15));box.Children.Add(discoveries);left.Children.Add(U.Card(box,10));}
        else left.Children.Add(U.Text(won?"熟悉的路线也会带回肥料；装饰集齐后的奖励按原规则转为肥料。":withdrew?"主动结束不算失败；已经获得的收获保留。":"伙伴没有永久损伤。返家培养后，可以再来挑战。",12,U.Muted));
        var detail=new StackPanel();
        if(run.DecorationReward>=0){int id=run.DecorationReward;var equip=Btn("",()=>G.EquipDecoration(id));updates.Add(()=>{equip.Content=G.S.DisplayDecoration==id?"✓ 已摆在宠物旁":"摆上「"+FarmGame.DecorationNames[id]+"」";equip.IsEnabled=G.S.DisplayDecoration!=id;});detail.Children.Add(equip);}
        if(run.NewRelic>=0)detail.Children.Add(Notice("信物的作用",FarmGame.RelicDetails[run.NewRelic]));
        if(run.LegacyRewards&&(run.RewardBerries>0||run.RewardXp>0))detail.Children.Add(U.Text("旧旅程累计：莓果 "+run.RewardBerries+" · 每位成长 "+run.RewardXp,12,U.Muted));
        if(run.InspirationsEnabled)detail.Children.Add(InspirationRunCard());if(run.Picnic>=0)detail.Children.Add(PicnicRunCard());
        detail.Children.Add(Notice("这套搭配的表现",G.ComboReport));if(run.Relics.Any(i=>i>=0))detail.Children.Add(Notice("信物战报",G.RelicReport));
        var expand=new Expander{Header="展开本趟详情 · 灵感、便当与配合",Content=detail,IsExpanded=resultDetailsOpen};AutomationProperties.SetAutomationId(expand,"return-details");expand.Expanded+=(s,e)=>resultDetailsOpen=true;expand.Collapsed+=(s,e)=>resultDetailsOpen=false;left.Children.Add(expand);
        var last=run.Battles.LastOrDefault();if(!won&&!withdrew&&last!=null)left.Children.Add(Notice("这场发生了什么",FarmGame.BattleObservation(last).Split('\n')[0]));
        int pet=ResultCarePet();bool soil=G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost,careFirst=!won&&!withdrew&&!soil;
        var next=new StackPanel();next.Children.Add(U.Text(soil?"下一步：给农田施肥":careFirst?"下一步：照顾"+G.Name(pet):"下一步：回农场",17));
        next.Children.Add(U.Text(soil?"土壤 Lv."+G.S.SoilLevel+" → "+(G.S.SoilLevel+1)+"，需要肥料 "+G.SoilCost+"。先返家，再确认施肥。":careFirst?"先看看"+G.Name(pet)+"的培养费用，再决定怎么提升。这里不会自动花费材料。":"回到首页，继续培养、进化或管理农田。",12,U.Muted));
        string farmLabel=soil?"返家 · 升级农田":"返家 · 回到农场",careLabel="返家 · 培养 / 调整技能";
        var primary=Btn(careFirst?careLabel:farmLabel,()=>ReturnFromReceipt(run,careFirst?"伙伴":"农场",careFirst?pet:-1),true,true);AutomationProperties.SetAutomationId(primary,"return-primary");FarmTheme.Accent(primary);if(WorldGuide)HighlightGuideAction(primary);next.Children.Add(primary);
        var alternative=Btn(careFirst?farmLabel:careLabel,()=>ReturnFromReceipt(run,careFirst?"农场":"伙伴",careFirst?-1:pet));next.Children.Add(alternative);
        next.Children.Add(Btn("归家安排 · 一起核对花费",()=>{if(StillViewingResult(run))OpenHomecoming();}));aside.Children.Add(U.Card(next,12));
        var route=new StackPanel();route.Children.Add(U.Text("下一趟，自己决定",15));route.Children.Add(Btn("准备再去一趟",()=>PrepareFromReceipt(run,run.Depth)));
        if(run.Depth<2){bool open=G.ExpeditionUnlocked(run.Difficulty,run.Depth+1);var deeper=Btn("准备挑战「"+FarmGame.ExpeditionName(run.Difficulty,run.Depth+1)+"」",()=>PrepareFromReceipt(run,run.Depth+1),open);deeper.FontSize=11;deeper.Padding=new Thickness(4,5,4,5);route.Children.Add(deeper);route.Children.Add(U.Text(open?"下一层建议 "+FarmGame.ExpeditionLevel(run.Difficulty,run.Depth+1):"先通关当前深度，下一层才开放。",11,U.Muted));}
        else route.Children.Add(U.Text("已到本区域最深处，可以继续收集装饰与肥料。",11,U.Muted));
        route.Children.Add(U.Text("只打开准备页，不自动出发。",10,U.Muted));aside.Children.Add(U.Card(route,10));
        aside.Children.Add(Btn("逐场复盘 · 伤害与配合",()=>OpenBattleReview()));aside.Children.Add(Btn("查看这趟与以往记录",()=>OpenHandbook(3)));
        aside.Children.Add(Btn("返家 · 信物与成长奖励",()=>ReturnFromReceipt(run,"手册")));
    }
}
