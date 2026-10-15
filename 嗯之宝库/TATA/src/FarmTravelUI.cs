using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private bool travelReportOpen;
    private UIElement TravelTrack()
    {
        if(GentleJourney)return GentleTravelProgress();
        var steps=new UniformGrid{Columns=5,Margin=new Thickness(0,0,0,10)};
        string[] names={"出发","林间","营地","关口","首领"};
        for(int i=0;i<5;i++){
            bool done=i<G.R.Node,current=i==G.R.Node;var p=new StackPanel();
            var number=U.Text(done?"✓":(i+1).ToString("00"),15,current?FarmTheme.Paper:U.Green);number.Margin=new Thickness(0);number.TextAlignment=TextAlignment.Center;
            p.Children.Add(new Border{Child=number,Background=current?U.Green:done?U.Pale:FarmTheme.Paper,CornerRadius=new CornerRadius(9),Padding=new Thickness(0,4,0,4)});
            var name=U.Text(names[i],11,current?U.Ink:U.Muted);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0,4,0,0);p.Children.Add(name);
            p.Margin=new Thickness(0,0,7,0);steps.Children.Add(p);
        }
        return steps;
    }
    private string TravelStatus(){return G.R.JourneyPaused?"旅程已暂停":G.R.Phase=="inspiration"?"挑选旅途灵感":G.R.Phase=="battle"?"自动战斗中":G.R.Node==2?"在营地休息":"正在前往下一站";}
    private void JourneyPage(StackPanel left,StackPanel aside)
    {
        if(G.R.Phase=="result"){JourneyResult(left,aside);return;}if(G.R.Phase=="inspiration"){InspirationChoicePage(left,aside);return;}
        Heading(left,GentleJourney?G.R.JourneyPaused?"先在这里歇一会儿":G.R.Phase=="battle"?"遇到小怪啦":G.R.Node==2?"在溪边歇一会儿":"他们会自己往前走":G.RouteName,GentleJourney?G.R.JourneyPaused?"旅程已暂停，等你点继续。":G.R.Phase=="battle"?"伙伴会自己应对，你可以看着他们。":"不用操作，也会继续这一趟旅程。":"小队在旅行，农田也在长。回来看看就好。");left.Children.Add(TravelTrack());
        var moment=new StackPanel();var heading=new DockPanel();var round=Live(()=>G.R.Phase=="battle"?"第 "+G.R.Round+" 回合":"第 "+(G.R.Node+1)+" / 5 站",11,U.Muted);DockPanel.SetDock(round,Dock.Right);heading.Children.Add(round);heading.Children.Add(Live(TravelStatus,16,U.Green));moment.Children.Add(heading);
        var scene=Scene(G.R.Phase=="battle");scene.Journey=G.R.Phase!="battle";scene.Height=scene.MinHeight=G.R.Phase=="battle"?214:190;scene.Margin=new Thickness(0);moment.Children.Add(scene);
        if(GentleJourney)moment.Children.Remove(heading);
        var frame=U.Card(moment,10);left.Children.Add(frame);
        if(GentleJourney){JourneyAside(aside);return;}
        left.Children.Add(Live(()=>G.R.Phase=="battle"?G.Intent:G.R.Node==2?"在这里，全队将恢复 40% 体力。":"无需操作，小队会自动继续前进。",12,U.Muted));
        if(!WorldGuide)left.Children.Add(Live(()=>String.IsNullOrEmpty(G.R.LastCombo)?"配好的技能会按站位自动触发。":G.R.LastCombo,13,U.Green));
        var report=new StackPanel();report.Children.Add(U.Text("最近一回合",14));report.Children.Add(Live(()=>G.R.Log,12));report.Children.Add(U.Text("本趟组合表现",14));report.Children.Add(Live(()=>G.ComboReport,12,U.Muted));
        if(G.R.Relics.Any(i=>i>=0)){report.Children.Add(U.Text("信物触发",14));report.Children.Add(Live(()=>G.RelicReport,12,U.Muted));}
        var expand=new Expander{Header="展开战报 · 技能与信物表现",Content=report,IsExpanded=travelReportOpen};expand.Expanded+=(s,e)=>travelReportOpen=true;expand.Collapsed+=(s,e)=>travelReportOpen=false;left.Children.Add(expand);
        JourneyAside(aside);
        left.Children.Add(Btn("逐场复盘 · 伤害与配合",()=>OpenBattleReview()));
    }
    private void JourneyAside(StackPanel aside)
    {
        if(GentleJourney){GentleTravelAside(aside);return;}
        var controls=new StackPanel();controls.Children.Add(Live(TravelStatus,18,U.Green));
        if(WorldGuide)controls.Children.Add(Live(GuideCombinationEvidence,11,U.Green));
        controls.Children.Add(Live(()=>G.R.JourneyPaused?"时间停在这里，点继续才会前进。":WorldGuide?"可以点右上角「缩成宠物」去忙，双击宠物再回来。":"下一步约 "+Math.Max(0,(int)Math.Ceiling((G.R.NextJourneyTicks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond))+" 秒 · 隐藏后继续",12,U.Muted));
        var pause=U.Button("",()=>{G.ToggleJourneyPause();if(WorldGuide)Render();else RefreshCounters();},true,!WorldGuide||G.R.JourneyPaused);updates.Add(()=>pause.Content=G.R.JourneyPaused?"继续这趟旅程":"暂停旅程");if(!WorldGuide||G.R.JourneyPaused)FarmTheme.Accent(pause);if(WorldGuide&&G.R.JourneyPaused)HighlightGuideAction(pause);controls.Children.Add(pause);
        controls.Children.Add(Btn("回农场看看",()=>Go("农场")));aside.Children.Add(U.Card(controls,12));
        if(GentleJourney){aside.Children.Add(U.Text("这一趟结束就会停下，等你回来。",12,U.Muted));return;}
        var team=new StackPanel();team.Children.Add(U.Text("同行伙伴",16));
        foreach(int pet in G.S.Team){int p=pet;var row=new DockPanel{Margin=new Thickness(0,0,0,7)};var portrait=Portrait(p,40);portrait.Width=42;portrait.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(portrait,Dock.Left);row.Children.Add(portrait);
            var stats=new StackPanel();var name=Live(()=>G.Name(p)+(G.R.Hp[p]<=0?" · 休息中":""),12);name.Margin=new Thickness(0);stats.Children.Add(name);var hp=Live(()=>G.R.Hp[p]+" / "+G.MaxHp(p)+(G.R.Shield[p]>0?"  +盾 "+G.R.Shield[p]:""),10,U.Muted);hp.Margin=new Thickness(0);stats.Children.Add(hp);var bar=U.Bar(G.R.Hp[p],G.MaxHp(p));bar.Margin=new Thickness(0,3,0,0);updates.Add(()=>{bar.Maximum=G.MaxHp(p);bar.Value=G.R.Hp[p];portrait.Opacity=G.R.Hp[p]>0?1:.4;});stats.Children.Add(bar);row.Children.Add(stats);team.Children.Add(row);}
        team.Children.Add(Live(()=>"已入库肥料  +"+G.R.RewardFertilizer,13,U.Gold));aside.Children.Add(U.Card(team,12));
        var pack=new StackPanel();pack.Children.Add(U.Text("本趟行囊",16));pack.Children.Add(U.Text(G.R.Blessing>=0?"祝福 · "+FarmGame.BlessingNames[G.R.Blessing]:"沿用旧旅程的能力",12,U.Green));pack.Children.Add(U.Text("信物 · "+G.RelicLoadout(G.R.Relics),12));pack.Children.Add(U.Text(FarmGame.ExpeditionRule(G.R.Depth),11,U.Muted));aside.Children.Add(U.Card(pack,12));
        if(G.R.InspirationsEnabled)aside.Children.Add(InspirationRunCard());
        if(G.R.Picnic>=0)aside.Children.Add(PicnicRunCard());
        if(!confirmRetreat)aside.Children.Add(Btn("提前返家…",()=>confirmRetreat=true));
        else{aside.Children.Add(U.Text("途中肥料保留，本趟未取得的首领奖励不会获得。",12,U.Muted));U.Row(aside,Btn("继续旅程",()=>confirmRetreat=false),Btn("确认返家",()=>{G.Retreat();confirmRetreat=false;}));}
    }
    private UIElement RewardTile(string glyph,string name,int amount,string use)
    {
        var p=new StackPanel();var row=new DockPanel();var icon=new FarmGlyph{Kind=glyph,Width=37,Height=37,Margin=new Thickness(0,0,10,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);var text=new StackPanel();text.Children.Add(U.Text(name+"  +"+amount,20,U.Gold));text.Children.Add(U.Text(use,11,U.Muted));row.Children.Add(text);p.Children.Add(row);var card=U.Card(p,12);card.Margin=new Thickness(0,0,7,10);return card;
    }
    private void JourneyResult(StackPanel left,StackPanel aside){ReturnOverview(left,aside);}
}
