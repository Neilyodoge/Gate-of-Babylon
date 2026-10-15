using System;
using System.Linq;

public partial class FarmPanel
{
    private int lifeRequestedCrop=-1;
    private bool plantingBerryEvolution;
    private void OpenCropRecovery(int kind,string returnTab)
    {
        if(!SimpleCourtyard||!G.AtHome||kind<0||kind>(G.S.GardenUnlocked?2:1))return;
        bool evolution=returnTab=="莓果田"&&berryForEvolution&&Object.ReferenceEquals(smallGrowthState,G.S);
        OpenLife("农田");plantingReturn=returnTab;lifeRequestedCrop=lifeCrop=kind;plantingBerryEvolution=evolution;
        lifePlot=Enumerable.Range(0,G.Plots).OrderBy(i=>G.S.Growth[i]/G.CropSeconds(G.S.Crops[i])).First();
    }
    private void ReturnLifePlanting()
    {
        string target=plantingReturn;bool evolution=plantingBerryEvolution;
        int plot=lifePlot,kind=lifeRequestedCrop;
        if(kind>=0&&plot>=0&&plot<G.Plots&&G.S.Crops[plot]==kind){
            if(target=="材料")smallResourcePlot=plot;
            if(target=="莓果田"){lessonPlotState=G.S;lessonPlot=plot;}
        }
        Go(target);
        if(target=="莓果田")berryForEvolution=evolution&&Object.ReferenceEquals(smallGrowthState,G.S);
        pageHistory.RemoveAll(t=>t=="农田");resetScroll=true;
    }
}
