using System;
using System.Linq;

public partial class FarmGame
{
    public static readonly string[] GardenSpotNames={"左侧花圃","中央小径","右侧篱笆"};
    public static readonly string[] DecorationStories={"从小溪边带回的风车，把旅途里的风留在院子里。","一盏蘑菇模样的小灯，给晚归的伙伴留个位置。","河岸上捡到的小花盆，正适合放在田边。","洞穴深处的蓝色晶簇，留住了潮水的颜色。","贝壳串成的小风铃，把海边的回忆挂起来。","弯弯的灯架和一小团月光，陪伙伴慢慢长大。"};
    public bool DecorationOwned(int id){return id>=0&&id<6&&S.Decorations[id];}
    private void NormalizeDecorating()
    {
        if(S.Decorations==null||S.Decorations.Length!=6)S.Decorations=new bool[6];
        if(!DecorationOwned(S.DisplayDecoration))S.DisplayDecoration=-1;
        if(S.DecorationLayoutVersion<1){S.GardenDecorations=new[]{-1,-1,S.DisplayDecoration};S.DecorationLayoutVersion=1;}
        if(S.GardenDecorations==null||S.GardenDecorations.Length!=3){var old=S.GardenDecorations;S.GardenDecorations=new[]{-1,-1,-1};if(old!=null)Array.Copy(old,S.GardenDecorations,Math.Min(3,old.Length));}
        for(int i=0;i<3;i++)if(!DecorationOwned(S.GardenDecorations[i])||S.GardenDecorations.Take(i).Contains(S.GardenDecorations[i]))S.GardenDecorations[i]=-1;
        if(S.DecorationWish< -1||S.DecorationWish>=6)S.DecorationWish=-1;
    }
    public int NextDecoration(int region){return region>=0&&region<2?Enumerable.Range(region*3,3).Where(i=>!S.Decorations[i]).DefaultIfEmpty(-1).First():-1;}
    public int DecorationTripsLeft(int id){return id>=0&&id<6&&!DecorationOwned(id)?Enumerable.Range(id/3*3,id%3+1).Count(i=>!S.Decorations[i]):0;}
    public string DecorationSource(int id)
    {
        if(id<0||id>=6)return "";
        if(DecorationOwned(id))return "已收藏 · 可免费摆放";
        return (id<3?"小溪":"洞穴")+"任一深度再通关 "+DecorationTripsLeft(id)+" 次";
    }
    public bool SetDecorationWish(int id)
    {
        if(id< -1||id>=6||S.DecorationWish==id)return false;
        S.DecorationWish=id;S.Notice=id<0?"已收起装饰收藏目标。":"想带回「"+DecorationNames[id]+"」："+DecorationSource(id)+"。";Save();return true;
    }
    public bool PlaceGardenDecoration(int slot,int id)
    {
        if(slot<0||slot>=3||id< -1||id>=6||(id>=0&&!DecorationOwned(id)))return false;
        NormalizeDecorating();if(S.GardenDecorations[slot]==id)return false;
        int previous=S.GardenDecorations[slot],other=id<0?-1:Array.IndexOf(S.GardenDecorations,id);
        if(other>=0)S.GardenDecorations[other]=previous;S.GardenDecorations[slot]=id;
        S.Notice=id<0?"已收起"+GardenSpotNames[slot]+"的装饰。":"「"+DecorationNames[id]+"」摆在"+GardenSpotNames[slot]+(other>=0?"，与原摆件互换位置。":"。");Save();return true;
    }
    public int[] PreviewGardenDecoration(int slot,int id)
    {
        var layout=(int[])S.GardenDecorations.Clone();
        if(slot<0||slot>=3||id< -1||id>=6)return layout;
        int other=id<0?-1:Array.IndexOf(layout,id);if(other>=0)layout[other]=layout[slot];layout[slot]=id;return layout;
    }
    private void ReceiveDecoration(int id)
    {
        bool first=!S.Decorations.Any(x=>x);S.Decorations[id]=true;R.DecorationReward=id;
        if(first&&S.DisplayDecoration<0)S.DisplayDecoration=id;
        if(first&&S.GardenDecorations.All(i=>i<0))S.GardenDecorations[1]=id;
    }
}
