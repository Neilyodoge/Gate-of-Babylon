using System;
using System.Windows;
using System.Windows.Media;

public class DecorationView:FrameworkElement
{
    public int Decoration=-1,Tick;
    protected override void OnRender(DrawingContext d){Draw(d,new Rect(0,0,ActualWidth,ActualHeight),Decoration,Tick);}
    public static void Draw(DrawingContext d,Rect box,int decoration,int tick)
    {
        if(decoration<0||decoration>5||box.Width<1||box.Height<1)return;
        d.PushTransform(new TranslateTransform(box.Left,box.Top));d.PushTransform(new ScaleTransform(box.Width/64,box.Height/64));
        var edge=new Pen(U.Color("#496254"),2);var wood=U.Color("#BD9265");
        d.DrawEllipse(U.Color("#65897555"),null,new Point(33,58),23,4);
        if(decoration==0){
            d.DrawRoundedRectangle(wood,edge,new Rect(27,25,12,32),3,3);d.PushTransform(new RotateTransform(tick*2,33,24));
            for(int i=0;i<4;i++){d.PushTransform(new RotateTransform(i*90,33,24));d.DrawRoundedRectangle(U.Color(i%2==0?"#F2D377":"#9BC9CF"),edge,new Rect(33,5,10,21),3,3);d.Pop();}d.Pop();d.DrawEllipse(U.Cream,edge,new Point(33,24),5,5);
        }else if(decoration==1){
            d.DrawRoundedRectangle(U.Color("#F9E7BD"),edge,new Rect(25,30,17,27),6,6);d.DrawEllipse(U.Color("#CB7B7F"),edge,new Point(33,28),25,15);d.DrawEllipse(U.Cream,null,new Point(22,24),4,4);d.DrawEllipse(U.Cream,null,new Point(40,20),5,4);d.DrawEllipse(U.Color("#FFE195"),null,new Point(33,44),5,8);
        }else if(decoration==2){
            d.DrawRoundedRectangle(U.Color("#C28D71"),edge,new Rect(17,38,31,20),5,5);d.DrawLine(new Pen(U.Green,3),new Point(32,40),new Point(32,20));
            d.DrawEllipse(U.Color("#86AD74"),null,new Point(24,31),10,5);for(int i=0;i<5;i++){double a=i*1.256;d.DrawEllipse(U.Color("#E9B5C8"),edge,new Point(32+Math.Cos(a)*10,19+Math.Sin(a)*10),7,7);}d.DrawEllipse(U.Gold,null,new Point(32,19),6,6);
        }else if(decoration==3){
            var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(32,3),true,true);c.PolyLineTo(new[]{new Point(51,29),new Point(44,56),new Point(20,56),new Point(13,29)},true,true);}d.DrawGeometry(U.Color("#94C9D8"),edge,g);d.DrawLine(new Pen(U.Cream,3),new Point(32,8),new Point(25,37));d.DrawLine(edge,new Point(13,29),new Point(51,29));
        }else if(decoration==4){
            d.DrawLine(new Pen(wood,5),new Point(12,12),new Point(52,12));for(int i=0;i<3;i++){double x=17+i*15,y=35+(i%2)*12;d.DrawLine(edge,new Point(x,12),new Point(x,y));d.DrawEllipse(U.Color(i%2==0?"#D9ABD1":"#9FCAC1"),edge,new Point(x,y+5),8,9);d.DrawLine(new Pen(U.Cream,2),new Point(x,y),new Point(x,y+10));}
        }else{
            d.DrawLine(new Pen(wood,5),new Point(16,56),new Point(16,12));d.DrawLine(new Pen(wood,5),new Point(16,12),new Point(42,12));d.DrawLine(edge,new Point(42,12),new Point(42,21));d.DrawRoundedRectangle(U.Color("#F5DEA0"),edge,new Rect(29,21,26,29),7,7);d.DrawEllipse(U.Cream,null,new Point(42,35),7,10);
        }
        d.Pop();d.Pop();
    }
}
