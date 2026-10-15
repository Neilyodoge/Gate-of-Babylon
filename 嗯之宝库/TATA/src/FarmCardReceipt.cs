using System;
using System.Collections.Generic;

public partial class FarmGame
{
    internal string CardTurnReceipt(bool rest,bool guard,int blocked,int taken,int counterDamage,bool fragile)
    {
        var parts=new List<string>();
        parts.Add(rest?"它没有攻击":guard?"它架起 9 护盾":"挡住 "+blocked+" 伤害，失去 "+taken+" 体力");
        if(counterDamage>0)parts.Add("反击造成 "+counterDamage+" 实际伤害");
        if(fragile){
            int extra=Math.Max(0,C.Hand.Count-4);
            parts.Add("破盾多抽 "+extra+" 张（共 "+C.Hand.Count+" 张）"+(extra==0?"，可抽牌不足":""));
        }
        if(C.Block>0)parts.Add("保留护盾 "+C.Block);
        return String.Join("。",parts)+"。";
    }
    // Report committed differences, including capped healing/drawing. No extra action or random call.
    internal string CardActionReceipt(int card,int hp,int block,int hand,int energy,bool combo)
    {
        int id=MossCards.Id(card),drawn=C.Hand.Count-hand+1;
        var parts=new List<string>();
        if(C.LastDamage>0)parts.Add("造成 "+C.LastDamage+" 实际伤害");
        if(C.LastBlock>0)parts.Add("敌人护盾挡住 "+C.LastBlock);
        if(id==8)parts.Add(C.Hp==hp?"体力已满，未恢复":"恢复 "+(C.Hp-hp)+" 体力");
        if(C.Block>block)parts.Add("护盾 +"+(C.Block-block));
        if(C.Energy>energy)parts.Add("能量 +"+(C.Energy-energy));
        if(id==6||id==9||id==13){
            int wanted=id==6?2:1;
            parts.Add("抽到 "+drawn+" 张"+(drawn<wanted?(C.Hand.Count>=7?"（手牌已满）":"（没有可抽的牌）"):""));
        }
        if(id==6)parts.Add("潮湿 "+C.Wet+" 回合");
        if(id==5)parts.Add("本回合每次受击反击 "+C.Reflect);
        if(id==12)parts.Add("敌人下次攻击每段 −3");
        return CurrentCardName(card)+(combo?" · 连招！":" · ")+String.Join(" · ",parts)+"。";
    }
}
