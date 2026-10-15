using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmGame
{
    public int ChapterGrowthPet{get{return Enumerable.Range(0,PetCount).OrderByDescending(p=>S.Xp[p]).ThenBy(p=>p==CourtyardCompanion?0:1).First();}}
    public int ChapterGuideTarget(int chapter){return chapter==1?20:chapter==6?2:chapter==7?6:1;}
    public int ChapterGuideValue(int chapter)
    {
        int value;
        switch(chapter){
            case 0:value=S.Harvests;break;case 1:value=S.Xp.Max();break;case 2:value=S.ExpeditionClears[0];break;
            case 3:value=S.SoilLevel;break;case 4:value=CaveUnlocked?1:0;break;case 5:value=S.RelicVictories;break;
            case 6:value=S.SeedStage;break;case 7:value=S.ExpeditionClears.Count(x=>x>0);break;default:return 0;
        }
        return Math.Max(0,Math.Min(ChapterGuideTarget(chapter),value));
    }
    public string ChapterGuideProgress(int chapter)
    {
        int value=ChapterGuideValue(chapter);
        switch(chapter){
            case 0:return "收成 "+value+" / 1 次";
            case 1:return ChapterDone(chapter)?Name(ChapterGrowthPet)+" Lv."+Level(ChapterGrowthPet)+" · 已达到目标":Name(ChapterGrowthPet)+"成长 "+value+" / 20 · 到 Lv.2";
            case 2:return "小溪初探通关 "+value+" / 1 次";
            case 3:return "土壤 Lv."+S.SoilLevel+(ChapterDone(chapter)?" · 已达到 Lv.1":" / 1");
            case 4:return "首次进化 "+value+" / 1 次";
            case 5:return "带信物获胜 "+value+" / 1 次";
            case 6:return "当前第 "+(S.SeedStage+1)+" 形态 · 目标第 3 形态";
            default:return "走过 "+value+" / 6 段旅途";
        }
    }
}

public partial class FarmPanel
{
    private int ChapterNextRoute(){return Enumerable.Range(0,6).Where(i=>G.S.ExpeditionClears[i]==0&&G.ExpeditionUnlocked(i/3,i%3)).DefaultIfEmpty(-1).First();}
    private int ChapterEvolutionPet(){return G.CourtyardCompanion==4?4:0;}
    private string ChapterStepLabel(int chapter)
    {
        switch(chapter){
            case 0:return "去看看农田";
            case 1:return "先看看"+G.Name(G.ChapterGrowthPet);
            case 2:return "先看看小溪初探";
            case 3:return "看看怎么施肥";
            case 4:return "先看看"+G.Name(ChapterEvolutionPet())+"进化";
            case 5:return G.S.EquippedRelics.Any(i=>i>=0)?"准备带信物去小溪":Enumerable.Range(0,6).Any(G.RelicOwned)?"先看看带哪件信物":"先去小溪找信物";
            case 6:return "先看看种子的下一形态";
            default:int route=ChapterNextRoute();return route<0?"先看看伙伴进化":"看看"+FarmGame.ExpeditionName(route/3,route%3);
        }
    }
    private void OpenChapterRoute(int route){OpenSmallDestination(route/3,route%3);smallDestinationStep=1;}
    private void OpenChapterStep(int chapter)
    {
        BeginChapterReturn(chapter);
        switch(chapter){
            case 0:OpenLife("农田");plantingReturn="成长手册";break;
            case 1:OpenLife("伙伴册");lifePet=G.ChapterGrowthPet;break;
            case 2:OpenChapterRoute(0);break;
            case 3:Go("施肥");break;
            case 4:OpenSmallEvolution(ChapterEvolutionPet());break;
            case 5:if(G.S.EquippedRelics.All(i=>i<0)&&Enumerable.Range(0,6).Any(G.RelicOwned))OpenLife("行囊");else OpenChapterRoute(0);break;
            case 6:OpenSmallEvolution(4);break;
            case 7:int route=ChapterNextRoute();if(route<0)OpenSmallEvolution(ChapterEvolutionPet());else OpenChapterRoute(route);break;
        }
    }
    private int[] ChapterStocks(){return new[]{G.S.Berries,G.S.Grain,G.S.Mint,G.S.Fertilizer,G.S.Crystals};}
    private int[] ChapterReceivable(int chapter)
    {
        var stocks=ChapterStocks();return FarmGame.ChapterRewards[chapter].Select((amount,i)=>Math.Min(amount,99999-stocks[i])).ToArray();
    }
    private string ChapterAmounts(int[] amounts)
    {
        string[] names={"莓果","麦穗","薄荷","肥料","晶石"};return String.Join(" · ",amounts.Select((n,i)=>n>0?names[i]+" +"+n:"").Where(s=>s!=""));
    }
    private void ChapterGuidePage()
    {
        int chapter=lifeChapter;var state=G.S;var current=LifeGuard();bool claimed=state.ChapterClaimed[chapter],shownReady=G.ChapterDone(chapter);string shownStep=ChapterStepLabel(chapter);
        var panel=GentleColumn(440);panel.Children.Add(GentleText(FarmGame.ChapterNames[chapter],21));
        if(chapter==1||chapter==4||chapter==6)panel.Children.Add(Portrait(chapter==6?4:chapter==4?ChapterEvolutionPet():G.ChapterGrowthPet,52));
        else panel.Children.Add(new FarmGlyph{Kind="farm",Width=52,Height=52,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(GentleText(claimed?"这份成长礼物已经收好啦。":FarmGame.ChapterDetails[chapter],13,U.Green));
        var progress=Live(()=>G.ChapterGuideProgress(chapter),12,U.Green);progress.TextAlignment=TextAlignment.Center;progress.Margin=new Thickness(0,4,0,3);panel.Children.Add(progress);
        var bar=U.Bar(0,G.ChapterGuideTarget(chapter));bar.Height=9;bar.MaxWidth=300;bar.Margin=new Thickness(0,0,0,7);AutomationProperties.SetAutomationId(bar,"chapter-guide-progress");panel.Children.Add(bar);
        updates.Add(()=>{bar.Value=G.ChapterGuideValue(chapter);AutomationProperties.SetName(bar,FarmGame.ChapterNames[chapter]+"："+G.ChapterGuideProgress(chapter));});
        var reward=Live(()=>{
            if(claimed)return lifeReceipt;
            var available=ChapterReceivable(chapter);bool full=available.SequenceEqual(FarmGame.ChapterRewards[chapter]);
            return full?"礼物："+FarmGame.ChapterRewardText(chapter):available.Sum()==0?"仓库已满，领取不会增加材料。":"仓库快满，这次能收下："+ChapterAmounts(available);
        },12,U.Muted);reward.TextAlignment=TextAlignment.Center;reward.Margin=new Thickness(0,3,0,6);panel.Children.Add(reward);
        updates.Add(()=>reward.Visibility=claimed&&lifeReceipt==""?Visibility.Collapsed:Visibility.Visible);
        var action=GentleAction("",()=>{
            if(!current()||state.ChapterClaimed[chapter]!=claimed||G.ChapterDone(chapter)!=shownReady||ChapterStepLabel(chapter)!=shownStep)return;
            if(claimed){Go("农场");return;}
            if(!shownReady){OpenChapterStep(chapter);return;}
            int[] before=ChapterStocks();if(G.ClaimChapter(chapter)){int[] after=ChapterStocks();string received=ChapterAmounts(after.Select((n,i)=>n-before[i]).ToArray());lifeReceipt=received==""?"仓库已满，礼物已记为收好。":"这次收好："+received;lifeRevision++;}
        });panel.Children.Add(action);
        updates.Add(()=>{shownReady=G.ChapterDone(chapter);shownStep=ChapterStepLabel(chapter);action.Content=claimed?"回小院":shownReady?"收好这份成长礼物":shownStep;});
        var page=GentleText((chapter+1)+" / 8 页",11,U.Muted);page.VerticalAlignment=VerticalAlignment.Center;page.Margin=new Thickness(7,0,7,0);
        U.Row(panel,Btn("前一页",()=>{if(current())LifeChange(()=>lifeChapter=chapter-1);},chapter>0),page,Btn("后一页",()=>{if(current())LifeChange(()=>lifeChapter=chapter+1);},chapter<7));
        if(!claimed)LifeBack(panel,"农场","先回小院",current);
    }
}
