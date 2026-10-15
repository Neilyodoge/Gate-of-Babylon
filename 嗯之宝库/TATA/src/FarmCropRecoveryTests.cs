using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

public partial class FarmPanel
{
    private void CropRecoveryPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"crop-recovery-"+name,size,true);
        FarmHeadlessTests.Check(WantsGentleNarrow,"crop recovery uses the small window");
        foreach(var target in KeyboardTargets().Where(c=>Under(c,scroller)))ReviewTarget(target,"crop recovery / "+name+" / "+ControlKey(target));
        FarmHeadlessTests.Check(GuidePrimary().IsEnabled,"crop recovery has one enabled primary action");
    }
    private void CropRecoveryFixture(int kind)
    {
        EverydayFixture();G.S.GardenUnlocked=true;G.ChooseCourtyardCompanion(4);G.S.Berries=G.S.Grain=G.S.Mint=0;
        for(int i=0;i<G.Plots;i++){G.S.Crops[i]=(kind+1)%3;G.S.Growth[i]=i+2;}
        G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(1).Ticks;
        if(kind==0)Go("莓果田");else OpenSmallResources(kind==1?"train":"mint",4);
    }
    internal void RenderCropRecoveryChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++){
            for(int kind=0;kind<3;kind++){
                string prefix=new[]{"berry","grain","mint"}[kind];CropRecoveryFixture(kind);CropRecoveryPreview(root,prefix+"-missing",size);
                string before=FarmPracticeTests.Xml(G.S);var source=GuidePrimary();GuideClick(source);CropRecoveryPreview(root,prefix+"-preview",size);
                check(Tab=="农田"&&lifeCrop==kind&&lifeRequestedCrop==kind&&!HasGuideButton("看看下一种")&&FarmPracticeTests.Xml(G.S)==before,"missing crop is preselected without planting, saving or exposing unrelated seed choices");
                var oldPlant=GuidePrimary();int first=lifePlot;
                GuideClick(UIAction("换一块看看"));CropRecoveryPreview(root,prefix+"-next-plot",size);
                check(lifePlot!=first&&lifeCrop==kind&&FarmPracticeTests.Xml(G.S)==before,"browsing plots retains the needed crop and all actual state");
                oldPlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old plot confirmation cannot plant after switching plot");
                KeyPress(Key.Escape);CropRecoveryPreview(root,prefix+"-cancelled",size);check(Tab==(kind==0?"莓果田":"材料")&&FarmPracticeTests.Xml(G.S)==before,"Escape returns to the original source without planting");
                source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Tab==(kind==0?"莓果田":"材料"),"old source entry cannot reopen a cancelled planting preview");
                GuideClick(GuidePrimary());CropRecoveryPreview(root,prefix+"-reopened",size);GuideClick(UIAction("换一块看看"));CropRecoveryPreview(root,prefix+"-chosen",size);
                int selected=lifePlot;var crops=(int[])G.S.Crops.Clone();var growth=(double[])G.S.Growth.Clone();int xp=G.S.Xp[4];
                oldPlant=GuidePrimary();GuideClick(oldPlant);CropRecoveryPreview(root,prefix+"-planted",size);
                check(G.S.Crops[selected]==kind&&G.S.Growth[selected]==0&&Enumerable.Range(0,G.Plots).Where(i=>i!=selected).All(i=>G.S.Crops[i]==crops[i]&&G.S.Growth[i]==growth[i])&&G.CropStock(kind)==0&&G.S.Xp[4]==xp,"explicit planting changes only one selected plot, without food or care rewards");
                before=FarmPracticeTests.Xml(G.S);oldPlant.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before,"old confirmation cannot reset the newly growing crop");
                GuideClick(GuidePrimary());CropRecoveryPreview(root,prefix+"-growing",size);check(scenes.Single().LearningPlot==selected&&scenes.Single().FocusedFarmPet==4,"return watches the chosen actual plot for the same companion");
                object content=Content;owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(5),false,true,false);
                check(Object.ReferenceEquals(content,Content)&&LessonProgress(kind==0?"quiet-berry-progress":"small-resource-progress").Value>0&&G.S.Xp[4]==xp,"real clock updates the same page without automatic care");
                owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(150),false,true,false);CropRecoveryPreview(root,prefix+"-ready",size);int stock=G.CropStock(kind);
                check(stock>=(kind==1?3:kind==0?2:1)&&G.S.Xp[4]==xp,"real harvest supplies the required resource without consuming it");
                GuideClick(GuidePrimary());CropRecoveryPreview(root,prefix+"-returned",size);
                check(Tab==new[]{"伙伴","照料","点心"}[kind]&&G.CourtyardCompanion==4&&G.CropStock(kind)==stock,"ready return preserves the original care action and does not spend");
                GuideClick(GuidePrimary());CropRecoveryPreview(root,prefix+"-cared",size);
                check(G.S.Xp[4]==xp+new[]{12,18,24}[kind]&&G.CropStock(kind)==stock-new[]{2,3,1}[kind],"only the explicit original care action consumes the exact ingredient once");
            }

            CropRecoveryFixture(0);G.ChooseCourtyardCompanion(0);OpenSmallEvolution(4);CropRecoveryPreview(root,"evolution-preview",size);GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-need",size);
            int step=smallGrowthStep;GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-source",size);GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-planting",size);
            check(plantingBerryEvolution&&lifeCrop==0&&smallGrowthPet==4&&G.CourtyardCompanion==0,"seed evolution shortage keeps its subject independently of the everyday companion");
            KeyPress(Key.Escape);CropRecoveryPreview(root,"evolution-cancelled",size);check(berryForEvolution&&Tab=="莓果田","planting cancellation preserves the evolution origin");
            GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-reopened",size);GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-planted",size);GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-growing",size);
            check(berryForEvolution&&scenes.Single().FocusedFarmPet==4,"committed crop also returns to the evolution companion");
            owner.TickClock(new DateTime(G.S.LastFarmTicks,DateTimeKind.Utc).AddSeconds(50),false,true,false);GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-returned",size);
            check(Tab=="进化"&&smallGrowthPet==4&&smallGrowthStep==step&&G.CourtyardCompanion==0&&!G.S.Evolved&&G.S.SeedStage==0,"return keeps the exact unmet evolution step and does not switch everyday companion or evolve");
            int seedXp=G.S.Xp[4],bubbleXp=G.S.Xp[0],berries=G.S.Berries;GuideClick(GuidePrimary());CropRecoveryPreview(root,"evolution-fed",size);
            check(G.S.Xp[4]==seedXp+12&&G.S.Xp[0]==bubbleXp&&G.S.Berries==berries-2&&G.S.SeedStage==0,"explicit returned feeding affects only the evolution subject");
        }

        CropRecoveryFixture(2);GuideClickAfterPreview(root,"picnic-setup");int plot=lifePlot;
        G.Plant(plot,2);G.S.Growth[plot]=17;CropRecoveryPreview(root,"already-growing");string unchanged=FarmPracticeTests.Xml(G.S);GuideClick(GuidePrimary());CropRecoveryPreview(root,"already-growing-return");
        check(FarmPracticeTests.Xml(G.S)==unchanged&&G.S.Growth[plot]==17&&smallResourcePlot==plot,"viewing a plot already growing the needed crop never resets it");

        CropRecoveryFixture(1);G.S.PlannedPicnic=5;journeyDifficulty=0;journeyDepth=0;OpenSmallResources("picnic",5);CropRecoveryPreview(root,"picnic-source");
        G.S.Berries=2;RefreshCounters();GuideClick(GuidePrimary());CropRecoveryPreview(root,"picnic-grain");GuideClick(GuidePrimary());CropRecoveryPreview(root,"picnic-planting");GuideClick(UIAction("换一块看看"));CropRecoveryPreview(root,"picnic-next-plot");
        check(lifeRequestedCrop==1&&lifeCrop==1&&smallResource.Recipe==5,"multi-ingredient picnic retains both recipe and requested grain while browsing plots");
        KeyPress(Key.Escape);CropRecoveryPreview(root,"picnic-cancelled");check(Tab=="材料"&&smallResourceKind==1&&G.S.PlannedPicnic==5,"picnic cancellation returns to the same ingredient without replacing its plan");

        CropRecoveryFixture(0);string path=Path.Combine(root,"crop-recovery-normal.xml");System.Xml.Linq.XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);
        var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.Go("莓果田");panel.CropRecoveryPreview(root,"normal-source");string disk=File.ReadAllText(path);
        panel.GuideClick(panel.GuidePrimary());panel.CropRecoveryPreview(root,"normal-preview");panel.GuideClick(panel.UIAction("换一块看看"));panel.CropRecoveryPreview(root,"normal-next");
        check(File.ReadAllText(path)==disk,"normal save browsing never writes an unconfirmed crop choice");plot=panel.lifePlot;panel.GuideClick(panel.GuidePrimary());panel.CropRecoveryPreview(root,"normal-planted");
        var loaded=new FarmGame(path);check(loaded.S.Crops[plot]==0&&loaded.S.CourtyardPet==4&&loaded.S.Xp[4]==game.S.Xp[4]&&loaded.S.GuideStep==8,"normal reload preserves only the committed crop and existing companionship progress");
        panel.OpenLife("农田");panel.CropRecoveryPreview(root,"normal-general");check(panel.lifeRequestedCrop==-1&&panel.HasGuideButton("看看下一种"),"opening general farm management restores all crop choices");

        CropRecoveryFixture(0);GuideClickAfterPreview(root,"before-reset");var stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();unchanged=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(lifeRequestedCrop==-1&&!plantingBerryEvolution&&FarmPracticeTests.Xml(G.S)==unchanged&&G.SavePath==null,"fresh tutorial discards all crop recovery context and ignores old planting");
        check(!IsVisible&&!owner.IsVisible&&new WindowInteropHelper(window).Handle==IntPtr.Zero&&new WindowInteropHelper(panel).Handle==IntPtr.Zero,"crop recovery checks create no native windows");
        File.WriteAllText(Path.Combine(root,"crop-recovery-regression.txt"),"PASS: berry/grain/mint recovery keeps requested crop while browsing plots; read-only previews and cancellation; one selected plot replanted only after confirmation; actual clock, watched plot and harvest; return to original feeding/training/snack; exact single care transaction; evolution subject and unmet step preserved independently of daily companion; multi-ingredient picnic continuity; already-growing crop not reset; stale source/plot controls; normal browsing without save writes and committed-crop reload; general crop picker restored; fresh guide isolation; 100/115/130% actual-viewport targets. Detached WPF only.");
    }
    private void GuideClickAfterPreview(string root,string name)
    {
        CropRecoveryPreview(root,name+"-source");GuideClick(GuidePrimary());CropRecoveryPreview(root,name+"-preview");
    }
}
