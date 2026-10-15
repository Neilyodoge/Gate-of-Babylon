using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed class CompanionMoment
{
    public FarmSave State;
    public int Pet;
    public string Kind,Words;
    public DateTime Started;
    public double Duration=4;
    public bool Active(FarmGame game){return Object.ReferenceEquals(State,game.S)&&game.AtHome&&(DateTime.UtcNow-Started).TotalSeconds<Duration;}
}

public static class CareFeedbackArt
{
    public static void Berry(DrawingContext d,double x,double y,double size)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(size/24,size/24));
        var edge=new Pen(U.Color("#86505B"),1.3);
        d.DrawEllipse(U.Color("#D8837E"),edge,new Point(-4,2),6,7);d.DrawEllipse(U.Color("#C9676D"),edge,new Point(4,3),6,7);
        d.DrawLine(new Pen(U.Color("#618449"),2),new Point(0,-8),new Point(0,-2));d.DrawEllipse(U.Color("#8DA65E"),null,new Point(-4,-7),5,2.5);
        d.DrawEllipse(U.Color("#FBE8D5"),null,new Point(-6,0),1.6,1.6);d.Pop();d.Pop();
    }
    public static void Heart(DrawingContext d,double x,double y,double size)
    {
        d.PushTransform(new TranslateTransform(x,y));d.PushTransform(new ScaleTransform(size/20,size/20));
        d.DrawGeometry(U.Color("#BC6C7B"),null,Geometry.Parse("M0,6 C-13,-2 -10,-11 -4,-10 C-1,-10 0,-8 0,-6 C2,-12 10,-12 11,-6 C13,0 5,4 0,9 Z"));d.Pop();d.Pop();
    }
}

public sealed class CompanionMomentView:FrameworkElement
{
    public FarmGame Game;
    public int Pet,Tick;
    public bool FoodThought;
    public Func<CompanionMoment> Moment;
    internal double PreviewSeconds=-1;
    protected override void OnRender(DrawingContext d)
    {
        double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        var cue=Moment();bool active=cue!=null&&cue.Active(Game);double seconds=active?(PreviewSeconds>=0?PreviewSeconds:(DateTime.UtcNow-cue.Started).TotalSeconds):0;
        bool motion=Game.S.DesktopMotion;double bounce=active&&motion?Math.Sin(Math.Min(1,seconds/1.1)*Math.PI)*5:0;
        double size=h*.91,cx=w*.5,cy=h*.5;
        d.PushTransform(new TranslateTransform(0,-bounce));d.DrawImage(Art.Frame(Pet,Tick,Game.S.Evolved,Game.S.SeedStage),new Rect(cx-size/2,cy-size/2,size,size));d.Pop();
        if(!active){
            if(FoodThought&&Game.CanFeed(Pet)){
                double x=w*.15,y=h*.25;var edge=new Pen(U.Line,1.5);
                d.DrawEllipse(FarmTheme.Paper,edge,new Point(x,y),24,21);CareFeedbackArt.Berry(d,x,y,30);
                d.DrawEllipse(FarmTheme.Paper,edge,new Point(x+24,y+18),4,4);d.DrawEllipse(FarmTheme.Paper,null,new Point(x+31,y+26),2,2);
            }
            return;
        }
        if(cue.Kind=="feed"&&motion&&seconds<1.25){
            double t=Math.Min(1,seconds/1.25);double x=w*.13+(cx-w*.13)*t,y=h*.68-Math.Sin(t*Math.PI)*h*.19;
            d.PushOpacity(t>.85?(1-t)/.15:1);CareFeedbackArt.Berry(d,x,y,26);d.Pop();
        }
        if(cue.Kind!="feed"||!motion||seconds>=1.1){
            double lift=motion?Math.Min(1,seconds/2)*8:0;CareFeedbackArt.Heart(d,cx+size*.4,cy-size*.2-lift,19);
            if(cue.Kind=="feed")CareFeedbackArt.Heart(d,cx-size*.4,cy-size*.12-lift,12);
        }
    }
}

public partial class FarmPanel
{
    private CompanionMoment companionMoment;
    private readonly List<CompanionMomentView> companionViews=new List<CompanionMomentView>();
    private void ShowCompanionMoment(int pet,string kind,string words)
    {
        companionMoment=new CompanionMoment{State=G.S,Pet=pet,Kind=kind,Words=words,Started=DateTime.UtcNow,Duration=kind=="touch"?5:4};
    }
    private CompanionMoment CurrentCompanionMoment(int pet){return companionMoment!=null&&companionMoment.Pet==pet&&companionMoment.Active(G)?companionMoment:null;}
    private string CompanionWords(int pet,Func<string> fallback){var cue=CurrentCompanionMoment(pet);return cue==null?fallback():cue.Words;}
    private TextBlock CompanionLine(int pet,Func<string> fallback,double size=15)
    {
        var text=Live(()=>CompanionWords(pet,fallback),size,U.Green);text.TextAlignment=TextAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetAutomationId(text,"companion-response");return text;
    }
}
