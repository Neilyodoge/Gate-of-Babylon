using System;
using System.Linq;
using System.Text.RegularExpressions;

internal sealed class MossCardLesson
{
    internal bool Active,End;
    internal int Slot=-1;
    internal string Text="";
}
public partial class FarmGame
{
    internal MossCardLesson CardFirstTurnLesson()
    {
        var lesson=new MossCardLesson();
        if(C.Phase!="battle"||C.Stage!=0||C.CompletedRuns!=0||C.Turn!=1)return lesson;
        lesson.Active=true;
        var playable=Enumerable.Range(0,C.Hand.Count).Where(i=>MossCards.All[MossCards.Id(C.Hand[i])].Cost<=C.Energy).ToArray();
        if(playable.Length==0){lesson.End=true;lesson.Text="点「结束回合」，敌人行动后补能量、抽牌。";return lesson;}
        int water=Array.Find(playable,i=>MossCards.Id(C.Hand[i])==0);
        bool hasWater=playable.Any(i=>MossCards.Id(C.Hand[i])==0);
        int spark=Array.Find(playable,i=>MossCards.Id(C.Hand[i])==1);
        bool hasSpark=playable.Any(i=>MossCards.Id(C.Hand[i])==1);
        if(C.Wet>0&&hasSpark){lesson.Slot=spark;lesson.Text="敌人潮湿了，可以打「小小电花」连招。";}
        else if(hasWater&&hasSpark&&C.Energy>=2){lesson.Slot=water;lesson.Text="先试试「泼水」。点牌底部的「打出」。";}
        else {
            bool shield=playable.Any(i=>MossCards.Id(C.Hand[i])==2);
            if(shield&&ForecastCards().Loss>0){lesson.Slot=Array.Find(playable,i=>MossCards.Id(C.Hand[i])==2);lesson.Text="可以打「"+CurrentCardName(C.Hand[lesson.Slot])+"」，挡住接下来的伤害。";}
            else {lesson.Slot=playable[0];lesson.Text="还可以试试「"+CurrentCardName(C.Hand[lesson.Slot])+"」。";}
        }
        return lesson;
    }
    internal string CardLessonMessage(MossCardLesson lesson)
    {
        // Old saves may retain the former count-based hint; remove it only for display.
        string message=(C.Message??"").Replace(" 潮湿了，再试试电花。","");
        message=Regex.Replace(message,@" 剩余 \d+ 能量，可用泡泡盾。","");
        if(!lesson.Active)return message;
        return C.Played==0?lesson.Text:message+" "+lesson.Text;
    }
}
public partial class FarmPanel
{
    private MossCardLesson cardLesson;
}
