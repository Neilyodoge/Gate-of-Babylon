using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class FarmPanel
{
    internal void RenderCardBrowseChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            G.C.Deck.Clear();for(int i=0;i<15;i++){G.C.Deck.Add(i);G.C.Deck.Add(i+100);}Render();ClickCard("cards-deck");CardShot(folder,"browse-start",size);
            cardBrowseScroll.ScrollToVerticalOffset(250);((FrameworkElement)Content).UpdateLayout();double offset=cardBrowseScroll.VerticalOffset;check(offset>100,"long deck scrolled");string before=FarmPracticeTests.Xml(G.S);
            ClickCard("cards-pile-2");CardShot(folder,"browse-draw",size);check(cardBrowseScroll.VerticalOffset==0,"new pile starts at top");ClickCard("cards-pile-0");CardShot(folder,"browse-restored",size);check(Math.Abs(cardBrowseScroll.VerticalOffset-offset)<1,"pile switching restores position");
            CardBack();ClickCard("cards-deck");CardShot(folder,"browse-reopened",size);check(Math.Abs(cardBrowseScroll.VerticalOffset-offset)<1&&before==FarmPracticeTests.Xml(G.S),"return and reopen restore position without save changes");
            G.C.Revision++;Render();FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-browse-reset-"+size+".png"),764,680);check(cardBrowseScroll.VerticalOffset==0,"changed gameplay revision resets obsolete position");CardBack();
            G.C.Phase="camp";G.C.Stage=2;G.C.Hp=32;Render();ClickCard("cards-upgrade");CardShot(folder,"browse-upgrade",size);
            HandlePanelKey(Key.End,ModifierKeys.None,false,null);((FrameworkElement)Content).UpdateLayout();check(cardBrowseScroll.VerticalOffset>0,"keyboard reaches upgrade list end");CardVisible("cards-overlay-back");CardVisible("cards-upgrade-28");
            before=FarmPracticeTests.Xml(G.S);offset=cardBrowseScroll.VerticalOffset;ClickCard("cards-overlay-back");check(before==FarmPracticeTests.Xml(G.S)&&G.C.Phase=="camp","return to camp does not consume choice");ClickCard("cards-upgrade");CardShot(folder,"browse-upgrade-end",size);check(Math.Abs(cardBrowseScroll.VerticalOffset-Math.Min(offset,cardBrowseScroll.ScrollableHeight))<1,"reopening upgrade restores its own position: expected "+offset+", actual "+cardBrowseScroll.VerticalOffset+", max "+cardBrowseScroll.ScrollableHeight);CardVisible("cards-overlay-back");
            int card=G.C.Deck[28],hp=G.C.Hp;var stale=CardFind("cards-upgrade-28");ClickCard("cards-upgrade-28");stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.C.Deck[28]==card+100&&G.C.Hp==hp&&G.C.Phase=="battle","chosen lower-list copy upgrades exactly once without healing");
            ClickCard("cards-deck");CardShot(folder,"browse-next-battle",size);check(cardBrowseScroll.VerticalOffset==0,"new battle resets old browsing position");CardBack();
        }
        File.WriteAllText(Path.Combine(folder,"cards-browse-regression.txt"),"PASS: per-pile and upgrade scroll restoration across switching/return/reopen; full save and RNG unchanged by browsing; revision/new battle reset; keyboard reaches long upgrade list end; return remains in viewport; camp cancellation preserves choice; exact lower-list copy upgrades once; stale duplicate click rejected; three scales at 764x680. Memory-only positions and detached WPF.");
        }finally{G.S=original;Tab=tab;cardBrowseScroll=null;cardBrowseOffsets.Clear();cardBrowseRun=null;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
