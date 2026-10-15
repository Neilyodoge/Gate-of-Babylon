using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Globalization;

public partial class FarmPanel
{
    private void StatusFixture(int size,int branch,int stage,int turn,params int[] cards)
    {
        G.S=new FarmSave{ReadingSize=size};G.StartCards(false);G.C.CompletedRuns=1;G.C.ShieldBranch=branch;G.C.BranchChosen=branch>0;G.C.Stage=stage;G.C.Turn=turn;
        G.C.Hand=cards.ToList();G.C.Draw=new[]{0,1,2,3}.ToList();G.C.Discard.Clear();G.C.Exhaust.Clear();G.C.Deck=G.C.Hand.Concat(G.C.Draw).ToList();G.C.EnemyHp=G.C.EnemyMax=200;ClearCardFeedback();Render();G.ValidateCards();
    }
    private void StatusShot(string folder,string name,int size)
    {
        CardShot(folder,name,size);var note=LogicalNodes((DependencyObject)Content).OfType<TextBlock>().First(t=>AutomationProperties.GetAutomationId(t)=="cards-battle-note");
        var text=new FormattedText(note.Text,CultureInfo.GetCultureInfo("zh-CN"),note.FlowDirection,new Typeface(note.FontFamily,note.FontStyle,note.FontWeight,note.FontStretch),note.FontSize,note.Foreground,VisualTreeHelper.GetDpi(note).PixelsPerDip);text.MaxTextWidth=note.ActualWidth;
        FarmHeadlessTests.Check(text.Height<=note.ActualHeight+1,"all current effect and message lines fit without clipping");
        CardVisible("cards-end");for(int i=0;i<Math.Min(4,G.C.Hand.Count);i++)CardVisible("cards-hand-"+i);
    }
    internal void RenderCardStatusChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
            for(int size=0;size<3;size++){
                StatusFixture(size,1,0,1,2,3,0,1);ClickCard("cards-hand-0");ClickCard("cards-hand-0");check(G.C.KeepShield&&G.CardEffectSummary().Contains("余盾保留 1 次")&&!G.C.Message.Contains("保留"),"carry remains visible after another card replaces receipt");StatusShot(folder,"status-carry",size);
                string state=FarmPracticeTests.Xml(G.S);G.CardEffectSummary();G.ForecastCards();ClickCard("cards-deck");CardBack();check(state==FarmPracticeTests.Xml(G.S),"viewing effects and browse do not alter state or RNG");
                ClickCard("cards-end");check(G.C.Block==2&&!G.C.KeepShield&&!G.CardEffectSummary().Contains("保留"),"carried shield is not advertised as pending a second carry");StatusShot(folder,"status-carry-used",size);
                StatusFixture(size,2,0,1,2,3,0,1);ClickCard("cards-hand-0");var stale=CardFind("cards-hand-0");ClickCard("cards-hand-0");check(G.CardEffectSummary().Contains("破盾后反击 8，下回合多抽 1"),"pending fragile effect remains visible");StatusShot(folder,"status-fragile",size);state=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"old button cannot recommit while effects shown");
                ClickCard("cards-end");check(G.C.BranchTriggers==1&&G.C.Hand.Count==5&&G.CardEffectSummary()=="","triggered fragile effect clears and extra draw is real");StatusShot(folder,"status-fragile-used",size);
                StatusFixture(size,0,1,3,12,3,0,1);ClickCard("cards-hand-0");ClickCard("cards-hand-0");ClickCard("cards-end");check(G.C.Weak==1&&G.CardEffectSummary().Contains("下次攻击 −3 / 段"),"weakening survives enemy rest");StatusShot(folder,"status-waiting",size);ClickCard("cards-end");check(G.C.Weak==0&&G.CardEffectSummary()=="","weakening clears after actual attack");
                StatusFixture(size,0,1,3,12,3,0,1);G.C.RulesVersion=0;ClickCard("cards-hand-0");check(G.CardEffectSummary().Contains("本回合"),"legacy weakening uses legacy duration");ClickCard("cards-end");check(G.CardEffectSummary()=="","legacy weakening expires under original rules");
                StatusFixture(size,2,4,3,2,5,12,0,1,2,3);ClickCard("cards-hand-0");ClickCard("cards-hand-0");ClickCard("cards-hand-0");check(G.C.Hand.Count==4&&G.CardEffectSummary().Contains("受击反击 4 / 次")&&G.CardEffectSummary().Contains("攻击 −3 / 段"),"multiple effects reflect actual simultaneous state");StatusShot(folder,"status-combined",size);state=FarmPracticeTests.Xml(G.S);G.ForecastCards();PauseForBackground();Pulse(false,false);check(state==FarmPracticeTests.Xml(G.S),"background and prediction leave pending effects unchanged");
                G.RetireCards(G.C.Revision);check(G.CardEffectSummary()=="","result hides obsolete battle flags");G.StartCards(false);check(G.CardEffectSummary()=="","new run has no stale effects");
                StatusFixture(size,0,0,1,5,105,3,0);ClickCard("cards-hand-0");ClickCard("cards-hand-0");check(G.CardEffectSummary().Contains("受击反击 11 / 次"),"stacked reflect reports actual accumulated amount");G.S.DesktopMotion=false;StatusShot(folder,"status-reflect",size);
            }
            var saved=new FarmGame(Path.Combine(folder,"status-isolated-"+Guid.NewGuid().ToString("N")+".xml"));saved.StartCards(false);saved.C.ShieldBranch=1;saved.C.BranchChosen=true;saved.PlayCard(2,saved.C.Revision);saved.PlayCard(0,saved.C.Revision);var load=new FarmGame(saved.SavePath);string disk=File.ReadAllText(saved.SavePath);check(load.CardEffectSummary()==saved.CardEffectSummary()&&load.CardEffectSummary().Contains("保留"),"pending effects derived from isolated reload");load.CardEffectSummary();check(disk==File.ReadAllText(saved.SavePath),"summary never writes save");saved.EndCardTurn(saved.C.Revision);load.EndCardTurn(load.C.Revision);check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(load.C),"reload continues same real effect resolution");
            File.WriteAllText(Path.Combine(folder,"cards-status-regression.txt"),"PASS: carry/fragile visibility after later cards; actual resolution and expiry; weakening across rest and legacy duration; combined effects with four visible cards; accumulated reflect; no stale effects after result/new run; browse/preview/background preserve full state and RNG; stale click; isolated save and deterministic continuation; three font scales at 764x680 with unclipped notes and visible first hand row/end button. Detached WPF only.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
