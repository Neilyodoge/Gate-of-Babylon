using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private PracticeDraft practiceDraft;
    private PracticeComparison practiceResult;
    private int practiceRegion,practiceDepth,practiceBlessing,practiceTurn=1;
    private void OpenPractice()
    {
        if(!G.AtHome){Go("冒险");return;}G.EnsureJourneyOffers();practiceDraft=G.CurrentPracticeDraft();practiceResult=null;
        practiceRegion=journeyDifficulty;practiceDepth=journeyDepth;if(!G.ExpeditionUnlocked(practiceRegion,practiceDepth)){practiceRegion=0;practiceDepth=0;}
        practiceBlessing=G.S.BlessingOffers.Contains(selectedBlessing)?selectedBlessing:G.S.BlessingOffers[0];Go("试练");resetScroll=true;
    }
    private void PracticeAssign(int slot,int pet)
    {
        int other=Array.IndexOf(practiceDraft.Team,pet),old=practiceDraft.Team[slot];if(other>=0)practiceDraft.Team[other]=old;practiceDraft.Team[slot]=pet;
        if(!G.SkillUnlocked(pet,practiceDraft.Skills[pet]))practiceDraft.Skills[pet]=0;practiceResult=null;
    }
    private void StartPractice(){practiceResult=G.Practice(practiceDraft,practiceRegion,practiceDepth,practiceBlessing);practiceTurn=1;resetScroll=true;}
    private void PracticePage()
    {
        var cols=Columns(236);var left=cols[0];var aside=cols[1];
        if(!G.AtHome){Heading(left,"等伙伴回来，再一起试练。","当前旅程会照常进行。");aside.Children.Add(Btn("查看当前旅程",()=>Go("冒险"),true,true));return;}
        G.EnsureJourneyOffers();if(practiceDraft==null)practiceDraft=G.CurrentPracticeDraft();
        if(practiceResult!=null){PracticeResults(left,aside);return;}
        Heading(left,"先试一试，配合就明白了。","免费试打第一场 · 最多 3 回合 · 不消耗体力或资源");
        left.Children.Add(SectionTitle("临时搭配","尚未改变正式队伍"));
        var team=new UniformGrid{Columns=3};left.Children.Add(team);
        for(int i=0;i<3;i++){
            int slot=i,pet=practiceDraft.Team[i];var p=new StackPanel();p.Children.Add(ReviewText((i+1)+" 号 · "+(i==0?"前排":"后排"),11,U.Muted));p.Children.Add(Portrait(pet,52));
            var member=new ComboBox{ItemsSource=Enumerable.Range(0,FarmGame.PetCount).Select(G.Name).ToArray(),SelectedIndex=pet,FontSize=12,Margin=new Thickness(0,3,0,6)};AutomationProperties.SetName(member,"试练第 "+(i+1)+" 位伙伴");member.SelectionChanged+=(s,e)=>{if(member.SelectedIndex>=0&&member.SelectedIndex!=practiceDraft.Team[slot])Do(()=>PracticeAssign(slot,member.SelectedIndex));};p.Children.Add(member);
            p.Children.Add(ReviewText("Lv."+G.Level(pet)+" · "+FarmGame.DutyNames[FarmGame.Duties[pet]],11,U.Muted));
            var skill=new ComboBox{ItemsSource=FarmGame.SkillNames[pet].Take(G.SkillUnlocked(pet,1)?2:1).ToArray(),SelectedIndex=practiceDraft.Skills[pet],FontSize=11,Margin=new Thickness(0,3,0,4)};AutomationProperties.SetName(skill,"试练"+G.Name(pet)+"技能");skill.ToolTip=FarmGame.SkillDetails[pet][practiceDraft.Skills[pet]];skill.SelectionChanged+=(s,e)=>{if(skill.SelectedIndex>=0&&skill.SelectedIndex!=practiceDraft.Skills[pet])Do(()=>practiceDraft.Skills[pet]=skill.SelectedIndex);};p.Children.Add(skill);
            var card=U.Card(p,8);card.Background=FarmTheme.ElementPaper[FarmGame.Elements[pet]];card.Margin=new Thickness(0,0,6,8);team.Children.Add(card);
        }
        var recipes=new UniformGrid{Columns=3};left.Children.Add(recipes);string[] names={"水雷连锁","护盾重击","灼烧蔓延","潮露毒种","火毒催化"};
        for(int i=0;i<5;i++){int preset=i;var button=Btn(names[i],()=>practiceDraft=G.PracticePreset(preset),G.PracticePreset(i)!=null);button.Padding=new Thickness(3,5,3,5);button.FontSize=11;button.Margin=new Thickness(0,0,6,5);button.ToolTip=i==1?"需泡泡、苔壳 Lv.2":i==2?"需电团 Lv.2":i==4?"需种子 Lv.2":"使用现有基础技能";recipes.Children.Add(button);}recipes.Children.Add(Btn("重置为当前",()=>practiceDraft=G.CurrentPracticeDraft()));
        left.Children.Add(Notice("这套搭配怎样运转",G.PracticeAdvice(practiceDraft,practiceBlessing)));
        var controls=new StackPanel();controls.Children.Add(U.Text("挑一场来试试",18));
        int[] routes=Enumerable.Range(0,6).Where(id=>G.ExpeditionUnlocked(id/3,id%3)).ToArray();int route=Array.IndexOf(routes,practiceRegion*3+practiceDepth);if(route<0){route=0;practiceRegion=routes[0]/3;practiceDepth=routes[0]%3;}
        var destination=new ComboBox{ItemsSource=routes.Select(id=>FarmGame.ExpeditionName(id/3,id%3)).ToArray(),SelectedIndex=route,FontSize=12,Margin=new Thickness(0,0,0,8)};AutomationProperties.SetName(destination,"试练路线");destination.SelectionChanged+=(s,e)=>{if(destination.SelectedIndex>=0)Do(()=>{int id=routes[destination.SelectedIndex];practiceRegion=id/3;practiceDepth=id%3;});};controls.Children.Add(destination);
        controls.Children.Add(ReviewText("双方使用同一份祝福",11,U.Muted));int[] offers=G.S.BlessingOffers;
        if(!offers.Contains(practiceBlessing))practiceBlessing=offers[0];var blessing=new ComboBox{ItemsSource=offers.Select(id=>FarmGame.BlessingNames[id]).ToArray(),SelectedIndex=Array.IndexOf(offers,practiceBlessing),FontSize=12,Margin=new Thickness(0,3,0,7)};AutomationProperties.SetName(blessing,"试练祝福");blessing.SelectionChanged+=(s,e)=>{if(blessing.SelectedIndex>=0)Do(()=>practiceBlessing=offers[blessing.SelectedIndex]);};controls.Children.Add(blessing);
        controls.Children.Add(ReviewText(FarmGame.BlessingDetails[practiceBlessing],11,U.Muted));PracticePicnicPicker(controls);var start=Btn("开始试练 · 免费",StartPractice,G.ValidPracticeDraft(practiceDraft),true);FarmTheme.Accent(start);controls.Children.Add(start);controls.Children.Add(Btn("返回配队",()=>Go("配队")));aside.Children.Add(U.Card(controls,12));
        aside.Children.Add(Notice("只看开场，慢慢试","当前队伍与临时搭配都从满体力开始，使用当前等级、进化和信物。打完第一场会提前结束；练习不发奖励、不计通关，也不写入旅行手记。"));
        left.Children.Add(U.Text("可选择已开放的路线和本次三份祝福；试练不会刷新祝福。",11,U.Muted));
    }
    private UIElement PracticeSideCard(string title,PracticeSide side,int turn)
    {
        int index=Math.Min(turn,side.Rounds.Length)-1;var note=side.Rounds[index];var p=new StackPanel();p.Children.Add(ReviewText(title,17,U.Green));p.Children.Add(ReviewText(FarmGame.PracticeOutcome(note),12));
        p.Children.Add(ReviewText(String.Join(" → ",note.Names),11,U.Muted));
        p.Children.Add(ReviewText("便当 · "+FarmGame.PicnicName(note.Picnic),11,U.Green));
        if(note.Picnic>=0)p.Children.Add(ReviewText(note.Picnic==0?"本趟体力上限每位 +8":note.Picnic==2?"便当实际治疗 "+note.PicnicHealing:"便当满足条件 "+note.PicnicTriggers+" 次",10,U.Muted));
        p.Children.Add(ReviewText("实际伤害  "+(note.Direct+note.Burn+note.Poison)+"     敌人剩余  "+note.EnemyRemaining,13,U.Green));
        p.Children.Add(ReviewText("治疗 "+note.Healing+" · 挡伤 "+note.Blocked.Sum()+" · 承伤 "+note.Taken.Sum(),11));
        p.Children.Add(ReviewText("队伍体力 "+note.EndHp.Sum()+" / "+note.MaxHp.Sum()+" · 存活 "+note.EndHp.Count(hp=>hp>0)+" / 3",11,U.Muted));
        p.Children.Add(U.Bar(note.EndHp.Sum(),note.MaxHp.Sum()));
        p.Children.Add(ReviewText("潮湿追击 "+note.Combos[0]+" · 闪电跳跃 "+note.Combos[1]+"\n护盾重击 "+note.Combos[2]+" · 余烬催化 "+note.Combos[5]+"\n灼烧伤害 "+note.Burn+" · 中毒伤害 "+note.Poison,11,U.Green));
        var details=new StackPanel();details.Children.Add(U.Text("这一回合的技能记录",12));details.Children.Add(U.Text(side.Logs[index],11));details.Children.Add(U.Text("技能记录显示出招数值；上方实际伤害已扣除硬壳与过量伤害。",10,U.Muted));p.Children.Add(new Expander{Header="看看出手过程",Content=details});
        var card=U.Card(p,10);card.Margin=new Thickness(0,0,7,8);return card;
    }
    private void PracticeResults(StackPanel left,StackPanel aside)
    {
        var result=practiceResult;bool current=G.PracticeIsCurrent(result);Heading(left,"换个搭配，开场有什么不同？",FarmGame.ExpeditionName(result.Region,result.Depth)+" · 第一场 · "+FarmGame.BlessingNames[result.Blessing]);
        int max=Math.Max(result.Current.Rounds.Length,result.Trial.Rounds.Length);int turn=Math.Min(practiceTurn,max);
        var turns=new UniformGrid{Columns=3};left.Children.Add(turns);for(int i=1;i<=max;i++){int selected=i;var button=Btn("第 "+i+" 回合",()=>practiceTurn=selected,true,turn==i);button.Margin=new Thickness(0,0,7,8);turns.Children.Add(button);}
        var compare=new UniformGrid{Columns=2};compare.Children.Add(PracticeSideCard("当前队伍",result.Current,turn));compare.Children.Add(PracticeSideCard("临时搭配",result.Trial,turn));left.Children.Add(compare);
        left.Children.Add(U.Text("数值累计到所选回合；提前清场或体力耗尽的一方保留结束时的数据。",11,U.Muted));
        left.Children.Add(Notice("截至第 "+turn+" 回合的配合",PracticeSummary(result,turn)));
        var actions=new StackPanel();actions.Children.Add(U.Text("试好了，再出发",18));actions.Children.Add(U.Text(current?"采用队伍、技能与便当计划，再到准备页确认真正出发。":"正式配置或成长已变化，重新试练后再采用。",12,U.Muted));
        if(result.Draft.Picnic>=0)actions.Children.Add(U.Text("便当未制作 · 出发需 "+FarmGame.PicnicCostText(result.Draft.Picnic),11,U.Gold));
        var adopt=Btn("采用搭配并去准备",()=>{if(G.AdoptPractice(result)){journeyDifficulty=result.Region;journeyDepth=result.Depth;selectedBlessing=result.Blessing;Go("冒险");resetScroll=true;}},current,true);FarmTheme.Accent(adopt);actions.Children.Add(adopt);
        actions.Children.Add(Btn("再调整一下",()=>{practiceResult=null;resetScroll=true;}));actions.Children.Add(Btn("保留原队伍，回配队",()=>Go("配队")));aside.Children.Add(U.Card(actions,12));
        var helper=G.PracticeCaretaker(result.Draft);aside.Children.Add(Notice("采用后的农场帮手",G.Name(helper)+(helper==G.Caretaker?"继续照料农田。":"接替照料农田，原帮手将随队出发。")+"\n"+G.CaretakerBonus(helper)));
        aside.Children.Add(U.Text("两边沿用当前信物；第一场的短时表现不代表整趟胜负。没有练习奖励或通关进度。",11,U.Muted));
    }
    private string PracticeSummary(PracticeComparison result,int turn)
    {
        var a=result.Current.Rounds[Math.Min(turn,result.Current.Rounds.Length)-1];var b=result.Trial.Rounds[Math.Min(turn,result.Trial.Rounds.Length)-1];string text="当前队伍："+FarmGame.PracticeOutcome(a)+"；临时搭配："+FarmGame.PracticeOutcome(b)+"。";
        if(b.Combos[0]>a.Combos[0])text+="\n临时搭配多触发了 "+(b.Combos[0]-a.Combos[0])+" 次潮湿追击。";
        if(b.Combos[5]>a.Combos[5])text+="\n临时搭配多触发了 "+(b.Combos[5]-a.Combos[5])+" 次余烬催化。";
        if(b.Combos[2]>a.Combos[2])text+="\n临时搭配多触发了 "+(b.Combos[2]-a.Combos[2])+" 次护盾重击。";
        if(a.Picnic!=b.Picnic)text+="\n便当不同：比较包含各自补给的效果；攻击加成遇到残血敌人时，可能不会增加实际扣血。";
        return text+"\n清场后停止统计，伤害数字相同也可能花了不同回合。";
    }
}
