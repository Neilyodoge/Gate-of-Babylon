using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public partial class FarmPanel
{
    private void CampFixture(int size,int hp,bool allUp=false)
    {
        G.S=new FarmSave{ReadingSize=size};G.StartCards(false);G.C.Phase="camp";G.C.Stage=2;G.C.Hp=hp;
        G.C.Deck=new[]{0,0,101,2}.Select(c=>allUp&&!MossCards.Up(c)?c+100:c).ToList();G.ValidateCards();ClearCardFeedback();Render();
    }
    private bool CampPrimary(string id){return CardFind(id).Background.ToString()==U.Green.ToString();}
    private void CampShot(string folder,string name,int size){CardShot(folder,name,size);CardVisible("cards-rest");CardVisible("cards-upgrade");}
    internal void RenderCardCampChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
            for(int size=0;size<3;size++){
                CampFixture(size,60);check(CampPrimary("cards-upgrade")&&!CampPrimary("cards-rest"),"full health highlights only read-only upgrade browsing");
                check((string)CardFind("cards-upgrade").Content=="查看可强化的 3 张牌"&&(string)CardFind("cards-rest").Content=="体力已满 · 直接继续","counts actual upgradeable copies, not distinct names");CampShot(folder,"camp-full",size);
                string state=FarmPracticeTests.Xml(G.S);var stale=CardFind("cards-rest");ClickCard("cards-upgrade");check(G.C.Phase=="camp"&&state==FarmPracticeTests.Xml(G.S),"highlighted browse does not upgrade or depart");CardShot(folder,"camp-preview",size);CardVisible("cards-overlay-back");ClickCard("cards-overlay-back");stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"return and obsolete continue preserve full camp state and RNG");
                ClickCard("cards-deck");CardBack();check(state==FarmPracticeTests.Xml(G.S),"deck browse and cancel preserve camp opportunity");CampShot(folder,"camp-return",size);
                ClickCard("cards-upgrade");var oldUpgrade=CardFind("cards-upgrade-0");ClickCard("cards-upgrade-0");check(G.C.Phase=="battle"&&G.C.Deck[0]==100&&G.C.Deck[1]==0&&G.C.Hp==60,"explicit upgrade changes only chosen copy and departs");state=FarmPracticeTests.Xml(G.S);oldUpgrade.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"old upgrade cannot commit again");
                CampFixture(size,59);check(CampPrimary("cards-rest")&&!CampPrimary("cards-upgrade")&&((string)CardFind("cards-rest").Content).Contains("恢复 1 体力"),"injured state retains real capped healing");CampShot(folder,"camp-partial",size);ClickCard("cards-rest");check(G.C.Hp==60&&G.C.Deck[0]==0&&G.C.Phase=="battle","rest heals actual one without upgrading");
                CampFixture(size,32);check(((string)CardFind("cards-rest").Content).Contains("恢复 16 体力"),"large injury shows the existing heal cap");CampShot(folder,"camp-hurt",size);
                CampFixture(size,60,true);check(!CardFind("cards-upgrade").IsEnabled&&CampPrimary("cards-rest")&&(string)CardFind("cards-upgrade").Content=="所有牌都已强化","all-upgraded boundary has one useful continuation");CampShot(folder,"camp-complete",size);state=FarmPracticeTests.Xml(G.S);PauseForBackground();Pulse(false,false);check(state==FarmPracticeTests.Xml(G.S),"background never leaves camp");
                var oldContinue=CardFind("cards-rest");ClickCard("cards-rest");check(G.C.Phase=="battle"&&G.C.Hp==60&&G.C.Deck.All(c=>MossCards.Up(c)),"full-health continuation neither grants healing nor changes cards");state=FarmPracticeTests.Xml(G.S);oldContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"continue cannot depart twice");
                CampFixture(size,60);ClickCard("cards-rest");check(G.C.Phase=="battle"&&G.C.Deck[0]==0,"player may explicitly continue without taking an upgrade");
                CampFixture(size,59);G.C.RulesVersion=0;ClickCard("cards-rest");check(G.C.RulesVersion==0&&G.C.Hp==60,"old run retains original rules after camp");
            }
            var saved=new FarmGame(Path.Combine(folder,"camp-isolated-"+Guid.NewGuid().ToString("N")+".xml"));saved.StartCards(false);saved.C.Phase="camp";saved.C.Stage=2;saved.Save();var loaded=new FarmGame(saved.SavePath);G.S=loaded.S;Render();string disk=File.ReadAllText(saved.SavePath);check(CampPrimary("cards-upgrade")&&((string)CardFind("cards-upgrade").Content).Contains("8 张"),"loaded full-health camp restores guidance from actual deck");ClickCard("cards-upgrade");CardBack();check(disk==File.ReadAllText(saved.SavePath),"browsing isolated loaded state does not write a save");saved.CardCamp(1,0,saved.C.Revision);loaded.CardCamp(1,0,loaded.C.Revision);check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(loaded.C),"saved and reloaded camp resolve the same chosen upgrade");
            File.WriteAllText(Path.Combine(folder,"cards-camp-regression.txt"),"PASS: full-health upgrade browsing primary; exact eligible copy count; partial and capped healing; all-upgraded boundary; explicit upgrade of one copy or optional direct continuation; no automatic choice; browse/back/deck/background preserve full state and RNG; stale continue/upgrade rejected; legacy rules; isolated normal reload and deterministic continuation; three font scales at 764x680 with actions visible. Detached WPF only, no player save or native window.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
