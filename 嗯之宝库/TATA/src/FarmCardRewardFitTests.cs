using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class FarmPanel
{
    internal void RenderRewardFitChecks(string folder)
    {
        var original=G.S;string tab=Tab;Action<bool,string> check=FarmHeadlessTests.Check;
        try{for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};Tab="卡牌冒险";cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();G.StartCards(false);
            // Reach the reward phase using real actions, then use controlled offers to cover each pairing.
            G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);check(G.C.Phase=="reward","fit test reached reward");G.C.Offers.Clear();G.C.Offers.AddRange(new[]{1,7,8});
            CardShot(folder,"fit-start",size);string before=FarmPracticeTests.Xml(G.S);
            var button=CardFind("cards-reward-0");DependencyObject node=button;while(!(node is Border&&((Border)node).Child is DockPanel))node=VisualTreeHelper.GetParent(node);var tile=(Border)node;
            tile.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});
            check(cardRewardFitTitle.Text.Contains("已有同名牌 1 张")&&cardRewardFitDetail.Text.Contains("泼水")&&G.RewardFit(1).Partners==2,"owned counts and actual wet sources");
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-fit-wet-"+size+".png"),764,680);CardVisible("cards-skip");for(int i=0;i<3;i++)CardVisible("cards-reward-"+i);
            tile.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent});check(cardRewardFitTitle.Text.Contains("移到奖励牌"),"leaving restores neutral instruction");
            button=CardFind("cards-reward-1");button.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,null,button){RoutedEvent=Keyboard.GotKeyboardFocusEvent});
            check(cardRewardFitTitle.Text.Contains("泡泡冲撞")&&cardRewardFitDetail.Text.Contains("泡泡盾")&&G.RewardFit(7).Partners==3,"keyboard shows actual shield partners");
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-fit-shield-"+size+".png"),764,680);check(before==FarmPracticeTests.Xml(G.S),"hover and focus preserve full save, offers and RNG");
            ClickCard("cards-deck");CardBack();check(before==FarmPracticeTests.Xml(G.S),"deck browsing returns to same reward without claiming");
            var stale=tile;var claim=CardFind("cards-reward-0");int count=G.C.Deck.Count,rewards=G.C.RewardCount;ClickCard("cards-reward-0");claim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));stale.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});
            check(G.C.Deck.Count==count+1&&G.C.RewardCount==rewards+1&&G.C.Phase=="branch","explicit reward is added once; stale hover and click ignored");
            G.C.Phase="reward";G.C.Offers.Clear();G.C.Offers.AddRange(new[]{101,107,108});G.C.Deck.Clear();G.C.Deck.AddRange(new[]{1,101,13});Render();ShowRewardFit(0,cardViewRevision);
            check(cardRewardFitTitle.Text.Contains("2 张（强化 1 张）")&&cardRewardFitDetail.Text.Contains("没有铺潮湿"),"mixed upgrades counted and absent synergy stated");
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-fit-missing-"+size+".png"),764,680);CardVisible("cards-skip");
            G.C.Deck.Clear();G.C.Deck.AddRange(new[]{2,102,5,105,12,112,14,114});G.C.ShieldBranch=2;Render();ShowRewardFit(1,cardViewRevision);check(cardRewardFitDetail.Text.Contains("脆泡泡")&&cardRewardFitDetail.Text.Contains("等 8 张"),"long mixed deck uses branch names and factual partner total");
            FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-fit-long-"+size+".png"),764,680);CardVisible("cards-skip");
            var content=(FrameworkElement)Content;var rect=cardRewardFitDetail.TransformToAncestor(content).TransformBounds(new Rect(0,0,cardRewardFitDetail.ActualWidth,cardRewardFitDetail.ActualHeight));check(rect.Right<=764&&rect.Bottom<=680&&rect.Height>0,"fit detail inside smallest window");
            before=FarmPracticeTests.Xml(G.S);for(int card=0;card<15;card++){check(G.RewardFit(card)!=null&&G.RewardFit(card+100)!=null,"all normal and upgraded cards have context");}check(!G.RewardFit(3).Detail.Contains("抽")&&G.RewardFit(13).Detail.Contains("抽一张"),"plain attack is not described as a draw card");check(G.RewardFit(99)==null&&FarmPracticeTests.Xml(G.S)==before,"all context is read-only and invalid id rejected");
            ShowRewardFit(2,cardViewRevision);check(cardRewardFitDetail.Text.Contains("不会在领取时恢复"),"healing context never promises immediate healing");
            count=G.C.Deck.Count;rewards=G.C.RewardCount;ClickCard("cards-skip");check(G.C.Deck.Count==count&&G.C.RewardCount==rewards,"skip remains free without claiming");
        }
        RenderUpgradedFitChecks(folder);
        // Exercise the actual pair rules behind the contextual relationships.
        int pairs=0;foreach(int source in new[]{0,4,6})foreach(int payoff in new[]{1,10,11}){
            var g=new FarmGame(null,null,true);g.StartCards(false);g.C.Hand.Clear();g.C.Hand.AddRange(new[]{source,payoff});g.C.EnemyHp=g.C.EnemyMax=500;g.C.Energy=9;
            check(g.PlayCard(0,g.C.Revision)&&g.PlayCard(0,g.C.Revision)&&g.C.Combos==1,"wet pairing activates real combat combo");pairs++;
        }
        foreach(int source in new[]{2,5,12,14}){
            var g=new FarmGame(null,null,true);g.StartCards(false);g.C.Hand.Clear();g.C.Hand.AddRange(new[]{source,7});g.C.Energy=9;g.C.EnemyHp=500;
            g.PlayCard(0,g.C.Revision);int block=g.C.Block;g.PlayCard(0,g.C.Revision);check(g.C.LastDamage==3+block,"shield pairing increases actual collision damage");pairs++;
        }
        File.WriteAllText(Path.Combine(folder,"cards-reward-fit-regression.txt"),"PASS: all 30 normal/upgraded cards; actual owned and partner counts; absent synergy; branch names; bounded long-deck text; "+pairs+" real rule pairings; mouse/focus/reset; browse preserves full save/offers/RNG; explicit claim once and skip; stale controls rejected; three font scales at 764x680 with all choices, context and skip visible. Controlled offer fixtures; detached WPF only.");
        }finally{G.S=original;Tab=tab;cardRewardFitTitle=null;cardRewardFitDetail=null;cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();}
    }
}

public partial class FarmPanel
{
    private void FitUpgradeShot(string folder,string name,int size,int slot)
    {
        Render();ShowRewardFit(slot,cardViewRevision);
        FarmHeadlessTests.Render(this,Path.Combine(folder,"cards-"+name+"-"+size+".png"),764,680);
        CardVisible("cards-skip");for(int i=0;i<3;i++)CardVisible("cards-reward-"+i);
        var text=cardRewardFitDetail;
        var formatted=new System.Windows.Media.FormattedText(text.Text,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,text.Foreground,VisualTreeHelper.GetDpi(text).PixelsPerDip);
        formatted.MaxTextWidth=text.ActualWidth;
        FarmHeadlessTests.Check(formatted.Height<=text.ActualHeight+1,"upgraded fit text fully fits");
    }
    internal void RenderUpgradedFitChecks(string folder)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++){
            G.S=new FarmSave{ReadingSize=size};G.StartCards(false);G.C.EnemyHp=1;G.PlayCard(0,G.C.Revision);
            cardDeckOpen=cardRulesOpen=cardRetireOpen=cardUpgradeOpen=false;ClearCardFeedback();
            G.C.Deck=new[]{106,109,109,6,9}.ToList();G.C.Offers=new[]{7,106,109}.ToList();
            string state=FarmPracticeTests.Xml(G.S);FitUpgradeShot(folder,"fit-upgraded-sources",size,0);
            check(G.RewardFit(7).Partners==3&&cardRewardFitDetail.Text.Contains("小雨点 +")&&cardRewardFitDetail.Text.Contains("深呼吸 +")&&!cardRewardFitDetail.Text.Contains("没有"),"only upgraded auxiliary copies count as shield sources");
            var stale=CardFind("cards-reward-0");ClickCard("cards-deck");CardBack();check(state==FarmPracticeTests.Xml(G.S),"upgraded fit browsing cancels without reward or RNG mutation");
            stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S),"stale upgraded fit control cannot claim");
            var claim=CardFind("cards-reward-0");ClickCard("cards-reward-0");state=FarmPracticeTests.Xml(G.S);claim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(state==FarmPracticeTests.Xml(G.S)&&G.C.Deck.Count==6,"upgraded fit claim happens exactly once");
            G.C.Phase="reward";G.C.Offers=new[]{106,109,6}.ToList();G.C.Deck=new[]{7,107}.ToList();
            FitUpgradeShot(folder,"fit-upgraded-rain",size,0);check(cardRewardFitDetail.Text.Contains("加盾，再接冲撞")&&G.RewardFit(106).Partners==2,"rain upgrade shows shield partner when no wet payoff exists");
            FitUpgradeShot(folder,"fit-upgraded-breath",size,1);check(cardRewardFitDetail.Text.Contains("泡泡冲撞"),"breath upgrade shows real collision partner");
            check(G.RewardFit(6).Partners==0&&G.RewardFit(9).Partners==0,"base rain and breath never claim upgraded shield");
            G.C.Deck.Add(1);FitUpgradeShot(folder,"fit-upgraded-dual",size,0);check(cardRewardFitDetail.Text.Contains("小小电花")&&!cardRewardFitDetail.Text.Contains("再接冲撞"),"dual-purpose rain keeps one existing wet example");
            G.C.Deck=new[]{106,109}.ToList();check(G.RewardFit(109).Detail.Contains("能量和抽牌"),"breath without collision retains useful energy and draw explanation");
            state=FarmPracticeTests.Xml(G.S);PauseForBackground();Pulse(false,false);check(state==FarmPracticeTests.Xml(G.S),"background does not claim previewed synergy");ClickCard("cards-skip");check(G.C.Deck.Count==2,"skip does not add a card");
        }
        int cases=0;
        foreach(int rules in new[]{0,1})foreach(int id in Enumerable.Range(0,15))foreach(int upgrade in new[]{0,100}){
            int card=id+upgrade;var g=new FarmGame(null,null,true);g.StartCards(false);g.C.RulesVersion=rules;g.C.Hand=new[]{card,7}.ToList();g.C.Deck=g.C.Hand.ToList();g.C.Draw.Clear();g.C.Discard.Clear();g.C.Exhaust.Clear();g.C.EnemyHp=g.C.EnemyMax=500;g.C.Energy=9;g.ValidateCards();
            string state=FarmPracticeTests.Xml(g.C);int partners=g.RewardFit(7).Partners;check(state==FarmPracticeTests.Xml(g.C),"classifying sources preserves rules and RNG");
            g.PlayCard(0,g.C.Revision);int shield=g.C.Block;
            check(partners==(shield>0?1:0),"all normal and upgraded source classifications agree with actual shield gain");
            if(shield>0){g.PlayCard(g.C.Hand.IndexOf(7),g.C.Revision);check(g.C.LastDamage==3+shield,"source adds actual collision damage");}cases++;
        }
        foreach(int rules in new[]{0,1}){
            var saved=new FarmGame(Path.Combine(folder,"fit-upgraded-isolated-"+rules+".xml"));saved.StartCards(false);saved.C.RulesVersion=rules;saved.C.EnemyHp=1;saved.PlayCard(0,saved.C.Revision);saved.C.Deck.AddRange(new[]{106,109});saved.C.Offers=new[]{7,106,109}.ToList();saved.Save();
            var loaded=new FarmGame(saved.SavePath);string disk=File.ReadAllText(saved.SavePath),state=FarmPracticeTests.Xml(loaded.C);
            check(saved.RewardFit(7).Partners==5&&loaded.RewardFit(7).Detail==saved.RewardFit(7).Detail,"normal starter deck plus upgraded sources reloads with five true partners");
            check(state==FarmPracticeTests.Xml(loaded.C)&&disk==File.ReadAllText(saved.SavePath),"loaded preview never changes isolated save");
            saved.PickCardReward(0,saved.C.Revision);loaded.PickCardReward(0,loaded.C.Revision);check(FarmPracticeTests.Xml(saved.C)==FarmPracticeTests.Xml(loaded.C),"old and current rules claim identically after reload");
        }
        File.WriteAllText(Path.Combine(folder,"cards-upgraded-fit-regression.txt"),"PASS: "+cases+" source classifications versus actual card actions across normal/old rules and all 30 cards; upgraded rain/breath shield increases collision damage; base versions excluded; exact duplicate counts; reverse pairing and one-example wet priority; three fonts at 764x680; preview/cancel/background preserve save and RNG; stale/repeated claim and skip; normal/old isolated reload. Boundary fixtures, not human understanding.");
    }
}
