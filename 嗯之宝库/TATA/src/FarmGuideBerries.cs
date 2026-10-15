using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmSave guideBerryState;
    private string guideBerryReturn="伙伴";
    private int guideBerryPet,guideBerryPlot=-1,guideBerryChoice,guideBerryRevision;
    private bool guideBerryPlanting;
    private bool GuideBerryScreen{get{return G.GuideActive&&Tab=="找莓果";}}
    private TextBlock GuideCareLine(int pet,Func<string> fallback,bool needsFood)
    {
        var line=Live(()=>needsFood&&G.S.Berries<2?CareWaitLabel(0,2):CompanionWords(pet,fallback),15,U.Green);line.TextAlignment=TextAlignment.Center;
        AutomationProperties.SetAutomationId(line,"companion-response");return line;
    }
    private void OpenGuideBerries(int pet)
    {
        if(!G.GuideActive||!G.AtHome||pet<0||pet>=FarmGame.PetCount)return;
        guideBerryState=G.S;guideBerryPet=pet;guideBerryReturn=Tab;guideBerryPlot=G.ResourcePlot(0);guideBerryPlanting=false;guideBerryRevision++;
        Go("找莓果");resetScroll=true;
    }
    private Func<bool> GuideBerryGuard()
    {
        var state=G.S;int revision=guideBerryRevision,visit=courtyardVisit;
        return ()=>Object.ReferenceEquals(state,G.S)&&Object.ReferenceEquals(guideBerryState,G.S)&&GuideBerryScreen&&G.AtHome&&revision==guideBerryRevision&&visit==courtyardVisit;
    }
    private int GuideBerryPlot()
    {
        if(guideBerryPlot<0||guideBerryPlot>=G.Plots||G.S.Crops[guideBerryPlot]!=0)guideBerryPlot=G.ResourcePlot(0);
        return guideBerryPlot;
    }
    private void ReturnGuideBerries()
    {
        if(!G.AtHome){Go("冒险");return;}
        string target=!G.GuideActive?"农场":G.S.GuideStep==0?"新手引导":guideBerryReturn=="农场"?"农场":"伙伴";
        if(target=="伙伴")SelectGuidePetIfNeeded(guideBerryPet);
        guideBerryPlanting=false;guideBerryRevision++;Go(target);pageHistory.RemoveAll(t=>t=="找莓果");resetScroll=true;
    }
    private void BackFromGuideBerries()
    {
        if(guideBerryPlanting&&Object.ReferenceEquals(guideBerryState,G.S)&&G.AtHome){guideBerryPlanting=false;guideBerryRevision++;resetScroll=true;}
        else ReturnGuideBerries();
    }
    private void PreviewGuideBerryPlanting()
    {
        guideBerryChoice=Enumerable.Range(0,G.Plots).OrderBy(i=>G.S.Growth[i]/G.CropSeconds(G.S.Crops[i])).First();
        guideBerryPlanting=true;guideBerryRevision++;resetScroll=true;
    }
    private string GuideBerryReturnLabel(bool ready)
    {
        return guideBerryReturn=="农场"?"回到刚才那块田":(ready?"回去喂":"先回去陪")+G.Name(guideBerryPet);
    }
    private Button GuideBerryAction(string label,Action action)
    {
        var button=GentleAction(label,action);button.MinHeight=44;button.Margin=new Thickness(0,6,0,4);return button;
    }
    private void GuideBerriesPage()
    {
        if(!Object.ReferenceEquals(guideBerryState,G.S)||!G.GuideActive||!G.AtHome){
            var empty=GentleColumn(420);empty.Children.Add(GentleText(G.AtHome?"回去接着陪伙伴吧":"伙伴还在外面",21));empty.Children.Add(GuideBerryAction(G.AtHome?"回去陪伙伴":"看看当前旅程",ReturnGuideBerries));return;
        }
        if(guideBerryPlanting){GuideBerryPlantingPage();return;}
        var state=G.S;var current=GuideBerryGuard();var panel=GentleColumn(420);
        var title=Live(()=>state.Berries>=2?"莓果够喂一口啦":GuideBerryPlot()<0?"田里还没种莓果":"等这一批莓果",21);title.TextAlignment=TextAlignment.Center;panel.Children.Add(title);
        var picture=new Grid{Height=112,Margin=new Thickness(0,0,0,6)};panel.Children.Add(picture);
        var scene=Scene();scene.FocusedFarmPet=guideBerryPet;scene.Height=scene.MinHeight=112;scene.Margin=new Thickness(0);MakeSceneTouchable(scene,guideBerryPet);picture.Children.Add(scene);
        var icon=new FarmGlyph{Kind="berry",Width=62,Height=62,HorizontalAlignment=HorizontalAlignment.Center};picture.Children.Add(icon);
        var stock=Live(()=>"篮子里有 "+state.Berries+" 颗 · 喂一口要 2 颗",13,U.Green);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        var progress=new StackPanel{MaxWidth=330,Margin=new Thickness(0,2,0,5)};panel.Children.Add(progress);
        var label=GentleText("",12,U.Muted);progress.Children.Add(label);var bar=U.Bar(0,100);bar.Height=10;bar.Margin=new Thickness(0);progress.Children.Add(bar);AutomationProperties.SetAutomationId(bar,"guide-berry-source-progress");
        var hint=Live(()=>state.Berries>=2?"回去以后，再喂给"+G.Name(guideBerryPet)+"。":GuideBerryPlot()<0?"先看一块田，确认后才改种。":"成熟后会自动收好，不用一直等在这里。",12,U.Muted);hint.TextAlignment=TextAlignment.Center;panel.Children.Add(hint);
        var action=GuideBerryAction("",()=>{if(!current())return;if(state.Berries<2&&GuideBerryPlot()<0)PreviewGuideBerryPlanting();else ReturnGuideBerries();});panel.Children.Add(action);
        var back=Btn(GuideBerryReturnLabel(false),()=>{if(current())ReturnGuideBerries();});back.HorizontalAlignment=HorizontalAlignment.Center;back.MinHeight=36;panel.Children.Add(back);
        updates.Add(()=>{
            int plot=GuideBerryPlot();bool ready=state.Berries>=2,missing=plot<0;
            scene.LearningPlot=plot;scene.ShowBerryBasket=!missing;scene.Visibility=missing?Visibility.Collapsed:Visibility.Visible;icon.Visibility=missing?Visibility.Visible:Visibility.Collapsed;
            progress.Visibility=missing||ready?Visibility.Collapsed:Visibility.Visible;
            if(!missing){bar.Value=Math.Min(100,100*state.Growth[plot]/G.CropSeconds(0));label.Text="第 "+(plot+1)+" 块田 · 约 "+G.SecondsLeft(plot)+" 秒后收好 "+G.CropYield(0)+" 颗";AutomationProperties.SetName(bar,"正在看的莓果田生长进度");AutomationProperties.SetHelpText(bar,label.Text);scene.InvalidateVisual();}
            action.Content=!ready&&missing?"选一块田种莓果":GuideBerryReturnLabel(ready);back.Visibility=!ready&&missing?Visibility.Visible:Visibility.Collapsed;
        });
    }
    private void GuideBerryPlantingPage()
    {
        var state=G.S;var current=GuideBerryGuard();int plot=Math.Max(0,Math.Min(G.Plots-1,guideBerryChoice)),kind=state.Crops[plot];var panel=GentleColumn(420);
        panel.Children.Add(GentleText("在这块田种莓果",21));
        var scene=Scene();scene.FocusedFarmPet=guideBerryPet;scene.LearningPlot=plot;scene.Height=scene.MinHeight=100;scene.Margin=new Thickness(0,0,0,6);panel.Children.Add(scene);
        var crop=Live(()=>"第 "+(plot+1)+" 块田 · 现在种着"+FarmGame.CropNames[state.Crops[plot]],14,U.Green);crop.TextAlignment=TextAlignment.Center;panel.Children.Add(crop);
        var timing=Live(()=>"约 "+G.SecondsLeft(plot)+" 秒后，收好这一批。",12,U.Muted);timing.TextAlignment=TextAlignment.Center;panel.Children.Add(timing);
        var caution=Live(()=>state.Crops[plot]==0?"已经种着莓果啦，不用重新种。":"确认后换成莓果，只有这块田从头生长。",12,U.Muted);caution.TextAlignment=TextAlignment.Center;panel.Children.Add(caution);
        var plant=GuideBerryAction("",()=>{
            if(!current()||guideBerryChoice!=plot)return;
            if(state.Crops[plot]!=0&&state.Crops[plot]!=kind){guideBerryRevision++;return;}
            if(state.Crops[plot]==0||G.Plant(plot,0)){guideBerryPlot=plot;guideBerryPlanting=false;guideBerryRevision++;resetScroll=true;}
        });panel.Children.Add(plant);
        updates.Add(()=>{plant.Content=state.Crops[plot]==0?"看看这块莓果田":"把这块田改种莓果";scene.InvalidateVisual();});
        var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(row);
        var next=Btn("换一块看看",()=>{if(current()){guideBerryChoice=(plot+1)%G.Plots;guideBerryRevision++;resetScroll=true;}},G.Plots>1);next.MinHeight=36;next.Margin=new Thickness(0,3,8,3);row.Children.Add(next);
        var cancel=Btn("先不改，返回",()=>{if(current())BackFromGuideBerries();});cancel.MinHeight=36;row.Children.Add(cancel);
    }
}
