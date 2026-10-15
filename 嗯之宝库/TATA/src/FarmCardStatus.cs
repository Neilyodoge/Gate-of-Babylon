using System;
using System.Collections.Generic;
using System.Linq;

public partial class FarmGame
{
    internal string CardEffectSummary()
    {
        if(C.Phase!="battle")return "";
        var active=new List<string>();
        if(C.KeepShield&&C.Block>0)active.Add("余盾保留 1 次");
        if(C.FragileShield&&C.Block>0)active.Add("破盾后反击 8，下回合多抽 1");
        if(C.Reflect>0)active.Add("受击反击 "+C.Reflect+" / 次");
        if(C.Weak>0)active.Add("敌人"+(C.RulesVersion==0?"本回合":"下次")+"攻击 −3 / 段");
        return active.Count==0?"":"当前效果："+String.Join(" · ",active);
    }
    internal string CardBattleNote(MossCardLesson lesson)
    {
        return String.Join("\n",new[]{CardLessonMessage(lesson),CardEffectSummary(),CardHabit}.Where(s=>s!=""));
    }
}
