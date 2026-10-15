using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmGame
{
    public bool PlanJourneyPicnic(int recipe,int region,int depth)
    {
        if(!AtHome||!PicnicUnlocked(recipe)||!ExpeditionUnlocked(region,depth))return false;
        S.PlannedRegion=region;S.PlannedDepth=depth;
        return PlanPicnic(recipe);
    }
}

public partial class FarmPanel
{
    private int PicnicRequiredFriend(int recipe){return recipe==3?2:recipe==4?1:recipe==5?4:-1;}
    private bool PicnicFriendPresent(int recipe){int pet=PicnicRequiredFriend(recipe);return pet<0||G.S.Team.Contains(pet);}
    private string PicnicPreparationText(int recipe)
    {
        if(PicnicFriendPresent(recipe))return FarmGame.PicnicDetails[recipe];
        return (recipe==3?"电团":recipe==4?"赤钳":"种子")+"不在这队，这份便当暂时用不上。";
    }
    private FarmSave preparationCardsState;
    private int preparationCard;
    private void OpenPreparationCards()
    {
        if(!SimpleCourtyard||!G.AtHome)return;
        preparationCardsState=G.S;preparationCard=0;OpenLife("准备小卡");
    }
    private int[] PreparationCards()
    {
        var cards=new List<int>{0,1,2,3};
        if(G.S.PlannedPicnic>=0)cards.Add(4);
        for(int slot=0;slot<2;slot++)if(G.S.EquippedRelics[slot]>=0)cards.Add(5+slot);
        return cards.ToArray();
    }
    private void PreparationCardsPage()
    {
        if(!Object.ReferenceEquals(preparationCardsState,G.S)){preparationCardsState=G.S;preparationCard=0;}
        var cards=PreparationCards();preparationCard=Math.Max(0,Math.Min(cards.Length-1,preparationCard));
        int index=preparationCard,card=cards[index];var current=LifeGuard();var panel=GentleColumn(450);
        var count=GentleText("准备小卡 · "+(index+1)+" / "+cards.Length,12,U.Muted);count.Margin=new Thickness(0,0,0,4);panel.Children.Add(count);
        if(card==0){
            panel.Children.Add(GentleText(FarmGame.ExpeditionName(journeyDifficulty,journeyDepth),21));
            var scene=Scene();scene.Journey=scene.JourneyPreview=true;scene.PreviewRegion=journeyDifficulty;scene.HidePreviewParty=true;scene.Height=scene.MinHeight=100;panel.Children.Add(scene);
            panel.Children.Add(GentleText("这次要走的路 · 建议 "+FarmGame.ExpeditionLevel(journeyDifficulty,journeyDepth),13,U.Green));
            panel.Children.Add(GentleText("先看看准备，回出发页后再决定。",12,U.Muted));
        }else if(card<=3){
            int slot=card-1,pet=G.S.Team[slot],skill=G.S.Skills[pet];
            panel.Children.Add(GentleText("第 "+(slot+1)+" 位 · "+G.Name(pet),21));panel.Children.Add(Portrait(pet,70));
            panel.Children.Add(GentleText((slot==0?"站在前排":"站在后排")+" · Lv."+G.Level(pet),12,U.Muted));
            panel.Children.Add(GentleText("这次会用「"+FarmGame.SkillNames[pet][skill]+"」",16,U.Green));
            panel.Children.Add(GentleText(FarmGame.SkillDetails[pet][skill],13));
        }else if(card==4){
            int recipe=G.S.PlannedPicnic;panel.Children.Add(GentleText(FarmGame.PicnicName(recipe),21));panel.Children.Add(new PicnicView{Recipe=recipe,Width=62,Height=62,HorizontalAlignment=HorizontalAlignment.Center});
            panel.Children.Add(GentleText(PicnicPreparationText(recipe),13,PicnicFriendPresent(recipe)?U.Green:U.Gold));
            panel.Children.Add(GentleText("出发时才用："+FarmGame.PicnicCostText(recipe),12,U.Muted));
            var stock=Live(()=>"现在有："+String.Join("、",Enumerable.Range(0,3).Where(k=>FarmGame.PicnicCost(recipe)[k]>0).Select(k=>FarmGame.CropNames[k]+" "+G.CropStock(k)).ToArray()),12,U.Muted);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
            var shortage=Live(()=>G.PicnicShortage(recipe),12,U.Green);shortage.TextAlignment=TextAlignment.Center;panel.Children.Add(shortage);
        }else{
            int slot=card-5,relic=G.S.EquippedRelics[slot];panel.Children.Add(GentleText(FarmGame.RelicNames[relic],21));panel.Children.Add(new RelicView{Relic=relic,Width=64,Height=64,HorizontalAlignment=HorizontalAlignment.Center});
            panel.Children.Add(GentleText("放在行囊第 "+(slot+1)+" 格",12,U.Muted));panel.Children.Add(GentleText(FarmGame.RelicDetails[relic],13,U.Green));
            panel.Children.Add(GentleText("这件已经带好，出发不用再扣材料。",12,U.Muted));
        }
        bool last=index==cards.Length-1;int next=last?-1:cards[index+1];
        string label=last?"回出发准备":next<=3?(next==1?"看看同行的朋友":"看看下一位"):next==4?"看看带的便当":"看看带的信物";
        panel.Children.Add(GentleAction(label,()=>{if(!current())return;if(last)Go("冒险");else LifeChange(()=>preparationCard=index+1);}));
        if(index>0&&!last)U.Row(panel,Btn("上一张",()=>{if(current())LifeChange(()=>preparationCard=index-1);}),Btn("先回出发准备",()=>{if(current())Go("冒险");}));
        else if(index>0)HomeGuideSecondary(panel,"上一张",()=>{if(current())LifeChange(()=>preparationCard=index-1);});
        else HomeGuideSecondary(panel,"先回出发准备",()=>{if(current())Go("冒险");});
        AutomationProperties.SetAutomationId(panel,"preparation-card-"+card);
    }
}
