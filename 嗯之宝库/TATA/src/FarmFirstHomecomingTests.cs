using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Xml.Linq;

public partial class FarmPanel
{
    internal void RenderFirstHomecomingChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        G.ResetGuideTest();ResetGuideView();G.S.GuideInWorld=true;G.S.GuideStep=6;G.S.Wins=1;G.S.Fertilizer=4;G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(2).Ticks;
        Go("农场");GuidePreview(root,"homecoming-soil-ready");
        check(SmallHomecoming&&WantsGentleNarrow&&scenes.Single().LearningPlot>=0&&!GuideWords().Contains("进化"),"first fertilizer action uses the familiar small one-plot scene");
        int plot=scenes.Single().LearningPlot;double cycle=G.CropSeconds(0);var growth=(double[])G.S.Growth.Clone();var crops=(int[])G.S.Crops.Clone();var apply=GuidePrimary();
        GuideClick(apply);GuidePreview(root,"homecoming-soil-applied");var receipt=G.ValidSoilReceipt;
        check(G.S.GuideStep==7&&G.S.SoilLevel==1&&G.S.Fertilizer==0&&receipt!=null&&receipt.Cost==4&&receipt.BeforeLevel==0&&receipt.AfterLevel==1,"real first fertilizer purchase records exact cost and levels");
        check(receipt.BeforeBerrySeconds==cycle&&receipt.AfterBerrySeconds==G.CropSeconds(0)&&G.S.Growth.SequenceEqual(growth)&&G.S.Crops.SequenceEqual(crops),"receipt uses actual full-cycle durations and never restarts crops");
        check(scenes.Single().LearningPlot==plot&&scenes.Single().SoilLessonApplied,"completed lesson keeps the same actual plot");
        check(LessonProgress("soil-time-before").Value==cycle&&LessonProgress("soil-time-after").Value==receipt.AfterBerrySeconds&&LessonProgress("soil-time-after").Maximum==cycle,"visual comparison lengths represent recorded waiting time, not invented growth progress");
        check(!GuideWords().Contains("土壤 Lv.")&&!HasGuideButton("我还想看看进化"),"completion initially teaches only the speed change");
        string before=FarmPracticeTests.Xml(G.S);GuidePreview(root,"homecoming-soil-repaint");apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"repainting and a stale purchase cannot spend or harvest again");
        string path=Path.Combine(root,"first-homecoming-saved.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var loaded=new FarmGame(path);
        check(loaded.S.GuideStep==7&&loaded.ValidSoilReceipt!=null&&loaded.ValidSoilReceipt.BeforeBerrySeconds==cycle&&loaded.ValidSoilReceipt.AfterBerrySeconds==receipt.AfterBerrySeconds,"normal reload preserves the real comparison and completion stage");
        var resumedWindow=new FarmWindow(loaded,true);resumedWindow.Panel=new FarmPanel(resumedWindow);resumedWindow.Panel.Go("农场");resumedWindow.Panel.GuidePreview(root,"homecoming-resumed");
        check(resumedWindow.Panel.LessonProgress("soil-time-after").Value==receipt.AfterBerrySeconds&&!resumedWindow.IsVisible&&!resumedWindow.Panel.IsVisible,"reloaded UI uses the saved snapshot without native windows");
        var xml=XDocument.Load(path);xml.Root.Element("LastSoilUpgrade").Remove();string oldPath=Path.Combine(root,"first-homecoming-old.xml");xml.Save(oldPath);var old=new FarmGame(oldPath);
        var oldWindow=new FarmWindow(old,true);oldWindow.Panel=new FarmPanel(oldWindow);oldWindow.Panel.Go("农场");oldWindow.Panel.GuidePreview(root,"homecoming-old-save");
        check(old.ValidSoilReceipt==null&&old.S.SoilLevel==1&&!oldWindow.Panel.GuideWords().Contains("施肥前")&&Convert.ToString(oldWindow.Panel.GuidePrimary().Content)=="回小院，慢慢玩","old saves show current timing without fabricating historical numbers or blocking completion");
        G.S.Evolved=true;GuidePreview(root,"homecoming-changed-helper");
        check(G.CropSeconds(0)!=receipt.AfterBerrySeconds&&LessonProgress("soil-time-after").Value==receipt.AfterBerrySeconds,"later rate changes cannot rewrite historical upgrade measurements");
        G.S.SoilLevel=2;check(G.ValidSoilReceipt==null,"an unrelated later upgrade does not reuse an earlier receipt as the current one");G.S.SoilLevel=1;
        foreach(double invalid in new[]{Double.NaN,Double.PositiveInfinity,-1,cycle}){receipt.AfterBerrySeconds=invalid;check(G.ValidSoilReceipt==null,"invalid duration cannot be presented as a real saving");}
        receipt.AfterBerrySeconds=loaded.ValidSoilReceipt.AfterBerrySeconds;receipt.Cost=-4;check(G.ValidSoilReceipt==null,"invalid cost does not become a teaching receipt");receipt.Cost=4;
        G.S.Evolved=false;GuidePreview(root,"homecoming-details-ready");
        var detail=UIChildren<Expander>((DependencyObject)Content).Single(e=>AutomationProperties.GetAutomationId(e)=="soil-guide-details");detail.IsExpanded=true;UIPreview(root,"homecoming-details",484,501);
        check(GuideWords().Contains("这次用了肥料 4")&&GuideWords().Contains("土壤 Lv.0 → Lv.1")&&HasGuideButton("我还想看看进化"),"cost, levels and optional evolution remain available in details");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());GuidePreview(root,"homecoming-finished");check(G.S.GuideStep==8&&G.S.SimpleCourtyard&&G.S.Fertilizer==0&&G.S.CareTarget.Pet==-1,"completion returns directly to simple companionship without assigning another goal");
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=6;G.S.GuideInWorld=true;G.S.Fertilizer=0;Go("农场");GuidePreview(root,"homecoming-shortage");
        check(GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="再去小溪收集肥料"&&G.ValidSoilReceipt==null&&!G.UpgradeSoil()&&G.S.LastSoilUpgrade==null,"insufficient fertilizer offers recovery without producing a success receipt");
        GuideClick(UIAction("再去小溪收集肥料"));GuidePreview(root,"homecoming-shortage-preparation");check(G.AtHome,"shortage recovery opens preparation without launching");
        G.ResetGuideTest();ResetGuideView();GuidePreview(root,"homecoming-reset");check(G.S.LastSoilUpgrade==null&&!IsVisible&&!owner.IsVisible&&G.SavePath==null,"tutorial reset discards the receipt and remains detached from player saves");
        File.WriteAllText(Path.Combine(root,"first-homecoming-regression.txt"),"PASS: one real observed plot; actual upgrade cost and full-cycle before/after timing; no crop reset; proportional time comparison; hidden advanced teaching; stale purchase protection; persistent normal receipt; old-save fallback without invented history; later rate and level changes; invalid data; optional exact details; quiet completion; shortage only opens preparation; no-save reset. Detached WPF and isolated saves only.");
    }
}
