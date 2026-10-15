using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

// Presentation only. Rules commit once before playback; no animation callback spends or saves.
internal sealed class MossFeedback
{
    internal MossRun Run;
    internal int Revision,EnemyPet,Stage,Turn,Hits,Damage,Taken,Blocked,EnemyBlocked,Shield,Heal,EnergyDelta,Drawn;
    internal bool EnemyAction,Attack,Combo,Motion;
    internal string Title="",EnemyText="",PlayerText="";
    internal DateTime Started=DateTime.UtcNow;
    internal double? TestElapsed;
    internal double Elapsed{get{return TestElapsed??(DateTime.UtcNow-Started).TotalMilliseconds;}}
    internal bool Active{get{return Elapsed<900;}}
    internal double Beat{get{double t=Math.Max(0,Elapsed-65)/600;return t>=1?0:Math.Sin(Math.PI*((t*Math.Max(1,Hits))%1));}}
    internal double PlayerOffset{get{return Motion&&Active&&Attack?(EnemyAction?-Math.Sin(Elapsed*.055)*8*Beat:38*Beat):0;}}
    internal double EnemyOffset{get{return Motion&&Active&&Attack?(EnemyAction?-38*Beat:Math.Sin(Elapsed*.055)*8*Beat):0;}}
}

internal sealed partial class MossScene
{
    private void FeedbackLabel(DrawingContext d,string text,double center,double y,Brush color,double width)
    {
        if(text=="")return;
        double scale=new[]{1d,1.15,1.3}[game.S.ReadingSize];
        double box=Math.Min(width,Math.Max(94,text.Length*12*scale+18));
        d.DrawRoundedRectangle(U.Color("#FFFCF3"),new Pen(color,1),new Rect(center-box/2,y,box,26*scale),8,8);
        Text(d,text,center-box/2+8,y+2,12,color,box-12);
    }
    private void DrawFeedback(DrawingContext d,double w,double h)
    {
        var f=Feedback;if(f==null||f.Motion&&!f.Active)return;
        double lift=f.Motion?Math.Min(9,f.Elapsed/90):0;
        if(f.Motion&&f.Active&&f.Attack){
            double target=f.EnemyAction?w*.23:w*.78,y=h*.59;
            var pen=new Pen(U.Color("#B88636"),2);
            for(int i=0;i<6;i++){double a=i*Math.PI/3,r=8+13*f.Beat;d.DrawLine(pen,new Point(target+Math.Cos(a)*r,y+Math.Sin(a)*r),new Point(target+Math.Cos(a)*(r+9),y+Math.Sin(a)*(r+9)));}
        }
        FeedbackLabel(d,f.PlayerText,w*.24,h-32-lift,f.Taken>0?U.Color("#8A4432"):U.Green,w*.46);
        FeedbackLabel(d,f.EnemyText,w*.76,h-32-lift,U.Color("#8A4432"),w*.46);
    }
}

public partial class FarmPanel
{
    private MossFeedback cardFeedback;
    private bool CurrentCardFeedback{get{return cardFeedback!=null&&Object.ReferenceEquals(cardFeedback.Run,G.C)&&cardFeedback.Revision==G.C.Revision;}}
    private bool HoldCardResult{get{return CurrentCardFeedback&&cardFeedback.Motion&&cardFeedback.Active&&G.C.Phase!="battle";}}
    private void ClearCardFeedback(){cardFeedback=null;}
    private void TickCardFeedback()
    {
        if(CurrentCardFeedback&&!cardFeedback.Active&&G.C.Phase!="battle"&&cardScene!=null&&cardScene.Feedback!=null){cardFeedback=null;Render();}
    }
    private void ActCard(int slot,int revision,bool enemy)
    {
        if(G.C.Phase!="battle"||G.C.Revision!=revision||(!enemy&&(slot<0||slot>=G.C.Hand.Count)))return;
        var r=G.C;int card=enemy?-1:r.Hand[slot],hp=r.Hp,enemyHp=r.EnemyHp,block=r.Block,energy=r.Energy,combo=r.Combos,hand=r.Hand.Count;
        bool attack=enemy?!G.CardIntentGuard&&!G.CardIntentRest:new[]{0,1,3,4,7,10,11,13}.Contains(MossCards.Id(card));
        var f=new MossFeedback{Run=r,EnemyAction=enemy,EnemyPet=G.CardEnemyPet,Stage=r.Stage,Turn=r.Turn,Motion=G.S.DesktopMotion,Attack=attack,Hits=enemy?G.CardIntentHits:MossCards.Id(card)==10?2:1,Title=enemy?(attack?"敌人行动":"敌人没有攻击"):G.CurrentCardName(card)};
        if(!(enemy?G.EndCardTurn(revision):G.PlayCard(slot,revision)))return;
        f.Revision=r.Revision;f.Damage=Math.Max(0,enemyHp-r.EnemyHp);f.Taken=Math.Max(0,hp-r.Hp);f.Heal=Math.Max(0,r.Hp-hp);f.Shield=enemy?0:Math.Max(0,r.Block-block);f.EnergyDelta=r.Energy-energy;f.Drawn=enemy?0:r.Hand.Count-hand+1;f.Combo=r.Combos>combo;
        f.Blocked=enemy?G.CardVisualBlocked:0;f.EnemyBlocked=enemy?0:r.LastBlock;if(enemy)f.Hits=G.CardVisualHits;
        f.EnemyText=f.Damage>0?(f.Hits>1?"合计 ":"")+"−"+f.Damage:"";
        if(f.EnemyBlocked>0)f.EnemyText+=(f.EnemyText!=""?" · ":"")+"盾挡 "+f.EnemyBlocked;
        f.PlayerText=f.Taken>0?"体力 −"+f.Taken:f.Heal>0?"体力 +"+f.Heal:f.Shield>0?"护盾 +"+f.Shield:"";
        if(f.Blocked>0)f.PlayerText+=(f.PlayerText!=""?" · ":"")+"挡住 "+f.Blocked;
        if(f.Combo)f.Title+=" · 连招！";
        if(!attack&&f.PlayerText=="")f.PlayerText=enemy?"轮到泡泡":"招式生效";
        if(!enemy){
            var gains=new System.Collections.Generic.List<string>();
            if(MossCards.Id(card)==8)gains.Add(f.Heal>0?"体力 +"+f.Heal:"体力已满");
            if(f.Shield>0)gains.Add("护盾 +"+f.Shield);
            if(f.EnergyDelta>0)gains.Add("能量 +"+f.EnergyDelta);
            if(new[]{6,9,13}.Contains(MossCards.Id(card)))gains.Add("抽 "+f.Drawn+" 张");
            if(gains.Count>0)f.PlayerText=String.Join(" · ",gains);
        }
        cardFeedback=f;
    }
    private Border CardEnergy()
    {
        int energy=G.C.Energy;var row=new DockPanel{LastChildFill=true};
        var label=Ct("能量  "+energy+(energy>3?"":" / 3"),18,energy==0?U.Color("#764D3C"):U.Green);label.Margin=new Thickness(0,0,12,0);DockPanel.SetDock(label,Dock.Left);row.Children.Add(label);
        var dots=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};DockPanel.SetDock(dots,Dock.Left);row.Children.Add(dots);
        for(int i=0;i<3;i++)dots.Children.Add(new System.Windows.Shapes.Ellipse{Width=13,Height=13,Margin=new Thickness(0,0,5,0),Fill=i<energy?U.Green:Brushes.Transparent,Stroke=U.Green,StrokeThickness=1.5});
        if(energy>3)dots.Children.Add(Ct("+"+(energy-3),12,U.Green));
        string help=energy>3?"含 "+(energy-3)+" 点额外能量 · 下回合回到 3":energy==0?(G.C.Hand.Any(c=>MossCards.All[MossCards.Id(c)].Cost==0)?"可打 0 费牌，或结束回合":"用完啦，点「结束回合」补满"):"打牌会消耗 · 每回合补到 3";
        var text=Ct(help,11,U.Ink);text.Margin=new Thickness(0);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);
        var box=new Border{Background=U.Color("#E1EBDA"),BorderBrush=U.Green,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,3,0,7),Child=row};
        AutomationProperties.SetAutomationId(box,"cards-energy");AutomationProperties.SetName(box,"当前能量 "+energy+"，每回合补到 3。"+help);return box;
    }
}
