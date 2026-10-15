using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool SimpleCourtyard{get{return !G.GuideActive&&G.S.SimpleCourtyard;}}
    private bool QuietShell{get{return SimpleCourtyard&&(LifeTab||Tab=="农场"||Tab=="伙伴"||Tab=="冒险"||Tab=="莓果田"||Tab=="施肥"||Tab=="小纪念"||Tab=="进化"||Tab=="目的地");}}
    private bool GentleJourney{get{return WorldGuide||SimpleCourtyard;}}
    private int touchedPet=-1,touchCount;
    private DateTime touchUntil;
    private FarmSave touchState;
    private void PetCompanion(FarmSave state,int pet)
    {
        if(!Object.ReferenceEquals(state,G.S)||!G.AtHome)return;
        touchState=state;touchedPet=pet;touchCount++;touchUntil=DateTime.UtcNow.AddSeconds(5);
        ShowCompanionMoment(pet,"touch",PetResponse(pet));
    }
    private bool PetResponding(int pet){return Object.ReferenceEquals(touchState,G.S)&&touchedPet==pet&&DateTime.UtcNow<touchUntil&&G.AtHome;}
    private string PetResponse(int pet)
    {
        return PetResponding(pet)?G.Name(pet)+(touchCount%2==1?"蹭了蹭你的手。":"开心地晃了晃身子。") : "摸摸它，也是在一起。";
    }
    private void CompanionResponse(StackPanel panel,int pet)
    {
        panel.Children.Add(CompanionLine(pet,()=>PetResponse(pet),13));
    }
    private void MakeSceneTouchable(FarmScene scene,int pet)
    {
        var state=G.S;scene.FocusedPetTouch=()=>Do(()=>PetCompanion(state,pet));
        scene.PetResponseUntil=PetResponding(pet)?touchUntil:DateTime.MinValue;
        scene.ShowBerryBasket=true;
        scene.ToolTip="点点"+G.Name(pet)+"，陪它一会儿。";
    }
    private UIElement TouchablePortrait(int pet,double height,bool foodThought=false)
    {
        var state=G.S;var button=Btn("摸摸"+G.Name(pet),()=>PetCompanion(state,pet),G.AtHome);
        var view=new CompanionMomentView{Game=G,Pet=pet,Tick=owner.Tick,Height=height,Width=height*1.65,FoodThought=foodThought,Moment=()=>CurrentCompanionMoment(pet)};companionViews.Add(view);
        button.Content=view;button.Background=System.Windows.Media.Brushes.Transparent;
        button.BorderBrush=System.Windows.Media.Brushes.Transparent;button.BorderThickness=new Thickness(1);button.Padding=new Thickness(0);button.Margin=new Thickness(0);button.HorizontalAlignment=HorizontalAlignment.Center;
        button.IsKeyboardFocusedChanged+=(s,e)=>button.BorderBrush=button.IsKeyboardFocused?U.Green:System.Windows.Media.Brushes.Transparent;
        button.ToolTip="摸摸"+G.Name(pet)+" · 不消耗莓果";
        AutomationProperties.SetName(button,"摸摸"+G.Name(pet));AutomationProperties.SetAutomationId(button,"companion-touch");AutomationProperties.SetHelpText(button,"轻轻摸摸，不消耗莓果。");return button;
    }
    private void ExpandAllPlay(string tab)
    {
        G.S.SimpleCourtyard=false;if(G.GuideActive)G.S.GuideStep=9;G.Save();Go(tab);
    }
    private void QuietHome()
    {
        int pet=G.CourtyardCompanion;var state=G.S;var panel=GentleColumn(480);
        panel.Children.Add(GentleText(G.AtHome?G.Name(pet)+"陪着你":"小院等着伙伴回来",22));
        var scene=Scene();scene.FocusedFarmPet=pet;scene.HideFarmPets=!G.AtHome;scene.Height=scene.MinHeight=140;if(G.AtHome)MakeSceneTouchable(scene,pet);panel.Children.Add(scene);
        var reminder=G.ValidSmallReminder(G.S.SmallReminder)?G.S.SmallReminder:null;
        if(reminder!=null)scene.Height=scene.MinHeight=126;
        Func<string> homeHint=()=>G.AtHome?reminder==null?"莓果会自动收好，有空再来看看。":(G.SmallResourcesReady(reminder)?"材料够啦 · ":"还记着 · ")+G.SmallReminderName(reminder):G.R.Phase=="result"?"伙伴回来啦，去看看这一趟的收获。":G.R.JourneyPaused?"旅程暂停着，莓果照常生长。":"莓果照常生长，伙伴走完就会回来。";
        var homeLine=reminder==null?CompanionLine(pet,homeHint,14):Live(homeHint,14,U.Green);homeLine.TextAlignment=TextAlignment.Center;panel.Children.Add(homeLine);
        if(G.AtHome){
            var feed=GentleAction(reminder==null?"去喂"+G.Name(pet):"接着上次的小事",()=>{if(!Object.ReferenceEquals(state,G.S)||!G.AtHome||Tab!="农场")return;if(reminder==null)Go("伙伴");else if(Object.ReferenceEquals(reminder,G.S.SmallReminder))OpenSmallResources(reminder);});feed.Margin=new Thickness(0,5,0,2);panel.Children.Add(feed);
            if(reminder!=null)HomeGuideSecondary(panel,"先去陪"+G.Name(pet),()=>{if(Object.ReferenceEquals(state,G.S)&&G.AtHome&&Tab=="农场")Go("伙伴");});
            if(reminder==null)HomeGuideSecondary(panel,"摸摸"+G.Name(pet),()=>PetCompanion(state,pet));
            panel.Children.Add(CourtyardThings());
        }else panel.Children.Add(GentleAction(G.R.Phase=="result"?"看看带回的收获":"看看伙伴走到哪了",()=>Go("冒险")));
    }
    private void QuietPet()
    {
        int pet=G.CourtyardCompanion;SelectGuidePetIfNeeded(pet);var state=G.S;var panel=GentleColumn(440);
        var title=GentleText(G.Name(pet)+"  Lv."+G.Level(pet),21);title.Margin=new Thickness(0,0,0,2);panel.Children.Add(title);panel.Children.Add(TouchablePortrait(pet,50));
        if(!G.AtHome){panel.Children.Add(GentleText("等伙伴回来，再陪它吃点东西。",14,U.Green));panel.Children.Add(GentleAction("看看伙伴",()=>Go("冒险")));return;}
        bool grown=G.Level(pet)>=10&&!G.NeedsEvolutionBond(pet);
        if(!grown&&state.Berries<2){var stock=Live(()=>"喂一口要 2 颗，篮子里有 "+state.Berries+" 颗。",13,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);}
        else panel.Children.Add(CompanionLine(pet,()=>grown?"已经长大啦，陪一会儿也很好。":"有空喂一口就好。",13));
        panel.Children.Add(GrowthCard(pet,true));
        int xp=state.Xp[pet],bond=G.PetBond(pet);
        var feed=GentleAction("",()=>{
            if(!Object.ReferenceEquals(state,G.S)||!SimpleCourtyard||!G.AtHome||Tab!="伙伴"||G.CourtyardCompanion!=pet||state.Xp[pet]!=xp||G.PetBond(pet)!=bond)return;
            if(grown)PetCompanion(state,pet);else if(state.Berries<2)Go("莓果田");else CareForFocusedPet(pet,()=>G.Feed(pet));
        });panel.Children.Add(feed);updates.Add(()=>feed.Content=grown?"摸摸"+G.Name(pet):state.Berries<2?"去看看莓果":FarmUX.FeedLabel(G,pet));
        int evolutionPet=G.HasNextEvolution(pet)?pet:G.HasNextEvolution(0)?0:4,visit=courtyardVisit,stage=SmallEvolutionStage(evolutionPet);
        var links=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(links);
        links.Children.Add(Btn("回小院",()=>Go("农场")));
        var more=LifeDetails(panel,"其他伙伴、训练与技能","life-companion-menu",Btn("看看其他伙伴",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&Tab=="伙伴")OpenLife("伙伴册");}),Btn("陪它练一会儿",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&Tab=="伙伴")OpenLife("照料");}),Btn("看看它的小本领",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&Tab=="伙伴")OpenLife("技能");}));
        if(G.HasNextEvolution(evolutionPet)){
            Button evolution=null;
            evolution=Btn(evolutionPet!=pet?"看看"+G.Name(evolutionPet)+"长大后":(evolutionPet==0?G.CanEvolve:G.CanEvolveSeed)?"可以进化啦 · 看看新样子":"看看长大后的样子",()=>{if(CourtyardPage(state,"伙伴",visit)&&G.CourtyardCompanion==pet&&SmallEvolutionStage(evolutionPet)==stage&&G.HasNextEvolution(evolutionPet)&&CanKeyboardReach(evolution))OpenSmallEvolution(evolutionPet);});
            AutomationProperties.SetAutomationId(evolution,"companion-evolution");
            if(evolutionPet==pet){evolution.Margin=new Thickness(8,3,0,3);links.Children.Add(evolution);}else ((StackPanel)more.Content).Children.Add(evolution);
        }
    }
}

