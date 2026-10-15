using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public partial class FarmPanel
{
    private void LifePicnic()
    {
        int id=picnicPreview,planned=G.S.PlannedPicnic;var current=LifeGuard();bool unlocked=G.PicnicUnlocked(id);var panel=GentleColumn(450);
        panel.Children.Add(GentleText(FarmGame.PicnicNames[id],21));panel.Children.Add(new PicnicView{Recipe=id,Width=62,Height=62,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(GentleText(PicnicPreparationText(id),13,PicnicFriendPresent(id)?U.Green:U.Gold));
        panel.Children.Add(GentleText(unlocked?"出发时制作："+FarmGame.PicnicCostText(id):"第一次通关小溪后开放。",12,U.Muted));
        if(unlocked){var shortage=Live(()=>G.PicnicShortage(id),12,U.Muted);shortage.TextAlignment=TextAlignment.Center;panel.Children.Add(shortage);}
        panel.Children.Add(GentleAction(unlocked?"选这份 · 出发时再做":"先去小溪看看",()=>{
            if(!current()||G.S.PlannedPicnic!=planned)return;
            if(unlocked){if(G.PlanJourneyPicnic(id,journeyDifficulty,journeyDepth))Go("冒险");}else{journeyDifficulty=journeyDepth=0;Go("冒险");}
        }));
        U.Row(panel,Btn("上一份",()=>{if(current())LifeChange(()=>picnicPreview=(id+FarmGame.PicnicCount-1)%FarmGame.PicnicCount);}),Btn("下一份",()=>{if(current())LifeChange(()=>picnicPreview=(id+1)%FarmGame.PicnicCount);}));
        LifeBack(panel,"冒险","返回出发准备",current);
        LifeDetails(panel,"搭配与材料","life-picnic-details",U.Text(G.PicnicAdvice(id,selectedBlessing),12,U.Green),Btn("看看这份便当的材料",()=>{if(current())OpenSmallResources("picnic",id);},unlocked),Btn("这次不带便当",()=>{if(current()&&G.S.PlannedPicnic==planned&&G.PlanJourneyPicnic(-1,journeyDifficulty,journeyDepth))Go("冒险");}));
    }
    private void LifeRelics()
    {
        var owned=Enumerable.Range(0,6).Where(G.RelicOwned).ToArray();var current=LifeGuard();var panel=GentleColumn(450);
        panel.Children.Add(GentleText("出门带的小信物",21));
        if(owned.Length==0){
            panel.Children.Add(new FarmGlyph{Kind="crystal",Width=70,Height=70,HorizontalAlignment=HorizontalAlignment.Center});panel.Children.Add(GentleText("第一次通关一段路，就能找到那里的信物。",13,U.Green));
            panel.Children.Add(GentleAction("回出发准备",()=>{if(current())Go("冒险");}));return;
        }
        if(!owned.Contains(lifeRelic))lifeRelic=owned[0];int id=lifeRelic,slot=Math.Min(1,lifeSlot),index=Array.IndexOf(owned,id);string before=String.Join(",",G.S.EquippedRelics);bool equipped=G.S.EquippedRelics[slot]==id;
        panel.Children.Add(new RelicView{Relic=id,Width=60,Height=60,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(GentleText(FarmGame.RelicNames[id],16,U.Green));panel.Children.Add(GentleText(FarmGame.RelicDetails[id],12));
        panel.Children.Add(GentleText("第 "+(slot+1)+" 格 · "+(G.S.EquippedRelics[slot]<0?"还空着":FarmGame.RelicNames[G.S.EquippedRelics[slot]]),12,U.Muted));
        panel.Children.Add(GentleAction(equipped?"回出发准备":"把这件放进第 "+(slot+1)+" 格",()=>{if(current()&&String.Join(",",G.S.EquippedRelics)==before){if(equipped)Go("冒险");else if(G.EquipRelic(id,slot))lifeRevision++;}}));
        U.Row(panel,Btn("看看下一件",()=>{if(current())LifeChange(()=>lifeRelic=owned[(index+1)%owned.Length]);},owned.Length>1),Btn("换到另一格",()=>{if(current())LifeChange(()=>lifeSlot=1-slot);}));
        if(!equipped)LifeBack(panel,"冒险","返回出发准备",current);
        LifeDetails(panel,"搭配与收起","life-relic-details",U.Text(G.RelicAdvice(id),12,U.Muted),U.Text("每件只带一份。已在另一格的信物会互换位置。",12,U.Muted),Btn("收起这一格",()=>{if(current()&&String.Join(",",G.S.EquippedRelics)==before&&G.EquipRelic(-1,slot))lifeRevision++;},G.S.EquippedRelics[slot]>=0));
    }
}

