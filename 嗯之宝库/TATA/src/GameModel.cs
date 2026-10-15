using System;
using System.IO;
using System.Xml.Serialization;

public class PetSave
{
    public int Version=1,Experience=0,Bond=0,Berries=4,Crystals=0,Wins=0;
    public bool Evolved=false;
    public long LastPatTicks=0;
    public string Phase="home",Message="先喂它一颗果子，然后一起去小溪看看。";
    public int Route=0,Room=0,Health=46,Focus=0,EnemyHealth=0,EnemyMaxHealth=0,EnemyTurn=0;
}

public sealed class PetGame
{
    public PetSave S;
    public readonly string SavePath;
    public string LoadWarning="";
    public PetGame(string path)
    {
        SavePath=path;S=new PetSave();
        if(File.Exists(path)){
            try{using(var f=File.OpenRead(path))S=(PetSave)new XmlSerializer(typeof(PetSave)).Deserialize(f);}
            catch(Exception){LoadWarning="未能读取原存档，旧文件已保留。";SavePath=path+".recovery.xml";}
        }
        if(S.Phase=="home")S.Health=MaxHealth;
    }
    public int Level{get{int[] t={0,30,80,150,240,360,510,690,900,1140};int l=1;for(int i=1;i<t.Length;i++)if(S.Experience>=t[i])l=i+1;return l;}}
    public int LevelStart{get{int[] t={0,30,80,150,240,360,510,690,900,1140};return t[Level-1];}}
    public int NextLevel{get{int[] t={30,80,150,240,360,510,690,900,1140,1400};return t[Level-1];}}
    public int MaxHealth{get{return 40+Level*6+(S.Evolved?24:0);}}
    public int Attack{get{return 7+Level*2+(S.Evolved?5:0);}}
    public int Rooms{get{return S.Route==0?2:3;}}
    public string PetName{get{return S.Evolved?"潮汐兽":"泡泡";}}
    public string RouteName{get{return S.Route==0?"小溪巡游":"潮汐洞穴";}}
    public int EnemyKind{get{return S.Route==0?S.Room:S.Room==2?2:S.Room;}}
    public string EnemyName{get{return new[]{"岩壳蟹","泡泡团","洞穴守卫"}[EnemyKind];}}
    public int EnemyDamage{get{return (S.Route==0?6:9)+S.Room*2;}}
    public bool HeavyAttack{get{return S.EnemyTurn%3==2;}}
    public int IncomingDamage{get{return EnemyDamage+(HeavyAttack?8:0);}}
    public bool CanEvolve{get{return !S.Evolved&&Level>=3&&S.Bond>=3&&S.Crystals>=1&&S.Phase=="home";}}
    public bool Busy{get{return S.Phase=="battle"||S.Phase=="between";}}
    public void Save()
    {
        string dir=Path.GetDirectoryName(SavePath);Directory.CreateDirectory(dir);
        string temporary=SavePath+".writing";
        using(var f=File.Create(temporary))new XmlSerializer(typeof(PetSave)).Serialize(f,S);
        if(File.Exists(SavePath))File.Replace(temporary,SavePath,SavePath+".bak",true);else File.Move(temporary,SavePath);
    }
    public bool Feed()
    {
        if(S.Phase!="home"||S.Berries<1)return false;
        int before=Level;S.Berries--;S.Bond++;S.Experience+=6;S.Health=MaxHealth;
        S.Message="吃得很开心：经验 +6，亲密 +1。"+(Level>before?" 升到了 Lv."+Level+"！":"");Save();return true;
    }
    public bool Pat()
    {
        if(S.Phase!="home")return false;
        long now=DateTime.UtcNow.Ticks;
        if(now-S.LastPatTicks<TimeSpan.FromSeconds(8).Ticks){S.Message="它正开心地蹭着你的手。";return false;}
        S.LastPatTicks=now;S.Bond++;S.Message="摸摸头，亲密 +1。";Save();return true;
    }
    public bool Begin(int route)
    {
        if(S.Phase!="home"||route<0||route>1||(route==1&&!S.Evolved))return false;
        S.Route=route;S.Room=0;S.Health=MaxHealth;S.Focus=0;StartRoom();Save();return true;
    }
    private void StartRoom()
    {
        S.Phase="battle";S.EnemyTurn=0;
        S.EnemyMaxHealth=S.Route==0?26+S.Room*8:42+S.Room*12;
        S.EnemyHealth=S.EnemyMaxHealth;
        S.Message="遇到了"+EnemyName+"。普通攻击积攒能量，留意敌人的下一招。";
    }
    public bool Act(string move)
    {
        if(S.Phase!="battle")return false;
        int damage=0;bool guarded=false;string action="";
        if(move=="attack"){damage=Attack;S.Focus=Math.Min(3,S.Focus+1);action="撞击造成 "+damage+" 点伤害，能量 +1。";}
        else if(move=="water"){
            if(S.Focus<2)return false;
            damage=Attack*2+2+(EnemyKind==0?4:0);S.Focus-=2;action="水泡冲击造成 "+damage+" 点伤害！"+(EnemyKind==0?" 击中了蟹壳的弱点。":"");
        }
        else if(move=="guard"){guarded=true;S.Focus=Math.Min(3,S.Focus+1);action="撑起水泡护盾，能量 +1。";}
        else if(move=="heal"){
            if(S.Berries<1||S.Health>=MaxHealth)return false;
            S.Berries--;int amount=Math.Min(24,MaxHealth-S.Health);S.Health+=amount;action="吃下一颗果子，恢复 "+amount+" 点体力。";
        }else return false;
        S.EnemyHealth=Math.Max(0,S.EnemyHealth-damage);
        if(S.EnemyHealth==0){WinRoom(action);Save();return true;}
        int incoming=guarded?(int)Math.Ceiling(IncomingDamage*.25):IncomingDamage;
        string counter=HeavyAttack?"重击":"攻击";
        S.Health=Math.Max(0,S.Health-incoming);S.EnemyTurn++;
        S.Message=action+"\n"+EnemyName+"的"+counter+"造成 "+incoming+" 点伤害。";
        if(S.Health==0){S.Phase="result";S.Message="这次没能走到终点。伙伴安全回来了，途中得到的奖励都保留。";}
        Save();return true;
    }
    private void WinRoom(string action)
    {
        int before=Level;S.Experience+=20;S.Berries++;
        S.Message=action+"\n战斗胜利：经验 +20，果子 +1。"+(Level>before?" 升到了 Lv."+Level+"！":"");
        if(S.Room+1==Rooms){
            S.Experience+=12;S.Crystals+=S.Route==0?1:2;S.Wins++;S.Phase="result";
            S.Message+="\n完成"+RouteName+"：经验 +12，晶石 +"+(S.Route==0?1:2)+"。";
        }else S.Phase="between";
    }
    public bool Continue(){if(S.Phase!="between")return false;S.Room++;StartRoom();Save();return true;}
    public bool Home(){if(S.Phase!="result"&&S.Phase!="home")return false;S.Phase="home";S.Health=MaxHealth;S.Focus=0;S.Message=CanEvolve?"成长条件已经满足，现在可以进化！":"回到家，体力恢复了。";Save();return true;}
    public bool Evolve()
    {
        if(!CanEvolve)return false;
        S.Crystals--;S.Evolved=true;S.Health=MaxHealth;S.Message="泡泡进化成潮汐兽！攻击与体力提升，潮汐洞穴已开放。";Save();return true;
    }
}

public static class GameplayTests
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception("Test failed: "+message);}
    private static void ClearRoute(PetGame g,int route)
    {
        Check(g.Begin(route),"begin route");int safety=0;
        while(g.S.Phase!="result"&&safety++<100){
            if(g.S.Phase=="between"){g.Continue();continue;}
            if(g.HeavyAttack)g.Act("guard");else if(g.S.Focus>=2)g.Act("water");else g.Act("attack");
        }
        Check(g.S.Health>0,"route survivable");Check(g.S.Phase=="result","route completes");
    }
    public static string Run(string folder)
    {
        string path=Path.Combine(folder,"game-test-"+Guid.NewGuid().ToString("N")+".xml");var g=new PetGame(path);
        Check(!g.Evolve(),"cannot evolve early");Check(!g.Begin(1),"advanced route locked");
        g.Feed();g.Feed();g.Feed();Check(g.S.Bond==3&&g.S.Berries==1,"feeding consumes food and grants bond");
        Check(g.Begin(0),"start first");int hp=g.S.Health;Check(!g.Act("water")&&g.S.Health==hp,"insufficient focus cannot consume turn");
        g.S.Phase="home";ClearRoute(g,0);Check(g.S.Crystals==1&&g.S.Wins==1,"completion rewards");
        int xp=g.S.Experience;Check(!g.Act("attack")&&g.S.Experience==xp,"no repeat rewards after victory");g.Home();
        ClearRoute(g,0);g.Home();Check(g.CanEvolve,"two adventures and feeding unlock evolution");
        g.Evolve();Check(g.S.Evolved&&g.S.Crystals==1,"evolution consumes one crystal");
        var reloaded=new PetGame(path);Check(reloaded.S.Evolved&&reloaded.S.Experience==g.S.Experience,"save reload preserves progression");
        Check(reloaded.Begin(1),"evolution unlocks new route");reloaded.Act("attack");var resumed=new PetGame(path);
        Check(resumed.S.Phase=="battle"&&resumed.S.EnemyHealth==reloaded.S.EnemyHealth,"battle resumes without resetting enemies");
        resumed.S.Phase="home";ClearRoute(resumed,1);Check(resumed.S.Wins==3,"advanced route completable");
        return "PASS: feeding, combat cost, rewards, evolution, unlocks, save reload, battle resume, advanced route";
    }
}
