using System;
using System.Linq;

public partial class FarmGame
{
    public static readonly string[] DepthNames={"初探","深入","秘境"};
    public static readonly string[] RelicNames={"晨露瓶","回响铃","丰收袋","余烬石","守护芽","催生花"};
    public static readonly string[] RelicDetails={
        "伙伴攻击潮湿目标时，直接攻击伤害 +3。适合泡泡带队。",
        "每次闪电跳跃，为当前前排增加 4 护盾。适合水雷连锁。",
        "每场胜利额外带回肥料 1；一趟完整旅程多得 4。",
        "灼烧结算后，若目标仍存活，追加 2 层中毒。可与种子配合。",
        "伙伴受到攻击时，若护盾挡住了伤害且仍存活，为自己恢复 2 体力。",
        "每次施加中毒时，额外叠加 2 层。适合种子或余烬石。"};
    public static readonly string[] ChapterNames={"第一份收成","伙伴长大啦","第一次远足","照料好土地","迎接新形态","让信物发挥作用","种子的第三形态","走遍六处旅途"};
    public static readonly string[] ChapterDetails={"自动收获至少 1 份作物。","任一伙伴达到 Lv.2。","通关小溪初探。","将农田土壤升到 Lv.1。","泡泡或种子完成一次进化。","携带至少一件信物，赢下一趟冒险。","种子进化到第三阶。","通关两张地图的全部三档深度。"};
    public static readonly int[][] ChapterRewards={new[]{4,3,0,0,0},new[]{0,6,0,0,0},new[]{0,0,0,3,0},new[]{6,6,0,0,0},new[]{0,0,0,0,1},new[]{0,0,0,6,0},new[]{0,0,2,8,0},new[]{0,0,0,12,4}};
    private static int[] FixedInts(int[] value,int length,int fill=0)
    {
        var result=Enumerable.Repeat(fill,length).ToArray();if(value!=null)Array.Copy(value,result,Math.Min(value.Length,length));return result;
    }
    private void NormalizeProgression()
    {
        S.ExpeditionClears=FixedInts(S.ExpeditionClears,6);for(int i=0;i<6;i++)S.ExpeditionClears[i]=Math.Max(0,Math.Min(99999,S.ExpeditionClears[i]));
        if(S.ChapterClaimed==null||S.ChapterClaimed.Length!=ChapterNames.Length){var old=S.ChapterClaimed;S.ChapterClaimed=new bool[ChapterNames.Length];if(old!=null)Array.Copy(old,S.ChapterClaimed,Math.Min(old.Length,S.ChapterClaimed.Length));}
        if(S.ProgressionVersion<1){
            if(S.GardenUnlocked||(S.Decorations!=null&&S.Decorations.Take(3).Any(x=>x)))S.ExpeditionClears[0]=Math.Max(1,S.ExpeditionClears[0]);
            if(S.Decorations!=null&&S.Decorations.Skip(3).Any(x=>x))S.ExpeditionClears[3]=Math.Max(1,S.ExpeditionClears[3]);
            S.ProgressionVersion=1;
        }
        S.EquippedRelics=FixedInts(S.EquippedRelics,2,-1);
        for(int i=0;i<2;i++)if(!RelicOwned(S.EquippedRelics[i])||i==1&&S.EquippedRelics[i]==S.EquippedRelics[0])S.EquippedRelics[i]=-1;
        S.RelicVictories=Math.Max(0,S.RelicVictories);
        if(R==null)return;R.Depth=Math.Max(0,Math.Min(2,R.Depth));R.NewRelic=R.NewRelic>=0&&R.NewRelic<6?R.NewRelic:-1;
        R.Relics=FixedInts(R.Relics,2,-1);for(int i=0;i<2;i++)if(R.Relics[i]<-1||R.Relics[i]>=6||i==1&&R.Relics[i]==R.Relics[0])R.Relics[i]=-1;
        R.RelicTriggers=FixedInts(R.RelicTriggers,6);for(int i=0;i<6;i++)R.RelicTriggers[i]=Math.Max(0,R.RelicTriggers[i]);
    }
    public static string ExpeditionName(int region,int depth){return (region==0?"小溪":"洞穴")+" · "+DepthNames[depth];}
    public bool ExpeditionUnlocked(int region,int depth){return region>=0&&region<2&&depth>=0&&depth<3&&(region==0||CaveUnlocked)&&(depth==0||S.ExpeditionClears[region*3+depth-1]>0);}
    public string ExpeditionGate(int region,int depth){if(region==1&&!CaveUnlocked)return "泡泡或种子首次进化后开放。";return depth>0?"先通关「"+ExpeditionName(region,depth-1)+"」。":"可以出发。";}
    public static string ExpeditionRule(int depth){return depth==0?"普通旅途：熟悉技能组合，收集肥料与装饰。":depth==1?"硬壳敌人：每次直接伤害额外减少 2；灼烧与中毒不受这层硬壳影响。":"再生敌人：每回合开始恢复体力（小溪 4 / 洞穴 6），需要持续施压。";}
    public static string ExpeditionLevel(int region,int depth){return new[]{"Lv.1～3","Lv.3～4","Lv.5～6","Lv.4～5","Lv.6～7","Lv.8～10"}[region*3+depth];}
    public static int ExpeditionFertilizer(int region,int depth){return 7+7*region+6*depth;}
    public static int ExpeditionCrystals(int region,int depth){return 2+region+depth;}
    public bool RelicOwned(int id){return id>=0&&id<6&&S.ExpeditionClears[id]>0;}
    public bool EquipRelic(int id,int slot)
    {
        if(!AtHome||slot<0||slot>1||id< -1||id>=6||id>=0&&!RelicOwned(id))return false;
        if(id>=0){int previous=Array.IndexOf(S.EquippedRelics,id);if(previous>=0&&previous!=slot)S.EquippedRelics[previous]=S.EquippedRelics[slot];}
        S.EquippedRelics[slot]=id;S.Notice=id<0?"已腾出信物位置。":"已把「"+RelicNames[id]+"」放入行囊，下趟冒险生效。";Save();return true;
    }
    private bool HasRelic(int id){return R.Casual&&R.Relics.Contains(id);}
    private void RelicProc(int id){R.RelicTriggers[id]++;}
    public string RelicLoadout(int[] relics){var names=relics.Where(i=>i>=0&&i<6).Select(i=>RelicNames[i]).ToArray();return names.Length==0?"暂未携带信物":String.Join(" + ",names);}
    public string RelicReport{get{return String.Join("\n",R.Relics.Where(i=>i>=0).Select(i=>RelicNames[i]+"：触发 "+R.RelicTriggers[i]+" 次").ToArray());}}
    public string RelicAdvice(int id)
    {
        if(id==0)return S.Team.Contains(0)?"泡泡能稳定制造潮湿。":"需要潮湿：可带泡泡或雷雨云祝福。";
        if(id==1)return S.Team.Contains(2)&&S.Skills[2]==0?"电团跳跃电弧可以把攻击转成保护。":"建议携带电团的「跳跃电弧」。";
        if(id==2)return "适合重走已熟悉的地图，帮助农田升级。";
        if(id==3)return S.Team.Contains(1)?"赤钳可提供持续灼烧。":"需要灼烧：可带赤钳或温暖篝火。";
        if(id==4)return S.Team.Contains(3)?"苔壳能持续补盾，适合保护前排。":"建议带苔壳或厚叶帐篷提供护盾。";
        return S.Team.Contains(4)||S.EquippedRelics.Contains(3)?"当前配置可以施加中毒。":"需要种子或余烬石来施加中毒。";
    }
    private void AwardExpeditionClear()
    {
        int id=R.Difficulty*3+R.Depth;bool first=S.ExpeditionClears[id]==0;S.ExpeditionClears[id]=Math.Min(99999,S.ExpeditionClears[id]+1);
        if(R.Relics.Any(i=>i>=0))S.RelicVictories++;
        if(first){R.NewRelic=id;int slot=Array.IndexOf(S.EquippedRelics,-1);if(slot>=0)S.EquippedRelics[slot]=id;}
    }
    private int RelicAttackBonus(int enemy){if(!HasRelic(0)||R.Wet[enemy]<=0)return 0;RelicProc(0);return 3;}
    private void RelicChainShield(){if(HasRelic(1)&&Front()>=0){AddShield(Front(),4);RelicProc(1);}}
    private void JourneyEnemyHit(int pet,int damage)
    {
        if(pet<0)return;int shield=R.Shield[pet],hp=R.Hp[pet];HitPet(pet,damage);
        if(R.ActiveBattle!=null){R.ActiveBattle.Taken[pet]+=Math.Max(0,hp-R.Hp[pet]);R.ActiveBattle.Blocked[pet]+=Math.Max(0,shield-R.Shield[pet]);if(hp>0&&R.Hp[pet]==0&&R.ActiveBattle.FellRound[pet]==0)R.ActiveBattle.FellRound[pet]=R.Round;}
        if(HasRelic(4)&&shield>R.Shield[pet]&&R.Hp[pet]>0&&R.Hp[pet]<MaxHp(pet)){JourneyHeal(pet,2,false);RelicProc(4);}
    }
    private void RegenerateExpeditionEnemies()
    {
        if(R.Depth!=2)return;for(int e=0;e<R.EnemyCount;e++)if(R.EnemyHp[e]>0){int before=R.EnemyHp[e];R.EnemyHp[e]=Math.Min(R.EnemyMax[e],R.EnemyHp[e]+(R.Difficulty==0?4:6));if(R.ActiveBattle!=null)R.ActiveBattle.EnemyHealing+=R.EnemyHp[e]-before;}
    }
    public bool ChapterDone(int chapter)
    {
        switch(chapter){case 0:return S.Harvests>0;case 1:return S.Xp.Any(x=>x>=20);case 2:return S.ExpeditionClears[0]>0;case 3:return S.SoilLevel>0;case 4:return CaveUnlocked;case 5:return S.RelicVictories>0;case 6:return S.SeedStage>=2;case 7:return S.ExpeditionClears.All(x=>x>0);default:return false;}
    }
    public string ChapterProgress(int chapter)
    {
        switch(chapter){case 0:return "收成 "+Math.Min(1,S.Harvests)+" / 1";case 1:return "最高 Lv."+Enumerable.Range(0,PetCount).Max(p=>Level(p))+" / 2";case 2:return "通关 "+Math.Min(1,S.ExpeditionClears[0])+" / 1";case 3:return "土壤 Lv."+S.SoilLevel+" / 1";case 4:return CaveUnlocked?"已迎接新形态":"等待首次进化";case 5:return "携信物通关 "+Math.Min(1,S.RelicVictories)+" / 1";case 6:return "形态 "+(S.SeedStage+1)+" / 3";default:return "探索 "+S.ExpeditionClears.Count(x=>x>0)+" / 6";}
    }
    public static string ChapterRewardText(int chapter){var names=new[]{"莓果","麦穗","薄荷","肥料","晶石"};return String.Join(" · ",Enumerable.Range(0,5).Where(i=>ChapterRewards[chapter][i]>0).Select(i=>names[i]+" +"+ChapterRewards[chapter][i]).ToArray());}
    public int ClaimableChapters{get{return Enumerable.Range(0,ChapterNames.Length).Count(i=>!S.ChapterClaimed[i]&&ChapterDone(i));}}
    public bool ClaimChapter(int chapter)
    {
        if(chapter<0||chapter>=ChapterNames.Length||S.ChapterClaimed[chapter]||!ChapterDone(chapter))return false;
        int[] before={S.Berries,S.Grain,S.Mint,S.Fertilizer,S.Crystals};var r=ChapterRewards[chapter];S.Berries=Math.Min(99999,S.Berries+r[0]);S.Grain=Math.Min(99999,S.Grain+r[1]);S.Mint=Math.Min(99999,S.Mint+r[2]);S.Fertilizer=Math.Min(99999,S.Fertilizer+r[3]);S.Crystals=Math.Min(99999,S.Crystals+r[4]);
        int[] after={S.Berries,S.Grain,S.Mint,S.Fertilizer,S.Crystals};string[] names={"莓果","麦穗","薄荷","肥料","晶石"};
        string received=String.Join(" · ",Enumerable.Range(0,5).Where(i=>after[i]>before[i]).Select(i=>names[i]+" +"+(after[i]-before[i])));
        S.ChapterClaimed[chapter]=true;S.Notice="成长手册 · "+ChapterNames[chapter]+"："+(received==""?"仓库已满，礼物已记为收好。":received+"，已入库。");Save();return true;
    }
    public void ClaimReadyChapters(){int count=0;for(int i=0;i<ChapterNames.Length;i++)if(ClaimChapter(i))count++;if(count>0){S.Notice="已领取 "+count+" 项成长奖励，全部放入仓库。";Save();}}
    public string NextChapterHint{get{int next=Enumerable.Range(0,ChapterNames.Length).FirstOrDefault(i=>!S.ChapterClaimed[i]);if(S.ChapterClaimed.All(x=>x))return "成长手册已完成，可以继续尝试新配队与收集装饰。";return ChapterDone(next)?"「"+ChapterNames[next]+"」已完成，去手册领取奖励。":ChapterNames[next]+"："+ChapterDetails[next];}}
    public int TrainToNextCost(int pet){return pet>=0&&pet<PetCount&&Level(pet)<10?((NextXp(pet)-S.Xp[pet]+17)/18)*3:0;}
    public bool TrainToNext(int pet)
    {
        int cost=TrainToNextCost(pet);if(!AtHome||cost<=0||S.Grain<cost)return false;
        S.Grain-=cost;S.Xp[pet]+=cost/3*18;CompleteSmallActivity("train",pet);S.Notice=Name(pet)+"升至 Lv."+Level(pet)+"，消耗麦穗 "+cost+"。训练不增加亲密。";Save();return true;
    }
}
