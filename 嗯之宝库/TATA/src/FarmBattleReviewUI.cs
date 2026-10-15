using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

public partial class FarmPanel
{
    private JourneyMemory reviewMemory;
    private int reviewStation=-1,reviewRound=-1;
    private void OpenBattleReview(JourneyMemory memory=null){reviewMemory=memory;reviewStation=-1;Go("复盘");resetScroll=true;}
    private void ReviewReturn(bool train,int pet)
    {
        if(!G.AtHome&&G.R.Phase!="result")return;
        if(G.R.Phase=="result")G.Home();if(train)OpenCare(pet,false);else Go("配队");resetScroll=true;
    }
    private UIElement BattleMetric(string name,string value,string detail)
    {
        var p=new StackPanel();p.Children.Add(ReviewText(name,11,U.Muted));p.Children.Add(ReviewText(value,21,U.Green));p.Children.Add(ReviewText(detail,10,U.Muted));var card=U.Card(p,9);card.Margin=new Thickness(0,0,6,8);return card;
    }
    private TextBlock ReviewText(string text,double size=12,System.Windows.Media.Brush color=null){var label=U.Text(text,size,color);label.Margin=new Thickness(0,0,0,2);return label;}
    private void BattleReviewPage()
    {
        reviewRound=G.R.Round;var notes=reviewMemory==null?G.CurrentBattleNotes():reviewMemory.Battles;
        var cols=Columns(236);var left=cols[0];var aside=cols[1];
        Heading(left,"这一趟，配合得怎样？",(reviewMemory==null?G.RouteName:FarmGame.ExpeditionName(reviewMemory.Region,reviewMemory.Depth))+" · "+(reviewMemory==null?"当前旅程的实战记录":"当时的队伍与实战记录"));
        if(notes.Length==0){left.Children.Add(Notice("这一趟还没有逐场记录",G.AtHome?"从新版开始的自动冒险会记录每场战斗。旧战报不补写不存在的数据。":"新旅程进入战斗后会出现记录。升级前开始的旅程仍保留原战报，不补写逐场数据。"));aside.Children.Add(Btn("返回冒险",()=>Go("冒险"),true,true));aside.Children.Add(Btn("查看旅行手记",()=>OpenHandbook(3)));return;}
        var selected=notes.FirstOrDefault(note=>note.Station==reviewStation)??notes.Last();var choices=new UniformGrid{Columns=4};left.Children.Add(choices);
        string[] stations={"溪口","林间","营地","关口","首领"};foreach(var entry in notes){int station=entry.Station;var choose=Btn(stations[station]+" · "+FarmGame.BattleOutcome(entry),()=>{reviewStation=station;resetScroll=true;},true,selected.Station==station);choose.FontSize=11;choose.Padding=new Thickness(3,7,3,7);choose.Margin=new Thickness(0,0,5,10);choices.Children.Add(choose);}
        var b=selected;var metrics=new UniformGrid{Columns=3};left.Children.Add(metrics);
        metrics.Children.Add(BattleMetric("实际造成伤害",(b.Direct+b.Burn+b.Poison).ToString(),"按敌人实际扣血统计"));metrics.Children.Add(BattleMetric("战中治疗",b.Healing.ToString(),"不含营地和战后恢复"));metrics.Children.Add(BattleMetric("护盾挡伤",b.Blocked.Sum().ToString(),"实际抵挡的伤害"));
        left.Children.Add(SectionTitle("伙伴这场做了什么","主动伤害含连锁与催化"));
        int max=Math.Max(1,b.Damage.Max());
        for(int i=0;i<3;i++){
            int slot=i,pet=b.Team[i];var p=new StackPanel();var row=new DockPanel();var amount=ReviewText("主动伤害 "+b.Damage[pet],13,U.Green);DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);row.Children.Add(ReviewText((i+1)+" · "+b.Names[i]+"  Lv."+b.Levels[i],15));p.Children.Add(row);var bar=U.Bar(b.Damage[pet],max);bar.Height=4;bar.Margin=new Thickness(0,2,0,4);p.Children.Add(bar);
            p.Children.Add(ReviewText("技能治疗 "+b.SkillHealing[pet]+" · 承受伤害 "+b.Taken[pet]+" · 护盾挡伤 "+b.Blocked[pet],12));
            p.Children.Add(ReviewText(FarmGame.SkillNames[pet][b.Skills[slot]]+" · 出手 "+b.Actions[pet]+" 次    体力 "+b.StartHp[slot]+" → "+b.EndHp[slot]+" / "+b.MaxHp[slot]+(b.FellRound[pet]>0?" · 第 "+b.FellRound[pet]+" 回合耗尽":""),11,U.Muted));var card=U.Card(p,8);card.Margin=new Thickness(0,0,0,8);left.Children.Add(card);
        }
        var status=new StackPanel();status.Children.Add(U.Text(FarmGame.BattleOutcome(b)+" · "+b.Rounds+" 回合",18));status.Children.Add(U.Text("敌人剩余体力 "+b.EnemyRemaining,12,U.Muted));
        status.Children.Add(Btn(reviewMemory==null?"回到冒险现场":"返回旅行手记",()=>{if(reviewMemory==null)Go("冒险");else OpenHandbook(3);},true,true));
        bool mayChange=G.AtHome||G.R.Phase=="result";int carePet=FarmGame.ReviewCarePet(b);
        status.Children.Add(Btn(G.R.Phase=="result"?"返家并调整配队":"调整配队与技能",()=>ReviewReturn(false,carePet),mayChange));
        status.Children.Add(Btn((G.R.Phase=="result"?"返家并培养":"培养")+G.Name(carePet),()=>ReviewReturn(true,carePet),mayChange));
        if(!mayChange)status.Children.Add(U.Text("当前旅程结束并返家后再调整；查看复盘不会暂停或继续旅程。",11,U.Muted));aside.Children.Add(U.Card(status,12));
        aside.Children.Add(Notice("从这场看到的",FarmGame.BattleObservation(b)));
        var effects=new StackPanel();effects.Children.Add(U.Text("持续伤害与连招",16));effects.Children.Add(U.Text("直接伤害 "+b.Direct+"\n灼烧伤害 "+b.Burn+" · 中毒伤害 "+b.Poison+"\n潮湿追击 "+b.Combos[0]+" · 闪电跳跃 "+b.Combos[1]+"\n护盾重击 "+b.Combos[2]+" · 灼烧蔓延 "+b.Combos[3]+"\n中毒施加 "+b.Combos[4]+" · 余烬催化 "+b.Combos[5],12,U.Green));
        effects.Children.Add(U.Text("灼烧和中毒可能由伙伴、祝福或信物共同产生，单独统计。治疗只计实际恢复，挡伤只计实际抵挡。",11,U.Muted));aside.Children.Add(U.Card(effects,12));
        if(b.Inspirations.Any(n=>n>0))aside.Children.Add(InspirationReview(b));
        if(b.Picnic>=0)aside.Children.Add(Notice("当场便当效果",FarmGame.PicnicReport(b.Picnic,b.PicnicTriggers,b.PicnicHealing)));
        if(reviewMemory!=null)left.Children.Add(U.Text("这是当时的记录。现在的等级、进化和配队不会改动这些数值。",11,U.Muted));
    }
}
