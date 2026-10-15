using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public enum HomeGuideStage { Harvest, FreshBerries, Complete, Resting, Invitation, StayingHome }

public partial class FarmGame
{
    public int GuideCompanion{get{return S.Team.Contains(0)?0:S.Team[0];}}
    public bool OpenGuideOnLaunch{get{return false;}} // The player opens the lesson from the desktop pet.
    public string HomeGuideResumeTab
    {
        get{
            if(S.GuideStep==2)return "伙伴";
            if(S.GuideStep==3){
                if(S.GuideOutingAccepted)return "冒险";
                if(S.GuideFarmLesson==1&&S.GuideHomeStage<=HomeGuideStage.FreshBerries)return "农场";
                if(!S.GuideFarmSeen||S.GuideHomeStage==HomeGuideStage.Complete)return "伙伴";
                return "农场";
            }
            return S.GuideStep==4||S.GuideStep==5?"冒险":"农场";
        }
    }
    public void ResetHomeGuide()
    {
        S.GuideFarmSeen=false;S.GuideFarmLesson=0;S.GuideDepartureLesson=0;S.GuideHomeStage=HomeGuideStage.Harvest;S.GuideHarvestBerries=0;S.GuideRestUntilTicks=0;S.GuideOutingAccepted=false;
    }
    private void AdvanceHomeGuide(int berries,long ticks)
    {
        if(S.GuideStep!=3||!AtHome)return;
        if(S.GuideHomeStage==HomeGuideStage.Harvest&&S.GuideFarmSeen&&berries>0){
            S.GuideHarvestBerries=berries;S.GuideHomeStage=HomeGuideStage.FreshBerries;Save();
        }else if(S.GuideHomeStage==HomeGuideStage.Resting&&ticks>=S.GuideRestUntilTicks){
            S.GuideHomeStage=HomeGuideStage.Invitation;Save();
        }
    }
    public bool RestAfterHomeGuide()
    {
        if(S.GuideStep!=3||!AtHome||S.GuideHomeStage!=HomeGuideStage.Complete)return false;
        S.GuideHomeStage=HomeGuideStage.Resting;S.GuideRestUntilTicks=DateTime.UtcNow.AddSeconds(30).Ticks;S.GuideOutingAccepted=false;Save();return true;
    }
    public bool DeferGuideOuting()
    {
        if(S.GuideStep!=3||!AtHome||(!S.GuideOutingAccepted&&S.GuideHomeStage!=HomeGuideStage.Invitation))return false;
        if(S.GuideHomeStage>=HomeGuideStage.Complete)S.GuideHomeStage=HomeGuideStage.StayingHome;
        S.GuideOutingAccepted=false;Save();return true;
    }
}

public partial class FarmPanel
{
    private void HomeGuideAction(FarmSave state,HomeGuideStage stage,Action action)
    {
        if(!Object.ReferenceEquals(state,G.S)||G.S.GuideStep!=3||G.S.GuideHomeStage!=stage||!G.AtHome)return;
        action();G.SyncGuide();G.Save();resetScroll=true;
    }
    private void HomeGuideSecondary(StackPanel panel,string label,Action action)
    {
        var button=Btn(label,action);button.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(button);
    }
    private void OpenGuideOuting(){journeyDifficulty=journeyDepth=0;Go("冒险");}
    private void ShowHomeGuideReceipt(StackPanel panel)
    {
        panel.Children.Add(GrowthCard(GuidePet,false,G.S.GuideHomeStage!=HomeGuideStage.FreshBerries));
    }
    private bool GentleHomePet()
    {
        if(G.S.GuideStep!=3||!G.AtHome||G.S.GuideHomeStage==HomeGuideStage.Harvest)return false;
        int pet=GuidePet;SelectGuidePetIfNeeded(pet);var state=G.S;var stage=state.GuideHomeStage;var panel=GentleColumn(440);
        bool fresh=stage==HomeGuideStage.FreshBerries,complete=stage==HomeGuideStage.Complete,fullyGrown=G.Level(pet)>=10&&!G.NeedsEvolutionBond(pet);
        panel.Children.Add(GentleText(complete?"这样就会照顾我啦":G.Name(pet),22));
        panel.Children.Add(TouchablePortrait(pet,96));
        panel.Children.Add(GuideCareLine(pet,()=>complete?"农田会自己长，有空再来喂我。":fresh?"莓果已经收好，留着下次吃吧。":"有你在，待在家里也很开心。",!fresh&&!complete&&!fullyGrown));
        if(!fresh)ShowHomeGuideReceipt(panel);
        if(complete){
            panel.Children.Add(GentleAction("回小院歇一会儿",()=>HomeGuideAction(state,stage,()=>{if(G.RestAfterHomeGuide())Go("农场");})));
            return true;
        }
        // A stale feed control cannot spend twice, even while ordinary companion feeding remains available.
        int xp=state.Xp[pet],bond=G.PetBond(pet);
        if(fresh){
            panel.Children.Add(GentleText("实际收好莓果 "+state.GuideHarvestBerries+"，不用再领一次。",12,U.Muted));
            panel.Children.Add(GentleAction("收好这份莓果",()=>HomeGuideAction(state,stage,()=>state.GuideHomeStage=HomeGuideStage.Complete)));
        }else if(state.Berries<2&&!fullyGrown){
            panel.Children.Add(GuideBerryAction("去农田看看莓果",()=>{if(Object.ReferenceEquals(state,G.S)&&G.S.GuideStep==3&&G.S.GuideHomeStage==stage&&Tab=="伙伴")OpenGuideBerries(pet);}));
        }else{
            var feed=GentleFeedAction(fresh?"喂新收的莓果 · 莓果 2":FarmUX.FeedLabel(G,pet),pet,()=>HomeGuideAction(state,stage,()=>{
                if(state.Xp[pet]==xp&&G.PetBond(pet)==bond)CareForFocusedPet(pet,()=>G.Feed(pet));
            }));panel.Children.Add(feed);
            var shortage=Live(()=>fullyGrown?"已经长大啦，陪一会儿也很好。":"",11,U.Muted);shortage.TextAlignment=TextAlignment.Center;panel.Children.Add(shortage);
            updates.Add(()=>shortage.Visibility=String.IsNullOrEmpty(shortage.Text)?Visibility.Collapsed:Visibility.Visible);
            
        }
        if(!fresh)HomeGuideSecondary(panel,"回小院",()=>Go("农场"));return true;
    }
    private void GentleHomeLesson()
    {
        var state=G.S;var stage=state.GuideHomeStage;
        if(stage==HomeGuideStage.Complete){GentleHomePet();return;}
        bool waiting=stage==HomeGuideStage.Harvest,fresh=stage==HomeGuideStage.FreshBerries,invite=stage==HomeGuideStage.Invitation,intro=FarmIntroduction;
        var panel=GentleColumn(480);
        panel.Children.Add(GentleText(intro?"莓果从这里长出来":waiting?"等这一批莓果":fresh?"这批莓果收好啦":invite?"泡泡想出去转转":"泡泡陪着你",22));
        int plot=LessonBerryPlot();var scene=Scene();scene.FocusedFarmPet=GuidePet;scene.Height=scene.MinHeight=166;MakeSceneTouchable(scene,GuidePet);
        if((intro||waiting||fresh)&&plot>=0){scene.LearningPlot=plot;scene.ShowBerryBasket=!intro;}panel.Children.Add(scene);
        if((intro||waiting)&&plot<0){panel.Children.Add(GentleText("这次还没种莓果，先看看一块田吧。",14,U.Green));panel.Children.Add(GuideBerryAction("去种莓果",()=>{if(Object.ReferenceEquals(state,G.S)&&Tab=="农场")OpenGuideBerries(GuidePet);}));return;}
        if(intro){panel.Children.Add(GentleText("这块田，会自己长出莓果。",14,U.Green));panel.Children.Add(GentleAction("看看它怎么长",()=>BeginWatchingFarm(state)));return;}
        if(waiting){
            panel.Children.Add(HarvestProgress(plot,false));
            panel.Children.Add(GentleAction("摸摸"+G.Name(GuidePet),()=>PetCompanion(state,GuidePet)));
            var response=Live(()=>PetResponding(GuidePet)?PetResponse(GuidePet):"成熟后会自动收进篮子。",12,U.Muted);response.TextAlignment=TextAlignment.Center;panel.Children.Add(response);
            
            return;
        }
        if(fresh){
            panel.Children.Add(HarvestProgress(plot,true));
            panel.Children.Add(GentleAction("收好，回去陪泡泡",()=>HomeGuideAction(state,stage,()=>{state.GuideHomeStage=HomeGuideStage.Complete;companionMoment=null;Go("伙伴");})));return;
        }
        if(invite){
            panel.Children.Add(GentleText("吃饱啦，想去小溪边转转。",15,U.Green));
            panel.Children.Add(GentleAction("去看看",()=>HomeGuideAction(state,stage,OpenGuideOuting)));
            HomeGuideSecondary(panel,"先待在家里",()=>HomeGuideAction(state,stage,()=>G.DeferGuideOuting()));return;
        }
        panel.Children.Add(GentleText(stage==HomeGuideStage.StayingHome?"那就一起待在家里吧。":"莓果在长，泡泡也在。",15,U.Green));
        panel.Children.Add(GentleText("有空喂一口，忙时缩成宠物陪着你。",12,U.Muted));
        panel.Children.Add(GentleAction("喂泡泡",()=>HomeGuideAction(state,stage,()=>Go("伙伴"))));
        if(stage==HomeGuideStage.StayingHome)HomeGuideSecondary(panel,"去小溪看看",()=>HomeGuideAction(state,stage,OpenGuideOuting));
    }
}
