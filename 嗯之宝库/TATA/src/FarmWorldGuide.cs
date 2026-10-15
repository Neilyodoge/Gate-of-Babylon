using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private bool WorldGuide{get{return G.GuideActive&&G.S.GuideInWorld;}}
    private int GuidePet{get{return G.GuideCompanion;}}
    private string GuideResumeTab{get{return G.HomeGuideResumeTab;}}
    private void RouteGuide()
    {
        if(Tab=="新手引导"&&G.S.GuideStep>0&&G.S.GuideStep<8){if(!G.S.GuideInWorld){G.S.GuideInWorld=true;G.Save();}G.SyncGuide();Tab=GuideResumeTab;if(Tab=="伙伴")SelectFocusedPet(GuidePet);resetScroll=true;}
        if(WorldGuide)G.SyncGuide();
        if(WorldGuide&&Tab=="冒险"&&G.AtHome&&G.S.GuideStep<=3&&!G.S.GuideOutingAccepted)GuideNavigated("冒险");
    }
    private void GuideNavigated(string tab)
    {
        if(WorldGuide&&tab=="冒险"&&G.AtHome&&G.S.GuideStep<=3){G.S.GuideOutingAccepted=true;if(G.S.GuideHomeStage==HomeGuideStage.Invitation)G.S.GuideHomeStage=HomeGuideStage.StayingHome;G.Save();}
        if(WorldGuide&&tab=="农场"&&!G.S.GuideFarmSeen){G.S.GuideFarmSeen=true;if(G.S.GuideStep==3&&G.S.GuideFarmLesson==0)G.S.GuideFarmLesson=1;G.Save();}
        if(WorldGuide&&G.S.GuideStep==1&&tab=="伙伴"){G.S.GuideStep=2;SelectFocusedPet(GuidePet);G.SyncGuide();G.Save();}
    }
    private static readonly DependencyProperty GuideActionProperty=DependencyProperty.RegisterAttached("IsGuideAction",typeof(bool),typeof(FarmPanel),new PropertyMetadata(false));
    internal static bool IsGuideAction(DependencyObject element){return (bool)element.GetValue(GuideActionProperty);}
    private void HighlightGuideAction(Button button)
    {
        button.SetValue(GuideActionProperty,true);
        AutomationProperties.SetAutomationId(button,"guide-primary");FarmTheme.Accent(button);button.BorderThickness=new Thickness(2);button.ToolTip="本轮引导的下一步；这也是平时使用的操作入口。";
    }
    private string WorldGuideHint()
    {
        int step=G.S.GuideStep;
        if(step<=2)return "先陪"+G.Name(GuidePet)+"吃一口。";
        if(step==3)return G.S.GuideHomeStage>=HomeGuideStage.Complete?"有空陪一会儿，想出门时再出门。":G.S.GuideFarmSeen?"看看莓果怎样成熟、自动收好。":"长大了一点！接着看看食物从哪里来。";
        if(step==4)return G.R.JourneyPaused?"旅程已暂停 · 需要你点击继续；缩小不会解除暂停。":"可以安心去忙 · 缩成宠物后，双击它回来查看收获。";
        if(step==5)return "冒险收获已经入库 · 先返家，再把肥料用到农田。";
        if(step==6)return "让下一份莓果更快成熟 · 用冒险获得的肥料施肥。";
        return "小循环完成 · 选择一个新目标，也可以先歇一会儿。";
    }
    private string GuideFoodDetail()
    {
        int feeds=Math.Max(1,(int)Math.Ceiling((20-G.S.Xp[GuidePet])/12d));
        if(G.S.Berries<2)return "已有莓果 "+G.S.Berries+"。"+CareWaitLabel(0,2)+"；可以到「管理农田」查看种植。";
        return "已有莓果 "+G.S.Berries+"。再喂"+G.Name(GuidePet)+" "+feeds+" 次（共 "+(feeds*2)+" 颗），就能长到 Lv.2。农田会自动收获、补种。";
    }
    private string GuideCombinationEvidence()
    {
        if(G.R.WaterTriggers>0&&G.R.ChainTriggers>0)return "配合已经发生：潮湿追击 "+G.R.WaterTriggers+" 次，闪电跳跃 "+G.R.ChainTriggers+" 次。潮湿让电团追击，连锁还能打到其他敌人。";
        if(G.R.ShieldTriggers>0)return "护盾已经转成攻击：护盾重击触发 "+G.R.ShieldTriggers+" 次。搭配能改变同一只伙伴的作用。";
        if(G.R.PoisonDamage>0)return "中毒已造成 "+G.R.PoisonDamage+" 伤害。持续伤害也能成为一支小队的思路。";
        if(G.R.BurnDamage>0)return "灼烧已造成 "+G.R.BurnDamage+" 伤害。伙伴会自动发挥你选好的技能。";
        return "配好的技能会自动按站位行动；发生配合时，这里会告诉你效果。";
    }
}
