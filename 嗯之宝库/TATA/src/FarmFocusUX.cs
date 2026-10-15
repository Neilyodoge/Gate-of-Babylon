using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

public partial class FarmPanel
{
    private bool journeyBlessingsOpen;
    private int focusedPet=-1;
    private string focusedCareReceipt="";
    private UIElement CompactJourneyDestination()
    {
        if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth)){journeyDepth=0;if(!G.ExpeditionUnlocked(journeyDifficulty,0))journeyDifficulty=0;}
        var p=new StackPanel();p.Children.Add(SectionTitle("01  选择目的地","随时可换"));
        var map=new ComboBox{ItemsSource=G.CaveUnlocked?new[]{"小溪远足","潮汐洞穴"}:new[]{"小溪远足"},SelectedIndex=journeyDifficulty,FontSize=12};
        System.Windows.Automation.AutomationProperties.SetName(map,"冒险地区");
        map.SelectionChanged+=(s,e)=>{if(map.SelectedIndex>=0&&map.SelectedIndex!=journeyDifficulty)Do(()=>{SelectJourneyDestination(map.SelectedIndex,0);});};
        int count=Enumerable.Range(0,3).Count(d=>G.ExpeditionUnlocked(journeyDifficulty,d));
        var depth=new ComboBox{ItemsSource=FarmGame.DepthNames.Take(count).ToArray(),SelectedIndex=journeyDepth,FontSize=12};System.Windows.Automation.AutomationProperties.SetName(depth,"冒险深度");
        depth.SelectionChanged+=(s,e)=>{if(depth.SelectedIndex>=0&&depth.SelectedIndex!=journeyDepth)Do(()=>SelectJourneyDestination(journeyDifficulty,depth.SelectedIndex));};U.Row(p,map,depth);
        var recommendation=U.Text("建议 "+FarmGame.ExpeditionLevel(journeyDifficulty,journeyDepth)+" · "+FarmGame.ExpeditionRule(journeyDepth),11,U.Muted);recommendation.Margin=new Thickness(0,5,0,3);p.Children.Add(recommendation);
        var details=Btn("查看全部深度与解锁条件",()=>OpenHandbook(1));details.Padding=new Thickness(4,2,4,2);details.FontSize=10;details.HorizontalAlignment=HorizontalAlignment.Left;p.Children.Add(details);return U.Card(p,10);
    }
    private void SelectFocusedPet(int pet){focusedPet=pet;focusedCareReceipt="";growthReceipt=null;flashUntil=DateTime.MinValue;resetScroll=true;}
    private void CareForFocusedPet(int pet,Func<bool> action)
    {
        if(focusedPet!=pet||!G.AtHome)return;
        int level=G.Level(pet),xp=G.S.Xp[pet],bond=G.PetBond(pet),attack=G.Attack(pet),hp=G.PermanentHp(pet),berries=G.S.Berries,grain=G.S.Grain,mint=G.S.Mint;
        if(!action())return;
        growthReceipt=new GuideGrowthReceipt{State=G.S,Pet=pet,BeforeXp=xp,AfterXp=G.S.Xp[pet],BeforeLevel=level,AfterLevel=G.Level(pet),BeforeAttack=attack,AfterAttack=G.Attack(pet),BeforeHp=hp,AfterHp=G.PermanentHp(pet)};
        if(G.S.Berries<berries)ShowCompanionMoment(pet,"feed",G.Level(pet)>level?"吃饱啦，又长大一点！":"啊呜，莓果真甜。");
        focusedCareReceipt=(level==G.Level(pet)?(G.S.Xp[pet]>xp?"成长 +"+(G.S.Xp[pet]-xp):"等级已满")+(G.PetBond(pet)>bond?" · 亲密 +"+(G.PetBond(pet)-bond):""):"Lv."+level+" → Lv."+G.Level(pet)+" · 攻击 "+attack+" → "+G.Attack(pet)+" · 体力 "+hp+" → "+G.PermanentHp(pet))+
            "\n消耗"+(berries!=G.S.Berries?"莓果 "+(berries-G.S.Berries):grain!=G.S.Grain?"麦穗 "+(grain-G.S.Grain):"薄荷 "+(mint-G.S.Mint))+(G.GuideTest?"，仅本次有效。":"，已保存。");
    }
    private string FocusedRole(int pet)
    {
        int slot=Array.IndexOf(G.S.Team,pet);return slot>=0?"出战第 "+(slot+1)+" 位 · "+(slot==0?"前排承伤":"后排行动"):pet==G.Caretaker?"留守农场 · "+G.CaretakerBonus(pet):"在家休息 · 可在配队中安排出战";
    }
    private void Pets()
    {
        if(SimpleCourtyard){QuietPet();return;}
        if(WorldGuide){GentleGuidePet();return;}
        if(focusedPet<0||focusedPet>=FarmGame.PetCount)focusedPet=G.S.DisplayPet;
        int pet=focusedPet;var cols=Columns(242);var left=cols[0];var right=cols[1];
        body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(left,1);Grid.SetRow(right,1);
        var top=new StackPanel();Grid.SetColumnSpan(top,2);body.Children.Add(top);Heading(top,WorldGuide?G.S.GuideStep==3?"长大了，去小溪试试身手":"陪伙伴准备第一次远足":"先选一位，再陪它长大",WorldGuide?G.S.GuideStep==3&&G.Level(pet)>=2?G.Name(pet)+"：感觉更有力气了，去小溪试试吧。":"这里就是平时培养伙伴的地方。先喂养，长到 Lv.2 再出发。":"点头像只查看伙伴；桌面展示和出战安排单独设置。");
        var picker=new UniformGrid{Columns=FarmGame.PetCount,Margin=new Thickness(0,0,0,9)};top.Children.Add(picker);
        for(int i=0;i<FarmGame.PetCount;i++){
            int choice=i;var content=new StackPanel();content.Children.Add(Portrait(i,37));var name=U.Text(G.Name(i)+"  Lv."+G.Level(i),11,i==pet?FarmTheme.Paper:U.Ink);name.Margin=new Thickness(0);name.TextAlignment=TextAlignment.Center;content.Children.Add(name);
            var b=Btn("查看"+G.Name(i),()=>SelectFocusedPet(choice),true,i==pet);b.Content=content;b.Padding=new Thickness(3,4,3,4);b.Margin=new Thickness(0,0,6,0);System.Windows.Automation.AutomationProperties.SetName(b,"查看伙伴 "+G.Name(i));picker.Children.Add(b);
        }
        var hero=new StackPanel();var row=new DockPanel();var portrait=Portrait(pet,78);portrait.Width=86;portrait.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(portrait,Dock.Left);row.Children.Add(portrait);var words=new StackPanel();words.Children.Add(U.Text(G.Name(pet)+"  Lv."+G.Level(pet),20));words.Children.Add(PetTags(pet));words.Children.Add(U.Text("攻击 "+G.Attack(pet)+" · 体力 "+G.PermanentHp(pet),12,U.Green));row.Children.Add(words);hero.Children.Add(row);
        hero.Children.Add(U.Text(FocusedRole(pet),11,U.Muted));
        var display=Btn(G.S.DisplayPet==pet?"✓ 桌面展示中":"展示为桌宠",()=>owner.SelectDisplayPet(pet),G.S.DisplayPet!=pet);display.Padding=new Thickness(5,4,5,4);hero.Children.Add(display);
        if(focusedCareReceipt!=""){var receipt=U.Text(focusedCareReceipt,11,U.Green);receipt.Margin=new Thickness(0,6,0,3);System.Windows.Automation.AutomationProperties.SetAutomationId(receipt,"focused-care-receipt");hero.Children.Add(receipt);}
        var onward=Btn(G.AtHome?"去冒险验证成长":G.R.Phase=="result"?"查看收获并返家":"查看当前冒险",()=>Go("冒险"));onward.Padding=new Thickness(5,4,5,4);if(WorldGuide&&G.S.GuideStep==3)HighlightGuideAction(onward);hero.Children.Add(onward);
        var heroCard=U.Card(hero,10);heroCard.Background=FarmTheme.ElementPaper[FarmGame.Elements[pet]];left.Children.Add(heroCard);
        var ability=new StackPanel();ability.Children.Add(U.Text("它在战斗里做什么",15));ability.Children.Add(U.Text(FarmUX.PetSkill(G,pet),12,U.Muted));ability.Children.Add(U.Text("当前技能："+FarmGame.SkillNames[pet][G.S.Skills[pet]],12,U.Green));left.Children.Add(U.Card(ability,10));
        var actions=new StackPanel();actions.Children.Add(U.Text(G.AtHome?"用农田收成培养":"返家后再培养",17));
        actions.Children.Add(U.Text(G.Level(pet)<10?"距 Lv."+(G.Level(pet)+1)+" 还需 "+(G.NextXp(pet)-G.S.Xp[pet])+" 成长":"等级已满"+(G.NeedsEvolutionBond(pet)?" · 可喂养补亲密":""),12,U.Muted));actions.Children.Add(G.Level(pet)<10?U.Bar(G.S.Xp[pet]-G.LevelStartXp(pet),G.NextXp(pet)-G.LevelStartXp(pet)):U.Bar(1,1));
        bool guidedFeed=WorldGuide;var feedSource=G.S;int feedStep=G.S.GuideStep;var feed=Btn(FarmUX.FeedLabel(G,pet),()=>{if(!guidedFeed||Object.ReferenceEquals(feedSource,G.S)&&G.S.GuideStep==feedStep)CareForFocusedPet(pet,()=>G.Feed(pet));},G.CanFeed(pet),!WorldGuide||G.S.GuideStep==2);if(!WorldGuide||G.S.GuideStep==2)FarmTheme.Accent(feed);if(WorldGuide&&G.S.GuideStep==2)HighlightGuideAction(feed);actions.Children.Add(feed);updates.Add(()=>feed.IsEnabled=G.CanFeed(pet));
        var feedHint=Live(()=>!G.AtHome?"先看收获并返家，再培养。":G.S.Berries<2?CareWaitLabel(0,2):G.Level(pet)>=10?FarmUX.FeedHint(G,pet):"成长 +12"+((pet==0||pet==4)?" · 亲密 +1":""),11,U.Muted);actions.Children.Add(feedHint);
        if(WorldGuide&&G.AtHome&&G.S.Berries<2)actions.Children.Add(Btn("去农田看看莓果",()=>Go("农田")));
        int targetLevel=G.Level(pet)+1;var train=Btn("",()=>{if(G.Level(pet)+1==targetLevel)CareForFocusedPet(pet,()=>G.TrainToNext(pet));});actions.Children.Add(train);updates.Add(()=>{int cost=G.TrainToNextCost(pet);train.Content=cost>0?"升至 Lv."+(G.Level(pet)+1)+" · 麦穗 "+cost:"等级已满";train.IsEnabled=G.AtHome&&cost>0&&G.S.Grain>=cost;});
        actions.Children.Add(Live(()=>!G.AtHome?"本趟冒险结束后恢复培养。":G.Level(pet)>=10?"不再消耗麦穗。":G.S.Grain<G.TrainToNextCost(pet)?CareWaitLabel(1,G.TrainToNextCost(pet)):"一次补到下一级，成长永久保留。",11,U.Muted));
        var plan=Btn("培养计划与材料",()=>OpenCare(pet,false));plan.Padding=new Thickness(5,4,5,4);actions.Children.Add(plan);right.Children.Add(U.Card(actions,10));
        var options=new StackPanel();SkillPicker(options,pet);options.Children.Add(U.Text("站位、出战和农场分工在配队页集中设置。",11,U.Muted));options.Children.Add(Btn("调整队伍与分工",()=>Go("配队")));var singleTrain=Btn("训练 · 麦穗 3",()=>CareForFocusedPet(pet,()=>G.Train(pet)));updates.Add(()=>singleTrain.IsEnabled=G.AtHome&&G.S.Grain>=3&&G.Level(pet)<10);options.Children.Add(singleTrain);
        if(G.S.GardenUnlocked){var mint=Btn("薄荷点心 · 薄荷 1 / 成长 +24",()=>CareForFocusedPet(pet,()=>G.HerbalFeed(pet)));updates.Add(()=>mint.IsEnabled=G.AtHome&&G.S.Mint>=1&&G.Level(pet)<10);options.Children.Add(mint);}
        if(G.HasNextEvolution(pet))options.Children.Add(Btn("查看下一次进化与材料",()=>OpenCare(pet,true)));
        var more=new Expander{Header="技能、站位与更多培养",Content=options,IsExpanded=petDetailsOpen[pet]};more.Expanded+=(s,e)=>petDetailsOpen[pet]=true;more.Collapsed+=(s,e)=>petDetailsOpen[pet]=false;left.Children.Add(more);
        left.Children.Add(Btn("全部伙伴与属性筛选",()=>Go("伙伴图鉴")));left.Children.Add(Btn("查看进化图谱",()=>{evolutionFocusPet=pet==0?0:4;Go("进化");}));
    }
}
