using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public partial class FarmGame
{
    public bool CardBossCharge{get{return C.RulesVersion>0&&C.Stage==4&&C.Turn%3==0;}}
    public bool CardIntentRest{get{return C.RulesVersion>0&&((C.Stage==1||C.Stage==3)&&C.Turn%3==0||CardBossCharge&&C.BossDamage>=12);}}
    public string CardHabit{get{
        if(C.RulesVersion==0)return "";
        if(C.Stage==1||C.Stage==3)return "习惯：进攻两回合后，休息一回合。";
        if(C.Stage==2)return C.SnailTriggered?"本回合已缩壳，下回合会再次警惕。":"本回合第一次被攻击后，获得 5 护盾。";
        if(C.Stage==4)return CardBossCharge?"打断重击：本回合实际伤害 "+C.BossDamage+" / 12":"每第三回合重击；当回合造成 12 伤害可打断。";
        return "";
    }}
    public string CardBranchName{get{return C.ShieldBranch==1?"厚泡泡":C.ShieldBranch==2?"脆泡泡":"原来的泡泡盾";}}
    public string CurrentCardName(int card){return MossCards.Id(card)==2&&C.ShieldBranch>0?CardBranchName+(MossCards.Up(card)?" +":""):MossCards.Name(card);}
    public string CurrentCardText(int card){
        if(MossCards.Id(card)!=2||C.ShieldBranch==0)return MossCards.Text(card);
        int bonus=MossCards.Up(card)?3:0;
        return C.ShieldBranch==1?"获得 "+(7+bonus)+" 护盾。本回合结束后保留剩余护盾一次。":"获得 "+(4+bonus)+" 护盾。本回合破盾反击 8，下回合多抽 1 张（最多一次）。";
    }
    public bool PickShieldBranch(int branch,int revision)
    {
        if(C.Phase!="branch"||C.Revision!=revision||branch<0||branch>2||C.BranchChosen)return false;
        C.ShieldBranch=branch;C.BranchChosen=true;C.Phase="route";C.Trail.Add("盾牌方向："+CardBranchName+"。");CardCommit();return true;
    }
}
public partial class FarmPanel
{
    private void CardBranchPage(Grid host)
    {
        var p=CardStack(host);int rev=G.C.Revision;
        p.Children.Add(Ct("让泡泡盾，变成你的套路",23));
        p.Children.Add(Ct("选一种变化，作用于这一趟所有泡泡盾。下一场就能用。",13,U.Muted));
        var thick=new StackPanel();thick.Children.Add(Ct("厚泡泡 · 攒盾再撞",18,U.Green));thick.Children.Add(Ct("获得 7 护盾，剩余护盾保留到下回合一次。\n适合积攒护盾，再打泡泡冲撞。",13));thick.Children.Add(Cb("选择厚泡泡",()=>G.PickShieldBranch(1,rev),true,false,"cards-branch-thick"));p.Children.Add(U.Card(thick,12));
        var fragile=new StackPanel();fragile.Children.Add(Ct("脆泡泡 · 接招反击",18,U.Green));fragile.Children.Add(Ct("获得 4 护盾，本回合破盾反击 8。\n下回合多抽 1 张，每回合最多一次。",13));fragile.Children.Add(Cb("选择脆泡泡",()=>G.PickShieldBranch(2,rev),true,false,"cards-branch-fragile"));p.Children.Add(U.Card(fragile,12));
        p.Children.Add(Cb("这次保持原样",()=>G.PickShieldBranch(0,rev),true,false,"cards-branch-skip"));
        p.Children.Add(Ct("不会花材料，也不会自动出发。强化过的盾牌保留 +3 护盾。",12,U.Muted));
    }
}
