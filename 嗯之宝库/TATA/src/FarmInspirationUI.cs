using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private bool inspirationPreferencesOpen,inspirationDiscoveredOnly;
    private FarmRun inspirationChoiceRun;
    private int inspirationChoiceStation=-1;
    private bool inspirationChoicesOpen;
    private static readonly string[] GentleInspirationEffects={
        "电团追击湿漉漉的敌人时，会打得更有力。","电团的追击还能跳向另一名敌人。","每回合，给走在前面的伙伴加一层护盾。","苔壳每次都能多恢复一些体力。","赤钳的攻击会留下更多火种。","大家的体力上限提高，还能恢复一些体力。",
        "每场刚开打时，伙伴的攻击更有力。","每隔三个回合，大家一起追加一次攻击。","打倒敌人后，帮最虚弱的伙伴恢复体力。","种子的攻击会让敌人中毒更深。","种子引爆余烬时，能造成更多伤害。","电团追击时，也会保护前面的伙伴。"};
    private UIElement InspirationPreferences()
    {
        var p=new StackPanel();p.Children.Add(U.Text("三场小战后各遇见一组三选一；不用守着屏幕，小队会自己挑。每种最多 2 层，只在本趟生效。",12,U.Muted));
        var choices=new UniformGrid{Columns=3};p.Children.Add(choices);
        for(int i=0;i<3;i++){int style=i;var card=new StackPanel();card.Children.Add(Btn(FarmGame.InspirationStyles[i],()=>G.SetInspirationStyle(style),G.AtHome,G.S.InspirationStyle==i));card.Children.Add(U.Text(FarmGame.InspirationStyleDetails[i],11,U.Muted));card.Margin=new Thickness(0,0,7,0);choices.Children.Add(card);}
        p.Children.Add(Btn("看看十二种旅途灵感",()=>OpenHandbook(4)));
        var expand=new Expander{Header="旅途灵感 · "+FarmGame.InspirationStyles[G.S.InspirationStyle]+" · 自动三选一",Content=p,IsExpanded=inspirationPreferencesOpen};expand.Expanded+=(s,e)=>inspirationPreferencesOpen=true;expand.Collapsed+=(s,e)=>inspirationPreferencesOpen=false;return expand;
    }
    private void InspirationChoicePage(StackPanel left,StackPanel aside)
    {
        var run=G.R;int station=run.Node,recommended=G.RecommendedInspiration;
        if(!Object.ReferenceEquals(inspirationChoiceRun,run)||inspirationChoiceStation!=station){inspirationChoiceRun=run;inspirationChoiceStation=station;inspirationChoicesOpen=false;}
        bool gentle=GentleJourney,folded=gentle&&!inspirationChoicesOpen;
        Heading(left,"路上，冒出一个新点子。",gentle?"伙伴会自己挑，你可以安心去忙。":"第 "+(run.InspirationPicks.Length+1)+" / 3 次灵感 · 选择一份，继续这趟旅程");
        if(!gentle)left.Children.Add(TravelTrack());
        if(gentle)left.Children.Add(Live(()=>run.JourneyPaused?"已暂停。可以先挑，挑完仍然暂停。":"约 "+Math.Max(0,(int)Math.Ceiling((run.NextJourneyTicks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond))+" 秒后自动选好，继续往前走。",14,U.Green));
        var cards=new UniformGrid{Columns=folded?1:3};left.Children.Add(cards);
        foreach(int offered in folded?new[]{recommended}:run.InspirationOffers){int id=offered;var dock=new DockPanel();
            var choose=Btn(folded?"就选这个，继续走":"选「"+FarmGame.InspirationNames[id]+"」",()=>{if(Object.ReferenceEquals(run,G.R))G.ChooseInspiration(id,station);},true,id==recommended);choose.FontSize=folded?13:11;choose.Padding=new Thickness(3,7,3,7);choose.Margin=new Thickness(0);DockPanel.SetDock(choose,Dock.Bottom);dock.Children.Add(choose);
            System.Windows.Automation.AutomationProperties.SetAutomationId(choose,"inspiration-choice-"+id);
            if(WorldGuide&&!run.JourneyPaused&&id==recommended){HighlightGuideAction(choose);System.Windows.Automation.AutomationProperties.SetAutomationId(choose,"inspiration-choice-"+id);}
            var p=new StackPanel();p.Children.Add(new InspirationView{Ability=id,Height=folded?52:40,Margin=new Thickness(0,0,0,6)});p.Children.Add(ReviewText(FarmGame.InspirationNames[id],folded?18:15,U.Green));
            p.Children.Add(ReviewText(folded?"伙伴推荐":(id==recommended?"小队推荐 · ":"")+"第 "+(run.Inspirations[id]+1)+" 层",folded?12:10,U.Muted));
            p.Children.Add(U.Text(folded?GentleInspirationEffects[id]:FarmGame.InspirationDetails[id],folded?15:12));dock.Children.Add(p);var card=U.Card(dock,folded?16:9);card.Margin=new Thickness(0,0,folded?0:6,8);if(id==recommended)card.Background=U.Pale;cards.Children.Add(card);
        }
        if(gentle)left.Children.Add(Btn(folded?"我想换一个":"收起，交给伙伴选",()=>{if(Object.ReferenceEquals(run,G.R)&&G.R.Phase=="inspiration"&&G.R.Node==station)inspirationChoicesOpen=!inspirationChoicesOpen;}));
        else{
            left.Children.Add(Live(()=>run.JourneyPaused?"已暂停：可以挑一份，挑完仍保持暂停。":"约 "+Math.Max(0,(int)Math.Ceiling((run.NextJourneyTicks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond))+" 秒后，小队自动选择「"+FarmGame.InspirationNames[recommended]+"」。",12,U.Green));
            left.Children.Add(U.Text("关闭、缩小或隐藏后，小队照常挑选；不需要回来点确认。选择和选项会自动保存。",11,U.Muted));
        }
        JourneyAside(aside);if(!folded)aside.Children.Insert(1,Notice("小队为什么推荐它",G.InspirationReason(recommended)));
    }
    private UIElement InspirationRunCard()
    {
        var p=new StackPanel();p.Children.Add(U.Text("这趟学会的小配合",16));p.Children.Add(U.Text("自动偏好 · "+FarmGame.InspirationStyles[G.R.InspirationStyle],11,U.Muted));
        if(G.R.Inspirations.All(n=>n==0))p.Children.Add(U.Text("第一场胜利后，遇见第一组旅途灵感。",12,U.Muted));
        foreach(int entry in Enumerable.Range(0,FarmGame.InspirationCount).Where(id=>G.R.Inspirations[id]>0)){
            int id=entry;var row=new DockPanel();var icon=new InspirationView{Ability=id,Width=28,Height=30,Margin=new Thickness(0,0,7,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(Live(()=>FarmGame.InspirationNames[id]+" ×"+G.R.Inspirations[id]+"\n"+(id==5?"体力上限在本趟持续生效":"触发 "+G.R.InspirationTriggers[id]+" 次"),11,U.Green));p.Children.Add(row);
        }
        var detail=new StackPanel();detail.Children.Add(U.Text(FarmGame.InspirationHistory(G.R.InspirationPicks),11,U.Muted));detail.Children.Add(U.Text("触发次数表示满足能力条件，额外伤害不单独归因；实际伤害、治疗和挡伤见逐场复盘。",10,U.Muted));p.Children.Add(new Expander{Header="查看选择过程",Content=detail});return U.Card(p,10);
    }
    private void InspirationBook(StackPanel stack)
    {
        var row=new DockPanel();var filter=Btn(inspirationDiscoveredOnly?"查看全部灵感":"只看已领悟",()=>{inspirationDiscoveredOnly=!inspirationDiscoveredOnly;resetScroll=true;});DockPanel.SetDock(filter,Dock.Right);row.Children.Add(filter);row.Children.Add(U.Text("已领悟 "+G.S.DiscoveredInspirations.Count(v=>v)+" / 12 · 选择过的灵感会留下足迹",16,U.Green));stack.Children.Add(row);
        stack.Children.Add(U.Text("所有说明都可以预览。每次冒险重新获得，最多选择 3 次，同一种最多 2 层；图鉴收藏不增加永久属性。",12,U.Muted));
        var grid=new UniformGrid{Columns=3};stack.Children.Add(grid);int visible=0;
        for(int i=0;i<FarmGame.InspirationCount;i++){
            int id=i;bool known=G.S.DiscoveredInspirations[i];if(inspirationDiscoveredOnly&&!known)continue;visible++;var p=new StackPanel();var header=new DockPanel();var icon=new InspirationView{Ability=i,Width=36,Height=38,Margin=new Thickness(0,0,7,0)};DockPanel.SetDock(icon,Dock.Left);header.Children.Add(icon);header.Children.Add(U.Text(FarmGame.InspirationNames[i],16,U.Green));p.Children.Add(header);p.Children.Add(U.Text(known?"✓ 已领悟":"尚未选过 · 可预览",11,U.Muted));p.Children.Add(U.Text(FarmGame.InspirationDetails[i],12));p.Children.Add(U.Text("适合："+FarmGame.InspirationNeeds[i],11,U.Muted));var card=U.Card(p,10);card.Margin=new Thickness(0,0,8,9);if(known)card.Background=U.Pale;grid.Children.Add(card);
        }
        if(visible==0)stack.Children.Add(Notice("第一份旅途灵感，等你发现。","新旅程的第一场胜利后就会遇见；小队自动挑选也算领悟。"));
        stack.Children.Add(Btn(G.AtHome?"去准备一趟冒险":"查看当前旅程",()=>Go("冒险"),true,true));
    }
    private UIElement InspirationReview(BattleNote note)
    {
        var p=new StackPanel();p.Children.Add(U.Text("当场携带的灵感",16));
        foreach(int id in Enumerable.Range(0,FarmGame.InspirationCount).Where(i=>note.Inspirations[i]>0))p.Children.Add(U.Text(FarmGame.InspirationNames[id]+" ×"+note.Inspirations[id]+" · "+(id==5?"体力上限生效":"触发 "+note.InspirationTriggers[id]+" 次"),11,U.Green));
        p.Children.Add(U.Text("只记录这场战斗；之后获得的灵感不会回填。触发次数不等同于额外伤害。",10,U.Muted));return U.Card(p,10);
    }
}

public class InspirationView:FrameworkElement
{
    public int Ability;
    protected override void OnRender(DrawingContext d)
    {
        double size=Math.Min(ActualWidth,ActualHeight);if(size<=0)return;d.PushTransform(new TranslateTransform((ActualWidth-size)/2,0));d.PushTransform(new ScaleTransform(size/48,size/48));
        int family=Ability==0||Ability==1?0:Ability==2||Ability==11?1:Ability==4||Ability==10?2:Ability==9?3:Ability==6||Ability==7?4:5;
        string[] papers={"#F0E8BE","#DCE8DF","#F4DDCF","#E1D9EA","#DEE9ED","#E2EACD"},inks={"#AC8B36","#528573","#C6784C","#9071A0","#638CA0","#739654"};var ink=U.Color(inks[family]);var pen=new Pen(ink,2);d.DrawEllipse(U.Color(papers[family]),null,new Point(24,24),22,22);
        if(family==0)d.DrawGeometry(ink,null,Geometry.Parse("M26,7 L13,26 L23,25 L20,41 L36,20 L26,21 Z"));
        else if(family==1){d.DrawGeometry(null,pen,Geometry.Parse("M11,12 L24,8 L37,12 L35,29 Q31,36 24,41 Q17,36 13,29 Z"));d.DrawLine(pen,new Point(24,16),new Point(24,31));d.DrawLine(pen,new Point(17,23),new Point(31,23));}
        else if(family==2){d.DrawGeometry(ink,null,Geometry.Parse("M24,7 C30,18 39,21 35,33 Q29,44 18,37 Q7,31 15,21 Q15,28 22,24 Q27,17 24,7 Z"));d.DrawEllipse(U.Color("#F9EBC4"),null,new Point(24,31),5,7);}
        else if(family==3){d.DrawEllipse(ink,null,new Point(18,28),6,9);d.DrawEllipse(ink,null,new Point(31,22),5,8);d.DrawLine(pen,new Point(21,21),new Point(24,12));d.DrawLine(pen,new Point(32,15),new Point(37,11));}
        else if(family==4){d.DrawLine(pen,new Point(12,17),new Point(35,17));d.DrawLine(pen,new Point(9,24),new Point(30,24));d.DrawLine(pen,new Point(15,31),new Point(36,31));d.DrawEllipse(ink,null,new Point(33,24),3,3);}
        else{d.DrawLine(pen,new Point(23,39),new Point(25,14));d.DrawEllipse(ink,null,new Point(17,21),8,5);d.DrawEllipse(ink,null,new Point(31,16),7,5);d.DrawLine(new Pen(U.Color("#A2B986"),2),new Point(24,30),new Point(32,25));}
        if(Ability==1||Ability==10||Ability==11){d.DrawEllipse(U.Cream,pen,new Point(36,36),7,7);d.DrawLine(pen,new Point(32,36),new Point(40,36));d.DrawLine(pen,new Point(36,32),new Point(36,40));}d.Pop();d.Pop();
    }
}
