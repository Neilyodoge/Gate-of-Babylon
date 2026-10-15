using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmGame
{
    public readonly bool GuideTest;
    public bool GuideActive{get{return S.GuideStep>=0&&S.GuideStep<8;}}
    public bool ResetGuideTest()
    {
        if(!GuideTest)return false;
        S=new FarmSave{GuideStep=0,GuideFarmLesson=0,GuideDepartureLesson=0,DesktopHotkeys=false};RecentBerryHarvest=null;LoadWarning="";LastHarvest="成熟后自动收获并入库，无需手动领取。";savePending=batchSaving=false;Save();return true;
    }
    public bool AdvanceGuideTest()
    {
        if(!GuideTest)return false;
        DateTime now=DateTime.UtcNow,fast=now.AddSeconds(30);TickFarm(fast);AdvanceJourney(fast);
        // Translate the simulated clock back to wall time, retaining time until
        // the next action. Subsequent real ticks must not wait for a future clock.
        S.LastFarmTicks=now.Ticks;if(JourneyRunning)R.NextJourneyTicks-=TimeSpan.FromSeconds(30).Ticks;Save();return true;
    }
    public void SyncGuide()
    {
        int before=S.GuideStep;if(S.GuideFarmLesson< -1||S.GuideFarmLesson>2)S.GuideFarmLesson=-1;
        if(S.GuideDepartureLesson< -1||S.GuideDepartureLesson>1)S.GuideDepartureLesson=-1;
        if(S.GuideStep>=1&&S.GuideStep<=3&&!AtHome)S.GuideStep=4;
        if(S.GuideStep==6&&!AtHome)S.GuideStep=4;
        if(S.GuideStep==1&&S.GuideInWorld&&S.Xp[GuideCompanion]>0)S.GuideStep=3;
        // A single real feed teaches the action. Older partly completed lessons also move on.
        if(S.GuideStep==2&&S.Xp[GuideCompanion]>0)S.GuideStep=3;
        if(S.GuideStep==3&&!AtHome)S.GuideStep=4;
        if(S.GuideStep==4&&R.Phase=="result")S.GuideStep=5;
        if((S.GuideStep==4||S.GuideStep==5)&&AtHome){S.GuideStep=S.Wins>0||S.Fertilizer>=SoilCost?6:3;S.GuideOutingAccepted=false;}
        if(S.GuideStep==6&&S.SoilLevel>=1)S.GuideStep=7;
        if(before!=S.GuideStep)Save();
    }
}

public partial class FarmWindow
{
    public void RestartGuideTest()
    {
        if(!G.ResetGuideTest())return;
        if(Panel!=null)Panel.ResetGuideView();BuildDesktop();ApplyDesktopPreferences();Refresh();
    }
}

public partial class FarmPanel
{
    internal void ResetGuideView(){lifeRequestedCrop=-1;plantingBerryEvolution=false;berryForEvolution=false;guideBerryState=null;guideBerryPlanting=false;guideBerryPlot=-1;guideBerryRevision++;lifeCareResult=null;growthReceipt=null;lessonPlotState=null;lessonPlot=-1;companionMoment=null;touchState=null;touchedPet=-1;touchCount=0;touchUntil=DateTime.MinValue;inspirationChoiceRun=null;inspirationChoicesOpen=false;Go("新手引导");ResetPanelInput();focusedPet=-1;focusedCareReceipt="";evolutionReceipt="";homeSoilReceipt="";journeyDifficulty=journeyDepth=0;selectedBlessing=-1;journeyBlessingsOpen=journeyConfigOpen=travelReportOpen=false;Array.Clear(petDetailsOpen,0,petDetailsOpen.Length);resetScroll=true;flashUntil=DateTime.MinValue;}
    private void OpenGuide(){if(!G.GuideActive){G.S.GuideStep=0;G.S.GuideInWorld=false;G.ResetHomeGuide();G.Save();}Go("新手引导");resetScroll=true;}
    private void GuideAction(FarmSave state,int step,Action action)
    {
        if(!Object.ReferenceEquals(state,G.S)||G.S.GuideStep!=step)return;action();G.SyncGuide();G.Save();resetScroll=true;
    }
}
