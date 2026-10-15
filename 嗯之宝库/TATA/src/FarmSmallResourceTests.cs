using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void ResourcePreview(string root,string name,int size=0){ReadingPreview(root,"resource-"+name,size,true);GuideFits(GuidePrimary());}
    internal void RenderSmallResourceChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        EverydayFixture();G.ChooseCourtyardCompanion(4);G.S.Grain=0;OpenLife("照料");ResourcePreview(root,"training-shortage");
        string before=FarmPracticeTests.Xml(G.S);var entry=GuidePrimary();GuideClick(entry);ResourcePreview(root,"grain-wait");
        check(Tab=="材料"&&WantsGentleNarrow&&FarmPracticeTests.Xml(G.S)==before&&G.S.SmallReminder==null,"source entry is read-only and creates no reminder");
        int plot=scenes.Single().LearningPlot;check(plot==G.ResourcePlot(1)&&G.S.Crops[plot]==1&&scenes.Single().FocusedFarmPet==4,"source selects earliest real wheat harvest and correct companion");
        var staleReturn=GuidePrimary();var staleRemember=UIAction("回小院，记着这件事");double progress=LessonProgress("small-resource-progress").Value;
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(5));RefreshCounters();
        check(scenes.Single().LearningPlot==plot&&LessonProgress("small-resource-progress").Value>progress,"growth follows the watched wheat plot without changing source");
        int xp=G.S.Xp[4];G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(150));RefreshCounters();check(G.S.Grain>=3&&G.S.Xp[4]==xp&&Tab=="材料","real automatic harvest never trains or navigates");
        int grain=G.S.Grain;before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ResourcePreview(root,"training-ready");check(Tab=="照料"&&G.CourtyardCompanion==4&&FarmPracticeTests.Xml(G.S)==before,"ready return keeps chosen companion and spends nothing");
        staleRemember.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.S.SmallReminder==null,"old source cannot create reminder after returning");GuideClick(GuidePrimary());ResourcePreview(root,"training-done");check(G.S.Grain==grain-3&&G.S.Xp[4]==xp+18,"explicit training still charges once");

        G.S.Grain=0;OpenSmallResources("train",4);ResourcePreview(root,"remember-source");GuideClick(UIAction("回小院，记着这件事"));ResourcePreview(root,"remember-home");
        check(Tab=="农场"&&G.S.SmallReminder!=null&&G.S.SmallReminder.Pet==4&&GuideWords().Contains("还记着"),"one explicitly chosen activity replaces ordinary home action");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ResourcePreview(root,"remember-resume");check(Tab=="材料"&&FarmPracticeTests.Xml(G.S)==before,"resume opens source without selecting, spending or planting");
        G.S.GardenUnlocked=true;G.S.Mint=0;OpenSmallResources("mint",4);ResourcePreview(root,"replacement-preview");check(G.S.SmallReminder.Activity=="train","viewing another source preserves saved reminder");GuideClick(UIAction("改记这件事，回小院"));ResourcePreview(root,"mint-remembered");check(G.S.SmallReminder.Activity=="mint","reminder replacement is explicit");
        GuideClick(GuidePrimary());ResourcePreview(root,"mint-no-crop");check(scenes.Single().Visibility==Visibility.Collapsed&&((StackPanel)LessonProgress("small-resource-progress").Parent).Visibility==Visibility.Collapsed,"missing crop has no fake farm scene");
        before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ResourcePreview(root,"mint-plant-preview");int mintPlot=lifePlot;var growth=(double[])G.S.Growth.Clone();check(Tab=="农田"&&lifeCrop==2&&G.S.Crops[mintPlot]!=2&&FarmPracticeTests.Xml(G.S)==before,"missing mint preselects only a planting preview");
        var plant=GuidePrimary();GuideClick(plant);ResourcePreview(root,"mint-planted");check(G.S.Crops[mintPlot]==2&&G.S.Growth[mintPlot]==0&&G.S.Growth.Where((v,i)=>i!=mintPlot).SequenceEqual(growth.Where((v,i)=>i!=mintPlot)),"mint planting affects only the confirmed plot");
        before=FarmPracticeTests.Xml(G.S);plant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"stale planting is inert");GuideClick(GuidePrimary());ResourcePreview(root,"mint-growing");check(Tab=="材料"&&scenes.Single().LearningPlot==mintPlot&&smallResourceKind==2,"mint detour returns to actual new crop");
        G.TickFarm(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(200));RefreshCounters();int mint=G.S.Mint;xp=G.S.Xp[4];GuideClick(GuidePrimary());ResourcePreview(root,"mint-ready");check(Tab=="点心"&&Convert.ToString(GuidePrimary().Content)=="吃一份点心 · 薄荷 1"&&G.S.Mint==mint&&G.S.Xp[4]==xp,"mint return opens one visible snack action without spending");
        GuideClick(GuidePrimary());ResourcePreview(root,"mint-done");check(G.S.SmallReminder==null&&G.S.Mint==mint-1&&G.S.Xp[4]==xp+24,"real snack completes only its own reminder");

        EverydayFixture();G.S.Team=new[]{0,2,4};G.S.GardenUnlocked=true;G.S.Evolved=true;G.S.ExpeditionClears[3]=1;journeyDifficulty=1;journeyDepth=1;G.S.Berries=G.S.Grain=G.S.Mint=0;OpenLife("便当");picnicPreview=5;ResourcePreview(root,"recipe-preview");
        LaterExpander("life-picnic-details").IsExpanded=true;UIPreview(root,"resource-recipe-details",484,501);before=FarmPracticeTests.Xml(G.S);GuideClick(UIAction("看看这份便当的材料"));ResourcePreview(root,"recipe-berries");check(smallResourceKind==0&&smallResource.Recipe==5&&FarmPracticeTests.Xml(G.S)==before,"recipe source uses previewed recipe and leaves the plan unchanged");
        G.S.Berries=2;RefreshCounters();var nextIngredient=GuidePrimary();GuideClick(nextIngredient);ResourcePreview(root,"recipe-grain");check(smallResourceKind==1&&G.S.Berries==2&&G.S.PlannedPicnic==-1,"one-at-a-time recipe materials advance without payment");
        nextIngredient.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(smallResourceKind==1,"old next-material control cannot skip another ingredient");
        G.S.Grain=2;RefreshCounters();GuideClick(GuidePrimary());ResourcePreview(root,"recipe-mint");check(smallResourceKind==2,"recipe shows the remaining third ingredient");
        G.S.Mint=1;RefreshCounters();before=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());ResourcePreview(root,"recipe-return");check(Tab=="便当"&&picnicPreview==5&&journeyDifficulty==1&&journeyDepth==1&&FarmPracticeTests.Xml(G.S)==before,"return preserves same recipe and destination without planning or spending");
        GuideClick(GuidePrimary());ResourcePreview(root,"recipe-planned");check(Tab=="冒险"&&G.S.PlannedPicnic==5&&G.S.Berries==2&&G.AtHome,"explicit recipe selection records plan only");
        G.S.Mint=0;ResourcePreview(root,"departure-shortage");before=FarmPracticeTests.Xml(G.S);var departureSource=GuidePrimary();GuideClick(departureSource);ResourcePreview(root,"departure-source");check(Tab=="材料"&&smallResourceKind==2&&FarmPracticeTests.Xml(G.S)==before,"short departure button directly opens missing planned ingredients");
        GuideClick(UIAction("回小院，记着这件事"));ResourcePreview(root,"recipe-remembered");

        string path=Path.Combine(root,"small-resource-save.xml");XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);var normal=new FarmGame(path);var window=new FarmWindow(normal,true);var panel=new FarmPanel(window);window.Panel=panel;
        check(normal.S.SmallReminder!=null&&normal.S.SmallReminder.Recipe==5&&normal.S.SmallReminder.Region==1&&normal.S.SmallReminder.Depth==1,"normal save reload retains activity and destination");
        panel.ResourcePreview(root,"reload-home");before=FarmPracticeTests.Xml(normal.S);panel.GuideClick(panel.GuidePrimary());panel.ResourcePreview(root,"reload-source");check(panel.Tab=="材料"&&panel.smallResource.Recipe==5&&FarmPracticeTests.Xml(normal.S)==before,"fresh panel resumes saved recipe read-only");
        normal.S.Mint=1;panel.RefreshCounters();panel.GuideClick(panel.GuidePrimary());panel.ResourcePreview(root,"reload-recipe");check(panel.picnicPreview==5&&panel.journeyDifficulty==1&&panel.journeyDepth==1,"reload recipe return restores saved destination");panel.GuideClick(panel.GuidePrimary());panel.ResourcePreview(root,"reload-preparation");
        int[] stocks={normal.S.Berries,normal.S.Grain,normal.S.Mint};var depart=panel.GuidePrimary();panel.GuideClick(depart);panel.ResourcePreview(root,"reload-departed");check(normal.R.Picnic==5&&normal.R.Difficulty==1&&normal.R.Depth==1&&normal.S.SmallReminder==null&&normal.S.Berries==stocks[0]-2&&normal.S.Grain==stocks[1]-2&&normal.S.Mint==stocks[2]-1,"actual departure spends exact recipe and clears matching reminder once");
        before=FarmPracticeTests.Xml(normal.S);depart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(normal.S)==before,"stale departure cannot spend twice");normal.ToggleJourneyPause();panel.OpenLife("照料");panel.ResourcePreview(root,"away-paused");check(panel.GuideWords().Contains("旅程暂停着")&&!panel.GuideWords().Contains("照常继续"),"away preparation states pause honestly");
        normal.Retreat();panel.ResourcePreview(root,"away-result");check(panel.GuideWords().Contains("带回的收获"),"completed travel routes to actual result");

        var legacy=XDocument.Parse(FarmPracticeTests.Xml(G.S));legacy.Root.Element("SmallReminder").Remove();string old=Path.Combine(root,"small-resource-legacy.xml");legacy.Save(old);var loaded=new FarmGame(old);check(loaded.S.SmallReminder==null&&loaded.S.PlannedPicnic==5&&loaded.S.Evolved,"old save gains no unsolicited reminder and preserves progress");
        legacy.Root.Add(new XElement("SmallReminder",new XElement("Activity","picnic"),new XElement("Recipe",999),new XElement("Region",99),new XElement("Depth",99)));legacy.Save(old);loaded=new FarmGame(old);check(loaded.S.SmallReminder==null&&loaded.S.PlannedPicnic==5,"invalid reminder clears without resetting game");
        G.S.SmallReminder=new FarmSmallReminder{Activity="train",Pet=4};G.S.Grain=6;check(G.Train(0)&&G.S.SmallReminder!=null,"unrelated care does not clear reminder");check(G.Train(4)&&G.S.SmallReminder==null,"intended real care clears its reminder");

        for(int size=0;size<3;size++){
            EverydayFixture();G.S.Grain=0;G.ChooseCourtyardCompanion(4);OpenSmallResources("train",4);ResourcePreview(root,"font-wait",size);GuideFits(UIAction("回小院，记着这件事"));GuideFits(LaterExpander("small-resource-details"));
            var saved=UIAction("回小院，记着这件事");GuideClick(saved);ResourcePreview(root,"font-home",size);GuideFits(UIAction("先去陪"+G.Name(4)));GuideFits(LaterExpander("courtyard-things"));
            G.S.Grain=3;RefreshCounters();check(GuideWords().Contains("材料够啦"),"home reminder notices harvest without page reopen");GuideClick(GuidePrimary());ResourcePreview(root,"font-ready",size);
            G.S.GardenUnlocked=true;G.S.Mint=0;OpenSmallResources("mint",4);ResourcePreview(root,"font-missing",size);GuideFits(UIAction("改记这件事，回小院"));
            GuideClick(GuidePrimary());ResourcePreview(root,"font-planting",size);GuideFits(UIAction("返回上一页"));
            G.S.Mint=1;ReturnSmallActivity(smallResource);ResourcePreview(root,"font-snack",size);check(Tab=="点心"&&Convert.ToString(GuidePrimary().Content)=="吃一份点心 · 薄荷 1","mint return exposes snack as the sole primary action at font "+size);GuideFits(UIAction("先不吃，回去陪伙伴"));
        }
        EverydayFixture();G.S.Grain=0;OpenSmallResources("train",0);ResourcePreview(root,"keyboard-source");ResetPanelInput();Render();ResourcePreview(root,"keyboard-ready");FocusByTab(UIAction("回小院，记着这件事"));KeyPress(Key.Enter);ResourcePreview(root,"keyboard-home");check(G.S.SmallReminder!=null,"keyboard can explicitly save a reminder");
        FocusByTab(GuidePrimary());KeyPress(Key.Enter);ResourcePreview(root,"keyboard-resume");check(Tab=="材料","keyboard resumes the source");LaterExpander("small-resource-details").IsExpanded=true;UIPreview(root,"resource-forget-details",484,501);GuideClick(UIAction("不再记这件事"));ResourcePreview(root,"forgotten");check(G.S.SmallReminder==null&&Convert.ToString(GuidePrimary().Content)=="去喂泡泡","forget returns ordinary companionship");
        OpenSmallResources("train",0);ResourcePreview(root,"reset-source");var oldRemember=UIAction("回小院，记着这件事");G.ResetGuideTest();ResetGuideView();ResourcePreview(root,"reset");before=FarmPracticeTests.Xml(G.S);oldRemember.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));staleReturn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(G.S.SmallReminder==null&&FarmPracticeTests.Xml(G.S)==before&&G.SavePath==null&&!owner.IsVisible&&!IsVisible,"fresh guide and stale resource controls never affect production saves or show windows");
        File.WriteAllText(Path.Combine(root,"small-resource-regression.txt"),"PASS: actual wheat/mint/berry source; stable watched plot and real automatic harvest; missing crop preview and single-plot explicit planting; same pet/snack/recipe return without spending; sequential recipe ingredients; departure shortage recovery; optional one-activity reminder, replacement, forgetting and matching completion; isolated normal save and fresh-panel reload with destination; old and malformed saves; paused/result wording; stale controls; default/115%/130% minimum-window layout and keyboard; fresh memory-only guide. Detached WPF only.");
    }
}
