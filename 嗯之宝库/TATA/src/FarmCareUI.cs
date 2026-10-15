using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private int careFarmResource=-1;
    private string careReceipt="";
    private void OpenCare(int pet=-1,bool? evolution=null)
    {
        if(pet>=0){G.SetCareTarget(pet,evolution??G.HasNextEvolution(pet));careReceipt="";}
        else if(G.CurrentCarePlan==null)G.SetCareTarget(G.S.DisplayPet,G.HasNextEvolution(G.S.DisplayPet));
        Go("培养");resetScroll=true;
    }
    private void ChooseCareTarget(int pet,bool evolution){G.SetCareTarget(pet,evolution);careReceipt="";resetScroll=true;}
    private void OpenCareFarm(int kind){Go("农场");careFarmResource=kind;selectedPlanting=kind==0?1:2;plantingOpen=true;resetScroll=true;}
    private void PrepareCareJourney(){if(!G.AtHome)return;journeyDifficulty=0;journeyDepth=0;Go("冒险");resetScroll=true;}
    private string CareWaitLabel(int kind,int cost)
    {
        int stock=kind==0?G.S.Berries:G.S.Grain,missing=Math.Max(0,cost-stock);if(missing==0)return "✓ 材料已齐";
        double seconds=G.CareResourceWait(kind,missing);
        return "还差 "+missing+(seconds<0?" · 目前没有种植":seconds<60?" · 约 "+Math.Max(1,(int)Math.Ceiling(seconds))+" 秒收齐":" · 约 "+(int)Math.Ceiling(seconds/60)+" 分钟收齐");
    }
    private void CareMaterial(StackPanel parent,int kind,int cost)
    {
        if(cost<=0)return;var row=new DockPanel();var icon=new FarmGlyph{Kind=kind==0?"berry":"grain",Width=28,Height=35,Margin=new Thickness(0,1,8,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
        var words=new StackPanel();words.Children.Add(Live(()=>(kind==0?"莓果":"麦穗")+"需 "+cost+" · 已有 "+(kind==0?G.S.Berries:G.S.Grain),13));words.Children.Add(Live(()=>CareWaitLabel(kind,cost),11,U.Green));row.Children.Add(words);parent.Children.Add(row);
    }
    private void RunCareTraining()
    {
        var p=G.CurrentCarePlan;if(p==null)return;int level=G.Level(p.Pet),bond=G.PetBond(p.Pet),attack=G.Attack(p.Pet),hp=G.PermanentHp(p.Pet);
        if(G.ApplyCareTraining())careReceipt="这次培养：Lv."+level+" → Lv."+G.Level(p.Pet)+"\n攻击 "+attack+" → "+G.Attack(p.Pet)+" · 体力 "+hp+" → "+G.PermanentHp(p.Pet)+(p.Evolution?"\n亲密 "+bond+" → "+G.PetBond(p.Pet):"")+"\n消耗莓果 "+p.Berries+"、麦穗 "+p.Grain+"。";
    }
    private void RunCareEvolution()
    {
        var p=G.CurrentCarePlan;if(p==null)return;string before=G.Name(p.Pet);int attack=G.Attack(p.Pet),hp=G.PermanentHp(p.Pet);
        if(G.CompleteCareEvolution())careReceipt=before+" → "+G.Name(p.Pet)+"\n攻击 "+attack+" → "+G.Attack(p.Pet)+" · 体力 "+hp+" → "+G.PermanentHp(p.Pet)+"\n消耗晶石 "+p.Crystals+"；等级与队伍位置保留。";
    }
    private UIElement CareGoalCard()
    {
        var p=G.CurrentCarePlan;if(p==null)return new Border();var stack=new StackPanel();stack.Children.Add(U.Text(p.Done?"成长目标完成":"正在培养 · "+G.Name(p.Pet),16));stack.Children.Add(U.Text(G.Name(p.Pet)+" · "+G.CareTargetName(p),12,U.Green));stack.Children.Add(Btn("查看培养计划",()=>OpenCare()));return U.Card(stack,12);
    }
    private void CarePage()
    {
        if(G.CurrentCarePlan==null)G.SetCareTarget(G.S.DisplayPet,G.HasNextEvolution(G.S.DisplayPet));var p=G.CurrentCarePlan;var cols=Columns(236);var left=cols[0];var aside=cols[1];
        Heading(left,"慢慢长成，喜欢的样子。","选一个成长目标，把需要的材料安排明白。");
        var pets=new UniformGrid{Columns=5};left.Children.Add(pets);
        for(int i=0;i<FarmGame.PetCount;i++){int pet=i;var b=Btn("",()=>ChooseCareTarget(pet,G.HasNextEvolution(pet)),true,p.Pet==pet);var content=new StackPanel();content.Children.Add(Portrait(pet,33));var label=U.Text(G.Name(pet),10,p.Pet==pet?FarmTheme.Paper:U.Ink);label.TextAlignment=TextAlignment.Center;label.Margin=new Thickness(0);content.Children.Add(label);b.Content=content;b.Padding=new Thickness(3,4,3,4);b.Margin=new Thickness(0,0,5,8);System.Windows.Automation.AutomationProperties.SetName(b,"培养"+G.Name(pet));pets.Children.Add(b);}
        U.Row(left,Btn("先升一级",()=>ChooseCareTarget(p.Pet,false),G.Level(p.Pet)<10,!p.Evolution),Btn("准备下一次进化",()=>ChooseCareTarget(p.Pet,true),G.HasNextEvolution(p.Pet),p.Evolution));
        var hero=new StackPanel();hero.Children.Add(FarmTheme.Badge((p.Done?"✓ 已完成 · ":"当前目标 · ")+G.CareTargetName(p)));var row=new DockPanel();var portrait=Portrait(p.Pet,92);portrait.Width=108;DockPanel.SetDock(portrait,Dock.Left);row.Children.Add(portrait);var stats=new StackPanel{Margin=new Thickness(8,9,0,0)};stats.Children.Add(U.Text(G.Name(p.Pet)+"  Lv."+G.Level(p.Pet),18));stats.Children.Add(PetTags(p.Pet));stats.Children.Add(U.Text("攻击 "+G.Attack(p.Pet)+" · 体力 "+G.PermanentHp(p.Pet),13));row.Children.Add(stats);hero.Children.Add(row);
        if(!p.Done){int delta=p.AfterLevel-G.Level(p.Pet);hero.Children.Add(U.Text("培养后：Lv."+p.AfterLevel+" · 攻击 "+(G.Attack(p.Pet)+delta*2)+" · 体力 "+(G.PermanentHp(p.Pet)+delta*6),13,U.Green));
            hero.Children.Add(U.Text("喂养 "+p.Feeds+" 次 + 训练 "+p.Trainings+" 次"+(p.Evolution?" · 亲密 "+G.PetBond(p.Pet)+" → "+p.AfterBond:""),12,U.Muted));
            if(p.Evolution)hero.Children.Add(U.Text("先喂到本阶所需亲密，再用麦穗补足等级；成长余量保留。",11,U.Muted));
        }else hero.Children.Add(U.Text("这一步已经完成，不会自动消耗材料继续培养。",12,U.Green));
        left.Children.Add(U.Card(hero,12));
        if(careReceipt!="")left.Children.Add(Notice("刚刚的成长",careReceipt));
        if(p.Evolution){var change=new StackPanel();change.Children.Add(U.Text(p.Done?"这一阶的新能力":"进化后会有什么变化",16));
            if(p.Pet==4){var future=SeedPreview(p.Stage,74);future.HorizontalAlignment=HorizontalAlignment.Left;change.Children.Add(future);change.Children.Add(U.Text(FarmGame.SeedChanges[p.Stage]+"\n本次进化额外增加攻击 2、体力 6。",12,U.Green));}
            else change.Children.Add(U.Text("开场让全体敌人潮湿；农场永久生长 +15%。本次进化额外增加攻击 3、体力 18。",12,U.Green));
            if(!p.Done&&p.Stage==1)change.Children.Add(U.Text("首次进化后，可探索潮汐洞穴。",11,U.Muted));left.Children.Add(U.Card(change,12));}
        var materials=new StackPanel();materials.Children.Add(U.Text(p.Done?"这一步，已经长大了":"这次需要的材料",18));
        if(p.Done){materials.Children.Add(U.Text("可以去冒险试试搭配，或亲手安排下一个目标。",12,U.Muted));var next=Btn("继续下个成长目标",()=>ChooseCareTarget(p.Pet,G.HasNextEvolution(p.Pet)),G.HasNextEvolution(p.Pet)||G.Level(p.Pet)<10,true);FarmTheme.Accent(next);materials.Children.Add(next);materials.Children.Add(Btn("去冒险试试",()=>Go("冒险")));}
        else{
            CareMaterial(materials,0,p.Berries);CareMaterial(materials,1,p.Grain);
            if(p.Evolution){materials.Children.Add(Live(()=>"晶石需 "+p.Crystals+" · 已有 "+G.S.Crystals,13));materials.Children.Add(Live(()=>G.S.Crystals>=p.Crystals?"✓ 晶石已齐，进化时消耗":"还差 "+(p.Crystals-G.S.Crystals)+" · 从冒险首领带回",11,U.Green));}
            if(p.HasTraining){var train=Btn("一次完成这步培养",RunCareTraining,true,true);FarmTheme.Accent(train);updates.Add(()=>{train.IsEnabled=G.AtHome&&G.S.Berries>=p.Berries&&G.S.Grain>=p.Grain;train.ToolTip=!G.AtHome?"返家后可培养。":"共消耗莓果 "+p.Berries+"、麦穗 "+p.Grain+"，不消耗晶石；材料不足不部分扣费。";});materials.Children.Add(train);}
            if(p.Evolution){var evolve=Btn("确认进化 · 晶石 "+p.Crystals,RunCareEvolution,true,!p.HasTraining);if(!p.HasTraining)FarmTheme.Accent(evolve);updates.Add(()=>evolve.IsEnabled=G.AtHome&&!G.CurrentCarePlan.HasTraining&&G.S.Crystals>=p.Crystals);materials.Children.Add(evolve);}
            materials.Children.Add(U.Text(!G.AtHome?"返家后才能培养和进化。当前旅程与暂停状态保留。":p.HasTraining?"按上方总材料计费，不足时不扣除。"+(p.Evolution?"进化另行确认。":""):"等级与亲密已经满足，晶石齐了就能进化。",11,U.Muted));
        }
        aside.Children.Add(U.Card(materials,12));
        if(!p.Done){var sources=new StackPanel();sources.Children.Add(U.Text("材料从哪里来",16));if(p.Berries>0)sources.Children.Add(Btn("安排莓果种植",()=>OpenCareFarm(0)));if(p.Grain>0)sources.Children.Add(Btn("安排麦穗种植",()=>OpenCareFarm(1)));
            if(p.Evolution){sources.Children.Add(Live(()=>G.S.Crystals>=p.Crystals?"晶石够了，可继续准备成长和亲密。":"小溪初探通关获得晶石 2；按当前缺口，还需通关 "+(int)Math.Ceiling((p.Crystals-G.S.Crystals)/2.0)+" 次。",12,U.Muted));sources.Children.Add(Btn("准备去小溪找晶石",PrepareCareJourney,G.AtHome));}
            sources.Children.Add(U.Text("收齐时间按当前田地、土壤和帮手估算；离线最多结算 4 小时收成。",11,U.Muted));aside.Children.Add(U.Card(sources,12));}
        aside.Children.Add(Btn("查看伙伴与单次喂养",()=>Go("伙伴")));aside.Children.Add(Btn("返回农场",()=>Go("农场")));
    }
    private void CarePlantingPage()
    {
        var p=G.CurrentCarePlan;var cols=Columns(236);var left=cols[0];var aside=cols[1];
        Heading(left,"先给成长备一点材料",(p==null?"伙伴":G.Name(p.Pet))+"需要"+(careFarmResource==0?"莓果":"麦穗")+"。先预览方案，再决定是否改种。");
        left.Children.Add(PlantingPlanner());left.Children.Add(U.Text("下方是当前田地。仅查看方案不会改动种植；应用后，仅改种的田地重新计时。",12,U.Muted));var scene=Scene();scene.Height=scene.MinHeight=174;left.Children.Add(scene);
        if(p!=null){var target=new StackPanel();target.Children.Add(U.Text(G.Name(p.Pet)+"的培养目标",17));target.Children.Add(U.Text(G.CareTargetName(p),13,U.Green));CareMaterial(target,careFarmResource,careFarmResource==0?p.Berries:p.Grain);target.Children.Add(Btn("返回培养计划",()=>OpenCare(),true,true));aside.Children.Add(U.Card(target,12));}
        aside.Children.Add(Btn("查看完整农场",()=>Go("农场")));
    }
}
