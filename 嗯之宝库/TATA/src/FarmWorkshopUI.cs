using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

public partial class FarmPanel
{
    private int overwritePartySlot=-1;
    private void PartyWorkshop()
    {
        var cols=Columns(236);var left=cols[0];var aside=cols[1];
        Heading(left,"配一支喜欢的小队","选伙伴、换技能、留个帮手。配好了就能放心出发。");
        if(!G.AtHome){left.Children.Add(Notice("这趟队伍正在旅行","返家后再调整或使用常用配置。这里可以查看当前搭配。"));left.Children.Add(Btn("查看当前旅程",()=>Go("冒险")));}
        left.Children.Add(SectionTitle("01  同行伙伴","选择已有队员会互换站位"));
        var party=new UniformGrid{Columns=3};left.Children.Add(party);
        for(int i=0;i<3;i++){
            int slot=i,pet=G.S.Team[i];var p=new StackPanel();p.Children.Add(U.Text((i+1)+" 号 · "+(i==0?"前排":"后排"),11,U.Muted));p.Children.Add(Portrait(pet,40));
            var choose=new ComboBox{ItemsSource=Enumerable.Range(0,FarmGame.PetCount).Select(G.Name).ToArray(),SelectedIndex=pet,IsEnabled=G.AtHome,FontSize=12,Margin=new Thickness(0,1,0,6)};
            System.Windows.Automation.AutomationProperties.SetName(choose,"第 "+(slot+1)+" 位伙伴");choose.SelectionChanged+=(s,e)=>{if(choose.SelectedIndex>=0&&choose.SelectedIndex!=G.S.Team[slot])Do(()=>G.AssignPartySlot(slot,choose.SelectedIndex));};p.Children.Add(choose);
            p.Children.Add(U.Text("Lv."+G.Level(pet)+" · "+FarmGame.DutyNames[FarmGame.Duties[pet]],11,U.Muted));
            var skill=new ComboBox{ItemsSource=FarmGame.SkillNames[pet].Take(G.SkillUnlocked(pet,1)?2:1).ToArray(),SelectedIndex=G.S.Skills[pet],IsEnabled=G.AtHome,FontSize=11,Margin=new Thickness(0,0,0,5)};System.Windows.Automation.AutomationProperties.SetName(skill,G.Name(pet)+"携带技能");skill.SelectionChanged+=(s,e)=>{if(skill.SelectedIndex>=0&&skill.SelectedIndex!=G.S.Skills[pet])Do(()=>G.SetSkill(pet,skill.SelectedIndex));};skill.ToolTip=FarmGame.SkillDetails[pet][G.S.Skills[pet]];p.Children.Add(skill);
            if(!G.SkillUnlocked(pet,1))skill.ToolTip+="\nLv.2 解锁第二技能。";var card=U.Card(p,7);card.Margin=new Thickness(0,0,7,5);card.Background=FarmTheme.ElementPaper[FarmGame.Elements[pet]];party.Children.Add(card);
        }
        var options=new StackPanel();options.Children.Add(Notice("这支队伍怎样配合 · "+G.PartyStyle,G.PartyAdvice()));
        options.Children.Add(SectionTitle("试试推荐组合","只调整队伍与技能"));var presets=new UniformGrid{Columns=2};options.Children.Add(presets);
        for(int i=0;i<4;i++){int preset=i;var button=Btn(new[]{"水雷连锁","护盾重击","灼烧蔓延","潮露毒种"}[i],()=>{if(preset==3)G.SetPoisonPreset();else G.SetPreset(preset);},G.AtHome&&(i==3||G.CanUsePreset(i)));button.Margin=new Thickness(0,0,7,6);button.ToolTip=i==1?"需要泡泡、苔壳 Lv.2。":i==2?"需要电团 Lv.2。":"伙伴现有基础技能就能搭配。";presets.Children.Add(button);}
        options.Children.Add(InspirationPreferences());left.Children.Add(SectionTitle("02  留一位农场帮手","出门时也会持续照料"));int[] resting=Enumerable.Range(0,FarmGame.PetCount).Where(p=>!G.S.Team.Contains(p)).ToArray();
        var helperRow=new Grid();helperRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});helperRow.ColumnDefinitions.Add(new ColumnDefinition());
        var helper=new ComboBox{ItemsSource=resting.Select(G.Name).ToArray(),SelectedIndex=Array.IndexOf(resting,G.Caretaker),IsEnabled=G.AtHome,FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0),ToolTip="选择后立即开始照料，不消耗材料。"};System.Windows.Automation.AutomationProperties.SetName(helper,"农场帮手");helper.SelectionChanged+=(s,e)=>{if(helper.SelectedIndex>=0&&resting[helper.SelectedIndex]!=G.Caretaker)Do(()=>G.SetCaretaker(resting[helper.SelectedIndex]));};helperRow.Children.Add(helper);
        var helperDetail=U.Text(G.CaretakerBonus(G.Caretaker)+"\n不加入本趟战斗。",11,U.Muted);helperDetail.Margin=new Thickness(0);Grid.SetColumn(helperDetail,1);helperRow.Children.Add(helperDetail);left.Children.Add(U.Card(helperRow,9));
        var expand=new Expander{Header="组合建议、推荐小队与灵感偏好",Content=options,IsExpanded=workshopOptionsOpen};System.Windows.Automation.AutomationProperties.SetAutomationId(expand,"workshop-options");expand.Expanded+=(s,e)=>workshopOptionsOpen=true;expand.Collapsed+=(s,e)=>workshopOptionsOpen=false;left.Children.Add(expand);
        var next=new StackPanel();next.Children.Add(U.Text("03  配好了，就出发",18));next.Children.Add(U.Text("换队和换技能免费。进入准备页后，确认出发才会开始旅行。",12,U.Muted));var adventure=Btn("去冒险验证搭配",()=>Go("冒险"),true,true);FarmTheme.Accent(adventure);next.Children.Add(adventure);next.Children.Add(Btn("搭配试练 · 免费试打",OpenPractice,G.AtHome));aside.Children.Add(U.Card(next,12));
        var train=new StackPanel();train.Children.Add(U.Text("想再强一点？",16));train.Children.Add(U.Text("出战伙伴各升 1 级；只花麦穗，不增加亲密。",11,U.Muted));
        var levels=Live(()=>String.Join(" / ",G.S.Team.Select(p=>G.Name(p)+" Lv."+G.Level(p)).ToArray()),11,U.Green);train.Children.Add(levels);
        int displayedCost=G.PartyTrainingCost;var displayedXp=(int[])G.S.Xp.Clone();var displayedTeam=(int[])G.S.Team.Clone();var bulk=Btn("",()=>{if(G.PartyTrainingCost==displayedCost&&G.S.Xp.SequenceEqual(displayedXp)&&G.S.Team.SequenceEqual(displayedTeam))G.TrainParty();});updates.Add(()=>{int cost=G.PartyTrainingCost;bulk.Content=cost>0?"全队升一级 · 麦穗 "+cost:"全队等级已满";bulk.IsEnabled=G.AtHome&&cost>0&&G.S.Grain>=cost;bulk.ToolTip=!G.AtHome?"返家后可训练。":G.S.Grain<cost?"还差麦穗 "+(cost-G.S.Grain)+"。":"一次完成三位伙伴到下一级所需的训练。";});train.Children.Add(bulk);train.Children.Add(Live(()=>!G.AtHome?"返家后再培养伙伴。":G.PartyTrainingCost==0?"这支队伍已满级。":G.S.Grain<G.PartyTrainingCost?"还差麦穗 "+(G.PartyTrainingCost-G.S.Grain)+"，农田会自动积攒。":"麦穗已够，点击后统一升一级。",11,U.Muted));train.Children.Add(Btn("缺麦穗？安排种植",OpenTeamFarm));aside.Children.Add(U.Card(train,10));aside.Children.Add(Btn("返回伙伴培养",()=>Go("伙伴")));
        aside.Children.Add(SectionTitle("常用队伍","保存 3 套"));
        for(int i=0;i<3;i++){
            int slot=i;var saved=G.S.SavedParties[i];var p=new StackPanel();p.Children.Add(U.Text("常用 "+(i+1)+(saved.Filled?" · "+saved.Name:" · 空位"),15,U.Green));
            p.Children.Add(U.Text(saved.Filled?String.Join(" → ",saved.Team.Select(G.Name).ToArray()):"保存队伍、站位、技能、信物和农场帮手。",11,U.Muted));
            if(saved.Filled){p.Children.Add(U.Text(G.RelicLoadout(saved.Relics),11,U.Muted));p.Children.Add(U.Text("灵感偏好 · "+FarmGame.InspirationStyles[saved.InspirationStyle],11,U.Muted));}
            U.Row(p,Btn("使用",()=>G.LoadParty(slot),G.AtHome&&saved.Filled),Btn(saved.Filled?"覆盖…":"保存当前",()=>{if(G.S.SavedParties[slot].Filled)overwritePartySlot=slot;else G.SaveParty(slot);},G.AtHome));
            if(overwritePartySlot==slot){p.Children.Add(U.Text("用现在的搭配覆盖这套配置？",11,U.Gold));U.Row(p,Btn("取消",()=>overwritePartySlot=-1),Btn("确认覆盖",()=>{G.SaveParty(slot);overwritePartySlot=-1;},G.AtHome));}aside.Children.Add(U.Card(p,10));
        }
        aside.Children.Add(Btn("配置信物",()=>OpenHandbook(2)));aside.Children.Add(Btn("查看最近冒险记录",()=>OpenHandbook(3)));
    }
    private bool plantingOpen;private int selectedPlanting;
    private UIElement PlantingPlanner()
    {
        var p=new StackPanel();var choices=new UniformGrid{Columns=2};p.Children.Add(choices);
        for(int i=0;i<4;i++){int plan=i;var b=Btn(FarmGame.PlantingNames[i],()=>selectedPlanting=plan,G.PlantingPlan(i)!=null,selectedPlanting==i);b.Margin=new Thickness(0,0,6,5);b.ToolTip=i==3&&!G.S.GardenUnlocked?"首次通关小溪后，薄荷与新增田地开放。":"预览种植比例，再决定是否应用。";choices.Children.Add(b);}
        if(G.PlantingPlan(selectedPlanting)==null)selectedPlanting=0;var crops=G.PlantingPlan(selectedPlanting);int changed=Enumerable.Range(0,G.Plots).Count(i=>G.S.Crops[i]!=crops[i]);
        p.Children.Add(U.Text("方案："+String.Join(" · ",Enumerable.Range(0,3).Where(k=>crops.Contains(k)).Select(k=>FarmGame.CropNames[k]+" "+crops.Count(x=>x==k)+" 块").ToArray()),12,U.Green));
        p.Children.Add(Live(()=>"预计每分钟："+RateText(G.FarmRates(G.PlantingPlan(selectedPlanting))),12,U.Gold));
        var apply=Btn(changed==0?"✓ 已按此方案种植":"应用方案 · 调整 "+changed+" 块田",()=>G.ApplyPlanting(selectedPlanting),changed>0,true);FarmTheme.Accent(apply);p.Children.Add(apply);p.Children.Add(U.Text("只有改种的田地重新计时。产量按当前土壤和帮手估算，实际按成熟批次入库。",11,U.Muted));
        var expand=new Expander{Header="种植计划 · 一次安排所有田地",Content=p,IsExpanded=plantingOpen};expand.Expanded+=(s,e)=>plantingOpen=true;expand.Collapsed+=(s,e)=>plantingOpen=false;return expand;
    }
    private static string RateText(double[] rates){return String.Join(" / ",Enumerable.Range(0,3).Where(i=>rates[i]>0).Select(i=>FarmGame.CropNames[i]+" "+rates[i].ToString("0.#")).ToArray());}
    private long historyExpanded;
    private void JourneyHistory(StackPanel stack)
    {
        stack.Children.Add(U.Text("保留最近 10 趟自动旅程。记录当时的等级、技能、祝福、信物与实战表现，方便对照搭配。",12,U.Muted));
        if(G.S.JourneyMemories.Length==0){stack.Children.Add(Notice("第一张旅行手记，等你带回来。","完成一趟自动冒险后会记录；失败与提前返家也会留下记录。升级前已经结束的旅程不补录。"));stack.Children.Add(Btn(G.AtHome?"准备一趟冒险":"查看当前旅程",()=>Go("冒险"),true,true));return;}
        foreach(var memory in G.S.JourneyMemories){var m=memory;var p=new StackPanel();var row=new DockPanel();var status=FarmTheme.Badge(m.Outcome=="victory"?"✓ 通关":m.Outcome=="retreat"?"提前返家":"暂未通关",m.Outcome=="victory"?U.Pale:FarmTheme.ElementPaper[3]);DockPanel.SetDock(status,Dock.Right);row.Children.Add(status);row.Children.Add(U.Text(FarmGame.ExpeditionName(m.Region,m.Depth)+" · "+m.Build,18));p.Children.Add(row);
            p.Children.Add(U.Text("记录于 "+new DateTime(m.RecordedTicks,DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm")+" · 到达第 "+m.Station+" / 5 站",11,U.Muted));
            p.Children.Add(U.Text(String.Join(" → ",Enumerable.Range(0,3).Select(i=>m.Names[i]+" Lv."+m.Levels[i]).ToArray()),13,U.Green));p.Children.Add(U.Text("带回肥料 "+m.Fertilizer+" · 晶石 "+m.Crystals+(m.Decoration>=0?" · 新装饰「"+FarmGame.DecorationNames[m.Decoration]+"」":"")+(m.Relic>=0?" · 新信物「"+FarmGame.RelicNames[m.Relic]+"」":""),12,U.Gold));
            var details=new StackPanel();details.Children.Add(U.Text("携带技能："+String.Join(" / ",m.Skills),12));details.Children.Add(U.Text("祝福："+m.Blessing,12));details.Children.Add(U.Text(m.Report,12,U.Green));details.Children.Add(U.Text("信物："+m.RelicReport,12,U.Muted));
            if(m.InspirationStyle>=0){details.Children.Add(U.Text("灵感偏好 · "+FarmGame.InspirationStyles[m.InspirationStyle],12,U.Green));details.Children.Add(U.Text(FarmGame.InspirationHistory(m.Inspirations),12,U.Muted));}
            if(m.Picnic>=0)details.Children.Add(U.Text("便当 · "+FarmGame.PicnicReport(m.Picnic,m.PicnicTriggers,m.PicnicHealing),12,U.Green));
            if(m.Battles.Length>0)details.Children.Add(Btn("查看逐场复盘",()=>OpenBattleReview(m)));
            var expand=new Expander{Header="展开当时的配置与战报",Content=details,IsExpanded=historyExpanded==m.RecordedTicks};expand.Expanded+=(s,e)=>historyExpanded=m.RecordedTicks;expand.Collapsed+=(s,e)=>{if(historyExpanded==m.RecordedTicks)historyExpanded=0;};p.Children.Add(expand);
            p.Children.Add(Btn("用当前队伍准备这条路线",()=>{SelectJourneyDestination(m.Region,m.Depth);Go("冒险");},G.AtHome&&G.ExpeditionUnlocked(m.Region,m.Depth)));stack.Children.Add(U.Card(p,12));
        }
    }
}
