using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;

public partial class FarmPanel
{
    private bool keyboardMode,backNavigating,suppressFocusScroll;
    private string keyboardFocusKey="";
    private Control keyboardTarget;
    private readonly Dictionary<Control,string> keyboardKeys=new Dictionary<Control,string>();
    private readonly List<string> pageHistory=new List<string>();
    private readonly Dictionary<string,double> pageOffsets=new Dictionary<string,double>();
    private double? navigationOffset,pendingScrollOffset;
    private FarmSave navigationState;
    private readonly HashSet<Key> activatedKeys=new HashSet<Key>();
    private void InitializePanelInput()
    {
        RequestBringIntoView+=(s,e)=>{if(suppressFocusScroll)e.Handled=true;};
        PreviewKeyDown+=(s,e)=>{Key key=e.Key==Key.System?e.SystemKey:e.Key;if(HandlePanelKey(key,Keyboard.Modifiers,e.IsRepeat,e.OriginalSource)){e.Handled=true;if(key==Key.Enter||key==Key.Space)activatedKeys.Add(key);}};
        PreviewKeyUp+=(s,e)=>{if(activatedKeys.Remove(e.Key))e.Handled=true;};
        PreviewMouseDown+=(s,e)=>UseMouseInput(e.OriginalSource as DependencyObject);
    }
    private static DependencyObject InputParent(DependencyObject node)
    {
        var parent=LogicalTreeHelper.GetParent(node);if(parent!=null)return parent;
        return node is Visual?VisualTreeHelper.GetParent(node):null;
    }
    private static Control InputControl(DependencyObject node,List<Control> controls)
    {
        while(node!=null){var control=node as Control;if(control!=null&&controls.Contains(control))return control;node=InputParent(node);}return null;
    }
    private void UseMouseInput(DependencyObject source)
    {
        var controls=KeyboardTargets();keyboardMode=false;keyboardTarget=InputControl(source,controls);keyboardFocusKey=keyboardTarget==null?"":keyboardKeys[keyboardTarget];
        foreach(var b in keyboardKeys.Keys.OfType<FarmButton>())b.ShowKeyFocus=false;
    }
    private static bool Under(DependencyObject node,DependencyObject ancestor)
    {
        while(node!=null){if(Object.ReferenceEquals(node,ancestor))return true;node=InputParent(node);}return false;
    }
    private static bool CanKeyboardReach(Control control)
    {
        if(!control.IsEnabled||control.Visibility!=Visibility.Visible)return false;
        DependencyObject node=control;
        while(node!=null&&!(node is Window)){var ui=node as UIElement;if(ui!=null&&ui.Visibility!=Visibility.Visible)return false;var parent=LogicalTreeHelper.GetParent(node);var exp=parent as Expander;if(exp!=null&&!exp.IsExpanded&&Object.ReferenceEquals(exp.Content,node))return false;node=parent;}
        return true;
    }
    private static int InputGroup(DependencyObject node)
    {
        while(node!=null){var value=node.ReadLocalValue(FocusGroupProperty);if(value!=DependencyProperty.UnsetValue)return (int)value;node=LogicalTreeHelper.GetParent(node);}return 3;
    }
    private static string ControlKey(Control control)
    {
        string id=AutomationProperties.GetAutomationId(control);if(id!="")return "id:"+id;
        string name=AutomationProperties.GetName(control);if(name!="")return "name:"+name;
        var button=control as Button;if(button!=null&&button.Content is string)return "button:"+(string)button.Content;
        var exp=control as Expander;if(exp!=null)return "expander:"+Convert.ToString(exp.Header);
        return control.GetType().Name;
    }
    private List<Control> KeyboardTargets()
    {
        keyboardKeys.Clear();var counts=new Dictionary<string,int>();
        var controls=LogicalNodes((DependencyObject)Content).OfType<Control>().Where(c=>c is Button||c is ComboBox||c is Slider||c is CheckBox||c is Expander||c is TextBox).ToList();
        foreach(var control in controls){string key=ControlKey(control);int count=counts.ContainsKey(key)?counts[key]:0;counts[key]=count+1;keyboardKeys[control]=key+(count==0?"":"#"+count);}
        return controls.Where(CanKeyboardReach).OrderBy(InputGroup).ThenBy(c=>InputGroup(c)==0&&IsGuideAction(c)?-1:0).ToList();
    }
    private void FocusPanelControl(Control control,bool reveal=true)
    {
        keyboardMode=true;keyboardTarget=control;keyboardFocusKey=keyboardKeys[control];
        foreach(var b in keyboardKeys.Keys.OfType<FarmButton>())b.ShowKeyFocus=Object.ReferenceEquals(b,control);
        control.Focusable=true;if(reveal)control.BringIntoView();if(IsVisible){ApplyPanelFocus(control,reveal);Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>{if(keyboardMode&&Object.ReferenceEquals(keyboardTarget,control)&&keyboardKeys.ContainsKey(control))ApplyPanelFocus(control,reveal);}));}
    }
    private void ApplyPanelFocus(Control control,bool reveal)
    {
        suppressFocusScroll=!reveal;try{control.Focus();if(reveal)control.BringIntoView();}finally{suppressFocusScroll=false;}
    }
    private void FinishPanelInput()
    {
        FocusGroup(body,0);var controls=KeyboardTargets();for(int i=0;i<controls.Count;i++)KeyboardNavigation.SetTabIndex(controls[i],i);
        if(!keyboardMode)return;
        var target=controls.FirstOrDefault(c=>keyboardKeys[c]==keyboardFocusKey)??controls.FirstOrDefault();if(target!=null)FocusPanelControl(target,false);
    }
    private bool HandlePanelKey(Key key,ModifierKeys modifiers,bool repeat,object source)
    {
        var controls=KeyboardTargets();var sourceNode=source as DependencyObject;var sourceControl=InputControl(sourceNode,controls);
        var editor=controls.FirstOrDefault(c=>(c is ComboBox||c is TextBox||c is Slider)&&(c.IsKeyboardFocusWithin||sourceNode!=null&&(Object.ReferenceEquals(c,sourceNode)||Under(sourceNode,c))));
        if(controls.OfType<ComboBox>().Any(c=>c.IsDropDownOpen))return false;
        if(editor!=null&&key==Key.Escape)return false;
        if((modifiers&ModifierKeys.Control)!=0){
            int size=G.S.ReadingSize;if(key==Key.Add||key==Key.OemPlus)size++;else if(key==Key.Subtract||key==Key.OemMinus)size--;else if(key==Key.D0||key==Key.NumPad0)size=0;else return false;
            if(!repeat&&G.SetReadingSize(Math.Max(0,Math.Min(2,size))))Render();return true;
        }
        if(key==Key.Tab&&(modifiers==ModifierKeys.None||modifiers==ModifierKeys.Shift)){
            if(controls.Count==0)return true;int index=controls.IndexOf(sourceControl??keyboardTarget);int direction=modifiers==ModifierKeys.Shift?-1:1;
            FocusPanelControl(controls[index<0?(direction>0?0:controls.Count-1):(index+direction+controls.Count)%controls.Count]);return true;
        }
        if(key==Key.Escape||key==Key.Left&&modifiers==ModifierKeys.Alt){if(!repeat)NavigatePanelBack();return true;}
        if(editor==null&&modifiers==ModifierKeys.None&&ScrollCardPile(key))return true;
        if(editor!=null||modifiers!=ModifierKeys.None)return false;
        if(key==Key.Enter||key==Key.Space){
            if(sourceControl!=null&&(!keyboardMode||!Object.ReferenceEquals(sourceControl,keyboardTarget)))FocusPanelControl(sourceControl,false);
            if(!keyboardMode||keyboardTarget==null||!controls.Contains(keyboardTarget))return false;
            if(repeat)return true;var button=keyboardTarget as Button;if(button!=null){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!IsVisible)Render();return true;}
            var expander=keyboardTarget as Expander;if(expander!=null){expander.IsExpanded=!expander.IsExpanded;return true;}
            var toggle=keyboardTarget as FarmCheckBox;if(toggle!=null){toggle.Activate();if(!IsVisible)Render();return true;}
        }
        return false;
    }
    private void RememberPage(string next)
    {
        if(!Object.ReferenceEquals(navigationState,G.S)){pageHistory.Clear();pageOffsets.Clear();navigationOffset=null;navigationState=G.S;}
        if(Tab==next)return;
        if(scroller!=null&&lastTab==Tab)pageOffsets[Tab]=pendingScrollOffset??scroller.VerticalOffset;
        if(!backNavigating){pageHistory.Add(Tab);if(pageHistory.Count>24)pageHistory.RemoveAt(0);}
        navigationOffset=pageOffsets.ContainsKey(next)?pageOffsets[next]:0;
    }
    private bool NavigatePanelBack()
    {
        if(Tab=="卡牌冒险")return CardBack();
        if(readingSettingsOpen){readingSettingsOpen=false;keyboardFocusKey="id:reading-settings";Render();return true;}
        if(moreNavigationOpen){moreNavigationOpen=false;Render();return true;}
        if(SmallRetreatVisible){CloseSmallRetreat();resetScroll=true;Render();return true;}
        if(confirmRetreat){confirmRetreat=false;Render();return true;}
        if(Tab=="冒险"&&G.R.Phase=="inspiration"&&inspirationChoicesOpen){inspirationChoicesOpen=false;keyboardFocusKey="button:我想换一个";Render();return true;}
        var expanded=KeyboardTargets().OfType<Expander>().Where(e=>e.IsExpanded).ToList();
        var close=expanded.LastOrDefault(e=>keyboardTarget!=null&&Under(keyboardTarget,e))??expanded.LastOrDefault();
        if(close!=null){keyboardFocusKey=keyboardKeys[close];close.IsExpanded=false;Render();return true;}
        if(Tab=="找莓果"){BackFromGuideBerries();Render();return true;}
        if(Tab=="农田"&&SimpleCourtyard&&lifeRequestedCrop>=0){ReturnLifePlanting();Render();return true;}
        if(Tab=="准备小卡"&&SimpleCourtyard){Go("冒险");resetScroll=true;Render();return true;}
        if(Tab=="便当伙伴"&&SimpleCourtyard){ReturnPicnicCompanion();Render();return true;}
        if(Tab=="冒险"&&G.AtHome){CancelOutingPreparation();Render();return true;}
        string previous=null;while(pageHistory.Count>0&&previous==null){string item=pageHistory[pageHistory.Count-1];pageHistory.RemoveAt(pageHistory.Count-1);if(item!=Tab&&item!="新手引导")previous=item;}
        if(previous==null){if(Tab=="农田"||Tab=="小院"||Tab=="归家")previous="农场";else if(Tab=="培养"||Tab=="配队"||Tab=="进化"||Tab=="伙伴图鉴"||Tab=="试练")previous="伙伴";else if(Tab=="复盘"||Tab=="便当")previous="冒险";else if(Tab=="怎么玩"||Tab=="详细规则"||Tab=="手册")previous="农场";}
        if(previous==null)return false;
        backNavigating=true;try{Go(previous);}finally{backNavigating=false;}resetScroll=false;keyboardFocusKey="";Render();return true;
    }
    private void CancelOutingPreparation()
    {
        departureSession++;
        if(CancelCrystalPreparation())return;
        if(G.GuideActive&&G.S.GuideStep<=3)G.DeferGuideOuting();
        pageHistory.RemoveAll(t=>t=="冒险");backNavigating=true;try{Go("农场");}finally{backNavigating=false;}resetScroll=true;
    }
    private Button PanelBackButton()
    {
        var b=Btn("返回",()=>NavigatePanelBack());b.Padding=new Thickness(8,5,8,5);b.Margin=new Thickness(0,0,6,0);b.ToolTip="返回上一页 · Esc / Alt+左方向键";AutomationProperties.SetAutomationId(b,"panel-back");return b;
    }
    private void RestorePanelScroll(double offset)
    {
        pendingScrollOffset=null;if(offset<=0)return;
        var view=scroller;pendingScrollOffset=offset;
        view.ScrollChanged+=(s,e)=>{if(Object.ReferenceEquals(scroller,view)&&pendingScrollOffset.HasValue&&view.ViewportHeight>0&&Math.Abs(view.VerticalOffset-Math.Min(pendingScrollOffset.Value,view.ScrollableHeight))<1)pendingScrollOffset=null;};
        view.ScrollToVerticalOffset(offset);
    }
    private void ResetPanelInput()
    {
        readingSettingsOpen=false;readingSettingsCard=null;keyboardMode=false;keyboardTarget=null;keyboardFocusKey="";keyboardKeys.Clear();activatedKeys.Clear();pageHistory.Clear();pageOffsets.Clear();navigationOffset=null;pendingScrollOffset=null;navigationState=G.S;
    }
}
