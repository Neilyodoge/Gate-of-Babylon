using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Automation;

public sealed class FeedingBerryIcon:FrameworkElement
{
    protected override void OnRender(DrawingContext d)
    {
        CareFeedbackArt.Berry(d,ActualWidth/2,ActualHeight/2,Math.Min(ActualWidth,ActualHeight));
    }
}

public partial class FarmPanel
{
    private static readonly DataTemplate FeedingLabel=MakeFeedingLabel();
    private static DataTemplate MakeFeedingLabel()
    {
        var row=new FrameworkElementFactory(typeof(DockPanel));row.SetValue(UIElement.IsHitTestVisibleProperty,false);
        var berry=new FrameworkElementFactory(typeof(FeedingBerryIcon));berry.SetValue(FrameworkElement.WidthProperty,36d);berry.SetValue(FrameworkElement.HeightProperty,36d);berry.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,12,0));berry.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);berry.SetValue(DockPanel.DockProperty,Dock.Left);row.AppendChild(berry);
        var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding());label.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);label.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(label);
        return new DataTemplate{VisualTree=row};
    }
    private Button GentleFeedAction(string label,int pet,Action action)
    {
        var state=G.S;string page=Tab;int xp=state.Xp[pet],bond=G.PetBond(pet);
        Func<bool> ready=()=>Object.ReferenceEquals(state,G.S)&&Tab==page&&state.Xp[pet]==xp&&G.PetBond(pet)==bond&&G.CanFeed(pet);
        var button=GentleAction(label,()=>{if(ready())action();},ready());
        // Keep the string content and normal button semantics; the icon is part of the same target.
        button.ContentTemplate=FeedingLabel;button.MinWidth=270;button.MaxWidth=360;button.MinHeight=56;button.FontSize=15;button.Padding=new Thickness(16,6,16,6);button.Margin=new Thickness(0,6,0,5);
        AutomationProperties.SetName(button,label);AutomationProperties.SetHelpText(button,"点击一次喂一口，消耗两颗莓果。点伙伴只会摸摸，不消耗莓果。");
        updates.Add(()=>button.IsEnabled=ready());return button;
    }
    private void FirstGuideFeed(FarmSave state,int pet)
    {
        if(!Object.ReferenceEquals(state,G.S)||G.S.GuideStep!=0||Tab!="新手引导"||!G.CanFeed(pet))return;
        SelectFocusedPet(pet);bool fed=false;CareForFocusedPet(pet,()=>{fed=G.Feed(pet);return fed;});
        // A failed transaction must leave the first lesson available.
        if(!fed)return;
        state.GuideInWorld=true;state.GuideStep=3;G.SyncGuide();G.Save();Go("伙伴");resetScroll=true;
    }
}
