using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

// A read-only suggestion derived from real progress, not a second tutorial save.
public sealed class FarmLoopHint
{
    public string Kind,Title,Detail,Next,Label;public int Step,Pet=-1;public bool Enabled=true;
    public static FarmLoopHint Current(FarmGame g)
    {
        if(!g.AtHome){
            var attention=FarmAttention.Current(g);
            return new FarmLoopHint{Kind="journey",Step=2,Title=attention.Title,Detail=attention.Detail,Label=attention.Label,
                Next=g.R.Phase=="result"?"接下来：看完收获返家，用肥料让农田长得更快。":g.R.JourneyPaused?"接下来：到冒险页手动继续，也可以让旅程保持暂停。":"接下来：可以缩成桌宠，回来时再看这趟收获。"};
        }
        if(g.S.SoilLevel<5&&g.S.Fertilizer>=g.SoilCost){double speed=45.0/g.CropSeconds(0);return new FarmLoopHint{Kind="soil",Step=3,Title="把冒险收获，变成更快的农田",Detail="土壤 Lv."+g.S.SoilLevel+" → "+(g.S.SoilLevel+1)+" · 全田生长速度再增加 12%。",Label="施肥升级 · 肥料 "+g.SoilCost,Next="升级后：莓果完整一轮约 "+g.CropSeconds(0).ToString("0.#")+" → "+(45/(speed+.12)).ToString("0.#")+" 秒，当前作物继续生长。"};}
        if(g.S.Wins==0&&g.S.Team.All(p=>g.Level(p)<2)){
            int pet=g.S.Team.Contains(g.S.DisplayPet)?g.S.DisplayPet:g.S.Team[0];int feeds=(int)Math.Ceiling((20-g.S.Xp[pet])/12d);
            string shortage="";if(g.S.Berries<2){double wait=g.CareResourceWait(0,2-g.S.Berries);shortage=wait<0?"当前没种莓果，可到管理农田改种。":"还差 "+(2-g.S.Berries)+" 颗莓果，按当前农田约 "+Math.Ceiling(wait)+" 秒收齐。";}
            return new FarmLoopHint{Kind="feed",Step=1,Pet=pet,Title=feeds>1?"先喂一口，让"+g.Name(pet)+"长大":"再喂一口，就能升到 Lv.2",Detail=shortage!=""?shortage:"农田已经种好，会自动收获、补种，无需浇水。再喂 "+feeds+" 次，就能让"+g.Name(pet)+"升到 Lv.2。",Label="喂"+g.Name(pet)+" · 莓果 2",Enabled=g.CanFeed(pet),Next="接下来：带上伙伴去小溪，自动战斗会带回肥料。"};
        }
        if(g.CanEvolveSeed||g.CanEvolve){int pet=g.CanEvolveSeed?4:0;return new FarmLoopHint{Kind="evolve",Step=1,Pet=pet,Title=g.Name(pet)+"准备好进化了",Detail="等级、亲密与晶石已经齐了，去看看下一阶段的样子与能力。",Label="查看"+g.Name(pet)+"进化",Next="接下来：确认进化后，再去冒险试试新的能力。"};}
        if(g.S.JourneyMemories.Length>0&&g.S.Wins==0&&g.S.Team.Any(p=>g.Level(p)<2)){
            int pet=g.S.Team.OrderBy(p=>g.Level(p)).First();return new FarmLoopHint{Kind="care",Step=1,Pet=pet,Title="让队友也长大一点",Detail=g.Name(pet)+"还是 Lv."+g.Level(pet)+"。用农田收成培养伙伴，下一趟会更有底气。",Label="准备培养"+g.Name(pet),Next="接下来：培养页先看花费，再确认；之后重新挑战小溪。"};
        }
        return new FarmLoopHint{Kind="prepare",Step=2,Title=g.S.Wins==0?"准备好啦，去小溪走一趟":"带着长大的伙伴，再出发",Detail=g.S.Wins==0?"已经有伙伴升到 Lv.2。默认队伍可以直接用，去准备页确认后就能出发。":"农田在自动积攒收成。用现有队伍去冒险，带回肥料、晶石与小院装饰。",Label=g.S.Wins==0?"准备第一次冒险":"准备一趟冒险",Next="接下来：自动旅行约 2～4 分钟，回来后用肥料升级农田。"};
    }
}

public partial class FarmPanel
{
    private bool moreNavigationOpen;
    private string MainCategory()
    {
        if(Tab=="伙伴"||Tab=="伙伴图鉴"||Tab=="培养"||Tab=="配队"||Tab=="进化"||Tab=="试练")return "伙伴";
        if(Tab=="冒险"||Tab=="复盘"||Tab=="便当")return "冒险";
        if(Tab=="农场"||Tab=="农田"||Tab=="小院"||Tab=="归家")return "农场";
        return "";
    }
    private UIElement MoreNavigation()
    {
        var grid=new UniformGrid{Columns=3,Margin=new Thickness(0,8,0,0)};
        string[] labels={"配队","进化","手册","出门便当","归家安排","怎么玩"};
        string[] details={"伙伴、技能与免费试练","形态与进化条件","成长奖励、图鉴与旅行记录","可选的单趟补给","一起核对领奖、施肥与训练","小循环与显示设置"};
        for(int i=0;i<labels.Length;i++){
            int item=i;var b=Btn(labels[i],()=>{moreNavigationOpen=false;if(item==3)OpenPicnic();else if(item==4)OpenHomecoming();else if(item==2)OpenHandbook(0);else Go(labels[item]);resetScroll=true;});
            b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Padding=new Thickness(10,6,10,6);b.Margin=new Thickness(0,0,6,6);
            var p=new StackPanel();p.Children.Add(U.Text(labels[i]+(item==2&&G.ClaimableChapters>0?" · "+G.ClaimableChapters+" 份奖励":""),13));var desc=U.Text(details[i],10,U.Muted);desc.Margin=new Thickness(0);p.Children.Add(desc);b.Content=p;System.Windows.Automation.AutomationProperties.SetName(b,labels[i]);grid.Children.Add(b);
        }
        return grid;
    }
    private void FollowHomeHint(FarmLoopHint shown)
    {
        // A queued click must not spend against a new hint after the screen changes.
        var current=FarmLoopHint.Current(G);if(shown.Kind!=current.Kind||shown.Pet!=current.Pet||shown.Label!=current.Label||!current.Enabled)return;
        if(current.Kind=="feed")G.Feed(current.Pet);
        else if(current.Kind=="soil")UpgradeFromHome();
        else if(current.Kind=="evolve"){evolutionFocusPet=current.Pet;previewSeed=-1;Go("进化");}
        else if(current.Kind=="care")OpenCare(current.Pet,false);
        else if(current.Kind=="journey")Go("冒险");
        else PrepareFromHome();
    }
    private void PrepareFromHome(){if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth)){journeyDifficulty=0;journeyDepth=0;}Go("冒险");resetScroll=true;}
    private UIElement HomeLoopGuide()
    {
        var p=new StackPanel();var rail=new UniformGrid{Columns=4,Margin=new Thickness(0,0,0,10)};p.Children.Add(rail);
        string[] steps={"农田产资源","培养伙伴","自动冒险","肥料升级"};
        for(int i=0;i<4;i++){
            int step=i;var label=U.Text((i+1)+"  "+steps[i]+(i<3?"   ›":"   ↻"),12);label.Margin=new Thickness(0);label.TextAlignment=TextAlignment.Center;
            var segment=new Border{Child=label,CornerRadius=new CornerRadius(8),Padding=new Thickness(3,5,3,5),Margin=new Thickness(0,0,5,0)};rail.Children.Add(segment);
            updates.Add(()=>{bool current=FarmLoopHint.Current(G).Step==step;segment.Background=current?U.Green:U.Pale;label.Foreground=current?FarmTheme.Paper:U.Muted;});
        }
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(214)});p.Children.Add(row);
        var copy=new StackPanel{Margin=new Thickness(0,0,16,0)};row.Children.Add(copy);var eyebrow=U.Text("现在做这一件就好",10,U.Green);eyebrow.Margin=new Thickness(0,0,0,2);copy.Children.Add(eyebrow);
        var title=U.Text("",21);title.Margin=new Thickness(0,0,0,5);copy.Children.Add(title);var detail=U.Text("",12,U.Muted);detail.Margin=new Thickness(0);copy.Children.Add(detail);
        FarmLoopHint shown=FarmLoopHint.Current(G);var actions=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(actions,1);row.Children.Add(actions);
        var primary=Btn(shown.Label,()=>FollowHomeHint(shown),shown.Enabled,true);primary.Padding=new Thickness(7,9,7,9);primary.FontSize=13;primary.Margin=new Thickness(0,0,0,5);System.Windows.Automation.AutomationProperties.SetAutomationId(primary,"home-next-action");FarmTheme.Accent(primary);actions.Children.Add(primary);
        var secondary=Btn("",()=>{if(FarmLoopHint.Current(G).Kind=="feed"&&G.S.Berries<2)Go("农田");else PrepareFromHome();});secondary.FontSize=11;secondary.Padding=new Thickness(5,3,5,3);secondary.Margin=new Thickness(0);actions.Children.Add(secondary);
        var next=U.Text("",11,U.Green);next.Margin=new Thickness(0,9,0,0);System.Windows.Automation.AutomationProperties.SetAutomationId(next,"home-loop-explanation");p.Children.Add(next);
        updates.Add(()=>{shown=FarmLoopHint.Current(G);title.Text=shown.Title;detail.Text=shown.Detail;next.Text=G.AtHome&&homeSoilReceipt!=""&&homeSoilReceiptLevel==G.S.SoilLevel?homeSoilReceipt:shown.Next;primary.Content=shown.Label;primary.IsEnabled=shown.Enabled;secondary.Visibility=G.AtHome&&shown.Kind!="prepare"?Visibility.Visible:Visibility.Collapsed;secondary.Content=shown.Kind=="feed"&&G.S.Berries<2?"管理农田 · 种莓果":"也可以直接去冒险";});
        var card=U.Card(p,12);card.BorderBrush=U.Color("#ABC1A2");return card;
    }
    private void HomeOverview()
    {
        if(SimpleCourtyard){QuietHome();return;}
        if(WorldGuide){GentleGuideHome();return;}
        var cols=Columns(238);var left=cols[0];var right=cols[1];body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(left,1);Grid.SetRow(right,1);
        var guide=HomeLoopGuide();Grid.SetColumnSpan(guide,2);body.Children.Add(guide);
        var field=new StackPanel();var heading=new DockPanel();var decorate=Btn("布置小院",()=>OpenGarden());decorate.Padding=new Thickness(7,3,7,3);decorate.FontSize=11;decorate.Margin=new Thickness(5,0,0,0);DockPanel.SetDock(decorate,Dock.Right);heading.Children.Add(decorate);heading.Children.Add(U.Text("农场在替你忙碌",15));field.Children.Add(heading);
        var scene=Scene();scene.Height=scene.MinHeight=128;scene.Margin=new Thickness(0);field.Children.Add(scene);
        var info=new DockPanel{Margin=new Thickness(0,6,0,0)};var manage=Btn("管理农田",()=>Go("农田"));manage.Padding=new Thickness(8,4,8,4);manage.FontSize=11;manage.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(manage,Dock.Right);info.Children.Add(manage);
        var production=Live(()=>G.Plots+" 块田 · 自动收获与补种\n土壤 Lv."+G.S.SoilLevel+" · 全田生长 +"+(G.S.SoilLevel*12)+"%",11,U.Muted);production.Margin=new Thickness(0);info.Children.Add(production);field.Children.Add(info);left.Children.Add(U.Card(field,10));
        int pet=G.S.DisplayPet;var companion=new StackPanel();var top=new DockPanel();var portrait=Portrait(pet,58);portrait.Width=64;portrait.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(portrait,Dock.Left);top.Children.Add(portrait);var words=new StackPanel();words.Children.Add(U.Text("陪伴你的桌宠",10,U.Muted));var name=Live(()=>G.Name(pet)+"  Lv."+G.Level(pet),16);name.Margin=new Thickness(0);words.Children.Add(name);top.Children.Add(words);companion.Children.Add(top);
        var growth=Live(()=>G.Level(pet)>=10?"等级已满"+(G.NeedsEvolutionBond(pet)?" · 可以补亲密":""):"下一级还需 "+(G.NextXp(pet)-G.S.Xp[pet])+" 成长",11,U.Muted);growth.Margin=new Thickness(0,5,0,3);companion.Children.Add(growth);
        var bar=U.Bar(0,1);updates.Add(()=>{bar.Maximum=G.Level(pet)>=10?1:G.NextXp(pet)-G.LevelStartXp(pet);bar.Value=G.Level(pet)>=10?1:G.S.Xp[pet]-G.LevelStartXp(pet);});companion.Children.Add(bar);
        var feed=FeedButton(pet);feed.FontSize=11;feed.Padding=new Thickness(3,4,3,4);var care=Btn("培养详情",()=>OpenCare(pet));care.FontSize=11;care.Padding=new Thickness(3,4,3,4);U.Row(companion,feed,care);var train=NextLevelButton(pet);train.FontSize=11;train.Padding=new Thickness(3,4,3,4);companion.Children.Add(train);
        if(!G.AtHome){var away=U.Text(G.R.Phase=="result"?"看完收获返家，就能继续培养。":"旅行中，返家后再培养。",10,U.Muted);away.Margin=new Thickness(0);companion.Children.Add(away);}
        right.Children.Add(U.Card(companion,10));
        if(G.CurrentCarePlan!=null){var resume=Btn("查看培养计划",()=>OpenCare());resume.ToolTip=G.Name(G.CurrentCarePlan.Pet)+" · "+G.CareTargetName(G.CurrentCarePlan);resume.FontSize=11;resume.Padding=new Thickness(6,3,6,3);right.Children.Add(resume);}
        var foot=Live(()=>G.LastHarvest,11,U.Muted);foot.Margin=new Thickness(2,0,0,3);left.Children.Add(foot);
        if(G.ClaimableChapters>0){var rewards=Btn("手册有 "+G.ClaimableChapters+" 份成长奖励可领",()=>OpenHandbook(0));rewards.FontSize=11;rewards.Padding=new Thickness(6,3,6,3);left.Children.Add(rewards);}
        var tips=U.Text("想研究组合或进化？在顶部「更多」里。",10,U.Muted);tips.Margin=new Thickness(1,0,0,0);right.Children.Add(tips);
        if(G.LoadWarning!="")left.Children.Add(U.Text(G.LoadWarning,12,U.Gold));
    }
    private void Help()
    {
        var cols=Columns(230);var left=cols[0];var right=cols[1];Heading(left,"只要记住这一个小循环","每次回来，先看农场首页的金色按钮。它会告诉你现在适合做什么。");
        string[] titles={"1  农田会自己产出","2  用收成培养伙伴","3  出门自动冒险","4  带回肥料，升级农田"};
        string[] details={"已经种好，无需浇水或点击收获。想换作物时再打开「管理农田」。","莓果用来喂养，麦穗用来训练。培养是永久的，等级高了可以去更远的地方。","默认队伍可以直接用。到冒险页确认出发，一趟约 2～4 分钟，缩小或隐藏后也会继续。","冒险奖励自动入库。看完收获返家，用肥料加快全田生长，让下一次培养更轻松。"};
        for(int i=0;i<4;i++)left.Children.Add(Notice(titles[i],details[i]));
        right.Children.Add(Btn("回首页，看看现在做什么",()=>Go("农场"),true,true));right.Children.Add(Notice("有空再研究搭配","「更多」里有配队、进化、便当与手册。它们帮助你走得更远，第一次出门不用全部学会。"));
        right.Children.Add(Notice("忙的时候，缩成一只宠物","右上角「缩成宠物」收起面板，双击宠物再打开。「隐藏」后从系统托盘找回。暂停旅程需要在冒险页手动点击。"));
        right.Children.Add(Btn("完整玩法与显示设置",()=>Go("详细规则")));
    }
}
