using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Interop;

public partial class FarmPanel
{
    private void ReviewTarget(Control target,string label,bool reveal=false)
    {
        var viewport=UIChildren<ScrollContentPresenter>(scroller).First();
        if(reveal){
            var point=target.TranslatePoint(new Point(0,0),viewport);
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset+point.Y);scroller.UpdateLayout();
        }
        var at=target.TranslatePoint(new Point(0,0),viewport);
        FarmHeadlessTests.Check(CanKeyboardReach(target)&&target.ActualWidth>0&&target.ActualHeight>0&&at.Y>=-.5&&at.Y+target.ActualHeight<=viewport.ActualHeight+.5,"review target within actual viewport: "+label);
    }
    private void ReviewFrame(string root,string name,int size)
    {
        ReadingPreview(root,"ux27-"+name,size,true);
        FarmHeadlessTests.Check(WantsGentleNarrow,"review retains narrow presentation: "+name);
    }
    internal void RenderUXReviewChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++){
            foreach(bool first in new[]{true,false}){
                LaterJourneyFixture();
                if(first){G.S.SimpleCourtyard=false;G.S.GuideStep=4;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;}
                string prefix=first?"first-":"repeat-";
                ReviewFrame(root,prefix+"travel",size);
                var wait=UIAction("回小院等他们");var details=LaterExpander(first?"guide-combo-details":"small-trip-details");
                ReviewTarget(wait,prefix+"wait");ReviewTarget(details,prefix+"return-disclosure");
                check(Convert.ToString(details.Header)=="旅程详情与返家","first and repeat trips expose the same explicit return location");
                string before=FarmPracticeTests.Xml(G.S);details.IsExpanded=true;UIPreview(root,"ux27-"+prefix+"details-"+size,484,501);
                var retreat=UIAction("提前回家…");
                check(Object.ReferenceEquals(((StackPanel)details.Content).Children[0],retreat),"return action precedes potentially long battle evidence");
                ReviewTarget(retreat,prefix+"retreat",true);check(FarmPracticeTests.Xml(G.S)==before,"expanding and scrolling details is read-only");
                GuideClick(retreat);ReviewFrame(root,prefix+"confirm",size);
                check(SmallRetreatVisible,"explicit return entry opens a separate decision");
                wait.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(SmallRetreatVisible&&Tab=="冒险"&&FarmPracticeTests.Xml(G.S)==before,"stale wait button cannot dismiss a return decision");
                KeyPress(Key.Escape);ReviewFrame(root,prefix+"cancelled",size);
                GuideClick(UIAction("暂停旅程"));ReviewFrame(root,prefix+"paused",size);
                before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("回小院等他们"));ReviewFrame(root,prefix+"waiting-home",size);
                check(Tab=="农场"&&G.R.JourneyPaused&&scenes.Single().HideFarmPets&&FarmPracticeTests.Xml(G.S)==before,"waiting at home preserves pause and does not duplicate travelling companions");
                GuideClick(GuidePrimary());ReviewFrame(root,prefix+"back-to-trip",size);GuideClick(UIAction("继续这趟旅程"));
                ReachGentleInspiration();ReviewFrame(root,prefix+"inspiration",size);GuideClick(UIAction("暂停旅程"));ReviewFrame(root,prefix+"choice-paused",size);
                GuideClick(UIAction("我想换一个"));ReviewFrame(root,prefix+"choices",size);
                details=LaterExpander("small-inspiration-return");ReviewTarget(details,prefix+"expanded-return",true);
                details.IsExpanded=true;scroller.UpdateLayout();retreat=UIAction("提前回家…");ReviewTarget(retreat,prefix+"expanded-return-action",true);
                UIPreview(root,"ux27-"+prefix+"choices-return-"+size,484,501);
                before=FarmPracticeTests.Xml(G.S);int picks=G.R.InspirationPicks.Length;GuideClick(retreat);ReviewFrame(root,prefix+"choices-confirm",size);
                check(SmallRetreatVisible&&G.R.JourneyPaused&&G.R.InspirationPicks.Length==picks,"expanded options can reach return without choosing or resuming");
                KeyPress(Key.Escape);ReviewFrame(root,prefix+"choices-cancelled",size);
                check(inspirationChoicesOpen&&G.R.JourneyPaused&&FarmPracticeTests.Xml(G.S)==before,"cancel restores the open choices and original paused run");
            }

            G.ResetGuideTest();ResetGuideView();G.S.GuideStep=6;G.S.GuideInWorld=true;G.S.Wins=1;G.S.Fertilizer=0;
            G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(1).Ticks;Go("农场");ReviewFrame(root,"soil-shortage",size);
            var recover=GuidePrimary();ReviewTarget(recover,"soil recovery");
            check(Convert.ToString(recover.Content)=="再去小溪收集肥料"&&recover.MinHeight>=44&&!UIChildren<Button>(body).Any(b=>!b.IsEnabled),"first soil shortage has one actionable primary and no disabled purchase");
            int level=G.S.SoilLevel;GuideClick(recover);ReviewFrame(root,"soil-preparation",size);
            check(G.AtHome&&Tab=="冒险"&&G.S.Fertilizer==0&&G.S.SoilLevel==level,"recovery only opens preparation without spending or departure");
            KeyPress(Key.Escape);ReviewFrame(root,"soil-cancelled",size);string unchanged=FarmPracticeTests.Xml(G.S);
            recover.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="农场"&&FarmPracticeTests.Xml(G.S)==unchanged,"old recovery cannot reopen preparation after cancellation");
            G.S.Fertilizer=G.SoilCost;ReviewFrame(root,"soil-ready",size);var apply=GuidePrimary();ReviewTarget(apply,"soil purchase");
            Go("伙伴");Go("农场");ReviewFrame(root,"soil-revisited",size);unchanged=FarmPracticeTests.Xml(G.S);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==unchanged,"old soil purchase cannot spend after navigation");
            GuideClick(GuidePrimary());ReviewFrame(root,"soil-applied",size);
            check(G.S.SoilLevel==level+1&&G.S.Fertilizer==0&&G.ValidSoilReceipt!=null,"revisited live purchase still applies exactly once");

            LaterJourneyFixture();G.S.SimpleCourtyard=false;G.S.GuideStep=4;G.S.GuideInWorld=true;
            G.AdvanceJourney(DateTime.UtcNow.AddHours(1));ReviewFrame(root,"first-receipt",size);
            ReviewTarget(GuidePrimary(),"first receipt home action");
            ReviewTarget(UIChildren<Expander>(body).Single(),"first receipt optional rewards");
        }
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=6;G.S.GuideInWorld=true;G.S.Wins=1;G.S.Fertilizer=0;G.S.Xp=Enumerable.Repeat(55,FarmGame.PetCount).ToArray();Go("农场");
        ReviewFrame(root,"recovery-loop-shortage",0);GuideClick(GuidePrimary());ReviewFrame(root,"recovery-loop-preparation",0);
        var departure=GuidePrimary();GuideClick(departure);ReviewFrame(root,"recovery-loop-travel",0);var run=G.R;string running=FarmPracticeTests.Xml(G.S);
        departure.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!G.AtHome&&Object.ReferenceEquals(run,G.R)&&FarmPracticeTests.Xml(G.S)==running,"explicit recovery departure starts once without changing the run on a second activation");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));ReviewFrame(root,"recovery-loop-receipt",0);int fertilizer=G.S.Fertilizer;
        check(fertilizer>=G.SoilCost&&G.S.GuideStep==5,"recovery journey records real rewards");GuideClick(GuidePrimary());ReviewFrame(root,"recovery-loop-soil",0);
        check(G.S.GuideStep==6&&G.AtHome,"recovery returns to the unfinished fertilizer lesson");int cost=G.SoilCost;GuideClick(GuidePrimary());ReviewFrame(root,"recovery-loop-complete",0);
        check(G.S.GuideStep==7&&G.S.Fertilizer==fertilizer-cost&&G.ValidSoilReceipt!=null,"only explicit application completes the original lesson and charges its actual cost");
        G.ResetGuideTest();ResetGuideView();
        check(G.SavePath==null&&!IsVisible&&!owner.IsVisible&&new WindowInteropHelper(this).Handle==IntPtr.Zero&&new WindowInteropHelper(owner).Handle==IntPtr.Zero,"review remains detached and tutorial-only");
        File.WriteAllText(Path.Combine(root,"ux-review-regression.txt"),"PASS: first/repeat journey return labels and disclosure; return before long logs; actual viewport and scroll reachability at 100/115/130%; waiting at home preserves pause and suppresses duplicate pets; stale waiting cannot dismiss return confirmation; expanded inspiration choices retain return access and cancel back to the unchanged paused choice; first fertilizer shortage has an enabled recovery action; recovery is preparation only; cancellation and old controls cannot reopen or spend; one actual fertilizer transaction; first receipt action and optional rewards fit. Detached WPF; no native windows or production saves.");
    }
}
