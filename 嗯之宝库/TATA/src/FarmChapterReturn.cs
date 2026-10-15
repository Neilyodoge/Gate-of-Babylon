using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

public partial class FarmPanel
{
    private FarmSave chapterReturnState;
    private int chapterReturnIndex=-1,chapterReturnRevision;
    private bool HasChapterReturn{get{return Object.ReferenceEquals(chapterReturnState,G.S)&&SimpleCourtyard&&G.AtHome&&chapterReturnIndex>=0&&chapterReturnIndex<8&&Tab!="成长手册";}}
    private void ClearChapterReturn()
    {
        if(chapterReturnState==null)return;
        chapterReturnState=null;chapterReturnIndex=-1;chapterReturnRevision++;
    }
    private void BeginChapterReturn(int chapter)
    {
        if(!SimpleCourtyard||!G.AtHome||chapter<0||chapter>=8)return;
        chapterReturnState=G.S;chapterReturnIndex=chapter;chapterReturnRevision++;
    }
    private void CheckChapterReturnScope(string target)
    {
        if(!Object.ReferenceEquals(chapterReturnState,G.S)||!SimpleCourtyard||!G.AtHome||!new[]{"伙伴册","伙伴","施肥","进化","莓果田","农田","材料","点心","照料","技能","行囊","目的地","冒险","便当","便当伙伴","同行","准备小卡"}.Contains(target))ClearChapterReturn();
    }
    private void ReturnToChapter()
    {
        if(!HasChapterReturn)return;
        int chapter=chapterReturnIndex,origin=pageHistory.LastIndexOf("成长手册");
        ClearChapterReturn();ClearCrystalPreparation();lifeChapter=chapter;lifeReceipt="";lifeRevision++;
        backNavigating=true;try{Go("成长手册");}finally{backNavigating=false;}
        if(origin>=0)pageHistory.RemoveRange(origin,pageHistory.Count-origin);
        resetScroll=true;
    }
    private void AddChapterReturn(Panel navigation)
    {
        if(!HasChapterReturn)return;
        var state=G.S;int revision=chapterReturnRevision,visit=courtyardVisit;string page=Tab;
        var back=Btn("回手册",()=>{if(HasChapterReturn&&Object.ReferenceEquals(state,G.S)&&chapterReturnRevision==revision&&courtyardVisit==visit&&Tab==page)ReturnToChapter();});
        back.FontSize=11;back.Padding=new Thickness(7,5,7,5);back.Margin=new Thickness(4,0,0,0);
        back.ToolTip="回刚才那页看进度。已经确认的操作保留，不会自动领取礼物。";
        AutomationProperties.SetAutomationId(back,"chapter-return");AutomationProperties.SetName(back,"返回成长手册："+FarmGame.ChapterNames[chapterReturnIndex]);AutomationProperties.SetHelpText(back,Convert.ToString(back.ToolTip));navigation.Children.Add(back);
    }
}
