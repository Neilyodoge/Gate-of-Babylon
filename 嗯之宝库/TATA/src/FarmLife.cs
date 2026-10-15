using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmGame
{
    public int CourtyardCompanion{get{return S.CourtyardPet>=0&&S.CourtyardPet<PetCount?S.CourtyardPet:GuideCompanion;}}
    public bool ChooseCourtyardCompanion(int pet)
    {
        if(!AtHome||pet<0||pet>=PetCount)return false;
        S.CourtyardPet=pet;Save();return true;
    }
}

public partial class FarmPanel
{
    private string LifeCategory{get{return new[]{"伙伴册","照料","技能","点心"}.Contains(Tab)?"伙伴":new[]{"同行","便当","行囊","准备小卡","便当伙伴"}.Contains(Tab)?"冒险":"农场";}}
    private FarmSave lifeState;
    private string lifePage="",lifeReceipt="",plantingReturn="农场";
    private int lifeRevision,lifePet,lifeSlot,lifeSkill,lifeRelic,lifePlot,lifeCrop,lifeDecoration,lifeGardenSpot,lifeMemory,lifeChapter;
    private bool LifeTab{get{return new[]{"伙伴册","照料","技能","同行","便当","行囊","农田","布置","旅途记忆","成长手册","材料","点心","离开时的收成","准备小卡","便当伙伴"}.Contains(Tab);}}
    private void EnsureLife()
    {
        if(!Object.ReferenceEquals(lifeState,G.S)){lifeState=G.S;lifeRequestedCrop=-1;plantingBerryEvolution=false;lifePage="";lifePet=G.CourtyardCompanion;lifeSlot=lifeSkill=lifeRelic=lifePlot=lifeCrop=lifeGardenSpot=lifeMemory=0;lifeDecoration=-1;plantingReturn="农场";smallResource=null;smallResourcePlot=-1;lifeMintOpen=false;}
        if(lifePage!=Tab){lifePage=Tab;lifeReceipt="";lifeRevision++;}
    }
    private void OpenLife(string tab)
    {
        EnsureLife();ClearLifeCareResult();lifeReceipt="";lifeRevision++;
        if(tab=="伙伴册")lifePet=G.CourtyardCompanion;
        if(tab=="照料")lifeMintOpen=false;
        if(tab=="技能")lifeSkill=G.S.Skills[G.CourtyardCompanion];
        if(tab=="同行"){lifeSlot=0;lifePet=G.S.Team[0];}
        if(tab=="便当")picnicPreview=G.S.PlannedPicnic>=0?G.S.PlannedPicnic:0;
        if(tab=="农田"){lifeRequestedCrop=-1;plantingBerryEvolution=false;plantingReturn=Tab=="莓果田"?"莓果田":Tab=="照料"?"照料":Tab=="便当"?"便当":"农场";lifePlot=0;lifeCrop=G.S.Crops[0];}
        if(tab=="布置"){lifeGardenSpot=0;lifeDecoration=G.S.GardenDecorations[0];}
        if(tab=="旅途记忆")lifeMemory=0;
        if(tab=="成长手册")lifeChapter=Enumerable.Range(0,8).Where(i=>!G.S.ChapterClaimed[i]&&G.ChapterDone(i)).DefaultIfEmpty(Enumerable.Range(0,8).Where(i=>!G.S.ChapterClaimed[i]).DefaultIfEmpty(7).First()).First();
        Go(tab);resetScroll=true;
    }
    private Func<bool> LifeGuard()
    {
        var state=G.S;string tab=Tab;int visit=courtyardVisit,revision=lifeRevision;
        return ()=>CourtyardPage(state,tab,visit)&&lifeRevision==revision;
    }
    private void LifeChange(Action change){change();lifeRevision++;lifeReceipt="";resetScroll=true;}
    private void LifeReceipt(StackPanel panel)
    {
        if(lifeReceipt!="")panel.Children.Add(GentleText(lifeReceipt,12,U.Green));
    }
    private void LifeBack(StackPanel panel,string tab,string label,Func<bool> current)
    {
        HomeGuideSecondary(panel,label,()=>{if(current())Go(tab);});
    }
    private Expander LifeDetails(StackPanel panel,string title,string id,params UIElement[] content)
    {
        var inner=new StackPanel();foreach(var item in content)inner.Children.Add(item);
        var expand=new Expander{Header=title,Content=inner,Margin=new Thickness(0,3,0,0)};AutomationProperties.SetAutomationId(expand,id);panel.Children.Add(expand);return expand;
    }
    private bool LifeAway()
    {
        EnsureLife();if(G.AtHome)return false;var panel=GentleColumn();panel.Children.Add(GentleText(G.R.Phase=="result"?"伙伴带着收获回来啦":"伙伴回来后，再慢慢准备",21));panel.Children.Add(GentleText(G.R.Phase=="result"?"先看看这一趟带回了什么。":G.R.JourneyPaused?"旅程暂停着，农田还在生长。":"农田还在生长，这趟旅程照常继续。",13,U.Green));panel.Children.Add(GentleAction(G.R.Phase=="result"?"看看带回的收获":"看看当前旅程",()=>Go("冒险")));return true;
    }
    private void LifePage()
    {
        if(LifeAway())return;
        if(Tab=="便当伙伴")PicnicCompanionPage();else if(Tab=="准备小卡")PreparationCardsPage();else if(Tab=="离开时的收成")AwayHarvestPage();else if(Tab=="点心")LifeSnack();else if(Tab=="材料")SmallResourcesPage();else if(Tab=="伙伴册")LifeFriends();else if(Tab=="照料")LifeTraining();else if(Tab=="技能")LifeSkills();else if(Tab=="同行")LifeParty();else if(Tab=="便当")LifePicnic();else if(Tab=="行囊")LifeRelics();else if(Tab=="农田")LifePlanting();else if(Tab=="布置")LifeGarden();else if(Tab=="成长手册")LifeChapters();else LifeJournal();
    }
    private void LifeFriends()
    {
        int pet=lifePet;var current=LifeGuard();var panel=GentleColumn(440);
        panel.Children.Add(GentleText(G.Name(pet)+"  Lv."+G.Level(pet),21));panel.Children.Add(Portrait(pet,74));
        panel.Children.Add(GentleText(new[]{"用水泡帮助朋友。","用钳子留下灼烧。","接住潮湿，跳出闪电。","用治疗和护盾保护朋友。","让毒种慢慢生长。"}[pet],13,U.Green));
        panel.Children.Add(GentleAction("陪陪"+G.Name(pet),()=>{if(current()&&G.ChooseCourtyardCompanion(pet))Go("伙伴");}));
        U.Row(panel,Btn("上一位",()=>{if(current())LifeChange(()=>lifePet=(pet+FarmGame.PetCount-1)%FarmGame.PetCount);}),Btn("下一位",()=>{if(current())LifeChange(()=>lifePet=(pet+1)%FarmGame.PetCount);}));
        LifeBack(panel,"伙伴","返回伙伴",current);
        LifeDetails(panel,"桌宠和农田分工","life-friend-details",
            Btn(G.S.DisplayPet==pet?"已经是桌宠":"让它陪在桌面",()=>{if(current()){owner.SelectDisplayPet(pet);lifeReceipt="桌宠换好啦。";lifeRevision++;}},G.S.DisplayPet!=pet),
            U.Text(G.S.Team.Contains(pet)?"这位在出门队伍里，换下后才能留守。":G.Name(pet)+"留守时："+G.CaretakerBonus(pet),12,U.Muted),
            Btn(G.Caretaker==pet?"正在照料农田":"请它照料农田",()=>{if(current()&&G.SetCaretaker(pet)){lifeReceipt="农田交给它啦。";lifeRevision++;}},!G.S.Team.Contains(pet)&&G.Caretaker!=pet));
        LifeReceipt(panel);
    }
    private void LifeTraining()
    {
        int pet=G.CourtyardCompanion,xp=G.S.Xp[pet];if(focusedPet!=pet)SelectFocusedPet(pet);if(ShowLifeCareResult())return;
        var current=LifeGuard();bool grown=G.Level(pet)>=10,skillGoal=HasSkillCare;var panel=GentleColumn(440);
        panel.Children.Add(GentleText("陪"+G.Name(pet)+"练一会儿",21));panel.Children.Add(Portrait(pet,54));panel.Children.Add(GrowthCard(pet,true,false));
        var stock=Live(()=>grown?"等级已经长满啦。":"麦穗 "+G.S.Grain+" · 练一次用 3，成长 +18",12,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        var train=GentleAction("",()=>{
            if(!current()||G.S.Xp[pet]!=xp||G.CourtyardCompanion!=pet)return;
            if(grown){if(skillGoal)ReturnSkillCare();else Go("伙伴");}else if(G.S.Grain<3)OpenSmallResources("train",pet);else ApplyLifeCare(pet,"train");
        });panel.Children.Add(train);updates.Add(()=>train.Content=grown?(skillGoal?"回去看这招":"回去陪伙伴"):G.S.Grain<3?"去看看麦穗田":"练一次 · 麦穗 3");
        if(!grown){if(skillGoal)HomeGuideSecondary(panel,"回去看这招",()=>{if(current())ReturnSkillCare();});else LifeBack(panel,"伙伴","返回伙伴",current);}
        if(!grown&&G.S.GardenUnlocked){
            var snack=Btn("",()=>{if(current()&&G.S.Xp[pet]==xp&&G.CourtyardCompanion==pet){if(G.S.Mint<1)OpenSmallResources("mint",pet);else ApplyLifeCare(pet,"mint");}});updates.Add(()=>snack.Content=G.S.Mint<1?"去看看薄荷田":"吃点心 · 薄荷 1");
            var mint=LifeDetails(panel,"也可以尝尝薄荷点心","life-mint-details",Live(()=>"已有薄荷 "+G.S.Mint+" · 吃一份用 1，成长 +24。",12,U.Muted),snack);
            mint.IsExpanded=lifeMintOpen;mint.Expanded+=(s,e)=>lifeMintOpen=true;mint.Collapsed+=(s,e)=>lifeMintOpen=false;
        }
    }
    private void LifeSkills()
    {
        int pet=G.CourtyardCompanion,skill=lifeSkill,old=G.S.Skills[pet];var current=LifeGuard();bool unlocked=G.SkillUnlocked(pet,skill);var panel=GentleColumn(440);
        panel.Children.Add(GentleText(G.Name(pet)+"的小本领",21));panel.Children.Add(Portrait(pet,62));
        panel.Children.Add(GentleText(FarmGame.SkillNames[pet][skill],17,U.Green));panel.Children.Add(GentleText(FarmGame.SkillDetails[pet][skill],13));
        panel.Children.Add(GentleText(!unlocked?"成长 "+G.S.Xp[pet]+" / 20 · 到 Lv.2 就学会。":old==skill?"这次出门会用这一招。":"已经学会 · 免费换上，下趟出门生效。",12,U.Muted));
        if(!unlocked){var progress=U.Bar(Math.Min(G.S.Xp[pet],20),20);progress.Height=9;progress.MaxWidth=300;progress.Margin=new Thickness(0,0,0,8);AutomationProperties.SetAutomationId(progress,"skill-learning-progress");AutomationProperties.SetName(progress,G.Name(pet)+"学会「"+FarmGame.SkillNames[pet][skill]+"」的成长进度");panel.Children.Add(progress);}
        panel.Children.Add(GentleAction(!unlocked?"先陪它长大":old==skill?"回去陪伙伴":"换上这一招",()=>{
            if(!current()||G.S.Skills[pet]!=old||G.CourtyardCompanion!=pet||G.SkillUnlocked(pet,skill)!=unlocked)return;
            if(!unlocked)OpenSkillCare(pet,skill);else if(old==skill)Go("伙伴");else if(G.SetSkill(pet,skill)){lifeRevision++;}
        }));
        var alternatives=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(alternatives);
        alternatives.Children.Add(Btn("看看另一招",()=>{if(current())LifeChange(()=>lifeSkill=1-skill);}));
        if(!unlocked||old!=skill)alternatives.Children.Add(Btn("返回伙伴",()=>{if(current())Go("伙伴");}));
    }
    private void LifeParty()
    {
        int slot=lifeSlot,pet=lifePet,old=G.S.Team[slot];string team=String.Join(",",G.S.Team);var current=LifeGuard();var panel=GentleColumn(450);
        panel.Children.Add(GentleText("第 "+(slot+1)+" 位 · "+(slot==0?"前排":"后排"),21));panel.Children.Add(Portrait(pet,66));panel.Children.Add(GentleText(G.Name(pet)+"  Lv."+G.Level(pet),16,U.Green));
        panel.Children.Add(GentleText(pet==old?"它已经站在这里啦。":"这里现在是"+G.Name(old)+(G.S.Team.Contains(pet)?"，确认后互换。":"，确认后让它回家休息。"),12,U.Muted));
        panel.Children.Add(GentleAction(pet==old?"回出发准备":"让"+G.Name(pet)+"站这里",()=>{
            if(!current()||String.Join(",",G.S.Team)!=team)return;
            if(pet==old)Go("冒险");else if(G.AssignPartySlot(slot,pet)){lifeReceipt="位置安排好啦。";lifeRevision++;}
        }));
        U.Row(panel,Btn("换一位看看",()=>{if(current())LifeChange(()=>lifePet=(pet+1)%FarmGame.PetCount);}),Btn("看下一个位置",()=>{if(current())LifeChange(()=>{lifeSlot=(slot+1)%3;lifePet=G.S.Team[lifeSlot];});}));
        if(pet!=old)LifeBack(panel,"冒险","返回出发准备",current);
        LifeDetails(panel,"看看三位怎么配合","life-party-details",U.Text(String.Join(" → ",G.S.Team.Select(G.Name).ToArray()),13,U.Green),U.Text(G.PartyAdvice(selectedBlessing),12,U.Muted));
    }
}


