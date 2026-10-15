using System;
using System.Linq;

public class SavedParty
{
    public int InspirationStyle;
    public bool Filled;public string Name="";public int[] Team=new int[0],Skills=new int[0],Relics=new int[0];public int Caretaker=-1;
}
public class JourneyMemory
{
    public int Picnic=-1,PicnicTriggers=0,PicnicHealing=0;
    public int InspirationStyle=-1;
    public InspirationPick[] Inspirations=new InspirationPick[0];
    public BattleNote[] Battles=new BattleNote[0];
    public long RecordedTicks;public int Region,Depth,Station,Fertilizer,Crystals,Decoration=-1,Relic=-1;
    public string Outcome="",Build="",Blessing="",Report="",RelicReport="";
    public string[] Names=new string[0],Skills=new string[0];public int[] Levels=new int[0];
}
public partial class FarmGame
{
    private static bool ValidParty(SavedParty p){return p!=null&&p.Filled&&p.Team!=null&&p.Team.Length==3&&p.Team.All(i=>i>=0&&i<PetCount)&&p.Team.Distinct().Count()==3&&p.Skills!=null&&p.Skills.Length==PetCount&&p.Skills.All(i=>i==0||i==1)&&p.Relics!=null&&p.Relics.Length==2&&p.Relics.All(i=>i>=-1&&i<6)&&(p.Relics[0]<0||p.Relics[0]!=p.Relics[1])&&p.Caretaker>=0&&p.Caretaker<PetCount&&!p.Team.Contains(p.Caretaker);}
    private void NormalizeManagement()
    {
        if(S.SavedParties==null||S.SavedParties.Length!=3){var old=S.SavedParties;S.SavedParties=new SavedParty[3];if(old!=null)Array.Copy(old,S.SavedParties,Math.Min(3,old.Length));}
        for(int i=0;i<3;i++){if(!ValidParty(S.SavedParties[i]))S.SavedParties[i]=new SavedParty();else{S.SavedParties[i].Name=ShortText(S.SavedParties[i].Name,30);S.SavedParties[i].InspirationStyle=Math.Max(0,Math.Min(2,S.SavedParties[i].InspirationStyle));}}
        S.JourneyMemories=(S.JourneyMemories??new JourneyMemory[0]).Where(m=>m!=null&&m.RecordedTicks>0&&m.RecordedTicks<=DateTime.MaxValue.Ticks&&m.Region>=0&&m.Region<2&&m.Depth>=0&&m.Depth<3&&m.Station>=1&&m.Station<=5&&m.Names!=null&&m.Names.Length==3&&m.Names.All(n=>n!=null)&&m.Skills!=null&&m.Skills.Length==3&&m.Skills.All(n=>n!=null)&&m.Levels!=null&&m.Levels.Length==3&&m.Levels.All(l=>l>=1&&l<=10)&&(m.Outcome=="victory"||m.Outcome=="defeat"||m.Outcome=="retreat")).Take(10).ToArray();
        foreach(var m in S.JourneyMemories){m.Inspirations=CleanInspirationPicks(m.Inspirations);m.InspirationStyle=m.InspirationStyle>=0&&m.InspirationStyle<3?m.InspirationStyle:-1;foreach(var pick in m.Inspirations)pick.Reason=ShortText(pick.Reason,180);m.Battles=CleanBattleNotes(m.Battles);m.Fertilizer=Math.Max(0,m.Fertilizer);m.Crystals=Math.Max(0,m.Crystals);m.Decoration=m.Decoration>=0&&m.Decoration<6?m.Decoration:-1;m.Relic=m.Relic>=0&&m.Relic<6?m.Relic:-1;m.Report=ShortText(m.Report,2000);m.RelicReport=ShortText(m.RelicReport,800);m.Build=ShortText(m.Build,50);m.Blessing=ShortText(m.Blessing,50);for(int i=0;i<3;i++){m.Names[i]=ShortText(m.Names[i],30);m.Skills[i]=ShortText(m.Skills[i],50);}}
    }
    private static string ShortText(string text,int limit){return String.IsNullOrEmpty(text)?"":text.Length>limit?text.Substring(0,limit):text;}
    public string PartyStyle
    {
        get{if(S.Team.Contains(4)&&S.Skills[4]==1&&S.Team.Contains(1))return "火毒催化";if(S.Team.Contains(4)&&S.Skills[4]==0&&S.Team.Contains(0))return "潮露毒种";if(S.Team.Contains(3)&&S.Skills[3]==1)return "护盾重击";if(S.Team.Contains(2)&&S.Skills[2]==0&&S.Team.Contains(0))return "水雷连锁";if(S.Team.Contains(1)&&S.Team.Contains(2)&&S.Skills[2]==1)return "灼烧蔓延";return "自由搭配";}
    }
    public bool SaveParty(int slot)
    {
        if(!AtHome||slot<0||slot>=3)return false;NormalizeManagement();
        S.SavedParties[slot]=new SavedParty{InspirationStyle=S.InspirationStyle,Filled=true,Name=PartyStyle,Team=(int[])S.Team.Clone(),Skills=(int[])S.Skills.Clone(),Relics=(int[])S.EquippedRelics.Clone(),Caretaker=Caretaker};S.Notice="已把当前队伍、技能、信物与农场分工保存到常用 "+(slot+1)+"。";Save();return true;
    }
    public bool LoadParty(int slot)
    {
        if(!AtHome||slot<0||slot>=3)return false;NormalizeManagement();var p=S.SavedParties[slot];
        if(!ValidParty(p)||p.Team.Any(i=>!SkillUnlocked(i,p.Skills[i]))||p.Relics.Any(i=>i>=0&&!RelicOwned(i)))return false;
        TickFarm(DateTime.UtcNow);S.Team=(int[])p.Team.Clone();S.Skills=(int[])p.Skills.Clone();S.EquippedRelics=(int[])p.Relics.Clone();S.FarmCaretaker=p.Caretaker;S.InspirationStyle=p.InspirationStyle;S.Notice="已使用常用 "+(slot+1)+" · "+p.Name+"。等级、进化与桌面展示保持不变。";Save();return true;
    }
    public bool AssignPartySlot(int slot,int pet)
    {
        if(!AtHome||slot<0||slot>=3||pet<0||pet>=PetCount||S.Team[slot]==pet)return false;
        TickFarm(DateTime.UtcNow);int previous=S.Team[slot],other=Array.IndexOf(S.Team,pet);if(other>=0)S.Team[other]=previous;else if(S.FarmCaretaker==pet)S.FarmCaretaker=previous;S.Team[slot]=pet;
        S.Notice=Name(pet)+"来到第 "+(slot+1)+" 位"+(other>=0?"，与"+Name(previous)+"互换站位。":"。原伙伴回到农场休息。");Save();return true;
    }
    public int PartyTrainingCost{get{return S.Team.Sum(p=>TrainToNextCost(p));}}
    public bool TrainParty()
    {
        int cost=PartyTrainingCost;if(!AtHome||cost<=0||S.Grain<cost)return false;
        int[] steps=S.Team.Select(p=>TrainToNextCost(p)/3).ToArray();S.Grain-=cost;for(int i=0;i<3;i++){S.Xp[S.Team[i]]+=steps[i]*18;if(steps[i]>0)CompleteSmallActivity("train",S.Team[i]);}
        S.Notice="全队训练完成，消耗麦穗 "+cost+"。未满级的出战伙伴各提升 1 级；亲密不变。";Save();return true;
    }
    public static readonly string[] PlantingNames={"均衡种植","多种莓果","多种麦穗","薄荷补给"};
    public int[] PlantingPlan(int plan)
    {
        if(plan<0||plan>3||(plan==3&&!S.GardenUnlocked))return null;
        if(plan==3)return new[]{0,0,1,1,2,2};int berries=plan==0?Plots/2:plan==1?Plots-1:1;return Enumerable.Range(0,Plots).Select(i=>i<berries?0:1).ToArray();
    }
    public bool ApplyPlanting(int plan)
    {
        var crops=PlantingPlan(plan);if(crops==null)return false;int changed=Enumerable.Range(0,Plots).Count(i=>S.Crops[i]!=crops[i]);if(changed==0)return false;
        TickFarm(DateTime.UtcNow);for(int i=0;i<Plots;i++)if(S.Crops[i]!=crops[i]){S.Crops[i]=crops[i];S.Growth[i]=0;}
        S.Notice="已改为「"+PlantingNames[plan]+"」，调整 "+changed+" 块田；其余田地保留生长进度。";Save();return true;
    }
    public double[] FarmRates(int[] crops=null){var result=new double[3];var planted=crops??S.Crops.Take(Plots).ToArray();foreach(int kind in planted)result[kind]+=60.0*CropYield(kind)/CropSeconds(kind);return result;}
    public string PartyAdvice(int blessing=-1)
    {
        var tips=new System.Collections.Generic.List<string>();
        int bubble=Array.IndexOf(S.Team,0),fire=Array.IndexOf(S.Team,1),lightning=Array.IndexOf(S.Team,2),seed=Array.IndexOf(S.Team,4);
        bool water=bubble>=0||blessing==0,burning=fire>=0||blessing==2,openingWater=blessing==0||(bubble>=0&&S.Evolved);
        if(lightning>=0&&S.Skills[2]==0){
            if(!water)tips.Add("电团需要潮湿才能连锁：可带泡泡，或选雷雨云。");
            else if(bubble<0)tips.Add("雷雨云提供开场潮湿，电团可接连锁；带上泡泡能持续补充潮湿。");
            else if(bubble>lightning&&!openingWater)tips.Add("泡泡放到电团前面，首轮更容易触发潮湿追击。");
            else tips.Add("水泡铺潮湿，电团接上连锁闪电。");
        }
        if(seed>=0){
            if(S.Skills[4]==0)tips.Add(!water?"带上泡泡或雷雨云，让毒种在潮湿目标上多叠中毒。":bubble>seed&&!openingWater?"泡泡先行动，种子首轮就能多叠中毒。":"潮湿帮助毒种叠层，中毒在敌人反击前结算。");
            else if(!burning)tips.Add("余烬催化需要灼烧目标：可搭配赤钳或温暖篝火。");
            else if(fire>seed&&blessing!=2)tips.Add("赤钳放到种子前面，首轮先叠灼烧，再让种子消耗毒素触发催化。");
            else tips.Add(blessing==2?"温暖篝火提供开场灼烧，种子可消耗毒素触发催化。":"赤钳先叠灼烧，种子随后消耗毒素打出催化伤害。");
        }
        if(lightning>=0&&S.Skills[2]==1){
            if(!burning)tips.Add("余热回流需要灼烧目标才会治疗：可带赤钳或温暖篝火。");
            else if(fire>lightning&&blessing!=2)tips.Add("赤钳放到电团前面，首轮更容易触发余热回流治疗。");
            else tips.Add("电团攻击灼烧目标会触发余热回流，治疗最虚弱的伙伴。");
        }
        if(S.Team.Contains(3)&&S.Skills[3]==1)tips.Add(S.Team[0]==3?"前排苔壳积累护盾，再消耗护盾重击。":"护盾主要给前排，建议让使用重击的苔壳站第 1 位。");
        bool healing=S.Team.Contains(3)||(bubble>=0&&S.Skills[0]==1)||(lightning>=0&&S.Skills[2]==1&&burning);
        if(!healing)tips.Add(blessing==3?"野餐篮补充战后体力；战斗中缺少治疗，注意前排承伤。":"本队缺少战斗中治疗，可带苔壳、雨露泡泡，或选野餐篮补充战后续航。");
        if(tips.Count==0)tips.Add("第 1 位承担主要伤害；配好输出与续航，再试一趟熟悉的地图。");return String.Join("\n",tips.ToArray());
    }
    private void RememberJourney()
    {
        if(!R.Casual||R.LegacyRewards||R.HistoryRecorded)return;NormalizeManagement();
        var memory=new JourneyMemory{RecordedTicks=DateTime.UtcNow.Ticks,Region=R.Difficulty,Depth=R.Depth,Station=R.Node+1,Outcome=R.Withdrew?"retreat":R.Outcome,Fertilizer=Math.Max(0,R.RewardFertilizer),Crystals=Math.Max(0,R.RewardCrystals),Decoration=R.DecorationReward,Relic=R.NewRelic,Names=S.Team.Select(Name).ToArray(),Levels=S.Team.Select(Level).ToArray(),Skills=S.Team.Select(p=>SkillNames[p][R.Skills[p]]).ToArray(),Build=PartyStyle,Blessing=R.Blessing>=0?BlessingNames[R.Blessing]:"",Report=ComboReport,RelicReport=RelicLoadout(R.Relics)+"\n"+RelicReport};
        memory.Picnic=R.Picnic;memory.PicnicTriggers=R.PicnicTriggers;memory.PicnicHealing=R.PicnicHealing;memory.InspirationStyle=R.InspirationsEnabled?R.InspirationStyle:-1;memory.Inspirations=R.InspirationPicks.Select(p=>p.Copy()).ToArray();memory.Battles=CleanBattleNotes(R.Battles).Select(b=>b.Copy()).ToArray();S.JourneyMemories=new[]{memory}.Concat(S.JourneyMemories).Take(10).ToArray();R.HistoryRecorded=true;
    }
}
