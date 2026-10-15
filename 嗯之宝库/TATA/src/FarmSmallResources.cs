using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public class FarmSmallReminder
{
    public string Activity="";
    public int Pet=-1,Recipe=-1,Region,Depth;
    public FarmSmallReminder Copy(){return (FarmSmallReminder)MemberwiseClone();}
}

public partial class FarmGame
{
    public bool ValidSmallReminder(FarmSmallReminder item)
    {
        if(item==null)return false;
        if(item.Activity=="picnic")return item.Recipe>=0&&item.Recipe<PicnicCount&&PicnicUnlocked(item.Recipe)&&ExpeditionUnlocked(item.Region,item.Depth);
        return (item.Activity=="train"||item.Activity=="mint"&&S.GardenUnlocked)&&item.Pet>=0&&item.Pet<PetCount&&Level(item.Pet)<10;
    }
    private void NormalizeSmallReminder(){if(!ValidSmallReminder(S.SmallReminder))S.SmallReminder=null;}
    public int[] SmallResourceNeeds(FarmSmallReminder item){return !ValidSmallReminder(item)?new int[3]:item.Activity=="picnic"?PicnicCost(item.Recipe):item.Activity=="train"?new[]{0,3,0}:new[]{0,0,1};}
    public bool SmallResourcesReady(FarmSmallReminder item){var needs=SmallResourceNeeds(item);return ValidSmallReminder(item)&&Enumerable.Range(0,3).All(k=>CropStock(k)>=needs[k]);}
    public string SmallReminderName(FarmSmallReminder item){return item.Activity=="picnic"?"准备"+PicnicName(item.Recipe):item.Activity=="train"?"陪"+Name(item.Pet)+"练一会儿":"给"+Name(item.Pet)+"尝薄荷点心";}
    public bool RememberSmallActivity(FarmSmallReminder item){if(!AtHome||!ValidSmallReminder(item))return false;S.SmallReminder=item.Copy();Save();return true;}
    public bool ForgetSmallActivity(){if(S.SmallReminder==null)return false;S.SmallReminder=null;Save();return true;}
    private void CompleteSmallActivity(string activity,int subject)
    {
        var item=S.SmallReminder;if(item!=null&&item.Activity==activity&&(activity=="picnic"?item.Recipe:item.Pet)==subject)S.SmallReminder=null;
    }
    public int ResourcePlot(int kind){return Enumerable.Range(0,Plots).Where(i=>S.Crops[i]==kind).OrderBy(i=>SecondsLeft(i)).DefaultIfEmpty(-1).First();}
}

public partial class FarmPanel
{
    private FarmSmallReminder smallResource;
    private int smallResourceKind,smallResourcePlot=-1;
    private bool lifeMintOpen;
    private void OpenSmallResources(string activity,int subject)
    {
        OpenSmallResources(new FarmSmallReminder{Activity=activity,Pet=activity=="picnic"?-1:subject,Recipe=activity=="picnic"?subject:-1,Region=journeyDifficulty,Depth=journeyDepth});
    }
    private void OpenSmallResources(FarmSmallReminder item)
    {
        EnsureLife();if(!G.AtHome||!G.ValidSmallReminder(item))return;
        smallResource=item.Copy();var needs=G.SmallResourceNeeds(item);
        smallResourceKind=Enumerable.Range(0,3).Where(k=>needs[k]>G.CropStock(k)).DefaultIfEmpty(Enumerable.Range(0,3).First(k=>needs[k]>0)).First();
        smallResourcePlot=G.ResourcePlot(smallResourceKind);lifeRevision++;Go("材料");resetScroll=true;
    }
    private void ReturnSmallActivity(FarmSmallReminder item)
    {
        if(item.Activity=="picnic"){picnicPreview=item.Recipe;journeyDifficulty=item.Region;journeyDepth=item.Depth;Go("便当");}
        else{if(G.CourtyardCompanion!=item.Pet)G.ChooseCourtyardCompanion(item.Pet);lifeMintOpen=false;Go(item.Activity=="mint"?"点心":"照料");}
        lifeRevision++;resetScroll=true;
    }
    private void OpenResourcePlanting(int kind)
    {
        OpenCropRecovery(kind,"材料");
    }
    private bool SameSmallActivity(FarmSmallReminder a,FarmSmallReminder b){return a!=null&&b!=null&&a.Activity==b.Activity&&a.Pet==b.Pet&&a.Recipe==b.Recipe&&a.Region==b.Region&&a.Depth==b.Depth;}
    private void SmallResourcesPage()
    {
        if(!G.ValidSmallReminder(smallResource)){var empty=GentleColumn();empty.Children.Add(GentleText("先选一件想做的小事",21));empty.Children.Add(GentleAction("回小院",()=>Go("农场")));return;}
        var item=smallResource;int kind=smallResourceKind;var needs=G.SmallResourceNeeds(item);var current=LifeGuard();var panel=GentleColumn(450);
        Func<int> plot=()=>{if(smallResourcePlot<0||smallResourcePlot>=G.Plots||G.S.Crops[smallResourcePlot]!=kind)smallResourcePlot=G.ResourcePlot(kind);return smallResourcePlot;};
        Func<bool> enough=()=>G.CropStock(kind)>=needs[kind];
        var title=Live(()=>enough()?FarmGame.CropNames[kind]+"够啦":plot()<0?"还没种"+FarmGame.CropNames[kind]:"等这一批"+FarmGame.CropNames[kind],21);title.TextAlignment=TextAlignment.Center;title.Margin=new Thickness(0,0,0,3);panel.Children.Add(title);
        var picture=new Grid{Height=92,Margin=new Thickness(0,0,0,4)};panel.Children.Add(picture);
        var scene=Scene();scene.FocusedFarmPet=item.Pet>=0?item.Pet:G.CourtyardCompanion;scene.Height=scene.MinHeight=92;scene.Margin=new Thickness(0);picture.Children.Add(scene);
        var crop=new FarmGlyph{Kind=new[]{"berry","grain","mint"}[kind],Width=60,Height=60,HorizontalAlignment=HorizontalAlignment.Center};picture.Children.Add(crop);
        var stock=Live(()=>FarmGame.CropNames[kind]+" "+G.CropStock(kind)+" / "+needs[kind]+" · "+(enough()?"这一样够了":"还差 "+(needs[kind]-G.CropStock(kind))),14,U.Green);stock.TextAlignment=TextAlignment.Center;stock.Margin=new Thickness(0,0,0,3);panel.Children.Add(stock);
        var progress=new StackPanel{MaxWidth=330,Margin=new Thickness(0,0,0,4)};panel.Children.Add(progress);var label=GentleText("",12,U.Muted);progress.Children.Add(label);
        var bar=U.Bar(0,100);bar.Height=9;bar.Margin=new Thickness(0);AutomationProperties.SetAutomationId(bar,"small-resource-progress");progress.Children.Add(bar);
        var hint=Live(()=>enough()?"回去后，再确认要不要用。":plot()<0?"先选一块田，确认改种才会重新计时。":"成熟会自动收好，可以先去做别的。",12,U.Muted);hint.TextAlignment=TextAlignment.Center;hint.Margin=new Thickness(0,0,0,2);panel.Children.Add(hint);
        var action=GentleAction("",()=>{
            if(!current())return;
            if(enough()&&!G.SmallResourcesReady(item)){LifeChange(()=>{smallResourceKind=Enumerable.Range(0,3).First(k=>G.CropStock(k)<needs[k]);smallResourcePlot=G.ResourcePlot(smallResourceKind);});}
            else if(enough()||plot()>=0)ReturnSmallActivity(item);
            else OpenResourcePlanting(kind);
        });action.Margin=new Thickness(0,5,0,3);panel.Children.Add(action);
        var priorReminder=G.S.SmallReminder;
        HomeGuideSecondary(panel,priorReminder!=null&&!SameSmallActivity(priorReminder,item)?"改记这件事，回小院":"回小院，记着这件事",()=>{if(current()&&Object.ReferenceEquals(priorReminder,G.S.SmallReminder)&&G.RememberSmallActivity(item))Go("农场");});
        LifeDetails(panel,"这份材料用来做什么","small-resource-details",U.Text(G.SmallReminderName(item),13,U.Green),
            Btn("先返回，不记提醒",()=>{if(current())ReturnSmallActivity(item);}),
            Btn("不再记这件事",()=>{if(current()&&Object.ReferenceEquals(priorReminder,G.S.SmallReminder)&&SameSmallActivity(priorReminder,item)){G.ForgetSmallActivity();Go("农场");}},SameSmallActivity(priorReminder,item)));
        updates.Add(()=>{
            int index=plot();scene.LearningPlot=index;scene.ShowBerryBasket=kind==0&&index>=0;scene.Visibility=index>=0?Visibility.Visible:Visibility.Collapsed;crop.Visibility=index<0?Visibility.Visible:Visibility.Collapsed;
            progress.Visibility=index>=0&&!enough()?Visibility.Visible:Visibility.Collapsed;
            if(index>=0){bar.Value=100*Math.Min(1,G.S.Growth[index]/G.CropSeconds(kind));label.Text="第 "+(index+1)+" 块田 · 约 "+G.SecondsLeft(index)+" 秒后收好 "+G.CropYield(kind)+" 份";AutomationProperties.SetName(bar,FarmGame.CropNames[kind]+"生长进度");AutomationProperties.SetHelpText(bar,label.Text);scene.InvalidateVisual();}
            action.Content=enough()&&!G.SmallResourcesReady(item)?"看看下一样缺的材料":!enough()&&index<0?"选一块田种"+FarmGame.CropNames[kind]:item.Activity=="picnic"?"回去看看这份便当":!enough()?"先回去陪"+G.Name(item.Pet):item.Activity=="mint"?"回去看看薄荷点心":"回去陪"+G.Name(item.Pet)+"练一会儿";
        });
    }
    private void LifeSnack()
    {
        int pet=G.CourtyardCompanion,xp=G.S.Xp[pet];if(focusedPet!=pet)SelectFocusedPet(pet);if(ShowLifeCareResult())return;
        var current=LifeGuard();bool grown=G.Level(pet)>=10,unlocked=G.S.GardenUnlocked,skillGoal=HasSkillCare;var panel=GentleColumn(440);
        panel.Children.Add(GentleText("给"+G.Name(pet)+"尝薄荷点心",21));panel.Children.Add(Portrait(pet,54));panel.Children.Add(GrowthCard(pet,true,false));
        var stock=Live(()=>grown?"等级已经长满啦。":!unlocked?"第一次通关小溪后，就能种薄荷啦。":"已有薄荷 "+G.S.Mint+" · 吃一份用 1，成长 +24",12,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        var eat=GentleAction("",()=>{
            if(!current()||G.S.Xp[pet]!=xp||G.CourtyardCompanion!=pet)return;
            if(grown||!unlocked){if(skillGoal)ReturnSkillCare();else Go("伙伴");}else if(G.S.Mint<1)OpenSmallResources("mint",pet);else ApplyLifeCare(pet,"mint");
        });panel.Children.Add(eat);updates.Add(()=>eat.Content=grown||!unlocked?(skillGoal?"回去看这招":"回去陪伙伴"):G.S.Mint<1?"去看看薄荷田":"吃一份点心 · 薄荷 1");
        if(!grown&&unlocked){if(skillGoal)HomeGuideSecondary(panel,"回去看这招",()=>{if(current())ReturnSkillCare();});else LifeBack(panel,"伙伴","先不吃，回去陪伙伴",current);}
    }
}
