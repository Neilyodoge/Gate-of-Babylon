using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private int JourneyHealth(int pet){return Math.Max(0,Math.Min(G.MaxHp(pet),G.R.Hp[pet]));}
    private string JourneyHealthSummary(FarmRun run)
    {
        return "伙伴体力 "+G.S.Team.Sum(p=>JourneyHealth(p))+" / "+G.S.Team.Sum(p=>G.MaxHp(p))+(run.JourneyPaused?" · 已暂停":" · 自动交战中");
    }
    private TextBlock SmallJourneyStatus(FarmRun run,string other)
    {
        var line=Live(()=>run.Phase=="battle"?JourneyHealthSummary(run):other,12,U.Green);line.TextAlignment=TextAlignment.Center;
        AutomationProperties.SetAutomationId(line,"small-party-health");
        AutomationProperties.SetHelpText(line,"体力不包含护盾；每位伙伴的体力与护盾可在旅程详情中查看。");return line;
    }
    private void SmallHealthDetails(StackPanel details,FarmRun run)
    {
        if(run.Phase!="battle")return;
        foreach(int member in G.S.Team){
            int pet=member;var line=Live(()=>G.Name(pet)+" · 体力 "+JourneyHealth(pet)+" / "+G.MaxHp(pet)+(run.Hp[pet]<=0?" · 歇一会儿":run.Shield[pet]>0?" · 护盾 "+run.Shield[pet]:""),12,U.Green);
            line.Margin=new Thickness(0,4,0,3);details.Children.Add(line);AutomationProperties.SetAutomationId(line,"small-health-pet-"+pet);
            var bar=U.Bar(0,1);bar.Height=8;bar.Margin=new Thickness(0,0,0,5);details.Children.Add(bar);
            AutomationProperties.SetAutomationId(bar,"small-health-bar-"+pet);
            updates.Add(()=>{bar.Maximum=G.MaxHp(pet);bar.Value=JourneyHealth(pet);AutomationProperties.SetName(bar,G.Name(pet)+"体力 "+JourneyHealth(pet)+" / "+G.MaxHp(pet));});
        }
    }
}
