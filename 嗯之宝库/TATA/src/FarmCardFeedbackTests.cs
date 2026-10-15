using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;

public partial class FarmPanel
{
    internal void RenderCardFeedbackChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int scale=0;scale<3;scale++){
            G.S=new FarmSave{ReadingSize=scale,DesktopMotion=true};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            CardShot(folder,"energy-full",scale);EnergyVisible();for(int i=0;i<4;i++)CardVisible("cards-hand-"+i);CardVisible("cards-end");
            var stale=CardFind("cards-hand-0");ClickCard("cards-hand-0");check(G.C.Energy==2&&cardFeedback.Damage==4&&cardFeedback.Title=="泼水","attack records actual damage and spends one energy");
            var f=cardFeedback;string state=FarmPracticeTests.Xml(G.C);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.C)==state&&cardFeedback==f,"stale attack cannot replay effects or spend twice");
            f.TestElapsed=0;CardShot(folder,"attack-start",scale);double start=f.PlayerOffset;f.TestElapsed=210;CardShot(folder,"attack-hit",scale);check(Math.Abs(f.PlayerOffset-start)>15,"player lunges using presentation time");
            f.TestElapsed=800;CardShot(folder,"attack-return",scale);check(f.PlayerOffset==0&&FarmPracticeTests.Xml(G.C)==state,"attack returns without committing gameplay again");
            ClickCard("cards-hand-0");check(cardFeedback.Combo&&cardFeedback.Damage==11,"combo displays real damage");cardFeedback.TestElapsed=210;CardShot(folder,"feedback-combo",scale);
            ClickCard("cards-hand-0");check(G.C.Energy==0&&cardFeedback.Shield==7,"shield feedback and empty energy");cardFeedback.TestElapsed=210;CardShot(folder,"energy-empty",scale);EnergyVisible();CardVisible("cards-end");check(!CardFind("cards-hand-0").IsEnabled,"unaffordable attack disabled");
            ClickCard("cards-end");check(cardFeedback.EnemyAction&&cardFeedback.Blocked==5&&cardFeedback.Taken==0&&G.C.Energy==3,"enemy block feedback and new-turn refill");cardFeedback.TestElapsed=210;CardShot(folder,"enemy-blocked",scale);check(cardFeedback.EnemyOffset<0,"enemy lunges toward player");
            state=FarmPracticeTests.Xml(G.C);ClickCard("cards-deck");CardBack();check(cardFeedback==null&&FarmPracticeTests.Xml(G.C)==state,"browse clears transient feedback without changing combat");
            G.C.Block=0;Render();ClickCard("cards-end");check(cardFeedback.Taken>0,"unblocked enemy damage feedback");cardFeedback.TestElapsed=210;CardShot(folder,"enemy-hit",scale);
            G.C.Hand=new[]{9,3}.ToList();G.C.Energy=0;Render();ClickCard("cards-hand-0");check(G.C.Energy==1&&cardFeedback.EnergyDelta==1,"zero-cost energy recovery still works");
            G.C.Hand=new[]{10}.ToList();G.C.Energy=3;G.C.EnemyHp=50;G.C.EnemyMax=50;Render();ClickCard("cards-hand-0");check(cardFeedback.Hits==2&&cardFeedback.Damage>=12,"two-hit card shows aggregate actual damage");cardFeedback.TestElapsed=210;CardShot(folder,"feedback-multihit",scale);
            G.C.Hand=new[]{3}.ToList();G.C.Energy=3;G.C.EnemyHp=1;Render();ClickCard("cards-hand-0");check(G.C.Phase=="reward"&&HoldCardResult&&cardFeedback.Damage==1,"last hit holds battle presentation after single committed victory");cardFeedback.TestElapsed=210;CardShot(folder,"last-hit",scale);state=FarmPracticeTests.Xml(G.C);cardFeedback.TestElapsed=950;TickCardFeedback();check(!HoldCardResult&&FarmPracticeTests.Xml(G.C)==state,"finishing animation never grants reward again");CardShot(folder,"feedback-reward",scale);CardVisible("cards-reward-0");
            G.S=new FarmSave{ReadingSize=scale,DesktopMotion=false};G.StartCards(false);Render();ClickCard("cards-hand-0");check(cardFeedback.PlayerOffset==0&&cardFeedback.EnemyOffset==0&&cardFeedback.Damage==4,"motion off keeps static hit feedback");cardFeedback.TestElapsed=210;CardShot(folder,"feedback-static",scale);
            state=FarmPracticeTests.Xml(G.C);PauseForBackground();Pulse(false,false);check(cardFeedback==null&&FarmPracticeTests.Xml(G.C)==state,"hiding cancels presentation, not progress");
        }
        File.WriteAllText(Path.Combine(folder,"cards-feedback-regression.txt"),"PASS: energy 3/2/1/0 and refill, free recovery, min-window viewport at 100/115/130%; real attack/combo/shield/block/hit and multihit feedback; deterministic forward/impact/return frames; stale buttons; last hit delays presentation only and never duplicates rewards; motion-off static alternative; browse/background cancel; no native window or player save. Physical display smoothness not tested.");
        }finally{G.S=original;Tab=tab;ClearCardFeedback();cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;}
    }
    private void EnergyVisible()
    {
        var box=LogicalNodes((DependencyObject)Content).OfType<Border>().First(b=>AutomationProperties.GetAutomationId(b)=="cards-energy");
        var rect=box.TransformToAncestor((FrameworkElement)Content).TransformBounds(new Rect(0,0,box.ActualWidth,box.ActualHeight));
        FarmHeadlessTests.Check(rect.Top>=0&&rect.Bottom<=680&&rect.Left>=0&&rect.Right<=764&&box.ActualHeight>=35,"energy always visible above hand");
    }
}
