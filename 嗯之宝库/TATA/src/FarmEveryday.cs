using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool SmallRepeatDeparture{get{return SimpleCourtyard&&Tab=="冒险"&&G.AtHome;}}
    private FarmSave repeatDetailState;
    private int repeatDetailSession=-1;
    private bool repeatDetailsOpen;

    private void QuietBerryPage()
    {
        if(!SimpleCourtyard){FarmDetails();return;}
        if(!G.AtHome){QuietHome();return;}
        var state=G.S;bool evolution=berryForEvolution&&Object.ReferenceEquals(smallGrowthState,state);int pet=evolution?smallGrowthPet:G.CourtyardCompanion,visit=courtyardVisit;var panel=GentleColumn(450);
        Func<bool> current=()=>CourtyardPage(state,"莓果田",visit)&&(evolution?Object.ReferenceEquals(smallGrowthState,state)&&smallGrowthPet==pet:!berryForEvolution&&G.CourtyardCompanion==pet);
        var title=Live(()=>state.Berries>=2?"莓果够喂一口啦":LessonBerryPlot()>=0?"等这一批莓果":"田里还没种莓果",22);title.TextAlignment=TextAlignment.Center;title.Margin=new Thickness(0,0,0,3);panel.Children.Add(title);
        var picture=new Grid{Height=100,Margin=new Thickness(0,0,0,4)};panel.Children.Add(picture);
        var scene=Scene();scene.FocusedFarmPet=pet;scene.LearningPlot=LessonBerryPlot();scene.Height=scene.MinHeight=100;scene.Margin=new Thickness(0);MakeSceneTouchable(scene,pet);picture.Children.Add(scene);
        var cropIcon=new FarmGlyph{Kind="berry",Width=60,Height=60,HorizontalAlignment=HorizontalAlignment.Center};picture.Children.Add(cropIcon);
        var progress=new StackPanel{MaxWidth=330,Margin=new Thickness(0,0,0,3)};panel.Children.Add(progress);
        var label=GentleText("",12,U.Green);label.Margin=new Thickness(0,0,0,5);progress.Children.Add(label);
        var bar=U.Bar(0,100);bar.Height=9;bar.Margin=new Thickness(0);AutomationProperties.SetAutomationId(bar,"quiet-berry-progress");progress.Children.Add(bar);
        var stock=Live(()=>"篮子里有 "+state.Berries+" 颗 · 喂一口要 2 颗",13,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        var hint=Live(()=>state.Berries>=2?"拿两颗给"+G.Name(pet)+"尝尝吧。":LessonBerryPlot()>=0?"成熟会自动收好，可以先回去陪伙伴。":"选一块田改种莓果，改种会重新计时。",12,U.Muted);hint.TextAlignment=TextAlignment.Center;hint.Margin=new Thickness(0,0,0,3);panel.Children.Add(hint);
        var back=GentleAction("",()=>{
            if(!current())return;
            if(state.Berries>=2||LessonBerryPlot()>=0)Go(evolution?"进化":"伙伴");else OpenCropRecovery(0,"莓果田");
        });panel.Children.Add(back);
        updates.Add(()=>{
            int plot=LessonBerryPlot();scene.LearningPlot=plot;scene.ShowBerryBasket=plot>=0;scene.Visibility=plot>=0?Visibility.Visible:Visibility.Collapsed;cropIcon.Visibility=plot<0?Visibility.Visible:Visibility.Collapsed;progress.Visibility=plot>=0?Visibility.Visible:Visibility.Collapsed;
            if(plot>=0){
                bar.Value=Math.Min(100,100*state.Growth[plot]/G.CropSeconds(0));
                label.Text="这块田 · 约 "+G.SecondsLeft(plot)+" 秒后收好 "+G.CropYield(0)+" 颗";
                AutomationProperties.SetName(bar,"这块莓果田的生长进度");AutomationProperties.SetHelpText(bar,label.Text);
            }
            back.Content=state.Berries>=2?"回去喂"+G.Name(pet):plot>=0?"先回去陪"+G.Name(pet):"去选择一块田";
        });
        HomeGuideSecondary(panel,"回小院",()=>{if(current())Go("农场");});
    }

    private void RepeatDeparturePage()
    {
        if(!G.ExpeditionUnlocked(journeyDifficulty,journeyDepth))journeyDifficulty=journeyDepth=0;
        var state=G.S;int session=departureSession,picnic=state.PlannedPicnic,region=journeyDifficulty,depth=journeyDepth,blessing=selectedBlessing;
        int crystalPet=HasCrystalPreparation?crystalPreparationPet:-1,crystalStage=HasCrystalPreparation?crystalPreparationStage:-1;
        string roster=String.Join(",",state.Team);bool missingFriend=!PicnicFriendPresent(picnic);
        Func<bool> current=()=>Object.ReferenceEquals(state,G.S)&&SmallRepeatDeparture&&session==departureSession&&region==journeyDifficulty&&depth==journeyDepth&&picnic==state.PlannedPicnic&&blessing==selectedBlessing&&String.Join(",",state.Team)==roster;
        var panel=GentleColumn(450);panel.Children.Add(GentleText(FarmGame.ExpeditionName(region,depth),22));
        var party=new UniformGrid{Columns=state.Team.Length,Margin=new Thickness(0,4,0,6)};panel.Children.Add(party);
        foreach(int pet in state.Team){var friend=new StackPanel();friend.Children.Add(Portrait(pet,58));var name=GentleText(G.Name(pet),12);name.Margin=new Thickness(0);friend.Children.Add(name);party.Children.Add(friend);}
        panel.Children.Add(GentleText(crystalPet>=0?"给"+G.Name(crystalPet)+"找晶石 · 约 2～4 分钟":"大家会自己走完，约 2～4 分钟。",13,U.Green));
        if(picnic<0)panel.Children.Add(GentleText("不用准备材料。",12,U.Muted));
        else{
            var row=new DockPanel{Margin=new Thickness(0,2,0,2)};panel.Children.Add(row);
            var cancel=Btn("不带便当",()=>{if(current())G.PlanJourneyPicnic(-1,region,depth);});cancel.FontSize=11;cancel.Padding=new Thickness(7,3,7,3);cancel.Margin=new Thickness(5,0,0,0);DockPanel.SetDock(cancel,Dock.Right);row.Children.Add(cancel);
            var name=U.Text("便当 · "+FarmGame.PicnicName(picnic),12,U.Green);name.Margin=new Thickness(0);name.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(name);
            var price=GentleText("出发消耗："+FarmGame.PicnicCostText(picnic),12,U.Muted);price.Margin=new Thickness(0,3,0,0);panel.Children.Add(price);
            var missing=Live(()=>missingFriend?PicnicPreparationText(picnic):G.CanPackPicnic(picnic)?"":G.PicnicShortage(picnic),11,U.Gold);missing.TextAlignment=TextAlignment.Center;missing.Margin=new Thickness(0);panel.Children.Add(missing);updates.Add(()=>missing.Visibility=missingFriend||!G.CanPackPicnic(picnic)?Visibility.Visible:Visibility.Collapsed);
        }
        var start=GentleAction(picnic<0?"出发 · 去"+(region==0?"小溪":"洞穴")+"看看":"制作便当并出发",()=>{
            if(!current())return;
            if(missingFriend){OpenPicnicCompanion(picnic);return;}
            if(!G.CanPackPicnic(picnic)){OpenSmallResources("picnic",picnic);return;}
            G.EnsureJourneyOffers();if(!state.BlessingOffers.Contains(selectedBlessing))selectedBlessing=state.BlessingOffers[0];
            if(G.BeginJourney(region,selectedBlessing,depth,crystalPet,crystalStage)){ClearCrystalPreparation();autoplay=false;confirmRetreat=false;}
        });panel.Children.Add(start);updates.Add(()=>start.Content=missingFriend?"先看看同行伙伴":!G.CanPackPicnic(picnic)?"先看看缺的材料":picnic<0?"出发 · 去"+(region==0?"小溪":"洞穴")+"看看":"制作便当并出发");
        var links=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(links);
        links.Children.Add(Btn(crystalPet>=0?"先回去看进化":"先待在家里",()=>{if(current())CancelOutingPreparation();}));
        if(!Object.ReferenceEquals(repeatDetailState,state)||repeatDetailSession!=session){repeatDetailState=state;repeatDetailSession=session;repeatDetailsOpen=false;}
        var destination=Btn("换个地方看看",()=>{if(current())OpenSmallDestination(region,depth);});destination.Margin=new Thickness(8,3,0,3);links.Children.Add(destination);
        var details=new StackPanel();details.Children.Add(U.Text("想准备哪一项，就先看看那一项。",12,U.Muted));
        details.Children.Add(Btn("看看这次准备",()=>{if(current())OpenPreparationCards();}));
        details.Children.Add(Btn("安排同行伙伴",()=>{if(current())OpenLife("同行");}));
        details.Children.Add(Btn("带一点便当",()=>{if(current())OpenLife("便当");}));
        details.Children.Add(Btn("挑一件信物",()=>{if(current())OpenLife("行囊");}));
        details.Children.Add(Btn("查看完整准备",()=>{if(current())ExpandAllPlay("冒险");}));
        var expand=new Expander{Header="想调整时，再展开",Content=details,IsExpanded=repeatDetailsOpen,Margin=new Thickness(0,6,0,0)};
        expand.Expanded+=(s,e)=>repeatDetailsOpen=true;expand.Collapsed+=(s,e)=>repeatDetailsOpen=false;AutomationProperties.SetAutomationId(expand,"repeat-departure-details");panel.Children.Add(expand);
    }
}

