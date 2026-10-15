using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Automation;
using System.Globalization;

public partial class FarmPanel
{
    private bool cardDeckOpen,cardRulesOpen,cardRetireOpen,cardUpgradeOpen,cardAssist,cardOptionsOpen;
    private int cardViewRevision;
    private MossScene cardScene;
    private double CardScale{get{return new[]{1d,1.15,1.3}[Math.Max(0,Math.Min(2,G.S.ReadingSize))];}}
    private TextBlock Ct(string text,double size=14,Brush color=null){return U.Text(text,size*CardScale,color);}
    private Button Cb(string text,Action action,bool enabled=true,bool primary=false,string id=null)
    {
        int view=cardViewRevision;var state=G.S;
        var b=U.Button(text,()=>{if(view!=cardViewRevision||Tab!="卡牌冒险"||!Object.ReferenceEquals(state,G.S))return;ClearCardFeedback();action();Render();owner.RefreshCounters();},enabled,primary);
        b.FontSize=13*CardScale;b.MinHeight=36;if(id!=null)AutomationProperties.SetAutomationId(b,id);return b;
    }
    internal bool CardBack()
    {
        ClearCardFeedback();if(cardDeckOpen)cardDeckOpen=false;else if(cardRulesOpen)cardRulesOpen=false;else if(cardRetireOpen)cardRetireOpen=false;else if(cardUpgradeOpen)cardUpgradeOpen=false;else{owner.ShowCompact();return true;}Render();return true;
    }
    private void RenderCards()
    {
        CaptureCardBrowse();ResetCardInspection();
        if(!CurrentCardFeedback)ClearCardFeedback();cardViewRevision++;cardScene=null;cardForecastText=null;cardDefaultForecast=null;cardRewardFitTitle=null;cardRewardFitDetail=null;scenes.Clear();portraits.Clear();updates.Clear();companionViews.Clear();seedPreviews.Clear();feedback=null;
        var area=owner.DesktopWorkArea;MinWidth=Math.Min(780,area.Width-32);MinHeight=Math.Min(720,area.Height-40);
        if(Width<MinWidth)Width=Math.Min(940,area.Width-32);if(Height<MinHeight)Height=Math.Min(760,area.Height-40);lastGentleNarrow=false;
        Title="苔间 · 口袋远征 v"+FarmProgram.Version;
        var root=new DockPanel{LastChildFill=true};Content=new Border{Background=U.Cream,Padding=new Thickness(16,10,16,10),Child=root};
        var header=new DockPanel{Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var close=Cb("缩成宠物",()=>owner.ShowCompact(),true,false,"cards-compact");DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        var size=Cb("Aa",()=>G.SetReadingSize((G.S.ReadingSize+1)%3),true,false,"cards-reading");size.ToolTip="文字大小：100% / 115% / 130% · Ctrl + 加号 / 减号";size.Margin=new Thickness(0,3,8,3);DockPanel.SetDock(size,Dock.Right);header.Children.Add(size);
        header.Children.Add(Ct("苔间  /  口袋远征",20));
        var footer=new DockPanel{Margin=new Thickness(0,5,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var deck=Cb(cardDeckOpen?"收起牌组":"牌组 · "+G.C.Deck.Count,()=>{cardDeckOpen=!cardDeckOpen;cardPile=0;cardRulesOpen=false;cardRetireOpen=false;cardUpgradeOpen=false;},G.C.Deck.Count>0,false,"cards-deck");deck.MinHeight=28;deck.Padding=new Thickness(8,3,8,3);DockPanel.SetDock(deck,Dock.Right);footer.Children.Add(deck);
        var rules=Cb("怎么玩",()=>{cardRulesOpen=!cardRulesOpen;cardDeckOpen=false;cardRetireOpen=false;cardUpgradeOpen=false;},true,false,"cards-rules");rules.MinHeight=28;rules.Padding=new Thickness(8,3,8,3);DockPanel.SetDock(rules,Dock.Right);footer.Children.Add(rules);
        footer.Children.Add(Ct(G.GuideTest?"试玩不保存 · 收起会停在当前这一步":"自动保存 · 收起或退出后可接着玩",11,U.Muted));
        var host=new Grid();body=host;root.Children.Add(host);
        if(cardDeckOpen)CardDeckPage(host,false);else if(cardUpgradeOpen)CardDeckPage(host,true);else if(cardRulesOpen)CardRulesPage(host);else if(cardRetireOpen)CardRetirePage(host);else if(G.C.Phase=="battle"||HoldCardResult)CardBattlePage(host);else if(G.C.Phase=="reward")CardRewardPage(host);else if(G.C.Phase=="branch")CardBranchPage(host);else if(G.C.Phase=="camp")CardCampPage(host);else if(G.C.Phase=="route")CardRoutePage(host);else if(G.C.Phase=="result")CardResultPage(host);else CardLobbyPage(host);
        FinishPanelInput();
    }
    private StackPanel CardStack(Grid host){var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var p=new StackPanel();scroll.Content=p;host.Children.Add(scroll);return p;}
    private MossScene CardArt(bool fight,double height){cardScene=new MossScene(G,fight){Height=height,Tick=owner.Tick,Feedback=fight&&CurrentCardFeedback?cardFeedback:null,Margin=new Thickness(0,0,0,8)};return cardScene;}
    private void CardLobbyPage(Grid host)
    {
        var p=CardStack(host);p.Children.Add(CardArt(false,200));p.Children.Add(Ct("陪泡泡，去苔间闯一趟",24));
        p.Children.Add(Ct("亲自出牌，沿途收集招式，挑战苔冠守门人。",15,U.Muted));
        var info=new StackPanel();info.Children.Add(Ct("一只泡泡 · 8 张起始牌 · 5 场战斗",16,U.Green));info.Children.Add(Ct("随时收起，下次接着打。没有出发费用。",13));p.Children.Add(U.Card(info));
        p.Children.Add(Cb("带泡泡出发",()=>{G.StartCards(cardAssist);},true,true,"cards-start"));
        var extras=new Expander{Header=Ct("轻松模式与小院",12,U.Muted),IsExpanded=cardOptionsOpen,Margin=new Thickness(0,8,0,0)};extras.Expanded+=(s,e)=>cardOptionsOpen=true;extras.Collapsed+=(s,e)=>cardOptionsOpen=false;var more=new StackPanel();extras.Content=more;
        more.Children.Add(Cb(cardAssist?"轻松模式：开 · 初始体力 80":"轻松模式：关 · 初始体力 60",()=>cardAssist=!cardAssist));
        more.Children.Add(Cb("回原来的小院",()=>{Go(G.GuideActive?"新手引导":"农场");}));p.Children.Add(extras);
        if(G.C.CompletedRuns>0)p.Children.Add(Ct("已经走过 "+G.C.CompletedRuns+" 趟 · 苔冠印记 "+G.C.TotalWins,12,U.Muted));
        if(G.LoadWarning!="")p.Children.Add(Ct(G.LoadWarning,12,U.Gold));
    }
    private void CardBattlePage(Grid host)
    {
        var r=G.C;int rev=r.Revision;cardLesson=G.CardFirstTurnLesson();
        for(int i=0;i<6;i++)host.RowDefinitions.Add(new RowDefinition{Height=i==4?new GridLength(1,GridUnitType.Star):GridLength.Auto});
        Action<UIElement,int> add=(element,row)=>{Grid.SetRow(element,row);host.Children.Add(element);};
        add(CardArt(true,100),0);
        var status=new Grid();status.ColumnDefinitions.Add(new ColumnDefinition());status.ColumnDefinitions.Add(new ColumnDefinition());
        var left=new StackPanel();left.Children.Add(Ct("泡泡  ♥ "+r.Hp+" / "+r.MaxHp+"    护盾 "+r.Block,14));left.Children.Add(U.Bar(r.Hp,r.MaxHp));status.Children.Add(left);
        var right=new StackPanel{Margin=new Thickness(20,0,0,0)};Grid.SetColumn(right,1);right.Children.Add(Ct(G.CardEnemyName+"  ♥ "+r.EnemyHp+" / "+r.EnemyMax+(r.EnemyBlock>0?"  盾 "+r.EnemyBlock:""),14));right.Children.Add(U.Bar(r.EnemyHp,r.EnemyMax,U.Color("#A9694D")));status.Children.Add(right);add(status,1);
        var hint=Ct(G.CardBattleNote(cardLesson),12,U.Green);AutomationProperties.SetAutomationId(hint,"cards-battle-note");hint.MaxHeight=65*CardScale;hint.ToolTip=hint.Text;add(hint,2);
        add(CardEnergy(),3);var hand=new UniformGrid{Columns=4};
        for(int i=0;i<r.Hand.Count;i++){int slot=i;hand.Children.Add(CardTile(r.Hand[i],()=>ActCard(slot,rev,false),r.Phase=="battle"&&r.Energy>=MossCards.All[MossCards.Id(r.Hand[i])].Cost,"打出","cards-hand-"+i));}
        if(r.Hand.Count==0)hand.Children.Add(Ct(r.Phase!="battle"?"这一战结束了。": "手牌打完了。结束回合，再抽 4 张。",15,U.Muted));
        var scroll=new ScrollViewer{Content=hand,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};add(scroll,4);
        var actions=new DockPanel{Margin=new Thickness(0,7,0,0)};
        var end=Cb(r.Phase=="battle"?"结束回合":"这一战已结束",()=>ActCard(-1,rev,true),r.Phase=="battle",cardLesson.Active?cardLesson.End:!r.Hand.Any(c=>MossCards.All[MossCards.Id(c)].Cost<=r.Energy),"cards-end");end.MinWidth=140;DockPanel.SetDock(end,Dock.Right);actions.Children.Add(end);AttachCardInspection(end,-1,cardInspect,active=>ShowCardForecast(active,cardViewRevision));
        var more=Cb("离开…",()=>cardRetireOpen=true,true,false,"cards-leave");DockPanel.SetDock(more,Dock.Right);actions.Children.Add(more);
        actions.Children.Add(CardForecastArea());add(actions,5);
    }
    private Border CardTile(int card,Action action,bool enabled,string verb,string id)
    {
        var def=MossCards.All[MossCards.Id(card)];var p=new DockPanel{LastChildFill=true};
        bool teach=verb=="打出"&&cardLesson!=null&&cardLesson.Active&&cardLesson.Slot>=0&&id=="cards-hand-"+cardLesson.Slot;
        var play=Cb(enabled?(verb=="打出"?verb+" · "+def.Cost+" 能量":"加入牌组"):G.C.Phase!="battle"?"结算中":"需 "+def.Cost+" 能量",action,enabled,teach,id);DockPanel.SetDock(play,Dock.Bottom);p.Children.Add(play);
        var title=Ct(G.CurrentCardName(card),15);DockPanel.SetDock(title,Dock.Top);p.Children.Add(title);
        var icon=new MossGlyph{Kind=def.Kind,Height=32,Margin=new Thickness(0,0,0,4)};DockPanel.SetDock(icon,Dock.Top);p.Children.Add(icon);
        string effect=(verb=="打出"?"":"出牌需 "+def.Cost+" 能量。\n")+G.CurrentCardText(card);
        if(verb=="打出"&&G.C.Phase=="battle")effect=G.CardHandText(card);
        var text=Ct(effect,12);if(id!=null)AutomationProperties.SetAutomationId(text,id+"-effect");text.Margin=new Thickness(0);p.Children.Add(text);
        var c=U.Card(p,10);c.Height=(verb=="打出"&&G.CardEffectSummary()!=""?176:188)*CardScale;c.Margin=new Thickness(0,0,7,6);c.Background=def.Kind=="盾"?U.Color("#EDF0DE"):def.Kind=="雷"?U.Color("#F1EAF5"):def.Kind=="叶"?U.Color("#F2EDD7"):U.Color("#E5EFF0");AttachCardForecast(c,id,verb);AttachRewardFit(c,id);return c;
    }
    private void CardRewardPage(Grid host)
    {
        var p=CardStack(host);var r=G.C;int rev=r.Revision;p.Children.Add(Ct("胜利！给这套牌一点新变化",23));p.Children.Add(Ct("已通过 "+r.Victories+" / 5 场 · 体力 "+r.Hp+" / "+r.MaxHp,14,U.Green));
        p.Children.Add(Ct(r.Elite?"随身小物："+G.CardRelicText+"。"+(r.Offers.Any(c=>MossCards.Up(c))?"这次还可选一张强化牌。":""):"选一张加入本局牌组，或者保持现在的组合。",13,U.Muted));
        var choices=new UniformGrid{Columns=3};for(int i=0;i<r.Offers.Count;i++){int pick=i;choices.Children.Add(CardTile(r.Offers[i],()=>G.PickCardReward(pick,rev),true,"选这张","cards-reward-"+i));}p.Children.Add(choices);
        p.Children.Add(CardRewardFitArea());
        var skip=Cb("这次不加牌",()=>G.PickCardReward(-1,rev),true,false,"cards-skip");AttachCardInspection(skip,-1,rewardInspect,active=>ShowRewardFit(active,cardViewRevision));p.Children.Add(skip);
    }
    private void CardRoutePage(Grid host)
    {
        var p=CardStack(host);var r=G.C;int rev=r.Revision;p.Children.Add(CardArt(false,150));p.Children.Add(Ct("下一站，走哪边？",24));p.Children.Add(Ct("第 "+(r.Stage+1)+" / 5 场 · 体力 "+r.Hp+" / "+r.MaxHp,14,U.Green));
        p.Children.Add(Cb("沿着小溪走 · 普通战斗，胜利选牌",()=>G.ChooseCardRoute(false,rev),true,true,"cards-route-safe"));
        p.Children.Add(Cb("穿过栗子丛 · 敌人更强，胜利得"+(r.Relic>0?"强化牌":"树叶帽"),()=>G.ChooseCardRoute(true,rev),true,false,"cards-route-elite"));
        p.Children.Add(Ct("树叶帽：每场开局 +4 护盾。险路敌人体力 +12，攻击 +3。",12,U.Muted));
        if(r.RulesVersion>0)p.Children.Add(Ct("两条路的敌人都会连续进攻两回合，再休息一回合。",12,U.Green));
        p.Children.Add(Cb("先收起，晚点再选",()=>owner.ShowCompact()));
    }
    private void CardCampPage(Grid host)
    {
        var p=CardStack(host);var r=G.C;int rev=r.Revision,heal=Math.Min(16,r.MaxHp-r.Hp),upgrades=r.Deck.Count(c=>!MossCards.Up(c));
        p.Children.Add(CardArt(false,150));p.Children.Add(Ct(r.Stage==4?"守门人就在前面":"在大叶子下歇一会",24));
        string prompt=heal==0?(upgrades>0?"体力已满，可以先看看强化。":"体力已满，牌也都强化过了。"):"休息或强化，只能选一项。";
        p.Children.Add(Ct(prompt+"继续后进入第 "+(r.Stage+1)+" / 5 场。",14,U.Muted));
        p.Children.Add(Cb(heal>0?"休息 · 恢复 "+heal+" 体力并继续":"体力已满 · 直接继续",()=>G.CardCamp(0,-1,rev),true,heal>0||upgrades==0,"cards-rest"));
        p.Children.Add(Cb(upgrades>0?"查看可强化的 "+upgrades+" 张牌":"所有牌都已强化",()=>cardUpgradeOpen=true,upgrades>0,heal==0&&upgrades>0,"cards-upgrade"));
        p.Children.Add(Ct("当前体力 "+r.Hp+" / "+r.MaxHp+"。查看强化和返回不会消耗营地机会。",12,U.Green));
    }
    private void CardDeckPage(Grid host,bool upgrade)
    {
        if(upgrade)CardUpgradePage(host);else CardPilePage(host);
    }
    private void CardRetirePage(Grid host)
    {
        var p=CardStack(host);int rev=G.C.Revision;p.Children.Add(Ct("要先歇一会吗？",24));p.Children.Add(Ct("收起会保留手牌、体力和敌人意图。",15));p.Children.Add(Cb("缩成宠物 · 保留这一趟",()=>{cardRetireOpen=false;owner.ShowCompact();},true,true));p.Children.Add(Cb("返回战斗",()=>cardRetireOpen=false,true,false,"cards-cancel-retire"));
        p.Children.Add(Ct("结束本局会放下这次的牌组，下一趟从头开始。",13,U.Muted));p.Children.Add(Cb("确认结束这一趟",()=>{G.RetireCards(rev);cardRetireOpen=false;},true,false,"cards-confirm-retire"));
    }
    private void CardRulesPage(Grid host)
    {
        var p=CardStack(host);p.Children.Add(Ct("看意图，再出牌",24));
        p.Children.Add(Ct("① 敌人头顶写着它下一步会做什么。\n② 每回合 3 能量，点「打出」使用一张牌。\n③ 点「结束回合」，敌人才行动，然后抽 4 张。",15));
        p.Children.Add(Ct("泼水 → 小小电花：潮湿时电花伤害更高。\n泡泡盾 → 泡泡冲撞：盾越厚，撞得越痛。",15,U.Green));
        p.Children.Add(Ct("普通护盾只挡本回合；厚泡泡按卡牌说明保留。潮湿每回合减少 1。\n战后可以选牌，也可以跳过。休息和强化二选一。\n随时缩回桌宠或退出，战斗不会自己进行。",14));
        p.Children.Add(Ct("移到牌上或用 Tab 选牌，先看下方的结果预览。\nEnter / 空格执行，Esc 返回或收起。",12,U.Muted));p.Children.Add(Cb("回去试试",()=>cardRulesOpen=false,true,true));
        p.Children.Add(Cb("去小院看看 · 保留远征",()=>{cardRulesOpen=false;Go(G.GuideActive?"新手引导":"农场");}));
    }
    private void CardResultPage(Grid host)
    {
        var p=CardStack(host);var r=G.C;p.Children.Add(CardArt(false,155));p.Children.Add(Ct(r.Outcome,24));p.Children.Add(Ct("通过 "+r.Victories+" / 5 场  ·  打出 "+r.Played+" 张牌  ·  连招 "+r.Combos+" 次",16,U.Green));
        p.Children.Add(Ct("本趟加入 "+r.RewardCount+" 张新牌 · 实际损失体力 "+r.DamageTaken+"\n苔冠印记累计 "+r.TotalWins+" 枚",14));
        p.Children.Add(Cb("再出发 · 换一套组合试试",()=>{G.StartCards(r.Assisted);},true,true,"cards-again"));p.Children.Add(Cb("回桌面陪伴",()=>owner.ShowCompact()));
        var log=new Expander{Header=Ct("这一趟的足迹",13,U.Muted)};log.Content=Ct(String.Join("\n",r.Trail),12);p.Children.Add(log);
        p.Children.Add(Ct("本局牌组会重新开始，留下印记与旅程次数。",12,U.Muted));
    }
}

internal sealed class MossGlyph:FrameworkElement
{
    internal string Kind;
    protected override void OnRender(DrawingContext d)
    {
        double x=ActualWidth/2,y=ActualHeight/2;var pen=new Pen(U.Color("#63887F"),2);
        d.DrawEllipse(U.Color("#FFFFFF"),null,new Point(x,y),29,20);
        if(Kind=="盾"){var shape=Geometry.Parse("M0,-15 L14,-9 L11,8 L0,17 L-11,8 L-14,-9 Z");d.PushTransform(new TranslateTransform(x,y));d.DrawGeometry(U.Color("#B6D6B5"),pen,shape);d.DrawLine(pen,new Point(-6,0),new Point(-1,5));d.DrawLine(pen,new Point(-1,5),new Point(7,-5));d.Pop();}
        else if(Kind=="雷"){d.PushTransform(new TranslateTransform(x,y));d.DrawGeometry(U.Color("#DBC264"),new Pen(U.Color("#947637"),1.5),Geometry.Parse("M3,-18 L-12,3 L-1,3 L-4,18 L13,-4 L3,-4 Z"));d.Pop();}
        else if(Kind=="叶"){d.PushTransform(new TranslateTransform(x,y));d.DrawGeometry(U.Color("#ABC984"),pen,Geometry.Parse("M-13,12 Q-22,-10 15,-15 Q25,10 -13,12"));d.DrawLine(pen,new Point(-13,12),new Point(9,-9));d.Pop();}
        else {d.PushTransform(new TranslateTransform(x,y));d.DrawGeometry(U.Color("#A0D4E1"),pen,Geometry.Parse("M0,-18 C-5,-9 -15,0 -13,8 C-10,22 13,22 14,7 C14,-1 5,-12 0,-18"));d.DrawEllipse(Brushes.White,null,new Point(-5,5),3,5);d.Pop();}
    }
}
internal sealed partial class MossScene:FrameworkElement
{
    private readonly FarmGame game;private readonly bool battle;internal int Tick;internal MossFeedback Feedback;
    internal MossScene(FarmGame g,bool fight){game=g;battle=fight;ClipToBounds=true;}
    private void Text(DrawingContext d,string s,double x,double y,double size,Brush color,double width)
    {
        var t=new FormattedText(s,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),size*new[]{1d,1.15,1.3}[game.S.ReadingSize],color,VisualTreeHelper.GetDpi(this).PixelsPerDip);t.MaxTextWidth=Math.Max(1,width);d.DrawText(t,new Point(x,y));
    }
    protected override void OnRender(DrawingContext d)
    {
        double w=ActualWidth,h=ActualHeight;if(w<100||h<50)return;
        d.DrawRoundedRectangle(U.Color("#E1EBDA"),new Pen(U.Color("#BECDAE"),1),new Rect(0,0,w,h),16,16);
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),16,16));
        d.DrawEllipse(U.Color("#F9EFC5"),null,new Point(w*.80,h*.25),24,24);
        for(int i=0;i<7;i++){double x=i*w/6;d.DrawEllipse(U.Color(i%2==0?"#D0DFCA":"#BACCAB"),null,new Point(x,h*.98),w*.23,h*.58);}
        d.DrawEllipse(U.Color("#A3CBBE"),null,new Point(w*.5,h+14),w*.42,h*.36);
        for(int i=0;i<20;i++){double x=(i*89)%w,y=h-8-(i*13)%22;d.DrawLine(new Pen(U.Color("#8EAD7B"),2),new Point(x,y),new Point(x+4,y-7));}
        double bob=game.S.DesktopMotion?Math.Sin(Tick*.07)*2:0;
        double dx=Feedback==null?0:Feedback.PlayerOffset,edx=Feedback==null?0:Feedback.EnemyOffset;
        double size=battle?86:Math.Min(138,h-30),px=battle?w*.23:w*.30;
        d.DrawEllipse(U.Color("#91AE85"),null,new Point(px,h-12),size*.30,5);d.DrawImage(Art.Frame(0,game.S.DesktopMotion?Tick:0,false),new Rect(px-size/2+dx,h-size-12+bob,size,size));
        if(battle){double ex=w*.78;d.DrawImage(Art.Frame(Feedback==null?game.CardEnemyPet:Feedback.EnemyPet,game.S.DesktopMotion?Tick:0,false,game.C.Stage==4?2:0),new Rect(ex-42+edx,h-94,84,84));
            if(game.C.Block>0)d.DrawEllipse(U.Color("#309BCDDF"),new Pen(U.Color("#6CA8AF"),2),new Point(px,h-51),47,47);
            if(game.C.Wet>0)for(int drop=0;drop<3;drop++)d.DrawEllipse(U.Color("#71B7CF"),null,new Point(ex-30+drop*30,h-22+(drop%2)*5),3,5);
            Text(d,"第 "+(game.C.Stage+1)+" / 5 场 · 回合 "+game.C.Turn,12,8,11,U.Green,w*.38);
            Text(d,game.C.Phase=="battle"?game.CardIntent:game.C.Hp<=0?"这一趟先休息":"击败了！",w*.52,8,13,U.Color("#764D3C"),w*.46);
            if(game.CardBossCharge){d.DrawRectangle(U.Pale,null,new Rect(w*.52,66,w*.20,5));d.DrawRectangle(U.Green,null,new Rect(w*.52,66,w*.20*game.C.BossDamage/12,5));}
            if(game.C.Wet>0)Text(d,"潮湿 "+game.C.Wet+" 回合",w*.52,39,12,U.Green,w*.26);
        }else{Text(d,"POCKET EXPEDITION",w*.51,h*.24,11,U.Green,w*.44);Text(d,"小小伙伴\n也有大冒险",w*.51,h*.40,21,U.Ink,w*.44);}
        if(battle&&Feedback!=null)DrawFeedback(d,w,h);
        d.Pop();
    }
}
