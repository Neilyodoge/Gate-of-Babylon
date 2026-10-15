using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public partial class FarmGame
{
    private bool batchSaving,savePending;
    public static readonly string[] BlessingNames={"雷雨云","厚叶帐篷","温暖篝火","野餐篮","追风羽","园丁福袋"};
    public static readonly string[] BlessingDetails={"每场开场全体敌人潮湿；连锁闪电额外跳跃一次。","每场开场全队护盾 +10，适合蓄盾与盾击。","每场开场全体敌人灼烧 +3；赤钳每次多叠 2 层灼烧。","每场胜利，全队额外恢复 12 体力。","每场前两回合，全队攻击 +5，快速解决小怪。","每场胜利额外获得肥料 1，适合回头刷熟悉的地图。"};
    public static readonly string[] DecorationNames={"溪边风车","蘑菇灯","河岸花盆","潮汐水晶","贝壳风铃","月光灯"};
    public static readonly string[][] SkillNames={new[]{"涟漪","雨露"},new[]{"余烬蔓延","炽焰"},new[]{"跳跃电弧","余热回流"},new[]{"厚叶","护盾重击"},new[]{"潮露播种","余烬催化"}};
    public static readonly string[][] SkillDetails={
        new[]{"水泡也让另一名敌人潮湿，方便电团连续追击。","水泡后治疗最虚弱伙伴 6；溢出的治疗转为前排护盾。"},
        new[]{"带灼烧的敌人倒下时，把灼烧传给下一个目标。","每次钳击额外叠加 4 层灼烧，适合消耗首领。"},
        new[]{"攻击潮湿目标后，闪电跳向另一名敌人；击杀时再跳一次。","攻击灼烧目标后治疗最虚弱伙伴 7，并加深该目标灼烧 2。"},
        new[]{"全队护盾每回合保留 75%，苔壳提供的护盾增加 4。","行动时消耗自身最多 12 护盾，每点追加 2 伤害；之后仍治疗和加盾。"},
        new[]{"毒种攻击后叠加中毒；潮湿目标额外叠加 3 层。","毒种攻击灼烧目标，消耗最多 4 层毒素，每层追加 3 伤害。"}};
    public bool JourneyRunning{get{return R.Casual&&!R.JourneyPaused&&R.Phase!="home"&&R.Phase!="result";}}
    public int SoilCost{get{return 4+S.SoilLevel*4;}}
    public string ComboReport{get{return "潮湿追击 "+R.WaterTriggers+" 次 · 闪电跳跃 "+R.ChainTriggers+" 次\n护盾重击 "+R.ShieldTriggers+" 次 · 灼烧蔓延 "+R.SpreadTriggers+" 次\n灼烧造成 "+R.BurnDamage+" 伤害 · 实际治疗 "+R.Recovery+" 体力"+(S.Team.Contains(4)||R.PoisonTriggers>0?"\n中毒施加 "+R.PoisonTriggers+" 次 · 中毒伤害 "+R.PoisonDamage+"\n毒素传递 "+R.PoisonSpreads+" 次 · 余烬催化 "+R.CatalystTriggers+" 次":"");}}
    public string JourneyStatus{get{return R.Phase=="result"?"旅程结束 · 查看收获后返家":R.JourneyPaused?"旅程已暂停 · 点继续后再前进":"第 "+(R.Node+1)+" / 5 站 · "+(R.Phase=="inspiration"?"挑选旅途灵感":R.Phase=="battle"?"自动战斗中":R.Node==2?"自动休息中":"正在前往下一站");}}
    private void ValidateJourneyState()
    {
        S.Fertilizer=Math.Max(0,Math.Min(99999,S.Fertilizer));S.SoilLevel=Math.Max(0,Math.Min(5,S.SoilLevel));
        if(S.Skills==null||S.Skills.Length!=PetCount)S.Skills=new int[PetCount];for(int i=0;i<PetCount;i++)S.Skills[i]=Math.Max(0,Math.Min(1,S.Skills[i]));
        if(S.Decorations==null||S.Decorations.Length!=6)S.Decorations=new bool[6];
        if(S.DisplayDecoration<0||S.DisplayDecoration>=6||!S.Decorations[S.DisplayDecoration])S.DisplayDecoration=-1;
        if(S.BlessingOffers==null||S.BlessingOffers.Length!=3||S.BlessingOffers.Any(x=>x<0||x>5)||S.BlessingOffers.Distinct().Count()!=3)S.BlessingOffers=new int[0];
        if(R.Skills==null||R.Skills.Length!=PetCount)R.Skills=new int[PetCount];for(int i=0;i<PetCount;i++)R.Skills[i]=Math.Max(0,Math.Min(1,R.Skills[i]));
        if(R.Blessing< -1||R.Blessing>5)R.Blessing=-1;
        if(R.Casual&&(R.NextJourneyTicks<=0||R.NextJourneyTicks>DateTime.UtcNow.AddDays(1).Ticks))R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(4).Ticks;
    }
    public bool SkillUnlocked(int pet,int skill){return pet>=0&&pet<PetCount&&(skill==0||skill==1&&Level(pet)>=2);}
    public bool HerbalFeed(int pet){if(!AtHome||!S.GardenUnlocked||pet<0||pet>=PetCount||Level(pet)>=10||S.Mint<1)return false;S.Mint--;S.Xp[pet]+=24;CompleteSmallActivity("mint",pet);S.Notice=Name(pet)+"吃了薄荷点心，成长 +24。";Save();return true;}
    public bool SetSkill(int pet,int skill){if(!AtHome||!SkillUnlocked(pet,skill))return false;S.Skills[pet]=skill;S.Notice=Name(pet)+"已携带「"+SkillNames[pet][skill]+"」，下趟冒险生效。";Save();return true;}
    public bool CanUsePreset(int preset){return preset==0||preset==1&&Level(0)>=2&&Level(3)>=2||preset==2&&Level(2)>=2;}
    public bool SetPreset(int preset)
    {
        if(!AtHome||preset<0||preset>2||!CanUsePreset(preset))return false;TickFarm(DateTime.UtcNow);S.Skills=new int[PetCount];
        if(preset==0)S.Team=new[]{0,2,3};
        if(preset==1){S.Team=new[]{3,0,1};S.Skills[0]=1;S.Skills[3]=1;}
        if(preset==2){S.Team=new[]{1,2,3};S.Skills[2]=1;}
        S.Notice="已配好「"+new[]{"水雷连锁","护盾重击","灼烧蔓延"}[preset]+"」，可继续自由调整。";Save();return true;
    }
    public void EnsureJourneyOffers(){if(!AtHome||S.BlessingOffers.Length==3)return;var pool=Enumerable.Range(0,6).ToList();var result=new List<int>();while(result.Count<3){int n=Random(pool.Count);result.Add(pool[n]);pool.RemoveAt(n);}S.BlessingOffers=result.ToArray();Save();}
    public bool BeginJourney(int difficulty,int blessing,int depth=0,int crystalPet=-1,int crystalStage=-1)
    {
        if((crystalPet!=-1||crystalStage!=-1)&&!ValidCrystalTarget(crystalPet,crystalStage))return false;
        if(!AtHome||!S.BlessingOffers.Contains(blessing)||!ExpeditionUnlocked(difficulty,depth)||!CanPackPicnic(S.PlannedPicnic,!practiceOnly))return false;
        int picnic=S.PlannedPicnic;bool wasBatch=batchSaving;batchSaving=true;
        try{
            if(!Begin(difficulty,0))return false;
            R.CrystalTargetPet=crystalPet;R.CrystalTargetStage=crystalStage;
            R.Casual=true;R.InspirationsEnabled=true;R.InspirationStyle=S.InspirationStyle;R.TraceBattles=true;R.Blessing=blessing;R.Depth=depth;R.Relics=(int[])S.EquippedRelics.Clone();R.Skills=(int[])S.Skills.Clone();R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(6).Ticks;
            if(!practiceOnly){S.PlannedRegion=difficulty;S.PlannedDepth=depth;}
            PackPicnic(picnic);S.BlessingOffers=new int[0];R.Log="带上「"+BlessingNames[blessing]+"」出发。小队会自动战斗、休息并走完整趟。"+(picnic>=0?"\n便当："+PicnicNames[picnic]+"，本趟生效。":"");S.Notice="旅程开始，隐藏桌宠也会继续。";Save();return true;
        }finally{batchSaving=wasBatch;if(!wasBatch&&savePending){savePending=false;Save();}}
    }
    public bool AdoptLegacyJourney()
    {
        if(AtHome||R.Casual||R.Phase=="result")return false;
        R.Casual=true;R.LegacyRewards=true;R.Skills=(int[])S.Skills.Clone();R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(4).Ticks;
        S.Notice="已接着自动走完旧旅程。原节点、体力、能力与已获奖励保留；待选能力将从原选项中自动选择。";Save();return true;
    }
    public void ToggleJourneyPause(){if(!R.Casual||AtHome||R.Phase=="result")return;R.JourneyPaused=!R.JourneyPaused;R.NextJourneyTicks=DateTime.UtcNow.AddSeconds(4).Ticks;Save();}
    public int AdvanceJourney(DateTime now)
    {
        if(!JourneyRunning)return 0;int steps=0;long ticks=now.ToUniversalTime().Ticks;
        if(R.NextJourneyTicks<=0){R.NextJourneyTicks=ticks+TimeSpan.FromSeconds(4).Ticks;Save();return 0;}
        batchSaving=true;try{
            while(JourneyRunning&&R.NextJourneyTicks<=ticks&&steps<400){
                if(R.Phase=="battle")Step();
                else if(R.Phase=="route")RouteChoice(0);
                else if(R.Phase=="inspiration")ChooseInspiration(RecommendedInspiration,R.Node,false);
                else if(R.Phase=="draft"){if(R.Offers.Length>0)Pick(R.Offers.OrderBy(x=>x==4?0:x==3?1:x==1?2:3).First());else{R.Node=Math.Min(4,R.Node+1);R.Phase="route";}}
                else break;
                steps++;R.NextJourneyTicks+=TimeSpan.FromSeconds(R.Phase=="battle"?4:R.Phase=="inspiration"?18:8).Ticks;savePending=true;
            }
        }finally{batchSaving=false;if(savePending){savePending=false;Save();}}
        return steps;
    }
    public bool UpgradeSoil()
    {
        if(S.SoilLevel>=5||S.Fertilizer<SoilCost)return false;TickFarm(DateTime.UtcNow);
        var receipt=new FarmSoilReceipt{BeforeLevel=S.SoilLevel,Cost=SoilCost,BeforeBerrySeconds=CropSeconds(0)};
        S.Fertilizer-=receipt.Cost;S.SoilLevel++;receipt.AfterLevel=S.SoilLevel;receipt.AfterBerrySeconds=CropSeconds(0);S.LastSoilUpgrade=receipt;
        S.Notice="土壤升到 "+S.SoilLevel+" 级，全田生长速度累计 +"+(S.SoilLevel*12)+"%。";Save();return true;
    }
    public bool EquipDecoration(int decoration){if(decoration< -1||decoration>=6||decoration>=0&&!S.Decorations[decoration])return false;S.DisplayDecoration=decoration;S.Notice=decoration<0?"已收起桌宠随身摆件。":"桌宠带上了「"+DecorationNames[decoration]+"」。小院布置可单独调整。";Save();return true;}
    private bool JourneySkill(int pet,int skill){return S.Team.Contains(pet)&&R.Hp[pet]>0&&R.Skills[pet]==skill;}
    private void AddShield(int pet,int amount){if(pet>=0)R.Shield[pet]=Math.Min(MaxHp(pet),R.Shield[pet]+amount);}
    private int JourneyHeal(int pet,int amount,bool overflow,int source=-1)
    {
        if(pet<0)return 0;int actual=Math.Min(amount,MaxHp(pet)-R.Hp[pet]);int before=R.Hp[pet];Heal(pet,amount);if(R.ActiveBattle!=null){int restored=Math.Max(0,R.Hp[pet]-before);R.ActiveBattle.Healing+=restored;if(source>=0&&source<PetCount)R.ActiveBattle.SkillHealing[source]+=restored;}R.Recovery+=actual;if(overflow)AddShield(Front(),amount-actual);return actual;
    }
    private int JourneyHit(int enemy,int damage,bool ongoing=false,int source=-1)
    {
        if(enemy<0||R.EnemyHp[enemy]<=0)return 0;int before=R.EnemyHp[enemy];int armor=(R.EnemyKind[enemy]==2?2:0)+(R.Depth==1&&!ongoing?2:0);R.EnemyHp[enemy]=Math.Max(0,before-Math.Max(1,damage-armor));
        if(R.ActiveBattle!=null&&!ongoing){int actual=before-R.EnemyHp[enemy];R.ActiveBattle.Direct+=actual;if(source>=0&&source<PetCount)R.ActiveBattle.Damage[source]+=actual;}
        if(R.EnemyHp[enemy]==0){R.Kills++;if(S.SeedStage>=3&&S.Team.Contains(4)&&R.Poison[enemy]>0){int next=FirstEnemy();if(next>=0){AddPoison(next,Math.Max(1,R.Poison[enemy]/2));R.PoisonSpreads++;}}if(R.Perks[11]>0&&JourneyHeal(Weakest(),10*R.Perks[11],false)>0)InspirationProc(8);
            if(R.Burn[enemy]>0&&JourneySkill(1,0)){int next=FirstEnemy();if(next>=0){R.Burn[next]=Math.Min(40,R.Burn[next]+Math.Max(2,R.Burn[enemy]));R.SpreadTriggers++;R.LastCombo="余烬蔓延！灼烧转移到"+EnemyName(next);}}}
        return before-R.EnemyHp[enemy];
    }
    private void ApplyBattleBlessing()
    {
        if(S.SeedStage>=3&&S.Team.Contains(4))for(int e=0;e<R.EnemyCount;e++)AddPoison(e,2);
        if(R.Difficulty==1)for(int i=0;i<R.EnemyCount;i++)R.EnemyHp[i]=R.EnemyMax[i]=(int)Math.Ceiling(R.EnemyMax[i]*1.35);
        if(R.Depth>0)for(int i=0;i<R.EnemyCount;i++)R.EnemyHp[i]=R.EnemyMax[i]=(int)Math.Ceiling(R.EnemyMax[i]*(1+R.Depth*.32));
        if(R.Blessing==0)for(int i=0;i<R.EnemyCount;i++)R.Wet[i]=3;
        if(R.Blessing==1)foreach(int p in S.Team)AddShield(p,10);
        if(R.Blessing==2)for(int i=0;i<R.EnemyCount;i++)R.Burn[i]=3;
        PicnicOpening();
    }
    private bool StepJourneyBattle()
    {
        if(R.Phase!="battle")return false;R.Round++;RegenerateExpeditionEnemies();var log=new List<string>();R.LastCombo="";
        foreach(int p in S.Team)R.Shield[p]=(int)(R.Shield[p]*(JourneySkill(3,0)?.75:.5));
        int front=Front();if(front<0){Finish(false);Save();return true;}InspirationShield(2,front,R.Perks[3]*4);PicnicRound();
        foreach(int p in S.Team){
            if(R.Hp[p]<=0)continue;int e=FirstEnemy();if(e<0)break;R.LastActor=p;if(R.ActiveBattle!=null)R.ActiveBattle.Actions[p]++;int damage=Attack(p)+(R.Round==1?R.Perks[7]*5:0)+(R.Blessing==4&&R.Round<=2?5:0)+RelicAttackBonus(e);
            if(R.Round==1)InspirationProc(6);
            if(p==0){JourneyHit(e,damage,false,p);if(R.EnemyHp[e]>0)R.Wet[e]=3;int other=Enumerable.Range(0,R.EnemyCount).FirstOrDefault(x=>x!=e&&R.EnemyHp[x]>0);
                if((R.Skills[p]==0||R.Perks[10]>0)&&other!=e&&R.EnemyHp[other]>0)R.Wet[other]=3;
                if(R.Skills[p]==1)JourneyHeal(Weakest(),6,true,p);log.Add(Name(p)+" · 水泡 "+damage+(R.Skills[p]==0?"，涟漪铺开":"，雨露治疗 / 溢出转盾"));}
            if(p==1){int priorBurn=R.Burn[e];int burn=3+R.Perks[5]*3+(R.Skills[p]==1?4:0)+(R.Blessing==2?2:0);R.Burn[e]=Math.Min(40,priorBurn+burn+(HasPicnic(4)?2:0));if(HasPicnic(4)&&priorBurn+burn<40)PicnicProc();if(R.Burn[e]>priorBurn)InspirationProc(4);JourneyHit(e,damage,false,p);log.Add("赤钳 · 钳击 "+damage+"，叠加灼烧");}
            if(p==2){bool wet=R.Wet[e]>0,burning=R.Burn[e]>0;if(wet){damage+=5+R.Perks[1]*5;if(HasPicnic(3)){damage+=3;PicnicProc();}R.WaterTriggers++;InspirationProc(0);InspirationShield(11,Front(),3*R.Inspirations[11]);}JourneyHit(e,damage,false,p);
                if(wet&&(R.Skills[p]==0||R.Perks[2]>0)){int jumps=1+(R.EnemyHp[e]==0?1:0)+(R.Blessing==0?1:0);foreach(int x in Enumerable.Range(0,R.EnemyCount).Where(x=>x!=e&&R.EnemyHp[x]>0).Take(jumps).ToArray()){JourneyHit(x,7+R.Perks[2]*7,false,p);R.ChainTriggers++;InspirationProc(1);RelicChainShield();}R.LastCombo="潮湿连锁！闪电在敌人间跳跃";}
                if(burning&&R.Skills[p]==1){JourneyHeal(Weakest(),7,false,p);if(R.EnemyHp[e]>0)R.Burn[e]=Math.Min(40,R.Burn[e]+2);R.LastCombo="余热回流！攻击灼烧目标，为伙伴恢复体力";}log.Add("电团 · "+(wet?"潮湿追击":"电击")+" "+damage);}
            if(p==4){SeedAttack(e,damage);log.Add(Name(4)+" · 毒种 "+damage+"，施加中毒");}
            if(p==3){int consumed=R.Skills[p]==1?Math.Min(12,R.Shield[p]):0;R.Shield[p]-=consumed;if(consumed>0){damage+=consumed*2;R.ShieldTriggers++;R.LastCombo="护盾重击！消耗 "+consumed+" 护盾，追加 "+consumed*2+" 伤害";}
                JourneyHit(e,damage,false,p);if(JourneyHeal(Weakest(),5+R.Perks[4]*5,false,p)>0)InspirationProc(3);AddShield(Front(),R.Skills[p]==0?10:6);log.Add("苔壳 · 撞击 "+damage+"，治疗与前排加盾");}
        }
        if(R.Round%3==0&&R.Perks[8]>0&&JourneyHit(FirstEnemy(),10*R.Perks[8])>0)InspirationProc(7);
        for(int e=0;e<R.EnemyCount;e++)if(R.EnemyHp[e]>0&&R.Burn[e]>0){R.BurnDamage+=JourneyHit(e,R.Burn[e],true);R.Burn[e]=Math.Max(0,R.Burn[e]-1);if(HasRelic(3)&&R.EnemyHp[e]>0){AddPoison(e,2);RelicProc(3);}}
        for(int e=0;e<R.EnemyCount;e++)if(R.EnemyHp[e]>0&&R.Poison[e]>0){R.PoisonDamage+=JourneyHit(e,R.Poison[e],true);R.Poison[e]=Math.Max(0,R.Poison[e]-1);}
        if(FirstEnemy()<0){R.Log=String.Join("\n",log);WinJourneyBattle();Save();return true;}
        bool heavy=R.Round%3==0;
        for(int e=0;e<R.EnemyCount;e++){if(R.EnemyHp[e]<=0)continue;int attack=(R.EnemyKind[e]==3?12:5+R.Node)+(R.Elite?3:0)+(R.Difficulty==1?6:0)+R.Depth*(R.Difficulty==0?2:3);
            if(heavy&&R.EnemyKind[e]==3){foreach(int p in S.Team)if(R.Hp[p]>0)JourneyEnemyHit(p,attack-3);}else JourneyEnemyHit(Front(),attack+(heavy?6:0));}
        for(int e=0;e<R.EnemyCount;e++)R.Wet[e]=Math.Max(0,R.Wet[e]-1);
        log.Add(heavy?"敌人发动重击，护盾优先抵挡。":"敌人反击。" );R.Log=String.Join("\n",log);
        if(Front()<0||R.Round>=60){Finish(false);S.Notice="小队平安返家，已获得的肥料和资源保留。";}Save();return true;
    }
    private void WinJourneyBattle()
    {
        CloseBattleReview(true);
        int fertilizer=1+R.Difficulty+R.Depth+(R.Blessing==5?1:0)+(HasRelic(2)?1:0);if(HasRelic(2))RelicProc(2);S.Fertilizer=Math.Min(99999,S.Fertilizer+fertilizer);R.RewardFertilizer+=fertilizer;
        foreach(int p in S.Team)if(R.Hp[p]>0)JourneyHeal(p,7+R.Perks[0]*8+(R.Meal==2?9:0)+(R.Blessing==3?12:0),false);
        if(R.LegacyRewards&&R.Perks[9]>0){S.Berries+=2*R.Perks[9];if(R.RewardBerries>=0)R.RewardBerries+=2*R.Perks[9];}
        R.LastReward="肥料 +"+fertilizer+"，已自动入库。";
        if(R.Node==4){int extra=3+R.Difficulty*3+R.Depth*2;S.Fertilizer=Math.Min(99999,S.Fertilizer+extra);R.RewardFertilizer+=extra;int crystals=ExpeditionCrystals(R.Difficulty,R.Depth);S.Crystals+=crystals;if(R.RewardCrystals>=0)R.RewardCrystals+=crystals;
            R.UnlockedGarden=!S.GardenUnlocked;S.GardenUnlocked=true;S.Wins++;
            int decoration=Enumerable.Range(R.Difficulty*3,3).FirstOrDefault(i=>!S.Decorations[i]);
            if(decoration>=R.Difficulty*3&&decoration<R.Difficulty*3+3&&!S.Decorations[decoration])ReceiveDecoration(decoration);
            else{S.Fertilizer=Math.Min(99999,S.Fertilizer+2);R.RewardFertilizer+=2;}
            AwardExpeditionClear();Finish(true);S.Notice="旅程完成！带回肥料 "+R.RewardFertilizer+(R.DecorationReward>=0?" 和「"+DecorationNames[R.DecorationReward]+"」":"")+(R.NewRelic>=0?"，发现新信物「"+RelicNames[R.NewRelic]+"」":"")+"。";
        }else if(R.InspirationsEnabled){OfferInspiration();}else{R.Node++;R.Phase="route";R.Log+="\n胜利，"+R.LastReward+"小队自动前往下一站。";}
    }
}
