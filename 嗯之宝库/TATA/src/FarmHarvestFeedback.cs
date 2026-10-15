using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

public sealed class FarmHarvestMoment
{
    public FarmSave State;
    public int[] BerryPlots;
    public int Berries;
    public DateTime Started;
}

public partial class FarmGame
{
    // Transient visual evidence of the actual harvest; never serialized or replayed as a reward.
    public FarmHarvestMoment RecentBerryHarvest;
}

public partial class FarmScene
{
    public bool ShowBerryBasket;
    internal double HarvestPreviewSeconds=-1;
    internal bool HasRecentBerryHarvest{get{return Game.RecentBerryHarvest!=null&&Object.ReferenceEquals(Game.RecentBerryHarvest.State,Game.S)&&(DateTime.UtcNow-Game.RecentBerryHarvest.Started).TotalSeconds<4;}}
    private void DrawBerryBasket(DrawingContext d,double w,double h)
    {
        if(!ShowBerryBasket)return;
        double x=w*.73,y=Math.Max(30,h-96);var edge=new Pen(U.Color("#92714A"),1.7);
        d.DrawEllipse(U.Color("#91A877"),null,new Point(x+19,y+25),23,4);
        d.DrawEllipse(null,new Pen(U.Color("#B18B55"),3),new Point(x+19,y-7),15,17);
        if(Game.S.Berries>0){CareFeedbackArt.Berry(d,x+13,y-4,18);CareFeedbackArt.Berry(d,x+26,y-1,17);}
        d.DrawRoundedRectangle(U.Color("#D8B67B"),edge,new Rect(x,y,39,23),5,5);
        for(int i=1;i<4;i++)d.DrawLine(new Pen(U.Color("#B7935D"),1),new Point(x+i*9,y+2),new Point(x+i*9,y+20));
        d.DrawLine(edge,new Point(x+2,y+9),new Point(x+37,y+9));d.DrawLine(edge,new Point(x+2,y+16),new Point(x+37,y+16));
        if(LearningPlot<0)CenterLabel(d,"莓果篮",x+19,y+25,11,U.Ink,w*.23);
        if(!HasRecentBerryHarvest)return;
        var harvest=Game.RecentBerryHarvest;double seconds=HarvestPreviewSeconds>=0?HarvestPreviewSeconds:(DateTime.UtcNow-harvest.Started).TotalSeconds;
        if(LearningPlot<0)CenterLabel(d,"+"+harvest.Berries+" 已收好",x+19,y-23,12,U.Green,w*.27,true);
        double plotW=(w-32)/Game.Plots;
        foreach(int plot in harvest.BerryPlots.Where(p=>LearningPlot<0||p==LearningPlot))d.DrawRoundedRectangle(null,new Pen(FarmTheme.Honey,2),LearningPlot<0?new Rect(19+plot*plotW,h-55,plotW-6,40):new Rect(20,h-57,w*.55,40),8,8);
        if(!Game.S.DesktopMotion||seconds>=1.5)return;
        double progress=Math.Min(1,seconds/1.5);
        foreach(int plot in harvest.BerryPlots.Where(p=>LearningPlot<0||p==LearningPlot)){
            double startX=LearningPlot<0?16+(plot+.5)*plotW:20+w*.275,startY=h-37;
            double px=startX+(x+19-startX)*progress,py=startY+(y-4-startY)*progress-Math.Sin(progress*Math.PI)*30;
            CareFeedbackArt.Berry(d,px,py,18);
        }
    }
}
