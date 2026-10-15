using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private static IEnumerable<T> UIChildren<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T)yield return (T)root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(T child in UIChildren<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private Button UIAction(string name){var button=UIChildren<Button>((DependencyObject)Content).FirstOrDefault(b=>Convert.ToString(b.Content)==name||AutomationProperties.GetName(b)==name);if(button==null)throw new Exception("UI action missing: "+name);return button;}
    private void UIPreview(string root,string name,double width=924,double height=721){FarmHeadlessTests.Render(this,Path.Combine(root,"ui-"+name+".png"),width,height);}
    private void UIVisible(FrameworkElement target,string label)
    {
        var at=target.TranslatePoint(new Point(0,0),(UIElement)Content);
        FarmHeadlessTests.Check(target.ActualWidth>0&&at.X>=0&&at.X+target.ActualWidth<=764&&at.Y>=0&&at.Y+target.ActualHeight<=532,label);
    }
    internal void RenderUIChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        Tab="农场";Render();UIPreview(root,"home");
        UIAction("管理农田").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Render();UIPreview(root,"field-management");
        var crops=UIChildren<ComboBox>((DependencyObject)Content).ToArray();
        check(crops.Length==G.Plots&&crops.All(c=>c.Focusable&&c.IsTabStop&&UIChildren<ToggleButton>(c).Any()),"themed crop pickers retain focus, selection and toggle controls");
        G.S.Growth[0]=7;crops[0].SelectedIndex=1;check(G.S.Crops[0]==1&&G.S.Growth[0]==0,"themed picker actually replants the selected plot");
        G.S.Berries=1234567;G.S.Grain=98765;G.S.Fertilizer=1234;G.S.Mint=9999;G.S.Crystals=Int32.MaxValue;RefreshCounters();UIPreview(root,"large-resources-small",764,571);
        foreach(var number in resourceNumbers){var fit=(Viewbox)number.Parent;UIVisible(fit,"all resource values remain in the small header");check(number.ToolTip!=null&&number.TextTrimming==TextTrimming.None&&fit.Stretch==Stretch.Uniform,"full resource quantities fit without removing the unit suffix");}
        G.S.Berries=28;G.S.Grain=42;G.S.Fertilizer=7;G.S.Mint=3;G.S.Crystals=2;
        UIAction("伙伴").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab=="伙伴","illustrated navigation routes to pets");Render();UIPreview(root,"pets");
        var details=UIChildren<Expander>((DependencyObject)Content).First(e=>Convert.ToString(e.Header)=="技能、站位与更多培养");details.IsExpanded=true;
        int xp=G.S.Xp[0];UIAction(FarmUX.FeedLabel(G,0)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.Xp[0]>xp,"front card feeding applies growth");Render();UIPreview(root,"pet-details-small",764,571);check(petDetailsOpen[0]&&UIChildren<Expander>((DependencyObject)Content).Any(e=>e.IsExpanded),"expanded pet details survive a care refresh");
        scroller.ScrollToVerticalOffset(180);scroller.UpdateLayout();double before=scroller.VerticalOffset;check(before>0,"custom scrollbar allows long roster to scroll");Render();UIPreview(root,"pet-scroll-small",764,571);check(scroller.VerticalOffset>0,"care refresh preserves roster scroll position");
        UIAction("冒险").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Render();UIPreview(root,"adventure-small",764,571);
        UIVisible(UIAction("出发 · 自动走完整趟"),"departure fully visible at minimum size");
        var blessings=UIChildren<Expander>((DependencyObject)Content).First(e=>AutomationProperties.GetAutomationId(e)=="departure-blessings");blessings.IsExpanded=true;Render();UIPreview(root,"adventure-blessings");
        var offers=(int[])G.S.BlessingOffers.Clone();int selected=selectedBlessing;UIAction("携带祝福").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(selectedBlessing!=selected&&G.S.BlessingOffers.SequenceEqual(offers),"blessing selection is actionable and does not reroll offers");
        G.S.Xp=Enumerable.Repeat(55,5).ToArray();G.SetPoisonPreset();Render();UIPreview(root,"adventure");
        var config=UIChildren<Expander>((DependencyObject)Content).First(e=>Convert.ToString(e.Header)=="换个搭配 · 推荐组合与自由配队");config.IsExpanded=true;Render();UIPreview(root,"adventure-config");check(journeyConfigOpen,"free team configuration expansion persists");
        UIAction("出发 · 自动走完整趟").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!G.AtHome&&G.R.Blessing==selectedBlessing,"accent departure starts chosen automatic journey");
        Tab="伙伴";Render();UIPreview(root,"away-pets-small",764,571);check(!UIAction(FarmUX.FeedLabel(G,0)).IsEnabled,"away pet care is disabled in redesigned cards");
        G.Retreat();G.Home();G.S.SeedBond=2;G.S.DisplayPet=4;Tab="进化";Render();UIPreview(root,"evolution");
        var evolve=UIAction("进化为「"+FarmGame.SeedNames[1]+"」");check(evolve.IsEnabled,"completed requirements enable evolution action");evolve.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.SeedStage==1,"new evolution action changes the form");
        check(!IsVisible&&!owner.IsVisible,"UI tests never show a window");
        File.WriteAllText(Path.Combine(root,"ui-regression.txt"),"PASS: themed crop selection; compact resource counts; navigation; feeding; details and scroll persistence; minimum-window departure; expandable blessing choices and stable offers; automatic departure; disabled care while away; evolution action. All input sent to isolated offscreen controls.");
    }
}
