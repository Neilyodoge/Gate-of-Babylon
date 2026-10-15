using System;
using System.Windows;
using System.Windows.Controls;

public partial class FarmGame
{
    public bool ValidCrystalTarget(int pet,int stage)
    {
        return (pet==0||pet==4)&&stage>=0&&stage==(pet==0?(S.Evolved?1:0):S.SeedStage)&&HasNextEvolution(pet);
    }
    public bool HasCrystalTarget(FarmRun run){return run!=null&&ValidCrystalTarget(run.CrystalTargetPet,run.CrystalTargetStage);}
}

public partial class FarmPanel
{
    private FarmSave crystalPreparationState;
    private int crystalPreparationPet=-1,crystalPreparationStage=-1;
    private bool HasCrystalPreparation{get{return SimpleCourtyard&&G.AtHome&&Object.ReferenceEquals(crystalPreparationState,G.S)&&G.ValidCrystalTarget(crystalPreparationPet,crystalPreparationStage);}}
    private void ClearCrystalPreparation(){crystalPreparationState=null;crystalPreparationPet=crystalPreparationStage=-1;}
    private void OpenCrystalPreparation(int pet,int stage)
    {
        if(!SimpleCourtyard||!G.AtHome||!G.ValidCrystalTarget(pet,stage))return;
        crystalPreparationState=G.S;crystalPreparationPet=pet;crystalPreparationStage=stage;
        journeyDifficulty=journeyDepth=0;Go("冒险");resetScroll=true;
    }
    private void ResumeSmallEvolution(int pet)
    {
        OpenSmallEvolution(pet);smallGrowthStep=1;smallGrowthRevision++;resetScroll=true;
    }
    private bool CancelCrystalPreparation()
    {
        if(!HasCrystalPreparation)return false;
        int pet=crystalPreparationPet;ClearCrystalPreparation();ResumeSmallEvolution(pet);
        pageHistory.RemoveAll(t=>t=="冒险");return true;
    }
    private bool CrystalJourneyReturn(FarmRun run)
    {
        if(!G.HasCrystalTarget(run))return false;
        int pet=run.CrystalTargetPet,stage=run.CrystalTargetStage,cost=pet==0?2:G.SeedEvolutionCost;
        int visit=courtyardVisit;var state=G.S;
        Func<bool> current=()=>Object.ReferenceEquals(state,G.S)&&SimpleCourtyard&&Tab=="冒险"&&courtyardVisit==visit&&StillViewingResult(run)&&G.ValidCrystalTarget(pet,stage);
        var panel=GentleColumn(440);
        panel.Children.Add(GentleText(run.RewardCrystals>0?"给"+G.Name(pet)+"带回晶石啦":"这次先回来歇一会儿",21));
        panel.Children.Add(Portrait(pet,56));
        panel.Children.Add(RewardTile("crystal","晶石",run.RewardCrystals,"这趟已经收好"));
        panel.Children.Add(GentleText("已有 "+state.Crystals+" / "+cost+" 颗 · "+(state.Crystals>=cost?"返家后再决定进化":"还差 "+(cost-state.Crystals)+" 颗，下次再来"),12,U.Green));
        var resume=GentleAction("返家 · 接着看进化",()=>{if(current()&&G.Home())ResumeSmallEvolution(pet);});resume.MinWidth=0;resume.MinHeight=44;resume.HorizontalAlignment=HorizontalAlignment.Stretch;
        U.Row(panel,resume,Btn("先回小院",()=>{if(current())ReturnFromReceipt(run,"农场");}));
        panel.Children.Add(GuideReturnExtras(run,true));return true;
    }
}
