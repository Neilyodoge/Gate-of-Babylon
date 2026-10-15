using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class FarmPanel
{
    private Border InspectTile(string id)
    {
        DependencyObject node=CardFind(id);while(!(node is Border&&((Border)node).Child is DockPanel))node=VisualTreeHelper.GetParent(node);return (Border)node;
    }
    private void InspectPointer(Border tile,bool enter){tile.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=enter?Mouse.MouseEnterEvent:Mouse.MouseLeaveEvent});}
    private void InspectFocus(Button button,bool enter){button.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,enter?null:button,enter?button:null){RoutedEvent=enter?Keyboard.GotKeyboardFocusEvent:Keyboard.LostKeyboardFocusEvent});}
    internal void RenderCardInspectChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            CardShot(folder,"inspect-start",size);string before=FarmPracticeTests.Xml(G.S);
            var attack=InspectTile("cards-hand-0");var shield=InspectTile("cards-hand-2");var attackButton=CardFind("cards-hand-0");var shieldButton=CardFind("cards-hand-2");
            InspectFocus(shieldButton,true);check(cardForecastText.Text.Contains("不掉体力"),"keyboard shield preview");
            InspectPointer(attack,true);check(cardForecastText.Text.Contains("5 体力"),"pointer attack takes over preview");
            InspectPointer(attack,false);check(cardForecastText.Text.Contains("不掉体力"),"leaving another card restores still-focused shield");ForecastShot(folder,"inspect-focus-return",size);
            InspectFocus(shieldButton,false);check(cardForecastText.Text.StartsWith("现在结束"),"no active source restores end-turn prediction");
            InspectPointer(shield,true);InspectFocus(attackButton,true);check(cardForecastText.Text.Contains("5 体力"),"keyboard can take over stationary pointer");
            InspectFocus(attackButton,false);check(cardForecastText.Text.Contains("不掉体力"),"leaving focus restores still-hovered shield");ForecastShot(folder,"inspect-pointer-return",size);
            var end=CardFind("cards-end");InspectFocus(end,true);check(cardForecastText.Text.StartsWith("现在结束")&&cardForecastText.Text.Contains("5 体力"),"end-turn focus overrides hovered card with actual end result");ForecastShot(folder,"inspect-end-focus",size);InspectFocus(end,false);check(cardForecastText.Text.Contains("不掉体力"),"leaving end focus restores hovered card");
            InspectPointer(attack,false);check(cardForecastText.Text.Contains("不掉体力"),"late exit from another tile cannot clear active pointer");
            InspectPointer(shield,false);check(cardForecastText.Text.StartsWith("现在结束"),"last pointer exit clears preview");
            check(before==FarmPracticeTests.Xml(G.S),"mixed preview events preserve full state and random seed");
            InspectFocus(shieldButton,true);ClickCard("cards-deck");CardBack();check(before==FarmPracticeTests.Xml(G.S)&&cardForecastText.Text.StartsWith("现在结束"),"browse rebuild clears obsolete preview source");
            InspectPointer(shield,true);InspectFocus(shieldButton,true);check(cardForecastText.Text.StartsWith("现在结束"),"old tiles cannot revive preview after rebuild");
            var current=CardFind("cards-hand-2");ClickCard("cards-hand-2");string after=FarmPracticeTests.Xml(G.S);current.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(after==FarmPracticeTests.Xml(G.S),"preview tracking does not allow duplicate play");
            G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);G.C.Offers.Clear();G.C.Offers.AddRange(new[]{1,7,8});CardShot(folder,"inspect-reward-start",size);before=FarmPracticeTests.Xml(G.S);
            var wet=InspectTile("cards-reward-0");var guard=InspectTile("cards-reward-1");var wetButton=CardFind("cards-reward-0");var guardButton=CardFind("cards-reward-1");
            InspectFocus(guardButton,true);InspectPointer(wet,true);check(cardRewardFitTitle.Text.Contains("小小电花"),"reward pointer context");
            InspectPointer(wet,false);check(cardRewardFitTitle.Text.Contains("泡泡冲撞"),"reward pointer exit restores focused choice");ForecastShot(folder,"inspect-reward-focus",size);
            InspectFocus(guardButton,false);check(cardRewardFitTitle.Text.Contains("移到奖励牌"),"reward neutral when neither source active");
            InspectPointer(guard,true);InspectFocus(wetButton,true);InspectFocus(wetButton,false);check(cardRewardFitTitle.Text.Contains("泡泡冲撞"),"reward focus exit restores hovered choice");ForecastShot(folder,"inspect-reward-pointer",size);
            var skip=CardFind("cards-skip");InspectFocus(skip,true);check(cardRewardFitTitle.Text.Contains("移到奖励牌"),"skip focus does not suggest a reward selection");InspectFocus(skip,false);check(cardRewardFitTitle.Text.Contains("泡泡冲撞"),"leaving skip focus restores hovered reward");
            InspectPointer(wet,false);check(cardRewardFitTitle.Text.Contains("泡泡冲撞"),"stale exit cannot clear reward context");InspectPointer(guard,false);
            check(before==FarmPracticeTests.Xml(G.S),"reward inspection neither claims nor rerolls");CardVisible("cards-skip");
            ClickCard("cards-deck");CardBack();check(before==FarmPracticeTests.Xml(G.S),"reward browse cancel preserves offers");var claim=CardFind("cards-reward-1");int count=G.C.Deck.Count;ClickCard("cards-reward-1");claim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));InspectPointer(wet,true);InspectFocus(guardButton,true);check(G.C.Deck.Count==count+1&&G.C.Phase=="branch","explicit claim once; stale events inert in next phase");
        }
        File.WriteAllText(Path.Combine(folder,"cards-inspect-regression.txt"),"PASS: latest active mouse/focus source; fallback to still-focused or still-hovered card; default only when both leave; unrelated late exits ignored; battle and reward contexts; rebuild and stale events; preview/cancel preserve full save and RNG; explicit play and claim only once; three font scales at 764x680. Synthetic routed events on detached WPF, not physical Windows input.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
