using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private int elementFilter=-1,dutyFilter=-1,previewSeed=-1;
    private List<Tuple<int,Image>> seedPreviews=new List<Tuple<int,Image>>();
    private Button FeedButton(int pet,bool primary=false)
    {
        var feed=Btn(FarmUX.FeedLabel(G,pet),()=>G.Feed(pet),G.CanFeed(pet),primary);
        updates.Add(()=>{feed.Content=FarmUX.FeedLabel(G,pet);feed.IsEnabled=G.CanFeed(pet);feed.ToolTip=FarmUX.FeedHint(G,pet);});return feed;
    }
    private Button NextLevelButton(int pet)
    {
        var train=Btn("",()=>G.TrainToNext(pet));updates.Add(()=>{int cost=G.TrainToNextCost(pet);train.Content=cost>0?"升至 Lv."+(G.Level(pet)+1)+" · 麦穗 "+cost:"等级已满";train.IsEnabled=G.AtHome&&cost>0&&G.S.Grain>=cost;train.ToolTip=!G.AtHome?"返家后可培养。":cost==0?"已达到 Lv.10。":"一次完成到下一级所需训练，不增加亲密。"+(G.S.Grain<cost?" 还差麦穗 "+(cost-G.S.Grain)+"。":"");});return train;
    }
    private Image PetIcon(string name,double size=23){return new Image{Source=Art.Icon(name),Width=size,Height=size,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,5,0)};}
    private UIElement PetTags(int pet)
    {
        var row=new WrapPanel{Margin=new Thickness(0,0,0,6)};
        row.Children.Add(PetIcon(FarmGame.ElementIcons[FarmGame.Elements[pet]]));row.Children.Add(U.Text(FarmGame.ElementNames[FarmGame.Elements[pet]]+"系",12,U.Muted));
        var spacer=new Border{Width=10};row.Children.Add(spacer);row.Children.Add(PetIcon(FarmGame.DutyIcons[FarmGame.Duties[pet]]));row.Children.Add(U.Text(FarmGame.DutyNames[FarmGame.Duties[pet]],12,U.Muted));return row;
    }
    private void PetFilters(StackPanel parent)
    {
        foreach(bool element in new[]{false,true}){
            string[] labels=element?FarmGame.ElementNames:FarmGame.DutyNames,icons=element?FarmGame.ElementIcons:FarmGame.DutyIcons;
            var row=new WrapPanel{Margin=new Thickness(0,0,0,4)};parent.Children.Add(row);
            var all=Btn("全部"+(element?"属性":"职责"),()=>{if(element)elementFilter=-1;else dutyFilter=-1;},true,(element?elementFilter:dutyFilter)<0);all.Margin=new Thickness(0,0,5,4);row.Children.Add(all);
            for(int i=0;i<labels.Length;i++){int id=i;var button=Btn(labels[i],()=>{if(element)elementFilter=id;else dutyFilter=id;},true,(element?elementFilter:dutyFilter)==i);var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(PetIcon(icons[i],22));var label=U.Text(labels[i],12,(element?elementFilter:dutyFilter)==i?Brushes.White:U.Ink);System.Windows.Automation.AutomationProperties.SetName(button,labels[i]);label.Margin=new Thickness(0);label.VerticalAlignment=VerticalAlignment.Center;content.Children.Add(label);button.Content=content;button.Padding=new Thickness(9,5,9,5);button.Margin=new Thickness(0,0,5,4);row.Children.Add(button);}
        }
    }
    private bool petFiltersOpen;
    private bool[] petDetailsOpen=new bool[FarmGame.PetCount];
    private void PetCatalogue()
    {
        var stack=new StackPanel();body.Children.Add(stack);
        stack.Children.Add(Btn("返回伙伴培养",()=>Go("伙伴")));
        Heading(stack,"伙伴图鉴","喂养、成长、一起出发。选 3 位冒险，留 1 位照料农田。");
        U.Row(stack,Btn("查看进化图谱",()=>Go("进化")),Btn("配队与全队训练",()=>Go("配队")));
        var filters=new StackPanel();PetFilters(filters);
        var filterBox=new Expander{Header="按属性 / 职责筛选"+(elementFilter>=0||dutyFilter>=0?" · 已筛选":""),Content=filters,IsExpanded=petFiltersOpen||elementFilter>=0||dutyFilter>=0};filterBox.Expanded+=(s,e)=>petFiltersOpen=true;filterBox.Collapsed+=(s,e)=>petFiltersOpen=false;stack.Children.Add(filterBox);
        if(!G.AtHome)stack.Children.Add(Notice("伙伴正在旅行","返家后可培养、进化和调整配置。桌面展示仍可切换。"));
        var shown=G.S.Team.Concat(Enumerable.Range(0,FarmGame.PetCount).Where(p=>!G.S.Team.Contains(p))).Where(p=>(elementFilter<0||FarmGame.Elements[p]==elementFilter)&&(dutyFilter<0||FarmGame.Duties[p]==dutyFilter)).ToArray();
        stack.Children.Add(U.Text("显示 "+shown.Length+" / "+FarmGame.PetCount+" 位 · 队伍按站位依次行动",11,U.Muted));
        if(shown.Length==0){stack.Children.Add(Notice("没有符合条件的伙伴","展开筛选，选择「全部职责」和「全部属性」可恢复全部伙伴。"));return;}
        var grid=new UniformGrid{Columns=shown.Length==1?1:2};stack.Children.Add(grid);
        foreach(int id in shown){
            int p=id,slot=Array.IndexOf(G.S.Team,p);var content=new StackPanel();
            var head=new DockPanel();var portrait=Portrait(p,84);portrait.Width=94;portrait.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(portrait,Dock.Left);head.Children.Add(portrait);
            var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var name=U.Text(G.Name(p)+"  Lv."+G.Level(p),18);name.Margin=new Thickness(0,0,5,0);info.Children.Add(name);info.Children.Add(PetTags(p));
            info.Children.Add(FarmTheme.Badge(slot>=0?"出战 · "+(slot+1)+" 号"+(slot==0?"前排":"后排"):p==G.Caretaker?"农场小帮手":"在家休息",FarmTheme.Paper));head.Children.Add(info);
            content.Children.Add(new Border{Background=FarmTheme.ElementPaper[FarmGame.Elements[p]],CornerRadius=new CornerRadius(15,15,0,0),Padding=new Thickness(12,9,12,9),Child=head});
            var inside=new StackPanel{Margin=new Thickness(12,10,12,10)};content.Children.Add(inside);
            inside.Children.Add(U.Text(G.Level(p)>=10?"等级已满"+(G.NeedsEvolutionBond(p)?" · 可喂养补亲密":""):"距下一级还需 "+(G.NextXp(p)-G.S.Xp[p])+" 成长",12,U.Muted));inside.Children.Add(G.Level(p)>=10?U.Bar(1,1):U.Bar(G.S.Xp[p]-G.LevelStartXp(p),G.NextXp(p)-G.LevelStartXp(p)));
            var feed=FeedButton(p,true);var train=Btn("训练 · 麦穗 3",()=>G.Train(p));train.ToolTip="成长 +18，不增加亲密";updates.Add(()=>train.IsEnabled=G.AtHome&&G.S.Grain>=3&&G.Level(p)<10);U.Row(inside,feed,train);inside.Children.Add(NextLevelButton(p));
            inside.Children.Add(Btn("培养计划与材料",()=>OpenCare(p)));var display=Btn(G.S.DisplayPet==p?"✓ 桌面展示中":"展示为桌宠",()=>owner.SelectDisplayPet(p),G.S.DisplayPet!=p);inside.Children.Add(display);
            var details=new StackPanel();details.Children.Add(U.Text(FarmUX.PetSkill(G,p),12,U.Muted));SkillPicker(details,p);
            if(slot>=0)U.Row(details,Btn("换上「"+G.Name(G.Caretaker)+"」",()=>G.Swap(p),G.AtHome),Btn(slot==0?"已在最前方":"移到第 1 位",()=>G.MoveFirst(p),G.AtHome&&slot>0));
            else{
                var join=new ComboBox{FontSize=12,ItemsSource=G.S.Team.Select((q,i)=>"替换第 "+(i+1)+" 位 · "+G.Name(q)).ToArray(),SelectedIndex=1,Margin=new Thickness(0,4,4,4)};
                U.Row(details,join,Btn("加入队伍",()=>G.JoinTeam(p,join.SelectedIndex),G.AtHome));details.Children.Add(Btn(p==G.Caretaker?"✓ 照料农田中":"负责照料农田",()=>G.SetCaretaker(p),G.AtHome&&p!=G.Caretaker));details.Children.Add(U.Text(G.CaretakerBonus(p),11,U.Muted));
            }
            if(G.S.GardenUnlocked){var mint=Btn("薄荷点心 · 薄荷 1 / 成长 +24",()=>G.HerbalFeed(p));updates.Add(()=>mint.IsEnabled=G.AtHome&&G.S.Mint>=1&&G.Level(p)<10);details.Children.Add(mint);}
            if(p==0||p==4)details.Children.Add(Btn(p==4?"四阶进化 · 当前 "+(G.S.SeedStage+1)+" / 4":"查看泡泡进化",()=>{previewSeed=-1;evolutionFocusPet=p;Go("进化");}));
            details.Children.Add(U.Text(G.Level(p)>=10?FarmUX.FeedHint(G,p):"喂养成长 +12；训练成长 +18"+(p==0||p==4?"；喂养亲密 +1":""),11,U.Muted));
            var more=new Expander{Header="技能、站位与更多培养",Content=details,IsExpanded=petDetailsOpen[p]};more.Expanded+=(s,e)=>petDetailsOpen[p]=true;more.Collapsed+=(s,e)=>petDetailsOpen[p]=false;inside.Children.Add(more);
            var card=U.Card(content,0);card.Margin=new Thickness(0,0,9,12);grid.Children.Add(card);
        }
    }
    private Image SeedPreview(int stage,double size){var im=new Image{Source=Art.Frame(4,owner.Tick,false,stage),Width=size,Height=size,Stretch=Stretch.Uniform};seedPreviews.Add(Tuple.Create(stage,im));return im;}
    private void EvolutionPage()
    {
        if(SimpleCourtyard){SmallEvolutionPage();return;}
        var columns=Columns(246);var left=columns[0];var aside=columns[1];
        U.Row(left,Btn("泡泡进化",()=>SelectEvolutionFamily(0),true,evolutionFocusPet==0),Btn("种子进化",()=>SelectEvolutionFamily(4),true,evolutionFocusPet!=0));
        if(evolutionFocusPet==0){left.Children.Add(EvolutionCard());left.Children.Add(Notice("进化是永久成长","泡泡进化后，开场让全体敌人潮湿；全田生长速度增加 15%，并开放潮汐洞穴。培养与进化分别确认。"));BubbleEvolutionAside(aside);return;}
        Heading(left,"一颗种子，四种形态","同一只宠物逐阶进化，等级与队伍位置保留。点击形态卡可预览动画。");
        var stages=new UniformGrid{Columns=2};left.Children.Add(stages);int selected=previewSeed<0?G.S.SeedStage:previewSeed;
        for(int i=0;i<4;i++){int stage=i;var p=new StackPanel();p.Children.Add(FarmTheme.Badge("0"+(i+1)+" / 04"+(i==G.S.SeedStage?"  ·  现在的我":""),FarmTheme.Paper,i==G.S.SeedStage?U.Green:U.Muted));p.Children.Add(SeedPreview(i,66));var title=U.Text(FarmGame.SeedNames[i],15);title.TextAlignment=TextAlignment.Center;p.Children.Add(title);p.Children.Add(Btn(i==G.S.SeedStage?"当前形态 · 预览":i<G.S.SeedStage?"已经历 · 预览":"未来形态 · 预览",()=>previewSeed=stage,true,selected==i));var c=U.Card(p,10);c.Background=selected==i?FarmTheme.ElementPaper[2]:FarmTheme.Paper;c.BorderBrush=selected==i?U.Green:U.Line;c.Margin=new Thickness(0,0,8,9);stages.Children.Add(c);}
        left.Children.Add(Notice("预览 · "+FarmGame.SeedNames[selected],FarmGame.SeedChanges[selected]+(selected==G.S.SeedStage?"":" 预览不会改变当前宠物。")));
        left.Children.Add(U.Text("每次进化：攻击 +2、体力 +6。进化能力逐阶累积；已经培养的等级与技能继续保留。",12,U.Muted));
        aside.Children.Add(U.Text("当前 · "+G.Name(4),17));aside.Children.Add(PetTags(4));aside.Children.Add(U.Text("Lv."+G.Level(4)+" · 亲密 "+G.S.SeedBond+" · 第 "+(G.S.SeedStage+1)+" / 4 阶",13,U.Green));
        if(G.S.SeedStage<3){int stageBefore=G.S.SeedStage;var needs=new StackPanel();needs.Children.Add(U.Text("距离下一次进化",16));needs.Children.Add(Live(()=>(G.Level(4)>=G.SeedRequiredLevel?"✓ ":"○ ")+"等级  "+G.Level(4)+" / "+G.SeedRequiredLevel,12,U.Green));needs.Children.Add(Live(()=>(G.S.SeedBond>=G.SeedRequiredBond?"✓ ":"○ ")+"亲密  "+G.S.SeedBond+" / "+G.SeedRequiredBond,12,U.Green));needs.Children.Add(Live(()=>(G.S.Crystals>=G.SeedEvolutionCost?"✓ ":"○ ")+"晶石  "+G.S.Crystals+" / "+G.SeedEvolutionCost,12,U.Green));var evolve=Btn("进化为「"+FarmGame.SeedNames[G.S.SeedStage+1]+"」",()=>{if(G.S.SeedStage!=stageBefore||evolutionFocusPet!=4)return;string before=G.Name(4);int cost=G.SeedEvolutionCost;if(G.EvolveSeed()){previewSeed=-1;evolutionReceipt=before+" → "+G.Name(4)+"\n晶石 −"+cost+" · 等级与站位保留。";}},G.CanEvolveSeed,true);FarmTheme.Accent(evolve);updates.Add(()=>evolve.IsEnabled=G.CanEvolveSeed);needs.Children.Add(evolve);aside.Children.Add(U.Card(needs,10));}
        if(evolutionReceipt!="")aside.Children.Add(U.Text(evolutionReceipt,12,U.Green));
        aside.Children.Add(Live(()=>G.SeedEvolutionHint,12,U.Muted));
        aside.Children.Add(Btn(G.HasNextEvolution(4)?"准备种子进化材料":"继续培养种子",()=>OpenCare(4,G.HasNextEvolution(4))));
        var feed=FeedButton(4);var train=Btn("训练 · 麦穗 3",()=>G.Train(4));train.ToolTip="成长 +18，不增加亲密";updates.Add(()=>train.IsEnabled=G.AtHome&&G.S.Grain>=3&&G.Level(4)<10);U.Row(aside,feed,train);
        aside.Children.Add(Live(()=>FarmUX.FeedHint(G,4),12,U.Muted));
        aside.Children.Add(NextLevelButton(4));
        aside.Children.Add(Btn("去冒险获取晶石",()=>Go("冒险")));aside.Children.Add(Btn("返回伙伴培养",()=>{SelectFocusedPet(4);Go("伙伴");}));
    }
    internal void RenderSeedRosterCheck(string root)
    {
        Tab="伙伴图鉴";elementFilter=2;dutyFilter=2;Render();FarmHeadlessTests.Render(this,System.IO.Path.Combine(root,"pets-seed-filtered.png"),924,721);
        elementFilter=-1;dutyFilter=0;Render();FarmHeadlessTests.Render(this,System.IO.Path.Combine(root,"pets-empty-filter.png"),764,571);
        elementFilter=dutyFilter=-1;
    }
}
