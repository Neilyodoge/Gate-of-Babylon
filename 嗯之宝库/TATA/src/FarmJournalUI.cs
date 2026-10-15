using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private bool showClaimedChapters;
    private void SelectHandbookPage(int page){if(handbookPage!=page)resetScroll=true;handbookPage=page;}
    private void ChapterPage(StackPanel stack)
    {
        var summary=new StackPanel();var row=new DockPanel();var claim=Btn("",()=>G.ClaimReadyChapters(),G.ClaimableChapters>0,true);FarmTheme.Accent(claim);claim.MinWidth=150;DockPanel.SetDock(claim,Dock.Right);row.Children.Add(claim);
        var title=Live(()=>"成长足迹  "+Enumerable.Range(0,8).Count(G.ChapterDone)+" / 8",18);title.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(title);summary.Children.Add(row);
        updates.Add(()=>{claim.IsEnabled=G.ClaimableChapters>0;claim.Content=G.ClaimableChapters>0?"一键领取 "+G.ClaimableChapters+" 项奖励":"暂无待领取奖励";});
        var bar=U.Bar(0,8);updates.Add(()=>bar.Value=Enumerable.Range(0,8).Count(G.ChapterDone));summary.Children.Add(bar);summary.Children.Add(Live(()=>G.NextChapterHint,12,U.Green));stack.Children.Add(U.Card(summary,12));
        var filters=new DockPanel();var toggle=Btn(showClaimedChapters?"收起已领取":"查看已领取（"+G.S.ChapterClaimed.Count(x=>x)+"）",()=>{showClaimedChapters=!showClaimedChapters;resetScroll=true;});DockPanel.SetDock(toggle,Dock.Right);filters.Children.Add(toggle);filters.Children.Add(U.Text("先领取奖励，再慢慢完成下一件事。",12,U.Muted));stack.Children.Add(filters);
        int[] chapters=Enumerable.Range(0,8).Where(i=>showClaimedChapters||!G.S.ChapterClaimed[i]).OrderBy(i=>G.S.ChapterClaimed[i]?2:G.ChapterDone(i)?0:1).ThenBy(i=>i).ToArray();
        if(chapters.Length==0){stack.Children.Add(Notice("这一页的足迹，都收集好了。","八项成长奖励已领取。去探索更深处，或换一套伙伴组合再玩一趟。"));stack.Children.Add(Btn("去看看探索地图",()=>SelectHandbookPage(1)));return;}
        var grid=new UniformGrid{Columns=2};stack.Children.Add(grid);
        string[] glyphs={"farm","pets","adventure","fertilizer","evolve","crystal","evolve","book"};
        foreach(int id in chapters){int chapter=id;var p=new DockPanel();var action=Btn("",()=>{if(G.ChapterDone(chapter))G.ClaimChapter(chapter);else ChapterAction(chapter);});DockPanel.SetDock(action,Dock.Bottom);p.Children.Add(action);
            var info=new StackPanel();var head=new DockPanel();var icon=new FarmGlyph{Kind=glyphs[id],Width=30,Height=30,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(icon,Dock.Left);head.Children.Add(icon);var label=U.Text((id+1).ToString("00")+"  "+FarmGame.ChapterNames[id],16);head.Children.Add(label);info.Children.Add(head);
            var status=Live(()=>G.S.ChapterClaimed[chapter]?"✓ 已领取":G.ChapterDone(chapter)?"完成啦 · 奖励待领取":"正在成长",11,U.Green);info.Children.Add(status);info.Children.Add(U.Text(FarmGame.ChapterDetails[id],12,U.Muted));info.Children.Add(Live(()=>G.ChapterProgress(chapter),12));
            var rewardRow=new WrapPanel{Margin=new Thickness(0,3,0,8)};string[] resources={"berry","grain","mint","fertilizer","crystal"},names={"莓果","麦穗","薄荷","肥料","晶石"};
            for(int r=0;r<5;r++){int amount=FarmGame.ChapterRewards[id][r];if(amount==0)continue;var chip=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,12,0),ToolTip=names[r]+" +"+amount};chip.Children.Add(new FarmGlyph{Kind=resources[r],Width=20,Height=22,Margin=new Thickness(0,0,5,0)});var text=U.Text("+"+amount,13,U.Gold);text.Margin=new Thickness(0);chip.Children.Add(text);rewardRow.Children.Add(chip);}info.Children.Add(rewardRow);p.Children.Add(info);
            var card=U.Card(p,12);card.Margin=new Thickness(0,0,9,10);grid.Children.Add(card);
            updates.Add(()=>{bool claimed=G.S.ChapterClaimed[chapter],ready=G.ChapterDone(chapter)&&!claimed;action.Content=claimed?"✓ 已领取":ready?"领取奖励":"去完成";action.IsEnabled=!claimed;action.Background=ready?FarmTheme.Honey:FarmTheme.Paper;action.Foreground=ready?FarmTheme.HoneyInk:U.Muted;card.Background=claimed?U.Cream:FarmTheme.Paper;card.BorderBrush=ready?FarmTheme.HoneyEdge:U.Line;});
        }
    }
    private void ExpeditionPage(StackPanel stack)
    {
        stack.Children.Add(U.Text("从初探到秘境，每次首通都会发现一件信物。先养好伙伴，再往深处走。",12,U.Muted));
        if(!G.AtHome){stack.Children.Add(Notice("先完成当前旅程","探索进度可以查看，返家后再选择下一趟。"));stack.Children.Add(Btn("回到当前旅程",()=>Go("冒险")));}
        var regions=new Grid();regions.ColumnDefinitions.Add(new ColumnDefinition());regions.ColumnDefinitions.Add(new ColumnDefinition());stack.Children.Add(regions);
        for(int region=0;region<2;region++){
            int area=region;var column=new StackPanel{Margin=new Thickness(0,0,region==0?10:0,0)};Grid.SetColumn(column,region);regions.Children.Add(column);
            var banner=new StackPanel();banner.Children.Add(new RegionLandscape{Region=region,Height=74,Margin=new Thickness(0,0,0,8)});banner.Children.Add(U.Text(region==0?"01  小溪远足":"02  潮汐洞穴",20));banner.Children.Add(U.Text(region==0?"从家门口出发，带回第一份收获。":"等伙伴进化后，探索更深的回响。",12,U.Muted));column.Children.Add(U.Card(banner,10));
            for(int depth=0;depth<3;depth++){
                int tier=depth,id=region*3+depth;bool open=G.ExpeditionUnlocked(region,depth),cleared=G.S.ExpeditionClears[id]>0;var content=new StackPanel();var row=new DockPanel();var badge=FarmTheme.Badge(cleared?"✓ 已通关":open?"可探索":"待解锁",cleared?U.Pale:open?FarmTheme.Honey:U.Cream,open?U.Green:U.Muted);DockPanel.SetDock(badge,Dock.Right);row.Children.Add(badge);row.Children.Add(U.Text("0"+(depth+1)+"  "+FarmGame.DepthNames[depth],17));content.Children.Add(row);
                content.Children.Add(U.Text("建议 "+FarmGame.ExpeditionLevel(region,depth)+" · 肥料 "+FarmGame.ExpeditionFertilizer(region,depth)+" / 晶石 "+FarmGame.ExpeditionCrystals(region,depth),12,U.Gold));content.Children.Add(U.Text(FarmGame.ExpeditionRule(depth),12,U.Muted));
                var reward=new DockPanel();var icon=new RelicView{Relic=id,Width=32,Height=32,Margin=new Thickness(0,0,7,0),Opacity=open?1:.45};DockPanel.SetDock(icon,Dock.Left);reward.Children.Add(icon);reward.Children.Add(U.Text((G.RelicOwned(id)?"已发现 · ":"首通发现 · ")+FarmGame.RelicNames[id],12,U.Green));content.Children.Add(reward);
                if(!open)content.Children.Add(U.Text(G.ExpeditionGate(region,depth),11,U.Muted));
                var visit=Btn(!open?"尚未开放":!G.AtHome?"旅途中 · 返家后出发":"准备这趟冒险",()=>{SelectJourneyDestination(area,tier);Go("冒险");},open&&G.AtHome);if(open&&!cleared)FarmTheme.Accent(visit);visit.ToolTip=FarmGame.ExpeditionName(region,depth);content.Children.Add(visit);var card=U.Card(content,12);if(!open)card.Background=U.Cream;column.Children.Add(card);
            }
        }
    }
}

public class RegionLandscape:FrameworkElement
{
    public int Region;
    protected override void OnRender(DrawingContext d)
    {
        double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),10,10));d.PushTransform(new ScaleTransform(w/340,h/84));
        bool creek=Region==0;d.DrawRectangle(U.Color(creek?"#E6EED8":"#DDDCE6"),null,new Rect(0,0,340,84));d.DrawEllipse(U.Color(creek?"#EED093":"#F2E7C4"),null,new Point(282,23),13,13);
        d.DrawEllipse(U.Color(creek?"#C6D7AD":"#B4B6CA"),null,new Point(80,91),145,68);d.DrawEllipse(U.Color(creek?"#ADC695":"#8D97B1"),null,new Point(280,99),165,62);
        if(creek){d.DrawGeometry(U.Color("#9BC9CC"),null,Geometry.Parse("M176,42 C128,52 230,59 148,84 L204,84 C262,53 175,47 194,42 Z"));for(int i=0;i<5;i++){double x=24+i*55;d.DrawLine(new Pen(U.Color("#8F795C"),3),new Point(x,68),new Point(x,43));d.DrawEllipse(U.Color("#739B72"),null,new Point(x,40),12,16);}}
        else{d.DrawGeometry(U.Color("#7E8AA1"),null,Geometry.Parse("M65,84 L108,21 L160,14 L201,84 Z"));d.DrawEllipse(U.Color("#4E5D73"),null,new Point(139,80),27,34);for(int i=0;i<3;i++){double x=226+i*22;var gem=Geometry.Parse("M0,24 L6,0 L14,8 L12,28 Z");d.PushTransform(new TranslateTransform(x,56-i*4));d.DrawGeometry(U.Color("#A6CCD1"),new Pen(U.Color("#648E9A"),1),gem);d.Pop();}}
        d.Pop();d.Pop();
    }
}
