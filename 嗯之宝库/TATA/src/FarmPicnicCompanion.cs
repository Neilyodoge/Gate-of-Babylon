using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmSave picnicPartyState;
    private int picnicPartyRecipe=-1,picnicPartySlot,picnicPartyRegion,picnicPartyDepth;
    private void OpenPicnicCompanion(int recipe)
    {
        if(!SimpleCourtyard||!G.AtHome||G.S.PlannedPicnic!=recipe||PicnicFriendPresent(recipe))return;
        picnicPartyState=G.S;picnicPartyRecipe=recipe;picnicPartySlot=0;picnicPartyRegion=journeyDifficulty;picnicPartyDepth=journeyDepth;
        OpenLife("便当伙伴");
    }
    private void ReturnPicnicCompanion()
    {
        if(Object.ReferenceEquals(picnicPartyState,G.S)){journeyDifficulty=picnicPartyRegion;journeyDepth=picnicPartyDepth;}
        picnicPartyState=null;picnicPartyRecipe=-1;Go("冒险");pageHistory.RemoveAll(t=>t=="便当伙伴");resetScroll=true;
    }
    private void PicnicCompanionPage()
    {
        int recipe=picnicPartyRecipe,pet=PicnicRequiredFriend(recipe),slot=picnicPartySlot;var state=G.S;var guard=LifeGuard();
        bool valid=Object.ReferenceEquals(picnicPartyState,state)&&recipe==state.PlannedPicnic&&pet>=0;
        if(!valid||PicnicFriendPresent(recipe)){
            var ready=GentleColumn();ready.Children.Add(GentleText(valid?"同行的伙伴已经在啦":"回去看看这次准备吧",21));
            ready.Children.Add(GentleAction("回出发准备",()=>{if(guard())ReturnPicnicCompanion();}));return;
        }
        int previous=state.Team[slot];string team=String.Join(",",state.Team);
        Func<bool> current=()=>guard()&&Object.ReferenceEquals(picnicPartyState,state)&&state.PlannedPicnic==recipe&&picnicPartyRecipe==recipe&&picnicPartySlot==slot&&String.Join(",",state.Team)==team;
        var panel=GentleColumn(450);panel.Children.Add(GentleText("让"+G.Name(pet)+"站哪里？",21));panel.Children.Add(Portrait(pet,70));
        panel.Children.Add(GentleText(FarmGame.PicnicName(recipe)+"需要它同行。",13,U.Green));
        panel.Children.Add(GentleText("第 "+(slot+1)+" 位 · "+(slot==0?"前排":"后排"),16,U.Green));
        panel.Children.Add(GentleText("这里现在是"+G.Name(previous)+"，确认后让它回农场休息。",12,U.Muted));
        panel.Children.Add(GentleAction("确认让"+G.Name(pet)+"站这里",()=>{if(current()&&G.AssignPartySlot(slot,pet))ReturnPicnicCompanion();}));
        U.Row(panel,Btn("换个位置看看",()=>{if(current())LifeChange(()=>picnicPartySlot=(slot+1)%3);}),Btn("先不换，回准备",()=>{if(current())ReturnPicnicCompanion();}));
        AutomationProperties.SetAutomationId(panel,"picnic-companion-recovery");
    }
}
