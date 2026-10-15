using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

public static class GuideTestLauncher
{
    [STAThread]public static int Main(string[] args)
    {
        string folder=AppDomain.CurrentDomain.BaseDirectory,path=Path.Combine(folder,"TataFarmAdventure.exe");
        bool compatible=false;
        try{if(File.Exists(path)){var program=Assembly.ReflectionOnlyLoadFrom(path).GetType("FarmProgram");compatible=program!=null&&Convert.ToString(program.GetField("Version").GetRawConstantValue())=="4.16";}}catch{compatible=false;}
        if(args.Length==1&&args[0]=="--verify")return compatible?0:1;
        try{if(!compatible)throw new FileNotFoundException("请完整解压最新试玩包，让「卡牌试玩.exe」和同一包内的「TataFarmAdventure.exe」放在同一目录。");Process.Start(new ProcessStartInfo{FileName=path,Arguments="--tutorial-test",WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true});return 0;}
        catch(Exception ex){MessageBox.Show(ex.Message,"引导测试启动失败");return 1;}
    }
}










