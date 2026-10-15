using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private int handbookPage,journeyDepth;
    private void OpenHandbook(int page){SelectHandbookPage(page);resetScroll=true;Go("手册");}
    private void Handbook()
    {
        var stack=new StackPanel();body.Children.Add(stack);
        Heading(stack,"一点点，把旅途走远","完成成长目标，收集旅行信物，再尝试更深处。成长奖励只领一次，进度永久保留。");
        U.Row(stack,Btn("成长目标",()=>SelectHandbookPage(0),true,handbookPage==0),Btn("探索地图 · "+G.S.ExpeditionClears.Count(x=>x>0)+" / 6",()=>SelectHandbookPage(1),true,handbookPage==1),Btn("旅行信物 · "+G.S.ExpeditionClears.Count(x=>x>0)+" / 6",()=>SelectHandbookPage(2),true,handbookPage==2),Btn("旅行手记",()=>SelectHandbookPage(3),true,handbookPage==3),Btn("旅途灵感",()=>SelectHandbookPage(4),true,handbookPage==4));
        if(handbookPage==0)ChapterPage(stack);else if(handbookPage==1)ExpeditionPage(stack);else if(handbookPage==2)RelicPage(stack);else if(handbookPage==3)JourneyHistory(stack);else InspirationBook(stack);
    }
    private void ChapterAction(int chapter){if(chapter==0||chapter==3)Go("农场");else if(chapter==1)Go("伙伴");else if(chapter==4||chapter==6)Go("进化");else if(chapter==5){SelectHandbookPage(2);}else Go("冒险");}
    private void RelicPage(StackPanel stack)
    {
        stack.Children.Add(U.Text("每趟最多携带 2 件，可免费替换。通关首次获得时，会自动放入空位；旅途中锁定这趟配置。",13,U.Muted));
        var slots=new UniformGrid{Columns=2};stack.Children.Add(slots);
        for(int s=0;s<2;s++){
            int slot=s,id=G.S.EquippedRelics[s];var p=new StackPanel();p.Children.Add(U.Text("行囊位置 "+(s+1),12,U.Muted));
            var row=new DockPanel();var icon=new RelicView{Relic=id,Width=44,Height=44,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(U.Text(id<0?"空位 · 从下方选择":FarmGame.RelicNames[id],17,U.Green));p.Children.Add(row);
            p.Children.Add(Btn("卸下信物",()=>G.EquipRelic(-1,slot),G.AtHome&&id>=0));var card=U.Card(p,10);card.Background=U.Pale;card.Margin=new Thickness(0,0,8,10);slots.Children.Add(card);
        }
        var grid=new UniformGrid{Columns=2};stack.Children.Add(grid);
        for(int i=0;i<6;i++){
            int id=i;bool owned=G.RelicOwned(i);var p=new StackPanel();var head=new DockPanel();var icon=new RelicView{Relic=i,Width=46,Height=46,Margin=new Thickness(0,0,9,0),Opacity=owned?1:.35};DockPanel.SetDock(icon,Dock.Left);head.Children.Add(icon);head.Children.Add(U.Text(FarmGame.RelicNames[i]+(owned?"":" · 未发现"),17,owned?U.Green:U.Muted));p.Children.Add(head);
            p.Children.Add(U.Text(FarmGame.RelicDetails[i],13));p.Children.Add(U.Text(owned?G.RelicAdvice(i):"首通「"+FarmGame.ExpeditionName(i/3,i%3)+"」获得。",12,U.Muted));
            if(owned)U.Row(p,Btn(G.S.EquippedRelics[0]==i?"✓ 位置 1":"放入位置 1",()=>G.EquipRelic(id,0),G.AtHome,G.S.EquippedRelics[0]==i),Btn(G.S.EquippedRelics[1]==i?"✓ 位置 2":"放入位置 2",()=>G.EquipRelic(id,1),G.AtHome,G.S.EquippedRelics[1]==i));
            else p.Children.Add(Btn("查看探索地图",()=>SelectHandbookPage(1)));
            var card=U.Card(p,12);card.Margin=new Thickness(0,0,8,10);grid.Children.Add(card);
        }
        stack.Children.Add(Btn(G.AtHome?"配好了，准备出发":"查看当前旅程",()=>Go("冒险"),true,true));
    }
    private void JourneyDestination(StackPanel plan)
    {
        if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth)){journeyDepth=0;if(!G.ExpeditionUnlocked(journeyDifficulty,0))journeyDifficulty=0;}
        plan.Children.Add(U.Text("选择目的地",18));
        var map=new ComboBox{ItemsSource=G.CaveUnlocked?new[]{"小溪远足","潮汐洞穴"}:new[]{"小溪远足"},SelectedIndex=journeyDifficulty,FontSize=14,Padding=new Thickness(7),Margin=new Thickness(0,3,0,7)};
        map.SelectionChanged+=(s,e)=>{if(map.SelectedIndex>=0)Do(()=>{SelectJourneyDestination(map.SelectedIndex,0);});};plan.Children.Add(map);
        int count=Enumerable.Range(0,3).Count(t=>G.ExpeditionUnlocked(journeyDifficulty,t));
        var depth=new ComboBox{ItemsSource=FarmGame.DepthNames.Take(count).ToArray(),SelectedIndex=journeyDepth,FontSize=14,Padding=new Thickness(7),Margin=new Thickness(0,3,0,7)};
        depth.SelectionChanged+=(s,e)=>{if(depth.SelectedIndex>=0)Do(()=>SelectJourneyDestination(journeyDifficulty,depth.SelectedIndex));};plan.Children.Add(depth);
        plan.Children.Add(U.Text("建议 "+FarmGame.ExpeditionLevel(journeyDifficulty,journeyDepth)+"\n通关基础肥料 "+FarmGame.ExpeditionFertilizer(journeyDifficulty,journeyDepth)+"、晶石 "+FarmGame.ExpeditionCrystals(journeyDifficulty,journeyDepth),13,U.Muted));
        plan.Children.Add(U.Text(FarmGame.ExpeditionRule(journeyDepth),12));
        int id=journeyDifficulty*3+journeyDepth;plan.Children.Add(U.Text(G.RelicOwned(id)?"已通关 "+G.S.ExpeditionClears[id]+" 次":"首通发现信物「"+FarmGame.RelicNames[id]+"」",13,U.Green));
        plan.Children.Add(Btn("查看全部深度与解锁条件",()=>OpenHandbook(1)));
    }
    internal void RenderHandbookCheck(string root,string prefix="handbook")
    {
        for(int page=0;page<3;page++){handbookPage=page;Tab="手册";Render();FarmHeadlessTests.Render(this,System.IO.Path.Combine(root,prefix+"-"+page+"-small.png"),764,571);FarmHeadlessTests.Render(this,System.IO.Path.Combine(root,prefix+"-"+page+".png"),924,721);}
    }
    internal void RenderDeepDepartureCheck(string root){journeyDifficulty=1;journeyDepth=2;Tab="冒险";Render();FarmHeadlessTests.Render(this,System.IO.Path.Combine(root,"progression-deep-prepare-small.png"),764,571);}
}

public class RelicView:FrameworkElement
{
    public int Relic=-1;
    protected override void OnRender(DrawingContext d)
    {
        double scale=Math.Min(ActualWidth,ActualHeight)/64;if(scale<=0)return;d.PushTransform(new ScaleTransform(scale,scale));
        var edge=new Pen(U.Color("#496254"),2.5);d.DrawEllipse(U.Pale,null,new Point(32,32),29,29);
        if(Relic==0){d.DrawRoundedRectangle(U.Color("#8CCBD4"),edge,new Rect(17,24,30,31),7,7);d.DrawRectangle(U.Cream,edge,new Rect(25,12,14,14));d.DrawLine(new Pen(U.Color("#BB9161"),6),new Point(24,12),new Point(40,12));d.DrawEllipse(Brushes.White,null,new Point(26,34),3,5);}
        else if(Relic==1){d.DrawEllipse(U.Gold,edge,new Point(32,15),6,6);d.DrawRoundedRectangle(U.Color("#EAD394"),edge,new Rect(18,22,28,25),12,12);d.DrawLine(new Pen(U.Gold,5),new Point(16,47),new Point(48,47));d.DrawEllipse(U.Gold,null,new Point(32,52),4,4);}
        else if(Relic==2){d.DrawRoundedRectangle(U.Color("#D1AF77"),edge,new Rect(15,23,34,32),10,10);d.DrawRectangle(U.Color("#8CA977"),edge,new Rect(22,14,20,10));d.DrawLine(new Pen(U.Green,3),new Point(19,25),new Point(45,25));d.DrawEllipse(U.Gold,null,new Point(32,39),7,8);}
        else if(Relic==3){var shape=new StreamGeometry();using(var c=shape.Open()){c.BeginFigure(new Point(34,10),true,true);c.PolyLineTo(new[]{new Point(48,29),new Point(43,51),new Point(22,54),new Point(14,32)},true,true);}d.DrawGeometry(U.Color("#D88D65"),edge,shape);d.DrawLine(new Pen(U.Color("#FFE4A8"),3),new Point(34,20),new Point(25,41));}
        else if(Relic==4){d.DrawLine(new Pen(U.Green,4),new Point(32,54),new Point(32,25));d.DrawEllipse(U.Color("#99BB7C"),edge,new Point(23,27),12,8);d.DrawEllipse(U.Color("#B6CF8E"),edge,new Point(41,19),11,8);d.DrawEllipse(U.Cream,edge,new Point(32,44),12,9);}
        else if(Relic==5){d.DrawLine(new Pen(U.Green,4),new Point(32,55),new Point(32,29));for(int i=0;i<5;i++){double a=i*Math.PI*2/5;d.DrawEllipse(U.Color("#C7A8D2"),edge,new Point(32+Math.Cos(a)*12,27+Math.Sin(a)*12),8,8);}d.DrawEllipse(U.Gold,edge,new Point(32,27),7,7);}
        else{d.DrawLine(new Pen(U.Muted,2),new Point(24,32),new Point(40,32));d.DrawLine(new Pen(U.Muted,2),new Point(32,24),new Point(32,40));}
        d.Pop();
    }
}
