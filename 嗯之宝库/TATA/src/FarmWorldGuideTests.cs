using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

public partial class FarmPanel
{
    private void GuidePreview(string root,string name,double width=0,double height=0)
    {
        Render();UIPreview(root,"guideworld-"+name,width>0?width:WantsGentleNarrow?544:764,height>0?height:WantsGentleNarrow?541:571);
    }
    private Button GuidePrimary()
    {
        var items=UIChildren<Button>((DependencyObject)Content).Where(IsGuideAction).ToArray();
        FarmHeadlessTests.Check(items.Length==1,"one guide action on "+Tab+", got "+items.Length);return items[0];
    }
    private void GuideClick(Button b){FarmHeadlessTests.Check(b.IsEnabled,"guide action enabled: "+b.Content);b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    private string GuideWords(){return String.Join("\n",UIChildren<TextBlock>((DependencyObject)Content).Select(t=>t.Text));}
    private bool HasGuideButton(string label){return UIChildren<Button>((DependencyObject)Content).Any(b=>Convert.ToString(b.Content)==label||AutomationProperties.GetName(b)==label);}
    private void GuideFits(FrameworkElement target,bool footer=false)
    {
        var panel=(FrameworkElement)Content;var at=target.TranslatePoint(new Point(0,0),panel);
        FarmHeadlessTests.Check(target.ActualWidth>0&&target.ActualHeight>0&&at.X>=0&&at.X+target.ActualWidth<=panel.ActualWidth&&at.Y>=0&&at.Y+target.ActualHeight<=panel.ActualHeight-(footer?0:36),"guide control fits current viewport: "+(target is Button?((Button)target).Content:target.GetType().Name));
    }
    internal void RenderGuideChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        Go("新手引导");GuidePreview(root,"welcome");
        check(WantsGentleNarrow&&Width==560&&Height==580,"first encounter opens a small window");
        check(resourceNumbers.All(n=>n==null)&&!HasGuideButton("伙伴")&&!HasGuideButton("农场")&&!HasGuideButton("冒险")&&!HasGuideButton("更多"),"first encounter reveals no dashboard resources or navigation");
        check(!GuideWords().Contains("肥料")&&!GuideWords().Contains("晶石")&&!GuideWords().Contains("进化")&&!GuideWords().Contains("远足"),"first encounter teaches one concrete interaction only");
        check(Convert.ToString(GuidePrimary().Content)=="喂泡泡 · 莓果 2","first action actually feeds rather than confirming a lecture");
        GuideFits(GuidePrimary());GuideFits(UIAction("从头测试"),true);GuideFits(UIAction("测试：快进 30 秒"),true);
        GuidePreview(root,"welcome-minimum",484,501);GuideFits(GuidePrimary());GuideFits(UIAction("自由玩"),true);
        var firstFeed=GuidePrimary();GuideClick(firstFeed);GuidePreview(root,"fed-once");
        check(Tab=="伙伴"&&G.S.GuideStep==3&&G.S.Berries==8&&G.S.Xp[0]==12&&G.S.Bond==1,"first click uses actual berries, XP and bond");
        check(focusedCareReceipt.Contains("成长 +12")&&focusedCareReceipt.Contains("莓果 2"),"first click leaves real feedback");
        check(resourceNumbers[0]!=null&&resourceNumbers.Skip(1).All(n=>n==null)&&HasGuideButton("伙伴")&&HasGuideButton("农场")&&!HasGuideButton("冒险"),"one feeding reveals berries and the next food-source page without adventure");
        check(!HasGuideButton("培养计划与材料")&&!HasGuideButton("查看伙伴 暗影种子")&&!HasGuideButton("升至 Lv.2 · 麦穗 3"),"first feeding does not expose roster, training or advanced systems");
        GuideFits(GuidePrimary());GuidePreview(root,"feed-minimum",484,501);GuideFits(GuidePrimary());
        check(G.Level(0)==1&&G.S.Xp[0]==12&&Convert.ToString(GuidePrimary().Content)=="去农场看看","one feed leads to the food source without a level gate or fake level-up");
        firstFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.Berries==8&&G.S.Xp[0]==12,"old first feed cannot spend again");
        GuideClick(GuidePrimary());GuidePreview(root,"farm");
        check(Tab=="农场"&&G.S.GuideFarmSeen&&!HasGuideButton("冒险")&&!HasGuideButton("出门")&&G.AtHome,"visiting the farm does not reveal or require adventure");
        check(scenes.Single().FocusedFarmPet==0&&!HasGuideButton("布置小院")&&!HasGuideButton("培养详情"),"first farm shows actual plots and one pet rather than multiple management cards");
        GuideFits(GuidePrimary());GuidePreview(root,"farm-minimum",484,501);GuideFits(GuidePrimary());
        check(FarmIntroduction&&scenes.Single().LearningPlot>=0&&!scenes.Single().ShowBerryBasket&&Convert.ToString(GuidePrimary().Content)=="看看它怎么长","first farm visit introduces one actual plot before harvest instructions");
        GuideClick(GuidePrimary());GuidePreview(root,"farm-watching");
        check(GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="摸摸泡泡"&&!GuideWords().Contains("肥料"),"waiting has an optional companion interaction without teaching later rewards");
        var petBefore=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());GuidePreview(root,"pet-response");
        check(FarmPracticeTests.Xml(G.S)==petBefore&&GuideWords().Contains("蹭了蹭你的手")&&scenes.Single().PetResponseUntil>DateTime.UtcNow,"petting responds without resources, XP, progression or clock changes");
        GuideFits(GuidePrimary());GuidePreview(root,"pet-response-minimum",484,501);GuideFits(GuidePrimary());
        int farmBerries=G.S.Berries;GuideClick(UIAction("测试：快进 30 秒"));GuidePreview(root,"harvest");
        check(G.S.GuideHomeStage==HomeGuideStage.FreshBerries&&G.S.Berries>farmBerries&&G.S.GuideHarvestBerries>0&&!HasGuideButton("出门"),"normal production unlocks feeding fresh berries, without granting tutorial resources");
        GuideFits(GuidePrimary());GuidePreview(root,"fresh-harvest");
        int freshBerries=G.S.Berries,freshXp=G.S.Xp[0];var freshFeed=GuidePrimary();GuideClick(freshFeed);GuidePreview(root,"basics-complete");
        check(G.S.Berries==freshBerries&&G.S.Xp[0]==freshXp&&G.S.GuideHomeStage==HomeGuideStage.Complete&&G.AtHome,"acknowledging the actual harvest closes basic care with no second feeding");
        check(!HasGuideButton("出门")&&!HasGuideButton("冒险")&&!GuideWords().Contains("肥料"),"basic care finishes before any outing lesson");
        freshFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.Berries==freshBerries,"stale harvest acknowledgement cannot consume or grant resources");
        GuideFits(GuidePrimary());GuideClick(GuidePrimary());GuidePreview(root,"companionship");
        check(G.S.GuideHomeStage==HomeGuideStage.Resting&&G.AtHome&&!HasGuideButton("出门")&&!HasGuideButton("去看看"),"finishing the basics leaves a quiet companionship interval");
        var attention=FarmAttention.Current(G);check(attention.Kind=="farm"&&attention.Tab=="新手引导"&&!attention.Detail.Contains("冒险"),"compact pet offers quiet companionship instead of premature adventure or rewards");
        GuideClick(UIAction("测试：快进 30 秒"));GuidePreview(root,"invitation");
        check(G.AtHome&&G.S.GuideHomeStage==HomeGuideStage.Invitation&&HasGuideButton("先待在家里")&&HasGuideButton("出门")&&!GuideWords().Contains("肥料"),"invitation appears after the pause without starting a journey or teaching rewards");
        GuideFits(GuidePrimary());GuideFits(UIAction("先待在家里"));GuidePreview(root,"invitation-minimum",484,501);GuideFits(GuidePrimary());GuideFits(UIAction("先待在家里"));
        var staleInvite=GuidePrimary();var stay=UIAction("先待在家里");GuideClick(stay);GuidePreview(root,"staying-home");
        check(G.S.GuideHomeStage==HomeGuideStage.StayingHome&&G.AtHome&&!G.S.GuideOutingAccepted&&!HasGuideButton("先待在家里")&&HasGuideButton("去小溪看看"),"deferring removes the invitation and keeps an accessible outing entry");
        staleInvite.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&Tab=="农场"&&!G.S.GuideOutingAccepted,"old invitation cannot undo the choice to stay home");
        GuideClick(UIAction("测试：快进 30 秒"));GuideClick(UIAction("伙伴"));GuidePreview(root,"home-feeding");
        int homeXp=G.S.Xp[0];GuideClick(GuidePrimary());GuidePreview(root,"home-fed");
        check(G.S.Xp[0]==homeXp+12&&G.S.GuideHomeStage==HomeGuideStage.StayingHome&&G.AtHome,"feeding remains available after declining the outing");
        GuideClick(UIAction("农场"));GuidePreview(root,"still-home");
        check(!HasGuideButton("先待在家里")&&!HasGuideButton("去看看")&&HasGuideButton("出门"),"time and navigation never repeat a deferred invitation");
        GuideFits(GuidePrimary());GuideFits(UIAction("去小溪看看"));GuidePreview(root,"staying-home-minimum",484,501);GuideFits(UIAction("去小溪看看"));
        GuideClick(UIAction("出门"));GuidePreview(root,"departure-cancellable");
        var cancelledStart=GuidePrimary();int cancelBerries=G.S.Berries;GuideClick(UIAction("先待在家里"));GuidePreview(root,"departure-cancelled");
        cancelledStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&G.S.Berries==cancelBerries&&!G.S.GuideOutingAccepted,"cancelling preparation has no cost and stale departure cannot launch");
        GuideClick(UIAction("去小溪看看"));GuidePreview(root,"departure");
        check(WantsGentleNarrow&&Width==560&&Tab=="冒险"&&G.AtHome&&Convert.ToString(GuidePrimary().Content)=="看看要去的地方","first outing stays small and introduces the actual companions first");
        GuideFits(GuidePrimary());GuideClick(GuidePrimary());GuidePreview(root,"departure-destination");check(G.AtHome&&G.S.GuideDepartureLesson==1,"meeting companions prepares a destination without launching the trip");
        check(!HasGuideButton("出门便当 · 可选")&&!HasGuideButton("搭配试练 · 免费试打")&&!GuideWords().Contains("晶石")&&!GuideWords().Contains("肥料"),"first departure keeps advanced options and later rewards out of the default view");
        GuideFits(GuidePrimary());GuideClick(GuidePrimary());GuidePreview(root,"travel");
        check(G.S.GuideStep==4&&G.R.Casual&&G.R.Difficulty==0&&G.R.Depth==0&&G.R.Picnic==-1&&G.JourneyRunning,"explicit first departure follows normal free journey rules");
        check(scenes.Single().Journey&&!scenes.Single().Battle,"route screen uses the journey scene rather than the farm");
        check(Convert.ToString(GuidePrimary().Content)=="缩成宠物","running journey offers compact mode as optional next action");GuideFits(GuidePrimary());
        // Compact and hide are not clicked: these controls intentionally show or hide real windows.
        check(GuideCombinationEvidence().Contains("发生配合时"),"no combination is claimed before it occurs");
        GuideClick(UIAction("暂停旅程"));GuidePreview(root,"paused");
        check(G.R.JourneyPaused&&Convert.ToString(GuidePrimary().Content)=="继续这趟旅程","explicit pause highlights explicit resume");
        int node=G.R.Node,round=G.R.Round,harvest=G.S.Harvests;var hp=(int[])G.R.Hp.Clone();
        GuideClick(UIAction("测试：快进 30 秒"));GuideClick(UIAction("农场"));GuidePreview(root,"paused-farm");GuideClick(GuidePrimary());GuidePreview(root,"paused-return");
        check(G.R.JourneyPaused&&G.R.Node==node&&G.R.Round==round&&G.R.Hp.SequenceEqual(hp)&&G.S.Harvests>harvest,"navigation and fast forward preserve pause while crops grow");
        GuideClick(GuidePrimary());GuidePreview(root,"resumed");
        bool evidence=false,battle=false;int ticks=0;
        while(G.R.Phase!="result"&&ticks++<20){
            G.AdvanceGuideTest();G.SyncGuide();
            if(!battle&&G.R.Phase=="battle"){GuidePreview(root,"battle");battle=true;}
            if(!evidence&&G.R.WaterTriggers>0&&G.R.ChainTriggers>0&&G.R.Phase!="result"){
                GuidePreview(root,"combination");check(!GuideWords().Contains("潮湿追击"),"combination details stay optional while travelling");
                UIChildren<Expander>((DependencyObject)Content).Single(e=>AutomationProperties.GetAutomationId(e)=="guide-combo-details").IsExpanded=true;GuidePreview(root,"combination-expanded");
                check(GuideWords().Contains("潮湿追击 "+G.R.WaterTriggers+" 次")&&GuideWords().Contains("闪电跳跃 "+G.R.ChainTriggers+" 次"),"optional combination hint is backed by actual run counters");evidence=true;
            }
        }
        GuidePreview(root,"result");
        check(evidence&&G.R.Outcome=="victory"&&G.S.GuideStep==5&&G.R.DecorationReward==0,"first journey earns genuine fertilizer and windmill");
        check(resourceNumbers[3]!=null&&resourceNumbers[4]==null&&!HasGuideButton("准备再去一趟")&&!HasGuideButton("归家安排 · 一起核对花费"),"return introduces fertilizer with one next task");
        GuideFits(GuidePrimary());int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;var receipt=GuidePrimary();GuideClick(receipt);GuidePreview(root,"soil");
        check(G.S.GuideStep==6&&G.AtHome&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals,"return neither spends nor duplicates rewards");
        double before=G.CropSeconds(0);var soil=GuidePrimary();GuideFits(soil);GuideClick(soil);GuidePreview(root,"complete");
        check(G.S.GuideStep==7&&G.S.SoilLevel==1&&G.S.Fertilizer==fertilizer-4&&G.CropSeconds(0)<before&&GuideWords().Contains("本次施肥"),"soil closes loop with real cost and faster growing");
        check(!GuideWords().Contains("晶石")&&G.S.CareTarget.Pet==-1,"completion does not prescribe another learning task");
        soil.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));receipt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.SoilLevel==1&&G.S.Fertilizer==fertilizer-4,"stale return and soil controls cannot duplicate operations");
        GuideClick(GuidePrimary());GuidePreview(root,"free-play");
        check(G.S.GuideStep==8&&!G.GuideActive&&G.S.SimpleCourtyard&&WantsGentleNarrow&&!HasGuideButton("更多")&&resourceNumbers[0]!=null&&resourceNumbers.Skip(1).All(n=>n==null)&&G.S.CareTarget.Pet==-1,"finishing keeps familiar simple courtyard with one resource");
        GuideFits(GuidePrimary());GuideFits(UIAction("展开全部玩法"),true);GuidePreview(root,"quiet-home-minimum",484,501);GuideFits(GuidePrimary());GuideFits(UIAction("展开全部玩法"),true);
        check(FarmAttention.Current(G).Tab=="农场"&&FarmAttention.Current(G).Kind=="farm","finished simple courtyard has quiet desktop attention");
        var quietBefore=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("摸摸泡泡"));GuidePreview(root,"quiet-pet-response");check(FarmPracticeTests.Xml(G.S)==quietBefore,"free-play petting never changes save state");
        GuideClick(UIAction("伙伴"));GuidePreview(root,"quiet-companion");int quietXp=G.S.Xp[0];GuideClick(GuidePrimary());GuidePreview(root,"quiet-fed");check(G.S.Xp[0]==quietXp+12&&G.S.SimpleCourtyard,"ordinary feeding works after completing the guide");
        GuideClick(UIAction("展开全部玩法"));GuidePreview(root,"expanded-play");check(!G.S.SimpleCourtyard&&HasGuideButton("更多")&&resourceNumbers.All(n=>n!=null),"explicit expansion makes all existing gameplay available");
        GuideClick(UIAction("回简洁小院"));GuidePreview(root,"quiet-return");check(G.S.SimpleCourtyard&&!HasGuideButton("更多")&&G.S.Xp[0]==quietXp+12,"returning to simple home preserves growth");

        petDetailsOpen[0]=journeyBlessingsOpen=journeyConfigOpen=travelReportOpen=true;
        GuideClick(UIAction("从头测试"));GuidePreview(root,"reset");
        check(G.S.GuideStep==0&&!G.S.GuideFarmSeen&&G.S.GuideHomeStage==HomeGuideStage.Harvest&&G.S.GuideHarvestBerries==0&&G.S.GuideRestUntilTicks==0&&!G.S.GuideOutingAccepted&&G.S.Berries==10&&G.S.Xp.All(x=>x==0)&&G.S.SoilLevel==0&&WantsGentleNarrow,"test restart clears progress, home lesson, invitation and revealed pages and restores small window");
        check(petDetailsOpen.All(x=>!x)&&!journeyBlessingsOpen&&!journeyConfigOpen&&!travelReportOpen,"restart folds advanced options");
        firstFeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));soil.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.GuideStep==0&&G.S.Berries==10&&G.S.Xp[0]==0,"old session controls cannot mutate reset session");
        GuideClick(UIAction("自由玩"));GuidePreview(root,"skipped");
        check(G.S.GuideStep==9&&HasGuideButton("更多")&&resourceNumbers.All(n=>n!=null),"skip reveals full UI immediately");
        GuideClick(UIAction("返回引导"));GuidePreview(root,"restart-skipped");check(G.S.GuideStep==0&&WantsGentleNarrow,"skipped guide can be reopened");
        G.S.GuideStep=2;G.S.GuideInWorld=true;G.S.Xp[0]=0;Go("伙伴");GuidePreview(root,"shortage-ready");G.S.Berries=0;for(int i=0;i<G.S.Crops.Length;i++)G.S.Crops[i]=1;GuidePreview(root,"shortage");
        check(GuidePrimary().IsEnabled&&GuideWords().Contains("目前没有种植"),"food shortage gives true reason");GuideFits(UIAction("去农田看看莓果"));
        GuideClick(UIAction("去农田看看莓果"));GuidePreview(root,"shortage-farm");check(Tab=="找莓果"&&WantsGentleNarrow,"shortage route opens the small real berry source");
        GuideClick(UIAction("先回去陪泡泡"));GuidePreview(root,"shortage-resumed");check(Tab=="伙伴"&&G.S.GuideStep==2&&G.S.Berries==0,"detour resumes without giving free resources");

        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=2;G.S.GuideInWorld=false;GuidePreview(root,"old-step-resumed");
        check(Tab=="伙伴"&&G.S.GuideInWorld&&G.S.Berries==10,"legacy guide steps resume with real progress");
        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=3;G.S.GuideInWorld=true;G.S.Xp[0]=24;Go("伙伴");GuidePreview(root,"old-growth-resumed");
        GuideClick(GuidePrimary());GuidePreview(root,"revealed-farm");
        check(G.S.GuideFarmSeen&&Tab=="农场","old levelled step can learn food source");
        G.S.GuideStep=7;G.S.SoilLevel=1;Go("农场");GuidePreview(root,"optional-evolution");
        UIChildren<Expander>((DependencyObject)Content).Single(e=>AutomationProperties.GetAutomationId(e)=="soil-guide-details").IsExpanded=true;UIPreview(root,"optional-evolution-expanded",484,501);
        GuideClick(UIAction("我还想看看进化"));GuidePreview(root,"evolution-plan");
        check(G.S.GuideStep==8&&Tab=="培养"&&G.S.CareTarget.Pet==4&&G.S.CareTarget.Evolution&&G.CurrentCarePlan.Berries==4&&G.CurrentCarePlan.Crystals==1,"evolution remains an optional real-cost plan");

        G.ResetGuideTest();ResetGuideView();G.S.GuideStep=2;G.S.GuideInWorld=true;Go("冒险");GuidePreview(root,"early-departure");GuideClick(GuidePrimary());GuidePreview(root,"early-travel");
        check(G.S.GuideStep==4&&G.Level(0)==1&&G.JourneyRunning,"early departure cannot strand guide on feeding");
        G.Retreat();G.Home();G.SyncGuide();Go("伙伴");GuidePreview(root,"early-return");
        check(G.S.GuideStep==3&&GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="喂养 · 莓果 2","early return still permits feeding a weak pet");
        G.S.GuideStep=6;G.S.Fertilizer=0;Go("农场");GuidePreview(root,"fertilizer-shortage");
        check(GuidePrimary().IsEnabled&&Convert.ToString(GuidePrimary().Content)=="再去小溪收集肥料"&&!G.UpgradeSoil(),"shortage offers preparation and never spends missing fertilizer");GuideFits(GuidePrimary());
        GuideClick(UIAction("再去小溪收集肥料"));GuidePreview(root,"fertilizer-retry");check(G.AtHome&&Tab=="冒险","fertilizer retry only opens preparation");
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"all guide checks remain detached and memory-only");
        File.WriteAllText(Path.Combine(root,"guide-ui-regression.txt"),"PASS: progressive disclosure; one real feed and actual harvest acknowledgement close basic care before optional adventure; quiet companionship interval; invitation can be deferred without repeat prompts; continued feeding and persistent outing entry; cancellable preparation and stale controls; no premature fertilizer lesson or compact-pet adventure prompt; small/default/minimum window layouts; genuine first journey, combo evidence and soil loop; no-save restart/skip/resume; pause survives fast-forward/navigation; legacy steps and shortages. No visible/native windows or production-save access.");
    }
}
