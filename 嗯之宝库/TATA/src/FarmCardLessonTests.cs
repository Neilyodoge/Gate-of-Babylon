using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private void AssertLessonTarget()
    {
        var lesson=G.CardFirstTurnLesson();if(!lesson.Active)return;
        var actions=LogicalNodes((DependencyObject)Content).OfType<Button>().Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("cards-hand-")||AutomationProperties.GetAutomationId(b)=="cards-end").ToArray();
        FarmHeadlessTests.Check(actions.Count(b=>Object.ReferenceEquals(b.Background,U.Green))==1,"one first-turn primary action");
        var target=CardFind(lesson.End?"cards-end":"cards-hand-"+lesson.Slot);
        FarmHeadlessTests.Check(target.IsEnabled&&Object.ReferenceEquals(target.Background,U.Green),"hint and enabled primary action match");
    }
    internal void RenderCardLessonChecks(string folder)
    {
        var copies=new System.Collections.Generic.List<string>();var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            CardShot(folder,"lesson-start",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(G.CardFirstTurnLesson().Slot==0,"initial water target");
            ClickCard("cards-hand-2");CardShot(folder,"lesson-shield-first",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(G.CardFirstTurnLesson().Slot==0,"shield-first can still teach water");
            ClickCard("cards-hand-1");CardShot(folder,"lesson-detour",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(!G.CardLessonMessage(G.CardFirstTurnLesson()).Contains("可用泡泡盾")&&!G.CardFirstTurnLesson().Text.Contains("泡泡盾"),"used shield is never suggested");
            string before=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");CardBack();check(before==FarmPracticeTests.Xml(G.S),"browsing never advances first turn lesson");
            int slot=G.CardFirstTurnLesson().Slot;ClickCard("cards-hand-"+slot);CardShot(folder,"lesson-end",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(G.CardFirstTurnLesson().End,"no payable card points at end turn");CardVisible("cards-end");
            ClickCard("cards-end");CardShot(folder,"lesson-next-turn",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));check(!G.CardFirstTurnLesson().Active,"next turn does not restart onboarding");
            G.C.CompletedRuns=0;G.C.Phase="result";G.StartCards(false);Render();ClickCard("cards-hand-0");CardShot(folder,"lesson-wet",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(G.CardFirstTurnLesson().Text.Contains("连招"),"water teaches current electric combo");
            ClickCard("cards-hand-0");CardShot(folder,"lesson-guard",size);if(size==0&&G.CardFirstTurnLesson().Active)copies.Add(G.CardLessonMessage(G.CardFirstTurnLesson()));AssertLessonTarget();check(MossCards.Id(G.C.Hand[G.CardFirstTurnLesson().Slot])==2,"combo then shield uses actual slot");
            G.C.Energy=0;G.C.Hand.Insert(0,9);Render();check(!G.CardFirstTurnLesson().End&&G.CardFirstTurnLesson().Slot==0,"zero-cost card stays available at zero energy");
            G.C.Message="电花 · 已生效。 剩余 1 能量，可用泡泡盾。";before=FarmPracticeTests.Xml(G.S);check(!G.CardLessonMessage(G.CardFirstTurnLesson()).Contains("可用泡泡盾")&&before==FarmPracticeTests.Xml(G.S),"legacy saved hint removed only in presentation");
            Render();check(!LogicalNodes((DependencyObject)Content).OfType<TextBlock>().Any(t=>t.ToolTip!=null&&t.ToolTip.ToString().Contains("可用泡泡盾")),"legacy hint absent from tooltip too");
            G.C.CompletedRuns=1;check(!G.CardFirstTurnLesson().Active,"returning player not taught first turn");
        }
        int permutations=0;
        foreach(int a in new[]{0,1,2,3})foreach(int b in new[]{0,1,2,3}.Where(x=>x!=a))foreach(int c in new[]{0,1,2,3}.Where(x=>x!=a&&x!=b)){
            var g=new FarmGame(null,null,true);g.StartCards(false);
            foreach(int id in new[]{a,b,c}){
                string before=FarmPracticeTests.Xml(g.S);var lesson=g.CardFirstTurnLesson();check(before==FarmPracticeTests.Xml(g.S),"lesson evaluation never changes state");
                check(lesson.Slot>=0&&lesson.Slot<g.C.Hand.Count&&MossCards.All[MossCards.Id(g.C.Hand[lesson.Slot])].Cost<=g.C.Energy,"every detour recommendation is payable and present");
                g.PlayCard(g.C.Hand.FindIndex(x=>MossCards.Id(x)==id),g.C.Revision);
            }
            check(g.CardFirstTurnLesson().End,"each three-card ordering ends with end-turn guidance");g.EndCardTurn(g.C.Revision);check(!g.CardFirstTurnLesson().Active,"early lesson ends after player ends turn");permutations++;
        }
        var saved=new FarmGame(Path.Combine(folder,"lesson-isolated.xml"));saved.StartCards(false);saved.PlayCard(2,saved.C.Revision);saved.PlayCard(1,saved.C.Revision);var reload=new FarmGame(saved.SavePath);
        string disk=File.ReadAllText(saved.SavePath);check(saved.CardFirstTurnLesson().Text==reload.CardFirstTurnLesson().Text,"reload derives same current guidance");reload.CardFirstTurnLesson();check(File.ReadAllText(saved.SavePath)==disk,"guidance does not write normal isolated save");
        File.WriteAllLines(Path.Combine(folder,"cards-lesson-copy.txt"),copies.Distinct().ToArray());
        File.WriteAllText(Path.Combine(folder,"cards-lesson-regression.txt"),"PASS: "+permutations+" first-three-card orderings; only payable present targets; shield-first/electric-first detours; one matching primary action; zero-cost at zero energy; next-turn and returning-player limits; legacy hint presentation cleanup; no browse/save/RNG mutation; isolated reload; three scales at 764x680. Detached WPF only.");
        }finally{G.S=original;Tab=tab;cardLesson=null;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
