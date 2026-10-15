using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool FirstSmallJourney{get{return WorldGuide&&Tab=="冒险"&&!G.AtHome&&G.R.Casual&&G.R.Phase!="result";}}
    private void FirstSmallJourneyPage()
    {
        if(SmallRetreatVisible){SmallRetreatPage();return;}
        if(G.R.Phase=="inspiration"){SmallInspirationPage();return;}
        var run=G.R;var panel=GentleColumn(450);bool paused=run.JourneyPaused;
        panel.Children.Add(GentleText(paused?"先在这里歇一会儿":run.Phase=="battle"?"遇到小怪啦":"他们会自己往前走",21));
        var scene=Scene();scene.Journey=scene.SmallTravel=true;scene.Height=scene.MinHeight=118;panel.Children.Add(scene);panel.Children.Add(GentleTravelProgress());
        panel.Children.Add(SmallJourneyStatus(run,paused?"旅程已暂停，等你点继续。":"可以点右上角「缩成宠物」去忙。"));
        var pause=SmallPause(run);if(paused)HighlightGuideAction(pause);
        U.Row(panel,pause,SmallWaitAtHome(run));
        if(!Object.ReferenceEquals(comboDetailRun,run)){comboDetailRun=run;guideComboOpen=false;}
        var contents=new StackPanel();contents.Children.Add(SmallRetreatEntry(run));SmallHealthDetails(contents,run);contents.Children.Add(Live(GuideCombinationEvidence,12,U.Green));
        var details=new Expander{Header="旅程详情与返家",Content=contents,IsExpanded=guideComboOpen,Margin=new Thickness(0,7,0,0)};
        details.Expanded+=(s,e)=>guideComboOpen=true;details.Collapsed+=(s,e)=>guideComboOpen=false;AutomationProperties.SetAutomationId(details,"guide-combo-details");panel.Children.Add(details);
    }
}
