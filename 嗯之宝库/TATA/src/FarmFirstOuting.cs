using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmRun comboDetailRun;
    private bool guideComboOpen;
    private int departureSession;
    private bool SmallDeparture{get{return WorldGuide&&Tab=="冒险"&&G.AtHome&&(G.S.GuideStep==3&&G.S.GuideDepartureLesson>=0||G.S.GuideStep==6);}}
    private void FirstDeparturePage()
    {
        var state=G.S;int step=state.GuideStep,lesson=step==6?1:state.GuideDepartureLesson,session=departureSession,picnic=state.PlannedPicnic;var panel=GentleColumn(470);
        Func<bool> current=()=>Object.ReferenceEquals(state,G.S)&&session==departureSession&&SmallDeparture&&state.GuideStep==step&&(step==6||state.GuideOutingAccepted);
        if(lesson==0){
            panel.Children.Add(GentleText("朋友会陪泡泡一起去",22));
            var party=new UniformGrid{Columns=state.Team.Length,Margin=new Thickness(0,14,0,12)};panel.Children.Add(party);
            foreach(int pet in state.Team){var card=new StackPanel();card.Children.Add(Portrait(pet,96));card.Children.Add(GentleText(G.Name(pet),14));party.Children.Add(card);}
            panel.Children.Add(GentleText("大家已经准备好了。",15,U.Green));
            panel.Children.Add(GentleAction("看看要去的地方",()=>{
                if(!current()||state.GuideDepartureLesson!=0)return;
                state.GuideDepartureLesson=1;G.Save();resetScroll=true;
            }));
        }else{
            if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth)){journeyDifficulty=journeyDepth=0;}
            panel.Children.Add(GentleText(journeyDifficulty==0?"去小溪走一圈":"去洞穴探探路",22));
            var view=Scene();view.Journey=view.JourneyPreview=true;view.PreviewRegion=journeyDifficulty;view.Height=view.MinHeight=134;panel.Children.Add(view);
            panel.Children.Add(GentleText("约 2～4 分钟，走完这一趟就回来。",14,U.Green));
            if(state.PlannedPicnic>=0)PicnicDeparture(panel);else panel.Children.Add(GentleText("不用准备材料。",12,U.Muted));
            var start=GentleAction(state.PlannedPicnic<0?"出发 · 去"+(journeyDifficulty==0?"小溪":"洞穴")+"看看":"制作便当并出发",()=>{
                if(!current()||step!=6&&state.GuideDepartureLesson!=1||state.PlannedPicnic!=picnic||!G.CanPackPicnic(picnic))return;
                G.EnsureJourneyOffers();if(!state.BlessingOffers.Contains(selectedBlessing))selectedBlessing=state.BlessingOffers[0];
                if(G.BeginJourney(journeyDifficulty,selectedBlessing,journeyDepth)){autoplay=false;confirmRetreat=false;}
            },G.CanPackPicnic(state.PlannedPicnic));panel.Children.Add(start);updates.Add(()=>start.IsEnabled=G.CanPackPicnic(state.PlannedPicnic));
        }
        HomeGuideSecondary(panel,"先待在家里",()=>{
            if(current())CancelOutingPreparation();
        });
    }
    private UIElement GentleTravelProgress()
    {
        var panel=new StackPanel{Margin=new Thickness(0,0,0,12)};
        var label=Live(()=>"已走过 "+Math.Max(0,G.R.Node)+" / 5 站",12,U.Muted);label.Margin=new Thickness(0,0,0,6);panel.Children.Add(label);
        var bar=U.Bar(G.R.Node,5);bar.Height=9;bar.Margin=new Thickness(0);AutomationProperties.SetAutomationId(bar,"gentle-trip-progress");panel.Children.Add(bar);
        updates.Add(()=>bar.Value=G.R.Node);return panel;
    }
    private void GentleTravelAside(StackPanel aside)
    {
        var run=G.R;var controls=new StackPanel();
        controls.Children.Add(Live(()=>run.JourneyPaused?"旅程停在这里":run.Phase=="inspiration"?"不用急着做决定":"可以去忙自己的事",17,U.Green));
        controls.Children.Add(Live(()=>run.JourneyPaused?"点继续，才会往前走。":run.Phase=="inspiration"?"不选也没关系，伙伴会自己挑。":"点右上角「缩成宠物」，想看时双击它回来。",12,U.Muted));
        bool paused=run.JourneyPaused;
        var pause=Btn(paused?"继续这趟旅程":"暂停旅程",()=>{if(Object.ReferenceEquals(run,G.R)&&run.JourneyPaused==paused&&run.Casual&&!G.AtHome&&run.Phase!="result")G.ToggleJourneyPause();},true,paused);
        if(WorldGuide&&run.JourneyPaused)HighlightGuideAction(pause);controls.Children.Add(pause);
        controls.Children.Add(Btn("回农场看看",()=>Go("农场")));aside.Children.Add(U.Card(controls,12));
        if(WorldGuide){
            if(!Object.ReferenceEquals(comboDetailRun,run)){comboDetailRun=run;guideComboOpen=false;}
            var details=new Expander{Header="看看伙伴怎么配合",Content=Live(GuideCombinationEvidence,12,U.Green),IsExpanded=guideComboOpen,Margin=new Thickness(0,5,0,0)};
            details.Expanded+=(s,e)=>guideComboOpen=true;details.Collapsed+=(s,e)=>guideComboOpen=false;AutomationProperties.SetAutomationId(details,"guide-combo-details");aside.Children.Add(details);
        }
    }
}
