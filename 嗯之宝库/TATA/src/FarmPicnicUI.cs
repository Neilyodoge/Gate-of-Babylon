using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation;

public partial class FarmPanel
{
    private int picnicPreview;
    private bool picnicPlanting;
    private void OpenPicnic(){picnicPreview=G.S.PlannedPicnic>=0?G.S.PlannedPicnic:0;picnicPlanting=false;Go("便当");resetScroll=true;}
    private void PicnicPage()
    {
        var cols=Columns(236);var left=cols[0];var aside=cols[1];
        if(picnicPlanting){PicnicPlantingPage(left,aside);return;}
        Heading(left,"给这趟旅行，带一点家里的味道。","每趟可带 1 份 · 确认出发时才扣材料 · 空手也能出发");
        var recipes=new UniformGrid{Columns=2};left.Children.Add(recipes);
        for(int i=0;i<FarmGame.PicnicCount;i++){
            int id=i;bool unlocked=G.PicnicUnlocked(id);var content=new DockPanel();
            var choose=Btn(picnicPreview==id?"✓ 正在查看":"看看这份",()=>{picnicPreview=id;resetScroll=true;},true,picnicPreview==id);AutomationProperties.SetName(choose,"查看便当 "+FarmGame.PicnicNames[id]);choose.FontSize=11;choose.Padding=new Thickness(5,5,5,5);DockPanel.SetDock(choose,Dock.Bottom);content.Children.Add(choose);
            var p=new StackPanel();var row=new DockPanel();var icon=new PicnicView{Recipe=id,Width=32,Height=32,Margin=new Thickness(0,0,7,4),Opacity=unlocked?1:.5};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(ReviewText(FarmGame.PicnicNames[id],15,U.Green));p.Children.Add(row);
            p.Children.Add(ReviewText(FarmGame.PicnicDetails[id],12));p.Children.Add(ReviewText(unlocked?FarmGame.PicnicCostText(id):"首通小溪后开放",11,U.Muted));content.Children.Add(p);
            var card=U.Card(content,8);card.Margin=new Thickness(0,0,8,9);if(picnicPreview==id)card.Background=U.Pale;recipes.Children.Add(card);
        }
        left.Children.Add(U.Text("选的是下趟计划，暂不制作；材料仍可用于培养。旅程开始后计划清空，下次自己决定带不带。",11,U.Muted));
        var pack=new StackPanel();pack.Children.Add(U.Text("这趟的便当计划",17));pack.Children.Add(U.Text(FarmGame.PicnicNames[picnicPreview],15,U.Green));
        pack.Children.Add(Live(()=>G.PicnicShortage(picnicPreview),12,U.Gold));
        var cost=FarmGame.PicnicCost(picnicPreview);
        for(int k=0;k<3;k++)if(cost[k]>0){int kind=k;pack.Children.Add(Live(()=>FarmGame.CropNames[kind]+" · 已有 "+G.CropStock(kind)+" / 需要 "+cost[kind],12));}
        var plan=Btn("选好，返回冒险准备",()=>{if(G.PlanJourneyPicnic(picnicPreview,journeyDifficulty,journeyDepth)){Go("冒险");resetScroll=true;}},G.AtHome&&G.PicnicUnlocked(picnicPreview),true);FarmTheme.Accent(plan);pack.Children.Add(plan);
        pack.Children.Add(Btn("这趟不带便当",()=>{if(G.PlanJourneyPicnic(-1,journeyDifficulty,journeyDepth)){Go("冒险");resetScroll=true;}},G.AtHome));
        if(!G.AtHome)pack.Children.Add(U.Text("当前旅程已出发。返家后可安排新的便当。",11,U.Muted));aside.Children.Add(U.Card(pack,12));
        aside.Children.Add(Notice("怎样配合",G.PicnicAdvice(picnicPreview,selectedBlessing)));
        aside.Children.Add(Btn("看看材料怎么种",()=>{picnicPlanting=true;selectedPlanting=cost[2]>0&&G.S.GardenUnlocked?3:cost[0]>cost[1]?1:2;plantingOpen=true;resetScroll=true;}));
        aside.Children.Add(Btn("先免费试打看看",()=>{OpenPractice();if(G.AtHome&&G.PicnicUnlocked(picnicPreview))practiceDraft.Picnic=picnicPreview;},G.AtHome&&G.PicnicUnlocked(picnicPreview)));
        aside.Children.Add(Btn(G.AtHome?"返回冒险准备":"查看当前旅程",()=>Go("冒险")));
    }
    private void PicnicPlantingPage(StackPanel left,StackPanel aside)
    {
        Heading(left,"从田里，准备一份便当。",FarmGame.PicnicNames[picnicPreview]+" · "+FarmGame.PicnicCostText(picnicPreview));
        left.Children.Add(PlantingPlanner());var scene=Scene();scene.Height=scene.MinHeight=174;left.Children.Add(scene);
        left.Children.Add(U.Text("先预览再应用。只有改种的田地重新计时；食谱仍未制作，不扣材料。",12,U.Muted));
        var materials=new StackPanel();materials.Children.Add(U.Text("按现在的田地，要等多久？",16));
        var cost=FarmGame.PicnicCost(picnicPreview);for(int k=0;k<3;k++)if(cost[k]>0){int kind=k;materials.Children.Add(Live(()=>PicnicIngredientWait(kind,cost[kind]),12,U.Green));}
        materials.Children.Add(U.Text("按当前帮手、土壤和成熟批次估算，改种后会更新。",11,U.Muted));aside.Children.Add(U.Card(materials,12));
        aside.Children.Add(Btn("返回便当食谱",()=>{picnicPlanting=false;resetScroll=true;},true,true));aside.Children.Add(Btn("查看完整农场",()=>Go("农场")));
    }
    private string PicnicIngredientWait(int kind,int need)
    {
        int missing=Math.Max(0,need-G.CropStock(kind));double wait=G.CareResourceWait(kind,missing);
        return FarmGame.CropNames[kind]+" · "+(missing==0?"已备齐":wait<0?"还差 "+missing+"，当前未种植":"还差 "+missing+"，约 "+Math.Max(1,(int)Math.Ceiling(wait))+" 秒后收齐");
    }
    private void PicnicDeparture(StackPanel plan)
    {
        if(G.S.PlannedPicnic<0)return;
        var row=new DockPanel{Margin=new Thickness(0,0,0,5)};var cancel=Btn("不带便当",()=>G.PlanJourneyPicnic(-1,journeyDifficulty,journeyDepth));cancel.FontSize=10;cancel.Padding=new Thickness(4,2,4,2);cancel.Margin=new Thickness(3,0,0,0);DockPanel.SetDock(cancel,Dock.Right);row.Children.Add(cancel);row.Children.Add(ReviewText("便当 · "+FarmGame.PicnicName(G.S.PlannedPicnic),12,U.Green));plan.Children.Add(row);
        var price=Live(()=>G.CanPackPicnic(G.S.PlannedPicnic)?"出发消耗："+FarmGame.PicnicCostText(G.S.PlannedPicnic):G.PicnicShortage(G.S.PlannedPicnic),11,U.Gold);price.ToolTip="制作一份共需："+FarmGame.PicnicCostText(G.S.PlannedPicnic);plan.Children.Add(price);
    }
    private UIElement PicnicRunCard()
    {
        var p=new StackPanel();p.Children.Add(U.Text("家里带来的便当",16,U.Green));p.Children.Add(Live(()=>FarmGame.PicnicReport(G.R.Picnic,G.R.PicnicTriggers,G.R.PicnicHealing),12));
        p.Children.Add(U.Text("只在本趟生效；返家后消失。",11,U.Muted));return U.Card(p,12);
    }
    private void PracticePicnicPicker(StackPanel p)
    {
        p.Children.Add(ReviewText("临时搭配的便当 · 免费模拟",11,U.Muted));var ids=Enumerable.Range(-1,FarmGame.PicnicCount+1).Where(G.PicnicUnlocked).ToArray();
        var select=new ComboBox{ItemsSource=ids.Select(FarmGame.PicnicName).ToArray(),SelectedIndex=Array.IndexOf(ids,practiceDraft.Picnic),FontSize=12,Margin=new Thickness(0,3,0,6)};AutomationProperties.SetName(select,"试练便当");
        select.SelectionChanged+=(s,e)=>{if(select.SelectedIndex>=0)Do(()=>practiceDraft.Picnic=ids[select.SelectedIndex]);};p.Children.Add(select);
    }
}

public class PicnicView:FrameworkElement
{
    public int Recipe;
    protected override void OnRender(DrawingContext d)
    {
        d.PushTransform(new ScaleTransform(ActualWidth/64,ActualHeight/64));var line=new Pen(U.Color("#8F7858"),1.6);d.DrawEllipse(U.Color("#E8DFC5"),null,new Point(32,52),26,6);
        if(Recipe==2){d.DrawRoundedRectangle(U.Color("#C5D9B8"),line,new Rect(12,18,34,32),5,5);d.DrawEllipse(null,line,new Point(48,31),9,10);d.DrawEllipse(U.Color("#7F9A62"),line,new Point(29,18),17,5);d.DrawLine(new Pen(U.Color("#B9B2A0"),2),new Point(23,11),new Point(25,3));d.DrawLine(new Pen(U.Color("#B9B2A0"),2),new Point(35,10),new Point(37,2));}
        else if(Recipe==0||Recipe==5){d.DrawGeometry(U.Color(Recipe==0?"#F4E7BF":"#B9CC91"),line,Geometry.Parse("M10,45 Q6,49 14,52 L50,52 Q59,49 54,43 L36,12 Q32,6 27,13 Z"));d.DrawRoundedRectangle(U.Color("#567E5A"),null,new Rect(24,34,17,19),3,3);d.DrawEllipse(U.Color(Recipe==0?"#CC7A70":"#E8C67D"),null,new Point(30,24),6,5);}
        else if(Recipe==3){for(int i=0;i<3;i++){double x=20+i*12,y=i==1?25:37;d.DrawEllipse(U.Color("#B98AB5"),line,new Point(x,y),11,12);d.DrawEllipse(U.Color("#6F9267"),null,new Point(x+3,y-11),7,3);}d.DrawGeometry(U.Color("#F2D68A"),null,Geometry.Parse("M35,18 L25,34 L33,32 L29,44 L44,27 L35,28 Z"));}
        else{d.DrawRoundedRectangle(U.Color(Recipe==1?"#E7BD76":"#C89565"),line,new Rect(7,19,50,33),12,12);for(int i=0;i<3;i++)d.DrawLine(new Pen(U.Color(Recipe==1?"#F7DFAB":"#856449"),3),new Point(19+i*11,27),new Point(15+i*11,41));d.DrawEllipse(U.Color("#EEE1BB"),null,new Point(24,13),3,2);}
        d.Pop();
    }
}
