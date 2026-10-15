using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private int courtyardVisit,keepsakeSelection=-1;
    private FarmSave courtyardThingsState;
    private FarmSave keepsakeState;
    private bool courtyardThingsOpen;
    private FarmSoilReceipt courtyardSoilReceipt;
    private bool CourtyardPage(FarmSave state,string tab,int visit){return Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome&&Tab==tab&&courtyardVisit==visit;}
    private UIElement CourtyardThings()
    {
        if(!Object.ReferenceEquals(courtyardThingsState,G.S)){courtyardThingsState=G.S;courtyardThingsOpen=false;}
        var state=G.S;var choices=new StackPanel();
        if(G.ValidAwayHarvest!=null)choices.Children.Add(Btn("看看离开时的收成",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)OpenAwayHarvest();}));
        var reminder=state.SmallReminder;if(reminder!=null)choices.Children.Add(Btn("忘掉上次的小事",()=>{if(Object.ReferenceEquals(state,G.S)&&Object.ReferenceEquals(reminder,G.S.SmallReminder)&&SimpleCourtyard&&G.AtHome&&Tab=="农场")G.ForgetSmallActivity();}));
        choices.Children.Add(Btn("给田施肥",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)Go("施肥");}));
        choices.Children.Add(Btn("看看小纪念",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)Go("小纪念");}));
        choices.Children.Add(Btn("看看田里种了什么",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)OpenLife("农田");}));
        choices.Children.Add(Btn("布置小院",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)OpenLife("布置");}));
        choices.Children.Add(Btn("翻翻旅途记忆",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)OpenLife("旅途记忆");}));
        choices.Children.Add(Btn("看看成长手册",()=>{if(Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&G.AtHome)OpenLife("成长手册");}));
        var expand=new Expander{Header="小院日常",Content=choices,IsExpanded=courtyardThingsOpen,HorizontalAlignment=HorizontalAlignment.Center,MinWidth=230,Margin=new Thickness(0)};
        expand.Expanded+=(s,e)=>courtyardThingsOpen=true;expand.Collapsed+=(s,e)=>courtyardThingsOpen=false;AutomationProperties.SetAutomationId(expand,"courtyard-things");return expand;
    }
    private void CourtyardSoilPage()
    {
        if(!SimpleCourtyard||!G.AtHome){QuietHome();return;}
        var state=G.S;int visit=courtyardVisit,level=state.SoilLevel,cost=G.SoilCost;bool applied=courtyardSoilReceipt!=null&&Object.ReferenceEquals(courtyardSoilReceipt,G.ValidSoilReceipt);
        var panel=GentleColumn(450);panel.Children.Add(GentleText(applied?"田里长得更快啦":level>=5?"农田已经照料好啦":"给农田一点肥料",21));
        panel.Children.Add(SoilLessonPicture(applied||level>=5));
        if(applied){panel.Children.Add(SoilTimeCard());panel.Children.Add(GentleAction("回小院，慢慢玩",()=>{if(CourtyardPage(state,"施肥",visit))Go("农场");}));return;}
        if(level>=5){panel.Children.Add(GentleText("土壤已经满级，不用再花肥料。",13,U.Green));panel.Children.Add(GentleAction("回小院",()=>Go("农场")));return;}
        var stock=Live(()=>"已有肥料 "+state.Fertilizer+" · 这次用 "+cost,13,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        panel.Children.Add(GentleText("所有田地都会长得快一点。",12,U.Muted));
        if(state.Fertilizer<cost)panel.Children.Add(GentleAction("去小溪找点肥料",()=>{if(CourtyardPage(state,"施肥",visit)){journeyDifficulty=journeyDepth=0;Go("冒险");}}));
        else{
            var apply=GentleAction("施肥 · 肥料 "+cost,()=>{
                if(!CourtyardPage(state,"施肥",visit)||state.SoilLevel!=level||G.SoilCost!=cost)return;
                if(G.UpgradeSoil()){courtyardSoilReceipt=G.ValidSoilReceipt;resetScroll=true;}
            });panel.Children.Add(apply);updates.Add(()=>apply.IsEnabled=state.SoilLevel==level&&state.Fertilizer>=cost);
        }
        HomeGuideSecondary(panel,"回小院",()=>{if(CourtyardPage(state,"施肥",visit))Go("农场");});
    }
    private void CourtyardKeepsakePage()
    {
        if(!SimpleCourtyard||!G.AtHome){QuietHome();return;}
        var state=G.S;int visit=courtyardVisit;var owned=Enumerable.Range(0,6).Where(G.DecorationOwned).ToArray();var panel=GentleColumn(450);
        if(!Object.ReferenceEquals(keepsakeState,state)){keepsakeState=state;keepsakeSelection=-1;}
        panel.Children.Add(GentleText("宠物旁的小纪念",21));
        if(owned.Length==0){
            panel.Children.Add(Portrait(state.DisplayPet,86));panel.Children.Add(GentleText("还没有小纪念。去小溪走一圈，就有机会带回来。",13,U.Green));
            panel.Children.Add(GentleAction("去小溪看看",()=>{if(CourtyardPage(state,"小纪念",visit)){journeyDifficulty=journeyDepth=0;Go("冒险");}}));HomeGuideSecondary(panel,"回小院",()=>Go("农场"));return;
        }
        if(!owned.Contains(keepsakeSelection)){
            var recent=state.JourneyMemories.FirstOrDefault(m=>G.DecorationOwned(m.Decoration));keepsakeSelection=recent!=null?recent.Decoration:G.DecorationOwned(state.DisplayDecoration)?state.DisplayDecoration:owned[0];
        }
        int selected=keepsakeSelection,display=state.DisplayDecoration,index=Array.IndexOf(owned,selected);bool equipped=display==selected;
        var picture=new Grid{Height=96,Margin=new Thickness(0,0,0,7),Background=U.Pale};panel.Children.Add(picture);
        var pet=Portrait(state.DisplayPet,80);pet.HorizontalAlignment=HorizontalAlignment.Left;pet.Margin=new Thickness(70,0,0,0);picture.Children.Add(pet);
        picture.Children.Add(new DecorationView{Decoration=selected,Width=66,Height=66,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,78,5)});
        panel.Children.Add(GentleText(FarmGame.DecorationNames[selected],17,U.Green));
        panel.Children.Add(GentleText(equipped?"已经带在桌宠旁边啦。":"先看看样子，带上不花材料。",12,U.Muted));
        panel.Children.Add(GentleAction(equipped?"回小院":"桌宠带上这件",()=>{
            if(!CourtyardPage(state,"小纪念",visit)||keepsakeSelection!=selected||state.DisplayDecoration!=display)return;
            if(equipped)Go("农场");else G.EquipDecoration(selected);
        }));
        if(owned.Length>1){
            U.Row(panel,Btn("上一件",()=>{if(CourtyardPage(state,"小纪念",visit)&&keepsakeSelection==selected)keepsakeSelection=owned[(index+owned.Length-1)%owned.Length];}),Btn("下一件",()=>{if(CourtyardPage(state,"小纪念",visit)&&keepsakeSelection==selected)keepsakeSelection=owned[(index+1)%owned.Length];}));
        }
        if(display>=0)HomeGuideSecondary(panel,"先收起桌宠摆件",()=>{if(CourtyardPage(state,"小纪念",visit)&&state.DisplayDecoration==display)G.EquipDecoration(-1);});
        if(!equipped)HomeGuideSecondary(panel,"回小院",()=>{if(CourtyardPage(state,"小纪念",visit))Go("农场");});
    }
}

