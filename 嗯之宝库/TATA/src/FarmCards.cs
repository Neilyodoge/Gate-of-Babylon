using System;
using System.Linq;
using System.Collections.Generic;

// The entire turn is persisted. No timer, offline settlement or farm resource can advance it.
public partial class MossRun
{
    public int RulesVersion,ShieldBranch,BossDamage,BranchTriggers;
    public bool BranchChosen,KeepShield,FragileShield,SnailTriggered;
    public string Phase="home",Message="",Outcome="";
    public int Seed=19731,Serial,Revision,Stage,Turn,Hp=60,MaxHp=60,Block,Energy,EnemyHp,EnemyMax,EnemyBlock,Wet,Reflect,Weak;
    public int Played,Combos,DamageTaken,Victories,Relic,CompletedRuns,TotalWins,LastDamage,LastBlock,RewardCount;
    public bool Elite,Assisted;
    public List<int> Deck=new List<int>(),Hand=new List<int>(),Draw=new List<int>(),Discard=new List<int>(),Exhaust=new List<int>(),Offers=new List<int>();
    public List<string> Trail=new List<string>();
}
public sealed class MossCard
{
    public string Name,Kind,Text;public int Cost;
    public MossCard(string n,string k,int c,string t){Name=n;Kind=k;Cost=c;Text=t;}
}
public static class MossCards
{
    public static readonly MossCard[] All={
        new MossCard("泼水","水",1,"造成 4 伤害。附加 2 回合潮湿。"),
        new MossCard("小小电花","雷",1,"造成 5 伤害。敌人潮湿时额外 +6。"),
        new MossCard("泡泡盾","盾",1,"获得 7 护盾，挡住本回合伤害。"),
        new MossCard("水球碰撞","水",1,"造成 7 伤害。"),
        new MossCard("翻涌","水",2,"造成 13 伤害。附加 3 回合潮湿。"),
        new MossCard("弹弹壳","盾",1,"获得 5 护盾。本回合每次被攻击反击 4 伤害。"),
        new MossCard("小雨点","水",1,"附加 3 回合潮湿。抽 2 张牌。"),
        new MossCard("泡泡冲撞","盾",1,"造成 3 + 当前护盾值的伤害。护盾保留。"),
        new MossCard("莓果点心","叶",1,"恢复 5 体力。本场只用一次。"),
        new MossCard("深呼吸","叶",0,"获得 1 能量，抽 1 张牌。本场只用一次。"),
        new MossCard("双生电花","雷",2,"造成两次 6 伤害。潮湿时每次额外 +4。"),
        new MossCard("小喷泉","水",2,"造成 12 伤害。消耗潮湿，额外 +10。"),
        new MossCard("凉凉雾","盾",1,"获得 5 护盾。敌人下次攻击每段伤害 -3。"),
        new MossCard("涟漪","水",1,"造成 3 伤害。抽 1 张牌。"),
        new MossCard("大叶伞","盾",1,"获得 13 护盾。本场只用一次。")
    };
    public static int Id(int card){return card%100;}
    public static bool Up(int card){return card>=100;}
    public static string Name(int card){return All[Id(card)].Name+(Up(card)?" +":"");}
    public static string Text(int card)
    {
        if(!Up(card))return All[Id(card)].Text;
        return new[]{"造成 7 伤害。附加 2 回合潮湿。","造成 8 伤害。敌人潮湿时额外 +6。","获得 10 护盾，挡住本回合伤害。","造成 10 伤害。","造成 16 伤害。附加 3 回合潮湿。","获得 8 护盾。本回合每次被攻击反击 7 伤害。","附加 3 回合潮湿。抽 2 张牌，获得 3 护盾。","造成 6 + 当前护盾值的伤害。护盾保留。","恢复 8 体力。本场只用一次。","获得 1 能量、3 护盾，抽 1 张牌。本场只用一次。","造成两次 9 伤害。潮湿时每次额外 +4。","造成 15 伤害。消耗潮湿，额外 +10。","获得 8 护盾。敌人下次攻击每段伤害 -3。","造成 6 伤害。抽 1 张牌。","获得 16 护盾。本场只用一次。"}[Id(card)];
    }
    public static bool Valid(int card){return card>=0&&card<115&&Id(card)<All.Length;}
}
public partial class FarmGame
{
    internal int CardVisualBlocked,CardVisualHits;
    public MossRun C{get{if(S.Cards==null)S.Cards=new MossRun();return S.Cards;}}
    public string CardEnemyName { get{return C.Stage==4?"苔冠守门人":C.Elite?"栗林赤钳":new[]{"迷路的小钳","雨靴蟹","苔石蜗牛","窗台巡游者"}[Math.Min(3,C.Stage)];} }
    public int CardEnemyPet{get{return C.Stage==4?4:C.Stage==2?3:1;}}
    public int CardIntentHits{get{return C.Stage==4&&C.Turn%3==2?2:C.RulesVersion==0&&C.Stage==1&&C.Turn%3==0?2:1;}}
    public bool CardIntentGuard{get{return !CardIntentRest&&!CardBossCharge&&C.Turn%3==0&&C.Stage!=1;}}
    public int CardIntentDamage{get{if(CardIntentGuard||CardIntentRest)return 0;int n=C.Stage==4?(C.RulesVersion>0?(CardBossCharge?24:C.Turn%3==1?10:6):(C.Turn%3==1?17:10)):5+C.Stage*2+(C.Elite?3:0)+(C.Turn%3==2?3:0);return Math.Max(0,n-(C.Weak>0?3:0));}}
    public string CardIntent{get{return CardIntentRest?(CardBossCharge?"已打断 · 本回合不攻击":"休息 · 本回合不攻击"):CardIntentGuard?"缩壳 · 获得 9 护盾":(CardBossCharge?"准备重击 · ":"准备攻击 · ")+CardIntentDamage+(CardIntentHits>1?" × "+CardIntentHits:"")+" 伤害";}}
    private int CardRandom(int max){C.Seed=(int)(((long)Math.Max(1,C.Seed)*48271)%2147483647);return C.Seed%max;}
    private void CardShuffle(List<int> cards){for(int i=cards.Count-1;i>0;i--){int j=CardRandom(i+1),v=cards[i];cards[i]=cards[j];cards[j]=v;}}
    private void CardDraw(int count){for(int n=0;n<count&&C.Hand.Count<7;n++){if(C.Draw.Count==0){C.Draw.AddRange(C.Discard);C.Discard.Clear();CardShuffle(C.Draw);}if(C.Draw.Count==0)return;C.Hand.Add(C.Draw[0]);C.Draw.RemoveAt(0);}}
    private void CardCommit(){C.Revision++;Save();}
    public bool StartCards(bool assisted)
    {
        if(C.Phase!="home"&&C.Phase!="result")return false;
        int serial=C.Serial+1,seed=C.Seed,wins=C.TotalWins,runs=C.CompletedRuns;
        S.Cards=new MossRun{RulesVersion=1,Serial=serial,Seed=seed,TotalWins=wins,CompletedRuns=runs,Assisted=assisted,MaxHp=assisted?80:60,Hp=assisted?80:60};
        C.Deck.AddRange(new[]{0,0,1,2,2,3,5,13});C.Trail.Add("带着 8 张牌，从苔间出发。");BeginCardBattle(false);CardCommit();return true;
    }
    private void BeginCardBattle(bool elite)
    {
        C.Elite=elite;C.Phase="battle";C.Turn=1;C.Block=0;C.Wet=0;C.Weak=0;C.Reflect=0;C.EnemyBlock=0;
        C.KeepShield=C.FragileShield=C.SnailTriggered=false;C.BossDamage=0;
        C.EnemyMax=new[]{24,35,43,51,86}[C.Stage]+(elite?12:0);C.EnemyHp=C.EnemyMax;
        C.Hand.Clear();C.Discard.Clear();C.Draw=new List<int>(C.Deck);C.Exhaust.Clear();CardShuffle(C.Draw);
        C.Energy=3;C.LastDamage=0;C.LastBlock=0;
        if(C.Stage==0){foreach(int card in new[]{0,1,2,3}){C.Draw.Remove(card);C.Hand.Add(card);}}else CardDraw(4);
        if(C.RulesVersion>0&&C.Stage==1&&C.ShieldBranch>0&&!C.Hand.Any(c=>MossCards.Id(c)==2)){
            int shield=C.Draw.FindIndex(c=>MossCards.Id(c)==2);if(shield>=0){int card=C.Draw[shield];C.Draw.RemoveAt(shield);C.Draw.Add(C.Hand[C.Hand.Count-1]);C.Hand[C.Hand.Count-1]=card;}
        }
        if(C.Relic==1)C.Block=4;
        C.Message=C.Stage==0&&C.CompletedRuns==0?"先试试「泼水」。点牌底部的「打出」。":"新一场，体力沿用，能量回满。";
    }
    public bool PlayCard(int slot,int revision)
    {
        if(C.Phase!="battle"||C.Revision!=revision||slot<0||slot>=C.Hand.Count)return false;
        int card=C.Hand[slot],id=MossCards.Id(card),cost=MossCards.All[id].Cost,bonus=MossCards.Up(card)?3:0;
        if(C.Energy<cost)return false;
        int beforeHp=C.Hp,beforeBlock=C.Block,beforeHand=C.Hand.Count,beforeEnergy=C.Energy;
        C.Hand.RemoveAt(slot);C.Energy-=cost;C.Played++;C.LastDamage=0;C.LastBlock=0;
        if(id==8||id==9||id==14)C.Exhaust.Add(card);else C.Discard.Add(card);
        bool combo=false;
        switch(id){
            case 0:CardHit(4+bonus);C.Wet=Math.Max(C.Wet,2);break;
            case 1:combo=C.Wet>0;CardHit(5+bonus+(combo?6:0)+(C.Relic==2?2:0));break;
            case 2:C.Block+=(C.ShieldBranch==2?4:7)+bonus;if(C.ShieldBranch==1)C.KeepShield=true;if(C.ShieldBranch==2)C.FragileShield=true;break;
            case 3:CardHit(7+bonus);break;
            case 4:CardHit(13+bonus);C.Wet=Math.Max(C.Wet,3);break;
            case 5:C.Block+=5+bonus;C.Reflect+=4+bonus;break;
            case 6:C.Wet=Math.Max(C.Wet,3);CardDraw(2);if(bonus>0)C.Block+=bonus;break;
            case 7:combo=C.Block>0;CardHit(3+bonus+C.Block);break;
            case 8:C.Hp=Math.Min(C.MaxHp,C.Hp+5+bonus);break;
            case 9:C.Energy++;CardDraw(1);if(bonus>0)C.Block+=bonus;break;
            case 10:combo=C.Wet>0;for(int i=0;i<2;i++)CardHit(6+bonus+(combo?4:0)+(C.Relic==2?2:0));break;
            case 11:combo=C.Wet>0;CardHit(12+bonus+(combo?10:0));C.Wet=0;break;
            case 12:C.Block+=5+bonus;C.Weak=1;break;
            case 13:CardHit(3+bonus);CardDraw(1);break;
            case 14:C.Block+=13+bonus;break;
        }
        if(combo)C.Combos++;
        C.Message=CardActionReceipt(card,beforeHp,beforeBlock,beforeHand,beforeEnergy,combo);
        if(C.EnemyHp<=0)WinCardBattle();CardCommit();return true;
    }
    private void CardHit(int value,bool direct=true){int blocked=Math.Min(C.EnemyBlock,value);C.EnemyBlock-=blocked;C.LastBlock+=blocked;int actual=Math.Min(C.EnemyHp,Math.Max(0,value-blocked));C.EnemyHp-=actual;C.LastDamage+=actual;if(direct&&CardBossCharge)C.BossDamage=Math.Min(12,C.BossDamage+actual);if(direct&&C.RulesVersion>0&&C.Stage==2&&!C.SnailTriggered&&value>0&&C.EnemyHp>0){C.SnailTriggered=true;C.EnemyBlock+=5;}}
    public bool EndCardTurn(int revision)
    {
        if(C.Phase!="battle"||C.Revision!=revision)return false;
        CardVisualBlocked=CardVisualHits=0;
        int enemyHp=C.EnemyHp;
        int damage=CardIntentDamage,hits=CardIntentHits,taken=0,blocked=0,extraDraw=0;bool guard=CardIntentGuard,rest=CardIntentRest;
        C.EnemyBlock=guard?9:0;
        if(!guard&&!rest)for(int i=0;i<hits&&C.Hp>0&&C.EnemyHp>0;i++){
            int shield=Math.Min(C.Block,damage);C.Block-=shield;blocked+=shield;CardVisualBlocked+=shield;CardVisualHits++;int lost=Math.Min(C.Hp,damage-shield);C.Hp-=lost;taken+=lost;if(C.Reflect>0)CardHit(C.Reflect,false);
            if(C.FragileShield&&shield>0&&C.Block==0){C.FragileShield=false;CardHit(8,false);extraDraw=1;C.BranchTriggers++;}
        }
        C.DamageTaken+=taken;C.Reflect=0;if(C.RulesVersion==0||!guard&&!rest)C.Weak=0;C.Wet=Math.Max(0,C.Wet-1);
        if(C.Hp<=0){FinishCards(false,false);CardCommit();return true;}
        if(C.EnemyHp<=0){WinCardBattle();CardCommit();return true;}
        C.Discard.AddRange(C.Hand);C.Hand.Clear();C.Turn++;C.Energy=3;if(!C.KeepShield)C.Block=0;else if(C.Block>0)C.BranchTriggers++;C.KeepShield=C.FragileShield=C.SnailTriggered=false;C.BossDamage=0;CardDraw(4+extraDraw);
        C.Message=CardTurnReceipt(rest,guard,blocked,taken,enemyHp-C.EnemyHp,extraDraw>0);
        CardCommit();return true;
    }
    private void WinCardBattle()
    {
        C.Victories++;C.Trail.Add("击败"+CardEnemyName+" · 第 "+C.Turn+" 回合 · 剩余体力 "+C.Hp);
        if(C.Stage==4){FinishCards(true,false);return;}
        C.Phase="reward";C.Offers.Clear();var pool=Enumerable.Range(1,14).ToList();
        for(int i=0;i<3;i++){int at=CardRandom(pool.Count);C.Offers.Add(pool[at]+(C.Elite&&C.Relic>0?100:0));pool.RemoveAt(at);}
        if(C.Elite&&C.Relic==0){C.Relic=1;C.Trail.Add("获得树叶帽：每场开局 +4 护盾。");}
        C.Message="赢了！选一张加入本局牌组，下一场就能抽到。";
    }
    public bool PickCardReward(int slot,int revision)
    {
        if(C.Phase!="reward"||C.Revision!=revision||slot< -1||slot>=C.Offers.Count)return false;
        if(slot>=0){int card=C.Offers[slot];C.Deck.Add(card);C.Trail.Add("加入 "+CurrentCardName(card));C.RewardCount++;}
        else C.Trail.Add("这次不加牌，保持牌组精简。");
        C.Offers.Clear();C.Stage++;C.Phase=C.RulesVersion>0&&C.Stage==1&&!C.BranchChosen?"branch":C.Stage==2||C.Stage==4?"camp":"route";C.Message="";CardCommit();return true;
    }
    public bool ChooseCardRoute(bool elite,int revision)
    {
        if(C.Phase!="route"||C.Revision!=revision)return false;BeginCardBattle(elite);CardCommit();return true;
    }
    public bool CardCamp(int option,int deckIndex,int revision)
    {
        if(C.Phase!="camp"||C.Revision!=revision)return false;
        if(option==0){int gain=Math.Min(16,C.MaxHp-C.Hp);C.Hp+=gain;C.Trail.Add("叶下休息：恢复 "+gain+" 体力。");}
        else if(option==1){if(deckIndex<0||deckIndex>=C.Deck.Count||MossCards.Up(C.Deck[deckIndex]))return false;C.Deck[deckIndex]+=100;C.Trail.Add("强化 "+CurrentCardName(C.Deck[deckIndex]));}
        else return false;
        BeginCardBattle(false);CardCommit();return true;
    }
    public bool RetireCards(int revision){if(C.Revision!=revision||C.Phase=="home"||C.Phase=="result")return false;FinishCards(false,true);CardCommit();return true;}
    private void FinishCards(bool won,bool retreat)
    {
        C.Phase="result";C.Outcome=won?"带着一身露水，凯旋！":retreat?"这一趟先到这里":"泡泡回到叶子下休息了";
        C.CompletedRuns++;if(won)C.TotalWins++;C.Trail.Add(won?"收集记录：苔冠印记 +1。":"本次没有获得苔冠印记。");C.Message="";
    }
    internal void ValidateCards()
    {
        var r=C;var phases=new[]{"home","battle","reward","branch","route","camp","result"};
        if(r.RulesVersion<0||r.RulesVersion>1||r.ShieldBranch<0||r.ShieldBranch>2||r.BossDamage<0||r.BossDamage>12||r.BranchTriggers<0||r.Phase=="branch"&&(r.Stage!=1||r.BranchChosen||r.RulesVersion==0))throw new Exception("Invalid branch state");
        if(!phases.Contains(r.Phase)||r.Stage<0||r.Stage>4||r.Hp<0||r.Hp>r.MaxHp||r.MaxHp<1||r.MaxHp>80||r.Energy<0||r.Energy>30||r.Relic<0||r.Relic>2||r.Turn<0||r.EnemyHp<0||r.EnemyHp>r.EnemyMax||r.Block<0||r.Wet<0||r.Serial<0||r.Revision<0)throw new Exception("Invalid card journey");
        foreach(var list in new[]{r.Deck,r.Hand,r.Draw,r.Discard,r.Exhaust,r.Offers})if(list==null||list.Count>30||list.Any(c=>!MossCards.Valid(c)))throw new Exception("Invalid cards");
        if(r.Trail==null||r.Hand.Count>7||r.Phase=="reward"&&r.Offers.Count!=3||r.Phase=="battle"&&(r.Hand.Count+r.Draw.Count+r.Discard.Count+r.Exhaust.Count!=r.Deck.Count||r.Turn<1||r.Hp==0||r.EnemyHp==0))throw new Exception("Invalid card turn");
        if(r.Phase=="battle"&&!r.Deck.OrderBy(c=>c).SequenceEqual(r.Hand.Concat(r.Draw).Concat(r.Discard).Concat(r.Exhaust).OrderBy(c=>c)))throw new Exception("Card piles do not match deck");
    }
}
