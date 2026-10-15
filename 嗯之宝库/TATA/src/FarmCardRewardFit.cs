using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

internal sealed class MossRewardFit
{
    internal int Owned,Upgraded,Partners;
    internal string Title,Detail;
}
public partial class FarmGame
{
    private static bool CardAddsShield(int card){return new[]{2,5,12,14}.Contains(MossCards.Id(card))||card==106||card==109;}
    internal MossRewardFit RewardFit(int card)
    {
        if(!MossCards.Valid(card))return null;
        int id=MossCards.Id(card);var fit=new MossRewardFit();
        fit.Owned=C.Deck.Count(c=>MossCards.Id(c)==id);fit.Upgraded=C.Deck.Count(c=>MossCards.Id(c)==id&&MossCards.Up(c));
        fit.Title=CurrentCardName(card)+" · 已有同名牌 "+fit.Owned+" 张"+(fit.Upgraded>0?"（强化 "+fit.Upgraded+" 张）":"");
        int[] partnerIds=null;string before="",after="",missing="";
        if(new[]{0,4,6}.Contains(id)){partnerIds=new[]{1,10,11};before="牌组搭档：";after="。这张可先铺潮湿。";missing="目前没有利用潮湿的牌，暂时还连不起来。";}
        else if(new[]{1,10,11}.Contains(id)){partnerIds=new[]{0,4,6};before="牌组搭档：";after="。先铺潮湿，再用这张。";missing="目前没有铺潮湿的牌，额外伤害还缺铺垫。";}
        else if(id==7){partnerIds=new[]{2,5,12,14};before="牌组搭档：";after="。先加盾，再用这张撞击。";missing="目前没有加盾牌，护盾加伤还缺铺垫。";}
        else if(new[]{2,5,12,14}.Contains(id)){partnerIds=new[]{7};before="牌组搭档：";after="。先加盾，再用冲撞。";missing="目前没有泡泡冲撞，这张仍可用于防守。";}
        // Upgraded rain/breath can set up collision even without a wet payoff.
        // Keep rain's existing wet example when it is available, rather than listing every possible combo.
        if((card==106||card==109)&&C.Deck.Any(c=>MossCards.Id(c)==7)&&(partnerIds==null||!C.Deck.Any(c=>partnerIds.Contains(MossCards.Id(c))))){
            partnerIds=new[]{7};before="牌组搭档：";after="。这张强化后加盾，再接冲撞。";
        }
        if(partnerIds!=null){
            var partners=C.Deck.Where(c=>id==7?CardAddsShield(c):partnerIds.Contains(MossCards.Id(c))).GroupBy(c=>c).OrderBy(g=>MossCards.Id(g.Key)).ThenBy(g=>g.Key).ToArray();
            fit.Partners=partners.Sum(g=>g.Count());
            // Show a short factual sample; large mixed decks retain the total without a wall of text.
            var names=partners.Take(2).Select(g=>"「"+CurrentCardName(g.Key)+"」（"+g.Count()+"张）");
            fit.Detail=fit.Partners==0?missing:before+String.Join("、",names)+(partners.Length>2?" 等 "+fit.Partners+" 张":"")+after;
        }else if(id==8)fit.Detail="补充战斗中的回血手段；不会在领取时恢复体力。";
        else if(id==9)fit.Detail="补充战斗中的能量和抽牌；本场使用后不会再洗回来。";
        else if(id==13)fit.Detail="攻击时多抽一张牌，帮助找下一张招式。";
        else fit.Detail="直接补充伤害，不需要先铺潮湿或护盾。";
        return fit;
    }
}
public partial class FarmPanel
{
    private TextBlock cardRewardFitTitle,cardRewardFitDetail;
    private Border CardRewardFitArea()
    {
        var p=new StackPanel();cardRewardFitTitle=Ct("",12,U.Green);cardRewardFitDetail=Ct("",12,U.Muted);
        cardRewardFitTitle.Margin=cardRewardFitDetail.Margin=new Thickness(0);
        AutomationProperties.SetAutomationId(cardRewardFitTitle,"cards-reward-fit-title");
        AutomationProperties.SetAutomationId(cardRewardFitDetail,"cards-reward-fit-detail");
        p.Children.Add(cardRewardFitTitle);p.Children.Add(cardRewardFitDetail);ShowRewardFit(-1,cardViewRevision);
        return U.Card(p,9);
    }
    private void ShowRewardFit(int slot,int view)
    {
        if(view!=cardViewRevision||Tab!="卡牌冒险"||G.C.Phase!="reward"||cardRewardFitTitle==null||cardRewardFitDetail==null)return;
        if(slot<0||slot>=G.C.Offers.Count){cardRewardFitTitle.Text="移到奖励牌上，或用 Tab 选中按钮，看看搭配。";cardRewardFitDetail.Text="这里只查看。点「加入牌组」才领取，也可以不加牌。";return;}
        var fit=G.RewardFit(G.C.Offers[slot]);if(fit==null)return;
        cardRewardFitTitle.Text=fit.Title;cardRewardFitDetail.Text=fit.Detail;
    }
    private void AttachRewardFit(Border tile,string id)
    {
        if(id==null||!id.StartsWith("cards-reward-"))return;
        int slot;if(!Int32.TryParse(id.Substring("cards-reward-".Length),out slot))return;
        int view=cardViewRevision;AttachCardInspection(tile,slot,rewardInspect,active=>ShowRewardFit(active,view));
    }
}
