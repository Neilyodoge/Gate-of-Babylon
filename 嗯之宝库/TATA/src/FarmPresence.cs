using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed class FarmAttention
{
    public string Kind,Title,Detail,Label,Tab,Mark;public int Pet=-1;
    private static FarmAttention Make(string kind,string title,string detail,string label,string tab,string mark,int pet=-1){return new FarmAttention{Kind=kind,Title=title,Detail=detail,Label=label,Tab=tab,Mark=mark,Pet=pet};}
    public static FarmAttention Current(FarmGame g)
    {
        if(g.C.Serial>0){bool done=g.C.Phase=="result";return Make("cards",done?"这一趟结束啦":"远征停在第 "+(g.C.Stage+1)+" / 5 场",done?g.C.Outcome:"手牌和体力已保留，等你回来继续出牌。",done?"看看这趟足迹":"继续卡牌远征","卡牌冒险",done?"✓":"Ⅱ");}
        if(!g.GuideActive&&g.S.SimpleCourtyard&&g.AtHome)return Make("farm",g.Name(g.S.DisplayPet)+"陪着你","莓果在慢慢长，有空再来看看。","回小院","农场","");
        if(g.GuideActive&&g.AtHome&&g.S.GuideStep<=3)return Make("farm","泡泡陪着你",g.S.GuideStep==0?"有空来喂一口莓果吧。":"农田会自己长，有空再来看看伙伴。","看看伙伴","新手引导","");
        if(g.R.Phase=="result")return Make("result","伙伴回来啦",g.R.Outcome=="victory"?"这趟带回肥料 "+g.R.RewardFertilizer+"、晶石 "+g.R.RewardCrystals+"，已放入仓库。":"小队已返家，途中收获保留。看看战报，再培养伙伴。","查看本趟收获","冒险","✓");
        if(!g.AtHome){
            if(!g.R.Casual)return Make("legacy","旧旅程等你接着走","当前进度已保留，打开冒险页可以接着自动走完。","查看当前旅程","冒险","…");
            if(g.R.JourneyPaused)return Make("paused","旅程已暂停","点开冒险页，再按继续；缩小或隐藏不会解除暂停。","查看暂停的旅程","冒险","Ⅱ");
            if(g.R.Phase=="inspiration")return Make("travel","发现旅途灵感", "小队会按「"+FarmGame.InspirationStyles[g.R.InspirationStyle]+"」自动挑选；有空也能亲自选。","看看这次灵感","冒险",(g.R.Node+1).ToString());
            return Make("travel","旅行中 "+(g.R.Node+1)+" / 5",g.RouteName+" · 小队会自动完成这一趟，农田也在生长。","看看旅行进度","冒险",(g.R.Node+1).ToString());
        }
        if(g.CanEvolveSeed)return Make("evolve","种子可以进化了","等级、亲密与晶石已经齐了，去迎接「"+FarmGame.SeedNames[g.S.SeedStage+1]+"」。","去看种子进化","进化","↑",4);
        if(g.CanEvolve)return Make("evolve","泡泡可以进化了","消耗晶石 2，迎接潮汐兽与新的旅途。","去看泡泡进化","进化","↑",0);
        if(g.ClaimableChapters>0){string ready=String.Join("、",Enumerable.Range(0,FarmGame.ChapterNames.Length).Where(i=>!g.S.ChapterClaimed[i]&&g.ChapterDone(i)).Take(3).Select(i=>FarmGame.ChapterNames[i]).ToArray());return Make("chapter","有 "+g.ClaimableChapters+" 份成长奖励","已完成："+ready+(g.ClaimableChapters>3?"等":"")+"。打开手册即可领取。","去手册领取奖励","手册","礼");}
        return Make("farm","农田在慢慢长大",g.NextChapterHint,"准备一趟冒险","冒险","");
    }
}

public partial class FarmWindow
{
    private Button compactStatus,compactBadge;
    private void AddCompactPresence(Grid root,StackPanel tools)
    {
        compactStatus=U.Button("",OpenAttention);compactStatus.Height=15;compactStatus.FontSize=10;compactStatus.Padding=new Thickness(0);compactStatus.Margin=new Thickness(0);compactStatus.BorderThickness=new Thickness(0);compactStatus.Background=U.Cream;tools.Children.Add(compactStatus);
        compactBadge=U.Button("",OpenAttention);compactBadge.Width=compactBadge.Height=20;compactBadge.FontSize=11;compactBadge.Padding=new Thickness(0);compactBadge.Margin=new Thickness(0,45,5,0);compactBadge.HorizontalAlignment=HorizontalAlignment.Right;compactBadge.VerticalAlignment=VerticalAlignment.Top;root.Children.Add(compactBadge);RefreshPresence();
    }
    private void RefreshPresence()
    {
        var state=FarmAttention.Current(G);
        if(compactStatus!=null){compactStatus.Content=state.Title+" ›";compactStatus.ToolTip=state.Detail+"\n点击"+state.Label;}
        if(compactBadge!=null){compactBadge.Visibility=G.S.DesktopStatusHints&&state.Kind!="farm"?Visibility.Visible:Visibility.Collapsed;compactBadge.Content=state.Mark;compactBadge.Background=state.Kind=="travel"?U.Pale:FarmTheme.Honey;compactBadge.Foreground=state.Kind=="travel"?U.Green:FarmTheme.HoneyInk;compactBadge.ToolTip=state.Title+"\n"+state.Detail+"\n点击"+state.Label;System.Windows.Automation.AutomationProperties.SetName(compactBadge,state.Title+"，"+state.Label);}
        if(tray!=null){string text="苔间 · "+state.Title+" · 点击恢复 / 右键更多";tray.Text=text.Length<=63?text:text.Substring(0,63);}
    }
    private void OpenAttention()
    {
        var state=FarmAttention.Current(G);
        if(testing){if(Panel==null)Panel=new FarmPanel(this);}
        else OpenPanel(state.Tab);
        Panel.OpenAttention(state);Panel.Render();
    }
}

public partial class FarmPanel
{
    private int evolutionFocusPet=4;
    internal void OpenAttention(FarmAttention state)
    {
        if(state.Kind=="chapter")OpenHandbook(0);
        else{if(state.Kind=="evolve"){evolutionFocusPet=state.Pet;previewSeed=-1;}Go(state.Tab);resetScroll=true;}
    }
    private UIElement HomeNextStep()
    {
        var p=new StackPanel();p.Children.Add(U.Text("现在可以做",11,U.Muted));var title=U.Text("",18);p.Children.Add(title);var detail=U.Text("",12,U.Muted);p.Children.Add(detail);
        var action=Btn("",()=>{var state=FarmAttention.Current(G);if(state.Kind=="farm"&&G.AtHome&&G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost)G.UpgradeSoil();else OpenAttention(state);},true,true);FarmTheme.Accent(action);p.Children.Add(action);
        updates.Add(()=>{var state=FarmAttention.Current(G);bool soil=state.Kind=="farm"&&G.AtHome&&G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost;title.Text=soil?"给农田添一点养分":state.Title;detail.Text=soil?"全田生长速度再增加 12%，下一份收成来得更快。":state.Detail;action.Content=soil?"施肥升级 · 肥料 "+G.SoilCost:state.Label;});
        var secondary=Btn("",()=>{if(FarmAttention.Current(G).Kind=="chapter")Go("冒险");else OpenHandbook(0);});updates.Add(()=>secondary.Content=FarmAttention.Current(G).Kind=="chapter"?"准备一趟冒险":"成长手册与奖励");p.Children.Add(secondary);return U.Card(p,12);
    }
}

