using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;

public partial class FarmGame
{
    internal string CardRelicText{get{return C.Relic==1?"树叶帽 · 每场开局 +4 护盾":C.Relic==2?"电花加成 · 小小电花和双生电花每段 +2 伤害":"还没有随身小物。";}}
}
public partial class MossRun
{
    // A fresh, fixed-order copy: looking at the draw pile never exposes draw order.
    internal int[] BrowsePile(int pile)
    {
        var source=pile==1?Hand:pile==2?Draw:pile==3?Discard:pile==4?Exhaust:Deck;
        return source.OrderBy(c=>MossCards.Id(c)).ThenBy(c=>MossCards.Up(c)).ToArray();
    }
}
public partial class FarmPanel
{
    private int cardPile;
    private static readonly string[] CardPileNames={"整套牌","手牌","抽牌堆","弃牌","本场用尽"};
    private static readonly string[] CardPileHelp={
        "相同的牌合并计数，强化牌单独列出。这里只查看，不能出牌。",
        "这些牌现在就在手里。返回战斗，点牌底部的「打出」才会使用。",
        "还没抽到的牌，按种类排列，不代表抽到的先后。抽完后会洗入弃牌。",
        "已打出或回合结束时放下的牌。抽牌堆用完后，会洗回来。",
        "本场不能再抽到的牌。下一场战斗会回到牌组。"};
    private bool ScrollCardPile(Key key)
    {
        if(Tab!="卡牌冒险"||(!cardDeckOpen&&!cardUpgradeOpen))return false;
        if(key!=Key.PageDown&&key!=Key.PageUp&&key!=Key.Home&&key!=Key.End)return false;
        var scroll=LogicalNodes((DependencyObject)Content).OfType<ScrollViewer>().FirstOrDefault(s=>AutomationProperties.GetAutomationId(s)=="cards-pile-scroll"||AutomationProperties.GetAutomationId(s)=="cards-upgrade-scroll");
        if(scroll==null)return false;
        if(key==Key.PageDown)scroll.PageDown();else if(key==Key.PageUp)scroll.PageUp();else if(key==Key.Home)scroll.ScrollToTop();else scroll.ScrollToEnd();
        return true;
    }
    private void CardPilePage(Grid host)
    {
        bool battle=G.C.Phase=="battle";if(!battle)cardPile=0;
        host.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        host.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        var top=new StackPanel();host.Children.Add(top);
        var heading=new DockPanel();var back=Cb("返回",()=>cardDeckOpen=false,true,false,"cards-overlay-back");
        DockPanel.SetDock(back,Dock.Right);heading.Children.Add(back);heading.Children.Add(Ct("这一趟的牌组 · "+G.C.Deck.Count+" 张",22));top.Children.Add(heading);
        if(battle){
            var filters=new UniformGrid{Columns=5,Margin=new Thickness(0,4,0,4)};
            for(int i=0;i<5;i++){int pick=i;int count=G.C.BrowsePile(i).Length;
                var button=Cb((cardPile==i?"✓ ":"")+CardPileNames[i]+" "+count,()=>cardPile=pick,true,cardPile==i,"cards-pile-"+i);
                button.Padding=new Thickness(4,5,4,5);button.Margin=new Thickness(0,0,i==4?0:4,0);
                AutomationProperties.SetName(button,CardPileNames[i]+"，"+count+" 张"+(cardPile==i?"，已选":""));filters.Children.Add(button);
            }top.Children.Add(filters);
        }
        top.Children.Add(Ct(CardPileHelp[cardPile],12,U.Muted));
        if(G.C.BrowsePile(cardPile).Distinct().Count()>4)top.Children.Add(Ct("PgUp / PgDn 翻页 · Home / End 到首尾",10,U.Muted));
        var rows=new StackPanel();var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        TrackCardBrowse(scroll,"pile-"+cardPile);AutomationProperties.SetAutomationId(scroll,"cards-pile-scroll");Grid.SetRow(scroll,1);host.Children.Add(scroll);
        var cards=G.C.BrowsePile(cardPile);
        if(cards.Length==0)rows.Children.Add(Ct(cardPile==2?"抽牌堆空了。下次抽牌会先洗入弃牌；两边都空时抽不到新牌。":cardPile==3?"还没有弃牌。打出普通牌后，会放到这里。":cardPile==4?"还没有本场用尽的牌。":cardPile==1?"手牌打完了。返回战斗，可以结束回合。":"这一趟还没有牌。",15));
        foreach(var group in cards.GroupBy(c=>c)){
            int card=group.Key;var row=new StackPanel();
            row.Children.Add(Ct(G.CurrentCardName(card)+" × "+group.Count()+"  ·  "+MossCards.All[MossCards.Id(card)].Cost+" 能量",16));
            row.Children.Add(Ct(G.CurrentCardText(card),12,U.Muted));var box=U.Card(row,10);
            AutomationProperties.SetAutomationId(box,"cards-pile-row-"+card);rows.Children.Add(box);
        }
        if(cardPile==0){
            if(G.C.BranchChosen)rows.Children.Add(Ct("本局方向："+G.CardBranchName,14,U.Green));
            rows.Children.Add(Ct(G.C.Relic>0?"随身小物："+G.CardRelicText:G.CardRelicText,13,U.Green));
        }
    }
}
