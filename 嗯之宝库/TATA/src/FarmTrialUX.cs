using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private string farmReturnTab="农场";
    private string evolutionReceipt="";
    private bool workshopOptionsOpen;
    private void OpenTeamFarm(){Go("农田");farmReturnTab="配队";plantingOpen=true;selectedPlanting=2;resetScroll=true;}
    private void SelectEvolutionFamily(int pet){evolutionFocusPet=pet;previewSeed=-1;evolutionReceipt="";resetScroll=true;}
    private void FarmManagementPage()
    {
        var cols=Columns(232);var left=cols[0];var right=cols[1];
        Heading(left,"选好作物，农田会自己忙","成熟后自动收获、补种。下面可以单独改种，也可以展开种植计划。");
        left.Children.Add(PlantingPlanner());var plots=new UniformGrid{Columns=3};left.Children.Add(plots);
        for(int i=0;i<6;i++){
            int plot=i;var p=new StackPanel();bool open=i<G.Plots;
            p.Children.Add(U.Text("田地 "+(i+1)+(open?"":" · 未开放"),13));
            if(open){
                var combo=new ComboBox{ItemsSource=FarmGame.CropNames.Take(G.S.GardenUnlocked?3:2).ToArray(),SelectedIndex=G.S.Crops[i],FontSize=11,Margin=new Thickness(0,2,0,5),ToolTip="选择后立即改种，这块田重新计时。"};AutomationProperties.SetName(combo,"田地 "+(i+1)+" 作物");
                combo.SelectionChanged+=(s,e)=>{if(combo.SelectedIndex>=0&&combo.SelectedIndex!=G.S.Crops[plot])Do(()=>G.Plant(plot,combo.SelectedIndex));};p.Children.Add(combo);
                p.Children.Add(Live(()=>G.SecondsLeft(plot)+" 秒后 +"+G.CropYield(G.S.Crops[plot]),11,U.Green));var progress=U.Bar(0,100);updates.Add(()=>progress.Value=100*G.S.Growth[plot]/G.CropSeconds(G.S.Crops[plot]));p.Children.Add(progress);
            }else{p.Children.Add(new FarmGlyph{Kind="farm",Height=30,Opacity=.4});p.Children.Add(U.Text("首通小溪后开放",11,U.Muted));}
            var card=U.Card(p,9);card.Margin=new Thickness(0,4,7,6);plots.Children.Add(card);
        }
        left.Children.Add(U.Text("改种只重计这块田；其他田地继续生长。离线最多积攒 4 小时收成。",11,U.Muted));
        left.Children.Add(Live(()=>G.LastHarvest,11,U.Green));var scene=Scene();scene.Height=scene.MinHeight=128;left.Children.Add(scene);
        if(farmReturnTab=="配队")right.Children.Add(Btn("返回配队",()=>Go("配队"),true,true));
        if(farmReturnTab=="新手引导")right.Children.Add(Btn("回去喂"+G.Name(GuidePet),()=>Go("新手引导"),true,true));
        else right.Children.Add(Btn("返回农场首页",()=>Go("农场"),true,farmReturnTab!="配队"));
        var uses=new StackPanel();uses.Children.Add(U.Text("缺什么，就种什么",16));
        string[] names={"莓果 → 喂养、亲密","麦穗 → 训练升级","薄荷 → 点心、便当"},icons={"berry","grain","mint"};
        for(int i=0;i<3;i++){int kind=i;var row=new DockPanel();var icon=new FarmGlyph{Kind=icons[i],Width=23,Height=30,Margin=new Thickness(0,0,7,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);var text=U.Text(names[i]+(kind==2&&!G.S.GardenUnlocked?"\n首通小溪后可种":""),11,U.Muted);row.Children.Add(text);uses.Children.Add(row);}right.Children.Add(U.Card(uses,10));
        var soil=new StackPanel();soil.Children.Add(U.Text("土壤 Lv."+G.S.SoilLevel+" / 5",16));soil.Children.Add(U.Text("每级让全田生长速度增加 12%，肥料来自冒险。",11,U.Muted));
        int level=G.S.SoilLevel;var upgrade=Btn("",()=>{if(G.S.SoilLevel==level)G.UpgradeSoil();});soil.Children.Add(upgrade);updates.Add(()=>{upgrade.Content=G.S.SoilLevel>=5?"土壤已经满级":"施肥升级 · 肥料 "+G.SoilCost;upgrade.IsEnabled=G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost;});
        soil.Children.Add(Live(()=>G.S.SoilLevel>=5?"所有田地持续享受加速。":G.S.Fertilizer<G.SoilCost?"还差肥料 "+(G.SoilCost-G.S.Fertilizer)+"，去冒险带回来。":"材料已齐，点击后升一级。",11,U.Muted));soil.Children.Add(Btn(G.AtHome?"去冒险获取肥料":"查看当前冒险",()=>Go("冒险")));right.Children.Add(U.Card(soil,10));
        right.Children.Add(U.Text("农场帮手："+G.Name(G.Caretaker)+"\n"+G.CaretakerEffect,11,U.Muted));right.Children.Add(Btn("调整帮手与队伍",()=>Go("配队")));right.Children.Add(Btn("布置小院",()=>OpenGarden()));
    }
    private void BubbleEvolutionAside(StackPanel aside)
    {
        var p=new StackPanel();p.Children.Add(U.Text(G.S.Evolved?"已经迎来新形态":"准备泡泡的下一步",17));
        p.Children.Add(Live(()=>"等级 "+G.Level(0)+" / 3 · 亲密 "+G.S.Bond+" / 3",12,U.Green));
        p.Children.Add(Live(()=>G.S.Evolved?"进化已完成，等级与站位保留。":"进化需晶石 2 · 已有 "+G.S.Crystals,12,U.Muted));
        if(!G.S.Evolved)p.Children.Add(Btn("准备泡泡进化材料",()=>OpenCare(0,true)));
        p.Children.Add(Btn(G.AtHome?"去冒险获取晶石":"查看当前旅程",()=>Go("冒险")));aside.Children.Add(U.Card(p,12));
        aside.Children.Add(Notice("材料从哪里来","莓果喂养增加成长与亲密，麦穗补足等级；小溪初探通关带回晶石 2。"));
        aside.Children.Add(Btn("返回伙伴培养",()=>{SelectFocusedPet(0);Go("伙伴");}));
    }
}
