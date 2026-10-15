using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

public partial class FarmScene
{
    public bool Journey;
    public bool JourneyPreview;
    public int PreviewRegion;
    public bool HidePreviewParty;
    public DateTime PetResponseUntil;
    public Action FocusedPetTouch;
    internal Rect PetHitBounds;
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if(FocusedPetTouch!=null&&PetHitBounds.Contains(e.GetPosition(this))){e.Handled=true;FocusedPetTouch();}
    }
    private void DrawJourney(DrawingContext d,double w,double h)
    {
        bool cave=JourneyPreview?PreviewRegion==1:Game.R.Difficulty==1;
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),14,14));
        d.DrawRectangle(U.Color(cave?"#DCE8E5":"#E9F0DE"),null,new Rect(0,0,w,h));
        if(!cave)d.DrawEllipse(U.Color("#F0DCA1"),null,new Point(w*.85,h*.2),19,19);
        d.DrawEllipse(U.Color(cave?"#B2CCCA":"#C8DAB8"),null,new Point(w*.15,h*.44),w*.4,h*.26);
        d.DrawEllipse(U.Color(cave?"#A2BEBC":"#B6CCA6"),null,new Point(w*.9,h*.45),w*.4,h*.25);
        d.DrawRectangle(U.Color(cave?"#93B2AD":"#AFC78F"),null,new Rect(0,h*.51,w,h*.49));
        if(cave){
            var rocks=new StreamGeometry();using(var c=rocks.Open()){c.BeginFigure(new Point(w*.54,0),true,true);c.LineTo(new Point(w*.59,h*.27),true,false);c.LineTo(new Point(w*.66,h*.06),true,false);c.LineTo(new Point(w*.73,h*.3),true,false);c.LineTo(new Point(w*.8,h*.09),true,false);c.LineTo(new Point(w,h*.21),true,false);c.LineTo(new Point(w,0),true,false);}d.DrawGeometry(U.Color("#789B99"),null,rocks);
            DecorationView.Draw(d,new Rect(w*.82,h*.32,35,35),3,Tick);
        }
        var creek=new StreamGeometry();using(var c=creek.Open()){
            c.BeginFigure(new Point(w*.7,h*.44),true,true);
            c.BezierTo(new Point(w*.42,h*.66),new Point(w*1.08,h*.66),new Point(w*.6,h),true,false);
            c.LineTo(new Point(w*1.05,h),true,false);
            c.BezierTo(new Point(w*1.3,h*.62),new Point(w*.61,h*.65),new Point(w*.83,h*.44),true,false);
        }
        d.DrawGeometry(U.Color(cave?"#7EAFBD":"#87BFBC"),new Pen(U.Color(cave?"#B3CECD":"#DBD9AF"),7),creek);
        for(int i=0;i<6;i++){
            double x=w*(.73+(i%3)*.055),y=h*(.61+(i/3)*.24);
            d.DrawLine(new Pen(U.Color("#C6E2D8"),2),new Point(x,y),new Point(x+16,y));
        }
        var path=new StreamGeometry();using(var c=path.Open()){
            c.BeginFigure(new Point(-20,h*.88),false,false);
            c.BezierTo(new Point(w*.25,h*.48),new Point(w*.38,h*.92),new Point(w*.72,h*.55),true,false);
        }
        d.DrawGeometry(null,new Pen(U.Color(cave?"#B6C6B5":"#D8C59A"),h*.2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},path);
        for(int i=0;i<(cave?0:18);i++){
            double x=(i*79+19)%w,y=h*(.51+(i%4)*.13);
            if(x>w*.65)continue;
            d.DrawLine(new Pen(U.Color("#819F69"),1.4),new Point(x,y),new Point(x-3,y-6));
            d.DrawLine(new Pen(U.Color("#819F69"),1.4),new Point(x,y),new Point(x+3,y-5));
        }
        bool camp=!JourneyPreview&&Game.R.Node==2;
        if(!JourneyPreview)Label(d,SmallTravel?FarmGame.ExpeditionName(Game.R.Difficulty,Game.R.Depth):Game.R.JourneyPaused?cave?"在洞穴里等你":"在小溪边等你":camp?cave?"洞穴营地":"溪边营地":cave?"沿着洞穴，慢慢往前走":"沿着小溪，慢慢往前走",14,12,12,U.Green);
        if(SmallTravel&&Game.R.Phase=="battle"){DrawSmallEncounter(d,w,h);d.Pop();return;}
        if(JourneyPreview&&HidePreviewParty){d.Pop();return;}
        int[] party=Game.S.Team;double size=Math.Min(86,Math.Min(h*.46,w*.2));
        for(int i=0;i<party.Length;i++){
            int pet=party[i];double x=w*(.16+i*.19),foot=h*(.79-(i%2)*.035);
            double bounce=JourneyPreview||Game.R.JourneyPaused||camp||!Game.S.DesktopMotion?0:Math.Sin(Tick*.12+i*1.4)*3;
            d.DrawEllipse(U.Color("#9EAA7B"),null,new Point(x,foot-1),size*.28,4);
            d.PushOpacity(JourneyPreview||Game.R.Hp[pet]>0?1:.55);d.DrawImage(Art.Frame(pet,Tick,Game.S.Evolved,Game.S.SeedStage),new Rect(x-size/2,foot-size+bounce,size,size));d.Pop();
            if(!JourneyPreview&&!SmallTravel)CenterLabel(d,Game.Name(pet),x,foot+7,12,U.Ink,w*.18);
        }
        d.Pop();
    }
}
