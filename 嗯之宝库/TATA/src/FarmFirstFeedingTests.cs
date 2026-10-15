using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

public partial class FarmPanel
{
    private void FeedingTargetCheck()
    {
        var button=GuidePrimary();var root=(FrameworkElement)Content;
        FarmHeadlessTests.Check(button.ActualWidth>=270&&button.ActualHeight>=56&&button.Content is string,"berry action keeps one large, named button");
        var berry=UIChildren<FeedingBerryIcon>(button).Single();
        var points=new[]{new Point(5,button.ActualHeight/2),new Point(button.ActualWidth-5,button.ActualHeight/2),berry.TranslatePoint(new Point(berry.ActualWidth/2,berry.ActualHeight/2),button)};
        var controls=KeyboardTargets();
        foreach(var point in points){
            // InputHitTest requires a presented window. Test the real drawing with the
            // normal enabled/hit-visible filters while keeping this fixture windowless.
            DependencyObject hit=null;
            VisualTreeHelper.HitTest(root,node=>{var ui=node as UIElement;return ui!=null&&(!ui.IsEnabled||!ui.IsHitTestVisible||ui.Visibility!=Visibility.Visible)?HitTestFilterBehavior.ContinueSkipSelfAndChildren:HitTestFilterBehavior.Continue;},result=>{hit=result.VisualHit;return HitTestResultBehavior.Stop;},new PointHitTestParameters(button.TranslatePoint(point,root)));
            FarmHeadlessTests.Check(Object.ReferenceEquals(InputControl(hit,controls),button),"berry artwork and padding hit the same feed button: "+point+" / "+(hit==null?"null":hit.GetType().Name));
        }
        GuideFits(button);
        var viewport=UIChildren<ScrollContentPresenter>(scroller).First();var inside=button.TranslatePoint(new Point(0,0),viewport);
        FarmHeadlessTests.Check(inside.Y>=0&&inside.Y+button.ActualHeight<=viewport.ActualHeight,"feed button fits the actual scroll viewport above the footer");
        foreach(var target in controls.Where(c=>Under(c,scroller))){var at=target.TranslatePoint(new Point(0,0),viewport);FarmHeadlessTests.Check(at.Y>=0&&at.Y+target.ActualHeight<=viewport.ActualHeight,"all first-feeding actions fit the real scroll viewport: "+ControlKey(target));}
    }
    internal void RenderFirstFeedingChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++){
            G.ResetGuideTest();ResetGuideView();ReadingPreview(root,"feeding-welcome",size,true);FeedingTargetCheck();
            check(companionViews.Count==1&&companionViews[0].FoodThought&&UIChildren<FeedingBerryIcon>((DependencyObject)Content).Count()==1,"welcome has one companion and one illustrated spending target");
            string before=FarmPracticeTests.Xml(G.S);GuideClick(TouchPortraitButton());ReadingPreview(root,"feeding-touch",size,true);
            check(FarmPracticeTests.Xml(G.S)==before&&G.S.GuideStep==0&&companionMoment.Kind=="touch","touching still responds for free without teaching the next concept");
            GuideClick(GuidePrimary());ReadingPreview(root,"feeding-first",size,true);ReviewTarget(GuidePrimary(),"single-feed next step");
            check(G.S.Berries==8&&G.S.Xp[0]==12&&G.S.Bond==1&&G.S.GuideStep==3,"illustrated feed consumes two berries and shows real growth");
            GuideClick(GuidePrimary());ReadingPreview(root,"feeding-farm",size,true);GuideClick(GuidePrimary());G.AdvanceGuideTest();
            GuidePreview(root,"feeding-harvest-"+size);ReadingPreview(root,"feeding-fresh",size,true);ReviewTarget(GuidePrimary(),"acknowledge harvest without another feed");
            int berries=G.S.Berries,xp=G.S.Xp[0];GuideClick(GuidePrimary());ReadingPreview(root,"feeding-complete",size,true);
            check(G.S.Berries==berries&&G.S.Xp[0]==xp&&G.S.GuideHomeStage==HomeGuideStage.Complete,"harvest acknowledgement spends nothing, does not feed again and shows the actual receipt");ReviewTarget(GuidePrimary(),"return to quiet home");
        }

        G.ResetGuideTest();ResetGuideView();ReadingPreview(root,"feeding-before-shortage",0,true);var stale=GuidePrimary();G.S.Berries=0;string unchanged=FarmPracticeTests.Xml(G.S);
        stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ReadingPreview(root,"feeding-shortage",0,true);
        check(FarmPracticeTests.Xml(G.S)==unchanged&&G.S.GuideStep==0&&!G.S.GuideInWorld&&!GuidePrimary().IsEnabled&&companionMoment==null,"stock loss between render and click cannot advance or fake the first feeding");
        var surface=Content;var waiting=GuidePrimary();owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(12),false,true,false);
        check(Object.ReferenceEquals(Content,surface)&&waiting.IsEnabled&&G.S.GuideStep==0&&G.S.Xp[0]==0&&GuideWords().Contains("点莓果按钮"),"production clock restores the same first-feed button without feeding automatically");
        UIPreview(root,"feeding-ready-in-place",484,501);int stock=G.S.Berries;GuideClick(waiting);ReadingPreview(root,"feeding-recovered",0,true);
        check(G.S.Berries==stock-2&&G.S.Xp[0]==12&&G.S.GuideStep==3,"one deliberate click feeds after stock recovers");unchanged=FarmPracticeTests.Xml(G.S);waiting.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==unchanged,"old first-feed targets cannot consume again");

        G.ResetGuideTest();ResetGuideView();G.S.GuideInWorld=true;G.S.GuideStep=2;Go("伙伴");ReadingPreview(root,"feeding-low-xp",0,true);stale=GuidePrimary();GuideClick(stale);Render();unchanged=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.S.GuideStep==3&&G.S.Xp[0]==12&&FarmPracticeTests.Xml(G.S)==unchanged,"old feed target cannot spend again after the single lesson");
        G.ResetGuideTest();ResetGuideView();ReadingPreview(root,"feeding-keyboard",0,true);KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"first Tab selects the illustrated feed button");KeyPress(Key.Enter);unchanged=FarmPracticeTests.Xml(G.S);KeyPress(Key.Enter,ModifierKeys.None,true);check(FarmPracticeTests.Xml(G.S)==unchanged&&G.S.Xp[0]==12,"holding Enter cannot eat the second portion");
        G.S.DesktopMotion=false;ReadingPreview(root,"feeding-motion-off",0,true);check(GuideWords().Contains("莓果真甜")&&CurrentCompanionMoment(0).Kind=="feed","motion-off preserves readable success");

        G.ResetGuideTest();ResetGuideView();G.S.Berries=0;for(int i=0;i<G.Plots;i++)G.S.Crops[i]=1;ReadingPreview(root,"feeding-no-berry-plot",2,true);GuideFits(GuidePrimary());
        check(Convert.ToString(GuidePrimary().Content)=="去种一点莓果","an old all-grain farm offers recovery instead of waiting forever");unchanged=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());GuidePreview(root,"feeding-planting");check(Tab=="找莓果"&&G.S.GuideStep==0&&FarmPracticeTests.Xml(G.S)==unchanged,"opening planting does not complete or charge the lesson");
        GuideClick(UIAction("先回去陪泡泡"));ReadingPreview(root,"feeding-planting-cancelled",2,true);check(G.S.GuideStep==0&&Tab=="新手引导","cancelled planting returns to the same first step");

        string path=Path.Combine(root,"first-feeding-save.xml");var normal=new FarmGame(path);normal.S.GuideStep=0;normal.S.GuideInWorld=false;normal.S.Berries=10;normal.S.Xp[0]=0;normal.S.Bond=0;normal.Save();
        var window=new FarmWindow(normal,true);var panel=new FarmPanel(window);window.Panel=panel;panel.ResetGuideView();panel.ReadingPreview(root,"feeding-normal",0,true);panel.GuideClick(panel.GuidePrimary());
        var reload=new FarmGame(path);check(reload.S.GuideStep==3&&reload.S.GuideInWorld&&reload.S.Berries==8&&reload.S.Xp[0]==12&&reload.HomeGuideResumeTab=="伙伴","normal save resumes after exactly one paid feed");
        var oldTouch=TouchPortraitButton();G.ResetGuideTest();ResetGuideView();Render();unchanged=FarmPracticeTests.Xml(G.S);oldTouch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==unchanged&&companionMoment==null,"old-session targets cannot affect a fresh test");
        check(G.SavePath==null&&!IsVisible&&!owner.IsVisible&&!window.IsVisible&&new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"first-feed checks remain detached with only an isolated normal-save fixture");
        File.WriteAllText(Path.Combine(root,"first-feeding-regression.txt"),"PASS: one illustrated 270x56 minimum feed target; actual art/padding hit tests; 100/115/130% minimum-window single first feed, next food-source action and harvest acknowledgement; free petting; real growth and cost; shortage between rendering and click; production-clock recovery without automatic feeding; stale controls in the same step and after reset; held Enter; motion-off receipt; old all-grain recovery and cancellation; isolated normal-save resume. No native windows or player-save access. Physical input and human comprehension not tested.");
    }
}
