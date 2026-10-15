using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmSave smallDestinationState;
    private int smallDestinationRegion,smallDestinationDepth,smallDestinationStep,smallDestinationRevision;
    private void OpenSmallDestination(int region,int depth)
    {
        smallDestinationState=G.S;smallDestinationRegion=Math.Max(0,Math.Min(1,region));smallDestinationDepth=Math.Max(0,Math.Min(2,depth));smallDestinationStep=0;smallDestinationRevision++;Go("目的地");resetScroll=true;
    }
    private void SmallDestinationPage()
    {
        if(!G.AtHome){var away=GentleColumn();away.Children.Add(GentleText("先走完眼前这一趟",21));away.Children.Add(GentleAction("看看当前旅程",()=>Go("冒险")));return;}
        if(!Object.ReferenceEquals(smallDestinationState,G.S)){smallDestinationState=G.S;smallDestinationRegion=smallDestinationDepth=smallDestinationStep=0;smallDestinationRevision++;}
        var state=G.S;int region=smallDestinationRegion,depth=smallDestinationDepth,step=smallDestinationStep,revision=smallDestinationRevision,visit=courtyardVisit;
        Func<bool> current=()=>CourtyardPage(state,"目的地",visit)&&smallDestinationRevision==revision;
        Action<int> setDepth=d=>{if(current()&&d>=0&&d<3){smallDestinationDepth=d;smallDestinationRevision++;resetScroll=true;}};
        bool regionOpen=region==0||G.CaveUnlocked,open=G.ExpeditionUnlocked(region,depth);var panel=GentleColumn(450);
        panel.Children.Add(GentleText(step==0?(region==0?"小溪":"洞穴"):FarmGame.ExpeditionName(region,depth),22));
        var scene=Scene();scene.Journey=scene.JourneyPreview=true;scene.PreviewRegion=region;scene.Height=scene.MinHeight=92;panel.Children.Add(scene);
        if(step==0){
            panel.Children.Add(GentleText(region==0?"沿着溪水，走一圈就回来。":regionOpen?"新的路开放啦，可以慢慢准备。":"伙伴首次进化后，这里就会开放。",13,U.Green));
            panel.Children.Add(GentleAction(regionOpen?"看看走哪一段":"先看看伙伴进化",()=>{if(current()){if(regionOpen){smallDestinationStep=1;smallDestinationRevision++;resetScroll=true;}else OpenSmallEvolution(G.GuideCompanion==4?4:0);}}));
            HomeGuideSecondary(panel,region==0?"也看看洞穴":"也看看小溪",()=>{if(current()){smallDestinationRegion=1-region;smallDestinationDepth=0;smallDestinationRevision++;resetScroll=true;}});
        }else{
            panel.Children.Add(GentleText(open?"建议伙伴 "+FarmGame.ExpeditionLevel(region,depth):G.ExpeditionGate(region,depth),13,U.Green));
            if(open)panel.Children.Add(GentleText(depth==0?"从这里熟悉路，再慢慢往里走。":depth==1?"敌人更硬了，灼烧和中毒仍然有效。":"敌人会恢复体力，要准备持续的攻击。",12,U.Muted));
            panel.Children.Add(GentleAction(open?"选这里 · 回出发准备":!regionOpen?"先看看伙伴进化":"先看看上一段",()=>{
                if(!current())return;
                if(open){if(!G.ExpeditionUnlocked(region,depth))return;SelectJourneyDestination(region,depth);Go("冒险");}
                else if(!regionOpen)OpenSmallEvolution(G.GuideCompanion==4?4:0);else setDepth(Math.Max(0,depth-1));
            }));
            U.Row(panel,Btn("上一段",()=>setDepth(depth-1),depth>0),Btn("下一段",()=>setDepth(depth+1),depth<2));
        }
        HomeGuideSecondary(panel,"返回出发准备",()=>{if(current())Go("冒险");});
        if(step==1){
            var details=new StackPanel();details.Children.Add(U.Text("通关基础收获：肥料 "+FarmGame.ExpeditionFertilizer(region,depth)+"、晶石 "+FarmGame.ExpeditionCrystals(region,depth),12,U.Green));details.Children.Add(U.Text(FarmGame.ExpeditionRule(depth),12,U.Muted));details.Children.Add(U.Text("选定后会记住，下次打开可以接着准备。",12,U.Muted));
            var expand=new Expander{Header="这段路会带回什么",Content=details,Margin=new Thickness(0,3,0,0)};AutomationProperties.SetAutomationId(expand,"small-destination-details");panel.Children.Add(expand);
        }
    }
}
