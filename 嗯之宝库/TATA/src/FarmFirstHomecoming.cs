using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;

public sealed class FarmSoilReceipt
{
    public int BeforeLevel,AfterLevel,Cost;
    public double BeforeBerrySeconds,AfterBerrySeconds;
}

public partial class FarmGame
{
    public FarmSoilReceipt ValidSoilReceipt
    {
        get{
            var receipt=S.LastSoilUpgrade;
            if(receipt==null||receipt.BeforeLevel<0||receipt.BeforeLevel>=5||receipt.AfterLevel!=receipt.BeforeLevel+1||receipt.AfterLevel!=S.SoilLevel||receipt.Cost!=4+receipt.BeforeLevel*4)return null;
            if(Double.IsNaN(receipt.BeforeBerrySeconds)||Double.IsInfinity(receipt.BeforeBerrySeconds)||Double.IsNaN(receipt.AfterBerrySeconds)||Double.IsInfinity(receipt.AfterBerrySeconds)||receipt.AfterBerrySeconds<=0||receipt.AfterBerrySeconds>=receipt.BeforeBerrySeconds)return null;
            return receipt;
        }
    }
}

public partial class FarmPanel
{
    private bool SmallHomecoming{get{return WorldGuide&&(Tab=="冒险"&&G.S.GuideStep==5&&G.R.Phase=="result"||Tab=="农场"&&G.AtHome&&(G.S.GuideStep==6||G.S.GuideStep==7));}}
    private UIElement GuideReturnExtras(FarmRun run,bool crystalPrimary=false)
    {
        var extra=new StackPanel();extra.Children.Add(U.Text(crystalPrimary?"另有肥料 "+run.RewardFertilizer+"，可以留着给田施肥。":run.RewardCrystals>=0?"另有晶石 "+run.RewardCrystals+"，留给之后的进化。":"这趟旧记录没有晶石总量。",12,U.Muted));
        if(run.DecorationReward>=0){var decor=new StackPanel();decor.Children.Add(new DecorationView{Decoration=run.DecorationReward,Height=66});decor.Children.Add(GentleText(FarmGame.DecorationNames[run.DecorationReward],16,U.Green));decor.Children.Add(GentleText("伙伴带回的小纪念，已经收好了。",12,U.Muted));extra.Children.Add(U.Card(decor,10));}
        if(run.NewRelic>=0)extra.Children.Add(U.Text("信物："+FarmGame.RelicNames[run.NewRelic],12));if(run.UnlockedGarden)extra.Children.Add(U.Text("新增两块田与薄荷种子，随时可以去看看。",12));
        return new Expander{Header="还有哪些收获",Content=extra,Margin=new Thickness(0,8,0,0)};
    }
    private void FirstHomecomingPage()
    {
        var run=G.R;var panel=GentleColumn(400);
        panel.Children.Add(GentleText(run.RewardFertilizer>0?"带回了一袋肥料":"回来歇一会儿吧",22));
        panel.Children.Add(Portrait(GuidePet,66));
        panel.Children.Add(RewardTile("fertilizer","肥料",run.RewardFertilizer,"已经收好啦"));
        panel.Children.Add(GentleText(run.RewardFertilizer>0?"肥料能让田里的莓果长得更快。":"途中拿到的东西，都会带回家。",14,U.Green));
        bool soil=G.S.SoilLevel<5&&G.S.Fertilizer>=G.SoilCost;
        panel.Children.Add(GentleAction("带回小院",()=>ReturnFromReceipt(run,soil?"农场":"伙伴",soil?-1:ResultCarePet())));
        panel.Children.Add(GuideReturnExtras(run));
    }
    private UIElement SoilLessonPicture(bool applied)
    {
        var picture=new Grid{Margin=new Thickness(0,0,0,8)};var scene=Scene();scene.FocusedFarmPet=GuidePet;scene.LearningPlot=LessonBerryPlot();scene.Height=scene.MinHeight=applied?112:144;scene.Margin=new Thickness(0);scene.SoilLessonApplied=applied;picture.Children.Add(scene);
        picture.Children.Add(new FarmGlyph{Kind="fertilizer",Width=40,Height=40,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,34,60,0)});
        return picture;
    }
    private UIElement SoilTimeCard()
    {
        var receipt=G.ValidSoilReceipt;var panel=new StackPanel();
        var title=GentleText(receipt==null?"现在种一批莓果":"本次施肥 · 莓果等待时间",12,U.Green);title.Margin=new Thickness(0,0,0,6);panel.Children.Add(title);
        if(receipt==null){panel.Children.Add(GentleText("约 "+G.CropSeconds(0).ToString("0.#")+" 秒",19,U.Green));}
        else{
            var times=new UniformGrid{Columns=2};panel.Children.Add(times);
            Action<string,double,string,System.Windows.Media.Brush> row=(caption,seconds,id,color)=>{
                var column=new StackPanel{Margin=new Thickness(6,0,6,0)};times.Children.Add(column);
                var label=GentleText(caption,12,U.Muted);label.Margin=new Thickness(0);column.Children.Add(label);
                var duration=GentleText("约 "+seconds.ToString("0.#")+" 秒",17,caption=="施肥前"?U.Ink:U.Green);duration.Margin=new Thickness(0,3,0,0);column.Children.Add(duration);
                var bar=U.Bar(0,1,color);bar.Maximum=receipt.BeforeBerrySeconds;bar.Value=seconds;bar.Height=7;bar.Margin=new Thickness(0,6,0,0);AutomationProperties.SetAutomationId(bar,id);AutomationProperties.SetName(bar,caption+"莓果成熟时间 "+seconds.ToString("0.#")+" 秒");column.Children.Add(bar);
            };
            row("施肥前",receipt.BeforeBerrySeconds,"soil-time-before",U.Color("#ABA692"));row("施肥后",receipt.AfterBerrySeconds,"soil-time-after",U.Green);
        }
        var card=U.Card(panel,8);card.MaxWidth=340;card.Margin=new Thickness(0,0,0,2);AutomationProperties.SetAutomationId(card,"soil-time-card");return card;
    }
    private void FirstSoilLesson()
    {
        var state=G.S;int step=state.GuideStep;bool applied=step==7;var receipt=G.ValidSoilReceipt;var panel=GentleColumn(450);
        panel.Children.Add(GentleText(applied?receipt==null?"田里会继续长莓果":"田里长得更快啦":"给农田一点肥料",22));
        panel.Children.Add(SoilLessonPicture(applied));
        if(!applied){
            panel.Children.Add(GentleText("撒进土里，莓果就会长得快一点。",14,U.Green));
            int level=state.SoilLevel,cost=G.SoilCost,visit=courtyardVisit;
            Func<bool> current=()=>Object.ReferenceEquals(state,G.S)&&Tab=="农场"&&G.AtHome&&G.S.GuideStep==step&&courtyardVisit==visit&&state.SoilLevel==level&&G.SoilCost==cost;
            panel.Children.Add(GentleText("已有肥料 "+state.Fertilizer+(state.Fertilizer<cost?" · 还差 "+(cost-state.Fertilizer):""),12,U.Muted));
            if(state.Fertilizer<cost){
                var find=GentleAction("再去小溪收集肥料",()=>{if(current()){journeyDifficulty=journeyDepth=0;Go("冒险");}});find.MinHeight=44;panel.Children.Add(find);
            }else{
                var use=GentleAction("施肥 · 肥料 "+cost,()=>{if(current())GuideAction(state,step,UpgradeFromHome);},true);use.MinHeight=44;panel.Children.Add(use);updates.Add(()=>use.IsEnabled=current()&&state.Fertilizer>=cost);
            }
            return;
        }
        panel.Children.Add(SoilTimeCard());
        panel.Children.Add(GentleAction("回小院，慢慢玩",()=>GuideAction(state,step,()=>{state.GuideStep=8;state.SimpleCourtyard=true;Go("农场");})));
        var details=new StackPanel();
        if(receipt!=null)details.Children.Add(U.Text("这次用了肥料 "+receipt.Cost+"\n土壤 Lv."+receipt.BeforeLevel+" → Lv."+receipt.AfterLevel,12,U.Muted));
        details.Children.Add(U.Text("所有田地都会受益。有空再来喂一口。",12,U.Muted));
        details.Children.Add(Btn("我还想看看进化",()=>GuideAction(state,step,()=>{state.GuideStep=8;state.SimpleCourtyard=false;OpenCare(4,G.HasNextEvolution(4));})));
        var expand=new Expander{Header="查看详情",Content=details,MaxWidth=340,Margin=new Thickness(0,5,0,0)};AutomationProperties.SetAutomationId(expand,"soil-guide-details");panel.Children.Add(expand);
    }
}
