using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;

public partial class FarmPanel
{
    private void KeyPress(Key key,ModifierKeys modifiers=ModifierKeys.None,bool repeat=false)
    {
        FarmHeadlessTests.Check(HandlePanelKey(key,modifiers,repeat,keyboardTarget??(object)this),"panel handles keyboard action: "+key);
    }
    private void FocusByTab(Control target)
    {
        int count=KeyboardTargets().Count+1;while(!Object.ReferenceEquals(keyboardTarget,target)&&count-->0)KeyPress(Key.Tab);
        FarmHeadlessTests.Check(Object.ReferenceEquals(keyboardTarget,target),"control is reachable by Tab: "+ControlKey(target));
    }
    internal void RenderKeyboardChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.ResetGuideTest();ResetGuideView();GuidePreview(root,"keyboard-start");
        KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary())&&((FarmButton)keyboardTarget).ShowKeyFocus,"first Tab reaches the real primary action with a visible focus border; actual="+(keyboardTarget==null?"null":ControlKey(keyboardTarget))+"; order="+String.Join(" | ",KeyboardTargets().Select(c=>InputGroup(c)+":"+ControlKey(c)).ToArray()));
        KeyPress(Key.Tab,ModifierKeys.Shift);check(Object.ReferenceEquals(keyboardTarget,KeyboardTargets().Last()),"Shift+Tab wraps to the last reachable control");KeyPress(Key.Tab);check(Object.ReferenceEquals(keyboardTarget,GuidePrimary()),"Tab returns from the footer to the primary action");
        UIPreview(root,"keyboard-focus",484,501);KeyPress(Key.Enter);GuidePreview(root,"keyboard-fed-once");
        check(G.S.Berries==8&&G.S.Xp[0]==12&&Tab=="伙伴","keyboard activation performs the same real feed");
        KeyPress(Key.Enter,ModifierKeys.None,true);check(G.S.Berries==8&&G.S.Xp[0]==12,"holding Enter cannot spend again after rerender");
        KeyPress(Key.Space);GuidePreview(root,"keyboard-farm");KeyPress(Key.Space,ModifierKeys.None,true);check(G.S.Xp[0]==12&&Tab=="农场","one Space opens food source and repeat cannot acknowledge the next lesson");
        check(FarmIntroduction,"keyboard first reaches the single-plot introduction");KeyPress(Key.Enter);GuidePreview(root,"keyboard-watching");check(!FarmIntroduction&&G.S.GuideFarmLesson==2,"keyboard introduces growth on the next step");
        string before=FarmPracticeTests.Xml(G.S);KeyPress(Key.Enter);check(FarmPracticeTests.Xml(G.S)==before,"keyboard petting is free");
        G.S.GuideHomeStage=HomeGuideStage.Invitation;GuidePreview(root,"keyboard-invitation");FocusByTab(UIAction("先待在家里"));KeyPress(Key.Enter);GuidePreview(root,"keyboard-staying");
        check(G.S.GuideHomeStage==HomeGuideStage.StayingHome&&G.AtHome,"optional invitation can be deferred using only the panel key route");
        FocusByTab(UIAction("出门"));KeyPress(Key.Enter);GuidePreview(root,"keyboard-preparation");var staleStart=GuidePrimary();
        KeyPress(Key.Escape);GuidePreview(root,"keyboard-preparation-cancelled");before=FarmPracticeTests.Xml(G.S);staleStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.AtHome&&!G.S.GuideOutingAccepted&&FarmPracticeTests.Xml(G.S)==before,"Escape cancels preparation without departure or spending; old start stays invalid");
        FocusByTab(UIAction("出门"));KeyPress(Key.Enter);GuidePreview(root,"keyboard-party");FocusByTab(GuidePrimary());KeyPress(Key.Enter);GuidePreview(root,"keyboard-ready");
        check(G.AtHome&&G.S.GuideDepartureLesson==1,"keyboard acknowledges companions without departing");FocusByTab(GuidePrimary());KeyPress(Key.Enter);GuidePreview(root,"keyboard-travelling");
        check(G.JourneyRunning,"explicit focused start launches a trip");FocusByTab(UIAction("暂停旅程"));KeyPress(Key.Space);GuidePreview(root,"keyboard-paused");
        string paused=FarmPracticeTests.Xml(G.R);KeyPress(Key.Space,ModifierKeys.None,true);check(G.R.JourneyPaused&&FarmPracticeTests.Xml(G.R)==paused,"holding the pause key cannot immediately resume");
        FocusByTab(UIAction("农场"));KeyPress(Key.Enter);GuidePreview(root,"keyboard-away-farm");KeyPress(Key.Escape);GuidePreview(root,"keyboard-back-to-trip");check(Tab=="冒险"&&FarmPracticeTests.Xml(G.R)==paused,"return navigation preserves explicit pause and current progress");
        KeyPress(Key.Add,ModifierKeys.Control);KeyPress(Key.Add,ModifierKeys.Control);GuidePreview(root,"keyboard-large-paused");check(G.S.ReadingSize==2&&FarmPracticeTests.Xml(G.R)==paused,"reading shortcuts never unpause or move the journey");
        FocusByTab(UIAction("阅读与操作"));KeyPress(Key.Enter);GuidePreview(root,"keyboard-reading-open");check(readingSettingsOpen,"reading controls are reachable with Tab");
        KeyPress(Key.Escape);GuidePreview(root,"keyboard-reading-closed");check(!readingSettingsOpen&&Tab=="冒险"&&FarmPracticeTests.Xml(G.R)==paused,"Escape closes reading settings before navigating");
        KeyPress(Key.D0,ModifierKeys.Control);check(G.S.ReadingSize==0,"Ctrl+0 restores default reading size");
        Go("农田");GuidePreview(root,"keyboard-field-editor");var editor=LogicalNodes((DependencyObject)Content).OfType<ComboBox>().First();before=FarmPracticeTests.Xml(G.S);string tab=Tab;
        check(!HandlePanelKey(Key.Enter,ModifierKeys.None,false,editor)&&!HandlePanelKey(Key.Escape,ModifierKeys.None,false,editor)&&Tab==tab&&FarmPracticeTests.Xml(G.S)==before,"native editors retain Enter/Escape without activating game actions or navigating away");
        Go("冒险");GuidePreview(root,"keyboard-returned");FocusByTab(UIAction("继续这趟旅程"));KeyPress(Key.Enter);check(!G.R.JourneyPaused,"only explicit resume restarts the trip");
        G.AdvanceJourney(DateTime.UtcNow.AddHours(1));G.SyncGuide();Go("冒险");GuidePreview(root,"keyboard-result");FocusByTab(GuidePrimary());KeyPress(Key.Enter);GuidePreview(root,"keyboard-soil");FocusByTab(GuidePrimary());KeyPress(Key.Space);GuidePreview(root,"keyboard-complete");
        check(G.S.SoilLevel==1&&G.S.GuideStep==7,"keyboard can return and apply real earned fertilizer");FocusByTab(GuidePrimary());KeyPress(Key.Enter);GuidePreview(root,"keyboard-quiet-home");check(G.S.SimpleCourtyard,"keyboard completes into the simple courtyard");
        ExpandAllPlay("农场");Go("详细规则");GuidePreview(root,"keyboard-long-page");scroller.ScrollToVerticalOffset(300);scroller.UpdateLayout();double oldOffset=scroller.VerticalOffset;Go("伙伴");GuidePreview(root,"keyboard-detour");
        KeyPress(Key.Left,ModifierKeys.Alt);GuidePreview(root,"keyboard-scroll-restored");check(Tab=="详细规则"&&oldOffset>0&&Math.Abs(scroller.VerticalOffset-oldOffset)<2,"back navigation restores the previous page and scroll position: "+Tab+" / "+oldOffset+" -> "+scroller.VerticalOffset);
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"keyboard-reset");check(!keyboardMode&&!readingSettingsOpen&&pageHistory.Count==0&&G.S.ReadingSize==0,"restart clears focus, reading and navigation session state");
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"keyboard routing checks use no native windows or player saves");
        File.WriteAllText(Path.Combine(root,"keyboard-regression.txt"),"PASS: application key routing and actual control events; body-first Tab and reverse navigation; visible focus; real feeding; Enter/Space repeat protection; optional invitation and cancellable preparation; pause survives settings and navigation; editor key ownership; return/soil/simple completion; scroll restoration; fresh reset. Detached testing, not physical keyboard or IME certification.");
    }
}
