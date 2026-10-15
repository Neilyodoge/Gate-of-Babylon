using System;
using System.Linq;

public partial class FarmGame
{
    public const int PicnicCount=6;
    public static readonly string[] PicnicNames={"莓果饭团","厚麦面包","月光薄荷茶","酥脆雷莓","炭烤麦饼","嫩芽卷"};
    public static readonly string[] PicnicDetails={
        "这趟全队体力上限 +8，出发时补满。",
        "每场开场，为当前前排增加 12 护盾；之后按技能规则保留。",
        "每场第 3、6、9…回合开始，为最虚弱的存活伙伴恢复 8 体力。",
        "电团攻击潮湿目标时，额外伤害 +3。",
        "赤钳每次攻击多施加 2 层灼烧。",
        "种子每次主攻击多施加 1 层中毒。"};
    public static readonly string[] PicnicNeeds={"通用 · 稳定体力","通用 · 前排保护 / 盾击","通用 · 长战斗续航","电团 + 潮湿来源","赤钳同行","种子同行"};
    private static readonly int[][] PicnicCosts={new[]{2,2,0},new[]{0,4,0},new[]{2,0,1},new[]{3,0,1},new[]{0,3,1},new[]{2,2,1}};
    public static string PicnicName(int id){return id>=0&&id<PicnicCount?PicnicNames[id]:"不带便当";}
    public static int[] PicnicCost(int id){return id>=0&&id<PicnicCount?(int[])PicnicCosts[id].Clone():new int[3];}
    public static string PicnicCostText(int id){var cost=PicnicCost(id);return id<0?"不消耗作物":String.Join(" · ",Enumerable.Range(0,3).Where(k=>cost[k]>0).Select(k=>CropNames[k]+" "+cost[k]).ToArray());}
    public bool PicnicUnlocked(int id){return id==-1||id>=0&&id<PicnicCount&&(id<2||S.GardenUnlocked);}
    public int CropStock(int kind){return kind==0?S.Berries:kind==1?S.Grain:S.Mint;}
    public bool CanPackPicnic(int id,bool checkStock=true){return PicnicUnlocked(id)&&(!checkStock||Enumerable.Range(0,3).All(k=>CropStock(k)>=PicnicCost(id)[k]));}
    public string PicnicShortage(int id)
    {
        if(!PicnicUnlocked(id))return "首次通关小溪后开放薄荷食谱。";
        var cost=PicnicCost(id);var missing=Enumerable.Range(0,3).Where(k=>CropStock(k)<cost[k]).Select(k=>CropNames[k]+" "+(cost[k]-CropStock(k))).ToArray();
        return missing.Length==0?"材料已齐，出发时制作一份。":"还差 "+String.Join(" · ",missing)+"。";
    }
    public bool PlanPicnic(int id)
    {
        if(!AtHome||!PicnicUnlocked(id))return false;S.PlannedPicnic=id;
        S.Notice=id<0?"这趟不带便当，可以免费出发。":"已选「"+PicnicNames[id]+"」，确认出发时才制作、扣材料。";Save();return true;
    }
    public string PicnicAdvice(int id,int blessing)
    {
        if(id<0)return "可以空手出发，原有冒险奖励不变。";
        if(id==3)return !S.Team.Contains(2)?"本队没有电团，这份便当不会触发；建议换通用食谱。":!S.Team.Contains(0)&&blessing!=0?"需要潮湿来源：带泡泡或雷雨云，才能强化电团追击。":"电团接上潮湿，便当与旅途中的导电叶脉可叠加。";
        if(id==4)return S.Team.Contains(1)?"每次钳击加深灼烧，可配合蔓延或余烬催化。":"本队没有赤钳，这份便当不会触发；建议换通用食谱。";
        if(id==5)return S.Team.Contains(4)?"让种子更快积攒毒素，适合潮露播种与余烬催化。":"本队没有种子，这份便当不会触发；建议换通用食谱。";
        if(id==1)return S.Team[0]==3&&S.Skills[3]==1?"前排苔壳可以把开场护盾用在第一次护盾重击。":"开场先保护前排；护盾每回合仍会按正常规则衰减。";
        if(id==2)return "从第三回合开始补充体力；快速清场时可能用不上。";
        return "提供整趟的体力余量，返家后恢复永久属性。";
    }
    private static int CleanPicnic(int id){return id>=-1&&id<PicnicCount?id:-1;}
    private void NormalizePicnic()
    {
        S.PlannedPicnic=CleanPicnic(S.PlannedPicnic);if(!PicnicUnlocked(S.PlannedPicnic))S.PlannedPicnic=-1;
        R.Picnic=CleanPicnic(R.Picnic);R.PicnicTriggers=Math.Max(0,R.PicnicTriggers);R.PicnicHealing=Math.Max(0,R.PicnicHealing);
        if(!R.Casual){R.Picnic=-1;R.PicnicTriggers=R.PicnicHealing=0;}
        foreach(var m in S.JourneyMemories){m.Picnic=CleanPicnic(m.Picnic);m.PicnicTriggers=Math.Max(0,m.PicnicTriggers);m.PicnicHealing=Math.Max(0,m.PicnicHealing);}
    }
    private bool HasPicnic(int id){return R.Casual&&R.Picnic==id;}
    private void PicnicProc(){R.PicnicTriggers++;}
    private void PackPicnic(int id)
    {
        R.Picnic=id;var cost=PicnicCost(id);if(!practiceOnly){S.Berries-=cost[0];S.Grain-=cost[1];S.Mint-=cost[2];CompleteSmallActivity("picnic",id);}
        S.PlannedPicnic=-1;foreach(int p in S.Team)R.Hp[p]=MaxHp(p);
    }
    private void PicnicOpening()
    {
        if(!HasPicnic(1))return;int front=Front();if(front<0)return;int before=R.Shield[front];AddShield(front,12);if(R.Shield[front]>before)PicnicProc();
    }
    private void PicnicRound()
    {
        if(!HasPicnic(2)||R.Round%3!=0)return;int healed=JourneyHeal(Weakest(),8,false);if(healed>0){R.PicnicHealing+=healed;PicnicProc();}
    }
    public static string PicnicReport(int id,int triggers,int healing)
    {
        if(id<0)return "这趟没有带便当。";
        return PicnicNames[id]+" · "+(id==0?"全队体力上限 +8":id==2?"实际恢复 "+healing+" 体力 · 生效 "+triggers+" 次":"满足条件 "+triggers+" 次")+"\n"+PicnicDetails[id];
    }
}
