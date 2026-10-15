using System;
using System.Linq;

public partial class FarmPanel
{
    private FarmSave skillCareState;
    private int skillCarePet=-1,skillCareSkill=-1;
    private bool HasSkillCare{get{return Object.ReferenceEquals(skillCareState,G.S)&&SimpleCourtyard&&G.AtHome&&skillCarePet==G.CourtyardCompanion&&skillCareSkill==1;}}
    private void ClearSkillCare(){skillCareState=null;skillCarePet=skillCareSkill=-1;}
    private void CheckSkillCareScope(string target)
    {
        if(!HasSkillCare||!new[]{"照料","点心","材料","农田"}.Contains(target))ClearSkillCare();
    }
    private void OpenSkillCare(int pet,int skill)
    {
        if(!SimpleCourtyard||!G.AtHome||pet!=G.CourtyardCompanion||skill!=1||G.SkillUnlocked(pet,skill))return;
        skillCareState=G.S;skillCarePet=pet;skillCareSkill=skill;OpenLife("照料");
    }
    private void ReturnSkillCare()
    {
        if(!HasSkillCare)return;
        int skill=skillCareSkill,origin=pageHistory.LastIndexOf("技能");
        ClearSkillCare();ClearLifeCareResult();lifeSkill=skill;lifeReceipt="";lifeRevision++;
        backNavigating=true;try{Go("技能");}finally{backNavigating=false;}
        if(origin>=0)pageHistory.RemoveRange(origin,pageHistory.Count-origin);
        resetScroll=true;
    }
}
