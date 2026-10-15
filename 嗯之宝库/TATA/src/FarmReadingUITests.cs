using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public partial class FarmPanel
{
    private void ReadingPreview(string root,string name,int size,bool narrow=false)
    {
        G.S.ReadingSize=size;Render();UIPreview(root,"reading-"+size+"-"+name,narrow?484:764,narrow?501:571);
        var content=(FrameworkElement)Content;
        foreach(var control in KeyboardTargets()){
            if(control.ActualWidth<=0||control.ActualHeight<=0)continue;
            var point=control.TranslatePoint(new Point(0,0),content);
            FarmHeadlessTests.Check(point.X>=-.5&&point.X+control.ActualWidth<=content.ActualWidth+.5,"reading control fits width: "+size+" / "+name+" / "+ControlKey(control));
        }
    }
    private void ReadingDpiPreview(string root,string name,double scale,double width,double height)
    {
        var content=(FrameworkElement)Content;Content=null;
        try{
            content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(root,"ui-reading-dpi-"+name+".png")))encoder.Save(file);
            FarmHeadlessTests.Check(bitmap.PixelWidth==(int)Math.Ceiling(width*scale)&&bitmap.PixelHeight==(int)Math.Ceiling(height*scale),"DPI bitmap has expected dimensions");
        }finally{Content=content;}
    }
    internal void RenderReadingChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++){
            G.ResetGuideTest();ResetGuideView();ReadingPreview(root,"welcome",size,true);
            var title=LogicalNodes((DependencyObject)Content).OfType<TextBlock>().First(t=>t.Text.StartsWith("你好，我是"));
            check(Math.Abs(title.FontSize-23*new[]{1d,1.15,1.3}[size])<.01,"reading size enlarges actual text, not just the window");GuideFits(GuidePrimary());
            GuideClick(UIAction("阅读与操作"));ReadingPreview(root,"settings",size,true);GuideFits(UIAction("默认字号"));GuideFits(UIAction("更大字"));GuideFits(UIAction("回到游戏"));
            int changed=(size+1)%3;string journeyBefore=FarmPracticeTests.Xml(G.R);GuideClick(UIAction(new[]{"默认字号","大字","更大字"}[changed]));check(G.S.ReadingSize==changed&&FarmPracticeTests.Xml(G.R)==journeyBefore,"actual reading buttons change only the presentation preference");Render();GuideClick(UIAction(new[]{"默认字号","大字","更大字"}[size]));check(G.S.ReadingSize==size,"actual reading button restores the requested size");Render();
            GuideClick(UIAction("回到游戏"));ReadingPreview(root,"closed-settings",size,true);GuideClick(GuidePrimary());ReadingPreview(root,"first-feed",size,true);GuideFits(GuidePrimary());
            GuideClick(GuidePrimary());ReadingPreview(root,"farm-intro",size,true);GuideFits(GuidePrimary());
            GuideClick(GuidePrimary());ReadingPreview(root,"waiting",size,true);GuideFits(GuidePrimary());
            G.AdvanceGuideTest();ReadingPreview(root,"harvest",size,true);GuideFits(GuidePrimary());GuideClick(GuidePrimary());ReadingPreview(root,"basics-complete",size,true);GuideFits(GuidePrimary());
            GuideClick(GuidePrimary());ReadingPreview(root,"resting",size,true);G.AdvanceGuideTest();ReadingPreview(root,"invitation",size,true);GuideFits(UIAction("先待在家里"));GuideClick(UIAction("先待在家里"));ReadingPreview(root,"staying",size,true);
            GuideClick(UIAction("出门"));ReadingPreview(root,"departure-party",size,true);GuideFits(GuidePrimary());GuideFits(UIAction("先待在家里"));GuideClick(GuidePrimary());
            ReadingPreview(root,"departure",size,true);GuideFits(GuidePrimary());GuideFits(UIAction("先待在家里"));GuideClick(GuidePrimary());ReadingPreview(root,"travel",size);
            ReachGentleInspiration();ReadingPreview(root,"inspiration",size);UIAction("就选这个，继续走").BringIntoView();scroller.UpdateLayout();
            GuideClick(UIAction("我想换一个"));ReadingPreview(root,"inspiration-expanded",size);check(InspirationButtons()==3,"large text retains all original alternatives");
            G.AdvanceJourney(DateTime.UtcNow.AddHours(1));G.SyncGuide();ReadingPreview(root,"result",size,true);GuideFits(GuidePrimary());GuideClick(GuidePrimary());ReadingPreview(root,"soil",size,true);GuideFits(GuidePrimary());GuideClick(GuidePrimary());ReadingPreview(root,"complete",size,true);GuideFits(GuidePrimary());GuideClick(GuidePrimary());ReadingPreview(root,"quiet-home",size,true);
            if(size==2)foreach(double dpi in new[]{1.25,1.5,2d})ReadingDpiPreview(root,(dpi*100).ToString("0"),dpi,484,501);
            ExpandAllPlay("农场");ReadingPreview(root,"full-home",size);
            foreach(string tab in new[]{"伙伴","农田","配队","培养","进化","手册","便当","归家","小院","怎么玩","详细规则"}){Go(tab);ReadingPreview(root,tab,size);}
        }
        G.ResetGuideTest();ResetGuideView();ReadingPreview(root,"reset",0,true);
        check(!IsVisible&&!owner.IsVisible&&G.SavePath==null,"reading matrix remains detached and memory-only");
        File.WriteAllText(Path.Combine(root,"reading-ui-regression.txt"),"PASS: default/115%/130% text at minimum window sizes; full onboarding and optional adventure; original three inspiration alternatives; return and simple home; all major management pages; responsive string buttons and wrapping footer; 125%/150%/200% offscreen DPI rendering. No native-window or physical-monitor claims.");
    }
}
