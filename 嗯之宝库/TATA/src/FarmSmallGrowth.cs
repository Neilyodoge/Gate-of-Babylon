using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmSave smallGrowthState;
    private int smallGrowthPet,smallGrowthStep,smallGrowthRevision,smallGrowthCost;
    private string smallGrowthBefore="";
    private bool smallGrowthOpenedCave,berryForEvolution;
    private void OpenSmallEvolution(int pet)
    {
        smallGrowthState=G.S;smallGrowthPet=pet==4?4:0;evolutionFocusPet=smallGrowthPet;
        smallGrowthStep=0;smallGrowthRevision++;smallGrowthBefore="";Go("进化");resetScroll=true;
    }
    private int SmallEvolutionStage(int pet){return pet==0?(G.S.Evolved?1:0):G.S.SeedStage;}
    private UIElement SmallEvolutionPicture(int pet,bool future)
    {
        if(!future)return Portrait(pet,82);
        if(pet==4)return SeedPreview(Math.Min(3,G.S.SeedStage+1),82);
        return new Image{Source=Art.Frame(0,owner.Tick,true,0),Height=82,Stretch=System.Windows.Media.Stretch.Uniform};
    }
    private void SmallEvolutionProgress(StackPanel panel,string label,int value,int total)
    {
        panel.Children.Add(GentleText(label,13,U.Green));
        var bar=U.Bar(Math.Min(value,total),total);bar.Height=9;bar.MaxWidth=300;bar.Margin=new Thickness(0,0,0,8);
        AutomationProperties.SetAutomationId(bar,"small-evolution-progress");AutomationProperties.SetName(bar,label);panel.Children.Add(bar);
    }
    private void SmallEvolutionPage()
    {
        if(!G.AtHome){var away=GentleColumn();away.Children.Add(GentleText("伙伴回来后，再看看进化",21));away.Children.Add(GentleAction("看看伙伴走到哪了",()=>Go("冒险")));return;}
        if(!Object.ReferenceEquals(smallGrowthState,G.S)){smallGrowthState=G.S;smallGrowthPet=evolutionFocusPet==4?4:0;smallGrowthStep=0;smallGrowthBefore="";smallGrowthRevision++;}
        var state=G.S;int pet=smallGrowthPet,step=smallGrowthStep,revision=smallGrowthRevision,visit=courtyardVisit,stage=SmallEvolutionStage(pet);
        if(focusedPet!=pet)SelectFocusedPet(pet);
        Func<bool> current=()=>CourtyardPage(state,"进化",visit)&&smallGrowthPet==pet&&smallGrowthStep==step&&smallGrowthRevision==revision&&SmallEvolutionStage(pet)==stage;
        var panel=GentleColumn(440);bool next=G.HasNextEvolution(pet);
        if(step==2&&smallGrowthBefore!=""){
            panel.Children.Add(GentleText(G.Name(pet)+"，你好呀",22));panel.Children.Add(SmallEvolutionPicture(pet,false));
            panel.Children.Add(GentleText(smallGrowthBefore+" → "+G.Name(pet),14,U.Green));
            panel.Children.Add(GentleText("用了 "+smallGrowthCost+" 颗晶石，等级和站位都保留。",12,U.Muted));
            panel.Children.Add(GentleAction(smallGrowthOpenedCave?"先看看新开放的洞穴":"回小院",()=>{if(current()){if(smallGrowthOpenedCave)OpenSmallDestination(1,0);else Go("农场");}}));
            if(smallGrowthOpenedCave)HomeGuideSecondary(panel,"先回小院",()=>{if(current())Go("农场");});return;
        }
        if(!next){
            panel.Children.Add(GentleText(G.Name(pet)+"已经进化好啦",21));panel.Children.Add(SmallEvolutionPicture(pet,false));panel.Children.Add(GentleText("有空再陪它一会儿。",13,U.Green));panel.Children.Add(GentleAction("回小院",()=>{if(current())Go("农场");}));return;
        }
        int level=pet==0?3:G.SeedRequiredLevel,bond=pet==0?3:G.SeedRequiredBond,cost=pet==0?2:G.SeedEvolutionCost;
        string future=pet==0?"潮汐兽":FarmGame.SeedNames[stage+1];
        if(step==0){
            panel.Children.Add(GentleText("长大以后，会是什么样？",21));panel.Children.Add(SmallEvolutionPicture(pet,true));
            panel.Children.Add(GentleText(future,17,U.Green));
            panel.Children.Add(GentleText(pet==0?"它能让农田长得更快。":"它会学会新的毒种本领。",13,U.Muted));
            panel.Children.Add(GentleAction("看看还差什么",()=>{if(current()){smallGrowthStep=1;smallGrowthRevision++;resetScroll=true;}}));
        }else{
            int need=G.Level(pet)<level?0:G.PetBond(pet)<bond?1:state.Crystals<cost?2:3;
            panel.Children.Add(GentleText(need==0?"先长到 Lv."+level:need==1?"再熟悉彼此一点":need==2?"还差 "+(cost-state.Crystals)+" 颗晶石":"准备好进化啦",21));
            panel.Children.Add(SmallEvolutionPicture(pet,need==3));
            if(need<2){
                int xp=state.Xp[pet],oldBond=G.PetBond(pet);bool shortage=state.Berries<2;
                int requiredXp=new[]{0,20,55,110,190,300,450,650,900,1250}[level-1];
                SmallEvolutionProgress(panel,need==0?"成长 "+xp+" / "+requiredXp:"亲密 "+oldBond+" / "+bond,need==0?xp:oldBond,need==0?requiredXp:bond);
                panel.Children.Add(GentleText(need==0?"喂一口，成长 +12。":"喂一口，亲密 +1。",12,U.Muted));
                panel.Children.Add(GentleAction(state.Berries<2?"去看看莓果":"喂一口 · 莓果 2",()=>{
                    if(!current()||state.Xp[pet]!=xp||G.PetBond(pet)!=oldBond)return;
                    if(shortage)Go("莓果田");else CareForFocusedPet(pet,()=>G.Feed(pet));
                }));
            }else if(need==2){
                SmallEvolutionProgress(panel,"晶石 "+state.Crystals+" / "+cost,state.Crystals,cost);
                panel.Children.Add(GentleText("小溪初探通关，会带回 2 颗晶石。",12,U.Muted));
                panel.Children.Add(GentleAction("准备去小溪找晶石",()=>{if(current())OpenCrystalPreparation(pet,stage);}));
            }else{
                panel.Children.Add(GentleText("进化为「"+future+"」",16,U.Green));
                panel.Children.Add(GentleText("已有晶石 "+state.Crystals+" · 这次用 "+cost+" 颗",13,U.Green));
                panel.Children.Add(GentleText("进化会永久保留，等级和站位不变。",12,U.Muted));
                panel.Children.Add(GentleAction("进化 · 晶石 "+cost,()=>{
                    if(!current())return;bool cave=G.CaveUnlocked;string before=G.Name(pet);
                    if(pet==0?G.Evolve():G.EvolveSeed()){smallGrowthBefore=before;smallGrowthCost=cost;smallGrowthOpenedCave=!cave&&G.CaveUnlocked;smallGrowthStep=2;smallGrowthRevision++;resetScroll=true;}
                },pet==0?G.CanEvolve:G.CanEvolveSeed));
            }
        }
        HomeGuideSecondary(panel,"回去陪伙伴",()=>{if(current())Go("伙伴");});
        if(step==1){
            var details=new StackPanel();details.Children.Add(U.Text("等级 "+G.Level(pet)+" / "+level+" · 亲密 "+G.PetBond(pet)+" / "+bond+" · 晶石 "+state.Crystals+" / "+cost,12,U.Green));
            details.Children.Add(U.Text(pet==0?"农田生长速度 +15%；开场让全体敌人潮湿。":FarmGame.SeedChanges[stage+1]+" 攻击 +2，体力 +6。",12,U.Muted));
            if(!G.CaveUnlocked)details.Children.Add(U.Text("首次进化还会开放洞穴。",12,U.Muted));
            var expand=new Expander{Header="想知道更多，再展开",Content=details,Margin=new Thickness(0,4,0,0)};AutomationProperties.SetAutomationId(expand,"small-evolution-details");panel.Children.Add(expand);
        }
        int other=pet==0?4:0;
        if(step==0&&G.HasNextEvolution(other)){
            int otherStage=SmallEvolutionStage(other);Button another=null;
            another=Btn("也看看"+G.Name(other),()=>{if(current()&&SmallEvolutionStage(other)==otherStage&&G.HasNextEvolution(other)&&CanKeyboardReach(another))OpenSmallEvolution(other);});
            AutomationProperties.SetAutomationId(another,"evolution-other-companion");
            LifeDetails(panel,"也看看其他伙伴","small-evolution-others",another);
        }
    }
}
