using System;
using System.Windows;

// Work areas and window bounds use WPF device-independent coordinates throughout.
public static class DesktopLayout
{
    public static Rect ChooseArea(Rect bounds,Rect[] areas)
    {
        if(areas==null||areas.Length==0)throw new ArgumentException("At least one work area is required");
        Rect chosen=areas[0];double overlap=-1,distance=Double.MaxValue;
        double cx=bounds.Left+bounds.Width/2,cy=bounds.Top+bounds.Height/2;
        foreach(var area in areas){
            Rect intersection=Rect.Intersect(bounds,area);double amount=intersection.IsEmpty?0:intersection.Width*intersection.Height;
            double dx=Math.Max(area.Left-cx,Math.Max(0,cx-area.Right)),dy=Math.Max(area.Top-cy,Math.Max(0,cy-area.Bottom));double d=dx*dx+dy*dy;
            if(amount>overlap||(amount==overlap&&d<distance)){chosen=area;overlap=amount;distance=d;}
        }
        return chosen;
    }
    public static Rect Clamp(Rect bounds,Rect area)
    {
        double width=Math.Min(bounds.Width,area.Width),height=Math.Min(bounds.Height,area.Height);
        return new Rect(Math.Max(area.Left,Math.Min(area.Right-width,bounds.Left)),Math.Max(area.Top,Math.Min(area.Bottom-height,bounds.Top)),width,height);
    }
    public static bool ShouldAnimate(bool desktopVisible,bool desktopMotion,bool panelVisible)
    {
        return (desktopVisible&&desktopMotion)||panelVisible;
    }
}
