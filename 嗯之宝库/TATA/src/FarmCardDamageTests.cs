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
    private TextBlock DamageText(int slot)
    {
        return LogicalNodes((DependencyObject)Content).OfType<TextBlock>().First(t=>AutomationProperties.GetAutomationId(t)=="cards-hand-"+slot+"-effect");
    }
    private void DamageShot(string folder,string name,int size)
    {
        StatusShot(folder,name,size);
        for(int i=0;i<Math.Min(4,G.C.Hand.Count);i++){
            var t=DamageText(i);var text=new FormattedText(t.Text,CultureInfo.GetCultureInfo("zh-CN"),t.FlowDirection,new Typeface(t.FontFamily,t.FontStyle,t.FontWeight,t.FontStretch),t.FontSize,t.Foreground,VisualTreeHelper.GetDpi(t).PixelsPerDip);text.MaxTextWidth=t.ActualWidth;
            FarmHeadlessTests.Check(text.Height<=t.ActualHeight+1,"all hand damage and secondary effect text fits");
        }
    }
    internal void RenderCardDamageChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;
            for(int size=0;size<3;size++){
                StatusFixture(size,0,2,1,10,110,0,13);G.C.Message="";G.C.Wet=2;Render();
                check(G.PreviewCardHit(0).Damage==15&&G.PreviewCardHit(0).Blocked==5&&G.PreviewCardHit(1).Damage==21,"snail gains shield between normal and upgraded double hits");
                check(DamageText(0).Text.Contains("预计伤害 15（两段合计）")&&DamageText(0).Text.Contains("敌盾挡住 5")&&DamageText(0).Text.Contains("已计入潮湿"),"hand distinguishes actual total and blocked amount");DamageShot(folder,"damage-snail",size);
                var stale=CardFind("cards-hand-0");string state=FarmPracticeTests.Xml(G.S);ClickCard("cards-deck");CardBack();check(state==FarmPracticeTests.Xml(G.S),"browse cancel and rebuilding damage previews preserve full state and RNG");
                int before=G.C.EnemyHp;ClickCard("cards-hand-0");check(before-G.C.EnemyHp==15&&cardFeedback.Damage==15,"explicit attack matches hand preview and action feedback");state=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"stale damage tile cannot attack");
                StatusFixture(size,0,0,1,1,7,10,11);G.C.Message="";G.C.Wet=2;G.C.EnemyBlock=30;G.C.Block=7;G.C.Reflect=4;Render();
                check(DamageText(0).Text.Contains("预计伤害 0")&&DamageText(0).Text.Contains("敌盾挡住 11"),"full shield does not advertise damage to enemy HP");DamageShot(folder,"damage-blocked",size);
                StatusFixture(size,0,2,1,0,1,10,13);G.C.Message="";Render();check(DamageText(1).Text.Contains("预计伤害 5")&&DamageText(1).Text.Contains("潮湿时额外 +6")&&DamageText(2).Text.Contains("潮湿时每段 +4"),"dry cards retain how to activate their combo");ClickCard("cards-hand-0");check(G.C.EnemyBlock==5&&G.C.Wet==2&&DamageText(0).Text.Contains("预计伤害 6")&&DamageText(1).Text.Contains("预计伤害 15"),"damage refresh includes wet and new enemy shield after a real action");DamageShot(folder,"damage-refresh",size);
                StatusFixture(size,0,4,3,0,10,11,13);G.C.Message="";G.C.EnemyHp=3;Render();check(G.PreviewCardHit(0).Damage==3&&G.PreviewCardHit(1).Damage==3,"lethal preview caps at remaining HP");DamageShot(folder,"damage-lethal",size);
                G.C.Energy=0;Render();state=FarmPracticeTests.Xml(G.S);check(G.PreviewCardHit(0)==null&&!DamageText(0).Text.Contains("预计伤害")&&!CardFind("cards-hand-0").IsEnabled,"unaffordable card keeps rules text without actionable prediction");DamageShot(folder,"damage-unaffordable",size);PauseForBackground();Pulse(false,false);check(state==FarmPracticeTests.Xml(G.S),"background does not apply predicted attack");
            }
            int cases=0;
            foreach(int id in new[]{0,1,3,4,7,10,11,13})foreach(int upgrade in new[]{0,100})foreach(int stage in new[]{0,2,4})foreach(int shield in new[]{0,9})foreach(int wet in new[]{0,2}){
                var g=new FarmGame(null,null,true);g.StartCards(false);g.C.CompletedRuns=1;g.C.Stage=stage;g.C.Turn=3;g.C.Block=7;g.C.Wet=wet;g.C.EnemyBlock=shield;g.C.Relic=2;g.C.EnemyHp=g.C.EnemyMax=200;
                g.C.Hand=new[]{id+upgrade,3}.ToList();g.C.Draw=new[]{0,1,2}.ToList();g.C.Discard.Clear();g.C.Exhaust.Clear();g.C.Deck=g.C.Hand.Concat(g.C.Draw).ToList();g.ValidateCards();
                string state=FarmPracticeTests.Xml(g.S);var hit=g.PreviewCardHit(0);check(hit!=null&&state==FarmPracticeTests.Xml(g.S),"preview never mutates any live state or RNG");int hp=g.C.EnemyHp;g.PlayCard(0,g.C.Revision);check(hit.Damage==hp-g.C.EnemyHp&&hit.Blocked==g.C.LastBlock,"all attack previews agree with committed rules including legacy relic bonus");cases++;
            }
            var saved=new FarmGame(Path.Combine(folder,"damage-isolated-"+Guid.NewGuid().ToString("N")+".xml"));saved.StartCards(false);string disk=File.ReadAllText(saved.SavePath);string expected=saved.CardHandText(0);check(disk==File.ReadAllText(saved.SavePath),"preview never writes original isolated save");var loaded=new FarmGame(saved.SavePath);disk=File.ReadAllText(saved.SavePath);check(loaded.CardHandText(0)==expected&&disk==File.ReadAllText(saved.SavePath),"normal isolated reload derives identical damage without save writes");check(loaded.PreviewCardHit(-1)==null&&loaded.PreviewCardHit(99)==null&&loaded.PreviewCardHit(2)==null,"invalid slot and non-attack are not falsely predicted");
            saved.PlayCard(0,saved.C.Revision);loaded.PlayCard(0,loaded.C.Revision);check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(loaded.C),"normal reload continues identical actual card action");
            File.WriteAllText(Path.Combine(folder,"cards-damage-regression.txt"),"PASS: "+cases+" actual attack comparisons across all eight attacks, upgrades, enemy shield, wet, snail between hits, boss and relic bonus; explicit zero damage and total for two hits; capped lethal hit; live refresh after wet/shell; secondary card effects retained; unaffordable and invalid inert; browse/cancel/background/repeated preview preserve full state and RNG; stale click; isolated normal save and deterministic continuation; three fonts at 764x680 with unclipped hand descriptions and visible first row/end. Detached WPF only; controlled boundary fixtures, not human testing.");
        }finally{G.S=original;Tab=tab;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}
