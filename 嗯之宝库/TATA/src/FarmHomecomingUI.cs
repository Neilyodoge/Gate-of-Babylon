using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool arrangeClaim,arrangeSoil,arrangeTrain,arrangeReceipt,arrangePlanting;
    private HomecomingQuote arrangeQuote;
    private void OpenHomecoming(bool receipt=false)
    {
        arrangeClaim=G.ClaimableChapters>0;arrangeSoil=arrangeTrain=arrangePlanting=false;arrangeReceipt=receipt&&G.S.LastHomecoming!=null;Go("归家");resetScroll=true;
    }
    private UIElement ArrangementCard(string title,string detail,bool selected,bool enabled,Action<bool> choose,UIElement extra=null)
    {
        var p=new StackPanel();var toggle=new FarmCheckBox{Content=title,IsChecked=selected,IsEnabled=enabled,FontSize=15,Foreground=U.Ink,Margin=new Thickness(0,0,0,6)};AutomationProperties.SetName(toggle,title);toggle.Checked+=(s,e)=>Do(()=>choose(true));toggle.Unchecked+=(s,e)=>Do(()=>choose(false));p.Children.Add(toggle);
        p.Children.Add(ReviewText(detail,12,U.Muted));if(extra!=null)p.Children.Add(extra);var card=U.Card(p,11);if(selected)card.Background=U.Pale;return card;
    }
    private void HomecomingPage()
    {
        var cols=Columns(236);var left=cols[0];var aside=cols[1];
        if(arrangeReceipt&&G.S.LastHomecoming!=null){HomecomingResult(left,aside);return;}
        if(!G.MayArrangeHomecoming){Heading(left,"等伙伴回来，再安排成长。","查看这页不会暂停或改变当前旅程。");aside.Children.Add(Btn("查看当前旅程",()=>Go("冒险"),true,true));if(G.S.LastHomecoming!=null)aside.Children.Add(Btn("上次安排的记录",()=>{arrangeReceipt=true;resetScroll=true;}));return;}
        if(arrangePlanting){HomecomingPlantingPage(left,aside);return;}
        arrangeQuote=G.QuoteHomecoming(arrangeClaim,arrangeSoil,arrangeTrain);var quote=arrangeQuote;
        Heading(left,"收好行囊，把收获用起来。","勾选这次想做的事 · 先核对花费，再一次确认");
        int ready=G.ClaimableChapters;var rewards=new StackPanel();var available=G.QuoteHomecoming(true,false,false);
        rewards.Children.Add(ReviewText(ready>0?FarmGame.HomecomingAmounts(available.Gains,"+"):"暂时没有可领取的成长奖励。",12,U.Green));
        if(ready>0)rewards.Children.Add(ReviewText(String.Join("、",available.Chapters.Select(i=>FarmGame.ChapterNames[i]).ToArray()),11,U.Muted));
        left.Children.Add(ArrangementCard("领取已完成奖励",ready+" 项 · 本次只领取现在已经完成的目标",arrangeClaim,ready>0,v=>arrangeClaim=v,rewards));
        var rates=new StackPanel();rates.Children.Add(ReviewText("土壤 Lv."+G.S.SoilLevel+" → "+Math.Min(5,G.S.SoilLevel+1)+" · 肥料 "+(G.S.SoilLevel<5?G.SoilCost:0),13,U.Green));
        rates.Children.Add(ReviewText("每分钟产量："+RateText(G.FarmRates()),11,U.Muted));rates.Children.Add(ReviewText("升级后预计："+RateText(G.HomecomingSoilRates(true)),11,U.Green));
        left.Children.Add(ArrangementCard("升级一次土壤",G.S.SoilLevel<5?"沿用当前种植，所有田地的生长速度再增加 12%。":"土壤已经满级。",arrangeSoil,G.S.SoilLevel<5,v=>arrangeSoil=v,rates));
        var team=new StackPanel();var trained=G.QuoteHomecoming(false,false,true);
        for(int i=0;i<3;i++){int pet=G.S.Team[i],delta=trained.AfterLevels[i]-trained.BeforeLevels[i];team.Children.Add(ReviewText(G.Name(pet)+"  Lv."+G.Level(pet)+(delta>0?" → "+trained.AfterLevels[i]+" · 攻击 +"+delta*2+" / 体力 +"+delta*6:" · 已满级，本次跳过"),12,U.Green));}
        left.Children.Add(ArrangementCard("出战伙伴各升一级","麦穗 "+G.PartyTrainingCost+" · 保留成长余量，不消耗莓果、薄荷或晶石。",arrangeTrain,G.PartyTrainingCost>0,v=>arrangeTrain=v,team));
        var budget=new StackPanel();budget.Children.Add(U.Text("这次的总账",18));budget.Children.Add(ReviewText("先入库："+FarmGame.HomecomingAmounts(quote.Gains,"+"),12,U.Green));budget.Children.Add(ReviewText("再消耗："+FarmGame.HomecomingAmounts(quote.Cost,"−"),12,U.Gold));
        budget.Children.Add(Live(()=>G.HomecomingShortage(quote),12,U.Muted));
        var confirm=Btn("确认这次安排",()=>{if(G.ApplyHomecoming(quote)){arrangeReceipt=true;resetScroll=true;}},G.CanApplyHomecoming(quote),true);FarmTheme.Accent(confirm);updates.Add(()=>confirm.IsEnabled=G.CanApplyHomecoming(quote));budget.Children.Add(confirm);
        budget.Children.Add(Btn("刷新安排",()=>{if(G.ClaimableChapters==0)arrangeClaim=false;if(G.S.SoilLevel>=5)arrangeSoil=false;if(G.PartyTrainingCost==0)arrangeTrain=false;}));
        budget.Children.Add(Btn(G.AtHome?"先不安排，回农场":"先不安排，回收获页",()=>Go(G.AtHome?"农场":"冒险")));aside.Children.Add(U.Card(budget,12));
        var stock=new StackPanel();stock.Children.Add(U.Text("完成后预计剩余",16));for(int k=0;k<5;k++){int kind=k;stock.Children.Add(Live(()=>{var q=G.QuoteHomecoming(arrangeClaim,arrangeSoil,arrangeTrain);return FarmGame.HomecomingResources[kind]+"  "+(q.After[kind]<0?"缺 "+(-q.After[kind]):q.After[kind].ToString());},12,U.Green));}aside.Children.Add(U.Card(stock,12));
        if(quote.Train)aside.Children.Add(Btn("缺麦穗？看看种植方案",()=>{arrangePlanting=true;plantingOpen=true;selectedPlanting=2;resetScroll=true;}));
        aside.Children.Add(U.Text("确认后若仍在收获页，会一并返家。进化、便当与下一趟出发由你另外决定。",11,U.Muted));
        if(G.S.LastHomecoming!=null)aside.Children.Add(Btn("上次安排的记录",()=>{arrangeReceipt=true;resetScroll=true;}));
    }
    private void HomecomingResult(StackPanel left,StackPanel aside)
    {
        var r=G.S.LastHomecoming;Heading(left,"安排好了，慢慢变强。",new DateTime(r.Ticks,DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm")+" · 上次确认的结果已保存");
        var reward=new StackPanel();reward.Children.Add(ReviewText("领取 "+r.Chapters+" 项成长奖励",16,U.Green));reward.Children.Add(ReviewText("实际入库："+FarmGame.HomecomingAmounts(r.Gained,"+"),12));reward.Children.Add(ReviewText("实际花费："+FarmGame.HomecomingAmounts(r.Spent,"−"),12,U.Gold));left.Children.Add(U.Card(reward,12));
        left.Children.Add(SectionTitle(r.BeforeLevels.SequenceEqual(r.AfterLevels)?"伙伴状态 · 本次未训练":"这次的伙伴成长","永久属性"));
        for(int i=0;i<3;i++){
            var p=new StackPanel();p.Children.Add(ReviewText(r.Names[i]+"  Lv."+r.BeforeLevels[i]+" → "+r.AfterLevels[i],15,U.Green));p.Children.Add(ReviewText("攻击 "+r.BeforeAttack[i]+" → "+r.AfterAttack[i]+"     体力 "+r.BeforeHp[i]+" → "+r.AfterHp[i],12,U.Muted));left.Children.Add(U.Card(p,9));
        }
        left.Children.Add(Notice("农田的变化","土壤 Lv."+r.SoilBefore+" → "+r.SoilAfter+"\n每分钟预计产量："+RateText(r.RatesBefore)+"\n安排后："+RateText(r.RatesAfter)));
        var next=new StackPanel();next.Children.Add(U.Text("再去验证一下？",18));next.Children.Add(U.Text("只去准备页，队伍、祝福和便当都还能调整。",12,U.Muted));
        next.Children.Add(Btn("重走「"+FarmGame.ExpeditionName(r.Region,r.Depth)+"」",()=>HomecomingRoute(r.Region,r.Depth),G.AtHome&&G.ExpeditionUnlocked(r.Region,r.Depth),true));
        if(r.Depth<2){bool open=G.ExpeditionUnlocked(r.Region,r.Depth+1);next.Children.Add(Btn("准备挑战「"+FarmGame.ExpeditionName(r.Region,r.Depth+1)+"」",()=>HomecomingRoute(r.Region,r.Depth+1),G.AtHome&&open));next.Children.Add(U.Text(open?"下一层建议 "+FarmGame.ExpeditionLevel(r.Region,r.Depth+1)+"；当前提升不保证通关。":G.ExpeditionGate(r.Region,r.Depth+1),11,U.Muted));}
        next.Children.Add(Btn("先免费试打",OpenPractice,G.AtHome));aside.Children.Add(U.Card(next,12));
        aside.Children.Add(Btn("回农场看看",()=>Go("农场")));aside.Children.Add(Btn("调整技能与信物",()=>Go("配队")));aside.Children.Add(Btn("查看进化与形态",()=>Go("进化")));
        aside.Children.Add(Btn("再安排别的事情",()=>OpenHomecoming(),G.MayArrangeHomecoming));
        aside.Children.Add(U.Text("记录展示当时的成长与花费；之后换队、改种或进化不会改写它。没有重复领奖或自动出发。",11,U.Muted));
    }
    private void HomecomingRoute(int region,int depth){if(!G.AtHome||!G.ExpeditionUnlocked(region,depth))return;SelectJourneyDestination(region,depth);Go("冒险");resetScroll=true;}
    private void HomecomingPlantingPage(StackPanel left,StackPanel aside)
    {
        Heading(left,"给这次训练，备一点麦穗。","原来的安排已保留 · 查看方案不改种，确认安排才扣材料");
        left.Children.Add(PlantingPlanner());var scene=Scene();scene.Height=scene.MinHeight=174;left.Children.Add(scene);
        var supply=new StackPanel();supply.Children.Add(U.Text("距离这次安排还差多少",17));
        supply.Children.Add(Live(()=>{var q=G.QuoteHomecoming(arrangeClaim,arrangeSoil,arrangeTrain);int missing=Math.Max(0,-q.After[1]);double wait=G.CareResourceWait(1,missing);return missing==0?"麦穗已经够了。":"还差麦穗 "+missing+(wait<0?"，当前未种植。":"，约 "+Math.Max(1,(int)Math.Ceiling(wait))+" 秒后收齐。");},13,U.Green));
        supply.Children.Add(U.Text("已计入勾选的成长奖励。时间按当前田地、土壤与帮手估算，应用方案后更新。",11,U.Muted));supply.Children.Add(Btn("带着原安排返回",()=>{arrangePlanting=false;resetScroll=true;},true,true));aside.Children.Add(U.Card(supply,12));
        aside.Children.Add(Btn("查看完整农场",()=>Go("农场")));left.Children.Add(U.Text("应用种植方案只改变需要改种的田地，不会执行训练、施肥或领奖。",11,U.Muted));
    }
}
