using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

public static class FarmDecoratingTests
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception("Decorating: "+label);}
    private static FarmGame New(string root){return new FarmGame(Path.Combine(root,"decorating-"+Guid.NewGuid().ToString("N")+".xml"));}
    private static void Win(FarmGame g,int region=0,int depth=0){g.S.BlessingOffers=new[]{0,1,3};Check(g.BeginJourney(region,0,depth),"fixture departs");g.AdvanceJourney(DateTime.UtcNow.AddHours(2));Check(g.R.Outcome=="victory","fixture wins");}
    public static string Run(string root)
    {
        var g=New(root);g.S.Xp=Enumerable.Repeat(1250,5).ToArray();g.S.Evolved=true;g.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;
        Check(g.S.GardenDecorations.SequenceEqual(new[]{-1,-1,-1})&&g.NextDecoration(0)==0&&g.NextDecoration(1)==3&&g.NextDecoration(2)==-1,"fresh gallery is empty and next rewards are regional");
        Check(g.SetDecorationWish(2)&&g.DecorationTripsLeft(2)==3&&!g.SetDecorationWish(99)&&!g.PlaceGardenDecoration(0,2),"wish does not grant an unowned decoration");
        var preview=g.PreviewGardenDecoration(1,2);preview[0]=1;Check(g.S.GardenDecorations.All(i=>i<0)&&g.S.Decorations.All(x=>!x),"preview is detached and never grants ownership");
        Win(g);Check(g.R.DecorationReward==0&&g.S.GardenDecorations.SequenceEqual(new[]{-1,0,-1})&&g.S.DisplayDecoration==0&&g.DecorationTripsLeft(2)==2,"first earned decoration appears in both displays and advances wish");
        g.EquipDecoration(-1);g.PlaceGardenDecoration(1,-1);g.Home();Win(g,0,1);Check(g.R.DecorationReward==1&&g.S.DisplayDecoration==-1&&g.S.GardenDecorations.All(i=>i<0)&&g.DecorationTripsLeft(2)==1,"deeper clears count and intentional empty displays stay empty");
        g.Home();Win(g);Check(g.R.DecorationReward==2&&g.NextDecoration(0)==-1&&g.DecorationTripsLeft(2)==0&&g.S.DecorationWish==2,"wish completion is retained for player to decorate");
        g.Home();Win(g);Check(g.R.DecorationReward==-1&&g.R.RewardFertilizer==9,"complete collection keeps existing duplicate fertilizer reward");g.Home();
        int berries=g.S.Berries,grain=g.S.Grain,fertilizer=g.S.Fertilizer,crystals=g.S.Crystals;int[] xp=(int[])g.S.Xp.Clone(),team=(int[])g.S.Team.Clone();double period=g.CropSeconds(0);
        Check(g.PlaceGardenDecoration(0,0)&&g.PlaceGardenDecoration(1,1)&&g.PlaceGardenDecoration(2,2),"all three garden slots can be filled");
        Check(g.PlaceGardenDecoration(0,2)&&g.S.GardenDecorations.SequenceEqual(new[]{2,1,0}),"moving a placed item swaps instead of duplicates");
        g.EquipDecoration(1);g.PlaceGardenDecoration(1,-1);Check(g.S.DisplayDecoration==1&&!g.PlaceGardenDecoration(2,5)&&!g.PlaceGardenDecoration(3,0),"garden changes do not alter companion display and invalid placements fail");
        g.EquipDecoration(-1);Check(g.S.GardenDecorations.SequenceEqual(new[]{2,-1,0})&&g.S.Berries==berries&&g.S.Grain==grain&&g.S.Fertilizer==fertilizer&&g.S.Crystals==crystals&&g.S.Xp.SequenceEqual(xp)&&g.S.Team.SequenceEqual(team)&&g.CropSeconds(0)==period,"cosmetics have no combat or production effects");
        g.S.DesktopHidden=true;g.S.DesktopMotion=false;g.Save();var loaded=new FarmGame(g.SavePath);Check(loaded.S.GardenDecorations.SequenceEqual(new[]{2,-1,0})&&loaded.S.DisplayDecoration==-1&&loaded.S.DecorationWish==2&&loaded.S.DesktopHidden&&!loaded.S.DesktopMotion,"independent decorations and wish persist with display settings");
        g.S.BlessingOffers=new[]{0,1,3};g.BeginJourney(0,0);g.ToggleJourneyPause();int node=g.R.Node,round=g.R.Round;long next=g.R.NextJourneyTicks;g.PlaceGardenDecoration(1,1);g.EquipDecoration(0);g.SetDecorationWish(5);Check(g.R.JourneyPaused&&g.R.Node==node&&g.R.Round==round&&g.R.NextJourneyTicks==next,"decorating while away preserves paused trip");g.Retreat();g.Home();Check(g.DecorationTripsLeft(5)==3,"retreat does not advance collection");
        g.EquipDecoration(2);var xml=XDocument.Load(g.SavePath);xml.Root.Element("DecorationLayoutVersion").Remove();xml.Root.Element("GardenDecorations").Remove();xml.Root.Element("DecorationWish").Remove();string legacy=Path.Combine(root,"pre-decorating.xml");xml.Save(legacy);var migrated=new FarmGame(legacy);Check(migrated.SavePath==legacy&&migrated.S.GardenDecorations.SequenceEqual(new[]{-1,-1,2})&&migrated.S.DisplayDecoration==2&&migrated.S.DecorationWish==-1&&migrated.S.Xp.SequenceEqual(g.S.Xp),"old single decoration migrates without lost progression");
        migrated.PlaceGardenDecoration(2,-1);Check(new FarmGame(legacy).S.GardenDecorations.All(i=>i<0),"cleared migrated layout is not restored on next load");
        g.S.GardenDecorations=new[]{0,0,99};g.S.DecorationWish=99;g.Save();Check(g.S.GardenDecorations.SequenceEqual(new[]{0,-1,-1})&&g.S.DecorationWish==-1,"invalid optional cosmetics repaired individually");
        var sparse=New(root);sparse.S.Decorations[2]=true;sparse.Save();Check(sparse.DecorationTripsLeft(2)==0&&sparse.NextDecoration(0)==0,"existing non-sequential ownership stays collected");
        return "PASS: three-slot placement and swaps; independent desktop decoration; nonmutating locked previews; wishes and regional reward order; all-depth clears; duplicate fertilizer; no cosmetic stats; empty display preference; save/reload and old-save migration; paused-trip preservation.";
    }
}

public partial class FarmPanel
{
    private void GardenRender(string root,string name,double width=924,double height=721){Render();UIPreview(root,name,width,height);}
    internal void RenderDecoratingChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;G.S.LastFarmTicks=DateTime.UtcNow.AddDays(1).Ticks;
        Tab="农场";GardenRender(root,"garden-home-entry",764,571);UIVisible(UIAction("布置小院"),"garden entry visible in minimum home");UIAction("布置小院").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-empty");check(Tab=="小院"&&G.S.Decorations.All(x=>!x),"home opens gallery without rewards");
        scroller.ScrollToVerticalOffset(180);scroller.UpdateLayout();UIAction("试摆月光灯").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-locked-small",764,571);UIVisible(UIAction("我想带回这件"),"collection goal action visible in small window");foreach(string spot in FarmGame.GardenSpotNames)UIVisible(UIAction(spot),"all garden positions visible in minimum window");check(scroller.VerticalOffset==0&&!UIAction("准备去洞穴").IsEnabled&&G.S.GardenDecorations.All(i=>i<0)&&scenes[0].DecorationPreview[1]==5,"locked preview scrolls into view but cannot change live garden or enter locked cave");
        UIAction("结束试摆").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-preview-cancelled");check(!decorationTrial&&scenes[0].DecorationPreview==null&&G.S.GardenDecorations.All(i=>i<0),"cancel preview restores actual layout without a save mutation");
        UIAction("我想带回这件").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-wish");check(G.S.DecorationWish==5,"wish action persists selected target");
        UIAction("试摆溪边风车").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-route-before");UIAction("准备去小溪").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-prepare-small",764,571);UIVisible(UIAction("出发 · 自动走完整趟"),"collection route retains visible explicit departure");check(Tab=="冒险"&&G.AtHome&&journeyDifficulty==0&&journeyDepth==0,"collectible shortcut only prepares the correct route");
        G.S.Xp=Enumerable.Repeat(1250,5).ToArray();UIAction("出发 · 自动走完整趟").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));G.AdvanceJourney(DateTime.UtcNow.AddHours(2));GardenRender(root,"garden-reward");int fertilizer=G.S.Fertilizer,crystals=G.S.Crystals;UIAction("去小院预览与摆放").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-new-reward");check(Tab=="小院"&&decorationSelection==0&&G.R.Phase=="result"&&G.S.Fertilizer==fertilizer&&G.S.Crystals==crystals,"reward opens correct preview without claiming again or dismissing result");
        G.Home();G.S.Decorations=Enumerable.Repeat(true,6).ToArray();G.S.GardenDecorations=new[]{0,2,5};G.S.DisplayDecoration=4;G.Save();OpenGarden(3);gardenSpot=1;GardenRender(root,"garden-full");GardenRender(root,"garden-place-small",764,571);UIVisible(UIAction("放到中央小径"),"place action stays visible in minimum garden");UIVisible(UIAction("桌宠带上这件"),"companion placement action stays visible");
        UIAction("放到中央小径").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-placed");check(G.S.GardenDecorations.SequenceEqual(new[]{0,3,5})&&G.S.DisplayDecoration==4&&!decorationTrial,"placement saves the chosen slot and shows actual garden");
        UIAction("桌宠带上这件").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-companion");check(G.S.DisplayDecoration==3&&G.S.GardenDecorations.SequenceEqual(new[]{0,3,5}),"companion uses independent decoration");
        UIAction("收起中央小径").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-cleared");check(G.S.GardenDecorations[1]==-1&&G.S.DisplayDecoration==3&&scenes[0].DecorationPreview==null,"clear updates real preview without affecting companion");
        UIAction("待发现 0").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-collected-all");check(UIChildren<TextBlock>((DependencyObject)Content).Any(t=>t.Text=="六件回忆，都带回来了。"),"empty unowned filter explains collection complete");
        G.S.BlessingOffers=new[]{0,1,3};G.BeginJourney(0,0);G.ToggleJourneyPause();OpenGarden(4);GardenRender(root,"garden-away");int node=G.R.Node,round=G.R.Round;UIAction("放到中央小径").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));GardenRender(root,"garden-away-placed");check(G.R.JourneyPaused&&G.R.Node==node&&G.R.Round==round&&G.S.GardenDecorations[1]==4,"cosmetic UI preserves paused trip");
        G.Retreat();G.Home();decorationFilter=0;OpenGarden();GardenRender(root,"garden-final");Tab="农场";GardenRender(root,"garden-home-decorated");
        check(!IsVisible&&!owner.IsVisible,"decorating checks never show windows");
        File.WriteAllText(Path.Combine(root,"decorating-ui-regression.txt"),"PASS: home entry, locked preview and route gate; persistent wish; explicit departure; receipt preview without duplicate rewards; minimum-window placement; independent companion display; cleared preview; collection filters; paused-trip preservation. Offscreen controls and isolated save only.");
    }
}

public partial class FarmWindow
{
    internal void BackgroundDecorationDisplayChecks(string root)
    {
        G.S.Decorations=Enumerable.Repeat(true,6).ToArray();G.S.GardenDecorations=new[]{0,3,5};G.S.DesktopMode="pet";G.S.DesktopMotion=false;G.S.DesktopHidden=true;
        foreach(int size in new[]{96,128,176}){G.S.PetSize=size;BuildDesktop();double width=Width,height=Height;G.EquipDecoration(4);Refresh();FarmHeadlessTests.Render(this,Path.Combine(root,"garden-companion-"+size+".png"),Width,Height);FarmHeadlessTests.Check(desktopDecoration.Decoration==4&&Width==width&&Height==height&&G.S.DesktopHidden&&!IsVisible&&!animation.IsEnabled,"decoration keeps compact size, hidden state and paused animation");}
        G.S.DesktopMode="farm";BuildDesktop();RefreshCounters();FarmHeadlessTests.Render(this,Path.Combine(root,"garden-desktop-farm.png"),Width,Height);
        FarmHeadlessTests.Check(Height==284&&G.S.GardenDecorations.SequenceEqual(new[]{0,3,5})&&desktopHandle==IntPtr.Zero,"three decorations retain farm widget size and no native resources");
    }
}
