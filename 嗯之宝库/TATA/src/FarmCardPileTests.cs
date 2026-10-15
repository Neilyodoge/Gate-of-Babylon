using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;

public partial class FarmPanel
{
    private string PileCopy(){return String.Join("|",LogicalNodes((DependencyObject)Content).OfType<TextBlock>().Select(t=>t.Text));}
    internal void RenderCardPileChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);Render();
            string before=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");CardShot(folder,"piles-all",size);
            check(PileCopy().Contains("泼水 × 2")&&PileCopy().Contains("泡泡盾 × 2"),"duplicate cards grouped with actual counts");
            for(int i=0;i<5;i++){
                ClickCard("cards-pile-"+i);FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-piles-view-"+i+"-"+size+".png"),764,680);
                CardVisible("cards-overlay-back");for(int j=0;j<5;j++)CardVisible("cards-pile-"+j);
                check(AutomationProperties.GetName(CardFind("cards-pile-"+i)).Contains("已选"),"selected pile has non-color label");
                check(FarmPracticeTests.Xml(G.S)==before,"all pile browsing preserves entire save and RNG");
            }
            check(PileCopy().Contains("还没有本场用尽"),"empty exhausted pile explains absence");
            ClickCard("cards-pile-2");CardShot(folder,"piles-draw",size);check(PileCopy().Contains("不代表抽到的先后"),"draw list explicitly hides order");
            var drawCopy=G.C.BrowsePile(2);var realDraw=G.C.Draw.ToArray();Array.Reverse(drawCopy);check(G.C.Draw.SequenceEqual(realDraw),"browse snapshot cannot mutate draw pile");
            string shown=PileCopy();G.C.Draw.Reverse();Render();check(shown==PileCopy(),"different draw order has identical presentation");G.C.Draw.Reverse();
            var stale=CardFind("cards-pile-4");CardBack();stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!cardDeckOpen&&G.C.Phase=="battle"&&FarmPracticeTests.Xml(G.S)==before,"stale pile button does not reopen or modify battle");
            ClickCard("cards-hand-0");check(G.C.Discard.Count==1&&G.C.Discard[0]==0,"actual played card moves to discard");ClickCard("cards-deck");ClickCard("cards-pile-3");CardShot(folder,"piles-discard",size);check(PileCopy().Contains("泼水 × 1"),"discard shows actual played card");CardBack();
            // Boundary fixture for once-per-battle cards and mixed upgraded copies.
            G.C.Hand.Insert(0,8);G.C.Deck.Add(8);G.C.Deck.Add(100);G.C.Energy=3;Render();ClickCard("cards-hand-0");ClickCard("cards-deck");ClickCard("cards-pile-4");CardShot(folder,"piles-exhaust",size);
            check(PileCopy().Contains("莓果点心 × 1")&&G.C.Exhaust.Contains(8),"one-use card is in exhausted pile after explicit use");
            ClickCard("cards-pile-0");check(PileCopy().Contains("泼水 + × 1")&&PileCopy().Contains("泼水 × 2"),"upgraded and normal copies are distinct");
            G.C.ShieldBranch=2;G.C.BranchChosen=true;Render();check(PileCopy().Contains("脆泡泡"),"branch names match current battle rules");
            before=FarmPracticeTests.Xml(G.S);CardBack();ClickCard("cards-deck");check(cardPile==0&&before==FarmPracticeTests.Xml(G.S),"reopen starts at full deck without spending");
            var button=CardFind("cards-pile-2");check(KeyboardTargets().Contains(button),"pile filters keyboard reachable");
            HandlePanelKey(Key.Enter,ModifierKeys.None,false,button);check(cardPile==2,"keyboard activates pile filter");HandlePanelKey(Key.Escape,ModifierKeys.None,false,null);check(!cardDeckOpen,"escape returns to battle");
            G.C.Phase="camp";Render();ClickCard("cards-deck");check(!LogicalNodes((DependencyObject)Content).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="cards-pile-2"),"outside battle stale pile contents are hidden");CardBack();
            ClickCard("cards-upgrade");before=FarmPracticeTests.Xml(G.S);ClickCard("cards-overlay-back");check(before==FarmPracticeTests.Xml(G.S),"upgrade remains separate and cancels freely");
            G.C.Deck.Clear();for(int c=0;c<15;c++){G.C.Deck.Add(c);G.C.Deck.Add(c+100);}Render();ClickCard("cards-deck");CardShot(folder,"piles-long",size);
            var scroll=LogicalNodes((DependencyObject)Content).OfType<ScrollViewer>().Single(s=>AutomationProperties.GetAutomationId(s)=="cards-pile-scroll");check(HandlePanelKey(Key.End,ModifierKeys.None,false,null),"keyboard end consumed");((FrameworkElement)Content).UpdateLayout();check(scroll.VerticalOffset>0,"keyboard reaches end of long collection");HandlePanelKey(Key.Home,ModifierKeys.None,false,null);((FrameworkElement)Content).UpdateLayout();check(scroll.VerticalOffset==0,"keyboard home returns to start");HandlePanelKey(Key.PageDown,ModifierKeys.None,false,null);((FrameworkElement)Content).UpdateLayout();check(scroll.VerticalOffset>0,"keyboard page down reveals cards");CardVisible("cards-overlay-back");check(scroll.ScrollableHeight>0,"long collection scrolls with fixed return");CardBack();
        }
        RenderCardRelicChecks(folder);
        File.WriteAllText(Path.Combine(folder,"cards-piles-regression.txt"),"PASS: actual counts and grouped duplicates; separate upgrades and branch names; immutable sorted snapshots hide draw order; actual play moves cards to discard/exhaust; empty states; browse, cancel and repeat preserve full save/RNG; stale buttons rejected; keyboard/escape and PageDown/Home/End scrolling; camp hides old battle piles; upgrade cancellation; three scales at 764x680 with fixed return and scrollable long collection. Detached WPF only.");
        }finally{G.S=original;Tab=tab;cardPile=0;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}

public partial class FarmPanel
{
    internal void RenderCardRelicChecks(string folder)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)foreach(int relic in new[]{0,1,2}){
            G.S=new FarmSave{ReadingSize=size};G.StartCards(false);G.C.Relic=relic;G.C.RulesVersion=relic==2?0:1;
            cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();Render();
            string state=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");
            check(PileCopy().Contains(G.CardRelicText),"pile describes actual stored relic");
            check(relic!=2||!PileCopy().Contains("树叶帽")&&!PileCopy().Contains("开局 +4"),"legacy electric relic never promises shield");
            var scroll=LogicalNodes((DependencyObject)Content).OfType<ScrollViewer>().Single(v=>AutomationProperties.GetAutomationId(v)=="cards-pile-scroll");
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-relic-layout-"+relic+"-"+size+".png"),764,680);
            scroll.ScrollToEnd();((FrameworkElement)Content).UpdateLayout();
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-relic-"+relic+"-"+size+".png"),764,680);
            CardVisible("cards-overlay-back");CardBack();ClickCard("cards-rules");CardBack();
            check(state==FarmPracticeTests.Xml(G.S),"relic browsing and cancel preserve full save and RNG");
            // Win through a real explicit hit; existing relics must not be replaced by the reward.
            G.C.Elite=true;G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);Render();
            check(G.C.Relic==(relic==0?1:relic),"elite victory keeps existing relic or awards first hat");
            check(PileCopy().Contains(G.CardRelicText),"reward description matches actual relic");
            check(relic!=2||!PileCopy().Contains("树叶帽"),"legacy reward does not invent hat");
            CardShot(folder,"relic-reward-"+relic,size);state=FarmPracticeTests.Xml(G.S);
            var stale=CardFind("cards-reward-0");ClickCard("cards-deck");CardBack();
            check(state==FarmPracticeTests.Xml(G.S),"reward preview and cancel preserve offer and RNG");
            stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(state==FarmPracticeTests.Xml(G.S),"stale reward cannot claim");
            var reward=CardFind("cards-reward-0");ClickCard("cards-reward-0");state=FarmPracticeTests.Xml(G.S);
            reward.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"reward cannot be claimed twice");
        }
        foreach(int rules in new[]{0,1})foreach(int relic in new[]{0,1,2}){
            var game=new FarmGame(Path.Combine(folder,"relic-isolated-"+rules+"-"+relic+".xml"));game.StartCards(false);game.C.RulesVersion=rules;game.C.Relic=relic;
            game.C.Phase="route";game.C.Stage=1;game.ChooseCardRoute(false,game.C.Revision);
            check(game.C.Block==(relic==1?4:0),"only hat grants actual opening shield");
            game.C.Hand=new[]{1,10,2,3}.ToList();game.C.Draw.Clear();game.C.Discard.Clear();game.C.Exhaust.Clear();game.C.Deck=game.C.Hand.ToList();game.C.EnemyHp=game.C.EnemyMax=200;game.ValidateCards();game.Save();
            var loaded=new FarmGame(game.SavePath);string disk=File.ReadAllText(game.SavePath),state=FarmPracticeTests.Xml(loaded.C);
            check(loaded.CardRelicText==game.CardRelicText&&loaded.CardHandText(1).Contains("预计伤害 "+(relic==2?7:5)),"isolated reload keeps relic description and single hit preview");
            check(state==FarmPracticeTests.Xml(loaded.C)&&disk==File.ReadAllText(game.SavePath),"preview does not write isolated save or mutate RNG");
            foreach(var current in new[]{game,loaded}){
                current.PlayCard(0,current.C.Revision);check(current.C.LastDamage==(relic==2?7:5),"actual small spark relic damage");
                current.PlayCard(0,current.C.Revision);check(current.C.LastDamage==(relic==2?16:12),"actual double spark adds bonus per hit");
            }
            check(FarmPracticeTests.Xml(game.C)==FarmPracticeTests.Xml(loaded.C),"normal and old rule reload continue identically");
        }
        File.WriteAllText(Path.Combine(folder,"cards-relic-regression.txt"),"PASS: actual no-relic/hat/electric descriptions; first elite awards hat and later elite keeps existing relic; single/double electric bonus and opening shield match actual rules; normal and legacy isolated saves; preview/cancel preserve state and RNG; stale and repeated reward; three font sizes at 764x680. Detached controls only.");
    }
}
