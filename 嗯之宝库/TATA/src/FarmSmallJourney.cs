using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool SmallJourney{get{return SimpleCourtyard&&Tab=="冒险"&&!G.AtHome&&G.R.Casual;}}
    private FarmRun smallJourneyDetailsRun;
    private bool smallJourneyDetailsOpen;
    private bool CurrentSmallRun(FarmRun run){return (SmallJourney||FirstSmallJourney)&&Object.ReferenceEquals(run,G.R)&&run.Phase!="result";}
    private Button SmallPause(FarmRun run)
    {
        bool paused=run.JourneyPaused;int station=run.Node,visit=courtyardVisit,revision=smallRetreatRevision;string phase=run.Phase;
        return Btn(paused?"继续这趟旅程":"暂停旅程",()=>{if(CurrentSmallRun(run)&&courtyardVisit==visit&&smallRetreatRevision==revision&&run.Node==station&&run.Phase==phase&&run.JourneyPaused==paused)G.ToggleJourneyPause();},true,paused);
    }
    private void SmallJourneyPage()
    {
        if(G.R.Phase=="result"){SmallJourneyReturn();return;}
        if(SmallRetreatVisible){SmallRetreatPage();return;}
        if(G.R.Phase=="inspiration"){SmallInspirationPage();return;}
        var run=G.R;bool paused=run.JourneyPaused,battle=run.Phase=="battle";var panel=GentleColumn(450);
        panel.Children.Add(GentleText(paused?"先在这里歇一会儿":battle?"遇到小怪啦":run.Node==2?"伙伴在营地歇脚":"他们在慢慢往前走",21));
        var scene=Scene();scene.Journey=scene.SmallTravel=true;scene.Height=scene.MinHeight=120;scene.Margin=new Thickness(0,0,0,5);panel.Children.Add(scene);
        panel.Children.Add(GentleTravelProgress());
        panel.Children.Add(SmallJourneyStatus(run,paused?"已暂停，点继续才会往前走。":"伙伴会自己走完这一趟，你可以先去忙。"));
        var home=SmallWaitAtHome(run);var pause=SmallPause(run);
        HighlightGuideAction(paused?pause:home);U.Row(panel,paused?pause:home,paused?home:pause);
        if(!Object.ReferenceEquals(smallJourneyDetailsRun,run)){smallJourneyDetailsRun=run;smallJourneyDetailsOpen=false;}
        var details=new StackPanel();details.Children.Add(SmallRetreatEntry(run));SmallHealthDetails(details,run);details.Children.Add(U.Text(FarmGame.ExpeditionName(run.Difficulty,run.Depth),14,U.Green));
        details.Children.Add(Live(()=>run.Phase=="battle"?"第 "+run.Round+" 回合":"第 "+(run.Node+1)+" / 5 站",12,U.Muted));
        details.Children.Add(Live(()=>String.IsNullOrEmpty(run.LastCombo)?"配好的技能会自动发挥作用。":run.LastCombo,12,U.Green));
        details.Children.Add(Live(()=>run.Log,12,U.Muted));
        var expand=new Expander{Header="旅程详情与返家",Content=details,IsExpanded=smallJourneyDetailsOpen,Margin=new Thickness(0,8,0,0)};
        expand.Expanded+=(s,e)=>smallJourneyDetailsOpen=true;expand.Collapsed+=(s,e)=>smallJourneyDetailsOpen=false;AutomationProperties.SetAutomationId(expand,"small-trip-details");panel.Children.Add(expand);
    }
    private void SmallInspirationPage()
    {
        var run=G.R;int station=run.Node,recommended=G.RecommendedInspiration,visit=courtyardVisit,revision=smallRetreatRevision;
        if(!Object.ReferenceEquals(inspirationChoiceRun,run)||inspirationChoiceStation!=station){inspirationChoiceRun=run;inspirationChoiceStation=station;inspirationChoicesOpen=false;}
        var panel=GentleColumn(450);panel.Children.Add(GentleText("路上有个新点子",21));
        var clock=Live(()=>run.JourneyPaused?"已暂停，选好后也会等你。":"约 "+Math.Max(0,(int)Math.Ceiling((run.NextJourneyTicks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond))+" 秒后，伙伴会自己挑。",12,U.Green);clock.TextAlignment=TextAlignment.Center;panel.Children.Add(clock);
        var options=Btn(inspirationChoicesOpen?"收起，交给伙伴选":"我想换一个",()=>{if(CurrentSmallRun(run)&&courtyardVisit==visit&&smallRetreatRevision==revision&&run.Phase=="inspiration"&&run.Node==station){inspirationChoicesOpen=!inspirationChoicesOpen;resetScroll=true;}});
        var pause=SmallPause(run);
        if(inspirationChoicesOpen)U.Row(panel,options,pause);
        foreach(int offered in inspirationChoicesOpen?new[]{recommended}.Concat(run.InspirationOffers.Where(i=>i!=recommended)):new[]{recommended}){
            int id=offered;bool best=id==recommended;var content=new StackPanel();var row=new DockPanel();content.Children.Add(row);
            var picture=new InspirationView{Ability=id,Width=40,Height=40,Margin=new Thickness(0,0,10,0)};DockPanel.SetDock(picture,Dock.Left);row.Children.Add(picture);
            var name=U.Text(FarmGame.InspirationNames[id]+(best?" · 推荐":""),16,U.Green);name.Margin=new Thickness(0);name.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(name);
            var description=U.Text(GentleInspirationEffects[id],13);description.Margin=new Thickness(0,7,0,8);content.Children.Add(description);
            string label=inspirationChoicesOpen?"选「"+FarmGame.InspirationNames[id]+"」":run.JourneyPaused?"选这个，继续暂停":"就选这个，继续走";
            var choose=Btn(label,()=>{if(CurrentSmallRun(run)&&courtyardVisit==visit&&smallRetreatRevision==revision&&run.Phase=="inspiration"&&run.Node==station)G.ChooseInspiration(id,station);},true,best);
            if(best)HighlightGuideAction(choose);AutomationProperties.SetAutomationId(choose,"inspiration-choice-"+id);content.Children.Add(choose);
            var card=U.Card(content,12);if(best)card.Background=U.Pale;panel.Children.Add(card);
        }
        if(!inspirationChoicesOpen)U.Row(panel,options,pause);
        var home=SmallWaitAtHome(run);home.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(home);
        var details=new Expander{Header="这次想先回家",Content=SmallRetreatEntry(run),Margin=new Thickness(0,3,0,0)};AutomationProperties.SetAutomationId(details,"small-inspiration-return");panel.Children.Add(details);
    }
    private void SmallJourneyReturn()
    {
        if(CrystalJourneyReturn(G.R))return;
        var run=G.R;var panel=GentleColumn(430);bool won=run.Outcome=="victory";
        panel.Children.Add(GentleText(won?"带着收获回来啦":run.Withdrew?"回来歇一会儿吧":"先回家休息吧",21));
        panel.Children.Add(Portrait(G.GuideCompanion,66));
        panel.Children.Add(RewardTile("fertilizer","肥料",run.RewardFertilizer,"这趟已经收好"));
        panel.Children.Add(GentleText(won?"回家后，可以给田施肥。":"途中拿到的东西都保留，伙伴没有永久损伤。",12,U.Green));
        panel.Children.Add(GentleAction("返家 · 回小院",()=>ReturnFromReceipt(run,"农场")));
        panel.Children.Add(GuideReturnExtras(run));
    }
}
