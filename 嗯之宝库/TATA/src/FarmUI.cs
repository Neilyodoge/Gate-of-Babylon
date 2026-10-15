using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private TextBlock[] resourceNumbers=new TextBlock[5];
    private void BuildPanelShell()
    {
        resourceNumbers=new TextBlock[5];
        if(GentleGuide||QuietShell){BuildGentleShell();return;}
        var root=new DockPanel{Background=U.Cream};FarmTheme.Apply(root);System.Windows.Documents.TextElement.SetFontFamily(root,FontFamily);TextOptions.SetTextFormattingMode(root,TextFormattingMode.Display);Content=root;
        var header=new StackPanel{Margin=new Thickness(18,10,18,0)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var top=new DockPanel{Margin=new Thickness(0,0,0,8)};header.Children.Add(top);
        var stock=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(stock,Dock.Right);top.Children.Add(stock);
        string[] names={"莓果","麦穗","薄荷","肥料","晶石"},glyphs={"berry","grain","mint","fertilizer","crystal"},uses={"喂养伙伴，增加成长与亲密","训练伙伴，提升等级","制作点心，获得成长","升级土壤，让农田长得更快","帮助伙伴进化"};
        for(int i=0;i<5;i++){
            var chip=new Border{Width=92,Background=FarmTheme.Paper,CornerRadius=new CornerRadius(10),BorderBrush=U.Line,BorderThickness=new Thickness(1,1,1,2),Padding=new Thickness(6,4,5,4),Margin=new Thickness(5,0,0,0),ToolTip=names[i]+" · "+uses[i]};
            var row=new DockPanel();chip.Child=row;var icon=new FarmGlyph{Kind=glyphs[i],Width=24,Height=28,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var text=new StackPanel();var name=U.Text(names[i],10,U.Muted);name.Margin=new Thickness(0);text.Children.Add(name);
            var count=U.Text("0",13);count.FontWeight=FontWeights.SemiBold;count.Margin=new Thickness(0);count.TextWrapping=TextWrapping.NoWrap;text.Children.Add(new Viewbox{Child=count,Height=19,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left});resourceNumbers[i]=count;row.Children.Add(text);stock.Children.Add(chip);
        }
        var brand=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};brand.Children.Add(new FarmGlyph{Kind="farm",Width=34,Height=36,Margin=new Thickness(0,0,8,0)});
        var words=new StackPanel();var title=U.Text("苔间",24);title.Margin=new Thickness(0);words.Children.Add(title);var sub=U.Text("桌上的小小冒险",10,U.Muted);sub.Margin=new Thickness(0);words.Children.Add(sub);brand.Children.Add(words);top.Children.Add(brand);
        var nav=new DockPanel();FocusGroup(nav,1);header.Children.Add(nav);var reading=ReadingButton();DockPanel.SetDock(reading,Dock.Right);nav.Children.Add(reading);
        var hide=U.Button("隐藏",owner.HideToTray);hide.Padding=new Thickness(10,6,10,6);hide.Margin=new Thickness(6,0,0,0);hide.ToolTip="隐藏到托盘，农田与当前旅程继续。";DockPanel.SetDock(hide,Dock.Right);nav.Children.Add(hide);
        var compact=U.Button("缩成宠物",owner.ShowCompact);compact.Padding=new Thickness(10,6,10,6);compact.Margin=new Thickness(0);compact.ToolTip="只留下陪伴你的宠物，双击宠物可再打开。";DockPanel.SetDock(compact,Dock.Right);nav.Children.Add(compact);
        if(WorldGuide&&Tab=="冒险"&&G.S.GuideStep==4&&!G.R.JourneyPaused)HighlightGuideAction(compact);
        var tabs=new StackPanel{Orientation=Orientation.Horizontal};nav.Children.Add(tabs);if(Tab!="农场"&&Tab!="伙伴"&&Tab!="冒险")tabs.Children.Add(PanelBackButton());
        string[] targets={"农场","伙伴","冒险"},icons={"farm","pets","adventure"};
        for(int i=0;i<targets.Length;i++){
            string target=targets[i];bool active=MainCategory()==target;var button=Btn(target,()=>Go(target),true,active);button.Padding=new Thickness(15,6,15,6);button.Margin=new Thickness(0,0,7,0);
            var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new FarmGlyph{Kind=icons[i],Width=17,Height=17,Margin=new Thickness(0,0,4,0)});
            var label=U.Text(target,12,active?FarmTheme.Paper:U.Ink);label.Margin=new Thickness(0);label.VerticalAlignment=VerticalAlignment.Center;label.TextWrapping=TextWrapping.NoWrap;row.Children.Add(label);button.Content=row;
            System.Windows.Automation.AutomationProperties.SetName(button,target);tabs.Children.Add(button);if(target=="手册")updates.Add(()=>label.Text=G.ClaimableChapters>0?"手册·"+G.ClaimableChapters:"手册");
        }
        var more=Btn(moreNavigationOpen?"收起更多 ▴":"更多 ▾",()=>{moreNavigationOpen=!moreNavigationOpen;resetScroll=true;});more.Padding=new Thickness(10,6,10,6);more.Margin=new Thickness(0);System.Windows.Automation.AutomationProperties.SetName(more,"更多");tabs.Children.Add(more);
        if(moreNavigationOpen)header.Children.Add(MoreNavigation());
        feedback=U.Text("",11,U.Muted);feedback.Margin=new Thickness(2,6,0,7);feedback.TextWrapping=TextWrapping.NoWrap;feedback.TextTrimming=TextTrimming.CharacterEllipsis;header.Children.Add(feedback);if(readingSettingsOpen)header.Children.Add(ReadingSettings());
        var footer=PanelFooter(false);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        body=new Grid{Margin=new Thickness(18,3,18,8)};scroller=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroller);
    }
    private UIElement SectionTitle(string title,string detail=null)
    {
        var row=new DockPanel{Margin=new Thickness(0,3,0,7)};var t=U.Text(title,17);t.Margin=new Thickness(0);row.Children.Add(t);
        if(detail!=null){var caption=U.Text(detail,11,U.Muted);caption.Margin=new Thickness(10,4,0,0);row.Children.Add(caption);}return row;
    }
    private void FarmDetails(){FarmManagementPage();}
}
