using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Input;
using System.Xml.Serialization;

public static class FarmCardsTests
{
    private static void Check(bool ok,string message){FarmHeadlessTests.Check(ok,"cards: "+message);}
    private static FarmGame New(){return new FarmGame(null,null,true);}
    private static void Hand(FarmGame g,params int[] cards){g.C.Hand=cards.ToList();g.C.Deck=cards.ToList();g.C.Draw.Clear();g.C.Discard.Clear();g.C.Exhaust.Clear();g.C.EnemyHp=g.C.EnemyMax=200;}
    public static string Run(string root)
    {
        var g=New();int berries=g.S.Berries,xp=g.S.Xp[0];Check(g.StartCards(false),"explicit start");Check(!g.StartCards(false),"duplicate start rejected");
        Check(g.C.Hand.SequenceEqual(new[]{0,1,2,3})&&g.C.Energy==3,"first hand demonstrates combo and defense");
        int rev=g.C.Revision;Check(g.PlayCard(0,rev)&&g.C.EnemyHp==20&&g.C.Wet==2,"water applies real damage and wet");Check(!g.PlayCard(0,rev),"stale double click rejected");
        Check(g.PlayCard(0,g.C.Revision)&&g.C.EnemyHp==9&&g.C.Combos==1,"wet spark gives 11 damage");
        Check(g.PlayCard(0,g.C.Revision)&&g.C.Block==7&&g.C.Energy==0,"shield consumes final energy");
        string before=FarmPracticeTests.Xml(g.C);Check(!g.PlayCard(0,g.C.Revision)&&before==FarmPracticeTests.Xml(g.C),"unaffordable card does not mutate");
        Check(g.EndCardTurn(g.C.Revision)&&g.C.Hp==60&&g.C.Block==0&&g.C.Energy==3&&g.C.Turn==2&&g.C.Wet==1,"enemy turn blocks then resets shield, draws and reduces wet");
        Check(g.S.Berries==berries&&g.S.Xp[0]==xp,"card actions do not spend farm inventory or repeat feeding");
        var reflect=New();reflect.StartCards(false);Hand(reflect,5,7,9,8);reflect.C.Hp=40;
        reflect.PlayCard(0,reflect.C.Revision);reflect.PlayCard(0,reflect.C.Revision);Check(reflect.C.EnemyHp==192&&reflect.C.Block==5,"shield slam uses and retains shield");
        reflect.PlayCard(0,reflect.C.Revision);Check(reflect.C.Exhaust.Contains(9)&&reflect.C.Energy==2,"breath exhausts and grants energy");
        reflect.PlayCard(0,reflect.C.Revision);Check(reflect.C.Hp==45&&reflect.C.Exhaust.Contains(8),"healing and exhaust");
        reflect.EndCardTurn(reflect.C.Revision);Check(reflect.C.EnemyHp==188&&reflect.C.Hp==45,"reflection damages without requiring health loss");
        for(int id=0;id<MossCards.All.Length;id++)foreach(int up in new[]{0,100}){var all=New();all.StartCards(false);Hand(all,id+up);all.C.Hp=30;all.C.Wet=2;all.C.Block=8;Check(all.PlayCard(0,all.C.Revision),"every base and upgraded card usable");all.ValidateCards();}
        var round=New();round.StartCards(false);for(int turn=0;turn<3;turn++){round.C.EnemyHp=999;round.C.EnemyMax=999;round.C.Hp=60;round.EndCardTurn(round.C.Revision);Check(round.C.Hand.Count==4,"draw refills from shuffled discard");round.ValidateCards();}
        var saved=new FarmGame(Path.Combine(root,"cards-save.xml"));saved.StartCards(false);saved.PlayCard(0,saved.C.Revision);string snapshot=FarmPracticeTests.Xml(saved.C);saved.Save();
        var load=new FarmGame(saved.SavePath);Check(FarmPracticeTests.Xml(load.C)==snapshot,"all card piles, random state and next intent survive reload");
        saved.EndCardTurn(saved.C.Revision);load.EndCardTurn(load.C.Revision);Check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(load.C),"identical next turn after reload");
        string old=Path.Combine(root,"cards-legacy.xml");var doc=System.Xml.Linq.XDocument.Load(load.SavePath);doc.Root.Element("Cards").Remove();doc.Save(old);var legacy=new FarmGame(old);Check(legacy.C.Phase=="home"&&legacy.S.Berries==load.S.Berries,"3.6 save adds idle cards while preserving inventory");
        var reward=New();reward.StartCards(false);reward.C.EnemyHp=1;reward.PlayCard(0,reward.C.Revision);Check(reward.C.Phase=="reward"&&reward.C.Offers.Distinct().Count()==3,"battle win yields three stable distinct choices");rev=reward.C.Revision;int count=reward.C.Deck.Count;reward.PickCardReward(0,rev);Check(reward.C.Deck.Count==count+1&&!reward.PickCardReward(0,rev),"one reward only");
        var abandon=New();abandon.StartCards(false);rev=abandon.C.Revision;Check(abandon.RetireCards(rev)&&!abandon.RetireCards(rev)&&abandon.C.CompletedRuns==1&&abandon.C.TotalWins==0,"retreat exactly once and no victory reward");
        int wins=0,assistWins=0,minTurns=999,maxTurns=0;var summaries=new List<string>();
        for(int seed=1;seed<=40;seed++)foreach(bool assist in new[]{false,true}){
            var sim=New();sim.C.Seed=seed*7919;sim.StartCards(assist);int totalTurns=0,actions=0;
            while(sim.C.Phase!="result"&&actions++<350){
                var r=sim.C;
                if(r.Phase=="battle"){
                    int choice=BestCard(sim);if(choice>=0)sim.PlayCard(choice,r.Revision);else{sim.EndCardTurn(r.Revision);totalTurns++;}
                }else if(r.Phase=="reward"){int pick=r.Offers.Select((card,index)=>new{card,index}).OrderByDescending(x=>RewardScore(x.card)).First().index;sim.PickCardReward(pick,r.Revision);}
                else if(r.Phase=="branch")sim.PickShieldBranch(seed%3,r.Revision);
                else if(r.Phase=="route")sim.ChooseCardRoute(seed%3==0,r.Revision);
                else if(r.Phase=="camp")sim.CardCamp(0,-1,r.Revision);
                sim.ValidateCards();
            }
            Check(sim.C.Phase=="result"&&actions<350,"all seeded runs terminate without softlocks");
            if(sim.C.Victories==5){if(assist)assistWins++;else wins++;}minTurns=Math.Min(minTurns,totalTurns);maxTurns=Math.Max(maxTurns,totalTurns);
            summaries.Add(seed+","+assist+","+sim.C.Victories+","+sim.C.Hp+","+totalTurns+","+actions);
        }
        Check(wins>0&&assistWins>0,"normal and assisted complete runs are possible");
        File.WriteAllLines(Path.Combine(root,"cards-simulation.csv"),new[]{"seed,assisted,victories,hp,end_turns,actions"}.Concat(summaries));
        File.WriteAllText(Path.Combine(root,"cards-balance.txt"),"Heuristic bot only, not human testing. Normal "+wins+"/40 wins; assisted "+assistWins+"/40; end-turn range "+minTurns+"–"+maxTurns+". Playtime and enjoyment unverified.");
        return "PASS: starter combo; energy; enemy intent; shields, reflection, healing, draw, exhaust and all 30 base/upgraded cards; stale/repeated actions; reward once; retreat once; seeded full runs; isolated save exact roundtrip and deterministic continuation; 3.6 migration; farm inventory unchanged.";
    }
    private static int RewardScore(int card){int[] score={4,8,6,4,7,6,5,9,7,8,10,9,7,3,4};return score[MossCards.Id(card)]+(MossCards.Up(card)?4:0);}
    internal static int BestCard(FarmGame g)
    {
        int best=-1;double value=-999;var r=g.C;
        for(int i=0;i<r.Hand.Count;i++){
            int card=r.Hand[i],id=MossCards.Id(card);if(MossCards.All[id].Cost>r.Energy)continue;double score=1;
            if(id==0)score=r.Wet==0?14:5;else if(id==1)score=r.Wet>0?16:4;else if(id==2||id==5||id==12||id==14)score=r.Block<g.CardIntentDamage*g.CardIntentHits?(id==5?13:11):-2;else if(id==7)score=r.Block+4;else if(id==9)score=19;else if(id==8)score=r.MaxHp-r.Hp>=5?10:-4;else if(id==10||id==11)score=r.Wet>0?22:8;else if(id==6)score=r.Wet==0?12:2;else score=7;
            if(MossCards.Up(card))score+=2;if(score>value){value=score;best=i;}
        }
        return value<0?-1:best;
    }
}
public partial class FarmPanel
{
    internal void RenderCardChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{
            G.S=new FarmSave();Tab="卡牌冒险";
            for(int size=0;size<3;size++){
                G.S=new FarmSave{ReadingSize=size};cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
                CardShot(folder,"lobby",size);ClickCard("cards-start");check(G.C.Phase=="battle","desktop card entry starts a battle without feeding");
                CardShot(folder,"battle",size);CardVisible("cards-hand-0");CardVisible("cards-hand-3");CardVisible("cards-end");
                var stale=CardFind("cards-hand-0");ClickCard("cards-hand-0");int energy=G.C.Energy;stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.C.Energy==energy,"removed card control cannot play twice");
                ClickCard("cards-hand-0");check(G.C.Combos==1,"rendered card buttons trigger combo");CardShot(folder,"combo",size);
                string stable=FarmPracticeTests.Xml(G.C);ClickCard("cards-deck");CardShot(folder,"deck",size);CardBack();ClickCard("cards-rules");CardBack();ClickCard("cards-leave");CardShot(folder,"cancel",size);ClickCard("cards-cancel-retire");
                Pulse(true,false);Pulse(false,false);check(stable==FarmPracticeTests.Xml(G.C),"view, cancel, background and pulses never advance cards or spend");
                G.C.EnemyHp=1;G.C.Energy=3;int attack=G.C.Hand.FindIndex(c=>MossCards.Id(c)==3);if(attack<0){G.C.Hand.Add(3);G.C.Deck.Add(3);attack=G.C.Hand.Count-1;}G.PlayCard(attack,G.C.Revision);
                CardShot(folder,"reward",size);CardVisible("cards-reward-0");CardVisible("cards-skip");ClickCard("cards-reward-0");if(G.C.Phase=="branch")ClickCard("cards-branch-skip");CardShot(folder,"route",size);ClickCard("cards-route-safe");
                G.C.Phase="camp";G.C.Stage=2;G.C.Hp=32;CardShot(folder,"camp",size);stable=FarmPracticeTests.Xml(G.C);ClickCard("cards-upgrade");CardShot(folder,"upgrade",size);ClickCard("cards-overlay-back");check(stable==FarmPracticeTests.Xml(G.C),"upgrade preview cancellation is free");
                ClickCard("cards-upgrade");ClickCard("cards-upgrade-0");check(G.C.Deck[0]==100&&G.C.Phase=="battle"&&G.C.Hp==32,"explicit upgrade consumes camp choice without healing");
                G.C.Stage=4;G.C.EnemyHp=G.C.EnemyMax=86;CardShot(folder,"boss",size);
                G.C.Hp=1;G.C.Block=0;G.EndCardTurn(G.C.Revision);CardShot(folder,"defeat",size);check(G.C.Phase=="result","defeat has a replay path");CardVisible("cards-again");ClickCard("cards-again");
                G.C.Stage=4;G.C.Victories=4;G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);CardShot(folder,"victory",size);check(G.C.TotalWins==1&&G.C.CompletedRuns==2,"victory receipt increments exactly once");
                Render();check(HandlePanelKey(Key.Tab,ModifierKeys.None,false,null),"card buttons keyboard traversable");
            }
            File.WriteAllText(Path.Combine(folder,"cards-ui-regression.txt"),"PASS: actual controls start, play combo, reject stale clicks, draft, route, preview/cancel/confirm upgrade, defeat/replay/victory; 100/115/130% at 764x680 content; default hand and next actions inside actual scroll viewport; full-turn preservation across browsing and background pulses; keyboard Tab path. No native window or player save.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;}
    }
    private Button CardFind(string id){return LogicalNodes((DependencyObject)Content).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)==id);}
    private void ClickCard(string id){CardFind(id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    private void CardShot(string folder,string state,int scale)
    {
        Render();FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-"+state+"-"+scale+".png"),764,680);
        if(scale==0){var nodes=LogicalNodes((DependencyObject)Content).ToList();var text=nodes.OfType<TextBlock>().Select(t=>t.Text).Concat(nodes.OfType<Button>().Where(b=>b.Content is string).Select(b=>(string)b.Content));File.WriteAllLines(Path.Combine(folder,"cards-copy-"+state+".txt"),text);}
    }
    private void CardVisible(string id)
    {
        var b=CardFind(id);var content=(FrameworkElement)Content;var r=b.TransformToAncestor(content).TransformBounds(new Rect(0,0,b.ActualWidth,b.ActualHeight));
        FarmHeadlessTests.Check(r.Top>=0&&r.Bottom<=680&&r.Left>=0&&r.Right<=764&&b.ActualHeight>20,"card primary visible: "+id);
        DependencyObject parent=VisualTreeHelper.GetParent(b);while(parent!=null&&parent!=content){var viewport=parent as ScrollContentPresenter;if(viewport!=null){var bounds=b.TransformToAncestor(viewport).TransformBounds(new Rect(0,0,b.ActualWidth,b.ActualHeight));FarmHeadlessTests.Check(bounds.Top>=0&&bounds.Bottom<=viewport.ActualHeight+.5,"card primary inside viewport: "+id);}parent=VisualTreeHelper.GetParent(parent);}
    }
}

