using System;
using System.Linq;
using System.Collections.Generic;

public class InspirationPick
{
    public int Station,Id,Rank,Style;
    public bool Manual;
    public int[] Offers=new int[0];
    public string Reason="";
    public InspirationPick Copy(){var copy=(InspirationPick)MemberwiseClone();copy.Offers=(int[])Offers.Clone();return copy;}
}
public partial class FarmGame
{
    public const int InspirationCount=12;
    public static readonly string[] InspirationStyles={"顺势搭配","进攻优先","稳健续航"};
    public static readonly string[] InspirationStyleDetails={"优先配合当前技能；体力不足时更看重保护。","更看重输出与连招，尽快解决敌人。","更看重护盾、治疗与体力，稳稳走远。"};
    public static readonly string[] InspirationNames={"导电叶脉","回响电弧","厚叶护符","回春露滴","炽热火种","长青根系","追风步伐","三拍合奏","生命露珠","毒种培育","催化余烬","潮汐护幕"};
    public static readonly string[] InspirationDetails={
        "电团攻击潮湿目标时，额外伤害 +5。","潮湿追击可跳向另一名敌人；每次跳跃伤害 +7。","每回合开始，为当前前排增加 4 护盾。","苔壳每次行动的治疗量 +5。","赤钳每次攻击多叠加 3 层灼烧。","全队体力上限 +12，存活伙伴立即恢复 12。",
        "每场第 1 回合，伙伴攻击 +5。","每场第 3、6、9…回合，追加一次 10 伤害的合奏。","击败敌人时，为最虚弱的存活伙伴恢复 10 体力。","种子主攻击多施加 2 层中毒。","种子触发余烬催化时，每层消耗的毒素额外造成 2 伤害。","电团触发潮湿追击时，为当前前排增加 3 护盾。"};
    public static readonly string[] InspirationNeeds={"电团 + 潮湿来源","电团 + 潮湿来源","所有队伍","苔壳同行","赤钳同行","所有队伍","所有队伍","所有队伍","所有队伍","种子同行","种子携带余烬催化 + 灼烧来源","电团 + 潮湿来源"};
    private static readonly int[] InspirationPerks={1,2,3,4,5,6,7,8,11,-1,-1,-1};
    private static bool InspirationDefense(int id){return id==2||id==3||id==5||id==8||id==11;}
    private static int[] InspirationArray(int[] source){var result=FixedInts(source,InspirationCount);for(int i=0;i<result.Length;i++)result[i]=Math.Max(0,result[i]);return result;}
    private static InspirationPick[] CleanInspirationPicks(InspirationPick[] source)
    {
        return (source??new InspirationPick[0]).Where(p=>p!=null&&new[]{0,1,3}.Contains(p.Station)&&p.Id>=0&&p.Id<InspirationCount&&p.Rank>=1&&p.Rank<=2&&p.Style>=0&&p.Style<3&&p.Offers!=null&&p.Offers.Length>=1&&p.Offers.Length<=3&&p.Offers.All(i=>i>=0&&i<InspirationCount)&&p.Offers.Distinct().Count()==p.Offers.Length&&p.Offers.Contains(p.Id)).GroupBy(p=>p.Station).Select(group=>group.First()).OrderBy(p=>p.Station).Take(3).ToArray();
    }
    private void NormalizeInspirations()
    {
        S.InspirationStyle=Math.Max(0,Math.Min(2,S.InspirationStyle));
        if(S.DiscoveredInspirations==null||S.DiscoveredInspirations.Length!=InspirationCount){var old=S.DiscoveredInspirations;S.DiscoveredInspirations=new bool[InspirationCount];if(old!=null)Array.Copy(old,S.DiscoveredInspirations,Math.Min(old.Length,InspirationCount));}
        R.InspirationStyle=Math.Max(0,Math.Min(2,R.InspirationStyle));R.Inspirations=InspirationArray(R.Inspirations);for(int i=0;i<InspirationCount;i++)R.Inspirations[i]=Math.Min(2,R.Inspirations[i]);
        R.InspirationTriggers=InspirationArray(R.InspirationTriggers);R.InspirationPicks=CleanInspirationPicks(R.InspirationPicks);
        R.InspirationOffers=(R.InspirationOffers??new int[0]).Where(id=>id>=0&&id<InspirationCount&&R.Inspirations[id]<2).Distinct().Take(3).ToArray();
        foreach(var pick in R.InspirationPicks)pick.Reason=ShortText(pick.Reason,180);
        if(R.Phase=="inspiration"&&(!R.InspirationsEnabled||!new[]{0,1,3}.Contains(R.Node)||R.InspirationPicks.Any(p=>p.Station==R.Node))){if(new[]{0,1,3}.Contains(R.Node))R.Node++;R.Phase="route";R.InspirationOffers=new int[0];}
        if(R.Phase=="inspiration"&&R.InspirationOffers.Length==0)R.InspirationOffers=Enumerable.Range(0,InspirationCount).Where(InspirationEligible).Take(3).ToArray();
        if(R.Phase=="inspiration"&&R.InspirationOffers.Length==0){R.Node++;R.Phase="route";}
    }
    public bool SetInspirationStyle(int style){if(!AtHome||style<0||style>2)return false;S.InspirationStyle=style;S.Notice="小队将按「"+InspirationStyles[style]+"」自动选择旅途灵感。";Save();return true;}
    public bool InspirationEligible(int id)
    {
        if(id<0||id>=InspirationCount||R.Inspirations[id]>=2)return false;
        bool water=S.Team.Contains(0)||R.Blessing==0,fire=S.Team.Contains(1)||R.Blessing==2;
        if(id==0||id==1||id==11)return S.Team.Contains(2)&&water;
        if(id==3)return S.Team.Contains(3);if(id==4)return S.Team.Contains(1);if(id==9)return S.Team.Contains(4);
        if(id==10)return S.Team.Contains(4)&&R.Skills[4]==1&&fire;return true;
    }
    private void OfferInspiration()
    {
        var pool=Enumerable.Range(0,InspirationCount).Where(InspirationEligible).ToList();var offers=new List<int>();
        foreach(bool defense in new[]{false,true}){var group=pool.Where(id=>InspirationDefense(id)==defense).ToArray();if(group.Length>0){int id=group[Random(group.Length)];offers.Add(id);pool.Remove(id);}}
        while(offers.Count<3&&pool.Count>0){int index=Random(pool.Count);offers.Add(pool[index]);pool.RemoveAt(index);}
        R.InspirationOffers=offers.ToArray();R.Phase="inspiration";R.Log+="\n小队发现三份旅途灵感，会按「"+InspirationStyles[R.InspirationStyle]+"」自动挑选。";
    }
    private bool InspirationLowHealth(){return S.Team.Sum(p=>R.Hp[p])<S.Team.Sum(p=>MaxHp(p))*.55;}
    private int InspirationScore(int id)
    {
        int score=20-R.Inspirations[id]*5;bool defense=InspirationDefense(id);
        if(id==0||id==1)score+=S.Team.Contains(2)?22:0;if(id==4)score+=S.Team.Contains(4)&&R.Skills[4]==1?24:16;
        if(id==9)score+=S.Team.Contains(4)?24:0;if(id==10)score+=28;if(id==11)score+=18;
        if(id==2&&S.Team[0]==3&&R.Skills[3]==1)score+=26;if(id==3)score+=12;
        if(R.InspirationStyle==1&&!defense)score+=38;if(R.InspirationStyle==2&&defense)score+=38;
        if(R.InspirationStyle==0&&InspirationLowHealth()&&defense)score+=45;
        return score;
    }
    public int RecommendedInspiration{get{return R.InspirationOffers.OrderByDescending(InspirationScore).ThenBy(id=>id).DefaultIfEmpty(-1).First();}}
    public string InspirationReason(int id)
    {
        if(id<0||id>=InspirationCount)return "等待发现新的灵感。";
        string reason=id==0||id==1||id==11?"电团能接上潮湿，适合把追击继续加强。":id==4?"赤钳能持续叠加灼烧。":id==9?"种子能持续施加中毒。":id==10?"灼烧与催化技能已经配齐。":id==2&&S.Team[0]==3&&R.Skills[3]==1?"前排苔壳可把护盾转成重击。":id==3?"苔壳的治疗可以帮助队伍续航。":id==5?"提高体力上限，并立即照顾存活伙伴。":id==8?"击败敌人后补充体力，帮助接着前进。":id==6?"强化每场开局，尽早减少敌人数量。":id==7?"长战斗每三回合都有额外输出。":"先给前排多一层保护。";
        return (R.InspirationStyle==0&&InspirationLowHealth()&&InspirationDefense(id)?"当前体力偏低，优先补保护。":InspirationStyles[R.InspirationStyle]+"：")+reason;
    }
    public bool ChooseInspiration(int id,int station,bool manual=true)
    {
        if(!R.Casual||!R.InspirationsEnabled||R.Phase!="inspiration"||R.Node!=station||!R.InspirationOffers.Contains(id)||!InspirationEligible(id)||R.InspirationPicks.Any(p=>p.Station==station))return false;
        string reason=manual?"你亲自选择了这份灵感。":InspirationReason(id);int[] offers=(int[])R.InspirationOffers.Clone();R.Inspirations[id]++;
        int perk=InspirationPerks[id];if(perk>=0)R.Perks[perk]++;
        if(id==5){foreach(int p in S.Team)if(R.Hp[p]>0)R.Hp[p]=Math.Min(MaxHp(p),R.Hp[p]+12);InspirationProc(5);}
        S.DiscoveredInspirations[id]=true;R.InspirationPicks=R.InspirationPicks.Concat(new[]{new InspirationPick{Station=station,Id=id,Rank=R.Inspirations[id],Style=R.InspirationStyle,Manual=manual,Offers=offers,Reason=reason}}).ToArray();
        R.Log=(manual?"你选择了":"小队选择了")+"「"+InspirationNames[id]+"」第 "+R.Inspirations[id]+" 层。"+InspirationDetails[id];R.InspirationOffers=new int[0];R.Node++;R.Phase="route";
        if(manual)R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(8).Ticks;Save();return true;
    }
    private void InspirationProc(int id){if(R.InspirationsEnabled&&R.Inspirations[id]>0)R.InspirationTriggers[id]++;}
    private void InspirationShield(int id,int pet,int amount){if(pet<0)return;int before=R.Shield[pet];AddShield(pet,amount);if(R.Shield[pet]>before)InspirationProc(id);}
    public static string InspirationLoadout(int[] counts){return counts==null||counts.All(n=>n==0)?"尚未获得旅途灵感":String.Join(" · ",Enumerable.Range(0,Math.Min(InspirationCount,counts.Length)).Where(id=>counts[id]>0).Select(id=>InspirationNames[id]+" ×"+counts[id]).ToArray());}
    public static string InspirationHistory(InspirationPick[] picks){return picks==null||picks.Length==0?"尚未记录旅途灵感。":String.Join("\n",picks.Select(p=>"第 "+(p.Station+1)+" 站 · "+InspirationNames[p.Id]+" ×"+p.Rank+" · "+(p.Manual?"亲自选择":"小队自动")+"\n"+p.Reason).ToArray());}
}
