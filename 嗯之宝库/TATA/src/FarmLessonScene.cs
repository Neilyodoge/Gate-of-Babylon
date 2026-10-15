using System;
using System.Windows;
using System.Windows.Media;

public partial class FarmScene
{
    public int LearningPlot=-1;
    public bool SoilLessonApplied;
    private void DrawLearningPlot(DrawingContext d,double w,double h)
    {
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),16,16));
        d.DrawRectangle(U.Color("#E6F2E5"),null,new Rect(0,0,w,h));
        d.DrawEllipse(U.Color("#F3D899"),null,new Point(w*.87,29),19,19);
        d.DrawEllipse(U.Color("#CFE4CE"),null,new Point(w*.2,h*.57),w*.66,h*.37);
        d.DrawEllipse(U.Color("#BDD8B9"),null,new Point(w*.86,h*.63),w*.45,h*.35);
        d.DrawRectangle(U.Color("#A4C68E"),null,new Rect(0,h*.7,w,h*.3));
        double x=20,y=h-57,plotWidth=w*.55;
        d.DrawRoundedRectangle(U.Color("#B09269"),new Pen(U.Color("#6F604D"),2),new Rect(x,y,plotWidth,40),11,11);
        if(SoilLessonApplied)d.DrawRoundedRectangle(null,new Pen(FarmTheme.Honey,3),new Rect(x-2,y-2,plotWidth+4,44),12,12);
        int kind=Game.S.Crops[LearningPlot];double growth=Math.Min(1,Game.S.Growth[LearningPlot]/Game.CropSeconds(kind));
        for(int i=0;i<4;i++){
            double px=x+plotWidth*(.16+i*.23),py=y+29,stem=8+growth*17;
            d.DrawLine(new Pen(U.Color("#4D7650"),3),new Point(px,py),new Point(px,py-stem));
            d.DrawEllipse(U.Color("#81A866"),null,new Point(px-5,py-stem*.55),6,3);
            if(kind==0)d.DrawEllipse(U.Color("#BB5275"),new Pen(U.Color("#843F59"),1),new Point(px,py-stem),5+growth*2,4+growth*2);
            else if(kind==1){for(int n=0;n<3;n++){d.DrawEllipse(U.Color("#E9BD66"),new Pen(U.Color("#8E7243"),.8),new Point(px-3,py-stem+n*4),3+growth,2);d.DrawEllipse(U.Color("#E9BD66"),null,new Point(px+3,py-stem+n*4-2),3+growth,2);}}
            else{d.DrawEllipse(U.Color("#BDD29C"),new Pen(U.Color("#4D7650"),1),new Point(px+5,py-stem),6+growth*2,4);d.DrawEllipse(U.Color("#9FB576"),null,new Point(px-4,py-stem+5),6+growth,3);}
        }
        int pet=FocusedFarmPet>=0?FocusedFarmPet:Game.GuideCompanion;double size=Math.Min(70,h*.4),cx=w*.33,foot=Math.Max(size+4,h-66);
        double bounce=DateTime.UtcNow<PetResponseUntil?6:0;d.DrawEllipse(U.Color("#8DAC7B"),null,new Point(cx,foot),22,4);
        d.DrawImage(Art.Frame(pet,Tick,Game.S.Evolved,Game.S.SeedStage),new Rect(cx-size/2,foot-size-bounce,size,size));PetHitBounds=new Rect(cx-size/2-10,foot-size-10,size+20,size+20);
        if(DateTime.UtcNow<PetResponseUntil)CareFeedbackArt.Heart(d,cx+31,foot-58,14);
        if(ShowBerryBasket)DrawBerryBasket(d,w,h);
        d.Pop();
    }
}
