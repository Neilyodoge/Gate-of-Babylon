using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

public static class FarmTheme
{
    public static readonly Brush Paper=U.Color("#FFFCF4"),Honey=U.Color("#EDC16E"),HoneyInk=U.Color("#593F22"),HoneyEdge=U.Color("#CDA252");
    public static readonly Brush[] ElementPaper={U.Color("#DFEDF0"),U.Color("#F6E1D4"),U.Color("#E7EECF"),U.Color("#EBDCC6"),U.Color("#EEE5BC")};
    public static string ResourceCount(int value){return value>=100000000?(value/100000000d).ToString("0.#")+"亿":value>=10000?(value/10000d).ToString("0.#")+"万":value.ToString();}
    public static void Accent(Button button)
    {
        Action paint=()=>{button.Background=button.IsEnabled?Honey:U.Pale;button.Foreground=button.IsEnabled?HoneyInk:U.Muted;button.BorderBrush=button.IsEnabled?HoneyEdge:U.Line;};paint();button.IsEnabledChanged+=(s,e)=>paint();
    }
    public static Border Badge(string label,Brush fill=null,Brush ink=null)
    {
        var text=U.Text(label,11,ink??U.Green);text.Margin=new Thickness(0);text.TextWrapping=TextWrapping.NoWrap;
        return new Border{Background=fill??U.Pale,CornerRadius=new CornerRadius(8),Padding=new Thickness(8,3,8,3),Child=text,HorizontalAlignment=HorizontalAlignment.Left};
    }
    public static void Apply(FrameworkElement element)
    {
        element.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
    }
    private const string Styles=@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='{x:Type ToolTip}'>
  <Setter Property='Background' Value='#FFFCF4'/><Setter Property='Foreground' Value='#4A4337'/><Setter Property='BorderBrush' Value='#D7C6A8'/><Setter Property='Padding' Value='10'/><Setter Property='FontSize' Value='12'/>
 </Style>
 <Style TargetType='{x:Type ComboBoxItem}'>
  <Setter Property='Padding' Value='8,6'/><Setter Property='Foreground' Value='#4A4337'/><Setter Property='Margin' Value='1'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'>
   <Border x:Name='Item' CornerRadius='7' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border>
   <ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Item' Property='Background' Value='#E4EDD9'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='Item' Property='Background' Value='#E4EDD9'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ComboBox}'>
  <Setter Property='Background' Value='#FFFCF4'/><Setter Property='Foreground' Value='#4A4337'/><Setter Property='BorderBrush' Value='#D7C6A8'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='9,6'/><Setter Property='MinHeight' Value='32'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'>
   <Grid>
    <ToggleButton Focusable='False' ClickMode='Press' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
     <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1,1,1,2'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='#82715B' StrokeThickness='1.7' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,10,0'/></Border></ControlTemplate></ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter Margin='9,5,27,5' VerticalAlignment='Center' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'/>
    <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='None'>
     <Border Background='#FFFCF4' BorderBrush='#D7C6A8' BorderThickness='1,1,1,2' CornerRadius='10' Padding='4' MinWidth='{TemplateBinding ActualWidth}' MaxHeight='230'>
      <ScrollViewer CanContentScroll='True'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
     </Border>
    </Popup>
   </Grid>
   <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.6'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter Property='BorderBrush' Value='#436C53'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ProgressBar}'>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ProgressBar}'>
   <Grid x:Name='PART_Track' ClipToBounds='True'><Border Background='{TemplateBinding Background}' CornerRadius='4'/><Border x:Name='PART_Indicator' Background='{TemplateBinding Foreground}' CornerRadius='4' HorizontalAlignment='Left'/></Grid>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type ScrollBar}'>
  <Setter Property='Width' Value='10'/><Setter Property='MinWidth' Value='10'/><Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
   <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'>
    <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border x:Name='Grip' Background='#CDBFA6' CornerRadius='4' Margin='2,0'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Grip' Property='Background' Value='#A59579'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
   </Track></Grid>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='{x:Type Expander}'>
  <Setter Property='Foreground' Value='#436C53'/><Setter Property='FontSize' Value='12'/><Setter Property='Margin' Value='0,5,0,5'/>
 </Style>
</ResourceDictionary>";
}

public class FarmGlyph:FrameworkElement
{
    public string Kind="farm";
    public Brush Ink;
    protected override void OnRender(DrawingContext d)
    {
        double size=Math.Min(ActualWidth,ActualHeight);if(size<=0)return;d.PushTransform(new ScaleTransform(size/32,size/32));
        var edge=new Pen(Ink??U.Color("#64523C"),1.8);edge.StartLineCap=edge.EndLineCap=PenLineCap.Round;var green=U.Color("#7C9E5E");var gold=U.Color("#E9BD66");
        if(Kind=="berry"){
            d.DrawEllipse(U.Color("#D8837E"),edge,new Point(11,19),7,8);d.DrawEllipse(U.Color("#C9676D"),edge,new Point(21,21),7,8);d.DrawLine(new Pen(green,3),new Point(17,6),new Point(17,15));d.DrawEllipse(green,edge,new Point(11,8),6,3);d.DrawEllipse(U.Color("#FBE8D5"),null,new Point(8,17),2,2);
        }else if(Kind=="grain"){
            d.DrawLine(edge,new Point(15,30),new Point(17,5));for(int i=0;i<3;i++){d.DrawEllipse(gold,edge,new Point(11,10+i*6),4,3);d.DrawEllipse(gold,edge,new Point(22,7+i*6),4,3);}
        }else if(Kind=="mint"||Kind=="evolve"){
            d.DrawLine(new Pen(green,2.5),new Point(16,29),new Point(16,12));d.DrawEllipse(U.Color("#9FB576"),edge,new Point(9,17),7,4);d.DrawEllipse(U.Color("#BDD29C"),edge,new Point(23,10),7,5);if(Kind=="evolve")d.DrawEllipse(gold,null,new Point(8,5),2,2);
        }else if(Kind=="fertilizer"){
            d.DrawRoundedRectangle(U.Color("#BCA17C"),edge,new Rect(6,10,20,20),6,6);d.DrawLine(edge,new Point(9,9),new Point(24,9));d.DrawRectangle(gold,edge,new Rect(10,3,13,7));d.DrawEllipse(green,null,new Point(16,21),5,5);
        }else if(Kind=="crystal"){
            var gem=Geometry.Parse("M16,2 L28,12 L23,29 L9,29 L4,12 Z M4,12 L28,12 M16,2 L12,12 L16,29 L22,12 Z");d.DrawGeometry(U.Color("#90C2CF"),edge,gem);
        }else if(Kind=="pets"){
            d.DrawEllipse(gold,edge,new Point(16,22),8,6);foreach(var p in new[]{new Point(6,12),new Point(13,7),new Point(22,8),new Point(28,15)})d.DrawEllipse(gold,edge,p,3,4);
        }else if(Kind=="adventure"){
            d.DrawEllipse(U.Color("#F7E5B2"),edge,new Point(16,17),12,12);d.DrawGeometry(U.Color("#BD8270"),edge,Geometry.Parse("M21,7 L19,19 L10,25 L13,13 Z"));d.DrawEllipse(FarmTheme.Paper,null,new Point(16,17),2,2);
        }else if(Kind=="book"||Kind=="help"){
            d.DrawRoundedRectangle(U.Color("#C2CDA0"),edge,new Rect(5,4,23,26),3,3);d.DrawLine(edge,new Point(10,5),new Point(10,29));d.DrawLine(edge,new Point(15,11),new Point(23,11));d.DrawLine(edge,new Point(15,17),new Point(23,17));if(Kind=="book")d.DrawRectangle(U.Color("#D28B76"),null,new Rect(21,3,4,7));else d.DrawEllipse(gold,null,new Point(20,23),2,2);
        }else{
            d.DrawGeometry(U.Color("#D39B78"),edge,Geometry.Parse("M3,14 L16,3 L29,14 Z"));d.DrawRoundedRectangle(U.Color("#F3DCAD"),edge,new Rect(7,14,19,16),2,2);d.DrawRectangle(green,edge,new Rect(14,19,6,11));
        }
        d.Pop();
    }
}
