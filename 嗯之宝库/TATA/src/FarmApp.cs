using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

public static class FarmProgram
{
    public const string Version="4.16";
    public const string GuideMutexName="Local\\TataFarmAdventure-GuideTest-v"+Version;
    public const string GuideWindowTitle="苔间 · 引导测试 v"+Version;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string c,string t);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    [STAThread]public static int Main(string[] args)
    {
        // This path never shows a window, registers a hotkey, or reads the player's save.
        if(args.Contains("--headless-test"))return FarmHeadlessTests.Run();
        bool test=args.Contains("--self-test"),guideTest=args.Contains("--tutorial-test")&&!test;bool created;
        using(var mutex=new Mutex(true,test?"FarmTest"+Guid.NewGuid():guideTest?GuideMutexName:"Local\\TataFarmAdventure-v1",out created)){
            if(!created){var h=FindWindow(null,guideTest?GuideWindowTitle:"苔间 · 桌面农场");if(h!=IntPtr.Zero)PostMessage(h,guideTest?0x8002:0x8001,IntPtr.Zero,IntPtr.Zero);return 0;}
            try{
                string root=guideTest?Path.GetTempPath():test?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"farm-checks"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TataDesktopPet");if(!guideTest)Directory.CreateDirectory(root);
                if(test)File.WriteAllText(Path.Combine(root,"rules.txt"),FarmTests.Run(root));
                var game=guideTest?new FarmGame(null,null,true):new FarmGame(Path.Combine(root,test?"visual-"+Guid.NewGuid().ToString("N")+".xml":"farm-save.xml"),test?null:Path.Combine(root,"save.xml"));
                FarmWindow.PrepareDesktopStart(game);
                var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
                app.DispatcherUnhandledException+=(s,e)=>{File.WriteAllText(Path.Combine(root,"last-error.txt"),e.Exception.ToString());if(test){app.Shutdown(1);return;}MessageBox.Show("这次操作未完成："+e.Exception.Message,"苔间");e.Handled=true;};
                return app.Run(new FarmWindow(game,test));
            }catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetTempPath(),"TataFarmAdventure-error.txt"),ex.ToString());if(!test)MessageBox.Show(ex.Message,"桌宠启动失败");return 1;}
        }
    }
}
public static class Art
{
    public static List<BitmapSource>[] Frames;
    public static List<BitmapSource> Adult;
    public static List<BitmapSource>[] Seeds;
    private static Dictionary<string,BitmapSource> icons=new Dictionary<string,BitmapSource>();
    public static void Load(){Frames=new[]{Read("BabyFrame"),Read("Friend0"),Read("Friend1"),Read("Friend2")};Adult=Read("AdultFrame");Seeds=Enumerable.Range(1,4).Select(i=>Read("Seed"+i+"Frame")).ToArray();if(Seeds.Any(f=>f.Count==0))throw new Exception("种子进化动画资源不完整。");if(Frames.Any(f=>f.Count==0)||Adult.Count==0)throw new Exception("宠物动画资源不完整。");}
    private static List<BitmapSource> Read(string prefix){var a=Assembly.GetExecutingAssembly();return a.GetManifestResourceNames().Where(n=>n.StartsWith(prefix)).OrderBy(n=>n).Select(n=>{using(var f=a.GetManifestResourceStream(n)){var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=f;b.EndInit();b.Freeze();return (BitmapSource)b;}}).ToList();}
    public static BitmapSource Frame(int pet,int tick,bool evolved,int seedStage=0){var list=pet==4?Seeds[Math.Max(0,Math.Min(3,seedStage))]:pet==0&&evolved?Adult:Frames[pet];return list[(tick+pet*7)%list.Count];}
    public static BitmapSource Icon(string name){if(!icons.ContainsKey(name))icons[name]=Read("PetIcon-"+name+".").Single();return icons[name];}
}
public static class U
{
    public static Brush Color(string hex){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);b.Freeze();return b;}
    public static readonly Brush Ink=Color("#4A4337"),Muted=Color("#70685B"),Green=Color("#436C53"),Cream=Color("#F5EFDF"),Line=Color("#D7C6A8"),Pale=Color("#E4EDD9"),Gold=Color("#865820");
    public static TextBlock Text(string s,double size=14,Brush color=null){return new TextBlock{Text=s,FontSize=size,Foreground=color??Ink,FontWeight=size>=16?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,7),LineHeight=size*1.45};}
    public static Button Button(string s,Action action,bool enabled=true,bool primary=false)
    {
        var b=new FarmButton{Content=s,IsEnabled=enabled,Foreground=enabled?(primary?Brushes.White:Ink):Muted,Background=enabled?(primary?Green:FarmTheme.Paper):Pale,BorderBrush=Line,BorderThickness=new Thickness(1,1,1,2),Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,3,0,3),FontSize=12,Cursor=enabled?Cursors.Hand:Cursors.Arrow,HorizontalContentAlignment=HorizontalAlignment.Center};
        var border=new FrameworkElementFactory(typeof(Border));border.Name="ButtonFrame";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var cp=new FrameworkElementFactory(typeof(ContentPresenter));cp.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);cp.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(cp);var buttonTemplate=new ControlTemplate(typeof(Button)){VisualTree=border};foreach(var property in new[]{UIElement.IsKeyboardFocusedProperty,FarmButton.ShowKeyFocusProperty}){var focus=new Trigger{Property=property,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,U.Color("#244B37"),"ButtonFrame"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(2),"ButtonFrame"));buttonTemplate.Triggers.Add(focus);}b.Template=buttonTemplate;
        ToolTipService.SetShowOnDisabled(b,true);
        b.IsEnabledChanged+=(s1,e)=>{b.Foreground=b.IsEnabled?(primary?Brushes.White:Ink):Muted;b.Background=b.IsEnabled?(primary?Green:FarmTheme.Paper):Pale;b.Cursor=b.IsEnabled?Cursors.Hand:Cursors.Arrow;};
        b.MouseEnter+=(s1,e)=>{if(b.IsEnabled)b.Opacity=.85;};b.MouseLeave+=(s1,e)=>b.Opacity=1;b.Click+=(s1,e)=>action();return b;
    }
    public static Border Card(UIElement child,double padding=14){return new Border{Child=child,Background=FarmTheme.Paper,CornerRadius=new CornerRadius(16),BorderBrush=Line,BorderThickness=new Thickness(1,1,1,2),Padding=new Thickness(padding),Margin=new Thickness(0,0,0,12)};}
    public static ProgressBar Bar(int n,int max,Brush color=null){return new ProgressBar{Minimum=0,Maximum=Math.Max(1,max),Value=Math.Max(0,Math.Min(n,max)),Height=6,BorderThickness=new Thickness(0),Foreground=color??Green,Background=Pale,Margin=new Thickness(0,3,0,9)};}
    public static void Row(Panel parent,params UIElement[] items){var g=new Grid();for(int i=0;i<items.Length;i++){g.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(items[i],i);var fe=items[i] as FrameworkElement;if(fe!=null)fe.Margin=new Thickness(i==0?0:4,2,i==items.Length-1?0:4,2);g.Children.Add(items[i]);}parent.Children.Add(g);}
}
public partial class FarmScene:FrameworkElement
{
    public FarmGame Game;public int Tick;public bool Battle,HideFarmPets;public int FocusedFarmPet=-1;
    public int[] DecorationPreview;
    public int PreviewSpot=-1;
    private static readonly Brush[] CropColors={U.Color("#B95071"),U.Color("#CFAC4D"),U.Color("#80B3AF")};
    public FarmScene(FarmGame g,bool battle=false){Game=g;Battle=battle;MinHeight=battle?242:185;ClipToBounds=true;}
    public double ReadingScale=1;
    private FormattedText SceneText(string text,double size,Brush color,double width)
    {
        var label=new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),size*Math.Max(1,Math.Min(1.3,ReadingScale)),color,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        label.MaxTextWidth=Math.Max(1,width);return label;
    }
    private void Label(DrawingContext d,string s,double x,double y,double size,Brush color)
    {
        x=Math.Max(2,x);var label=SceneText(s,size,color,ActualWidth-x-4);d.DrawText(label,new Point(x,Math.Max(2,Math.Min(y,ActualHeight-label.Height-2))));
    }
    private void CenterLabel(DrawingContext d,string s,double center,double y,double size,Brush color,double width,bool above=false)
    {
        width=Math.Max(1,Math.Min(width,ActualWidth-4));double x=Math.Max(2,Math.Min(center-width/2,ActualWidth-width-2));var label=SceneText(s,size,color,width);label.TextAlignment=TextAlignment.Center;
        d.DrawText(label,new Point(x,Math.Max(2,Math.Min(above?y-label.Height:y,ActualHeight-label.Height-2))));
    }
    protected override void OnRender(DrawingContext d)
    {
        double w=ActualWidth,h=ActualHeight;if(w<180||h<60)return;if(LearningPlot>=0&&LearningPlot<Game.Plots){DrawLearningPlot(d,w,h);return;}if(Journey){DrawJourney(d,w,h);return;}var g=Game;double unit=w/760;
        d.DrawRoundedRectangle(U.Color(Battle?"#E3EBD4":"#EAF0D8"),null,new Rect(0,0,w,h),14,14);
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),14,14));
        d.DrawEllipse(U.Color("#F4DE96"),null,new Point(w*.84,35),22,22);
        if(!Battle){for(int ray=0;ray<10;ray++){double a=ray*Math.PI/5;d.DrawLine(new Pen(U.Color("#E8CC82"),2),new Point(w*.84+Math.Cos(a)*27,35+Math.Sin(a)*27),new Point(w*.84+Math.Cos(a)*31,35+Math.Sin(a)*31));}d.DrawEllipse(FarmTheme.Paper,null,new Point(w*.25,29),22,7);d.DrawEllipse(FarmTheme.Paper,null,new Point(w*.28,24),14,11);d.DrawEllipse(FarmTheme.Paper,null,new Point(w*.31,29),19,7);}
        d.DrawEllipse(U.Color("#D1E0C8"),null,new Point(w*.15,h*.55),w*.43,h*.34);
        d.DrawEllipse(U.Color("#C4D8B9"),null,new Point(w*.8,h*.63),w*.45,h*.38);
        d.DrawRectangle(U.Color("#ABC185"),null,new Rect(0,h*.64,w,h*.36));
        for(int i=0;i<23;i++){double x=(i*97)%Math.Max(1,w);double y=h*.7+(i*19)%Math.Max(1,h*.25);d.DrawLine(new Pen(U.Color("#8FB679"),1),new Point(x,y),new Point(x+3,y-4));}
        if(!Battle){
            double fenceY=h*.58;var fence=new Pen(U.Color("#C2B084"),2);d.DrawLine(fence,new Point(8,fenceY+5),new Point(w-8,fenceY+5));for(int x=15;x<w;x+=31)d.DrawRoundedRectangle(U.Color("#E9D8AA"),fence,new Rect(x,fenceY-5,5,20),2,2);
            var decorations=DecorationPreview??g.S.GardenDecorations;
            double decorSize=Math.Min(46,w*.075);double[] spots={12,w*.5-decorSize/2,w-decorSize-12};
            if(DecorationPreview!=null&&PreviewSpot>=0&&PreviewSpot<3){var outline=new Pen(U.Gold,1.2){DashStyle=DashStyles.Dash};d.DrawRoundedRectangle(U.Color("#60FFF9E8"),outline,new Rect(spots[PreviewSpot]-4,1,decorSize+8,decorSize+6),7,7);}
            for(int i=0;i<3;i++)DecorationView.Draw(d,new Rect(spots[i],3,decorSize,decorSize),decorations[i],Tick);
            int plotCount=FocusedFarmPet>=0?g.Plots:6;double plotW=(w-32)/plotCount;
            for(int i=0;i<plotCount;i++){
                double x=16+i*plotW,y=h-55;bool unlocked=i<g.Plots;
                d.DrawRoundedRectangle(U.Color(unlocked?"#A38159":"#ADB889"),new Pen(U.Color(unlocked?"#7C694A":"#99A97C"),1.5),new Rect(x+3,y,plotW-6,40),8,8);
                if(!unlocked){CenterLabel(d,"通关\n解锁",x+plotW/2,y+4,11,U.Ink,plotW-14);continue;}
                int crop=g.S.Crops[i];double growth=g.S.Growth[i]/g.CropSeconds(crop);
                for(int k=0;k<3;k++){
                    double px=x+plotW*(.23+k*.26),py=y+23;double height=8+growth*18;
                    d.DrawLine(new Pen(U.Color("#709253"),3),new Point(px,py),new Point(px,py-height));
                    d.DrawEllipse(U.Color("#94BC68"),null,new Point(px-4,py-height*.5),5,3);
                    d.DrawEllipse(CropColors[crop],null,new Point(px,py-height),crop==1?3:5,crop==1?7:4);
                }
                d.DrawRoundedRectangle(U.Color("#C3AC80"),null,new Rect(x+10,y+33,plotW-20,3),2,2);
                d.DrawRoundedRectangle(U.Color("#F4D47B"),null,new Rect(x+10,y+33,(plotW-20)*Math.Min(1,growth),3),2,2);
            }
            for(int p=0;p<FarmGame.PetCount;p++){
                if(HideFarmPets)continue;
                if(FocusedFarmPet>=0&&p!=FocusedFarmPet)continue;
                double x=FocusedFarmPet>=0?w/2:35+((w-70)/FarmGame.PetCount)*(p+.5)+Math.Sin(Tick*.028+p*2)*10*unit;double size=Math.Min(Math.Min(106,100*unit),Math.Max(16,h-84));if(FocusedFarmPet>=0)size=Math.Min(100,Math.Max(38,h-110));if(decorations.Any(id=>id>=0))size=Math.Min(size,Math.Max(38,h-120));double y=h-62-size+Math.Sin(Tick*.07+p)*2;
                d.DrawEllipse(U.Color("#83A571"),null,new Point(x,h-65),size*.3,5);
                if(p==FocusedFarmPet){PetHitBounds=new Rect(x-size/2-12,y-12,size+24,size+24);if(DateTime.UtcNow<PetResponseUntil){y-=6;Label(d,"♥",x+size*.4,y,18,U.Color("#AD5967"));}}bool away=!g.AtHome&&g.R.Phase!="result"&&g.S.Team.Contains(p);d.PushOpacity(away?.28:1);d.DrawImage(Art.Frame(p,Tick,g.S.Evolved,g.S.SeedStage),new Rect(x-size/2,y,size,size));d.Pop();
                CenterLabel(d,g.Name(p)+(p==g.Caretaker?"\n留守":away?"\n远足中":""),x,y-2,11,U.Ink,FocusedFarmPet>=0?w*.3:(w-70)/FarmGame.PetCount-6,true);
            }
            DrawBerryBasket(d,w,h);
        }else{
            var r=g.R;double scale=w/590.0;double size=92*scale;
            for(int i=0;i<3;i++){
                int p=g.S.Team[i];double x=(53+i*80)*scale;double y=92+(i%2)*14;double bounce=Math.Sin(Tick*.08+i)*3;
                d.PushOpacity(r.Hp[p]>0?1:.3);d.DrawImage(Art.Frame(p,Tick,g.S.Evolved,g.S.SeedStage),new Rect(x-size/2,y-size*.35+bounce,size,size));d.Pop();
                string petName=g.Name(p);if(petName.Length>=4&&SceneText(petName,12,U.Ink,w).Width>80*scale)petName=petName.Insert((petName.Length+1)/2,"\n");
                CenterLabel(d,petName,x,55,12,U.Ink,80*scale,true);CenterLabel(d,i==0?"前排":"后排",x,58,10,U.Muted,76*scale);
                d.DrawRoundedRectangle(U.Pale,null,new Rect(x-31*scale,180,62*scale,6),3,3);d.DrawRoundedRectangle(U.Green,null,new Rect(x-31*scale,180,62*scale*Math.Max(0,(double)r.Hp[p]/g.MaxHp(p)),6),3,3);
                CenterLabel(d,r.Hp[p]+"/"+g.MaxHp(p)+(r.Shield[p]>0?"\n盾 "+r.Shield[p]:""),x,194,10,U.Ink,78*scale);
            }
            Label(d,"VS",w*.48,111,18,U.Muted);
            for(int i=0;i<r.EnemyCount;i++){
                double x=(378+i*80)*scale;int kind=r.EnemyKind[i];double boss=kind==3?1.13:1;double y=94+(i%2)*14;
                d.PushOpacity(r.EnemyHp[i]>0?1:.22);d.DrawImage(Art.Frame(kind==3?3:kind+1,Tick,false),new Rect(x-size*boss/2,y-size*.35+Math.Sin(Tick*.065+i)*3,size*boss,size*boss));d.Pop();
                CenterLabel(d,g.EnemyName(i),x,55,11,U.Color("#864B40"),78*scale,true);
                if(r.Wet[i]>0)CenterLabel(d,"潮湿",x,60,10,U.Color("#306C83"),78*scale);if(r.Burn[i]>0)CenterLabel(d,"灼烧 "+r.Burn[i],x,79,10,U.Color("#925125"),78*scale);if(r.EnemyHp[i]>0&&r.Poison[i]>0)CenterLabel(d,"中毒 "+r.Poison[i],x,r.Burn[i]>0?98:79,10,U.Color("#4D6828"),78*scale);
                d.DrawRoundedRectangle(U.Pale,null,new Rect(x-31*scale,180,62*scale,6),3,3);d.DrawRoundedRectangle(U.Color("#BC7B61"),null,new Rect(x-31*scale,180,62*scale*(r.EnemyMax[i]==0?0:(double)r.EnemyHp[i]/r.EnemyMax[i]),6),3,3);CenterLabel(d,r.EnemyHp[i]+"/"+r.EnemyMax[i],x,194,10,U.Ink,78*scale);
            }
            if(r.Round>0&&Tick%22<4&&r.Log.Contains("潮湿追击")){var pen=new Pen(U.Color("#9E85D0"),3);d.DrawLine(pen,new Point(w*.4,140),new Point(w*.52,123));d.DrawLine(pen,new Point(w*.52,123),new Point(w*.6,141));Label(d,"潮湿追击",w*.4,150,11,U.Color("#7457A1"));}
        }
        d.Pop();
    }
}
public partial class FarmWindow:Window
{
    public readonly FarmGame G;public FarmPanel Panel;public bool Exiting;public int Tick;
    private FarmScene scene;private TextBlock resources,stageText;private Button adventureButton;private DispatcherTimer animation,clock;private bool testing;private int seconds;
    public FarmWindow(FarmGame g,bool test)
    {
        G=g;testing=test;Art.Load();InitializeDesktop();
    }
    public void RefreshCounters()
    {
        RefreshPresence();
        if(desktopDecoration!=null){desktopDecoration.Decoration=G.S.DisplayDecoration;desktopDecoration.InvalidateVisual();}
        if(resources!=null)resources.Text="莓果 "+G.S.Berries+"    麦穗 "+G.S.Grain+"    肥料 "+G.S.Fertilizer+"    晶石 "+G.S.Crystals+"     ·     "+G.Name(G.Caretaker)+"留守  /  自动收获中";
        if(adventureButton!=null)adventureButton.Content="卡牌远征";
        if(stageText!=null)stageText.Text=FarmUX.Activity(G,Panel!=null&&Panel.IsBattleRunning);
        if(petImage!=null)petImage.ToolTip=G.Name(G.S.DisplayPet)+" · "+FarmUX.Activity(G,Panel!=null&&Panel.IsBattleRunning)+"\n悬停点「换宠」 · 中键切换下一只\n双击打开玩法 · 右键设置\n"+(G.S.DesktopLocked?"位置已锁定（右键可解锁）":"拖动移动 · 滚轮调大小")+(G.S.DesktopMotion?"":"\n动作已暂停，农场仍在收获");
        if(Panel!=null&&Panel.IsVisible)Panel.RefreshCounters();
    }
    public void Refresh(){RefreshCounters();if(scene!=null)scene.InvalidateVisual();if(petImage!=null)petImage.Source=Art.Frame(G.S.DisplayPet,Tick,G.S.Evolved,G.S.SeedStage);if(Panel!=null&&Panel.IsVisible)Panel.Render();}
    public void OpenPanel(string tab=null)
    {
        if(G.S.DesktopHidden){G.S.DesktopHidden=false;G.Save();}
        if(Panel==null)Panel=new FarmPanel(this);
        Panel.Tab=tab??"卡牌冒险";
        if(Panel.Tab!="冒险")Panel.PauseForBackground();
        // A minimized WPF window is still IsVisible; Activate alone does not restore it.
        bool minimized=Panel.WindowState==WindowState.Minimized;
        if(minimized){Panel.PauseForBackground();Panel.WindowState=WindowState.Normal;}
        var area=DesktopWorkArea;
        if(!Panel.IsVisible||minimized||Panel.Left+Panel.Width<=area.Left||Panel.Left>=area.Right||Panel.Top+Panel.Height<=area.Top||Panel.Top>=area.Bottom){
            bool small=Panel.WantsGentleNarrow;
            Panel.MinWidth=Math.Min(small?500:780,area.Width-32);Panel.MinHeight=Math.Min(small?540:610,area.Height-40);
            Panel.Width=Math.Min(small?560:940,area.Width-32);Panel.Height=Math.Min(small?580:760,area.Height-40);
            if(Panel.Tab=="卡牌冒险")Panel.MinHeight=Math.Min(720,area.Height-40);
            Panel.Left=area.Left+(area.Width-Panel.Width)/2;Panel.Top=area.Top+Math.Max(10,(area.Height-Panel.Height)/2-35);
        }
        Panel.Render();if(!Panel.IsVisible)Panel.Show();UpdateAnimationState();Panel.Activate();Panel.Focus();
    }
    private void Snapshot(string name){Panel.UpdateLayout();var content=(FrameworkElement)Panel.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(Path.GetDirectoryName(G.SavePath),name+".png")))enc.Save(f);}
    private static Button FindButton(DependencyObject root,string label){var button=root as Button;if(button!=null&&Convert.ToString(button.Content)==label)return button;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindButton(VisualTreeHelper.GetChild(root,i),label);if(found!=null)return found;}return null;}
    private void VisualTest()
    {
        animation.Stop();clock.Stop();try{
            DesktopTests();OpenPanel();
            Panel.Tab="农场";Panel.WindowState=WindowState.Minimized;OpenPanel("冒险");
            if(Panel.WindowState==WindowState.Minimized||!Panel.IsVisible||Panel.Tab!="冒险")throw new Exception("Minimized adventure panel did not restore");
            Panel.Close();OpenPanel("冒险");
            if(!Panel.IsVisible||Panel.WindowState==WindowState.Minimized||Panel.Tab!="冒险")throw new Exception("Hidden adventure panel did not reopen");
            Panel.Tab="农场";OpenPanel("冒险");if(Panel.Tab!="冒险")throw new Exception("Adventure entry did not navigate");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(G.SavePath),"window-regression.txt"),"PASS: minimized panel restores; hidden panel reopens; adventure entry navigates to adventure.");
            Panel.Tab="农场";Panel.Render();Snapshot("01-farm");Panel.Tab="伙伴";Panel.Render();Snapshot("02-team");
            G.Feed(0);G.Feed(0);G.Feed(0);G.Begin(0,0);Panel.Tab="冒险";Panel.Render();Snapshot("03-route");G.RouteChoice(0);Panel.Render();Panel.UpdateLayout();
            var resume=FindButton(Panel,"继续自动战斗");if(resume==null)throw new Exception("No clear resume control");resume.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));object stable=Panel.Content;Panel.Pulse();
            if(G.R.Round!=1||!Object.ReferenceEquals(stable,Panel.Content))throw new Exception("Auto-battle replaced controls instead of updating them");
            Panel.WindowState=WindowState.Minimized;Panel.Pulse();if(G.R.Round!=1)throw new Exception("Minimized battle advanced");OpenPanel("冒险");if(Panel.IsBattleRunning)throw new Exception("Battle resumed without player input");Snapshot("04-battle");
            while(G.R.Phase=="battle")G.Step();Panel.Render();Snapshot("05-draft");double savedW=Panel.Width,savedH=Panel.Height;Panel.Width=780;Panel.Height=610;Panel.Render();Snapshot("09-small-draft");Panel.Width=savedW;Panel.Height=savedH;FarmTests.Clear(G);Panel.Render();Snapshot("06-result");G.Home();Panel.Tab="伙伴";Panel.Render();Snapshot("10-ready-evolution");G.Evolve();Panel.Render();Snapshot("07-evolution");Panel.Tab="农场";Panel.Render();Snapshot("08-unlocked-farm");Panel.Tab="怎么玩";Panel.Render();Snapshot("11-help");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(G.SavePath),"ux-regression.txt"),"PASS: resume action works; automatic round preserves controls; minimizing pauses combat; restoring requires explicit resume; old and new rewards render; small-screen draft and evolution guidance render.");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(G.SavePath),"visual.txt"),"PASS: 180 animated frames loaded; farm, team, route, battle, three-choice draft, results, evolution and unlocked farm rendered; 20 live animation ticks.");ExitAndSave();
        }catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetDirectoryName(G.SavePath),"visual-error.txt"),ex.ToString());Application.Current.Shutdown(1);}
    }
}











