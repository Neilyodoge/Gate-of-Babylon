using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

public partial class FarmWindow
{
    private Image petImage;
    private DecorationView desktopDecoration;
    private Border petTools;
    private FarmPetHover petHover;
    private Button petWelcome;
    private FrameworkElement moveHandle;
    private Point? petDragOrigin;
    private bool desktopReady,exitRequested,compactKey,hideKey;
    private string compactShortcut="Ctrl + Alt + P";
    private string hideShortcut="Ctrl + Alt + H";
    private Rect[] testWorkAreas;
    private Forms.NotifyIcon tray;
    private Drawing.Icon trayIcon;
    private HwndSource desktopSource;
    private IntPtr desktopHandle;
    private const int CompactHotkey=0x541,HideHotkey=0x542;
    [DllImport("user32.dll")]private static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr h);
    public bool IsCompact{get{return G.S.DesktopMode=="pet";}}
    public string DesktopShortcuts{get{if(!G.S.DesktopHotkeys)return "全局快捷键已关闭（右键可开启）。";return (compactKey?compactShortcut+"：农场 / 单宠物":"缩放快捷键未启用，请用按钮或右键菜单")+"\n"+(hideKey?hideShortcut+"：隐藏 / 恢复":"隐藏快捷键未启用，请用按钮或托盘")+"。";}}

    private void InitializeDesktop()
    {
        Title=G.GuideTest?FarmProgram.GuideWindowTitle:"苔间 · 桌面农场";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;Topmost=G.S.DesktopTopmost;ShowInTaskbar=true;ShowActivated=false;
        FontFamily=new FontFamily("Microsoft YaHei");UseLayoutRounding=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
        BuildDesktop();
        SourceInitialized+=(s,e)=>{
            desktopHandle=new WindowInteropHelper(this).Handle;desktopSource=HwndSource.FromHwnd(desktopHandle);desktopSource.AddHook(DesktopMessage);
            if(!testing){RegisterDesktopHotkeys();CreateTray();}
            BuildDesktopMenu();RefreshCounters();
        };
        animation=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(67)};
        animation.Tick+=(s,e)=>{
            Tick++;if(IsVisible&&G.S.DesktopMotion){if(scene!=null){scene.Tick=Tick;scene.InvalidateVisual();}if(petImage!=null)petImage.Source=Art.Frame(G.S.DisplayPet,Tick,G.S.Evolved,G.S.SeedStage);}
            if(IsVisible&&G.S.DesktopMotion&&desktopDecoration!=null){desktopDecoration.Tick=Tick;desktopDecoration.InvalidateVisual();}if(Panel!=null)Panel.Animate(Tick);if(testing&&Tick==20)VisualTest();
        };
        clock=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        clock.Tick+=(s,e)=>TickClock(DateTime.UtcNow,IsVisible,Panel!=null&&Panel.IsVisible,Panel!=null&&Panel.WindowState==WindowState.Minimized);
        Loaded+=(s,e)=>{
            if(desktopReady)return;
            PlaceDesktop();desktopReady=true;RememberPosition();clock.Start();RefreshCounters();
            if(G.S.DesktopHidden&&!testing)HideToTray();else UpdateAnimationState();
        };
        StateChanged+=(s,e)=>{if(desktopReady&&WindowState==WindowState.Minimized){WindowState=WindowState.Normal;HideToTray();}};
        Closing+=(s,e)=>{
            if(!exitRequested&&!testing){e.Cancel=true;HideToTray();return;}
            try{G.TickFarm(DateTime.UtcNow);RememberPosition();G.Save();Exiting=true;}
            catch(Exception ex){exitRequested=false;e.Cancel=true;MessageBox.Show("进度保存失败："+ex.Message,"苔间");}
        };
        Closed+=(s,e)=>{
            if(petHover!=null)petHover.Dispose();
            animation.Stop();clock.Stop();UnregisterDesktopHotkeys();
            if(tray!=null){tray.Visible=false;tray.Dispose();}if(trayIcon!=null)trayIcon.Dispose();
            if(desktopSource!=null)desktopSource.RemoveHook(DesktopMessage);if(Panel!=null)Panel.Close();
        };
    }
    internal void TickClock(DateTime now,bool desktopVisible,bool panelVisible,bool panelMinimized)
    {
        int harvested=G.TickFarm(now);G.AdvanceJourney(now);seconds++;if(seconds%10==0||harvested>0)G.Save();
        if(desktopVisible)RefreshCounters();else RefreshPresence();
        if(Panel!=null)Panel.Pulse(panelVisible,panelMinimized);
    }
    private IntPtr DesktopMessage(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x8002&&G.GuideTest){RestartGuideTest();ShowCompact();handled=true;}
        if(message==0x8001){RestoreDesktop();handled=true;}
        if(message==0x0312&&G.S.DesktopHotkeys){if(w.ToInt32()==CompactHotkey){ToggleDesktop();handled=true;}else if(w.ToInt32()==HideHotkey){ToggleHidden();handled=true;}}
        return IntPtr.Zero;
    }
    private void RegisterDesktopHotkeys()
    {
        UnregisterDesktopHotkeys();if(testing||desktopHandle==IntPtr.Zero||!G.S.DesktopHotkeys)return;
        compactShortcut="Ctrl + Alt + P";hideShortcut="Ctrl + Alt + H";
        compactKey=RegisterHotKey(desktopHandle,CompactHotkey,0x4003,0x50);
        if(!compactKey){compactKey=RegisterHotKey(desktopHandle,CompactHotkey,0x4007,0x50);compactShortcut="Ctrl + Alt + Shift + P";}
        hideKey=RegisterHotKey(desktopHandle,HideHotkey,0x4003,0x48);
        if(!hideKey){hideKey=RegisterHotKey(desktopHandle,HideHotkey,0x4007,0x48);hideShortcut="Ctrl + Alt + Shift + H";}
    }
    private void UnregisterDesktopHotkeys()
    {
        if(compactKey)UnregisterHotKey(desktopHandle,CompactHotkey);if(hideKey)UnregisterHotKey(desktopHandle,HideHotkey);compactKey=hideKey=false;
    }
    public void UpdateAnimationState()
    {
        if(animation==null)return;
        bool panelVisible=Panel!=null&&Panel.IsVisible&&Panel.WindowState!=WindowState.Minimized;
        if(DesktopLayout.ShouldAnimate(IsVisible,G.S.DesktopMotion,panelVisible))animation.Start();else animation.Stop();
    }
    private void ApplyDesktopPreferences()
    {
        Topmost=G.S.DesktopTopmost;if(Panel!=null)Panel.Topmost=G.S.DesktopTopmost;
        if(moveHandle!=null)moveHandle.Cursor=G.S.DesktopLocked?Cursors.Arrow:(IsCompact?Cursors.Hand:Cursors.SizeAll);
        UpdateAnimationState();RefreshCounters();G.Save();
    }
    public void SetDesktopPreference(string preference,bool enabled)
    {
        if(preference=="topmost")G.S.DesktopTopmost=enabled;
        else if(preference=="motion")G.S.DesktopMotion=enabled;
        else if(preference=="locked")G.S.DesktopLocked=enabled;
        else if(preference=="hints")G.S.DesktopStatusHints=enabled;
        else if(preference=="hotkeys"){G.S.DesktopHotkeys=enabled;RegisterDesktopHotkeys();BuildDesktopMenu();}
        ApplyDesktopPreferences();
    }
    private void CreateTray()
    {
        try{
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Art.Frame(0,0,false)));
            using(var stream=new MemoryStream()){
                encoder.Save(stream);stream.Position=0;
                using(var bitmap=new Drawing.Bitmap(stream))using(var small=new Drawing.Bitmap(bitmap,32,32)){
                    IntPtr handle=small.GetHicon();try{using(var temporary=Drawing.Icon.FromHandle(handle))trayIcon=(Drawing.Icon)temporary.Clone();}finally{DestroyIcon(handle);}
                }
            }
            tray=new Forms.NotifyIcon{Icon=trayIcon,Text="苔间 "+FarmProgram.Version+" · 点击恢复桌宠 / 右键更多",Visible=true};
            var menu=new Forms.ContextMenuStrip();
            AddTray(menu,"显示桌宠",RestoreDesktop);AddTray(menu,"只显示一只宠物",ShowCompact);AddTray(menu,"展开完整农场",ShowFarm);AddTray(menu,"打开卡牌冒险",()=>OpenPanel());
            var pets=new Forms.ToolStripMenuItem("展示哪位伙伴");menu.Items.Add(pets);
            for(int i=0;i<FarmGame.PetCount;i++){int pet=i;var item=new Forms.ToolStripMenuItem(G.Name(i));item.Click+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>SelectDisplayPet(pet)));pets.DropDownItems.Add(item);}
            menu.Opening+=(s,e)=>{for(int i=0;i<FarmGame.PetCount;i++){var item=(Forms.ToolStripMenuItem)pets.DropDownItems[i];item.Text=G.Name(i);item.Checked=G.S.DisplayPet==i;}};
            menu.Items.Add(new Forms.ToolStripSeparator());
            AddTrayPreference(menu,"保持置顶","topmost",()=>G.S.DesktopTopmost);
            AddTrayPreference(menu,"播放桌宠动作","motion",()=>G.S.DesktopMotion);
            AddTrayPreference(menu,"锁定位置与滚轮大小","locked",()=>G.S.DesktopLocked);
            AddTrayPreference(menu,"启用全局快捷键","hotkeys",()=>G.S.DesktopHotkeys);
            AddTrayPreference(menu,"显示桌宠状态角标","hints",()=>G.S.DesktopStatusHints);
            menu.Items.Add(new Forms.ToolStripSeparator());AddTray(menu,"隐藏桌宠",HideToTray);AddTray(menu,G.GuideTest?"退出测试（不保存）":"退出并保存",ExitAndSave);tray.ContextMenuStrip=menu;
            tray.MouseClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.BeginInvoke(new Action(RestoreDesktop));};
        }catch(Exception ex){
            if(tray!=null){tray.Dispose();tray=null;}if(trayIcon!=null){trayIcon.Dispose();trayIcon=null;}
            File.WriteAllText(Path.Combine(G.GuideTest?Path.GetTempPath():Path.GetDirectoryName(G.SavePath),"tray-error.txt"),ex.ToString());ShowInTaskbar=true;
        }
    }
    private void AddTray(Forms.ContextMenuStrip menu,string label,Action action){menu.Items.Add(label,null,(s,e)=>Dispatcher.BeginInvoke(action));}
    private void AddTrayPreference(Forms.ContextMenuStrip menu,string label,string preference,Func<bool> get)
    {
        var item=new Forms.ToolStripMenuItem(label);menu.Items.Add(item);
        menu.Opening+=(s,e)=>item.Checked=get();item.Click+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>SetDesktopPreference(preference,!get())));
    }
    private MenuItem Menu(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(s,e)=>action();return item;}
    public void SelectDisplayPet(int pet)
    {
        if(pet<0||pet>=FarmGame.PetCount)return;
        G.S.DisplayPet=pet;G.S.Notice="桌面展示已切换为"+G.Name(pet)+"。出战队伍与农田分工保持不变。";
        if(petImage!=null){petImage.Source=Art.Frame(pet,Tick,G.S.Evolved,G.S.SeedStage);System.Windows.Automation.AutomationProperties.SetName(petImage,G.Name(pet)+"，双击打开玩法，中键切换下一只宠物");}
        RefreshCounters();if(Panel!=null&&Panel.IsVisible)Panel.Render();G.Save();
    }
    private void NextDisplayPet(){SelectDisplayPet((G.S.DisplayPet+1)%FarmGame.PetCount);}
    private StackPanel DisplayPetRow(int pet)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        row.Children.Add(new Image{Source=Art.Frame(pet,Tick,G.S.Evolved,G.S.SeedStage),Width=40,Height=40,Margin=new Thickness(0,0,8,0)});
        var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};row.Children.Add(labels);
        var title=U.Text(G.Name(pet)+(G.S.DisplayPet==pet?"  ✓ 当前展示":""),13);title.Margin=new Thickness(0);labels.Children.Add(title);
        var detail=U.Text(FarmGame.ElementNames[FarmGame.Elements[pet]]+"系 · "+FarmGame.DutyNames[FarmGame.Duties[pet]]+(pet==4?" · 第 "+(G.S.SeedStage+1)+" 阶":""),11,U.Muted);detail.Margin=new Thickness(0);labels.Children.Add(detail);return row;
    }
    private ContextMenu DisplayPetPicker()
    {
        var menu=new ContextMenu{Background=U.Cream,Foreground=U.Ink,FontFamily=FontFamily,Padding=new Thickness(4)};
        menu.Items.Add(new MenuItem{Header="选择桌面伙伴 · 不影响出战",IsEnabled=false});
        for(int i=0;i<FarmGame.PetCount;i++){int pet=i;var item=Menu("",()=>SelectDisplayPet(pet));item.Header=DisplayPetRow(pet);menu.Items.Add(item);}
        menu.Items.Add(new Separator());menu.Items.Add(Menu("下一只（也可按鼠标中键）",NextDisplayPet));return menu;
    }
    private MenuItem PreferenceMenu(string label,string preference,Func<bool> get)
    {
        var item=new MenuItem{Header=label,IsCheckable=true,IsChecked=get()};item.Click+=(s,e)=>SetDesktopPreference(preference,item.IsChecked);return item;
    }
    private void BuildDesktopMenu()
    {
        var menu=new ContextMenu();menu.Items.Add(new MenuItem{Header="苔间 · "+FarmProgram.Version,IsEnabled=false});menu.Items.Add(Menu("打开卡牌冒险（也可双击宠物）",()=>OpenPanel()));
        menu.Items.Add(Menu("只显示一只宠物"+(compactKey?"    "+compactShortcut:""),ShowCompact));
        menu.Items.Add(Menu("展开完整农场",ShowFarm));
        var sizes=new MenuItem{Header="宠物大小（也可滚轮调整）"};
        foreach(int value in new[]{96,128,176}){int size=value;var item=Menu(size==96?"小":size==128?"中 · 默认":"大",()=>SetPetSize(size));item.IsCheckable=true;item.IsChecked=G.S.PetSize==size;sizes.Items.Add(item);}menu.Items.Add(sizes);
        var pets=new MenuItem{Header="展示哪位伙伴"};for(int i=0;i<FarmGame.PetCount;i++){int pet=i;var item=Menu(G.Name(pet),()=>SelectDisplayPet(pet));item.IsCheckable=true;item.IsChecked=G.S.DisplayPet==pet;pets.Items.Add(item);}menu.Items.Add(pets);
        menu.Items.Add(Menu("下一只桌宠（鼠标中键）",NextDisplayPet));
        var work=new MenuItem{Header="工作时的显示设置"};
        var top=PreferenceMenu("保持置顶","topmost",()=>G.S.DesktopTopmost);work.Items.Add(top);
        var motion=PreferenceMenu("播放桌宠动作","motion",()=>G.S.DesktopMotion);work.Items.Add(motion);
        var locked=PreferenceMenu("锁定位置与滚轮大小","locked",()=>G.S.DesktopLocked);work.Items.Add(locked);
        var keys=PreferenceMenu("启用全局快捷键","hotkeys",()=>G.S.DesktopHotkeys);work.Items.Add(keys);var hints=PreferenceMenu("显示桌宠状态角标","hints",()=>G.S.DesktopStatusHints);work.Items.Add(hints);menu.Items.Add(work);
        menu.Items.Add(new Separator());menu.Items.Add(Menu("隐藏到托盘"+(hideKey?"    "+hideShortcut:""),HideToTray));
        menu.Items.Add(Menu("操作说明",()=>OpenPanel("怎么玩")));menu.Items.Add(Menu(G.GuideTest?"退出测试（不保存）":"退出并保存",ExitAndSave));ContextMenu=menu;
        menu.Opened+=(s,e)=>{for(int i=0;i<FarmGame.PetCount;i++){var item=(MenuItem)pets.Items[i];item.Header=G.Name(i);item.IsChecked=G.S.DisplayPet==i;}top.IsChecked=G.S.DesktopTopmost;motion.IsChecked=G.S.DesktopMotion;locked.IsChecked=G.S.DesktopLocked;keys.IsChecked=G.S.DesktopHotkeys;hints.IsChecked=G.S.DesktopStatusHints;};
    }
    private void BuildDesktop()
    {
        // Remove the old scene before resizing; Windows can arrange synchronously on Width changes.
        if(petHover!=null){petHover.Dispose();petHover=null;}Content=null;petWelcome=null;
        scene=null;resources=null;stageText=null;adventureButton=null;petImage=null;petTools=null;moveHandle=null;desktopDecoration=null;compactStatus=null;compactBadge=null;
        if(IsCompact)BuildPet();else BuildFarm();BuildDesktopMenu();
    }
    private void BuildFarm()
    {
        Width=Math.Min(800,Math.Max(420,DesktopWorkArea.Width-20));Height=284;
        var root=new Border{Background=U.Cream,CornerRadius=new CornerRadius(18),BorderBrush=U.Line,BorderThickness=new Thickness(1),Padding=new Thickness(12)};Content=root;var stack=new StackPanel();root.Child=stack;
        var head=new DockPanel{Margin=new Thickness(0,0,0,6)};stack.Children.Add(head);
        var hide=U.Button("隐藏",HideToTray);hide.ToolTip="隐藏到系统托盘，农场继续生长。";hide.ToolTipOpening+=(s,e)=>hide.ToolTip="隐藏到系统托盘，农场继续生长。\n"+DesktopShortcuts;hide.Padding=new Thickness(10,4,10,4);DockPanel.SetDock(hide,Dock.Right);head.Children.Add(hide);
        var compact=U.Button("缩成宠物",ShowCompact);compact.ToolTip="只留下单只宠物；双击宠物可以继续养成与冒险。";compact.Padding=new Thickness(10,4,10,4);compact.Margin=new Thickness(5,3,5,3);DockPanel.SetDock(compact,Dock.Right);head.Children.Add(compact);
        adventureButton=U.Button("卡牌远征",()=>OpenPanel("卡牌冒险"),true,true);adventureButton.Padding=new Thickness(12,4,12,4);adventureButton.Margin=new Thickness(5,3,5,3);DockPanel.SetDock(adventureButton,Dock.Right);head.Children.Add(adventureButton);
        var open=U.Button("农场",()=>OpenPanel("农场"));open.Padding=new Thickness(12,4,12,4);DockPanel.SetDock(open,Dock.Right);head.Children.Add(open);
        var title=U.Text("苔间  ·  桌面农场",15);title.FontWeight=FontWeights.SemiBold;title.VerticalAlignment=VerticalAlignment.Center;title.Cursor=G.S.DesktopLocked?Cursors.Arrow:Cursors.SizeAll;moveHandle=title;
        title.MouseLeftButtonDown+=(s,e)=>{if(G.S.DesktopLocked)return;try{DragMove();ClampPosition();RememberPosition();G.Save();}catch(InvalidOperationException){}};head.Children.Add(title);
        scene=new FarmScene(G){Height=166,MinHeight=166,Cursor=Cursors.Hand};scene.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2)OpenPanel();};stack.Children.Add(scene);
        resources=U.Text("",11,U.Muted);resources.Margin=new Thickness(4,5,0,0);stack.Children.Add(resources);
        stageText=U.Text("",12,U.Green);stageText.Margin=new Thickness(4,4,0,0);stack.Children.Add(stageText);
    }
    private void BuildPet()
    {
        double scale=new[]{1d,1.15,1.3}[Math.Max(0,Math.Min(2,G.S.ReadingSize))];
        bool welcome=G.GuideActive&&G.S.GuideStep==0;
        Width=Math.Max(156,Math.Max(G.S.PetSize+20,150*scale+6));Height=G.S.PetSize+42*scale+(welcome?36*scale:0);
        var root=new Grid{Background=Brushes.Transparent};Content=root;
        desktopDecoration=new DecorationView{Decoration=G.S.DisplayDecoration,Width=46,Height=46,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};root.Children.Add(desktopDecoration);
        petImage=new Image{Width=G.S.PetSize,Height=G.S.PetSize,Stretch=Stretch.Uniform,Source=Art.Frame(G.S.DisplayPet,Tick,G.S.Evolved,G.S.SeedStage),VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Cursor=G.S.DesktopLocked?Cursors.Arrow:Cursors.Hand};moveHandle=petImage;
        System.Windows.Automation.AutomationProperties.SetName(petImage,G.Name(G.S.DisplayPet)+"，双击打开玩法，中键切换下一只宠物");root.Children.Add(petImage);
        var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
        Func<bool> current=()=>Object.ReferenceEquals(Content,root);
        Button swap=null;swap=U.Button("换宠",()=>{if(!current())return;var picker=DisplayPetPicker();var hover=petHover;hover.SetPopup(true);picker.Closed+=(s,e)=>hover.SetPopup(false);picker.PlacementTarget=swap;picker.Placement=PlacementMode.Bottom;if(!testing)picker.IsOpen=true;});swap.ToolTip="选择桌面展示的伙伴，也可按鼠标中键切换下一只。";row.Children.Add(swap);
        var expand=U.Button("展开",()=>{if(current())OpenCompactPanel();});expand.ToolTip="打开卡牌冒险，接着刚才的手牌玩";row.Children.Add(expand);
        var hide=U.Button("隐藏",()=>{if(current())HideToTray();});hide.ToolTip="隐藏到托盘，点击托盘图标或再次运行程序可恢复。";row.Children.Add(hide);
        foreach(Button button in row.Children){button.Height=22*scale;button.FontSize=11*scale;button.Padding=new Thickness(6*scale,0,6*scale,0);button.Margin=new Thickness(1,0,1,0);System.Windows.Automation.AutomationProperties.SetAutomationId(button,"pet-tool-"+button.Content);}
        var tools=new StackPanel();tools.Children.Add(row);AddCompactPresence(root,tools);
        compactStatus.FontSize=10*scale;compactStatus.Height=15*scale;
        petTools=new Border{Child=tools,Width=150*scale,Background=U.Cream,CornerRadius=new CornerRadius(10),Padding=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Opacity=0,IsHitTestVisible=false};root.Children.Add(petTools);
        petHover=new FarmPetHover(root,petTools,current,testing);
        root.MouseLeave+=(s,e)=>petDragOrigin=null;
        if(welcome){petWelcome=U.Button("点这里，去冒险",()=>{if(current())OpenCompactPanel();},true,true);petWelcome.FontSize=12*scale;petWelcome.Padding=new Thickness(6,4,6,4);petWelcome.Margin=new Thickness(0,42*scale,0,0);petWelcome.Width=150*scale;petWelcome.Height=32*scale;petWelcome.HorizontalAlignment=HorizontalAlignment.Center;petWelcome.VerticalAlignment=VerticalAlignment.Top;System.Windows.Automation.AutomationProperties.SetAutomationId(petWelcome,"pet-welcome");root.Children.Add(petWelcome);}
        petImage.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){petDragOrigin=null;OpenCompactPanel();}else if(!G.S.DesktopLocked)petDragOrigin=e.GetPosition(this);e.Handled=true;};
        petImage.MouseLeftButtonUp+=(s,e)=>petDragOrigin=null;
        petImage.MouseDown+=(s,e)=>{if(e.ChangedButton==MouseButton.Middle){petDragOrigin=null;NextDisplayPet();e.Handled=true;}};
        petImage.MouseMove+=(s,e)=>{
            if(G.S.DesktopLocked||e.LeftButton!=MouseButtonState.Pressed||!petDragOrigin.HasValue)return;
            var p=e.GetPosition(this);if(Math.Abs(p.X-petDragOrigin.Value.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(p.Y-petDragOrigin.Value.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            petDragOrigin=null;try{DragMove();ClampPosition();RememberPosition();G.Save();}catch(InvalidOperationException){}
        };
        root.MouseWheel+=(s,e)=>{if(!G.S.DesktopLocked)SetPetSize(G.S.PetSize+(e.Delta>0?16:-16));e.Handled=true;};
    }
    private static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
    private Rect[] DesktopWorkAreas
    {
        get{
            if(testWorkAreas!=null)return testWorkAreas;
            Matrix transform;
            if(desktopSource!=null&&desktopSource.CompositionTarget!=null)transform=desktopSource.CompositionTarget.TransformFromDevice;
            else{var primary=Forms.Screen.PrimaryScreen.WorkingArea;var logical=SystemParameters.WorkArea;double scale=logical.Width/Math.Max(1,primary.Width);transform=new Matrix(scale,0,0,scale,0,0);}
            return Forms.Screen.AllScreens.OrderByDescending(screen=>screen.Primary).Select(screen=>{var area=screen.WorkingArea;Point p=transform.Transform(new Point(area.Left,area.Top));Point q=transform.Transform(new Point(area.Right,area.Bottom));return new Rect(p,q);}).ToArray();
        }
    }
    public Rect DesktopWorkArea
    {
        get{
            Rect[] areas=DesktopWorkAreas;double x=Left,y=Top;
            if(!Finite(x)||!Finite(y)){x=IsCompact?G.S.PetLeft:G.S.Left;y=IsCompact?G.S.PetTop:G.S.Top;}
            if(!Finite(x)||!Finite(y)||(!IsCompact&&x==-1&&y==-1))return areas[0];
            return DesktopLayout.ChooseArea(new Rect(x,y,Finite(Width)?Width:156,Finite(Height)?Height:170),areas);
        }
    }
    private void PlaceDesktop()
    {
        var areas=DesktopWorkAreas;double x=IsCompact?G.S.PetLeft:G.S.Left,y=IsCompact?G.S.PetTop:G.S.Top;
        bool saved=Finite(x)&&Finite(y)&&(IsCompact||x!=-1||y!=-1);
        var area=saved?DesktopLayout.ChooseArea(new Rect(x,y,Width,Height),areas):areas[0];
        Left=saved?x:IsCompact?area.Right-Width-28:area.Left+(area.Width-Width)/2;Top=saved?y:area.Bottom-Height-12;
        ClampPosition();
    }
    private void ClampPosition(){var area=DesktopWorkArea;var bounds=DesktopLayout.Clamp(new Rect(Finite(Left)?Left:area.Left,Finite(Top)?Top:area.Top,Width,Height),area);Left=bounds.Left;Top=bounds.Top;}
    private void RememberPosition(){if(!desktopReady)return;if(IsCompact){G.S.PetLeft=Left;G.S.PetTop=Top;}else{G.S.Left=Left;G.S.Top=Top;}}
    private void PauseAndFoldPanel(){foldingPanel=true;try{if(Panel!=null){Panel.PauseForBackground();Panel.Hide();}}finally{foldingPanel=false;}desktopSuppressedForPanel=false;}
    private void ChangeDesktop(string mode)
    {
        RememberPosition();PauseAndFoldPanel();G.S.DesktopMode=mode;G.S.DesktopHidden=false;BuildDesktop();PlaceDesktop();
        if(!testing&&!IsVisible)Show();WindowState=WindowState.Normal;UpdateAnimationState();RefreshCounters();RememberPosition();G.Save();
    }
    public void ShowCompact(){ChangeDesktop("pet");}
    public void ShowFarm(){ChangeDesktop("farm");}
    public void ToggleDesktop(){if(Panel!=null&&Panel.IsVisible)ShowCompact();else if(IsCompact)ShowFarm();else ShowCompact();}
    public void SetPetSize(int size)
    {
        int clamped=Math.Max(96,Math.Min(176,size));if(clamped==G.S.PetSize&&IsCompact)return;
        double center=Left+Width/2,bottom=Top+Height;RememberPosition();G.S.PetSize=clamped;
        if(!IsCompact){ShowCompact();return;}BuildDesktop();Left=center-Width/2;Top=bottom-Height;ClampPosition();RememberPosition();G.Save();RefreshCounters();
    }
    public void HideToTray()
    {
        // A hidden window must always have a recovery path, even if Shell notification setup failed.
        if(!testing&&(tray==null||!tray.Visible)&&!hideKey){G.S.DesktopHidden=false;G.Save();UpdateAnimationState();MessageBox.Show("托盘暂不可用，已保留桌宠。可用右键菜单退出并保存。","苔间");return;}
        if(petHover!=null)petHover.Expire(false,false);RememberPosition();PauseAndFoldPanel();G.S.DesktopHidden=true;G.Save();Hide();animation.Stop();
    }
    public void RestoreDesktop()
    {
        if(Panel!=null&&Panel.IsVisible){G.S.DesktopHidden=false;G.Save();if(Panel.WindowState==WindowState.Minimized)Panel.WindowState=WindowState.Normal;Panel.Activate();SyncPanelPresence();return;}
        G.S.DesktopHidden=false;WindowState=WindowState.Normal;ClampPosition();if(!IsVisible)Show();UpdateAnimationState();RefreshCounters();G.Save();
    }
    public void ToggleHidden(){if(G.S.DesktopHidden||!IsVisible&&(Panel==null||!Panel.IsVisible))RestoreDesktop();else HideToTray();}
    public void ExitAndSave(){exitRequested=true;Close();}
    private void SnapshotDesktop(string name)
    {
        UpdateLayout();var content=(FrameworkElement)Content;
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(content);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(Path.GetDirectoryName(G.SavePath),name+".png")))encoder.Save(f);
    }
    private static void DesktopCheck(bool condition,string message){if(!condition)throw new Exception("Desktop test: "+message);}
    private void DesktopTests()
    {
        DesktopCheck(IsCompact&&Panel==null,"startup opens only the compact pet");SnapshotDesktop("12-compact");
        var previousPet=(UIElement)Content;ShowFarm();previousPet.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseLeaveEvent});SnapshotDesktop("13-full-farm");double farmX=Left,farmY=Top;
        ShowCompact();SetPetSize(96);SnapshotDesktop("14-small-pet");SetPetSize(176);SnapshotDesktop("15-large-pet");SetPetSize(128);
        G.S.DisplayPet=2;ShowCompact();SnapshotDesktop("16-other-pet");G.S.DisplayPet=0;ShowCompact();
        petTools.Opacity=1;SnapshotDesktop("17-pet-controls");petTools.Opacity=0;
        G.Save();var reloaded=new FarmGame(G.SavePath);DesktopCheck(reloaded.S.DesktopMode=="pet"&&reloaded.S.PetSize==128&&Finite(reloaded.S.PetLeft),"display settings survive reload");
        ShowFarm();DesktopCheck(Math.Abs(Left-farmX)<1&&Math.Abs(Top-farmY)<1,"farm position is separate from pet position");
        var originalRun=G.S.Run;G.Begin(0,0);G.RouteChoice(0);OpenPanel("冒险");Panel.UpdateLayout();
        FindButton(Panel,"继续自动战斗").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));DesktopCheck(Panel.IsBattleRunning,"combat active before compacting");
        int round=G.R.Round;ShowCompact();Panel.Pulse();DesktopCheck(!Panel.IsVisible&&!Panel.IsBattleRunning&&G.R.Round==round,"compacting pauses and folds the panel");
        OpenPanel("冒险");Panel.UpdateLayout();FindButton(Panel,"继续自动战斗").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));HideToTray();Panel.Pulse();
        DesktopCheck(!IsVisible&&!Panel.IsVisible&&!animation.IsEnabled&&G.R.Round==round,"hidden mode stops rendering and combat");
        reloaded=new FarmGame(G.SavePath);DesktopCheck(reloaded.S.DesktopHidden,"hidden preference saved");
        int berries=G.S.Berries;G.S.LastFarmTicks=DateTime.UtcNow.AddSeconds(-65).Ticks;G.TickFarm(DateTime.UtcNow);DesktopCheck(G.S.Berries>berries,"hidden farm still harvests");
        bool handled=false;DesktopMessage(desktopHandle,0x8001,IntPtr.Zero,IntPtr.Zero,ref handled);DesktopCheck(handled&&IsVisible&&!G.S.DesktopHidden&&!Panel.IsVisible&&!Panel.IsBattleRunning,"second launch restores without resuming combat");
        DesktopMessage(desktopHandle,0x0312,new IntPtr(HideHotkey),IntPtr.Zero,ref handled);DesktopCheck(!IsVisible,"hide hotkey route");
        DesktopMessage(desktopHandle,0x0312,new IntPtr(HideHotkey),IntPtr.Zero,ref handled);DesktopCheck(IsVisible,"restore hotkey route");
        DesktopMessage(desktopHandle,0x0312,new IntPtr(CompactHotkey),IntPtr.Zero,ref handled);DesktopCheck(!IsCompact,"expand hotkey route");ShowCompact();
        G.S.Run=originalRun;G.Save();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(G.SavePath),"desktop-regression.txt"),"PASS: compact startup without panel; independent positions; size and display persistence; compact/hide pause combat; hidden animation stops; farming continues; hidden preference saves; second-launch and hotkey message recovery; all three pet sizes and alternate pet render.");
    }
}


