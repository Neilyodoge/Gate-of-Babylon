using System;
using System.Linq;

public class BattleNote
{
    public int Picnic=-1,PicnicStart=0,PicnicTriggers=0,PicnicHealingStart=0,PicnicHealing=0;
    public int[] Inspirations=new int[12],InspirationStart=new int[12],InspirationTriggers=new int[12];
    public int Station,Rounds,Direct,Healing,EnemyHealing,EnemyRemaining,EnemyStart,Burn,Poison;
    public int BurnStart,PoisonStart;
    public string Outcome="ongoing",Reason="";
    public int[] Team=new int[3],Levels=new int[3],Skills=new int[3],StartHp=new int[3],EndHp=new int[3],MaxHp=new int[3];
    public string[] Names=new string[3];
    public int[] Damage=new int[FarmGame.PetCount],SkillHealing=new int[FarmGame.PetCount],Taken=new int[FarmGame.PetCount],Blocked=new int[FarmGame.PetCount],Actions=new int[FarmGame.PetCount],FellRound=new int[FarmGame.PetCount];
    public int[] ComboStart=new int[6],Combos=new int[6];
    public BattleNote Copy()
    {
        var b=(BattleNote)MemberwiseClone();b.Inspirations=(int[])Inspirations.Clone();b.InspirationStart=(int[])InspirationStart.Clone();b.InspirationTriggers=(int[])InspirationTriggers.Clone();b.Team=(int[])Team.Clone();b.Levels=(int[])Levels.Clone();b.Skills=(int[])Skills.Clone();b.StartHp=(int[])StartHp.Clone();b.EndHp=(int[])EndHp.Clone();b.MaxHp=(int[])MaxHp.Clone();b.Names=(string[])Names.Clone();
        b.Damage=(int[])Damage.Clone();b.SkillHealing=(int[])SkillHealing.Clone();b.Taken=(int[])Taken.Clone();b.Blocked=(int[])Blocked.Clone();b.Actions=(int[])Actions.Clone();b.FellRound=(int[])FellRound.Clone();b.ComboStart=(int[])ComboStart.Clone();b.Combos=(int[])Combos.Clone();return b;
    }
}
public partial class FarmGame
{
    private static bool ValidBattleNote(BattleNote b)
    {
        if(b!=null){b.Picnic=CleanPicnic(b.Picnic);b.PicnicStart=Math.Max(0,b.PicnicStart);b.PicnicTriggers=Math.Max(0,b.PicnicTriggers);b.PicnicHealingStart=Math.Max(0,b.PicnicHealingStart);b.PicnicHealing=Math.Max(0,b.PicnicHealing);b.Inspirations=InspirationArray(b.Inspirations);b.InspirationStart=InspirationArray(b.InspirationStart);b.InspirationTriggers=InspirationArray(b.InspirationTriggers);}
        if(b==null||b.Station<0||b.Station>4||b.Station==2||b.Rounds<0||b.Rounds>60||b.Team==null||b.Team.Length!=3||b.Team.Any(p=>p<0||p>=PetCount)||b.Team.Distinct().Count()!=3||b.Names==null||b.Names.Length!=3||b.Names.Any(n=>String.IsNullOrEmpty(n)))return false;
        if(new[]{b.Levels,b.Skills,b.StartHp,b.EndHp,b.MaxHp}.Any(a=>a==null||a.Length!=3||a.Any(x=>x<0))||b.Levels.Any(x=>x<1||x>10)||b.Skills.Any(x=>x>1))return false;
        if(new[]{b.Damage,b.SkillHealing,b.Taken,b.Blocked,b.Actions,b.FellRound}.Any(a=>a==null||a.Length!=PetCount||a.Any(x=>x<0)))return false;
        return new[]{b.ComboStart,b.Combos}.All(a=>a!=null&&a.Length==6&&a.All(x=>x>=0))&&new[]{"ongoing","victory","defeat","retreat"}.Contains(b.Outcome)&&new[]{b.Direct,b.Healing,b.EnemyHealing,b.EnemyRemaining,b.EnemyStart,b.Burn,b.Poison,b.BurnStart,b.PoisonStart}.All(x=>x>=0);
    }
    private static BattleNote[] CleanBattleNotes(BattleNote[] notes){return (notes??new BattleNote[0]).Where(b=>ValidBattleNote(b)&&b.Outcome!="ongoing").GroupBy(b=>b.Station).Select(g=>g.First()).OrderBy(b=>b.Station).Take(4).ToArray();}
    private void NormalizeBattleReview()
    {
        R.Battles=CleanBattleNotes(R.Battles);
        if(!ValidBattleNote(R.ActiveBattle)||R.ActiveBattle.Outcome!="ongoing"||R.ActiveBattle.Station!=R.Node||!R.TraceBattles)R.ActiveBattle=null;
    }
    private int[] BattleCombos(){return new[]{R.WaterTriggers,R.ChainTriggers,R.ShieldTriggers,R.SpreadTriggers,R.PoisonTriggers,R.CatalystTriggers};}
    private void StartBattleReview()
    {
        if(!R.Casual||R.LegacyRewards||!R.TraceBattles)return;
        R.ActiveBattle=new BattleNote{Inspirations=(int[])R.Inspirations.Clone(),InspirationStart=(int[])R.InspirationTriggers.Clone(),Station=R.Node,Team=(int[])S.Team.Clone(),Names=S.Team.Select(Name).ToArray(),Levels=S.Team.Select(Level).ToArray(),Skills=S.Team.Select(p=>R.Skills[p]).ToArray(),StartHp=S.Team.Select(p=>R.Hp[p]).ToArray(),EndHp=S.Team.Select(p=>R.Hp[p]).ToArray(),MaxHp=S.Team.Select(MaxHp).ToArray(),ComboStart=BattleCombos(),BurnStart=R.BurnDamage,PoisonStart=R.PoisonDamage};
        R.ActiveBattle.Picnic=R.Picnic;R.ActiveBattle.PicnicStart=R.PicnicTriggers;R.ActiveBattle.PicnicHealingStart=R.PicnicHealing;
    }
    private void RefreshBattleReview()
    {
        var b=R.ActiveBattle;if(b==null)return;b.Rounds=R.Round;b.EndHp=b.Team.Select(p=>R.Hp[p]).ToArray();b.EnemyRemaining=R.EnemyHp.Take(R.EnemyCount).Sum();
        b.Burn=Math.Max(0,R.BurnDamage-b.BurnStart);b.Poison=Math.Max(0,R.PoisonDamage-b.PoisonStart);var counters=BattleCombos();b.Combos=Enumerable.Range(0,6).Select(i=>Math.Max(0,counters[i]-b.ComboStart[i])).ToArray();
        b.InspirationTriggers=Enumerable.Range(0,InspirationCount).Select(i=>Math.Max(0,R.InspirationTriggers[i]-b.InspirationStart[i])).ToArray();
        b.PicnicTriggers=Math.Max(0,R.PicnicTriggers-b.PicnicStart);b.PicnicHealing=Math.Max(0,R.PicnicHealing-b.PicnicHealingStart);
    }
    private void CloseBattleReview(bool won)
    {
        var b=R.ActiveBattle;if(b==null)return;RefreshBattleReview();b.Outcome=won?"victory":R.Withdrew?"retreat":"defeat";
        b.Reason=won?"cleared":R.Withdrew?"retreat":Front()<0?"exhausted":R.Round>=60?"round-limit":"ended";
        R.Battles=R.Battles.Where(n=>n.Station!=b.Station).Concat(new[]{b.Copy()}).OrderBy(n=>n.Station).Take(4).ToArray();R.ActiveBattle=null;
    }
    public BattleNote[] CurrentBattleNotes(){RefreshBattleReview();return R.Battles.Concat(R.ActiveBattle==null?new BattleNote[0]:new[]{R.ActiveBattle}).Select(b=>b.Copy()).ToArray();}
    public static string BattleOutcome(BattleNote b){return b.Outcome=="victory"?"通关":b.Outcome=="retreat"?"提前返家":b.Outcome=="ongoing"?"进行中":b.Reason=="round-limit"?"达到回合上限":"暂未通关";}
    public static int ReviewCarePet(BattleNote b)
    {
        return b.Team.Where(p=>b.FellRound[p]>0).OrderBy(p=>b.FellRound[p]).DefaultIfEmpty(b.Team[0]).First();
    }
    public static string BattleObservation(BattleNote b)
    {
        string facts=b.Outcome=="victory"?"这场用了 "+b.Rounds+" 回合，击败了全部敌人。":b.Outcome=="retreat"?"你在这场提前返家，已经获得的奖励保留。":b.Outcome=="ongoing"?"正在第 "+b.Rounds+" 回合，数据会随战斗继续更新。":b.Reason=="round-limit"?"达到 60 回合上限时，敌人还剩 "+b.EnemyRemaining+" 体力。":(b.Reason=="exhausted"?"队伍体力耗尽时":"这场结束时")+"，敌人还剩 "+b.EnemyRemaining+" 体力。";
        int fallen=ReviewCarePet(b);if(b.FellRound[fallen]>0){int index=Array.IndexOf(b.Team,fallen);facts+="\n"+b.Names[index]+"在第 "+b.FellRound[fallen]+" 回合耗尽体力；可检查前排保护与培养。";}
        if(b.EnemyHealing>0)facts+="\n敌人实际回复了 "+b.EnemyHealing+" 体力；持续伤害与集中输出有助于应对再生。";
        int lightning=Array.IndexOf(b.Team,2),seed=Array.IndexOf(b.Team,4),shell=Array.IndexOf(b.Team,3);
        if(lightning>=0&&b.Skills[lightning]==0&&b.Actions[2]>0&&b.Combos[0]==0)facts+="\n电团出手 "+b.Actions[2]+" 次，没有触发潮湿追击。可检查泡泡站位或雷雨云祝福。";
        if(seed>=0&&b.Skills[seed]==1&&b.Actions[4]>0&&b.Combos[5]==0)facts+="\n种子出手 "+b.Actions[4]+" 次，没有触发余烬催化。可检查赤钳站位或灼烧来源。";
        if(shell>=0&&b.Skills[shell]==1&&b.Actions[3]>0&&b.Combos[2]==0)facts+="\n苔壳出手 "+b.Actions[3]+" 次，没有触发护盾重击。可检查前排站位和护盾来源。";
        if(b.Combos[0]>0||b.Combos[5]>0||b.Combos[2]>0)facts+="\n已触发："+String.Join("、",new[]{b.Combos[0]>0?"潮湿追击 "+b.Combos[0]+" 次":null,b.Combos[5]>0?"余烬催化 "+b.Combos[5]+" 次":null,b.Combos[2]>0?"护盾重击 "+b.Combos[2]+" 次":null}.Where(s=>s!=null).ToArray())+"。";
        return facts;
    }
}
