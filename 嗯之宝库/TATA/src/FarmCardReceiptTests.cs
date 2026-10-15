using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    internal void RenderCardTurnReceiptChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
            for(int size=0;size<3;size++){
                StatusFixture(size,2,4,2,2,5,0,1);ClickCard("cards-hand-0");ClickCard("cards-hand-0");
                var stale=CardFind("cards-end");string before=FarmPracticeTests.Xml(G.S);var forecast=G.ForecastCards();
                check(before==FarmPracticeTests.Xml(G.S),"turn receipt preview preserves all state and RNG");ClickCard("cards-end");
                check(G.C.Hp==57&&G.C.EnemyHp==184&&G.C.Hand.Count==5&&G.C.BranchTriggers==1,"two-hit reflect and fragile resolve real 16 damage and fifth draw");
                check(G.C.Message.Contains("反击造成 16 实际伤害")&&G.C.Message.Contains("破盾多抽 1 张（共 5 张）")&&forecast.Hp==G.C.Hp,"persistent receipt matches aggregate action and forecast");
                check(cardFeedback.Damage==16&&cardFeedback.Taken==3,"popup and persistent receipt agree");cardFeedback.TestElapsed=950;StatusShot(folder,"turn-counter",size);
                string state=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"stale end button cannot duplicate counter or draw");
                ClickCard("cards-deck");CardBack();PauseForBackground();Pulse(false,false);check(state==FarmPracticeTests.Xml(G.S),"browse cancel and background preserve committed turn receipt");StatusShot(folder,"turn-return",size);
                StatusFixture(size,1,1,1,2,5,0,1);ClickCard("cards-hand-0");ClickCard("cards-hand-0");ClickCard("cards-end");
                check(G.C.Block==5&&G.C.Message.Contains("保留护盾 5")&&G.C.Message.Contains("反击造成 4 实际伤害")&&!G.C.Message.Contains("破盾"),"ordinary reflect and real retained shield are both reported");G.S.DesktopMotion=false;StatusShot(folder,"turn-carry",size);
                StatusFixture(size,2,0,1,2);G.C.Draw.Clear();G.C.Exhaust=new[]{8,14}.ToList();G.C.Deck=G.C.Hand.Concat(G.C.Exhaust).ToList();G.ValidateCards();Render();ClickCard("cards-hand-0");ClickCard("cards-end");
                check(G.C.Hand.Count==1&&G.C.Message.Contains("破盾多抽 0 张（共 1 张），可抽牌不足")&&!G.C.Message.Contains("多抽 1 张"),"legal small-pile boundary never invents an extra draw");StatusShot(folder,"turn-short",size);
                StatusFixture(size,2,1,3,2,5,0,1);ClickCard("cards-hand-0");ClickCard("cards-hand-0");ClickCard("cards-end");
                check(G.C.EnemyHp==200&&!G.C.Message.Contains("反击")&&!G.C.Message.Contains("破盾")&&G.C.Message.Contains("没有攻击"),"rest never reports a counter or bonus draw");StatusShot(folder,"turn-rest",size);
                StatusFixture(size,0,1,3,5,0,1,2);G.C.RulesVersion=0;Render();ClickCard("cards-hand-0");ClickCard("cards-end");check(G.C.Message.Contains("反击造成 8 实际伤害")&&G.C.Hp==51,"old two-hit attack receipt follows legacy rules");
                StatusFixture(size,0,0,1,5,0,1,2);G.C.EnemyHp=3;Render();ClickCard("cards-hand-0");ClickCard("cards-end");
                check(G.C.Phase=="reward"&&cardFeedback.Damage==3&&G.C.Victories==1,"lethal counter shows capped actual damage and one reward");state=FarmPracticeTests.Xml(G.S);cardFeedback.TestElapsed=950;TickCardFeedback();check(state==FarmPracticeTests.Xml(G.S),"lethal feedback completion cannot replay settlement");
            }
            var saved=new FarmGame(Path.Combine(folder,"turn-receipt-"+Guid.NewGuid().ToString("N")+".xml"));saved.StartCards(false);saved.C.ShieldBranch=2;saved.C.BranchChosen=true;saved.PlayCard(2,saved.C.Revision);saved.EndCardTurn(saved.C.Revision);
            var loaded=new FarmGame(saved.SavePath);check(saved.C.Message==loaded.C.Message&&loaded.C.Message.Contains("反击造成 8 实际伤害")&&loaded.C.Message.Contains("共 5 张"),"normal isolated reload keeps actual turn receipt");string disk=File.ReadAllText(saved.SavePath);loaded.ForecastCards();check(disk==File.ReadAllText(saved.SavePath),"loaded preview never rewrites receipt save");saved.EndCardTurn(saved.C.Revision);loaded.EndCardTurn(loaded.C.Revision);check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(loaded.C),"reload continues identical settlement");
            File.WriteAllText(Path.Combine(folder,"cards-turn-receipt-regression.txt"),"PASS: actual aggregate reflect and fragile damage; actual base/extra draw with short-pile boundary; retained shield; no counter during rest; legacy two-hit attack; lethal capped feedback and exactly one reward; prediction/browse/cancel/background preserve state and RNG; stale end input; isolated normal save reload and deterministic continuation; three fonts at 764x680, unclipped receipts and visible first hand row/end; no visible window or real player save.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
    private void ReceiptFixture(int size,int card,int hp,int others,int draw)
    {
        G.S=new FarmSave{ReadingSize=size};G.StartCards(false);G.C.Turn=2;G.C.Hp=hp;
        G.C.Hand=new[]{card}.Concat(Enumerable.Repeat(3,others)).ToList();G.C.Draw=Enumerable.Repeat(3,draw).ToList();
        G.C.Discard.Clear();G.C.Exhaust.Clear();G.C.Deck=G.C.Hand.Concat(G.C.Draw).ToList();G.C.EnemyHp=G.C.EnemyMax=200;
        ClearCardFeedback();Render();G.ValidateCards();
    }
    internal void RenderCardReceiptChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{
            Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
            for(int size=0;size<3;size++){
                ReceiptFixture(size,8,58,2,3);var stale=CardFind("cards-hand-0");ClickCard("cards-hand-0");
                check(G.C.Hp==60&&G.C.Message.Contains("恢复 2 体力")&&cardFeedback.PlayerText=="体力 +2","healing is actual capped gain");
                cardFeedback.TestElapsed=210;CardShot(folder,"receipt-heal",size);string state=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"stale healing cannot spend twice");
                ReceiptFixture(size,8,60,2,3);ClickCard("cards-hand-0");check(G.C.Message.Contains("体力已满，未恢复")&&cardFeedback.PlayerText=="体力已满","full HP does not claim healing");CardShot(folder,"receipt-full",size);
                ReceiptFixture(size,109,45,2,2);G.C.Stage=2;Render();ClickCard("cards-hand-0");
                check(G.C.Energy==4&&G.C.Block==3&&cardFeedback.Drawn==1&&G.C.Message.Contains("护盾 +3 · 能量 +1 · 抽到 1 张"),"upgraded breath reports all three gains");
                check(cardFeedback.PlayerText.Contains("护盾 +3")&&cardFeedback.PlayerText.Contains("能量 +1")&&cardFeedback.PlayerText.Contains("抽 1 张"),"popup never hides simultaneous gains");
                cardFeedback.TestElapsed=210;CardShot(folder,"receipt-breath",size);check(LogicalNodes((DependencyObject)Content).OfType<TextBlock>().Any(t=>t.Text=="能量  4"),"extra energy is not shown as a false 3-point cap");state=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");CardBack();check(state==FarmPracticeTests.Xml(G.S)&&G.C.Message.Contains("抽到 1 张"),"receipt persists through browse and cancellation");CardShot(folder,"receipt-return",size);CardVisible("cards-end");
                ReceiptFixture(size,9,45,0,0);G.C.Energy=0;Render();ClickCard("cards-hand-0");check(G.C.Hand.Count==0&&G.C.Energy==1&&cardFeedback.Drawn==0&&cardFeedback.PlayerText.Contains("抽 0 张")&&G.C.Message.Contains("没有可抽的牌"),"empty deck never claims a drawn card");CardShot(folder,"receipt-empty",size);
                ReceiptFixture(size,106,45,6,3);ClickCard("cards-hand-0");check(G.C.Hand.Count==7&&cardFeedback.Drawn==1&&G.C.Message.Contains("抽到 1 张（手牌已满）")&&G.C.Block==3,"draw cap reports one actual card, not two");cardFeedback.TestElapsed=210;CardShot(folder,"receipt-cap",size);CardVisible("cards-end");
                for(int id=0;id<15;id++)foreach(int upgrade in new[]{0,100}){
                    ReceiptFixture(size,id+upgrade,40,1,3);G.C.Wet=2;string before=FarmPracticeTests.Xml(G.S);G.ForecastCards(0);check(before==FarmPracticeTests.Xml(G.S),"prediction never writes receipts or changes RNG");
                    ClickCard("cards-hand-0");G.ValidateCards();check(!G.C.Message.Contains("已生效")&&G.C.Message.Length>G.CurrentCardName(id+upgrade).Length+4,"each normal and upgraded card has a concrete receipt");
                    if(id==5)check(G.C.Message.Contains("反击 "+(upgrade>0?7:4)),"reflection amount remains visible after card leaves hand");
                    if(id==12)check(G.C.Message.Contains("下次攻击每段 −3"),"weakening is explicit");
                    if(id==13)check(G.C.Message.Contains("实际伤害")&&G.C.Message.Contains("抽到 1 张"),"attack and draw both reported");
                }
                ReceiptFixture(size,109,45,2,2);G.S.DesktopMotion=false;ClickCard("cards-hand-0");check(cardFeedback.PlayerOffset==0&&cardFeedback.PlayerText.Contains("抽 1 张"),"motion-off retains gain feedback");CardShot(folder,"receipt-static",size);
            }
            var saved=new FarmGame(Path.Combine(folder,"receipt-isolated.xml"));saved.StartCards(false);saved.PlayCard(2,saved.C.Revision);string expected=saved.C.Message;var loaded=new FarmGame(saved.SavePath);check(expected==loaded.C.Message&&loaded.C.Message.Contains("护盾 +7"),"real isolated save preserves receipt");
            File.WriteAllText(Path.Combine(folder,"cards-receipt-regression.txt"),"PASS: actual partial/full healing; shield/energy/draw combined; zero draws with empty deck; capped draws; all 15 cards and upgrades across three scales; attack+draw and reflect/weak receipts; no preview/browse/RNG mutation; stale button rejection; persisted receipt and isolated normal reload; motion-off; 764x680 detached WPF only. Boundary fixtures are not human playthroughs.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
