using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private void LifePlanting()
    {
        lifePlot=Math.Max(0,Math.Min(G.Plots-1,lifePlot));lifeCrop=Math.Max(0,Math.Min(G.S.GardenUnlocked?2:1,lifeRequestedCrop>=0?lifeRequestedCrop:lifeCrop));
        int plot=lifePlot,kind=lifeCrop,old=G.S.Crops[plot];var state=G.S;var current=LifeGuard();var panel=GentleColumn(450);
        panel.Children.Add(GentleText("第 "+(plot+1)+" 块田",21));
        panel.Children.Add(new FarmGlyph{Kind=new[]{"berry","grain","mint"}[kind],Width=54,Height=54,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(GentleText(lifeRequestedCrop>=0?(kind==old?FarmGame.CropNames[kind]+"在长啦":"准备种"+FarmGame.CropNames[kind]):"看看"+FarmGame.CropNames[kind],16,U.Green));
        var words=Live(()=>"现在种着"+FarmGame.CropNames[old]+" · 约 "+G.SecondsLeft(plot)+" 秒后收好",12,U.Muted);words.TextAlignment=TextAlignment.Center;panel.Children.Add(words);
        var bar=U.Bar(0,100);bar.Height=9;bar.MaxWidth=300;AutomationProperties.SetAutomationId(bar,"life-plot-progress");AutomationProperties.SetName(bar,"第 "+(plot+1)+" 块田当前作物生长进度");updates.Add(()=>bar.Value=100*Math.Min(1,state.Growth[plot]/G.CropSeconds(old)));panel.Children.Add(bar);
        panel.Children.Add(GentleText(kind==old?"这块田已经在种它了。":"确认改种后，这块田会从头生长。",12,U.Muted));
        var action=GentleAction(kind==old?(lifeRequestedCrop>=0?"看看它怎么长":"返回上一页"):"改种"+FarmGame.CropNames[kind]+" · 重新计时",()=>{
            if(!current()||state.Crops[plot]!=old)return;
            if(kind==old){if(lifeRequestedCrop>=0)ReturnLifePlanting();else Go(plantingReturn);}else if(G.Plant(plot,kind)){lifeReceipt="这块田改种好啦，其他田照常生长。";lifeRevision++;}
        });if(lifeRequestedCrop>=0)action.MinHeight=44;panel.Children.Add(action);
        if(lifeRequestedCrop>=0){
            if(kind!=old)U.Row(panel,Btn("换一块看看",()=>{if(current())LifeChange(()=>lifePlot=(plot+1)%G.Plots);}),Btn("返回上一页",()=>{if(current())ReturnLifePlanting();}));
        }else{
            U.Row(panel,Btn("看看下一种",()=>{if(current())LifeChange(()=>lifeCrop=(kind+1)%(state.GardenUnlocked?3:2));}),Btn("下一块田",()=>{if(current())LifeChange(()=>{lifePlot=(plot+1)%G.Plots;lifeCrop=state.Crops[lifePlot];});}));
            if(kind!=old)LifeBack(panel,plantingReturn,"返回上一页",current);
        }
    }
    private void LifeGarden()
    {
        var available=new[]{-1}.Concat(Enumerable.Range(0,6).Where(G.DecorationOwned)).ToArray();if(!available.Contains(lifeDecoration))lifeDecoration=-1;
        int id=lifeDecoration,slot=lifeGardenSpot,index=Array.IndexOf(available,id);string before=String.Join(",",G.S.GardenDecorations);var current=LifeGuard();var panel=GentleColumn(450);bool placed=G.S.GardenDecorations[slot]==id;
        panel.Children.Add(GentleText(FarmGame.GardenSpotNames[slot],21));
        var scene=Scene();scene.Height=scene.MinHeight=100;scene.FocusedFarmPet=G.CourtyardCompanion;scene.HideFarmPets=true;scene.DecorationPreview=G.PreviewGardenDecoration(slot,id);scene.PreviewSpot=slot;panel.Children.Add(scene);
        panel.Children.Add(GentleText(id<0?"留一点空地":FarmGame.DecorationNames[id],16,U.Green));
        panel.Children.Add(GentleText(placed?"院子里现在就是这样。":"先看看，点确认才会摆进小院。",12,U.Muted));
        panel.Children.Add(GentleAction(placed?"回小院":id<0?"收起这里的摆件":"就摆在这里",()=>{if(current()&&String.Join(",",G.S.GardenDecorations)==before){if(placed)Go("农场");else if(G.PlaceGardenDecoration(slot,id))lifeRevision++;}}));
        U.Row(panel,Btn("换一件看看",()=>{if(current())LifeChange(()=>lifeDecoration=available[(index+1)%available.Length]);},available.Length>1),Btn("换个位置",()=>{if(current())LifeChange(()=>{lifeGardenSpot=(slot+1)%3;lifeDecoration=G.S.GardenDecorations[lifeGardenSpot];});}));
        if(!placed)LifeBack(panel,"农场","先不摆，回小院",current);
        if(available.Length==1)panel.Children.Add(GentleText("还没带回摆件，去小溪走一圈就有机会找到。",12,U.Muted));
    }
    private void LifeJournal()
    {
        var memories=G.S.JourneyMemories;var current=LifeGuard();var panel=GentleColumn(450);panel.Children.Add(GentleText("一起走过的路",21));
        if(memories.Length==0){panel.Children.Add(GentleText("等第一次回来，就会在这里留下一页。",13,U.Green));panel.Children.Add(GentleAction("回小院",()=>{if(current())Go("农场");}));return;}
        lifeMemory=Math.Max(0,Math.Min(memories.Length-1,lifeMemory));var memory=memories[lifeMemory];int index=lifeMemory;
        var scene=Scene();scene.Journey=scene.JourneyPreview=true;scene.HidePreviewParty=true;scene.PreviewRegion=memory.Region;scene.Height=scene.MinHeight=82;panel.Children.Add(scene);
        panel.Children.Add(GentleText(FarmGame.ExpeditionName(memory.Region,memory.Depth),17,U.Green));
        panel.Children.Add(GentleText(new DateTime(memory.RecordedTicks,DateTimeKind.Utc).ToLocalTime().ToString("M月d日 HH:mm")+" · "+(memory.Outcome=="victory"?"走完啦":memory.Outcome=="retreat"?"提前回家":"回来休息"),12,U.Muted));
        panel.Children.Add(GentleText("带回肥料 "+memory.Fertilizer+" · 晶石 "+memory.Crystals,13,U.Green));
        panel.Children.Add(GentleAction("回小院",()=>{if(current())Go("农场");}));
        if(memories.Length>1)U.Row(panel,Btn("较新的一页",()=>{if(current())LifeChange(()=>lifeMemory=index-1);},index>0),Btn("较早的一页",()=>{if(current())LifeChange(()=>lifeMemory=index+1);},index<memories.Length-1));
        LifeDetails(panel,"看看那天的伙伴与收获","life-memory-details",U.Text(String.Join("、",memory.Names.Select((name,i)=>name+" Lv."+memory.Levels[i]).ToArray()),12,U.Muted),
            U.Text((memory.Decoration>=0?"小纪念："+FarmGame.DecorationNames[memory.Decoration]+"。":"")+(memory.Relic>=0?"信物："+FarmGame.RelicNames[memory.Relic]+"。":""),12,U.Green),
            Btn("再准备去一次",()=>{if(current()&&G.ExpeditionUnlocked(memory.Region,memory.Depth)){SelectJourneyDestination(memory.Region,memory.Depth);Go("冒险");}},G.ExpeditionUnlocked(memory.Region,memory.Depth)));
    }
    private void LifeChapters()
    {
        ChapterGuidePage();
    }
}

