using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private int journeyNode=-1,selectedBlessing=-1,journeyDifficulty;
    private void SkillPicker(StackPanel content,int pet)
    {
        content.Children.Add(U.Text("携带技能 · 可免费更换",12,U.Green));
        var row=new UniformGrid{Columns=2};content.Children.Add(row);
        for(int n=0;n<2;n++){int skill=n;bool unlocked=G.SkillUnlocked(pet,skill);var button=Btn((G.S.Skills[pet]==skill?"✓ ":"")+FarmGame.SkillNames[pet][skill]+(unlocked?"":" · Lv.2"),()=>G.SetSkill(pet,skill),G.AtHome&&unlocked,G.S.Skills[pet]==skill);button.Padding=new Thickness(5,6,5,6);button.Margin=new Thickness(0,0,5,6);button.ToolTip=FarmGame.SkillDetails[pet][skill]+(unlocked?"":" 升到 Lv.2 后解锁。");row.Children.Add(button);}
        content.Children.Add(U.Text(FarmGame.SkillDetails[pet][G.S.Skills[pet]],12,U.Muted));
    }
    private void PrepareJourney(StackPanel left,StackPanel aside)
    {
        G.EnsureJourneyOffers();if(!G.S.BlessingOffers.Contains(selectedBlessing))selectedBlessing=G.S.BlessingOffers[0];
        if(GentleJourney){GentleGuideDeparture(left,aside);return;}
        Heading(left,"选个地方，就能出发",WorldGuide?"第一次推荐小溪初探：带回肥料，试着拿到溪边风车。":"选目的地 → 看同行伙伴 → 确认出发。祝福已有默认选择，其他准备都可选。");
        left.Children.Add(CompactJourneyDestination());
        left.Children.Add(SectionTitle("02  同行伙伴","按站位自动行动"));
        var party=new UniformGrid{Columns=3};left.Children.Add(party);
        for(int slot=0;slot<G.S.Team.Length;slot++){int pet=G.S.Team[slot];var p=new StackPanel();var position=U.Text((slot+1)+" 号 · "+(slot==0?"前排":"后排"),10,U.Muted);position.Margin=new Thickness(0);p.Children.Add(position);p.Children.Add(Portrait(pet,52));var name=U.Text(G.Name(pet)+" Lv."+G.Level(pet),13);name.Margin=new Thickness(0,0,0,3);p.Children.Add(name);var skill=U.Text(FarmGame.SkillNames[pet][G.S.Skills[pet]],11,U.Green);skill.Margin=new Thickness(0);skill.ToolTip=FarmGame.SkillDetails[pet][G.S.Skills[pet]];p.Children.Add(skill);var card=U.Card(p,8);card.Background=FarmTheme.ElementPaper[FarmGame.Elements[pet]];card.Margin=new Thickness(0,0,6,8);party.Children.Add(card);}
        var presets=new UniformGrid{Columns=3};
        for(int i=0;i<3;i++){int preset=i;var b=Btn(new[]{"水雷连锁","护盾重击","灼烧蔓延"}[i],()=>G.SetPreset(preset),G.CanUsePreset(i));b.Padding=new Thickness(5,6,5,6);b.Margin=new Thickness(0,0,5,5);b.ToolTip=i==0?"泡泡铺潮湿，电团连锁，苔壳提供续航。":i==1?"需要泡泡和苔壳 Lv.2：雨露转盾，苔壳在前排消耗护盾重击。":"需要电团 Lv.2：赤钳蔓延灼烧，电团从灼烧目标回流治疗。";presets.Children.Add(b);}
        var offers=new UniformGrid{Columns=3};
        foreach(int id in G.S.BlessingOffers){int blessing=id;var p=new DockPanel{LastChildFill=true};var choose=Btn(selectedBlessing==id?"✓ 已携带":"携带祝福",()=>selectedBlessing=blessing,true,selectedBlessing==id);choose.Padding=new Thickness(5,6,5,6);DockPanel.SetDock(choose,Dock.Bottom);p.Children.Add(choose);var description=new StackPanel();description.Children.Add(U.Text(FarmGame.BlessingNames[id],15,U.Green));description.Children.Add(U.Text(FarmGame.BlessingDetails[id],12));p.Children.Add(description);var c=U.Card(p,9);c.Margin=new Thickness(0,0,6,6);c.ToolTip="选项会保存，换队或重开不会刷新。";if(selectedBlessing==id)c.Background=U.Pale;offers.Children.Add(c);}
        var blessingOptions=new Expander{Header="调整祝福 · 可选 · 当前「"+FarmGame.BlessingNames[selectedBlessing]+"」",Content=offers,IsExpanded=journeyBlessingsOpen};blessingOptions.Expanded+=(s,e)=>journeyBlessingsOpen=true;blessingOptions.Collapsed+=(s,e)=>journeyBlessingsOpen=false;System.Windows.Automation.AutomationProperties.SetAutomationId(blessingOptions,"departure-blessings");left.Children.Add(blessingOptions);
        var more=new StackPanel();more.Children.Add(presets);more.Children.Add(Btn("潮露毒种 · 泡泡 / 种子 / 苔壳",()=>G.SetPoisonPreset()));more.Children.Add(U.Text("盾击队需泡泡、苔壳 Lv.2；灼烧队需电团 Lv.2。",12,U.Muted));more.Children.Add(Btn("配队、常用配置与全队训练",()=>Go("配队")));var configs=new Expander{Header="换个搭配 · 推荐组合与自由配队",Content=more,IsExpanded=journeyConfigOpen};configs.Expanded+=(s,e)=>journeyConfigOpen=true;configs.Collapsed+=(s,e)=>journeyConfigOpen=false;left.Children.Add(configs);left.Children.Add(Btn("搭配试练 · 免费试打",OpenPractice));left.Children.Add(InspirationPreferences());
        var plan=new StackPanel();plan.Children.Add(U.Text("03  确认出发",18));plan.Children.Add(U.Text(FarmGame.ExpeditionName(journeyDifficulty,journeyDepth),16,U.Green));plan.Children.Add(U.Text("约 2～4 分钟 · 隐藏后继续",11,U.Muted));
        plan.Children.Add(U.Text("通关基础收获\n肥料 "+FarmGame.ExpeditionFertilizer(journeyDifficulty,journeyDepth)+" · 晶石 "+FarmGame.ExpeditionCrystals(journeyDifficulty,journeyDepth),13));
        plan.Children.Add(Live(()=>"祝福："+FarmGame.BlessingNames[selectedBlessing],12,U.Green));
        PicnicDeparture(plan);
        if(G.S.PlannedPicnic<0)plan.Children.Add(U.Text("本次消耗：无 · 不带便当",12,U.Green));
        var start=Btn(G.S.PlannedPicnic<0?"出发 · 自动走完整趟":"制作便当并出发",()=>{if(G.BeginJourney(journeyDifficulty,selectedBlessing,journeyDepth)){autoplay=false;confirmRetreat=false;}else if(G.AtHome)G.S.Notice=G.PicnicShortage(G.S.PlannedPicnic);},true,true);updates.Add(()=>start.IsEnabled=G.CanPackPicnic(G.S.PlannedPicnic));FarmTheme.Accent(start);if(WorldGuide)HighlightGuideAction(start);plan.Children.Add(start);
        plan.Children.Add(U.Text("点出发才开始。自动走完这一趟后停下，失败保留途中收获。",11,U.Muted));aside.Children.Add(U.Card(plan));
        aside.Children.Add(Btn(G.S.PlannedPicnic>=0?"更换 / 不带便当":"出门便当 · 可选",OpenPicnic));
        aside.Children.Add(JourneyDecorationCard(journeyDifficulty));
        var bag=new StackPanel();bag.Children.Add(U.Text("旅行信物 · 最多 2 件",16));bag.Children.Add(U.Text(G.RelicLoadout(G.S.EquippedRelics),13,U.Green));bag.Children.Add(Btn("配置信物与查看搭配",()=>OpenHandbook(2)));aside.Children.Add(U.Card(bag));aside.Children.Add(Notice("这套搭配怎样配合",G.PartyAdvice(selectedBlessing)));aside.Children.Add(Btn("查看最近冒险记录",()=>OpenHandbook(3)));
    }
    private bool journeyConfigOpen;
    private void FarmImprovements(StackPanel left,StackPanel right)
    {
        var soil=new StackPanel();soil.Children.Add(U.Text("肥料升级农田",18));soil.Children.Add(Live(()=>"土壤 Lv."+G.S.SoilLevel+" / 5 · 全田生长 +"+(G.S.SoilLevel*12)+"%",13,U.Green));
        soil.Children.Add(Live(()=>"已有肥料 "+G.S.Fertilizer+(G.S.SoilLevel<5?" · 下次需要 "+G.SoilCost:" · 已满级"),12,U.Muted));
        var improve=Btn("施肥升级 · 永久加速",()=>G.UpgradeSoil(),true,true);updates.Add(()=>improve.IsEnabled=G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost);soil.Children.Add(improve);soil.Children.Add(U.Text("每级增加 12% 生长速度。肥料来自冒险；升级对全部田地生效。",12,U.Muted));right.Children.Add(U.Card(soil));
        left.Children.Add(DecorationGoalCard());
    }
}
