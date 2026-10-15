using System;
using System.Linq;

public partial class FarmGame
{
    public const int PetCount=5;
    public static readonly string[] ElementNames={"水","火","草","土","雷"};
    public static readonly string[] ElementIcons={"water","fire","grass","earth","lightning"};
    public static readonly string[] DutyNames={"坦克","战士","射手","治疗","辅助","特殊机制"};
    public static readonly string[] DutyIcons={"tank","warrior","ranger","healer","support","special"};
    public static readonly int[] Elements={0,1,4,2,2},Duties={4,1,5,3,2};
    public static readonly string[] SeedNames={"暗影种子","幽影花苞","幻影花","秘影花灵"};
    public static readonly string[] SeedChanges={"毒种让目标中毒；潮湿会帮助毒种生长。","毒种多叠 1 层；每次攻击也让另一名敌人中毒。","毒种再多叠 1 层；每第三次出手，让全体敌人中毒。","每场开场让全体敌人中毒；中毒敌人倒下时，毒素传给另一名敌人。"};
    private void NormalizeRoster()
    {
        if(S==null)return;
        if(S.CourtyardPet< -1||S.CourtyardPet>=PetCount)S.CourtyardPet=-1;
        if(S.Xp!=null&&S.Xp.Length==4)Array.Resize(ref S.Xp,PetCount);
        if(S.Skills!=null&&S.Skills.Length==4)Array.Resize(ref S.Skills,PetCount);
        if(S.Run==null)return;
        if(R.Hp!=null&&R.Hp.Length==4)Array.Resize(ref R.Hp,PetCount);
        if(R.Shield!=null&&R.Shield.Length==4)Array.Resize(ref R.Shield,PetCount);
        if(R.Skills!=null&&R.Skills.Length==4)Array.Resize(ref R.Skills,PetCount);
        S.SeedStage=Math.Max(0,Math.Min(3,S.SeedStage));S.SeedBond=Math.Max(0,S.SeedBond);
        if(S.FarmCaretaker< -1||S.FarmCaretaker>=PetCount)S.FarmCaretaker=-1;
        if(R.Poison==null||R.Poison.Length!=3)R.Poison=new int[3];
        for(int i=0;i<3;i++)R.Poison[i]=Math.Max(0,Math.Min(40,R.Poison[i]));
    }
    public bool CaveUnlocked{get{return S.Evolved||S.SeedStage>0;}}
    public int SeedRequiredLevel{get{return new[]{2,4,6,6}[S.SeedStage];}}
    public int SeedRequiredBond{get{return new[]{2,4,6,6}[S.SeedStage];}}
    public int SeedEvolutionCost{get{return new[]{1,3,5,0}[S.SeedStage];}}
    public bool CanEvolveSeed{get{return AtHome&&S.SeedStage<3&&Level(4)>=SeedRequiredLevel&&S.SeedBond>=SeedRequiredBond&&S.Crystals>=SeedEvolutionCost;}}
    public bool EvolveSeed()
    {
        if(!CanEvolveSeed)return false;
        S.Crystals-=SeedEvolutionCost;S.SeedStage++;S.Notice="进化完成！"+Name(4)+"学会了新的毒种效果。成长、技能与队伍位置全部保留。";Save();return true;
    }
    public string SeedEvolutionHint{get{
        if(S.SeedStage==3)return "四阶进化已完成。可继续培养等级、调整技能与队伍。";
        if(CanEvolveSeed)return "条件已满足，消耗晶石 "+SeedEvolutionCost+" 进化为「"+SeedNames[S.SeedStage+1]+"」。";
        int[] thresholds={0,20,55,110,190,300,450,650,900,1250};
        return "还需成长 "+Math.Max(0,thresholds[SeedRequiredLevel-1]-S.Xp[4])+"、亲密 "+Math.Max(0,SeedRequiredBond-S.SeedBond)+"、晶石 "+Math.Max(0,SeedEvolutionCost-S.Crystals)+"。喂养增加成长与亲密，冒险首领掉落晶石。"+(!AtHome?" 返家后可培养和进化。":"");
    }}
    public string CaretakerBonus(int p){return new[]{"浇灌：全田生长速度 +20%","翻土：麦穗每次收获 +1","活力：全田生长速度 +10%","育苗：莓果每次收获 +1","播种：莓果生长速度 +15%"}[p];}
    public bool SetCaretaker(int pet){if(!AtHome||pet<0||pet>=PetCount||S.Team.Contains(pet))return false;TickFarm(DateTime.UtcNow);S.FarmCaretaker=pet;S.Notice=Name(pet)+"开始照料农田："+CaretakerBonus(pet)+"。";Save();return true;}
    public bool JoinTeam(int pet,int slot)
    {
        if(!AtHome||pet<0||pet>=PetCount||slot<0||slot>=3||S.Team.Contains(pet))return false;
        TickFarm(DateTime.UtcNow);int old=S.Team[slot];S.Team[slot]=pet;if(S.FarmCaretaker==pet)S.FarmCaretaker=old;
        S.Notice=Name(pet)+"替换"+Name(old)+"加入第 "+(slot+1)+" 位。";Save();return true;
    }
    public bool SetPoisonPreset()
    {
        if(!AtHome)return false;TickFarm(DateTime.UtcNow);S.Team=new[]{0,4,3};S.Skills[0]=0;S.Skills[4]=0;S.Skills[3]=0;
        S.Notice="已配好「潮露毒种」：泡泡铺潮湿，种子叠中毒，苔壳负责续航。";Save();return true;
    }
    private void AddPoison(int enemy,int amount){if(enemy<0||R.EnemyHp[enemy]<=0)return;if(HasRelic(5)){amount+=2;RelicProc(5);}R.Poison[enemy]=Math.Min(40,R.Poison[enemy]+amount);R.PoisonTriggers++;}
    private void SeedAttack(int enemy,int damage)
    {
        int poisonBefore=R.Poison[enemy],poison=2+S.SeedStage+(R.Skills[4]==0&&R.Wet[enemy]>0?3:0)+2*R.Inspirations[9];AddPoison(enemy,poison+(HasPicnic(5)?1:0));if(HasPicnic(5)&&poisonBefore+poison+(HasRelic(5)?2:0)<40)PicnicProc();if(R.Poison[enemy]>poisonBefore)InspirationProc(9);
        if(R.Skills[4]==1&&R.Burn[enemy]>0){int consumed=Math.Min(4,R.Poison[enemy]);R.Poison[enemy]-=consumed;int catalystDamage=consumed*(3+2*R.Inspirations[10]);damage+=catalystDamage;R.CatalystTriggers++;if(consumed>0)InspirationProc(10);R.LastCombo="余烬催化！灼烧引爆 "+consumed+" 层毒素，追加 "+catalystDamage+" 伤害";}
        else R.LastCombo=R.Skills[4]==0&&R.Wet[enemy]>0?"潮露毒种！潮湿目标额外叠加中毒":"毒种命中，毒素会在回合结束造成伤害";
        JourneyHit(enemy,damage,false,4);
        if(S.SeedStage>=1){int other=Enumerable.Range(0,R.EnemyCount).Where(e=>e!=enemy&&R.EnemyHp[e]>0).DefaultIfEmpty(-1).First();AddPoison(other,2);}
        if(S.SeedStage>=2&&R.Round%3==0){for(int e=0;e<R.EnemyCount;e++)AddPoison(e,3);R.LastCombo="毒种连射！全体敌人追加 3 层中毒";}
    }
}
