using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;

// An event source for routed-event checks; it never owns a native window.
internal sealed class FarmDetachedInputSource:PresentationSource
{
    public override Visual RootVisual{get;set;}
    public override bool IsDisposed{get{return false;}}
    protected override CompositionTarget GetCompositionTargetCore(){return null;}
}

public partial class FarmPanel
{
    private static IEnumerable<GlyphRunDrawing> SceneGlyphs(Drawing drawing)
    {
        var glyph=drawing as GlyphRunDrawing;if(glyph!=null)yield return glyph;
        var group=drawing as DrawingGroup;if(group!=null)foreach(var child in group.Children)foreach(var run in SceneGlyphs(child))yield return run;
    }
    private static void CheckSceneReading(FarmScene scene,string root,string name,double width,double height,double scale)
    {
        scene.ReadingScale=scale;scene.InvalidateVisual();FarmHeadlessTests.RenderContent(scene,Path.Combine(root,"ui-final-scene-"+name+"-"+(scale*100).ToString("0")+".png"),width,height);
        var glyphs=SceneGlyphs(VisualTreeHelper.GetDrawing(scene)).ToArray();
        FarmHeadlessTests.Check(glyphs.Length>=2&&glyphs.All(g=>g.GlyphRun.FontRenderingEmSize>=10*scale-.01),"scene text uses reading size: "+name);
        foreach(var glyph in glyphs){var b=glyph.Bounds;FarmHeadlessTests.Check(b.Left>=-1&&b.Top>=-1&&b.Right<=width+1&&b.Bottom<=height+1,"scene label fits: "+name+" / "+new string(glyph.GlyphRun.Characters.ToArray()));}
        for(int i=0;i<glyphs.Length;i++)for(int j=i+1;j<glyphs.Length;j++){
            var intersection=Rect.Intersect(glyphs[i].Bounds,glyphs[j].Bounds);
            FarmHeadlessTests.Check(intersection.IsEmpty||intersection.Width<1||intersection.Height<1,"scene labels do not overlap: "+name+" / "+new string(glyphs[i].GlyphRun.Characters.ToArray())+" / "+new string(glyphs[j].GlyphRun.Characters.ToArray()));
        }
    }
    internal void RenderFinalUXChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"final-mouse-switch");KeyPress(Key.Tab);
        var reading=UIAction("阅读与操作");var label=UIChildren<TextBlock>(reading).First(t=>t.Text=="Aa");
        label.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
        check(!keyboardMode&&Object.ReferenceEquals(keyboardTarget,reading)&&!((FarmButton)GuidePrimary()).ShowKeyFocus,"routed mouse input finds a templated button and clears old keyboard hints");
        var targets=KeyboardTargets();var next=targets[(targets.IndexOf(reading)+1)%targets.Count];
        check(HandlePanelKey(Key.Tab,ModifierKeys.None,false,label)&&Object.ReferenceEquals(keyboardTarget,next),"Tab continues from the mouse control instead of jumping to the first action");
        UseMouseInput(label);check(HandlePanelKey(Key.Enter,ModifierKeys.None,false,label)&&readingSettingsOpen,"Enter activates a mouse-focused control through the same guarded key route");
        GuidePreview(root,"final-reading-open");check(AutomationProperties.GetAutomationId(keyboardTarget)=="reading-size-0","opening reading settings focuses their first actual control");
        var input=new FarmDetachedInputSource{RootVisual=(Visual)Content};var escape=new KeyEventArgs(Keyboard.PrimaryDevice,input,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};keyboardTarget.RaiseEvent(escape);
        check(escape.Handled&&!readingSettingsOpen&&AutomationProperties.GetAutomationId(keyboardTarget)=="reading-settings","routed Escape closes settings and returns focus to the opener");

        G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;G.S.GuideHomeStage=HomeGuideStage.StayingHome;G.S.Xp[0]=36;
        Go("农场");Go("冒险");GuidePreview(root,"final-cancel-ready");var stale=GuidePrimary();KeyPress(Key.Escape);GuidePreview(root,"final-cancel-home");KeyPress(Key.Escape);GuidePreview(root,"final-cancel-back-again");
        string before=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&Tab!="冒险"&&!G.S.GuideOutingAccepted&&FarmPracticeTests.Xml(G.S)==before,"a second back action does not resurrect cancelled preparation or its stale start");
        Go("冒险");GuidePreview(root,"final-pointer-cancel");GuideClick(UIAction("先待在家里"));GuidePreview(root,"final-pointer-home");KeyPress(Key.Escape);GuidePreview(root,"final-pointer-back");check(!G.S.GuideOutingAccepted&&Tab!="冒险","pointer cancellation has the same clean navigation history");

        Go("冒险");GuidePreview(root,"final-trip-party");GuideClick(GuidePrimary());GuidePreview(root,"final-trip-ready");GuideClick(GuidePrimary());ReachGentleInspiration();G.ToggleJourneyPause();GuidePreview(root,"final-options");FocusByTab(UIAction("我想换一个"));KeyPress(Key.Enter);GuidePreview(root,"final-options-expanded");
        string runBefore=FarmPracticeTests.Xml(G.R);KeyPress(Key.Escape);GuidePreview(root,"final-options-folded");check(Tab=="冒险"&&!inspirationChoicesOpen&&InspirationButtons()==1&&FarmPracticeTests.Xml(G.R)==runBefore,"Escape folds optional choices without leaving the trip, rerolling or unpausing");
        Go("农田");GuidePreview(root,"final-planner");var expander=KeyboardTargets().OfType<Expander>().First();FocusByTab(expander);KeyPress(Key.Space);check(expander.IsExpanded,"keyboard opens the real planting plan");
        KeyPress(Key.Escape);GuidePreview(root,"final-planner-folded");check(Tab=="农田"&&!KeyboardTargets().OfType<Expander>().Any(e=>e.IsExpanded),"Escape closes expanded details before page navigation");var folded=KeyboardTargets().OfType<Expander>().First();check(KeyboardTargets().All(c=>!Under(c,(DependencyObject)folded.Content)),"collapsed planner actions stay outside the Tab sequence");
        G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(1));G.Home();G.S.GuideStep=8;G.S.SimpleCourtyard=false;ResetPanelInput();
        Go("详细规则");GuidePreview(root,"final-checkbox");var motion=LogicalNodes((DependencyObject)Content).OfType<CheckBox>().First(c=>Convert.ToString(c.Content)=="播放桌宠动作");bool wasMotion=G.S.DesktopMotion;FocusByTab(motion);KeyPress(Key.Enter);GuidePreview(root,"final-checkbox-changed");check(G.S.DesktopMotion!=wasMotion,"Enter runs checkbox click behavior, including the actual preference handler");KeyPress(Key.Enter,ModifierKeys.None,true);check(G.S.DesktopMotion!=wasMotion,"held checkbox keys cannot toggle the preference back");
        Go("归家");GuidePreview(root,"final-arrangement");var soil=LogicalNodes((DependencyObject)Content).OfType<CheckBox>().First(c=>Convert.ToString(c.Content)=="升级一次土壤");bool planned=arrangeSoil;before=FarmPracticeTests.Xml(G.S);FocusByTab(soil);KeyPress(Key.Space);GuidePreview(root,"final-arrangement-changed");check(arrangeSoil!=planned&&FarmPracticeTests.Xml(G.S)==before,"checkbox keyboard input changes the pending plan without applying purchases");

        Go("农田");G.S.ReadingSize=0;GuidePreview(root,"final-base-fonts");var baseFonts=KeyboardTargets().ToDictionary(c=>keyboardKeys[c],c=>c.FontSize);
        foreach(int size in new[]{1,2}){G.S.ReadingSize=size;GuidePreview(root,"final-scaled-fonts-"+size);foreach(var control in KeyboardTargets())if(baseFonts.ContainsKey(keyboardKeys[control]))check(Math.Abs(control.FontSize-baseFonts[keyboardKeys[control]]*new[]{1d,1.15,1.3}[size])<.01,"nested controls scale once: "+keyboardKeys[control]);}

        var drawingGame=new FarmGame(null,null,true);drawingGame.S.Team=new[]{4,0,3};drawingGame.S.Xp=Enumerable.Repeat(55,5).ToArray();drawingGame.S.Evolved=true;drawingGame.S.SeedStage=3;
        foreach(double scale in new[]{1d,1.15,1.3}){
            var farm=new FarmScene(drawingGame){Height=176,MinHeight=176};CheckSceneReading(farm,root,"farm",444,176,scale);
            var quiet=new FarmScene(drawingGame){FocusedFarmPet=0,ShowBerryBasket=true,Height=176,MinHeight=176};CheckSceneReading(quiet,root,"quiet",444,176,scale);
        }
        check(drawingGame.Begin(0,0),"battle scene fixture starts with normal rules");drawingGame.RouteChoice(0);drawingGame.R.Shield[0]=125;
        foreach(double scale in new[]{1d,1.15,1.3}){
            var battle=new FarmScene(drawingGame,true){Height=242,MinHeight=242};CheckSceneReading(battle,root,"battle",464,242,scale);
            var travel=new FarmScene(drawingGame){Journey=true,Height=176,MinHeight=176};CheckSceneReading(travel,root,"travel",444,176,scale);
        }
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"final-reset");check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"final UX tests remain detached and isolated");
        File.WriteAllText(Path.Combine(root,"final-ux-regression.txt"),"PASS: routed mouse and Escape events; template-aware focus and pointer-to-keyboard continuation; checkbox click/checked behavior and held-key protection; repeated and pointer preparation cancellation; optional choices and expanded details close before navigation; no changes to pause or rewards; nested typography scales once; actual farm/travel/battle glyph sizes, bounds and overlap at all three reading sizes. Detached event checks, not physical keyboard input.");
    }
}
