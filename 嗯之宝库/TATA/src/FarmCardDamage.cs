using System;
using System.Linq;

internal sealed class MossCardHitPreview
{
    internal int Damage,Blocked;
}
public partial class FarmGame
{
    internal MossCardHitPreview PreviewCardHit(int slot)
    {
        if(C.Phase!="battle"||slot<0||slot>=C.Hand.Count)return null;
        int card=C.Hand[slot];
        if(!new[]{0,1,3,4,7,10,11,13}.Contains(MossCards.Id(card)))return null;
        var shadow=CardPreviewClone();int hp=shadow.C.EnemyHp;
        if(!shadow.PlayCard(slot,shadow.C.Revision))return null;
        return new MossCardHitPreview{Damage=hp-shadow.C.EnemyHp,Blocked=shadow.C.LastBlock};
    }
    internal string CardHandText(int card)
    {
        var hit=PreviewCardHit(C.Hand.IndexOf(card));
        if(hit==null)return CurrentCardText(card);
        int id=MossCards.Id(card);
        string text="预计伤害 "+hit.Damage+(id==10?"（两段合计）":"")+"。";
        if(hit.Blocked>0)text+="\n敌盾挡住 "+hit.Blocked+"。";
        if(id==0||id==4)text+="\n附加 "+(id==0?2:3)+" 回合潮湿。";
        if(id==1||id==10)text+=C.Wet>0?"\n已计入潮湿连招。":id==1?"\n潮湿时额外 +6。":"\n潮湿时每段 +4。";
        if(id==7)text+="\n护盾保留。";
        if(id==11)text+=C.Wet>0?"\n含连招，并消耗潮湿。":"\n潮湿时额外 +10。";
        if(id==13)text+="\n抽 1 张牌。";
        return text;
    }
}
