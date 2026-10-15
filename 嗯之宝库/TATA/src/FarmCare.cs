using System;
using System.Linq;

public class CareTarget
{
    public int Pet=-1,Level=1,Stage;
    public bool Evolution;
}
public class CarePlan
{
    public int Pet,Level,Stage,RequiredBond,Feeds,Trainings,Berries,Grain,Crystals,AfterXp,AfterBond,AfterLevel;
    public bool Evolution,Done;
    public bool HasTraining{get{return Feeds>0||Trainings>0;}}
}
public partial class FarmGame
{
    private static readonly int[] CareThresholds={0,20,55,110,190,300,450,650,900,1250};
    public int PetStage(int pet){return pet==0?(S.Evolved?1:0):pet==4?S.SeedStage:0;}
    public int PetBond(int pet){return pet==0?S.Bond:pet==4?S.SeedBond:0;}
    public bool HasNextEvolution(int pet){return pet==0&&!S.Evolved||pet==4&&S.SeedStage<3;}
    public int PermanentHp(int pet){return MaxHp(pet)-R.Perks[6]*12-(R.Meal==1?12:0)-(HasPicnic(0)?8:0);}
    private void NormalizeCare()
    {
        var t=S.CareTarget;
        bool valid=t!=null&&t.Pet>=0&&t.Pet<PetCount&&t.Level>=1&&t.Level<=10;
        if(valid&&t.Evolution){valid=(t.Pet==0&&t.Stage==1&&t.Level==3)||(t.Pet==4&&t.Stage>=1&&t.Stage<=3&&t.Level==new[]{2,4,6}[t.Stage-1]);if(valid&&t.Stage>PetStage(t.Pet)+1)valid=false;}
        if(!valid)S.CareTarget=new CareTarget();
    }
    public bool SetCareTarget(int pet,bool evolution)
    {
        if(pet<0||pet>=PetCount||evolution&&!HasNextEvolution(pet))return false;
        int stage=evolution?PetStage(pet)+1:PetStage(pet);
        S.CareTarget=new CareTarget{Pet=pet,Evolution=evolution,Stage=stage,Level=evolution?(pet==0?3:new[]{2,4,6}[stage-1]):Math.Min(10,Level(pet)+1)};
        Save();return true;
    }
    public CarePlan CurrentCarePlan
    {
        get{
            var t=S.CareTarget;if(t==null||t.Pet<0||t.Pet>=PetCount)return null;
            int pet=t.Pet;var p=new CarePlan{Pet=pet,Level=t.Level,Stage=t.Stage,Evolution=t.Evolution,AfterXp=S.Xp[pet],AfterBond=PetBond(pet)};
            p.Done=t.Evolution?PetStage(pet)>=t.Stage:Level(pet)>=t.Level;
            if(t.Evolution){p.RequiredBond=pet==0?3:new[]{2,4,6}[t.Stage-1];p.Crystals=p.Done?0:pet==0?2:new[]{1,3,5}[t.Stage-1];}
            if(!p.Done){
                p.Feeds=t.Evolution?Math.Max(0,p.RequiredBond-p.AfterBond):0;p.Berries=p.Feeds*2;
                for(int i=0;i<p.Feeds;i++){if(p.AfterXp<1250)p.AfterXp+=12;p.AfterBond++;}
                p.Trainings=Math.Max(0,(CareThresholds[p.Level-1]-p.AfterXp+17)/18);p.Grain=p.Trainings*3;p.AfterXp+=p.Trainings*18;
            }
            p.AfterLevel=1;for(int i=1;i<CareThresholds.Length;i++)if(p.AfterXp>=CareThresholds[i])p.AfterLevel=i+1;return p;
        }
    }
    public bool ApplyCareTraining()
    {
        var p=CurrentCarePlan;if(p==null||p.Done||!p.HasTraining||!AtHome||S.Berries<p.Berries||S.Grain<p.Grain)return false;
        S.Berries-=p.Berries;S.Grain-=p.Grain;S.Xp[p.Pet]=p.AfterXp;if(p.Trainings>0)CompleteSmallActivity("train",p.Pet);if(p.Pet==0)S.Bond=p.AfterBond;if(p.Pet==4)S.SeedBond=p.AfterBond;
        S.Notice=Name(p.Pet)+"完成培养：喂养 "+p.Feeds+" 次，训练 "+p.Trainings+" 次；消耗莓果 "+p.Berries+"、麦穗 "+p.Grain+"。";Save();return true;
    }
    public bool CompleteCareEvolution()
    {
        var p=CurrentCarePlan;if(p==null||!p.Evolution||p.Done||p.Stage!=PetStage(p.Pet)+1)return false;
        return p.Pet==0?Evolve():p.Pet==4&&EvolveSeed();
    }
    public string CareTargetName(CarePlan p){return p.Evolution?"进化为"+(p.Pet==0?"潮汐兽":SeedNames[p.Stage]):"长到 Lv."+p.Level;}
    public double CareResourceWait(int kind,int needed)
    {
        if(needed<=0)return 0;if(kind<0||kind>2)return -1;
        var plots=Enumerable.Range(0,Plots).Where(i=>S.Crops[i]==kind&&(kind!=2||S.GardenUnlocked)).ToArray();if(plots.Length==0)return -1;
        double period=CropSeconds(kind),low=0,high=period*Math.Ceiling(needed/(double)CropYield(kind));
        for(int n=0;n<40;n++){double middle=(low+high)/2,total=plots.Sum(i=>Math.Floor((S.Growth[i]+middle)/period)*CropYield(kind));if(total>=needed)high=middle;else low=middle;}return high;
    }
}
