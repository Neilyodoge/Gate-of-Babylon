using System;
using System.Linq;

public sealed class HomecomingQuote
{
    internal FarmRun Source;
    internal string Key;
    public bool Claim,Soil,Train;
    public int[] Chapters,Cost,Gains,After,BeforeLevels,AfterLevels,AfterXp;
    public bool Available;
}
public class HomecomingReceipt
{
    public long Ticks;
    public int Region,Depth,Chapters,SoilBefore,SoilAfter;
    public int[] Gained=new int[5],Spent=new int[5],BeforeLevels=new int[3],AfterLevels=new int[3],BeforeHp=new int[3],AfterHp=new int[3],BeforeAttack=new int[3],AfterAttack=new int[3];
    public string[] Names=new string[3];
    public double[] RatesBefore=new double[3],RatesAfter=new double[3];
}
public partial class FarmGame
{
    public static readonly string[] HomecomingResources={"莓果","麦穗","薄荷","肥料","晶石"};
    public bool MayArrangeHomecoming{get{return AtHome||R.Phase=="result";}}
    private int[] HomecomingStocks(){return new[]{S.Berries,S.Grain,S.Mint,S.Fertilizer,S.Crystals};}
    private static int HomecomingCredit(int stock,int gain){return gain<=0?stock:(int)Math.Max(stock,Math.Min(99999L,(long)stock+gain));}
    private string HomecomingKey()
    {
        return String.Join("|",new[]{R.Phase,String.Join(",",S.Team),String.Join(",",S.Xp),S.SoilLevel.ToString(),S.Evolved.ToString(),S.SeedStage.ToString(),Caretaker.ToString(),String.Join(",",S.Skills),S.GardenUnlocked.ToString(),String.Join(",",S.Crops),String.Join(",",S.ChapterClaimed),String.Join(",",Enumerable.Range(0,ChapterNames.Length).Select(ChapterDone))});
    }
    public HomecomingQuote QuoteHomecoming(bool claim,bool soil,bool train)
    {
        var q=new HomecomingQuote{Source=R,Key=HomecomingKey(),Claim=claim,Soil=soil,Train=train,Cost=new int[5],Gains=new int[5],After=HomecomingStocks(),BeforeLevels=S.Team.Select(Level).ToArray(),AfterLevels=S.Team.Select(Level).ToArray(),AfterXp=S.Team.Select(p=>S.Xp[p]).ToArray()};
        q.Chapters=claim?Enumerable.Range(0,ChapterNames.Length).Where(i=>!S.ChapterClaimed[i]&&ChapterDone(i)).ToArray():new int[0];
        foreach(int id in q.Chapters)for(int k=0;k<5;k++)q.Gains[k]+=ChapterRewards[id][k];
        for(int k=0;k<5;k++){int credited=HomecomingCredit(q.After[k],q.Gains[k]);q.Gains[k]=credited-q.After[k];q.After[k]=credited;}
        if(soil&&S.SoilLevel<5)q.Cost[3]=SoilCost;
        if(train)for(int i=0;i<3;i++){
            int cost=TrainToNextCost(S.Team[i]);q.Cost[1]+=cost;q.AfterXp[i]+=cost/3*18;
            q.AfterLevels[i]=1;for(int level=1;level<CareThresholds.Length;level++)if(q.AfterXp[i]>=CareThresholds[level])q.AfterLevels[i]=level+1;
        }
        for(int k=0;k<5;k++)q.After[k]-=q.Cost[k];
        q.Available=MayArrangeHomecoming&&(!soil||S.SoilLevel<5)&&(!train||q.Cost[1]>0)&&(q.Chapters.Length>0||q.Cost.Any(n=>n>0));return q;
    }
    public bool HomecomingQuoteCurrent(HomecomingQuote q){return q!=null&&MayArrangeHomecoming&&Object.ReferenceEquals(q.Source,R)&&q.Key==HomecomingKey();}
    public bool CanApplyHomecoming(HomecomingQuote q){if(!HomecomingQuoteCurrent(q))return false;var fresh=QuoteHomecoming(q.Claim,q.Soil,q.Train);return fresh.Available&&fresh.After.All(n=>n>=0);}
    public string HomecomingShortage(HomecomingQuote q)
    {
        if(!HomecomingQuoteCurrent(q))return "成长或配置已变化，点「刷新安排」重新核对。";
        var fresh=QuoteHomecoming(q.Claim,q.Soil,q.Train);if(!fresh.Available)return "先勾选至少一项可完成的安排。";
        var gaps=Enumerable.Range(0,5).Where(k=>fresh.After[k]<0).Select(k=>HomecomingResources[k]+" "+(-fresh.After[k])).ToArray();return gaps.Length==0?"材料够了，确认后只执行勾选的项目。":"还差 "+String.Join(" · ",gaps)+"；可取消部分安排。";
    }
    public bool ApplyHomecoming(HomecomingQuote quote)
    {
        if(!CanApplyHomecoming(quote))return false;
        // Settle elapsed growth at the old soil speed before applying any upgrade.
        var reviewed=QuoteHomecoming(quote.Claim,quote.Soil,quote.Train);TickFarm(DateTime.UtcNow);var q=QuoteHomecoming(quote.Claim,quote.Soil,quote.Train);
        // Only claim chapters present in the reviewed offer, never newly earned ones.
        q.Chapters=(int[])reviewed.Chapters.Clone();var stock=HomecomingStocks();q.Gains=new int[5];
        foreach(int id in q.Chapters)for(int k=0;k<5;k++)q.Gains[k]+=ChapterRewards[id][k];
        for(int k=0;k<5;k++){int credited=HomecomingCredit(stock[k],q.Gains[k]);q.Gains[k]=credited-stock[k];q.After[k]=credited-q.Cost[k];}
        var last=S.JourneyMemories.FirstOrDefault();int region=R.Phase=="result"?R.Difficulty:last==null?0:last.Region,depth=R.Phase=="result"?R.Depth:last==null?0:last.Depth;
        var receipt=new HomecomingReceipt{Ticks=DateTime.UtcNow.Ticks,Region=region,Depth=depth,Chapters=q.Chapters.Length,SoilBefore=S.SoilLevel,SoilAfter=S.SoilLevel+(q.Soil?1:0),Gained=(int[])q.Gains.Clone(),Spent=(int[])q.Cost.Clone(),BeforeLevels=S.Team.Select(Level).ToArray(),Names=S.Team.Select(Name).ToArray(),BeforeHp=S.Team.Select(PermanentHp).ToArray(),BeforeAttack=S.Team.Select(Attack).ToArray(),RatesBefore=FarmRates()};
        if(R.Phase=="result")S.Run=new FarmRun();
        S.Berries=q.After[0];S.Grain=q.After[1];S.Mint=q.After[2];S.Fertilizer=q.After[3];S.Crystals=q.After[4];
        foreach(int id in q.Chapters)S.ChapterClaimed[id]=true;if(q.Soil)S.SoilLevel++;
        if(q.Train)for(int i=0;i<3;i++)S.Xp[S.Team[i]]=q.AfterXp[i];
        receipt.AfterLevels=S.Team.Select(Level).ToArray();receipt.AfterHp=S.Team.Select(PermanentHp).ToArray();receipt.AfterAttack=S.Team.Select(Attack).ToArray();receipt.RatesAfter=FarmRates();S.LastHomecoming=receipt;
        S.Notice="归家安排完成："+String.Join("，",new[]{q.Chapters.Length>0?"领取 "+q.Chapters.Length+" 项成长奖励":null,q.Soil?"土壤升到 Lv."+S.SoilLevel:null,q.Train?"出战伙伴完成一级训练":null}.Where(x=>x!=null).ToArray())+"。下一趟等你决定。";Save();return true;
    }
    private void NormalizeHomecoming()
    {
        var r=S.LastHomecoming;if(r==null)return;
        bool valid=r.Ticks>0&&r.Ticks<=DateTime.MaxValue.Ticks&&r.Region>=0&&r.Region<2&&r.Depth>=0&&r.Depth<3&&r.Chapters>=0&&r.Chapters<=8&&r.SoilBefore>=0&&r.SoilBefore<=5&&r.SoilAfter>=r.SoilBefore&&r.SoilAfter<=5;
        valid=valid&&r.Names!=null&&r.Names.Length==3&&r.Names.All(n=>!String.IsNullOrEmpty(n));
        valid=valid&&new[]{r.Gained,r.Spent}.All(a=>a!=null&&a.Length==5&&a.All(n=>n>=0));
        valid=valid&&new[]{r.BeforeLevels,r.AfterLevels,r.BeforeHp,r.AfterHp,r.BeforeAttack,r.AfterAttack}.All(a=>a!=null&&a.Length==3&&a.All(n=>n>=0));
        valid=valid&&new[]{r.RatesBefore,r.RatesAfter}.All(a=>a!=null&&a.Length==3&&a.All(n=>!Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=0));
        if(!valid){S.LastHomecoming=null;return;}for(int i=0;i<3;i++)r.Names[i]=ShortText(r.Names[i],30);
    }
    public static string HomecomingAmounts(int[] amounts,string sign){var rows=Enumerable.Range(0,5).Where(k=>amounts[k]>0).Select(k=>HomecomingResources[k]+" "+sign+amounts[k]).ToArray();return rows.Length==0?"无":String.Join(" · ",rows);}
    public double[] HomecomingSoilRates(bool upgrade)
    {
        var rates=FarmRates();if(!upgrade||S.SoilLevel>=5)return rates;
        for(int k=0;k<3;k++){double speed=new[]{45.0,60.0,75.0}[k]/CropSeconds(k);rates[k]*=(speed+.12)/speed;}return rates;
    }
}
