using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public sealed class FarmAwayHarvest
{
    public long FromTicks,ToTicks;
    public double CountedSeconds;
    public int[] Added=new int[3];
}

public partial class FarmGame
{
    public FarmAwayHarvest ValidAwayHarvest
    {
        get{
            var item=S.LastAwayHarvest;
            return item!=null&&item.FromTicks>0&&item.FromTicks<item.ToTicks&&item.ToTicks<=DateTime.MaxValue.Ticks&&
                !Double.IsNaN(item.CountedSeconds)&&!Double.IsInfinity(item.CountedSeconds)&&item.CountedSeconds>=300&&item.CountedSeconds<=14400&&
                item.Added!=null&&item.Added.Length==3&&item.Added.All(v=>v>=0&&v<=99999)&&item.Added.Any(v=>v>0)?item:null;
        }
    }
    private void NormalizeAwayHarvest(){if(ValidAwayHarvest==null)S.LastAwayHarvest=null;}
    private void SettleLoadedFarm(DateTime now)
    {
        long from=S.LastFarmTicks;var before=new[]{S.Berries,S.Grain,S.Mint};TickFarm(now);
        double elapsed=(now.ToUniversalTime().Ticks-from)/(double)TimeSpan.TicksPerSecond;
        var added=new[]{S.Berries-before[0],S.Grain-before[1],S.Mint-before[2]};
        if(elapsed>=300&&added.Any(v=>v>0))S.LastAwayHarvest=new FarmAwayHarvest{FromTicks=from,ToTicks=now.ToUniversalTime().Ticks,CountedSeconds=Math.Min(14400,elapsed),Added=added};
    }
}

public partial class FarmPanel
{
    private FarmAwayHarvest awayHarvestPage;
    private int awayHarvestKind;
    private void OpenAwayHarvest()
    {
        OpenLife("离开时的收成");awayHarvestPage=G.ValidAwayHarvest;
        if(awayHarvestPage==null)return;
        var reminder=G.S.SmallReminder;int[] needs=G.SmallResourceNeeds(reminder);
        awayHarvestKind=Enumerable.Range(0,3).Where(k=>awayHarvestPage.Added[k]>0&&needs[k]>0).DefaultIfEmpty(Enumerable.Range(0,3).First(k=>awayHarvestPage.Added[k]>0)).First();
    }
    private void AwayHarvestPage()
    {
        var item=G.ValidAwayHarvest;var panel=GentleColumn(440);var current=LifeGuard();
        if(item==null){panel.Children.Add(GentleText("田里的收成会自动收好",21));panel.Children.Add(GentleAction("回小院",()=>{if(current())Go("农场");}));return;}
        if(!Object.ReferenceEquals(awayHarvestPage,item)){awayHarvestPage=item;awayHarvestKind=Enumerable.Range(0,3).First(k=>item.Added[k]>0);}
        int kind=awayHarvestKind;var kinds=Enumerable.Range(0,3).Where(k=>item.Added[k]>0).ToArray();int index=Array.IndexOf(kinds,kind);
        var reminder=G.ValidSmallReminder(G.S.SmallReminder)?G.S.SmallReminder:null;
        panel.Children.Add(GentleText("你离开时，田里也在长",21));
        panel.Children.Add(new FarmGlyph{Kind=new[]{"berry","grain","mint"}[kind],Width=64,Height=64,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(GentleText(FarmGame.CropNames[kind]+" +"+item.Added[kind]+" · 已经收好",17,U.Green));
        var stock=Live(()=>"现在有 "+G.CropStock(kind)+" 份"+FarmGame.CropNames[kind],12,U.Muted);stock.TextAlignment=TextAlignment.Center;panel.Children.Add(stock);
        panel.Children.Add(GentleText(new DateTime(item.ToTicks,DateTimeKind.Utc).ToLocalTime().ToString("M月d日 HH:mm")+" 回来时记录",12,U.Muted));
        panel.Children.Add(GentleAction(reminder==null?"回小院":"接着上次的小事",()=>{if(!current()||!Object.ReferenceEquals(item,G.ValidAwayHarvest))return;if(reminder!=null&&Object.ReferenceEquals(reminder,G.S.SmallReminder))OpenSmallResources(reminder);else Go("农场");}));
        var links=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(links);
        if(kinds.Length>1)links.Children.Add(Btn("看看另一种收成",()=>{if(current())LifeChange(()=>awayHarvestKind=kinds[(index+1)%kinds.Length]);}));
        if(reminder!=null)links.Children.Add(Btn("先回小院",()=>{if(current())Go("农场");}));
        bool capped=(item.ToTicks-item.FromTicks)/(double)TimeSpan.TicksPerSecond>14400;
        LifeDetails(panel,"看看这次怎么算的","away-harvest-details",U.Text(capped?"离开超过 4 小时，这次按 4 小时计算。":"这次计算了约 "+Math.Floor(item.CountedSeconds/60)+" 分钟的生长。",12,U.Muted),U.Text("这里记录的是已经放进篮子的数量。翻看不会再次发放；冒险收获另外记录。",12,U.Muted));
    }
}
