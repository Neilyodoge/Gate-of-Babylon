using System;
using System.Windows;
using System.Windows.Controls;

public sealed class LifeCareResult
{
    public FarmSave State;
    public int Pet,Visit,AfterXp,BeforeLevel,Growth,Cost;
    public string Page,Activity;
}

public partial class FarmPanel
{
    private LifeCareResult lifeCareResult;
    private void ApplyLifeCare(int pet,string activity)
    {
        int xp=G.S.Xp[pet],level=G.Level(pet),before=activity=="train"?G.S.Grain:G.S.Mint;
        CareForFocusedPet(pet,()=>activity=="train"?G.Train(pet):G.HerbalFeed(pet));
        if(G.S.Xp[pet]==xp)return;
        lifeRevision++;resetScroll=true;
        lifeCareResult=new LifeCareResult{State=G.S,Pet=pet,Visit=courtyardVisit,Page=Tab,Activity=activity,BeforeLevel=level,AfterXp=G.S.Xp[pet],Growth=G.S.Xp[pet]-xp,Cost=before-(activity=="train"?G.S.Grain:G.S.Mint)};
        ShowCompanionMoment(pet,activity=="train"?"train":"snack",activity=="train"?"练好啦，歇一会儿吧。":"啊呜，薄荷点心真香。");
    }
    private bool ShowLifeCareResult()
    {
        var result=lifeCareResult;
        if(result==null||!Object.ReferenceEquals(result.State,G.S)||result.Page!=Tab||result.Visit!=courtyardVisit||result.Pet!=G.CourtyardCompanion||result.AfterXp!=G.S.Xp[result.Pet])return false;
        int pet=result.Pet;bool trained=result.Activity=="train",learned=result.BeforeLevel<2&&G.Level(pet)>=2,skillGoal=HasSkillCare;var current=LifeGuard();var panel=GentleColumn(440);
        panel.Children.Add(GentleText(trained?"练好啦，歇一会儿吧":"点心吃好啦",21));panel.Children.Add(TouchablePortrait(pet,54));panel.Children.Add(GrowthCard(pet,true));
        panel.Children.Add(GentleText("成长 +"+result.Growth+" · 用了"+(trained?"麦穗":"薄荷")+" "+result.Cost,13,U.Green));
        var back=GentleAction(skillGoal?"回去看这招":"回去陪"+G.Name(pet),()=>{if(current()){if(skillGoal)ReturnSkillCare();else{ClearLifeCareResult();Go("伙伴");}}});back.Margin=new Thickness(0,5,0,2);panel.Children.Add(back);
        if(learned&&!skillGoal)HomeGuideSecondary(panel,"看看刚学会的一招",()=>{if(current()){ClearLifeCareResult();OpenLife("技能");lifeSkill=1;}});
        LifeDetails(panel,"还想再陪一会儿","life-care-next",Btn(trained?"再看看训练":"再看看薄荷点心",()=>{if(current()){ClearLifeCareResult();lifeRevision++;lifeMintOpen=false;if(!trained)Go("点心");resetScroll=true;}}));
        return true;
    }
    private void ClearLifeCareResult(){if(lifeCareResult!=null){lifeCareResult=null;growthReceipt=null;}}
}
