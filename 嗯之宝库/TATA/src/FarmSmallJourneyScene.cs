using System;
using System.Windows;
using System.Windows.Media;

public partial class FarmScene
{
    public bool SmallTravel;
    private void DrawSmallEncounter(DrawingContext d,double w,double h)
    {
        var run=Game.R;double size=Math.Min(52,w*.115),foot=h*.75;
        for(int i=0;i<Game.S.Team.Length;i++){
            int pet=Game.S.Team[i];double x=w*(.11+i*.145),bounce=Game.S.DesktopMotion&&!run.JourneyPaused&&run.Hp[pet]>0?Math.Sin(Tick*.12+i)*2:0;
            d.DrawEllipse(U.Color("#789389"),null,new Point(x,foot),size*.25,3);
            d.PushOpacity(run.Hp[pet]>0?1:.4);d.DrawImage(Art.Frame(pet,Tick,Game.S.Evolved,Game.S.SeedStage),new Rect(x-size/2,foot-size+bounce,size,size));d.Pop();
            DrawSmallHealth(d,x-size*.38,foot+7,size*.76,Math.Max(0,(double)run.Hp[pet]/Game.MaxHp(pet)),U.Green);
        }
        CenterLabel(d,"VS",w*.515,h*.48,12,U.Ink,30);
        for(int i=0;i<run.EnemyCount;i++){
            int kind=run.EnemyKind[i];double x=w*(.66+i*.14),bounce=Game.S.DesktopMotion&&!run.JourneyPaused&&run.EnemyHp[i]>0?Math.Sin(Tick*.1+i+2)*2:0;
            d.DrawEllipse(U.Color("#789389"),null,new Point(x,foot),size*.25,3);
            d.PushOpacity(run.EnemyHp[i]>0?1:.4);d.DrawImage(Art.Frame(kind==3?3:kind+1,Tick,false),new Rect(x-size/2,foot-size+bounce,size,size));d.Pop();
            DrawSmallHealth(d,x-size*.38,foot+7,size*.76,run.EnemyMax[i]>0?Math.Max(0,(double)run.EnemyHp[i]/run.EnemyMax[i]):0,U.Color("#B87560"));
        }
    }
    private void DrawSmallHealth(DrawingContext d,double x,double y,double width,double ratio,Brush fill)
    {
        double height=8*ReadingScale;
        d.DrawRoundedRectangle(U.Cream,new Pen(U.Ink,.7),new Rect(x,y,width,height),2,2);
        d.DrawRoundedRectangle(fill,null,new Rect(x,y,width*Math.Min(1,ratio),height),2,2);
    }
}
