using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private readonly Dictionary<string,double> cardBrowseOffsets=new Dictionary<string,double>();
    private MossRun cardBrowseRun;
    private int cardBrowseRevision;
    private ScrollViewer cardBrowseScroll;
    private string cardBrowseKey;
    private double cardBrowseScale=1;
    private void CaptureCardBrowse()
    {
        if(cardBrowseScroll!=null&&cardBrowseScroll.ViewportHeight>0&&Object.ReferenceEquals(cardBrowseRun,G.C)&&cardBrowseRevision==G.C.Revision)
            cardBrowseOffsets[cardBrowseKey]=cardBrowseScroll.VerticalOffset/cardBrowseScale;
        cardBrowseScroll=null;
    }
    private void TrackCardBrowse(ScrollViewer scroll,string key)
    {
        if(!Object.ReferenceEquals(cardBrowseRun,G.C)||cardBrowseRevision!=G.C.Revision){cardBrowseOffsets.Clear();cardBrowseRun=G.C;cardBrowseRevision=G.C.Revision;}
        cardBrowseScroll=scroll;cardBrowseKey=key;cardBrowseScale=CardScale;
        double offset;if(cardBrowseOffsets.TryGetValue(key,out offset))scroll.ScrollToVerticalOffset(offset*CardScale);
    }
    private void CardUpgradePage(Grid host)
    {
        host.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});host.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        var top=new StackPanel();host.Children.Add(top);var heading=new DockPanel();
        var back=Cb("返回营地",()=>cardUpgradeOpen=false,true,false,"cards-overlay-back");DockPanel.SetDock(back,Dock.Right);heading.Children.Add(back);heading.Children.Add(Ct("选一张强化，再继续远征",22));top.Children.Add(heading);
        top.Children.Add(Ct("只强化选中的这一张。返回仍可选择休息。",12,U.Muted));
        top.Children.Add(Ct("PgUp / PgDn 翻页 · Home / End 到首尾",10,U.Muted));
        var rows=new StackPanel();var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        AutomationProperties.SetAutomationId(scroll,"cards-upgrade-scroll");TrackCardBrowse(scroll,"upgrade");Grid.SetRow(scroll,1);host.Children.Add(scroll);
        int rev=G.C.Revision;
        for(int i=0;i<G.C.Deck.Count;i++){
            int index=i,card=G.C.Deck[i];var row=new StackPanel();row.Children.Add(Ct(G.CurrentCardName(card)+" · "+MossCards.All[MossCards.Id(card)].Cost+" 能量",16));row.Children.Add(Ct(G.CurrentCardText(card),12,U.Muted));
            if(!MossCards.Up(card)){row.Children.Add(Ct("强化后："+G.CurrentCardText(card+100),12,U.Green));row.Children.Add(Cb("强化这张并继续",()=>{if(G.CardCamp(1,index,rev))cardUpgradeOpen=false;},true,false,"cards-upgrade-"+i));}
            else row.Children.Add(Ct("已经强化过了",12,U.Muted));rows.Children.Add(U.Card(row,10));
        }
    }
}
