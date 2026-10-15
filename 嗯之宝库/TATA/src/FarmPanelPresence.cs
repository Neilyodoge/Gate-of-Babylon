using System;
using System.Windows;

public partial class FarmWindow
{
    private bool desktopSuppressedForPanel,foldingPanel;
    internal static bool DesktopNeedsCompanion(bool hidden,bool panelVisible,bool minimized)
    {
        return !hidden&&(!panelVisible||minimized);
    }
    internal void SyncPanelPresence()
    {
        if(foldingPanel||Exiting)return;
        ApplyPanelPresence(Panel!=null&&Panel.IsVisible,Panel!=null&&Panel.WindowState==WindowState.Minimized);
    }
    private void ApplyPanelPresence(bool panelVisible,bool minimized=false)
    {
        bool active=panelVisible&&!minimized;
        if(active){
            if(IsVisible)RememberPosition();desktopSuppressedForPanel=true;
            if(!testing&&IsVisible)Hide();
        }else if(desktopSuppressedForPanel){
            desktopSuppressedForPanel=false;
            if(!testing&&DesktopNeedsCompanion(G.S.DesktopHidden,panelVisible,minimized)&&!IsVisible){WindowState=WindowState.Normal;Show();}
        }
        UpdateAnimationState();
    }
}
