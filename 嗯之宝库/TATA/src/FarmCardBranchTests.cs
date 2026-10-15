using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public static class FarmCardBranchTests
{
    private static void Check(bool value,string name){FarmHeadlessTests.Check(value,"branch: "+name);}
    private static FarmGame New(int branch=0){var g=new FarmGame(null,null,true);g.StartCards(false);g.C.ShieldBranch=branch;g.C.BranchChosen=true;g.C.EnemyHp=g.C.EnemyMax=200;return g;}
    private static void SetHand(FarmGame g,params int[] cards){g.C.Hand=cards.ToList();g.C.Draw=new[]{3,0,1,2}.ToList();g.C.Discard.Clear();g.C.Exhaust.Clear();g.C.Deck=g.C.Hand.Concat(g.C.Draw).ToList();}
    public static string Run(string root)
    {
        var thick=New(1);thick.PlayCard(2,thick.C.Revision);thick.EndCardTurn(thick.C.Revision);Check(thick.C.Block==2&&!thick.C.KeepShield&&thick.C.BranchTriggers==1,"thick shields retain remaining 2 once");
        thick.EndCardTurn(thick.C.Revision);Check(thick.C.Block==0&&thick.C.Hp==54,"retained shield absorbs next attack and expires without another thick card");
        var fragile=New(2);fragile.PlayCard(2,fragile.C.Revision);fragile.EndCardTurn(fragile.C.Revision);Check(fragile.C.Hp==59&&fragile.C.EnemyHp==192&&fragile.C.Hand.Count==5&&fragile.C.BranchTriggers==1,"fragile breaks, counters 8 and adds one next-turn draw");
        var rest=New(2);rest.C.Stage=1;rest.C.Turn=3;rest.PlayCard(2,rest.C.Revision);int hp=rest.C.Hp;rest.EndCardTurn(rest.C.Revision);Check(rest.C.Hp==hp&&rest.C.BranchTriggers==0&&!rest.C.FragileShield&&rest.C.Block==0,"rest causes no fake shield break or counter");
        var mist=New();mist.C.Stage=1;mist.C.Turn=3;SetHand(mist,12);mist.PlayCard(0,mist.C.Revision);mist.EndCardTurn(mist.C.Revision);Check(mist.C.Weak==1&&mist.CardIntentDamage==4,"next-attack reduction survives a resting enemy turn");mist.EndCardTurn(mist.C.Revision);Check(mist.C.Hp==56&&mist.C.Weak==0,"reduction expires only after the next attack");
        var multi=New(2);multi.C.Stage=4;multi.C.Turn=2;SetHand(multi,2,2,0,1);multi.PlayCard(0,multi.C.Revision);multi.PlayCard(0,multi.C.Revision);multi.EndCardTurn(multi.C.Revision);Check(multi.C.Hp==56&&multi.C.EnemyHp==192&&multi.C.BranchTriggers==1,"two fragile shields and two-hit enemy trigger only once");
        foreach(int branch in new[]{1,2}){var up=New(branch);SetHand(up,102,7);up.PlayCard(0,up.C.Revision);Check(up.C.Block==(branch==1?10:7),"upgraded branch retains plus three shield");up.PlayCard(0,up.C.Revision);Check(up.C.EnemyHp==200-3-up.C.Block,"shield slam uses branch shield amount");}
        var snail=New();snail.C.Stage=2;SetHand(snail,3,1,10,2);snail.PlayCard(0,snail.C.Revision);Check(snail.C.EnemyHp==193&&snail.C.EnemyBlock==5&&snail.C.SnailTriggered,"first direct hit resolves before shell");snail.PlayCard(0,snail.C.Revision);Check(snail.C.EnemyHp==193&&snail.C.EnemyBlock==0,"second hit is blocked without another shell");snail.EndCardTurn(snail.C.Revision);Check(!snail.C.SnailTriggered,"snail resets next turn");
        var doubleHit=New();doubleHit.C.Stage=2;SetHand(doubleHit,10);doubleHit.PlayCard(0,doubleHit.C.Revision);Check(doubleHit.C.EnemyHp==193&&doubleHit.C.EnemyBlock==0,"shell applies between two hits of one card");
        var counter=New(2);counter.C.Stage=2;counter.PlayCard(2,counter.C.Revision);counter.EndCardTurn(counter.C.Revision);Check(counter.C.EnemyHp==192&&!counter.C.SnailTriggered,"retaliation does not trigger direct-hit shell");
        var boss=New();boss.C.Stage=4;boss.C.Turn=3;SetHand(boss,3,1,0,2);Check(boss.CardIntentDamage==24&&!boss.CardIntentRest,"boss telegraphs heavy attack");boss.PlayCard(0,boss.C.Revision);Check(boss.C.BossDamage==7&&!boss.CardIntentRest,"partial actual damage counts");boss.PlayCard(0,boss.C.Revision);Check(boss.C.BossDamage==12&&boss.CardIntentRest&&boss.CardIntentDamage==0,"exact threshold interrupts");boss.EndCardTurn(boss.C.Revision);Check(boss.C.Hp==60&&boss.C.BossDamage==0,"interrupted enemy does nothing and progress resets");
        var blocked=New();blocked.C.Stage=4;blocked.C.Turn=3;blocked.C.EnemyBlock=5;SetHand(blocked,3,1);blocked.PlayCard(0,blocked.C.Revision);blocked.PlayCard(0,blocked.C.Revision);Check(blocked.C.BossDamage==7&&!blocked.CardIntentRest,"absorbed damage does not count toward interrupt");blocked.EndCardTurn(blocked.C.Revision);Check(blocked.C.Hp==36,"failed interrupt executes advertised heavy attack");
        var choice=New();choice.C.BranchChosen=false;choice.C.EnemyHp=1;choice.PlayCard(0,choice.C.Revision);choice.PickCardReward(-1,choice.C.Revision);Check(choice.C.Phase=="branch","first reward introduces separate branch choice");int rev=choice.C.Revision;int choiceSeed=choice.C.Seed;Check(choice.PickShieldBranch(1,rev)&&!choice.PickShieldBranch(2,rev)&&choice.C.Phase=="route"&&choice.C.Seed==choiceSeed,"choice once, no random reroll or automatic departure");
        choice.RetireCards(choice.C.Revision);choice.StartCards(false);Check(choice.C.ShieldBranch==0&&!choice.C.BranchChosen,"new run resets branch");
        for(int seed=1;seed<=30;seed++){var first=New();first.C.Seed=seed*731;first.C.BranchChosen=false;first.C.EnemyHp=1;first.PlayCard(0,first.C.Revision);first.PickCardReward(-1,first.C.Revision);first.PickShieldBranch(seed%2+1,first.C.Revision);first.ChooseCardRoute(false,first.C.Revision);Check(first.C.Hand.Any(c=>MossCards.Id(c)==2),"second battle opening always demonstrates chosen shield");first.ValidateCards();}
        var save=new FarmGame(Path.Combine(root,"branches-roundtrip.xml"));save.StartCards(false);save.C.ShieldBranch=2;save.C.BranchChosen=true;save.C.Stage=4;save.C.Turn=3;save.C.BossDamage=7;save.PlayCard(2,save.C.Revision);save.Save();string xml=FarmPracticeTests.Xml(save.C);var load=new FarmGame(save.SavePath);Check(FarmPracticeTests.Xml(load.C)==xml,"pending fragile and interrupt progress survive save reload");save.EndCardTurn(save.C.Revision);load.EndCardTurn(load.C.Revision);Check(FarmPracticeTests.Xml(save.C)==FarmPracticeTests.Xml(load.C),"reload preserves next action exactly");
        var doc=System.Xml.Linq.XDocument.Load(save.SavePath);foreach(string field in new[]{"RulesVersion","ShieldBranch","BossDamage","BranchTriggers","BranchChosen","KeepShield","FragileShield","SnailTriggered"})doc.Root.Element("Cards").Element(field).Remove();string legacy=Path.Combine(root,"cards-v400.xml");doc.Save(legacy);var old=new FarmGame(legacy);old.C.Stage=1;old.C.Turn=3;Check(old.C.RulesVersion==0&&!old.CardIntentRest&&old.CardIntentHits==2,"4.0 old run keeps its attack schedule");old.RetireCards(old.C.Revision);old.StartCards(false);Check(old.C.RulesVersion==1,"next run opts into new rules");
        return "PASS: thick one-turn carry; fragile break/draw timing and one trigger across multiple shields/hits; upgraded branches and slam; snail first hit, multihit and retaliation; crab rest; boss interrupt exact/below threshold and block absorption; branch choice and stale rejection; isolated pending-effect saves and deterministic continuation; 4.0 old-run compatibility; new-run reset.";
    }
}
public partial class FarmPanel
{
    internal void RenderBranchChecks(string folder)
    {
        var original=G.S;string tab=Tab;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;G.StartCards(false);G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);G.PickCardReward(-1,G.C.Revision);
            CardShot(folder,"branch",size);CardVisible("cards-branch-thick");CardVisible("cards-branch-fragile");CardVisible("cards-branch-skip");string before=FarmPracticeTests.Xml(G.C);ClickCard("cards-deck");CardBack();FarmHeadlessTests.Check(FarmPracticeTests.Xml(G.C)==before,"branch preview and back are free");
            var stale=CardFind("cards-branch-thick");ClickCard("cards-branch-fragile");stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));FarmHeadlessTests.Check(G.C.ShieldBranch==2&&G.C.Phase=="route","stale branch cannot replace explicit choice");ClickCard("cards-route-safe");
            CardShot(folder,"branch-battle",size);CardVisible("cards-end");for(int i=0;i<4;i++)CardVisible("cards-hand-"+i);G.C.Stage=2;G.C.SnailTriggered=false;G.C.EnemyHp=G.C.EnemyMax=43;CardShot(folder,"snail",size);CardVisible("cards-hand-0");
            G.C.Stage=1;G.C.Turn=3;CardShot(folder,"rest",size);G.C.Stage=4;G.C.EnemyHp=G.C.EnemyMax=86;G.C.BossDamage=7;CardShot(folder,"charge",size);CardVisible("cards-end");G.C.BossDamage=12;CardShot(folder,"interrupted",size);
            G.C.ShieldBranch=1;G.C.Stage=2;G.C.Turn=1;G.C.Hand=new[]{2,7,0,1}.ToList();G.C.Energy=3;G.C.Block=2;CardShot(folder,"thick",size);CardVisible("cards-hand-0");
        }File.WriteAllText(Path.Combine(folder,"branch-ui-regression.txt"),"PASS: three font scales at 764x680; branch choices inside actual viewport; browsing and cancel preserve choice; stale choice rejection; real route action; branch card descriptions, snail/rest/interrupt cues and progress; thick card hand and end turn visible. Detached WPF only.");}
        finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;}
    }
}
