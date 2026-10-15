using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;

public class FarmSave
{
    public MossRun Cards=new MossRun();
    public int GuideStep=-1; // Old saves do not interrupt players with onboarding.
    public bool GuideInWorld;
    public bool GuideFarmSeen;
    public int GuideFarmLesson=-1; // Older saves keep the already familiar farm flow.
    public int GuideDepartureLesson=-1; // Existing preparations remain ready to depart.
    public HomeGuideStage GuideHomeStage;
    public int GuideHarvestBerries;
    public long GuideRestUntilTicks;
    public bool GuideOutingAccepted;
    public int ReadingSize=0; public bool SimpleCourtyard; // Missing in old saves: retain their existing full interface.
    public int CourtyardPet=-1; // Older saves keep their familiar companion until explicitly changed.
    public FarmSmallReminder SmallReminder; // Only an explicitly saved activity is resumed.
    public FarmAwayHarvest LastAwayHarvest;
    public HomecomingReceipt LastHomecoming;
    public FarmSoilReceipt LastSoilUpgrade;
    public int PlannedPicnic=-1;
    public int PlannedRegion=0,PlannedDepth=0;
    public int InspirationStyle=0;
    public bool[] DiscoveredInspirations=new bool[12];
    public int Version=3,Berries=10,Grain=4,Mint=0,Crystals=0,Bond=0,Wins=0,Harvests=0,Seed=731;
    public int[] Xp=new int[FarmGame.PetCount],Team=new[]{0,2,3},Crops=new[]{0,0,1,1,0,0};
    public double[] Growth=new double[]{34,19,15,2,0,0};
    public bool Evolved=false,GardenUnlocked=false;
    public long LastFarmTicks=DateTime.UtcNow.Ticks;
    public double Left=-1,Top=-1;
    public string DesktopMode="pet";
    public bool DesktopHidden=false;
    public bool DesktopTopmost=true,DesktopMotion=true,DesktopLocked=false,DesktopHotkeys=true;
    public bool DesktopStatusHints=true;
    public int PetSize=128,DisplayPet=0;
    public double PetLeft=Double.NaN,PetTop=Double.NaN;
    public int Fertilizer=0,SoilLevel=0,DisplayDecoration=-1;
    public int DecorationLayoutVersion=0,DecorationWish=-1;
    public int[] GardenDecorations=new[]{-1,-1,-1};
    public int SeedStage=0,SeedBond=0,FarmCaretaker=-1;
    public int ProgressionVersion=0,RelicVictories=0;
    public int[] ExpeditionClears=new int[6],EquippedRelics=new[]{-1,-1};
    public bool[] ChapterClaimed=new bool[8];
    public SavedParty[] SavedParties=new[]{new SavedParty(),new SavedParty(),new SavedParty()};
    public JourneyMemory[] JourneyMemories=new JourneyMemory[0];
    public CareTarget CareTarget=new CareTarget();
    public int[] Skills=new int[FarmGame.PetCount],BlessingOffers=new int[0];
    public bool[] Decorations=new bool[6];
    public string Notice="第一批莓果快熟了。先喂泡泡，再带队去小溪探险。";
    public FarmRun Run=new FarmRun();
}
public class FarmRun
{
    public int CrystalTargetPet=-1,CrystalTargetStage=-1;
    public int Picnic=-1,PicnicTriggers=0,PicnicHealing=0;
    public bool InspirationsEnabled=false;
    public int InspirationStyle=0;
    public int[] Inspirations=new int[12],InspirationTriggers=new int[12],InspirationOffers=new int[0];
    public InspirationPick[] InspirationPicks=new InspirationPick[0];
    public bool TraceBattles=false;
    public BattleNote ActiveBattle;
    public BattleNote[] Battles=new BattleNote[0];
    public bool HistoryRecorded=false,Withdrew=false;
    public string Phase="home",Outcome="",Log="";
    public string LastReward="";
    public int RewardBerries=-1,RewardCrystals=-1,RewardXp=-1;
    public bool UnlockedGarden=false;
    public int Node=0,Round=0,Difficulty=0,Meal=0,EnemyCount=0,Kills=0,LastActor=-1;
    public bool Elite=false;
    public bool Casual=false,JourneyPaused=false,LegacyRewards=false;
    public long NextJourneyTicks=0;
    public int Blessing=-1,RewardFertilizer=0,DecorationReward=-1,WaterTriggers=0,ChainTriggers=0,ShieldTriggers=0,SpreadTriggers=0,BurnDamage=0,Recovery=0;
    public int[] Skills=new int[FarmGame.PetCount];
    public string LastCombo="";
    public int[] Poison=new int[3];
    public int PoisonDamage=0,PoisonTriggers=0,PoisonSpreads=0,CatalystTriggers=0;
    public int Depth=0,NewRelic=-1;
    public int[] Relics=new[]{-1,-1},RelicTriggers=new int[6];
    public int[] Hp=new int[FarmGame.PetCount],Shield=new int[FarmGame.PetCount],EnemyHp=new int[3],EnemyMax=new int[3],EnemyKind=new int[3],Wet=new int[3],Burn=new int[3],Perks=new int[12],Offers=new int[0];
}
public partial class FarmGame
{
    public FarmSave S;
    public readonly string SavePath;
    public string LoadWarning="";
    public string LastHarvest="成熟后自动收获并入库，无需手动领取。";
    public static readonly string[] Names={"泡泡","赤钳","电团","苔壳","暗影种子"};
    public static readonly string[] Roles={"水系 · 辅助 · 潮湿标记","火系 · 战士 · 灼烧输出","雷系 · 特殊机制 · 连锁追击","草系 · 治疗 · 治疗护盾","草系 · 射手 · 毒种扩散"};
    public static readonly string[] CropNames={"莓果","麦穗","月光薄荷"};
    public static readonly string[] PerkNames={"露营补给","导电体质","连锁闪电","厚叶护盾","回春","炽热钳击","健壮","抢占先机","节奏连击","甜蜜战利品","激流扩散","生命汲取"};
    public static readonly string[] PerkDescriptions={"每场胜利后全队额外恢复 8 体力。","电团攻击潮湿目标，额外伤害 +5。","电团触发潮湿追击时，闪电跳向另一名敌人，造成 7 伤害。","每回合为最前方伙伴增加 4 护盾。","苔壳每回合的治疗量 +5。","赤钳每次攻击附加的灼烧 +3。","所有伙伴体力上限 +12，并立即恢复 12。","每场战斗首回合，全队攻击 +5。","每第三回合，最前方伙伴追加一次 10 伤害的攻击。","每场胜利额外获得莓果 ×2。","泡泡攻击后，也让另一名敌人潮湿。","击败一名敌人，为伤势最重的伙伴恢复 10 体力。"};
    public FarmRun R{get{return S.Run;}}
    public bool AtHome{get{return R.Phase=="home";}}
    public int Plots{get{return S.GardenUnlocked?6:4;}}
    public int Caretaker{get{if(S.FarmCaretaker>=0&&S.FarmCaretaker<PetCount&&!S.Team.Contains(S.FarmCaretaker))return S.FarmCaretaker;for(int i=0;i<PetCount;i++)if(!S.Team.Contains(i))return i;return 3;}}
    public string CaretakerEffect{get{return CaretakerBonus(Caretaker);}}
    public FarmGame(string path,string legacy=null,bool discardProgress=false)
    {
        GuideTest=discardProgress;SavePath=discardProgress?null:path;S=new FarmSave();
        if(GuideTest){ResetGuideTest();return;}
        if(File.Exists(path)){
            try{S=Read(path);bool oldRoster=S.Xp!=null&&S.Xp.Length==4;bool oldProgression=S.ProgressionVersion<1;Validate();if(oldRoster&&!File.Exists(path+".before-pets-v1.6.bak"))File.Copy(path,path+".before-pets-v1.6.bak");if(oldProgression&&!File.Exists(path+".before-progression-v1.8.bak"))File.Copy(path,path+".before-progression-v1.8.bak");}
            catch(Exception){
                SavePath=path+".recovery-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".xml";
                try{S=Read(path+".bak");Validate();LoadWarning="原存档未能读取，已从备份恢复；原文件保持不变。";}
                catch(Exception){S=new FarmSave();LoadWarning="原存档未能读取，已保留原文件并建立独立恢复存档。";}
            }
        }else if(legacy!=null&&File.Exists(legacy)){
            try{using(var f=File.OpenRead(legacy)){var old=(PetSave)new XmlSerializer(typeof(PetSave)).Deserialize(f);S.Xp[0]=Math.Max(0,old.Experience);S.Berries=Math.Max(10,old.Berries);S.Crystals=Math.Max(0,old.Crystals);S.Bond=Math.Max(0,old.Bond);S.Evolved=old.Evolved;S.Wins=Math.Max(0,old.Wins);S.GardenUnlocked=old.Wins>0;S.Notice="已带入旧版泡泡的成长和资源，旧存档仍保留。";}}
            catch(Exception){LoadWarning="旧版存档未能读取，旧文件仍保留。";}
        }
        else{S.GuideStep=0;S.GuideFarmLesson=0;S.GuideDepartureLesson=0;}
        SettleLoadedFarm(DateTime.UtcNow);AdvanceJourney(DateTime.UtcNow);Save();
    }
    private FarmSave Read(string p){using(var f=File.OpenRead(p))return (FarmSave)new XmlSerializer(typeof(FarmSave)).Deserialize(f);}
    private void Validate()
    {
        ValidateCards();
        NormalizeRoster();
        if(S==null||S.Version!=3||S.Xp==null||S.Xp.Length!=PetCount||S.Team==null||S.Team.Length!=3||S.Team.Distinct().Count()!=3||S.Team.Any(x=>x<0||x>=PetCount)||S.Crops==null||S.Crops.Length!=6||S.Crops.Any(x=>x<0||x>2)||S.Growth==null||S.Growth.Length!=6||S.Growth.Any(x=>Double.IsNaN(x)||Double.IsInfinity(x)||x<0)||S.Run==null)throw new Exception("Invalid save");
        if(R.Hp==null||R.Hp.Length!=PetCount||R.Shield==null||R.Shield.Length!=PetCount||R.EnemyHp==null||R.EnemyHp.Length!=3||R.EnemyMax==null||R.EnemyMax.Length!=3||R.EnemyKind==null||R.EnemyKind.Length!=3||R.EnemyKind.Any(x=>x<0||x>3)||R.Wet==null||R.Wet.Length!=3||R.Burn==null||R.Burn.Length!=3||R.Perks==null||R.Perks.Length!=12||R.Perks.Any(x=>x<0||x>2)||R.Offers==null||R.Offers.Any(x=>x<0||x>=12)||R.Node<0||R.Node>4||R.Difficulty<0||R.Difficulty>1||R.Meal<0||R.Meal>2||R.EnemyCount<0||R.EnemyCount>3||!new[]{"home","route","battle","draft","inspiration","result"}.Contains(R.Phase))throw new Exception("Invalid adventure state");
        S.Berries=Math.Max(0,S.Berries);S.Grain=Math.Max(0,S.Grain);S.Mint=Math.Max(0,S.Mint);S.Crystals=Math.Max(0,S.Crystals);for(int i=0;i<PetCount;i++)S.Xp[i]=Math.Max(0,Math.Min(2000,S.Xp[i]));
        if(S.DesktopMode!="farm"&&S.DesktopMode!="pet")S.DesktopMode="pet";
        S.ReadingSize=Math.Max(0,Math.Min(2,S.ReadingSize));S.PetSize=Math.Max(96,Math.Min(176,S.PetSize));S.DisplayPet=Math.Max(0,Math.Min(PetCount-1,S.DisplayPet));
        ValidateJourneyState();NormalizeProgression();NormalizeManagement();NormalizeDecorating();NormalizeCare();NormalizeInspirations();NormalizePicnic();NormalizeDestination();NormalizeSmallReminder();NormalizeAwayHarvest();NormalizeHomecoming();NormalizeBattleReview();
    }
    public void Save(){S.ReadingSize=Math.Max(0,Math.Min(2,S.ReadingSize));NormalizeRoster();NormalizeProgression();NormalizeManagement();NormalizeDecorating();NormalizeCare();NormalizeInspirations();NormalizePicnic();NormalizeDestination();NormalizeSmallReminder();NormalizeAwayHarvest();NormalizeHomecoming();NormalizeBattleReview();RefreshBattleReview();if(S.GuideStep< -1||S.GuideStep>9)S.GuideStep=-1;if(practiceOnly||GuideTest)return;if(batchSaving){savePending=true;return;}Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string temp=SavePath+".writing";using(var f=File.Create(temp))new XmlSerializer(typeof(FarmSave)).Serialize(f,S);if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak",true);else File.Move(temp,SavePath);}
    public string Name(int p){return p==4?SeedNames[S.SeedStage]:p==0&&S.Evolved?"潮汐兽":Names[p];}
    public int Level(int p){int[] t={0,20,55,110,190,300,450,650,900,1250};int l=1;for(int i=1;i<t.Length;i++)if(S.Xp[p]>=t[i])l=i+1;return l;}
    public int NextXp(int p){return new[]{20,55,110,190,300,450,650,900,1250,2000}[Level(p)-1];}
    public int LevelStartXp(int p){return new[]{0,20,55,110,190,300,450,650,900,1250}[Level(p)-1];}
    public int MaxHp(int p){return 42+Level(p)*6+(p==3?18:0)+(p==0&&S.Evolved?18:0)+(p==4?S.SeedStage*6:0)+R.Perks[6]*12+(R.Meal==1?12:0)+(HasPicnic(0)?8:0);}
    public int Attack(int p){return new[]{7,10,7,4,7}[p]+Level(p)*2+(p==0&&S.Evolved?3:0)+(p==4?S.SeedStage*2:0);}
    public double CropSeconds(int kind){double rate=1+(Caretaker==0?.2:Caretaker==2?.1:0)+(S.Evolved?.15:0)+S.SoilLevel*.12+(Caretaker==4&&kind==0?.15:0);return new[]{45.0,60.0,75.0}[kind]/rate;}
    public int CropYield(int kind){return kind==0?2+(Caretaker==3?1:0):kind==1?2+(Caretaker==1?1:0):1;}
    public int SecondsLeft(int plot){return Math.Max(1,(int)Math.Ceiling(CropSeconds(S.Crops[plot])-S.Growth[plot]));}
    public int TickFarm(DateTime now)
    {
        long ticks=now.ToUniversalTime().Ticks;if(ticks<=S.LastFarmTicks)return 0;
        double elapsed=Math.Min(4*3600,(ticks-S.LastFarmTicks)/(double)TimeSpan.TicksPerSecond);S.LastFarmTicks=ticks;int total=0;int[] gains=new int[3];int berriesBefore=S.Berries;var berryPlots=new List<int>();
        for(int i=0;i<Plots;i++){
            int crop=S.Crops[i];if(crop==2&&!S.GardenUnlocked)continue;
            S.Growth[i]+=elapsed;double seconds=CropSeconds(crop);int n=(int)Math.Floor(S.Growth[i]/seconds);if(n==0)continue;S.Growth[i]-=n*seconds;int amount=n*CropYield(crop);total+=amount;gains[crop]+=amount;
            if(crop==0){S.Berries=Math.Min(99999,S.Berries+amount);berryPlots.Add(i);}else if(crop==1)S.Grain=Math.Min(99999,S.Grain+amount);else S.Mint=Math.Min(99999,S.Mint+amount);
        }
        if(total>0){S.Harvests+=total;LastHarvest="最近收获已入库："+String.Join("  ",Enumerable.Range(0,3).Where(i=>gains[i]>0).Select(i=>CropNames[i]+" +"+gains[i]).ToArray());}
        if(S.Berries>berriesBefore)RecentBerryHarvest=new FarmHarvestMoment{State=S,BerryPlots=berryPlots.ToArray(),Berries=S.Berries-berriesBefore,Started=DateTime.UtcNow};
        AdvanceHomeGuide(gains[0],ticks);return total;
    }
    public bool Plant(int plot,int kind){if(plot<0||plot>=Plots||kind<0||kind>2||(kind==2&&!S.GardenUnlocked)||S.Crops[plot]==kind)return false;TickFarm(DateTime.UtcNow);S.Crops[plot]=kind;S.Growth[plot]=0;S.Notice="已改种"+CropNames[kind]+"，成熟后会自动收获并继续种植。";Save();return true;}
    public bool NeedsEvolutionBond(int pet){return pet==0?!S.Evolved&&S.Bond<3:pet==4&&S.SeedStage<3&&S.SeedBond<6;}
    public bool CanFeed(int pet){return AtHome&&pet>=0&&pet<PetCount&&S.Berries>=2&&(Level(pet)<10||NeedsEvolutionBond(pet));}
    public bool Feed(int pet)
    {
        if(!CanFeed(pet))return false;
        bool full=Level(pet)>=10;S.Berries-=2;if(!full)S.Xp[pet]+=12;if(pet==0)S.Bond++;if(pet==4)S.SeedBond++;
        if(S.GuideStep==3&&S.GuideFarmSeen&&S.GuideHomeStage==HomeGuideStage.FreshBerries&&pet==GuideCompanion)S.GuideHomeStage=HomeGuideStage.Complete;
        S.Notice=Name(pet)+(full?"吃了莓果：亲密 +1。等级已满，成长不再增加。":"吃了莓果：成长 +12"+(pet==0||pet==4?"，亲密 +1。":"。"));Save();return true;
    }
    public bool Train(int pet){if(!AtHome||pet<0||pet>=PetCount||S.Grain<3||Level(pet)>=10)return false;S.Grain-=3;S.Xp[pet]+=18;CompleteSmallActivity("train",pet);S.Notice=Name(pet)+"完成训练：成长 +18。";Save();return true;}
    public bool Swap(int pet){if(!AtHome||pet<0||pet>=PetCount)return false;TickFarm(DateTime.UtcNow);int left=Caretaker;int slot=Array.IndexOf(S.Team,pet);if(slot<0)return false;S.Team[slot]=left;S.FarmCaretaker=pet;S.Notice=Name(left)+"加入队伍，"+Name(pet)+"留守农场。";Save();return true;}
    public bool MoveFirst(int pet){if(!AtHome)return false;int i=Array.IndexOf(S.Team,pet);if(i<=0)return false;TickFarm(DateTime.UtcNow);var list=S.Team.ToList();list.Remove(pet);list.Insert(0,pet);S.Team=list.ToArray();S.Notice=Name(pet)+"来到前排。伙伴按站位顺序行动，敌人优先攻击前排。";Save();return true;}
    public bool CanEvolve{get{return AtHome&&!S.Evolved&&Level(0)>=3&&S.Bond>=3&&S.Crystals>=2;}}
    public bool Evolve(){if(!CanEvolve)return false;TickFarm(DateTime.UtcNow);S.Crystals-=2;S.Evolved=true;S.Notice="泡泡进化成潮汐兽！开场为全体敌人附加潮湿；农场永久获得雨露，生长 +15%。";Save();return true;}
    public string Goal{get{return NextChapterHint;}}
    public bool Begin(int difficulty,int meal)
    {
        NormalizeRoster();
        if(!AtHome||difficulty<0||difficulty>1||(difficulty==1&&!CaveUnlocked)||meal<0||meal>2)return false;
        if((meal==1&&(S.Berries<2||S.Grain<2))||(meal==2&&(S.Mint<1||S.Grain<2)))return false;
        if(meal==1){S.Berries-=2;S.Grain-=2;}if(meal==2){S.Mint--;S.Grain-=2;}
        R.Phase="home";S.Run=new FarmRun{Phase="route",Difficulty=difficulty,Meal=meal,RewardBerries=0,RewardCrystals=0,RewardXp=0};for(int i=0;i<PetCount;i++)R.Hp[i]=MaxHp(i);
        R.Log="小队出发。每场结束都会等你选择，关闭面板会暂停战斗。";S.Notice="伙伴出发了，"+Name(Caretaker)+"正在照料农场。";Save();return true;
    }
    public string RouteName{get{return (R.Difficulty==0?"小溪远足":"潮汐洞穴")+(R.Casual?" · "+DepthNames[R.Depth]:"");}}
    public string NodeName{get{return R.Node==4&&R.Difficulty==1?"深潮领主":new[]{"溪口草地","分岔小径","林间营地","守卫关口","溪流领主"}[R.Node];}}
    public string EnemyName(int e){if(R.EnemyKind[e]==3)return R.Difficulty==0?"溪流领主":"深潮领主";return new[]{"野生赤钳","迷雾电团","岩壳守卫"}[R.EnemyKind[e]];}
    public string Intent{get{return (R.Round+1)%3==0?(R.Node==4?"下一回合：领主横扫全队，小怪重击前排。":"下一回合：敌人重击前排，护盾优先抵挡。"):"下一回合：敌人普通攻击，优先攻击最前方伙伴。";}}
    public bool RouteChoice(int choice)
    {
        if(R.Phase!="route"||choice<0||choice>1)return false;
        if(R.Node==2){if(choice==0){foreach(int p in S.Team)R.Hp[p]=Math.Min(MaxHp(p),R.Hp[p]+(int)Math.Ceiling(MaxHp(p)*.4));R.Log="在营地休息，全队恢复 40% 体力。";R.Node++;}else{R.Log="找到古树秘藏，选择一项能力。";MakeOffers();R.Phase="draft";}Save();return true;}
        R.Elite=choice==1&&(R.Node==1||R.Node==3);R.Round=0;R.Shield=new int[PetCount];R.Poison=new int[3];R.Wet=new int[3];R.Burn=new int[3];R.EnemyHp=new int[3];R.EnemyMax=new int[3];R.EnemyKind=new int[3];R.EnemyCount=R.Node==4?3:2;
        for(int e=0;e<R.EnemyCount;e++){
            R.EnemyKind[e]=R.Node==4&&e==0?3:(R.Node+e)%3;
            int hp=R.Node==4?(e==0?105:25):24+R.Node*5+e*3;
            if(R.Elite)hp+=15;hp+=(R.Difficulty==1?14:0);R.EnemyHp[e]=R.EnemyMax[e]=hp;
            if(S.Evolved&&S.Team.Contains(0))R.Wet[e]=2;
        }
        R.Phase="battle";R.Log="进入"+NodeName+(R.Elite?" · 精英挑战":"")+"。"+(S.Evolved&&S.Team.Contains(0)?" 潮汐兽唤雨，全体敌人潮湿！":"");StartBattleReview();if(R.Casual)ApplyBattleBlessing();if(R.ActiveBattle!=null)R.ActiveBattle.EnemyStart=R.EnemyHp.Take(R.EnemyCount).Sum();Save();return true;
    }
    private int FirstEnemy(){for(int i=0;i<R.EnemyCount;i++)if(R.EnemyHp[i]>0)return i;return -1;}
    private int Front(){foreach(int p in S.Team)if(R.Hp[p]>0)return p;return -1;}
    private int Weakest(){int best=-1;double ratio=2;foreach(int p in S.Team)if(R.Hp[p]>0&&(double)R.Hp[p]/MaxHp(p)<ratio){best=p;ratio=(double)R.Hp[p]/MaxHp(p);}return best;}
    private void Heal(int p,int n){if(p>=0&&R.Hp[p]>0)R.Hp[p]=Math.Min(MaxHp(p),R.Hp[p]+n);}
    private void HitEnemy(int e,int n){if(e<0||R.EnemyHp[e]<=0)return;int armor=R.EnemyKind[e]==2?2:0;R.EnemyHp[e]=Math.Max(0,R.EnemyHp[e]-Math.Max(1,n-armor));if(R.EnemyHp[e]==0){R.Kills++;if(R.Perks[11]>0)Heal(Weakest(),10*R.Perks[11]);}}
    private void HitPet(int p,int n){if(p<0)return;int block=Math.Min(n,R.Shield[p]);R.Shield[p]-=block;R.Hp[p]=Math.Max(0,R.Hp[p]-(n-block));}
    public bool Step()
    {
        if(R.Casual)return StepJourneyBattle();
        if(R.Phase!="battle")return false;R.Round++;var log=new List<string>();
        foreach(int p in S.Team)R.Shield[p]/=2;int front=Front();if(front<0){Finish(false);Save();return true;}
        R.Shield[front]+=R.Perks[3]*4;
        foreach(int p in S.Team){
            if(R.Hp[p]<=0)continue;int e=FirstEnemy();if(e<0)break;R.LastActor=p;int damage=Attack(p)+(R.Round==1?R.Perks[7]*5:0);
            if(p==0){HitEnemy(e,damage);R.Wet[e]=3;if(R.Perks[10]>0){for(int x=0;x<R.EnemyCount;x++)if(x!=e&&R.EnemyHp[x]>0){R.Wet[x]=3;break;}}log.Add(Name(p)+" · 水泡 "+damage+"，附加潮湿");}
            if(p==1){HitEnemy(e,damage);R.Burn[e]+=3+R.Perks[5]*3;log.Add("赤钳 · 钳击 "+damage+"，附加灼烧");}
            if(p==2){bool wet=R.Wet[e]>0;if(wet)damage+=5+R.Perks[1]*5;HitEnemy(e,damage);if(wet&&R.Perks[2]>0){for(int x=0;x<R.EnemyCount;x++)if(x!=e&&R.EnemyHp[x]>0){HitEnemy(x,7*R.Perks[2]);break;}}log.Add("电团 · "+(wet?"潮湿追击":"电击")+" "+damage+(wet&&R.Perks[2]>0?"，闪电连锁！":""));}
            if(p==3){HitEnemy(e,damage);int heal=5+R.Perks[4]*5;Heal(Weakest(),heal);int f=Front();if(f>=0)R.Shield[f]+=6;log.Add("苔壳 · 撞击 "+damage+"，治疗 "+heal+"，前排护盾 +6");}
        }
        if(R.Round%3==0&&R.Perks[8]>0){HitEnemy(FirstEnemy(),10*R.Perks[8]);log.Add("节奏连击 · 追加攻击！");}
        for(int e=0;e<R.EnemyCount;e++)if(R.EnemyHp[e]>0&&R.Burn[e]>0){HitEnemy(e,R.Burn[e]);R.Burn[e]=Math.Max(0,R.Burn[e]-1);}
        if(FirstEnemy()<0){R.Log=String.Join("\n",log);WinBattle();Save();return true;}
        bool heavy=R.Round%3==0;int hurt=0;
        for(int e=0;e<R.EnemyCount;e++){
            if(R.EnemyHp[e]<=0)continue;
            int attack=(R.EnemyKind[e]==3?12:5+R.Node)+(R.Elite?3:0)+(R.Difficulty==1?3:0);
            if(heavy&&R.EnemyKind[e]==3){foreach(int p in S.Team)if(R.Hp[p]>0){HitPet(p,attack-3);hurt+=attack-3;}}
            else{int p=Front();if(p>=0){int amount=attack+(heavy?6:0);HitPet(p,amount);hurt+=amount;}}
        }
        for(int e=0;e<R.EnemyCount;e++)R.Wet[e]=Math.Max(0,R.Wet[e]-1);
        log.Add("敌方"+(heavy?"重击":"反击")+" · 共 "+hurt+" 点伤害（护盾可抵挡）");R.Log=String.Join("\n",log);
        if(Front()<0||R.Round>=60)Finish(false);Save();return true;
    }
    private void WinBattle()
    {
        int xp=R.Node==4?18:R.Elite?10:6;foreach(int p in S.Team){S.Xp[p]+=xp;R.Hp[p]=Math.Min(MaxHp(p),R.Hp[p]+7+R.Perks[0]*8+(R.Meal==2?9:0));}
        int food=2+R.Perks[9]*2;S.Berries+=food;if(R.Elite)S.Crystals++;
        R.LastReward="本场已入账：莓果 +"+food+"，每位出战伙伴成长 +"+xp+(R.Elite?"，晶石 +1":"")+"。";
        if(R.RewardBerries>=0){R.RewardBerries+=food;R.RewardXp+=xp;if(R.Elite)R.RewardCrystals++;}
        R.Log+="\n胜利！每位出战伙伴成长 +"+xp+"，莓果 +"+food+(R.Elite?"，晶石 +1":"")+"。";
        if(R.Node==4){S.Wins++;S.Crystals+=R.Difficulty==0?2:3;if(R.RewardCrystals>=0)R.RewardCrystals+=R.Difficulty==0?2:3;bool first=!S.GardenUnlocked;R.UnlockedGarden=first;S.GardenUnlocked=true;R.Log+="\n领主奖励：晶石 +"+(R.Difficulty==0?2:3)+"。"+(first?" 解锁月光薄荷与两块新田！":"");R.LastReward+=" 首领奖励：晶石 +"+(R.Difficulty==0?2:3)+"。";Finish(true);}
        else{MakeOffers();R.Phase="draft";}
    }
    private int Random(int max){S.Seed=unchecked(S.Seed*1664525+1013904223)&Int32.MaxValue;return S.Seed%max;}
    private void MakeOffers()
    {
        var pool=new List<int>();for(int i=0;i<12;i++){if(R.Perks[i]>=PerkLimit(i))continue;if((i==1||i==2)&&!S.Team.Contains(2))continue;if(i==4&&!S.Team.Contains(3))continue;if(i==5&&!S.Team.Contains(1))continue;if(i==10&&!S.Team.Contains(0))continue;pool.Add(i);}
        var offers=new List<int>();while(offers.Count<3&&pool.Count>0){int i=Random(pool.Count);offers.Add(pool[i]);pool.RemoveAt(i);}R.Offers=offers.ToArray();
    }
    public static int PerkLimit(int perk){return perk==10?1:2;}
    public bool Pick(int perk){if(R.Phase!="draft"||!R.Offers.Contains(perk)||R.Perks[perk]>=PerkLimit(perk))return false;R.Perks[perk]++;if(perk==6)foreach(int p in S.Team)if(R.Hp[p]>0)R.Hp[p]+=12;R.Log="获得「"+PerkNames[perk]+"」。"+PerkDescriptions[perk];R.Offers=new int[0];R.Node++;R.Phase="route";Save();return true;}
    private void Finish(bool won){CloseBattleReview(won);R.Phase="result";R.Outcome=won?"victory":"defeat";if(!won)R.Log+=R.Casual?"\n小队平安撤回。已经获得的肥料与资源保留。返家培养伙伴、调整技能后再试。":"\n小队平安撤回。已获得的成长、食物与晶石保留。可调整前排、补充料理后再试。";RememberJourney();S.Notice=won?"冒险完成！带回了晶石，来看看新的成长目标。":"伙伴回家休息了，途中的收获仍然保留。";}
    public bool Retreat(){if(AtHome||R.Phase=="result")return false;R.Withdrew=true;R.Log="主动结束本次冒险，途中收获保留。";Finish(false);Save();return true;}
    public bool Home(){if(R.Phase!="result")return false;S.Run=new FarmRun();S.Notice=CanEvolve?"泡泡已经满足进化条件，去伙伴页迎接新形态。":Goal;Save();return true;}
}

public static class FarmTests
{
    static void Check(bool condition,string why){if(!condition)throw new Exception("Test failed: "+why);}
    static FarmGame New(string root){return new FarmGame(Path.Combine(root,"test-"+Guid.NewGuid().ToString("N")+".xml"));}
    public static void Clear(FarmGame g,bool cache=false){int limit=0;while(g.R.Phase!="result"&&limit++<180){if(g.R.Phase=="route")g.RouteChoice(g.R.Node==2&&cache?1:0);else if(g.R.Phase=="battle")g.Step();else if(g.R.Phase=="draft"){int p=g.R.Offers.OrderBy(x=>x==4?0:x==1?1:x==3?2:3).First();g.Pick(p);}else throw new Exception("Unexpected phase");}Check(limit<180,"run terminates");}
    public static string Run(string root)
    {
        var g=New(root);long t=g.S.LastFarmTicks;int b=g.S.Berries;g.TickFarm(new DateTime(t+TimeSpan.FromSeconds(46).Ticks,DateTimeKind.Utc));Check(g.S.Berries>b,"automatic harvest");int b2=g.S.Berries;g.TickFarm(new DateTime(g.S.LastFarmTicks,DateTimeKind.Utc));Check(g.S.Berries==b2,"no duplicate harvest");Check(!g.Plant(4,0)&&!g.Plant(0,2),"locked land and seed");
        var offline=New(root);offline.S.LastFarmTicks=DateTime.UtcNow.AddDays(-3).Ticks;offline.TickFarm(DateTime.UtcNow);Check(offline.S.Berries<3000,"offline production is bounded");
        g.S.Berries=0;int xp=g.S.Xp[0];Check(!g.Feed(0)&&g.S.Xp[0]==xp,"no free feeding");g.S.Berries=10;g.Feed(0);g.Feed(0);g.Feed(0);Check(g.S.Bond==3&&g.S.Berries==4,"feeding and bond");Check(!g.Evolve(),"evolution gated");Check(g.Begin(0,0),"begin");Check(!g.Swap(0)&&!g.Feed(0),"party immutable while away");g.RouteChoice(0);g.Step();int eh=g.R.EnemyHp.Sum();var reload=new FarmGame(g.SavePath);Check(reload.R.Phase=="battle"&&reload.R.EnemyHp.Sum()==eh&&reload.R.Round==1,"mid battle reload");
        Clear(reload);Check(reload.R.Outcome=="victory","starter team clears creek");int c=reload.S.Crystals;int wins=reload.S.Wins;Check(!reload.Step()&&!reload.RouteChoice(0)&&reload.S.Crystals==c&&reload.S.Wins==wins,"rewards cannot repeat");Check(reload.S.GardenUnlocked&&reload.Plots==6&&reload.Plant(4,2),"boss unlocks farming");reload.Home();Check(reload.CanEvolve,"first adventure plus three feeds enables evolution");double old=reload.CropSeconds(0);reload.Evolve();Check(reload.S.Evolved&&reload.CropSeconds(0)<old&&reload.S.Crystals==c-2,"evolution changes farm and costs crystals");
        reload.Begin(1,0);reload.RouteChoice(0);Check(reload.R.Wet.Take(reload.R.EnemyCount).All(x=>x>0),"evolution causes opening rain");reload.Retreat();reload.Home();Check(reload.S.Evolved&&reload.R.Perks.Sum()==0,"permanent evolution and run reset");
        var wet=New(root);wet.Begin(0,0);wet.RouteChoice(0);wet.R.EnemyHp[0]=wet.R.EnemyMax[0]=200;wet.Step();Check(wet.R.Log.Contains("潮湿追击"),"water lightning interaction");
        var fire=New(root);fire.Swap(2);fire.Begin(0,0);fire.RouteChoice(0);fire.R.EnemyHp[0]=fire.R.EnemyMax[0]=200;fire.Step();Check(fire.R.Burn[0]>0,"fire burn");
        var draft=New(root);draft.Begin(0,0);draft.RouteChoice(0);while(draft.R.Phase=="battle")draft.Step();int[] offers=draft.R.Offers.ToArray();var saved=new FarmGame(draft.SavePath);Check(saved.R.Offers.SequenceEqual(offers)&&offers.Distinct().Count()==3,"draft does not reroll on reload");int pick=offers[0];Check(saved.Pick(pick)&&!saved.Pick(pick)&&saved.R.Perks[pick]==1,"pick exactly once");
        var loss=New(root);loss.Begin(0,0);loss.RouteChoice(0);foreach(int p in loss.S.Team)loss.R.Hp[p]=1;loss.R.EnemyCount=3;for(int e=0;e<3;e++){loss.R.EnemyHp[e]=1000;loss.R.EnemyKind[e]=3;}loss.Step();Check(loss.R.Phase=="result"&&loss.R.Outcome=="defeat","defeat resolves");loss.Home();Check(loss.Begin(0,0)&&loss.S.Team.All(p=>loss.R.Hp[p]==loss.MaxHp(p)),"healthy restart");
        var meal=New(root);meal.S.Berries=1;Check(!meal.Begin(0,1)&&meal.AtHome,"meal cost atomic");meal.S.Berries=10;Check(meal.Begin(0,1)&&meal.S.Berries==8&&meal.R.Hp[0]==meal.MaxHp(0),"meal consumed once");
        var corrupt=New(root);File.WriteAllText(corrupt.SavePath,"bad-save");var recovered=new FarmGame(corrupt.SavePath);Check(recovered.SavePath!=corrupt.SavePath&&File.ReadAllText(corrupt.SavePath)=="bad-save","corrupt source preserved");
        var mint=New(root);mint.S.GardenUnlocked=true;mint.Plant(4,2);mint.TickFarm(new DateTime(mint.S.LastFarmTicks+TimeSpan.FromSeconds(80).Ticks,DateTimeKind.Utc));Check(mint.S.Mint>0,"unlocked seed produces cooking ingredient");
        for(int keeper=0;keeper<4;keeper++){var alt=New(root);alt.S.Team=Enumerable.Range(0,4).Where(p=>p!=keeper).ToArray();alt.S.Seed=123+keeper;alt.Begin(0,0);Clear(alt,true);Check(alt.R.Phase=="result","alternative roster and cache path resolves");}
        var advanced=New(root);advanced.S.Evolved=true;advanced.S.Xp=new[]{110,55,55,55};advanced.Begin(1,1);Clear(advanced);Check(advanced.R.Outcome=="victory","grown party can clear advanced route");
        var elite=New(root);elite.Begin(0,0);elite.R.Node=1;elite.RouteChoice(1);for(int e=0;e<elite.R.EnemyCount;e++)elite.R.EnemyHp[e]=1;elite.Step();Check(elite.R.Phase=="draft"&&elite.S.Crystals==1,"elite reward and next draft");
        Check(elite.R.RewardCrystals==1&&elite.R.RewardBerries==2&&elite.R.RewardXp==10,"run rewards counted separately from farm production");
        var capped=New(root);capped.Begin(0,0);capped.R.Perks[10]=1;capped.R.Node=2;capped.RouteChoice(1);Check(!capped.R.Offers.Contains(10),"nonstacking ability is not offered twice");
        var loadedUx=new FarmGame(elite.SavePath);Check(loadedUx.R.RewardCrystals==1&&loadedUx.R.LastReward.Contains("晶石 +1"),"reward summary survives reload");
        string legacyPath=Path.Combine(root,"old-format-"+Guid.NewGuid().ToString("N")+".xml");string legacyXml=System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(elite.SavePath),@"\s*<(RewardBerries|RewardCrystals|RewardXp|LastReward|UnlockedGarden)>.*?</\1>","");File.WriteAllText(legacyPath,legacyXml);var legacy=new FarmGame(legacyPath);Check(legacy.R.Phase==elite.R.Phase&&legacy.R.Node==elite.R.Node&&legacy.R.Offers.SequenceEqual(elite.R.Offers)&&legacy.R.RewardBerries==-1,"older save retains route and choices without invented totals");
        var display=New(root);display.Begin(0,0);display.RouteChoice(0);display.Step();display.S.DesktopMode="farm";display.S.PetSize=176;display.S.DesktopHidden=true;display.S.DisplayPet=3;display.Save();
        var displayReload=new FarmGame(display.SavePath);Check(displayReload.S.DesktopMode=="farm"&&displayReload.S.PetSize==176&&displayReload.S.DesktopHidden&&displayReload.S.DisplayPet==3&&displayReload.R.Round==1,"display preferences coexist with active adventure");
        string oldDisplayPath=Path.Combine(root,"old-display-"+Guid.NewGuid().ToString("N")+".xml");string oldDisplayXml=System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(display.SavePath),@"\s*<(DesktopMode|DesktopHidden|PetSize|DisplayPet|PetLeft|PetTop)>.*?</\1>","");File.WriteAllText(oldDisplayPath,oldDisplayXml);var oldDisplay=new FarmGame(oldDisplayPath);Check(oldDisplay.SavePath==oldDisplayPath&&oldDisplay.S.DesktopMode=="pet"&&oldDisplay.S.PetSize==128&&!oldDisplay.S.DesktopHidden&&oldDisplay.R.Round==1&&oldDisplay.R.EnemyHp.SequenceEqual(display.R.EnemyHp),"pre-compact save gains safe defaults and keeps combat");
        display.S.DesktopMode="invalid";display.S.PetSize=-200;display.S.DisplayPet=99;display.Save();var repairedDisplay=new FarmGame(display.SavePath);Check(repairedDisplay.SavePath==display.SavePath&&repairedDisplay.S.DesktopMode=="pet"&&repairedDisplay.S.PetSize==96&&repairedDisplay.S.DisplayPet==FarmGame.PetCount-1&&repairedDisplay.R.Round==1,"invalid presentation preferences cannot reset progression");
        return "PASS: farm harvest / offline cap / crop locks / feeding / party locks / combat resume / starter victory / single rewards / garden unlock / permanent evolution / irrigation / opening rain / synergy / burn / stable draft / defeat / meal costs / corruption recovery / mint production / all four rosters / cache route / advanced victory / elite reward / separate reward totals / no duplicate single-use perk / reward persistence";
    }
}
