using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool GentleGuide{get{return G.GuideActive&&(Tab=="新手引导"||GuideBerryScreen||WorldGuide&&(Tab=="农场"||Tab=="伙伴"||Tab=="冒险"));}}
    internal bool WantsGentleNarrow{get{return Tab!="卡牌冒险"&&((QuietShell||GentleGuide&&(G.S.GuideStep<=3||!G.AtHome))&&Tab!="冒险"||SmallDeparture||SmallHomecoming||SmallRepeatDeparture||SmallJourney||FirstSmallJourney||GuideBerryScreen);}}
    private bool? lastGentleNarrow;
    private void ApplyGentleWindowSize()
    {
        bool narrow=WantsGentleNarrow;if(lastGentleNarrow==narrow)return;
        if(!lastGentleNarrow.HasValue&&!narrow){lastGentleNarrow=false;return;}
        lastGentleNarrow=narrow;var area=owner.DesktopWorkArea;
        double cx=IsVisible&&!Double.IsNaN(Left)?Left+Width/2:area.Left+area.Width/2;
        double cy=IsVisible&&!Double.IsNaN(Top)?Top+Height/2:area.Top+area.Height/2;
        MinWidth=Math.Min(narrow?500:780,area.Width-32);MinHeight=Math.Min(narrow?540:610,area.Height-40);
        Width=Math.Min(narrow?560:940,area.Width-32);Height=Math.Min(narrow?580:760,area.Height-40);
        var bounds=DesktopLayout.Clamp(new Rect(cx-Width/2,cy-Height/2,Width,Height),area);Left=bounds.Left;Top=bounds.Top;
    }
    private bool GuideShowsTab(string tab)
    {
        if(QuietShell)return true;
        if(G.S.GuideStep==0)return false;
        if(tab==Tab||tab=="伙伴")return true;
        if(tab=="农场")return G.S.GuideFarmSeen||G.S.GuideStep==1||G.S.GuideStep>=3;
        return G.S.GuideStep>=4||G.S.GuideStep==3&&(G.S.GuideOutingAccepted||G.S.GuideHomeStage>=HomeGuideStage.Invitation);
    }
    private void BuildGentleShell()
    {
        var root=new DockPanel{Background=U.Cream};FarmTheme.Apply(root);System.Windows.Documents.TextElement.SetFontFamily(root,FontFamily);Content=root;
        var header=new StackPanel{Margin=new Thickness(18,12,18,0)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var top=new DockPanel();header.Children.Add(top);
        var utilities=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(utilities,Dock.Right);top.Children.Add(utilities);FocusGroup(utilities,2);utilities.Children.Add(ReadingButton());
        var compact=U.Button("缩成宠物",owner.ShowCompact);compact.FontSize=11;compact.Padding=new Thickness(8,5,8,5);utilities.Children.Add(compact);
        var hide=U.Button("隐藏",owner.HideToTray);hide.FontSize=11;hide.Padding=new Thickness(8,5,8,5);hide.Margin=new Thickness(5,3,0,3);utilities.Children.Add(hide);
        if(G.S.GuideStep==4&&Tab=="冒险"&&!G.R.JourneyPaused&&G.R.Phase!="inspiration"&&!SmallRetreatVisible)HighlightGuideAction(compact);
        var brand=U.Text("苔间",18,U.Green);brand.VerticalAlignment=VerticalAlignment.Center;brand.Margin=new Thickness(0);top.Children.Add(brand);
        if(QuietShell||G.S.GuideStep>0){
            var row=new DockPanel{Margin=new Thickness(0,8,0,0)};header.Children.Add(row);
            var stock=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(stock,Dock.Right);row.Children.Add(stock);
            foreach(int kind in !QuietShell&&G.S.GuideStep>=5?new[]{0,3}:new[]{0}){
                var item=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0)};item.Children.Add(new FarmGlyph{Kind=kind==0?"berry":"fertilizer",Width=20,Height=20,Margin=new Thickness(0,0,4,0)});
                var count=U.Text("",12,U.Muted);count.Margin=new Thickness(0);resourceNumbers[kind]=count;item.Children.Add(count);stock.Children.Add(item);
            }
            var nav=new StackPanel{Orientation=Orientation.Horizontal};FocusGroup(nav,1);row.Children.Add(nav);
            foreach(string tab in new[]{"伙伴","农场","冒险"})if(GuideShowsTab(tab)){string target=tab;string label=target=="冒险"&&G.AtHome&&(G.S.GuideStep==3||QuietShell)?"出门":target;var b=Btn(label,()=>{ClearSkillCare();ClearChapterReturn();Go(target);},true,Tab==target||LifeTab&&LifeCategory==target||target=="伙伴"&&Tab=="进化"||target=="冒险"&&Tab=="目的地"||target=="农场"&&(Tab=="莓果田"||Tab=="施肥"||Tab=="小纪念"));b.Padding=new Thickness(9,5,9,5);b.Margin=new Thickness(0,0,5,0);nav.Children.Add(b);}
            AddChapterReturn(nav);
        }
        feedback=U.Text("",11,U.Muted);feedback.Margin=new Thickness(0,7,0,5);feedback.TextWrapping=TextWrapping.NoWrap;feedback.TextTrimming=TextTrimming.CharacterEllipsis;
        // The action already has its own short hint; do not repeat a lesson above it.
        feedback.Visibility=Visibility.Collapsed;header.Children.Add(feedback);if(readingSettingsOpen)header.Children.Add(ReadingSettings());
        var footer=PanelFooter(true);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        body=new Grid{Margin=new Thickness(18,10,18,8)};scroller=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroller);
    }
    private StackPanel GentleColumn(double width=480)
    {
        var p=new StackPanel{MaxWidth=width,HorizontalAlignment=HorizontalAlignment.Stretch};body.Children.Add(p);return p;
    }
    private TextBlock GentleText(string text,double size=15,System.Windows.Media.Brush color=null)
    {
        var t=U.Text(text,size,color);t.TextAlignment=TextAlignment.Center;return t;
    }
    private Button GentleAction(string label,Action action,bool enabled=true)
    {
        var b=Btn(label,action,enabled,true);b.MinWidth=230;b.HorizontalAlignment=HorizontalAlignment.Center;b.Margin=new Thickness(0,10,0,5);HighlightGuideAction(b);return b;
    }
    private void GuidePage()
    {
        if(G.S.GuideStep!=0){var p=GentleColumn();p.Children.Add(U.Text("随时可以回来看看",20));p.Children.Add(Btn("重新查看引导",()=>{G.S.GuideStep=0;G.S.GuideInWorld=false;G.ResetHomeGuide();G.Save();},true,true));p.Children.Add(Btn("回到农场",()=>Go("农场")));return;}
        int pet=GuidePet;var state=G.S;var panel=GentleColumn(400);panel.Margin=new Thickness(0,10,0,0);
        bool feed=G.AtHome&&G.Level(pet)<2;
        panel.Children.Add(GentleText("你好，我是"+G.Name(pet),23));panel.Children.Add(TouchablePortrait(pet,120,feed));
        panel.Children.Add(CompanionLine(pet,()=>feed?G.CanFeed(pet)?"点莓果按钮，喂我一口吧。":CareWaitLabel(0,2):G.AtHome?"回来啦，我们接着慢慢玩。":"伙伴们还在外面，我们去看看吧。",16));
        if(feed){
            panel.Children.Add(GrowthCard(pet,true));
            if(G.S.Berries<2&&!Enumerable.Range(0,G.Plots).Any(i=>G.S.Crops[i]==0))panel.Children.Add(GentleAction("去种一点莓果",()=>{if(Object.ReferenceEquals(state,G.S)&&G.S.GuideStep==0&&Tab=="新手引导"){OpenGuideBerries(pet);}}));
            else panel.Children.Add(GentleFeedAction("喂"+G.Name(pet)+" · 莓果 2",pet,()=>FirstGuideFeed(state,pet)));
            return;
        }
        var button=GentleAction(G.AtHome?"看看伙伴":"查看当前冒险",()=>GuideAction(state,0,()=>{
            G.S.GuideInWorld=true;SelectFocusedPet(pet);G.S.GuideStep=G.AtHome?2:4;
            Go(G.AtHome?"伙伴":"冒险");
        }));panel.Children.Add(button);
    }
    private void GentleGuidePet()
    {
        if(GentleHomePet())return;
        int pet=focusedPet>=0&&G.S.Team.Contains(focusedPet)?focusedPet:GuidePet;SelectGuidePetIfNeeded(pet);var state=G.S;int step=G.S.GuideStep;
        var p=GentleColumn(440);p.Children.Add(GentleText(G.Name(pet),22));p.Children.Add(TouchablePortrait(pet,96));
        bool growing=G.AtHome&&(step<=2||step==3&&G.S.Xp[pet]==0);
        p.Children.Add(GuideCareLine(pet,()=>!G.AtHome?"等伙伴回来，再陪它吃点东西。":growing?"来，先吃两颗莓果吧。":"吃好啦，看看莓果从哪里来。",growing));
        if(G.AtHome)p.Children.Add(GrowthCard(pet));
        if(growing&&G.S.Berries<2){
            p.Children.Add(GuideBerryAction("去农田看看莓果",()=>{if(Object.ReferenceEquals(state,G.S)&&G.S.GuideStep==step&&Tab=="伙伴")OpenGuideBerries(pet);}));
        }else if(growing){
            var feed=GentleFeedAction(FarmUX.FeedLabel(G,pet),pet,()=>GuideAction(state,step,()=>CareForFocusedPet(pet,()=>G.Feed(pet))));p.Children.Add(feed);
            
        }else{
            string target=!G.AtHome?"冒险":step<=3||step>=6?"农场":"冒险";
            p.Children.Add(GentleAction(target=="农场"?"去农场看看":G.AtHome?"去冒险验证成长":"查看当前冒险",()=>Go(target)));
            if(G.AtHome&&G.Level(pet)>=2)p.Children.Add(GrowthDetails(pet));
        }
    }
    private void SelectGuidePetIfNeeded(int pet){if(focusedPet!=pet)SelectFocusedPet(pet);}
    private void GentleGuideHome()
    {
        if(G.S.GuideStep==3&&G.AtHome){GentleHomeLesson();return;}
        if(G.AtHome&&(G.S.GuideStep==6||G.S.GuideStep==7)){FirstSoilLesson();return;}
        int step=G.S.GuideStep;var state=G.S;var p=GentleColumn(650);
        p.Children.Add(GentleText(step==7?"现在，可以安心陪伴了":step==6?"把肥料用到田里":step>=4?"农田也在慢慢长":"莓果会自己长",22));
        var scene=Scene();scene.FocusedFarmPet=GuidePet;scene.HideFarmPets=!G.AtHome;scene.Height=scene.MinHeight=G.AtHome?176:140;p.Children.Add(scene);
        string copy=step==7?homeSoilReceipt!=""?homeSoilReceipt:"农田更快了，下次培养会更轻松。":step==6?"施肥后，所有田地都会长得更快。":step>=4?G.JourneyStatus:"成熟会自动收获、补种。你不用守在这里。";
        p.Children.Add(GentleText(copy,13,U.Green));
        if(step==6)p.Children.Add(GentleText("已有肥料 "+G.S.Fertilizer+" · 本次需要 "+G.SoilCost,12,U.Muted));
        string label=step<=2?"去「伙伴」喂养":step==3?"去「冒险」准备":step<=5?"查看这趟冒险":step==6?"施肥升级 · 肥料 "+G.SoilCost:"回小院，慢慢玩";
        var action=GentleAction(label,()=>GuideAction(state,step,()=>{
            if(step<=2){G.S.GuideStep=2;Go("伙伴");}
            else if(step==3){journeyDifficulty=journeyDepth=0;Go("冒险");}
            else if(step<=5)Go("冒险");
            else if(step==6)UpgradeFromHome();
            else{G.S.GuideStep=8;G.S.SimpleCourtyard=true;Go("农场");}
        }),step!=6||G.S.Fertilizer>=G.SoilCost);p.Children.Add(action);
        if(step==6){updates.Add(()=>action.IsEnabled=G.AtHome&&G.S.Fertilizer>=G.SoilCost);if(G.S.Fertilizer<G.SoilCost)p.Children.Add(Btn("再去小溪收集肥料",()=>{journeyDifficulty=journeyDepth=0;Go("冒险");}));}
        if(step==7){p.Children.Add(GentleText("有空喂一口，想出门再出门。忙时点右上角「缩成宠物」。",12,U.Muted));var later=Btn("我还想看看进化",()=>GuideAction(state,step,()=>{G.S.GuideStep=8;G.S.SimpleCourtyard=false;OpenCare(4,G.HasNextEvolution(4));}));later.HorizontalAlignment=HorizontalAlignment.Center;p.Children.Add(later);}
        if(step<=3&&G.S.Berries<2)p.Children.Add(Btn("管理农田",()=>Go("农田")));
    }
    private void GentleGuideDeparture(StackPanel left,StackPanel aside)
    {
        if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth)){journeyDifficulty=journeyDepth=0;}
        Heading(left,"去小溪散散步","伙伴们会自己行动，你可以随时回来看看。" );
        var party=new UniformGrid{Columns=G.S.Team.Length};left.Children.Add(party);
        foreach(int pet in G.S.Team){var card=new StackPanel();card.Children.Add(Portrait(pet,94));card.Children.Add(GentleText(G.Name(pet)+" Lv."+G.Level(pet),13));party.Children.Add(card);}
        left.Children.Add(U.Text("这次同行的伙伴",12,U.Muted));
        if(G.S.Team.Contains(2)&&G.S.Team.Contains(3))left.Children.Add(U.Text("电团会放电，苔壳照顾大家，一起陪"+G.Name(GuidePet)+"去转转。",14,U.Green));
        var plan=new StackPanel();plan.Children.Add(U.Text(FarmGame.ExpeditionName(journeyDifficulty,journeyDepth),18,U.Green));plan.Children.Add(U.Text("约 2～4 分钟，走完这一趟就回来。",13));
        if(G.S.PlannedPicnic>=0)PicnicDeparture(plan);else plan.Children.Add(U.Text("出发不消耗材料。",12,U.Muted));
        var state=G.S;int step=G.S.GuideStep;var start=Btn(G.S.PlannedPicnic<0?"出发 · 去小溪看看":"制作便当并出发",()=>GuideAction(state,step,()=>{if(G.GuideActive&&step<=3&&!G.S.GuideOutingAccepted)return;if(G.BeginJourney(journeyDifficulty,selectedBlessing,journeyDepth)){autoplay=false;confirmRetreat=false;}}),G.CanPackPicnic(G.S.PlannedPicnic),true);HighlightGuideAction(start);updates.Add(()=>start.IsEnabled=G.CanPackPicnic(G.S.PlannedPicnic));plan.Children.Add(start);aside.Children.Add(U.Card(plan));
        if(step==3)aside.Children.Add(Btn("先待在家里",()=>GuideAction(state,step,CancelOutingPreparation)));
        var settings=new StackPanel();settings.Children.Add(U.Text("祝福："+FarmGame.BlessingNames[selectedBlessing]+"\n"+G.PartyAdvice(selectedBlessing),12,U.Muted));settings.Children.Add(Btn("查看完整准备",()=>ExpandAllPlay("冒险")));
        left.Children.Add(new Expander{Header="想调整时，再展开",Content=settings});
    }
    private void GentleGuideReturn(StackPanel left,StackPanel aside)
    {
        var run=G.R;bool won=run.Outcome=="victory";
        Heading(left,won?"他们带着收获回来啦":"回来歇一会儿吧",won?"这袋肥料，正好给农田用。":"途中带回的收获会保留。" );
        left.Children.Add(RewardTile("fertilizer","肥料",run.RewardFertilizer,"让农田长得更快"));
        left.Children.Add(GuideReturnExtras(run));
        bool soil=G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost;var next=new StackPanel();next.Children.Add(U.Text(SimpleCourtyard?"回小院歇一会儿":soil?"先回家，再给田施肥":"先回家，陪伙伴长大",18));next.Children.Add(U.Text("收获已经自动入库。",12,U.Muted));
        var primary=Btn(SimpleCourtyard?"返家 · 回小院":soil?"返家 · 升级农田":"返家 · 培养伙伴",()=>ReturnFromReceipt(run,SimpleCourtyard||soil?"农场":"伙伴",SimpleCourtyard||soil?-1:ResultCarePet()),true,true);HighlightGuideAction(primary);next.Children.Add(primary);aside.Children.Add(U.Card(next));
    }
}





