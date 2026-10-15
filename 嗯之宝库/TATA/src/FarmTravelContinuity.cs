using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmGame
{
    private void NormalizeDestination()
    {
        if(S.PlannedRegion<0||S.PlannedRegion>1||!ExpeditionUnlocked(S.PlannedRegion,0))S.PlannedRegion=0;
        if(S.PlannedDepth<0||S.PlannedDepth>2)S.PlannedDepth=0;
        while(S.PlannedDepth>0&&!ExpeditionUnlocked(S.PlannedRegion,S.PlannedDepth))S.PlannedDepth--;
    }
    public bool PlanDestination(int region,int depth)
    {
        if(!AtHome||!ExpeditionUnlocked(region,depth))return false;
        if(S.PlannedRegion==region&&S.PlannedDepth==depth)return true;
        S.PlannedRegion=region;S.PlannedDepth=depth;Save();return true;
    }
}

public partial class FarmPanel
{
    private FarmRun smallRetreatRun;
    private int smallRetreatVisit,smallRetreatRevision;
    private bool SmallRetreatVisible{get{return smallRetreatRun!=null&&CurrentSmallRun(smallRetreatRun)&&smallRetreatVisit==courtyardVisit;}}
    private void SelectJourneyDestination(int region,int depth)
    {
        if(!G.PlanDestination(region,depth))return;
        journeyDifficulty=region;journeyDepth=depth;
    }
    private void CloseSmallRetreat(){smallRetreatRun=null;smallRetreatRevision++;}
    private void OpenSmallRetreat(FarmRun run)
    {
        if(!CurrentSmallRun(run))return;
        smallRetreatRun=run;smallRetreatVisit=courtyardVisit;smallRetreatRevision++;resetScroll=true;keyboardFocusKey="";
    }
    private Button SmallRetreatEntry(FarmRun run)
    {
        int visit=courtyardVisit,revision=smallRetreatRevision;
        return Btn("提前回家…",()=>{if(CurrentSmallRun(run)&&courtyardVisit==visit&&smallRetreatRevision==revision)OpenSmallRetreat(run);});
    }
    private Button SmallWaitAtHome(FarmRun run)
    {
        int visit=courtyardVisit,revision=smallRetreatRevision;
        var button=Btn("回小院等他们",()=>{if(CurrentSmallRun(run)&&courtyardVisit==visit&&smallRetreatRevision==revision)Go("农场");});
        AutomationProperties.SetHelpText(button,"你回小院，伙伴仍留在旅途中；已暂停的旅程会继续保持暂停。");
        return button;
    }
    private void SmallRetreatPage()
    {
        var run=G.R;int revision=smallRetreatRevision,visit=courtyardVisit,station=run.Node;string phase=run.Phase;bool paused=run.JourneyPaused;
        Func<bool> current=()=>SmallRetreatVisible&&Object.ReferenceEquals(run,smallRetreatRun)&&revision==smallRetreatRevision&&visit==courtyardVisit&&run.Node==station&&run.Phase==phase&&run.JourneyPaused==paused;
        var panel=GentleColumn(450);panel.Children.Add(GentleText("这次先回家吗？",22));
        var scene=Scene();scene.Journey=scene.SmallTravel=true;scene.Height=scene.MinHeight=92;panel.Children.Add(scene);
        panel.Children.Add(GentleTravelProgress());
        panel.Children.Add(GentleText(paused?"旅程仍然暂停，等你决定。":"伙伴还在往前走，你可以慢慢决定。",12,U.Green));
        panel.Children.Add(GentleText("途中收获会保留。\n还没拿到的首领奖励，下次再来。",13));
        var stay=GentleAction(paused?"保留暂停，回旅程":"再待一会儿",()=>{if(current()){CloseSmallRetreat();resetScroll=true;}});stay.MinWidth=0;stay.HorizontalAlignment=HorizontalAlignment.Stretch;
        var leave=Btn("确认返家",()=>{if(current()){CloseSmallRetreat();G.Retreat();resetScroll=true;}});
        U.Row(panel,stay,leave);
        AutomationProperties.SetAutomationId(panel,"small-retreat-confirmation");
    }
}
