using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class MossRun
{
    internal MossRun CopyForPreview()
    {
        var copy=(MossRun)MemberwiseClone();
        copy.Deck=Deck.ToList();copy.Hand=Hand.ToList();copy.Draw=Draw.ToList();copy.Discard=Discard.ToList();
        copy.Exhaust=Exhaust.ToList();copy.Offers=Offers.ToList();copy.Trail=Trail.ToList();return copy;
    }
}
internal sealed class MossForecast
{
    internal int Hp,Loss,Blocked,EnemyHp;
    internal bool Playable,Won,Defeat,CardWon;
}
public partial class FarmGame
{
    internal FarmGame CardPreviewClone(){var shadow=new FarmGame(null,null,true);shadow.S.Cards=C.CopyForPreview();return shadow;}
    internal MossForecast ForecastCards(int slot=-1)
    {
        var result=new MossForecast{Hp=C.Hp,EnemyHp=C.EnemyHp};
        if(C.Phase!="battle"||slot< -1||slot>=C.Hand.Count)return result;
        var shadow=CardPreviewClone();
        if(slot>=0&&!shadow.PlayCard(slot,shadow.C.Revision))return result;
        result.Playable=true;
        result.CardWon=slot>=0&&shadow.C.Phase!="battle"&&shadow.C.Hp>0&&shadow.C.EnemyHp==0;
        int beforeEnemy=shadow.C.Hp;
        if(shadow.C.Phase=="battle")shadow.EndCardTurn(shadow.C.Revision);
        result.Hp=shadow.C.Hp;result.Loss=beforeEnemy-shadow.C.Hp;result.Blocked=shadow.CardVisualBlocked;
        result.EnemyHp=shadow.C.EnemyHp;result.Defeat=shadow.C.Hp==0;result.Won=!result.Defeat&&shadow.C.EnemyHp==0;
        return result;
    }
}
public partial class FarmPanel
{
    private TextBlock cardForecastText;
    private MossForecast cardDefaultForecast;
    private void ShowCardForecast(int slot,int view)
    {
        if(view!=cardViewRevision||Tab!="卡牌冒险"||G.C.Phase!="battle"||cardForecastText==null)return;
        var f=slot<0?cardDefaultForecast:G.ForecastCards(slot);
        if(f==null)return;
        string prefix=slot<0?"现在结束：":"先打这张再结束：";
        string outcome=f.Defeat?"会败退":f.Won?"反击取胜":f.Loss==0?"不掉体力":"会掉 "+f.Loss+" 体力";
        if(f.CardWon){prefix="打出这张：";outcome="击败对手";}
        if(!f.Playable){prefix="还不能打：";outcome="能量不足";}
        cardForecastText.Text=prefix+outcome;
        cardForecastText.Foreground=f.Defeat||f.Loss>0?U.Color("#8A4432"):U.Green;
        cardForecastText.ToolTip=f.Playable?"预计体力 "+G.C.Hp+" → "+f.Hp+"；护盾挡住 "+f.Blocked+"。\n只预览，不出牌、不结束回合。":"补足能量后才能打出。预览不会消耗任何东西。";
        AutomationProperties.SetName(cardForecastText,cardForecastText.Text);
    }
    private StackPanel CardForecastArea()
    {
        var area=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
        cardForecastText=Ct("",12);cardForecastText.Margin=new Thickness(0);
        AutomationProperties.SetAutomationId(cardForecastText,"cards-forecast");area.Children.Add(cardForecastText);
        if(G.C.Phase=="battle"){cardDefaultForecast=G.ForecastCards();ShowCardForecast(-1,cardViewRevision);}
        else cardForecastText.Text="这一战已结束";
        var counts=Ct("手牌 "+G.C.Hand.Count+" · 抽牌 "+G.C.Draw.Count+" · 弃牌 "+G.C.Discard.Count+(G.C.Hand.Count>4?"（向下看更多）":""),10,U.Muted);counts.Margin=new Thickness(0);area.Children.Add(counts);
        return area;
    }
    private void AttachCardForecast(Border tile,string id,string verb)
    {
        if(verb!="打出"||id==null||!id.StartsWith("cards-hand-"))return;
        int slot;if(!Int32.TryParse(id.Substring("cards-hand-".Length),out slot))return;
        int view=cardViewRevision;AttachCardInspection(tile,slot,cardInspect,active=>ShowCardForecast(active,view));
    }
}
