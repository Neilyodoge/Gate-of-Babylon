using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;

public static class FarmCardForecastTests
{
    private static void Check(bool ok,string name){FarmHeadlessTests.Check(ok,"forecast: "+name);}
    private static FarmGame New(){var g=new FarmGame(null,null,true);g.StartCards(false);return g;}
    public static string Run(string folder)
    {
        var g=New();string before=FarmPracticeTests.Xml(g.S);var f=g.ForecastCards();
        Check(f.Hp==55&&f.Loss==5&&f.Playable,"default turn predicts five damage");
        f=g.ForecastCards(2);Check(f.Hp==60&&f.Loss==0&&f.Blocked==5,"shield preview prevents damage");
        for(int i=0;i<20;i++){g.ForecastCards();for(int j=0;j<4;j++)g.ForecastCards(j);}
        Check(FarmPracticeTests.Xml(g.S)==before,"repeated previews preserve full save and random seed");
        var clone=g.CardPreviewClone();
        foreach(var field in typeof(MossRun).GetFields().Where(x=>x.FieldType.IsGenericType&&x.FieldType.GetGenericTypeDefinition()==typeof(System.Collections.Generic.List<>)))
            Check(!Object.ReferenceEquals(field.GetValue(g.C),field.GetValue(clone.C)),"every mutable card list is independent");
        g.C.Stage=4;g.C.Turn=2;g.C.Block=7;f=g.ForecastCards();Check(f.Loss==5&&f.Blocked==7,"shield covers total across two hits");
        g.C.Block=10;g.C.Reflect=4;g.C.EnemyHp=4;f=g.ForecastCards();Check(f.Won&&f.Blocked==6&&f.Loss==0,"counter kill cancels second enemy hit");
        g=New();g.C.ShieldBranch=2;g.C.EnemyHp=8;f=g.ForecastCards(2);Check(f.Won&&f.Loss==1&&f.Hp==59,"fragile shield takes one and wins by counter");
        g=New();g.C.Stage=4;g.C.Turn=3;g.C.BossDamage=7;g.C.EnemyHp=g.C.EnemyMax=86;f=g.ForecastCards(1);Check(f.Loss==0&&g.ForecastCards().Loss==24,"card interrupts heavy attack in preview");
        g.C.Stage=1;g.C.Turn=3;Check(g.ForecastCards().Loss==0,"rest is safe");
        g.C.RulesVersion=0;Check(g.ForecastCards().Loss>0,"legacy run keeps legacy attack schedule");
        g=New();g.C.Hp=1;Check(g.ForecastCards().Defeat,"lethal turn is identified");
        g.C.EnemyHp=1;f=g.ForecastCards(0);Check(f.CardWon&&f.Won&&!f.Defeat&&f.Hp==1,"direct kill does not simulate another attack");
        g=New();g.C.Energy=0;before=FarmPracticeTests.Xml(g.S);Check(!g.ForecastCards(0).Playable&&!g.ForecastCards(99).Playable&&FarmPracticeTests.Xml(g.S)==before,"invalid or unaffordable preview stays inert");
        int cases=0;
        for(int stage=0;stage<5;stage++)for(int turn=1;turn<=3;turn++)for(int branch=0;branch<3;branch++)for(int slot=-1;slot<4;slot++){
            g=New();g.C.Stage=stage;g.C.Turn=turn;g.C.ShieldBranch=branch;g.C.Block=branch*3;g.C.Wet=turn%2;g.C.EnemyBlock=turn;g.C.EnemyHp=g.C.EnemyMax=86;g.C.Seed=stage*731+turn*37+branch+1;
            before=FarmPracticeTests.Xml(g.S);f=g.ForecastCards(slot);Check(before==FarmPracticeTests.Xml(g.S),"prediction never mutates live source");
            if(slot>=0)g.PlayCard(slot,g.C.Revision);int hp=g.C.Hp;
            if(g.C.Phase=="battle")g.EndCardTurn(g.C.Revision);
            Check(f.Hp==g.C.Hp&&f.EnemyHp==g.C.EnemyHp&&f.Loss==hp-g.C.Hp,"predicted results match real committed actions");cases++;
        }
        var saved=new FarmGame(Path.Combine(folder,"forecast-isolated.xml"));saved.StartCards(false);string disk=File.ReadAllText(saved.SavePath);saved.ForecastCards(2);Check(disk==File.ReadAllText(saved.SavePath),"normal save preview never writes");
        return "PASS: "+cases+" predictions versus actual actions; full save and RNG unchanged; all lists isolated; shield/multihit/retaliation/fragile/interrupt/rest/legacy/lethal/direct kill; invalid and unaffordable actions; isolated normal save remains unchanged.";
    }
}
public partial class FarmPanel
{
    internal void RenderCardForecastChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            CardShot(folder,"forecast-danger",size);ShowCardForecast(-1,cardViewRevision);
            check(cardForecastText.Text.Contains("5 体力"),"default forecast visible");
            var content=(FrameworkElement)Content;var bounds=cardForecastText.TransformToAncestor(content).TransformBounds(new Rect(0,0,cardForecastText.ActualWidth,cardForecastText.ActualHeight));
            check(bounds.Top>=0&&bounds.Bottom<=680&&bounds.Right<=764,"forecast inside minimum window");CardVisible("cards-hand-3");CardVisible("cards-end");
            string before=FarmPracticeTests.Xml(G.S);var button=CardFind("cards-hand-2");DependencyObject node=button;while(!(node is Border&&((Border)node).Child is DockPanel))node=VisualTreeHelper.GetParent(node);var tile=(Border)node;
            tile.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});
            check(cardForecastText.Text.Contains("不掉体力"),"hover shield predicts protection");ForecastShot(folder,"forecast-shield",size);
            tile.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent});
            check(cardForecastText.Text.Contains("5 体力"),"leaving restores default");
            button.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,null,button){RoutedEvent=Keyboard.GotKeyboardFocusEvent});
            check(cardForecastText.Text.Contains("不掉体力"),"keyboard focus previews shield");
            check(FarmPracticeTests.Xml(G.S)==before,"hover and focus do not spend, reroll or save");
            var stale=tile;ClickCard("cards-hand-2");check(G.C.Block==7&&G.C.Energy==2,"explicit click plays once");
            ShowCardForecast(-1,cardViewRevision);string shown=cardForecastText.Text;stale.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});check(cardForecastText.Text==shown,"stale tile cannot change current forecast");
            CardShot(folder,"forecast-protected",size);CardVisible("cards-end");for(int i=0;i<G.C.Hand.Count;i++)CardVisible("cards-hand-"+i);
            before=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");CardBack();check(FarmPracticeTests.Xml(G.S)==before,"browse and return remain free");
            G.C.Hp=1;G.C.Block=0;CardShot(folder,"forecast-lethal",size);check(cardForecastText.Text.Contains("败退"),"lethal warning");
            G.C.EnemyHp=1;Render();ShowCardForecast(0,cardViewRevision);check(cardForecastText.Text.Contains("击败对手"),"kill preview supersedes end-turn warning");ForecastShot(folder,"forecast-kill",size);
            G.C.Energy=0;Render();ShowCardForecast(0,cardViewRevision);check(cardForecastText.Text.Contains("能量不足"),"no misleading unaffordable outcome");
        }
        File.WriteAllText(Path.Combine(folder,"cards-forecast-ui.txt"),"PASS: default loss, hover and keyboard-focus shield preview, reset on leaving, no save or random mutation, stale tile rejection, explicit click only, lethal/kill/unaffordable outcomes; three font scales at 764x680 and actual hand/end-turn viewport. Detached WPF; physical hover and focus not tested.");
        }finally{G.S=original;Tab=tab;ClearCardFeedback();cardForecastText=null;cardDefaultForecast=null;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;}
    }
    private void ForecastShot(string folder,string state,int scale)
    {
        FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-"+state+"-"+scale+".png"),764,680);
    }
}
