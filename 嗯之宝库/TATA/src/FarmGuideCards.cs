using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation;

public sealed class GuideGrowthReceipt
{
    public FarmSave State;
    public int Pet,BeforeXp,AfterXp,BeforeLevel,AfterLevel,BeforeAttack,AfterAttack,BeforeHp,AfterHp;
}

public partial class FarmPanel
{
    private GuideGrowthReceipt growthReceipt;
    private FarmSave lessonPlotState;
    private int lessonPlot=-1;
    private static readonly Brush GrowthInk=U.Color("#27456E"),GrowthFill=U.Color("#429DD1"),GrowthPaper=U.Color("#EDF6FC");
    private GuideGrowthReceipt CurrentGrowthReceipt(int pet)
    {
        return growthReceipt!=null&&Object.ReferenceEquals(growthReceipt.State,G.S)&&growthReceipt.Pet==pet&&growthReceipt.AfterXp==G.S.Xp[pet]?growthReceipt:null;
    }
    private UIElement GrowthCard(int pet,bool compact=false,bool showReceipt=true)
    {
        var receipt=showReceipt?CurrentGrowthReceipt(pet):null;bool levelled=receipt!=null&&receipt.AfterLevel>receipt.BeforeLevel;
        var p=new StackPanel();var row=new DockPanel();p.Children.Add(row);
        var icon=new Image{Source=Art.Icon("water"),Width=compact?24:30,Height=compact?24:30,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
        string caption=levelled?"Lv."+receipt.BeforeLevel+"  →  Lv."+receipt.AfterLevel:receipt!=null&&receipt.AfterXp>receipt.BeforeXp?"成长 +"+(receipt.AfterXp-receipt.BeforeXp):"成长";
        var label=U.Text(caption,compact?13:16,GrowthInk);label.Margin=new Thickness(0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
        if(receipt!=null)AutomationProperties.SetAutomationId(label,"focused-care-receipt");
        var bar=U.Bar(0,1,GrowthFill);bar.Height=10;bar.Margin=new Thickness(0,8,0,5);AutomationProperties.SetAutomationId(bar,"guide-growth-progress");p.Children.Add(bar);
        var hint=U.Text("",12,GrowthInk);hint.Margin=new Thickness(0);hint.TextAlignment=TextAlignment.Center;p.Children.Add(hint);
        updates.Add(()=>{
            int start=G.LevelStartXp(pet),end=G.NextXp(pet);
            if(levelled||G.Level(pet)>=10){bar.Maximum=1;bar.Value=1;hint.Text=levelled?"成长满啦！":"已经长大啦";}
            else{bar.Maximum=Math.Max(1,end-start);bar.Value=Math.Max(0,G.S.Xp[pet]-start);hint.Text=(G.S.Xp[pet]-start)+" / "+(end-start)+"  ·  Lv."+G.Level(pet)+" → Lv."+(G.Level(pet)+1);}
        });
        var card=new Border{Child=p,Background=GrowthPaper,BorderBrush=U.Color("#88BADA"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(16),Padding=new Thickness(14,8,14,8),MaxWidth=320,Margin=new Thickness(0,5,0,4)};
        AutomationProperties.SetAutomationId(card,"guide-growth-card");return card;
    }
    private UIElement GrowthDetails(int pet)
    {
        var receipt=CurrentGrowthReceipt(pet);var p=new StackPanel();
        Action<string,string,int,int> stat=(icon,title,before,after)=>{
            var row=new DockPanel{Margin=new Thickness(0,2,0,2)};var art=new Image{Source=Art.Icon(icon),Width=26,Height=26,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(art,Dock.Left);row.Children.Add(art);
            row.Children.Add(U.Text(title+"  "+(receipt==null?after.ToString():before+" → "+after),13,GrowthInk));p.Children.Add(row);
        };
        stat("warrior","攻击",receipt==null?G.Attack(pet):receipt.BeforeAttack,G.Attack(pet));stat("tank","体力",receipt==null?G.PermanentHp(pet):receipt.BeforeHp,G.PermanentHp(pet));
        return new Expander{Header="看看长大后的变化",Content=p,MaxWidth=320,Margin=new Thickness(0,2,0,0)};
    }
    private int LessonBerryPlot()
    {
        if(!Object.ReferenceEquals(lessonPlotState,G.S)){lessonPlotState=G.S;lessonPlot=-1;}
        // Keep watching the same real plot after it is harvested and replanted.
        if(lessonPlot>=0&&lessonPlot<G.Plots&&G.S.Crops[lessonPlot]==0)return lessonPlot;
        lessonPlot=-1;
        var harvest=G.RecentBerryHarvest;
        if(G.S.GuideHomeStage==HomeGuideStage.FreshBerries&&harvest!=null&&Object.ReferenceEquals(harvest.State,G.S))
            lessonPlot=harvest.BerryPlots.Where(i=>i>=0&&i<G.Plots&&G.S.Crops[i]==0).DefaultIfEmpty(-1).First();
        if(lessonPlot<0)lessonPlot=Enumerable.Range(0,G.Plots).Where(i=>G.S.Crops[i]==0).OrderBy(i=>G.SecondsLeft(i)).DefaultIfEmpty(-1).First();
        return lessonPlot;
    }
    private UIElement HarvestProgress(int plot,bool collected)
    {
        var p=new StackPanel{MaxWidth=330,Margin=new Thickness(0,8,0,2)};
        var row=new DockPanel();p.Children.Add(row);var icon=new FarmGlyph{Kind="berry",Width=22,Height=22,Margin=new Thickness(0,0,6,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
        var label=U.Text("",13,U.Green);label.Margin=new Thickness(0);row.Children.Add(label);
        var bar=U.Bar(0,100);bar.Height=11;bar.Margin=new Thickness(0,7,0,0);p.Children.Add(bar);AutomationProperties.SetAutomationId(bar,"guide-harvest-progress");
        updates.Add(()=>{bool ready=collected||G.S.GuideHomeStage==HomeGuideStage.FreshBerries;bool valid=plot>=0&&plot<G.Plots&&G.S.Crops[plot]==0;
            bar.Value=ready?100:valid?Math.Min(100,100*G.S.Growth[plot]/G.CropSeconds(0)):0;
            label.Text=ready?"已收好 +"+G.S.GuideHarvestBerries+" 颗莓果":valid?"约 "+G.SecondsLeft(plot)+" 秒后收好":"还没有种莓果";
        });return p;
    }
    private bool FarmIntroduction{get{return G.S.GuideFarmLesson==1&&G.S.GuideStep==3&&G.AtHome&&G.S.GuideHomeStage<=HomeGuideStage.FreshBerries;}}
    private void BeginWatchingFarm(FarmSave state)
    {
        if(!Object.ReferenceEquals(state,G.S)||!FarmIntroduction)return;G.S.GuideFarmLesson=2;G.Save();resetScroll=true;
    }
}
