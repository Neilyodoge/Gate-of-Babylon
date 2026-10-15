using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public partial class FarmPanel
{
    private int decorationSelection=-1,gardenSpot=1,decorationFilter;
    private bool decorationTrial;
    private void OpenGarden(int decoration=-1)
    {
        decorationTrial=decoration>=0&&decoration<6;if(decorationTrial)decorationSelection=decoration;
        Go("小院");resetScroll=true;
    }
    private void PrepareDecorationJourney(int decoration)
    {
        if(!G.AtHome||decoration<0||decoration>=6||!G.ExpeditionUnlocked(decoration/3,0))return;
        journeyDifficulty=decoration/3;journeyDepth=0;Go("冒险");resetScroll=true;
    }
    private UIElement DecorationGoalCard()
    {
        var p=new StackPanel();int wish=G.S.DecorationWish;
        p.Children.Add(U.Text(wish>=0?"想带回的小东西":"冒险带回的小装饰",17));
        p.Children.Add(U.Text("已收藏 "+G.S.Decorations.Count(x=>x)+" / 6 · 小院可摆 3 件",12,U.Muted));
        if(wish>=0){p.Children.Add(U.Text(FarmGame.DecorationNames[wish]+" · "+G.DecorationSource(wish),13,U.Green));
            p.Children.Add(Btn(G.DecorationOwned(wish)?"目标已带回 · 去布置":"查看目标与获取路线",()=>OpenGarden(wish),true,true));}
        else{p.Children.Add(U.Text("选一件想要的装饰，看看下趟能带回什么。",12,U.Muted));p.Children.Add(Btn("打开装饰收藏与布置",()=>OpenGarden()));}
        return U.Card(p,12);
    }
    private UIElement JourneyDecorationCard(int region)
    {
        int id=G.NextDecoration(region);var p=new StackPanel();p.Children.Add(U.Text("这趟的装饰收获",16));
        if(id<0)p.Children.Add(U.Text("本地区三件已集齐。再次通关额外获得肥料 2。",12,U.Green));
        else{var row=new DockPanel();var icon=new DecorationView{Decoration=id,Width=46,Height=46,Margin=new Thickness(0,0,8,0)};DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(U.Text("通关带回\n"+FarmGame.DecorationNames[id],13,U.Green));p.Children.Add(row);}
        int wish=G.S.DecorationWish;
        if(wish>=0&&!G.DecorationOwned(wish))p.Children.Add(U.Text("收藏目标："+FarmGame.DecorationNames[wish]+"\n"+G.DecorationSource(wish)+(wish/3!=region?" · 在另一地区":""),11,U.Muted));
        p.Children.Add(Btn("看看装饰与布置",()=>OpenGarden(id)));return U.Card(p,12);
    }
    private void GardenStudio()
    {
        if(decorationSelection<0||decorationSelection>=6)decorationSelection=G.S.DecorationWish>=0?G.S.DecorationWish:G.S.DisplayDecoration>=0?G.S.DisplayDecoration:0;
        int selected=decorationSelection;bool owned=G.DecorationOwned(selected);var cols=Columns(236);var left=cols[0];var aside=cols[1];
        Heading(left,"把旅途，带回小院。","选位置，再选装饰。想要的小东西，也能先试摆看看。");
        var preview=new StackPanel();var previewHead=new DockPanel();var end=Btn("结束试摆",()=>decorationTrial=false,decorationTrial);end.Padding=new Thickness(7,2,7,2);end.Margin=new Thickness(4,0,0,3);DockPanel.SetDock(end,Dock.Right);previewHead.Children.Add(end);previewHead.Children.Add(FarmTheme.Badge(decorationTrial?"试摆预览 · "+FarmGame.DecorationNames[selected]:"当前布置 · 已保存",owned||!decorationTrial?U.Pale:FarmTheme.Honey));preview.Children.Add(previewHead);
        var scene=Scene();scene.Height=scene.MinHeight=190;scene.DecorationPreview=decorationTrial?G.PreviewGardenDecoration(gardenSpot,selected):null;scene.PreviewSpot=decorationTrial?gardenSpot:-1;scene.Margin=new Thickness(0,0,0,6);preview.Children.Add(scene);
        preview.Children.Add(U.Text(!owned&&decorationTrial?"这件还未收藏，可以先试摆；带回后才能保存到小院。":"预览不会改动布置，点击右侧「放到…」后保存。",11,U.Muted));left.Children.Add(U.Card(preview,10));
        var spots=new UniformGrid{Columns=3};left.Children.Add(spots);
        for(int i=0;i<3;i++){int slot=i;var p=new StackPanel();var b=Btn(FarmGame.GardenSpotNames[i],()=>{gardenSpot=slot;decorationTrial=true;},true,gardenSpot==i);b.Padding=new Thickness(4,6,4,6);p.Children.Add(b);int placed=G.S.GardenDecorations[i];var label=U.Text(placed>=0?FarmGame.DecorationNames[placed]:"还空着",11,U.Muted);label.TextAlignment=TextAlignment.Center;p.Children.Add(label);p.Margin=new Thickness(0,0,6,8);spots.Children.Add(p);}
        left.Children.Add(SectionTitle("旅行收藏","每地区 3 件 · 任意深度可获得"));
        var filters=new UniformGrid{Columns=3};left.Children.Add(filters);string[] names={"全部 6","已收藏 "+G.S.Decorations.Count(x=>x),"待发现 "+G.S.Decorations.Count(x=>!x)};
        for(int i=0;i<3;i++){int filter=i;var b=Btn(names[i],()=>decorationFilter=filter,true,decorationFilter==i);b.Margin=new Thickness(0,0,6,7);filters.Children.Add(b);}
        var shelf=new UniformGrid{Columns=3};left.Children.Add(shelf);int shown=0;
        for(int i=0;i<6;i++){
            int id=i;bool have=G.DecorationOwned(id);if(decorationFilter==1&&!have||decorationFilter==2&&have)continue;shown++;
            var p=new StackPanel();p.Children.Add(new DecorationView{Decoration=id,Width=62,Height=62,HorizontalAlignment=HorizontalAlignment.Center,Opacity=have?1:.55});
            var name=U.Text(FarmGame.DecorationNames[id],13);name.TextAlignment=TextAlignment.Center;p.Children.Add(name);p.Children.Add(U.Text(have?"✓ 已收藏":G.DecorationSource(id),11,have?U.Green:U.Muted));
            if(G.S.DecorationWish==id)p.Children.Add(U.Text(have?"★ 目标已带回":"★ 收藏目标",11,U.Gold));
            var choose=Btn(selected==id&&decorationTrial?"正在试摆":"试摆看看",()=>{decorationSelection=id;decorationTrial=true;resetScroll=true;},true,selected==id);System.Windows.Automation.AutomationProperties.SetName(choose,"试摆"+FarmGame.DecorationNames[id]);p.Children.Add(choose);
            var card=U.Card(p,8);card.Margin=new Thickness(0,0,6,8);if(selected==id)card.Background=U.Pale;shelf.Children.Add(card);
        }
        if(shown==0)left.Children.Add(Notice(decorationFilter==1?"第一件装饰，在小溪等你。":"六件回忆，都带回来了。",decorationFilter==1?"小溪通关后会带回溪边风车；失败和提前返家不会消耗收藏进度。":"可以随时换个布置，再去冒险积攒肥料。"));
        var details=new StackPanel();details.Children.Add(U.Text(FarmGame.DecorationNames[selected],19));details.Children.Add(U.Text(G.DecorationSource(selected),12,U.Green));details.Children.Add(U.Text(FarmGame.DecorationStories[selected],12,U.Muted));
        if(owned){
            var place=Btn("放到"+FarmGame.GardenSpotNames[gardenSpot],()=>{G.PlaceGardenDecoration(gardenSpot,selected);decorationTrial=false;},G.S.GardenDecorations[gardenSpot]!=selected,true);FarmTheme.Accent(place);details.Children.Add(place);
            if(G.S.GardenDecorations[gardenSpot]==selected)details.Children.Add(U.Text("✓ 这个位置已保存",11,U.Green));
            details.Children.Add(Btn("桌宠带上这件",()=>G.EquipDecoration(selected),G.S.DisplayDecoration!=selected));
        }else{
            var wish=Btn(G.S.DecorationWish==selected?"★ 已设为收藏目标":"我想带回这件",()=>G.SetDecorationWish(selected),G.S.DecorationWish!=selected,true);FarmTheme.Accent(wish);details.Children.Add(wish);
            details.Children.Add(Btn("准备去"+(selected<3?"小溪":"洞穴"),()=>PrepareDecorationJourney(selected),G.AtHome&&G.ExpeditionUnlocked(selected/3,0)));
            details.Children.Add(U.Text(!G.ExpeditionUnlocked(selected/3,0)?"泡泡或种子首次进化后开放洞穴。":!G.AtHome?"这趟结束并返家后，再安排下一趟。":"会先进入初探准备页，确认队伍和祝福后出发。",11,U.Muted));
        }
        details.Children.Add(Btn("收起"+FarmGame.GardenSpotNames[gardenSpot],()=>{G.PlaceGardenDecoration(gardenSpot,-1);decorationTrial=false;},G.S.GardenDecorations[gardenSpot]>=0));
        details.Children.Add(U.Text("装饰只改变外观，不增加战力。",11,U.Muted));aside.Children.Add(U.Card(details,12));
        var pet=new StackPanel();pet.Children.Add(U.Text("桌宠的随身摆件",16));
        var petPreview=new Grid{Height=90,Background=U.Pale};var art=Portrait(G.S.DisplayPet,86);art.HorizontalAlignment=HorizontalAlignment.Center;petPreview.Children.Add(art);petPreview.Children.Add(new DecorationView{Decoration=G.S.DisplayDecoration,Width=42,Height=42,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,8,0)});pet.Children.Add(petPreview);
        pet.Children.Add(U.Text(G.S.DisplayDecoration>=0?"当前："+FarmGame.DecorationNames[G.S.DisplayDecoration]:"当前：只展示宠物",11,U.Muted));pet.Children.Add(Btn("收起桌宠摆件",()=>G.EquipDecoration(-1),G.S.DisplayDecoration>=0));aside.Children.Add(U.Card(pet,12));
        if(G.S.DecorationWish>=0){aside.Children.Add(DecorationGoalCard());aside.Children.Add(Btn("收起收藏目标",()=>G.SetDecorationWish(-1)));}
        aside.Children.Add(Btn("回农场照料伙伴",()=>Go("农场")));
    }
}
