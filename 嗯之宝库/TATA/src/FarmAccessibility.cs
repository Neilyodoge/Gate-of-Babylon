using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;

public class FarmButton:Button
{
    public static readonly DependencyProperty ShowKeyFocusProperty=DependencyProperty.Register("ShowKeyFocus",typeof(bool),typeof(FarmButton),new PropertyMetadata(false));
    public bool ShowKeyFocus{get{return (bool)GetValue(ShowKeyFocusProperty);}set{SetValue(ShowKeyFocusProperty,value);}}
    private static readonly DataTemplate WrappedLabel=MakeLabel();
    private static DataTemplate MakeLabel()
    {
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding());text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);text.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);
        return new DataTemplate{VisualTree=text};
    }
    protected override void OnContentChanged(object oldContent,object newContent)
    {
        base.OnContentChanged(oldContent,newContent);ContentTemplate=newContent is string?WrappedLabel:null;
    }
}

public class FarmCheckBox:CheckBox
{
    internal void Activate(){OnClick();}
}

public partial class FarmGame
{
    public bool SetReadingSize(int size)
    {
        if(size<0||size>2||size==S.ReadingSize)return false;S.ReadingSize=size;Save();return true;
    }
}

public partial class FarmPanel
{
    private static readonly DependencyProperty FocusGroupProperty=DependencyProperty.RegisterAttached("FocusGroup",typeof(int),typeof(FarmPanel),new PropertyMetadata(3));
    private bool readingSettingsOpen;
    private FrameworkElement readingSettingsCard;
    private static void FocusGroup(DependencyObject element,int group){element.SetValue(FocusGroupProperty,group);}
    private static IEnumerable<DependencyObject> LogicalNodes(DependencyObject root)
    {
        if(root==null)yield break;yield return root;
        foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)foreach(var nested in LogicalNodes(child))yield return nested;}
    }
    private Button ReadingButton()
    {
        var button=Btn("Aa",()=>{readingSettingsOpen=!readingSettingsOpen;keyboardFocusKey="";resetScroll=false;});button.Padding=new Thickness(8,5,8,5);button.Margin=new Thickness(0,3,5,3);button.FontSize=12;
        AutomationProperties.SetName(button,"阅读与操作");AutomationProperties.SetAutomationId(button,"reading-settings");button.ToolTip="文字大小与键盘操作 · Ctrl + 加号 / 减号";FocusGroup(button,2);return button;
    }
    private UIElement ReadingSettings()
    {
        var p=new StackPanel();p.Children.Add(U.Text("读得舒服一点",16,U.Green));var sizes=new WrapPanel();p.Children.Add(sizes);var state=G.S;
        for(int i=0;i<3;i++){int size=i;var b=Btn(new[]{"默认字号","大字","更大字"}[i],()=>{if(Object.ReferenceEquals(state,G.S))G.SetReadingSize(size);},true,G.S.ReadingSize==i);b.Margin=new Thickness(0,0,8,4);AutomationProperties.SetAutomationId(b,"reading-size-"+i);sizes.Children.Add(b);}
        p.Children.Add(U.Text("Tab / Shift+Tab 换按钮；Enter 或空格执行当前按钮。\nEsc 收起选项或返回；Ctrl + 加号 / 减号调字，Ctrl+0 恢复默认。",12,U.Muted));
        var close=Btn("回到游戏",()=>{readingSettingsOpen=false;keyboardFocusKey="id:reading-settings";});close.HorizontalAlignment=HorizontalAlignment.Left;p.Children.Add(close);
        var card=U.Card(p,10);card.Margin=new Thickness(0,8,0,4);readingSettingsCard=card;FocusGroup(card,-1);return card;
    }
    private void ApplyReadingSize()
    {
        double scale=new[]{1d,1.15,1.3}[Math.Max(0,Math.Min(2,G.S.ReadingSize))];
        if(scale==1)return;
        var nodes=LogicalNodes((DependencyObject)Content).ToList();
        var controlSizes=nodes.OfType<Control>().ToDictionary(c=>c,c=>c.FontSize);
        foreach(var item in nodes){
            var text=item as TextBlock;if(text!=null){if(text.ReadLocalValue(TextBlock.FontSizeProperty)!=DependencyProperty.UnsetValue)text.FontSize*=scale;if(!Double.IsNaN(text.LineHeight))text.LineHeight*=scale;continue;}
            var control=item as Control;if(control!=null)control.FontSize=controlSizes[control]*scale;
            var scene=item as FarmScene;if(scene!=null)scene.ReadingScale=scale;
            var box=item as Viewbox;if(box!=null&&!Double.IsNaN(box.Height))box.Height*=scale;
        }
    }
    private FrameworkElement PanelFooter(bool gentle)
    {
        var wrap=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};FocusGroup(wrap,3);
        var mode=U.Text("v"+FarmProgram.Version+(G.GuideTest?" · 测试不保存":" · 自动保存"),10,U.Muted);mode.Margin=new Thickness(0,5,12,5);mode.VerticalAlignment=VerticalAlignment.Center;wrap.Children.Add(mode);
        Action<Button> add=b=>{b.FontSize=10;b.Padding=new Thickness(6,4,6,4);b.Margin=new Thickness(4,2,0,2);wrap.Children.Add(b);};
        add(Btn("卡牌冒险",()=>Go("卡牌冒险")));
        if(G.GuideTest){var fast=Btn("快进 30 秒",()=>G.AdvanceGuideTest());AutomationProperties.SetName(fast,"测试：快进 30 秒");add(fast);add(Btn("从头测试",owner.RestartGuideTest));}
        if(gentle){var all=Btn(QuietShell?"展开全部玩法":"自由玩",()=>ExpandAllPlay("农场"));all.ToolTip="打开完整功能，本轮进度保留。";add(all);}
        else{
            if(G.GuideTest)add(Btn("返回引导",()=>OpenGuide()));else add(Btn(G.GuideActive?"继续引导":"新手引导",()=>OpenGuide()));
            if(WorldGuide)add(Btn("结束引导",()=>ExpandAllPlay("农场")));
            if(!G.GuideActive)add(Btn("回简洁小院",()=>{G.S.SimpleCourtyard=true;G.Save();Go("农场");}));
            add(Btn(!G.AtHome&&Tab!="冒险"?"返回当前冒险":"玩法说明",()=>Go(!G.AtHome&&Tab!="冒险"?"冒险":"怎么玩")));
        }
        return new Border{Child=wrap,Padding=new Thickness(14,5,14,7),Background=gentle?U.Cream:FarmTheme.Paper};
    }
}

