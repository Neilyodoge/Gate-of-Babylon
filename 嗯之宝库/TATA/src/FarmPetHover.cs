using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// A layered transparent window loses native mouse hit testing on zero-alpha gaps.
// Keep a continuous, nearly transparent surface only while its toolbar is in use.
internal sealed class FarmPetHover : IDisposable
{
    private readonly Grid root;
    private readonly Border toolbar;
    private readonly Func<bool> current;
    private readonly bool testing;
    private readonly DispatcherTimer close;
    private bool popup,disposed;
    internal FarmPetHover(Grid root,Border toolbar,Func<bool> current,bool testing)
    {
        this.root=root;this.toolbar=toolbar;this.current=current;this.testing=testing;
        close=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};
        close.Tick+=(s,e)=>Expire(root.IsMouseOver,root.IsKeyboardFocusWithin);
        root.MouseEnter+=(s,e)=>Enter();root.MouseLeave+=(s,e)=>Leave();
        root.GotKeyboardFocus+=(s,e)=>Enter();root.LostKeyboardFocus+=(s,e)=>Leave();
        root.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape&&!popup){Keyboard.ClearFocus();Hide();e.Handled=true;}};
    }
    internal bool Pending { get; private set; }
    internal bool IsOpen { get{return !disposed&&toolbar.Opacity==1;} }
    internal void Enter()
    {
        if(disposed||!current())return;close.Stop();Pending=false;
        root.Background=new SolidColorBrush(Color.FromArgb(1,245,239,223));
        toolbar.Opacity=1;toolbar.IsHitTestVisible=true;
    }
    internal void Leave()
    {
        if(disposed||!current())return;Pending=true;if(!testing){close.Stop();close.Start();}
    }
    internal void SetPopup(bool value){if(disposed||!current())return;popup=value;if(value)Enter();else Leave();}
    internal void Expire(bool pointerInside,bool focusInside)
    {
        close.Stop();if(disposed||!current())return;
        if(pointerInside||focusInside||popup){Pending=false;return;}Hide();
    }
    private void Hide(){close.Stop();Pending=false;toolbar.Opacity=0;toolbar.IsHitTestVisible=false;root.Background=Brushes.Transparent;}
    public void Dispose(){if(disposed)return;Hide();disposed=true;}
}

public partial class FarmWindow
{
    internal static void PrepareDesktopStart(FarmGame game){game.S.DesktopMode="pet";}
    private void OpenCompactPanel()
    {
        if(!testing){OpenPanel();return;}
        if(Panel==null)Panel=new FarmPanel(this);
        Panel.Tab="卡牌冒险";
        Panel.Render();ApplyPanelPresence(true);
    }
}
