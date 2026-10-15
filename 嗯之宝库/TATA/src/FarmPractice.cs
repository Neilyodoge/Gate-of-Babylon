using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;

public class PracticeDraft
{
    public int Picnic=-1;
    public int[] Team,Skills;
    public PracticeDraft Copy(){return new PracticeDraft{Picnic=Picnic,Team=(int[])Team.Clone(),Skills=(int[])Skills.Clone()};}
}
public class PracticeSide
{
    public BattleNote[] Rounds;
    public string[] Logs;
    public BattleNote Final{get{return Rounds.Last();}}
}
public class PracticeComparison
{
    public int Region,Depth,Blessing;
    public string SourceKey;
    public PracticeDraft Draft;
    public PracticeSide Current,Trial;
}
public partial class FarmGame
{
    private readonly bool practiceOnly;
    private FarmGame(FarmSave state){practiceOnly=true;SavePath=null;S=state;Validate();}
    private FarmGame PracticeCopy()
    {
        var serializer=new XmlSerializer(typeof(FarmSave));
        using(var buffer=new MemoryStream()){
            serializer.Serialize(buffer,S);buffer.Position=0;var state=(FarmSave)serializer.Deserialize(buffer);
            state.Run=new FarmRun();return new FarmGame(state);
        }
    }
    public PracticeDraft CurrentPracticeDraft(){return new PracticeDraft{Picnic=S.PlannedPicnic,Team=(int[])S.Team.Clone(),Skills=(int[])S.Skills.Clone()};}
    public bool ValidPracticeDraft(PracticeDraft draft)
    {
        return draft!=null&&PicnicUnlocked(draft.Picnic)&&draft.Team!=null&&draft.Team.Length==3&&draft.Team.Distinct().Count()==3&&draft.Team.All(p=>p>=0&&p<PetCount)&&draft.Skills!=null&&draft.Skills.Length==PetCount&&draft.Skills.All(s=>s>=0&&s<=1)&&draft.Team.All(p=>SkillUnlocked(p,draft.Skills[p]));
    }
    public PracticeDraft PracticePreset(int preset)
    {
        if(preset<0||preset>4)return null;var draft=CurrentPracticeDraft();
        draft.Team=new[]{new[]{0,2,3},new[]{3,0,1},new[]{1,2,3},new[]{0,4,3},new[]{1,4,3}}[preset];
        foreach(int pet in draft.Team)draft.Skills[pet]=0;
        if(preset==1){draft.Skills[0]=1;draft.Skills[3]=1;}
        if(preset==2)draft.Skills[2]=1;if(preset==4)draft.Skills[4]=1;
        return ValidPracticeDraft(draft)?draft:null;
    }
    public int PracticeCaretaker(PracticeDraft draft){return !draft.Team.Contains(Caretaker)?Caretaker:Enumerable.Range(0,PetCount).First(p=>!draft.Team.Contains(p));}
    public string PracticeKey()
    {
        return String.Join("|",new[]{String.Join(",",S.Xp),String.Join(",",S.Team),String.Join(",",S.Skills),S.Evolved.ToString(),S.SeedStage.ToString(),String.Join(",",S.EquippedRelics),String.Join(",",S.ExpeditionClears),String.Join(",",S.BlessingOffers),Caretaker.ToString(),S.PlannedPicnic.ToString(),S.GardenUnlocked.ToString()});
    }
    private PracticeSide RunPractice(PracticeDraft draft,int region,int depth,int blessing)
    {
        var game=PracticeCopy();game.S.Team=(int[])draft.Team.Clone();game.S.Skills=(int[])draft.Skills.Clone();game.S.PlannedPicnic=draft.Picnic;
        if(!game.BeginJourney(region,blessing,depth)||!game.RouteChoice(0))throw new InvalidOperationException("这套试练条件已变化，请重新准备。");
        var rounds=new List<BattleNote>();var logs=new List<string>();
        for(int turn=0;turn<3&&game.R.Phase=="battle";turn++){
            game.Step();rounds.Add(game.CurrentBattleNotes().Last());logs.Add(String.Join("\n",game.R.Log.Split('\n').Where(line=>!line.Contains("入库")&&!line.Contains("肥料")&&!line.Contains("资源保留")&&!line.Contains("旅途灵感")).ToArray()));
        }
        return new PracticeSide{Rounds=rounds.ToArray(),Logs=logs.ToArray()};
    }
    public PracticeComparison Practice(PracticeDraft draft,int region,int depth,int blessing)
    {
        if(!AtHome||!ValidPracticeDraft(draft)||!ExpeditionUnlocked(region,depth)||!S.BlessingOffers.Contains(blessing))return null;
        var frozen=draft.Copy();return new PracticeComparison{Region=region,Depth=depth,Blessing=blessing,SourceKey=PracticeKey(),Draft=frozen,Current=RunPractice(CurrentPracticeDraft(),region,depth,blessing),Trial=RunPractice(frozen,region,depth,blessing)};
    }
    public bool PracticeIsCurrent(PracticeComparison result){return result!=null&&AtHome&&result.SourceKey==PracticeKey()&&ValidPracticeDraft(result.Draft)&&ExpeditionUnlocked(result.Region,result.Depth)&&S.BlessingOffers.Contains(result.Blessing);}
    public string PracticeAdvice(PracticeDraft draft,int blessing){if(!ValidPracticeDraft(draft))return "先选齐三位伙伴与已解锁技能。";var game=PracticeCopy();game.S.Team=(int[])draft.Team.Clone();game.S.Skills=(int[])draft.Skills.Clone();return game.PartyAdvice(blessing);}
    public bool AdoptPractice(PracticeComparison result)
    {
        if(!PracticeIsCurrent(result))return false;int helper=PracticeCaretaker(result.Draft);TickFarm(DateTime.UtcNow);
        S.Team=(int[])result.Draft.Team.Clone();foreach(int pet in S.Team)S.Skills[pet]=result.Draft.Skills[pet];S.FarmCaretaker=helper;S.PlannedPicnic=result.Draft.Picnic;S.PlannedRegion=result.Region;S.PlannedDepth=result.Depth;
        S.Notice="已采用试练队伍、技能与便当计划。接下来确认祝福、目的地和材料费用，再开始冒险。";Save();return true;
    }
    public static string PracticeOutcome(BattleNote note){return note.Outcome=="victory"?"第 "+note.Rounds+" 回合清场":note.Outcome=="defeat"?"第 "+note.Rounds+" 回合体力耗尽":"已试打 "+note.Rounds+" 回合";}
}
