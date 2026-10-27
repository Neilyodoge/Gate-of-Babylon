using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Stormfield;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTests.Run();
        string basePath = AppContext.BaseDirectory;
        string data = Path.Combine(basePath, args.Contains("--capture") ? "capture-data" : "data");
        Directory.CreateDirectory(data);
        try
        {
            var tuning = File.Exists(Path.Combine(basePath, "tuning.json"))
                ? JsonSerializer.Deserialize<Tuning>(File.ReadAllText(Path.Combine(basePath, "tuning.json"))) ?? new() : new Tuning();
            tuning.Validate();
            var store = new SaveStore(Path.Combine(data, "progress.json"));
            var progress = args.Contains("--capture") ? new Progress() : store.Load();
            using var game = new GameApp(new(tuning, progress, args.Contains("--capture") ? null : store), data, args.Contains("--capture"));
            game.Run(args.Contains("--capture"));
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(data, "crash.log"), e.ToString());
            Console.Error.WriteLine(e); return 1;
        }
    }
}

public sealed class GameApp : IDisposable
{
    readonly Simulation sim;
    readonly Renderer renderer;
    readonly string data;
    Camera3D camera;
    float yaw, pitch = .60f, distance = 15.5f;
    bool paused = true, help, started, tactical, exit, captured;
    public GameApp(Simulation simulation, string dataPath, bool hidden = false)
    {
        sim = simulation; data = dataPath;
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | (hidden ? ConfigFlags.HiddenWindow : 0));
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(1440, 900, "Stormfield | Geometric Combat Prototype");
        Raylib.SetWindowMinSize(1120, 720); Raylib.SetTargetFPS(120); Raylib.SetExitKey(KeyboardKey.Null);
        renderer = new Renderer(sim);
        UpdateCamera(1);
    }
    void SetPaused(bool value)
    {
        paused = value;
        if (value) { Raylib.EnableCursor(); captured = false; }
        else { Raylib.DisableCursor(); captured = true; }
    }
    public void Run(bool capture)
    {
        int frame = 0;
        while (!exit && !Raylib.WindowShouldClose())
        {
            float dt = Math.Clamp(Raylib.GetFrameTime(), .0001f, .05f);
            if (capture) dt = 1f / 60;
            if (!capture) UpdateInput(dt);
            else
            {
                if (frame == 0) { paused = false; started = true; }
                if (frame == 30) Capture(Path.Combine(data, "01-hub.png"));
                if (frame == 35) { sim.EnterField(); sim.Player = new(0, 0, 8); sim.GodMode = true; }
                if (frame == 80) Capture(Path.Combine(data, "02-combat.png"));
                if (frame == 90) { sim.ToggleStorm(); sim.Build = Build.Circuit; sim.Fields.Add(new(new(-3, 0, 1))); }
                if (frame == 118) sim.Hazards.Add(new(new(-4.5f, 0, .5f), 3.5f, 1.35f, 0));
                sim.Tick(dt, new(Vector3.Zero, -Vector3.UnitZ, frame > 40 && frame % 2 == 0, false, frame == 140));
                if (frame == 148) Capture(Path.Combine(data, "03-storm.png"));
                if (frame == 154) { sim.Weapon = Weapon.Blade; tactical = true; }
                if (frame == 159) Capture(Path.Combine(data, "04-tactical.png"));
                if (frame == 161) { paused = true; help = true; }
                if (frame == 164) Capture(Path.Combine(data, "05-help.png"));
                if (frame >= 167) exit = true;
            }
            UpdateCamera(dt);
            Raylib.BeginDrawing();
            renderer.Draw(camera, tactical);
            if (paused || sim.Dead) DrawMenu();
            Raylib.EndDrawing();
            frame++;
        }
        if (!capture) sim.Save();
    }
    void UpdateInput(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) && (Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt)))
            Raylib.ToggleBorderlessWindowed();
        if (!Raylib.IsWindowFocused() && !paused) SetPaused(true);
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { help = false; SetPaused(!paused); }
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) { help = !help; SetPaused(help); }
        if (paused)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { help = false; started = true; SetPaused(false); }
            return;
        }
        if (sim.Dead)
        {
            if (captured) { Raylib.EnableCursor(); captured = false; }
            if (Raylib.IsKeyPressed(KeyboardKey.R)) { sim.ResetTrial(); SetPaused(false); }
            if (Raylib.IsKeyPressed(KeyboardKey.H)) { sim.ReturnHub(); SetPaused(false); }
            return;
        }
        Vector2 mouse = Raylib.GetMouseDelta();
        yaw += mouse.X * sim.Config.MouseSensitivity;
        pitch = Math.Clamp(pitch + mouse.Y * sim.Config.MouseSensitivity, .25f, 1.12f);
        distance = Math.Clamp(distance - Raylib.GetMouseWheelMove() * 1.2f, 7, 24);
        if (Raylib.IsKeyPressed(KeyboardKey.V)) tactical = !tactical;
        if (Raylib.IsKeyPressed(KeyboardKey.One)) sim.Weapon = Weapon.Bolt;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) sim.Weapon = Weapon.Blade;
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) { sim.Build = sim.Build == Build.Impact ? Build.Circuit : Build.Impact; sim.Notify("当前构筑：" + sim.BuildName + " · 试玩版可随时切换"); }
        if (Raylib.IsKeyPressed(KeyboardKey.T)) sim.ToggleStorm();
        if (Raylib.IsKeyPressed(KeyboardKey.F2)) { sim.ManualWeather = !sim.ManualWeather; sim.Notify(sim.ManualWeather ? "天气已固定" : "自动天气循环已开启"); }
        if (Raylib.IsKeyPressed(KeyboardKey.F6)) { sim.SlowMotion = sim.SlowMotion == 1 ? .5f : 1; sim.Notify(sim.SlowMotion == 1 ? "正常速度" : "慢动作：观察攻击预警"); }
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) { sim.GodMode = !sim.GodMode; sim.Notify(sim.GodMode ? "无敌测试已开启" : "无敌测试已关闭"); }
        if (Raylib.IsKeyPressed(KeyboardKey.F4)) { Capture(Path.Combine(data, "screenshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png")); sim.Notify("截图已保存到 data 目录"); }
        if (Raylib.IsKeyPressed(KeyboardKey.H)) { sim.ReturnHub(); yaw = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.R)) { sim.ResetTrial(); yaw = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.N)) sim.NewLayout();
        if (Raylib.IsKeyPressed(KeyboardKey.F)) { bool wasHub = sim.InHub; sim.Interact(); if (wasHub) yaw = 0; }
        Vector3 forward = new(MathF.Sin(yaw), 0, -MathF.Cos(yaw)), right = new(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        Vector3 move = Vector3.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) move += forward;
        if (Raylib.IsKeyDown(KeyboardKey.S)) move -= forward;
        if (Raylib.IsKeyDown(KeyboardKey.D)) move += right;
        if (Raylib.IsKeyDown(KeyboardKey.A)) move -= right;
        // Screen-centre ray intersects the weapon-height plane, matching the reticle to shots.
        Ray ray = Raylib.GetScreenToWorldRay(new(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f), camera);
        Vector3 aim = forward;
        if (ray.Direction.Y < -.01f)
        {
            float t = (1 - ray.Position.Y) / ray.Direction.Y;
            aim = Simulation.FlatNormal(ray.Position + ray.Direction * t - sim.Player, forward);
        }
        sim.Tick(dt * sim.SlowMotion, new(move, aim, Raylib.IsMouseButtonDown(MouseButton.Left),
            Raylib.IsKeyPressed(KeyboardKey.Space), Raylib.IsKeyPressed(KeyboardKey.Q), Raylib.IsKeyPressed(KeyboardKey.E)));
    }
    void UpdateCamera(float dt)
    {
        Vector3 forward = new(MathF.Sin(yaw), 0, -MathF.Cos(yaw));
        float p = tactical ? 1.03f : pitch, dist = tactical ? 23 : distance;
        Vector3 focus = sim.Player + Vector3.UnitY * 1.1f;
        Vector3 target = sim.Player + Vector3.UnitY * .9f + forward * 6;
        Vector3 offset = -forward * MathF.Cos(p) * dist + Vector3.UnitY * MathF.Sin(p) * dist;
        Vector3 desired = focus + offset;
        if (!sim.InHub)
        {
            Ray ray = new() { Position = focus, Direction = Vector3.Normalize(offset) };
            foreach (var cover in sim.World.Covers)
            {
                var a = cover.Area;
                BoundingBox box = new(new(a.X - a.W / 2 - .2f, 0, a.Z - a.D / 2 - .2f), new(a.X + a.W / 2 + .2f, cover.Height + .3f, a.Z + a.D / 2 + .2f));
                var hit = Raylib.GetRayCollisionBox(ray, box);
                if (hit.Hit && hit.Distance < Vector3.Distance(focus, desired)) desired = focus + ray.Direction * MathF.Max(.7f, hit.Distance - .3f);
            }
        }
        camera = new() { Position = desired, Target = target, Up = Vector3.UnitY, FovY = 57, Projection = CameraProjection.Perspective };
    }
    static unsafe void Capture(string path)
    {
        Image image = Raylib.LoadImageFromScreen();
        sbyte* extension = stackalloc sbyte[] { 46, 112, 110, 103, 0 };
        int size = 0;
        byte* bytes = Raylib.ExportImageToMemory(image, extension, &size);
        try
        {
            if (bytes == null || size <= 0) throw new IOException("Screenshot encoding failed");
            File.WriteAllBytes(path, new ReadOnlySpan<byte>(bytes, size));
        }
        finally { if (bytes != null) Raylib.MemFree(bytes); Raylib.UnloadImage(image); }
    }
    void DrawMenu()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, h, new Color(5, 10, 18, 220));
        float x = w / 2f - 440, y = h / 2f - 290;
        renderer.Panel(x, y, 880, 580, new Color(16, 27, 39, 250));
        renderer.Text("STORMFIELD", x + 34, y + 26, 18, Renderer.Teal);
        renderer.Text(sim.Dead ? "试验中断" : help ? "操作与试验说明" : started ? "试验已暂停" : "风暴试场", x + 32, y + 54, 40, Renderer.White);
        renderer.Text("几何化动作原型 / 移动 · 构筑 · 环境", x + 34, y + 108, 19, Renderer.Muted);
        string[] left = ["W A S D   相对镜头移动", "鼠标移动   旋转镜头 / 瞄准", "鼠标左键   普通攻击（可按住）", "Space   两段闪避，短暂无敌", "Q   冲击波    E   引力场", "1 / 2   远程 / 近战"];
        string[] right = ["Tab   切换破阵 / 回路构筑", "T   晴天 / 风暴    F2   自动天气", "F   出发 / 激活中继", "H   返回据点    R   同布局重试", "V   近景 / 高位镜头    滚轮   距离", "F8   无敌    F6   慢动作    F4   截图"];
        for (int i = 0; i < left.Length; i++)
        { renderer.Text(left[i], x + 36, y + 159 + i * 33, 18, Renderer.White); renderer.Text(right[i], x + 452, y + 159 + i * 33, 18, Renderer.White); }
        renderer.Text("目标：清除三个区域的敌人，靠近黄色中继塔按 F，修复进展自动保存。", x + 36, y + 378, 18, Renderer.Gold);
        renderer.Text("风暴规则：湿地导电会伤害双方。回路攻击可触发连锁，请保留干燥站位。", x + 36, y + 410, 18, Renderer.Muted);
        renderer.Text("N 在据点生成新布局；R 重置本轮中继，累计记录保留。Esc 暂停并释放鼠标。", x + 36, y + 442, 17, Renderer.Muted);
        if (renderer.Button(new(x + 36, y + 497, 270, 48), sim.Dead ? "R  重试同布局" : started ? "Enter  继续试验" : "Enter  开始试验", true))
        { if (sim.Dead) sim.ResetTrial(); started = true; help = false; SetPaused(false); }
        if (renderer.Button(new(x + 326, y + 497, 250, 48), "返回安全区", false))
        { sim.ReturnHub(); started = true; help = false; SetPaused(false); }
        if (renderer.Button(new(x + 596, y + 497, 248, 48), "退出并保存", false)) exit = true;
    }
    public void Dispose() { renderer.Dispose(); Raylib.CloseWindow(); }
}
