using System;
using System.Windows;
using System.Windows.Controls;

// View-local input ownership. No save, combat or random-state changes.
internal sealed class MossCardInspect
{
    private int? pointer,focus;
    private long order,pointerOrder,focusOrder;
    internal int Slot{get{return !pointer.HasValue?(focus??-1):!focus.HasValue?pointer.Value:pointerOrder>focusOrder?pointer.Value:focus.Value;}}
    internal void Pointer(int slot,bool active){if(active){pointer=slot;pointerOrder=++order;}else if(pointer==slot)pointer=null;}
    internal void Focus(int slot,bool active){if(active){focus=slot;focusOrder=++order;}else if(focus==slot)focus=null;}
}
public partial class FarmPanel
{
    private MossCardInspect cardInspect=new MossCardInspect(),rewardInspect=new MossCardInspect();
    private void ResetCardInspection(){cardInspect=new MossCardInspect();rewardInspect=new MossCardInspect();}
    private void AttachCardInspection(FrameworkElement tile,int slot,MossCardInspect inspection,Action<int> show)
    {
        int view=cardViewRevision;var state=G.S;
        Action<bool,bool> update=(pointer,active)=>{
            if(view!=cardViewRevision||Tab!="卡牌冒险"||!Object.ReferenceEquals(state,G.S))return;
            if(pointer)inspection.Pointer(slot,active);else inspection.Focus(slot,active);
            show(inspection.Slot);
        };
        tile.MouseEnter+=(s,e)=>update(true,true);tile.MouseLeave+=(s,e)=>update(true,false);
        tile.GotKeyboardFocus+=(s,e)=>update(false,true);tile.LostKeyboardFocus+=(s,e)=>update(false,false);
    }
}
