using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;

public partial class FarmPanel
{
    private static IEnumerable<GeometryDrawing> HealthGeometry(Drawing drawing)
    {
        var geometry=drawing as GeometryDrawing;if(geometry!=null)yield return geometry;
        var group=drawing as DrawingGroup;if(group!=null)foreach(var child in group.Children)foreach(var item in HealthGeometry(child))yield return item;
    }
    private void HealthPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"ux30-"+name,size,true);
        var primary=GuidePrimary();if(Under(primary,scroller))ReviewTarget(primary,"health primary "+name+" "+size);else GuideFits(primary);
    }
    private TextBlock HealthSummaryLine(){return UIChildren<TextBlock>((DependencyObject)Content).Single(t=>AutomationProperties.GetAutomationId(t)=="small-party-health");}
    internal void RenderJourneyHealthChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        foreach(bool first in new[]{true,false})for(int size=0;size<3;size++){
            LaterJourneyFixture();G.S.DesktopMotion=false;
            if(first){G.S.SimpleCourtyard=false;G.S.GuideStep=4;G.S.GuideInWorld=true;G.S.GuideFarmSeen=true;}
            int[] party=G.S.Team;G.R.Hp[party[0]]=0;G.R.Hp[party[1]]=G.MaxHp(party[1])/2;G.R.Shield[party[1]]=17;
            G.S.ReadingSize=size;string prefix=first?"first":"repeat",before=FarmPracticeTests.Xml(G.S);
            HealthPreview(root,prefix+"-battle",size);var scene=scenes.Single();var details=LaterExpander(first?"guide-combo-details":"small-trip-details");
            int expected=party.Sum(p=>Math.Max(0,G.R.Hp[p]));
            check(HealthSummaryLine().Text=="伙伴体力 "+expected+" / "+party.Sum(p=>G.MaxHp(p))+" · 自动交战中"&&FarmPracticeTests.Xml(G.S)==before,"default summary is actual team health, excludes shield and never mutates");
            check(!GuideWords().Contains("护盾 17"),"individual health and shields remain optional");
            ReviewTarget(UIAction("暂停旅程"),prefix+" pause "+size);ReviewTarget(UIAction("回小院等他们"),prefix+" wait "+size);ReviewTarget(details,prefix+" details "+size);
            var bars=HealthGeometry(VisualTreeHelper.GetDrawing(scene)).Where(d=>d.Pen!=null&&Math.Abs(d.Pen.Thickness-.7)<.001&&d.Geometry is RectangleGeometry).ToArray();
            check(bars.Length==party.Length+G.R.EnemyCount&&bars.All(b=>Math.Abs(((RectangleGeometry)b.Geometry).Rect.Height-8*scene.ReadingScale)<.01&&b.Bounds.Bottom<scene.ActualHeight),"outlined scene health bars scale and fit at "+size);
            CheckSceneReading(scene,root,"small-health-"+prefix,scene.ActualWidth,scene.ActualHeight,new[]{1d,1.15,1.3}[size]);
            details.IsExpanded=true;UIPreview(root,"ux30-"+prefix+"-details-"+size,484,501);
            check(FarmPracticeTests.Xml(G.S)==before&&GuideWords().Contains("歇一会儿")&&GuideWords().Contains("护盾 17"),"details expose individual rest and shield without advancing");
            foreach(int pet in party){var bar=LessonProgress("small-health-bar-"+pet);check(bar.Value==G.R.Hp[pet]&&bar.Maximum==G.MaxHp(pet)&&AutomationProperties.GetName(bar).Contains(G.Name(pet)),"individual bar and accessible name match health");}
            ReviewTarget(UIAction("提前回家…"),prefix+" retreat stays first",true);
            KeyPress(Key.Escape);HealthPreview(root,prefix+"-collapsed",size);GuideClick(UIAction("暂停旅程"));HealthPreview(root,prefix+"-paused",size);
            check(HealthSummaryLine().Text.EndsWith("已暂停")&&!HealthSummaryLine().Text.Contains("自动交战"),"paused health never claims automatic combat");
            string paused=FarmPracticeTests.Xml(G.R);owner.TickClock(DateTime.UtcNow.AddSeconds(5),false,true,false);check(FarmPracticeTests.Xml(G.R)==paused,"five seconds without input respects pause");
        }

        LaterJourneyFixture();G.S.DesktopMotion=false;HealthPreview(root,"frozen-before");var frozenScene=scenes.Single();var sameRoot=Content;int round=G.R.Round;
        double width=frozenScene.ActualWidth,height=frozenScene.ActualHeight;
        string beforeImage=Path.Combine(root,"ui-ux30-frozen-scene-before.png"),afterImage=Path.Combine(root,"ui-ux30-frozen-scene-after.png");
        FarmHeadlessTests.RenderContent(frozenScene,beforeImage,width,height);
        owner.TickClock(new DateTime(G.R.NextJourneyTicks,DateTimeKind.Utc),false,true,false);
        check(G.R.Phase=="battle"&&G.R.Round==round+1&&Object.ReferenceEquals(Content,sameRoot)&&Object.ReferenceEquals(scenes.Single(),frozenScene),"production clock advances the same battle without rebuilding the page");
        FarmHeadlessTests.RenderContent(frozenScene,afterImage,width,height);
        check(!File.ReadAllBytes(beforeImage).SequenceEqual(File.ReadAllBytes(afterImage)),"health drawing refreshes while animation is off");
        check(HealthSummaryLine().Text.Contains(G.S.Team.Sum(p=>Math.Max(0,G.R.Hp[p]))+" / "),"same-page numeric health refreshes with the production clock");
        G.ToggleJourneyPause();HealthPreview(root,"frozen-paused");string saved=FarmPracticeTests.Xml(G.R);owner.TickClock(DateTime.UtcNow.AddSeconds(5),false,true,false);check(saved==FarmPracticeTests.Xml(G.R),"idle paused health remains exact");
        G.ToggleJourneyPause();G.AdvanceJourney(DateTime.UtcNow.AddHours(1));HealthPreview(root,"ordinary-receipt");check(!UIChildren<TextBlock>((DependencyObject)Content).Any(t=>AutomationProperties.GetAutomationId(t)=="small-party-health"),"result removes live battle health rather than keeping stale combat state");
        G.ResetGuideTest();ResetGuideView();HealthPreview(root,"fresh");check(G.SavePath==null&&!IsVisible&&!owner.IsVisible,"health review remains detached with fresh in-memory guide");
        File.WriteAllText(Path.Combine(root,"journey-health-regression.txt"),"PASS: first and repeat battle summaries show actual total HP excluding shields; per-pet HP, maximum, shield and resting state only in optional details; 8-DIP outlined bars scale at 100/115/130%; actual viewport and scene glyph bounds; retreat remains first; production clock updates existing scene with animation disabled; pause and 5-second idle; result removes live battle status; read-only review and memory-only guide. Detached WPF, not physical input or player recognition timing.");
    }
}
