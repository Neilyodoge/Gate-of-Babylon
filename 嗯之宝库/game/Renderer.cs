using System.Numerics;
using Raylib_cs;

namespace Stormfield;

public sealed class Renderer : IDisposable
{
    public static readonly Color Teal = new(91, 231, 207, 255), White = new(233, 242, 244, 255), Muted = new(147, 171, 184, 255),
        Gold = new(247, 202, 104, 255), Red = new(252, 116, 112, 255), Blue = new(94, 169, 255, 255);
    readonly Simulation s;
    readonly Font font;
    readonly bool customFont;
    Camera3D camera;
    public unsafe Renderer(Simulation sim)
    {
        s = sim;
        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simhei.ttf");
        if (File.Exists(fontPath))
        {
            string glyphPath = Path.Combine(AppContext.BaseDirectory, "glyphs.txt");
            string corpus = File.Exists(glyphPath) ? File.ReadAllText(glyphPath) : "风暴试场安全区开始探索继续返回构筑地图技能攻击闪避敌人天气中继已修复出发";
            int[] codepoints = Enumerable.Range(32, 95).Concat(corpus.Select(c => (int)c)).Distinct().ToArray();
            font = Raylib.LoadFontEx(fontPath, 40, codepoints, codepoints.Length);
            customFont = font.Texture.Id != 0;
            Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        }
        else font = Raylib.GetFontDefault();
    }
    public void Text(string text, float x, float y, float size, Color color) => Raylib.DrawTextEx(font, text, new(x, y), size, .5f, color);
    public void Panel(float x, float y, float w, float h, Color? color = null)
    {
        var r = new Rectangle(x, y, w, h);
        Raylib.DrawRectangleRec(r, color ?? new Color(12, 23, 34, 235));
        Raylib.DrawRectangleLinesEx(r, 1, new Color(68, 98, 112, 150));
    }
    public bool Button(Rectangle r, string text, bool primary)
    {
        bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Raylib.DrawRectangleRec(r, primary ? hover ? White : Teal : hover ? new Color(44, 66, 80, 255) : new Color(25, 43, 57, 255));
        Text(text, r.X + 18, r.Y + 13, 20, primary ? new Color(12, 30, 37, 255) : White);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
    public void Draw(Camera3D view, bool tactical)
    {
        camera = view;
        Raylib.ClearBackground(s.Storm && !s.InHub ? new Color(22, 32, 51, 255) : new Color(30, 47, 59, 255));
        Raylib.BeginMode3D(view);
        if (s.InHub) DrawHub(); else DrawWorld();
        DrawEffects(); DrawPlayer(s.Player, 1);
        Raylib.EndMode3D();
        if (s.Storm && !s.InHub) DrawRain();
        DrawWorldLabels(); DrawHud(tactical);
        if (s.HurtFlash > 0)
        {
            int alpha = (int)(s.HurtFlash / .32f * 100);
            Raylib.DrawRectangleLinesEx(new(4, 4, Raylib.GetScreenWidth() - 8, Raylib.GetScreenHeight() - 8), 7, new Color(255, 94, 100, alpha));
        }
    }
    static Color Fade(Color c, float a) => new(c.R, c.G, c.B, (int)(Math.Clamp(a, 0, 1) * 255));
    void Box(Vector3 center, Vector3 size, Color color, bool wire = true)
    {
        Raylib.DrawCubeV(center, size, color);
        if (wire) Raylib.DrawCubeWiresV(center, size + new Vector3(.006f), Fade(White, .16f));
    }
    void Floor(Area a, Color color)
    {
        Box(a.Center - Vector3.UnitY * .26f, new(a.W, .5f, a.D), color);
        for (float x = a.X - a.W / 2 + 1.5f; x < a.X + a.W / 2; x += 3)
            Raylib.DrawLine3D(new(x, .006f, a.Z - a.D / 2), new(x, .006f, a.Z + a.D / 2), new Color(72, 99, 109, 120));
        for (float z = a.Z - a.D / 2 + 1.5f; z < a.Z + a.D / 2; z += 3)
            Raylib.DrawLine3D(new(a.X - a.W / 2, .006f, z), new(a.X + a.W / 2, .006f, z), new Color(72, 99, 109, 120));
    }
    void DrawHub()
    {
        Floor(new(0, 0, 24, 24), new Color(44, 67, 74, 255));
        Floor(new(0, -8, 7, 6), new Color(49, 90, 94, 255));
        for (int side = -1; side <= 1; side += 2)
        {
            Box(new(side * 3.2f, 2.5f, -8), new(.7f, 5, .9f), new Color(73, 115, 120, 255));
            Box(new(side * 2.75f, 2.5f, -8), new(.12f, 4.6f, .8f), Teal, false);
            for (int i = 0; i < 4; i++) Box(new(side * 10, .5f, 7 - i * 5), new(1, 1, 1), new Color(63, 92, 104, 255));
        }
        Box(new(0, 5, -8), new(7, .6f, .9f), new Color(75, 120, 126, 255));
        Ring(new(0, .06f, -8), 2.4f, Teal);
        for (int i = 0; i < 7; i++)
        {
            float z = -6 + i * 1.4f;
            Raylib.DrawLine3D(new(-.4f, .03f, z + .4f), new(0, .03f, z), Fade(Teal, .65f));
            Raylib.DrawLine3D(new(.4f, .03f, z + .4f), new(0, .03f, z), Fade(Teal, .65f));
        }
        for (int i = 0; i < 3; i++)
        {
            Color color = s.Progress.Relays[i] ? Teal : new Color(95, 119, 129, 255);
            Vector3 p = new(-8, 0, -5 + i * 4);
            Box(p + Vector3.UnitY * .35f, new(2, .7f, 2), new Color(41, 59, 72, 255));
            Raylib.DrawCylinder(p + Vector3.UnitY * .7f, .45f, .7f, 1.5f, 6, color);
            Ring(p + Vector3.UnitY * .02f, 1.3f, Fade(color, .7f));
        }
        Box(new(8, .8f, -4), new(2.5f, 1.6f, 2), new Color(62, 81, 95, 255));
        Box(new(8, 1.67f, -4), new(2, .12f, 1.4f), Gold);
    }
    void DrawWorld()
    {
        for (int i = 0; i < s.World.Floors.Count; i++)
            Floor(s.World.Floors[i], i < 3 ? new Color(46 + i * 3, 65 + i * 3, 73 + i * 4, 255) : new Color(54, 78, 86, 255));
        foreach (var room in s.World.Rooms)
        {
            Color color = s.Progress.Relays[room.Index] ? Teal : Gold;
            Vector3 p = room.Relay;
            Box(p + Vector3.UnitY * .25f, new(2.8f, .5f, 2.8f), new Color(33, 50, 63, 255));
            Raylib.DrawCylinder(p + Vector3.UnitY * .5f, .42f, .8f, 2.8f, 6, new Color(72, 95, 112, 255));
            Raylib.DrawSphereEx(p + Vector3.UnitY * (3.55f + MathF.Sin(s.Clock * 2) * .13f), .38f, 6, 8, color);
            Raylib.DrawCylinder(p + Vector3.UnitY * .02f, 1.8f, 1.8f, .035f, 24, Fade(color, .20f));
            Ring(p + Vector3.UnitY * .07f, 2, color);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathF.PI / 4 + MathF.PI / 2 * i;
                Vector3 corner = room.Center + new Vector3(MathF.Sin(angle) * 16, 0, MathF.Cos(angle) * 16);
                Box(corner + Vector3.UnitY * 1.1f, new(.8f, 2.2f, .8f), new Color(70, 97, 110, 255));
                Box(corner + Vector3.UnitY * 2.25f, new(.6f, .12f, .6f), room.Index == s.CurrentRoom ? Teal : Muted, false);
            }
        }
        foreach (var pool in s.World.Pools)
        {
            var a = pool.Area;
            Color color = s.Storm ? pool.Flash > 0 ? new Color(154, 224, 255, 240) : new Color(42, 117, 167, 230) : new Color(46, 88, 102, 210);
            Box(a.Center + Vector3.UnitY * .015f, new(a.W, .025f, a.D), color, false);
            float border = .065f;
            Color edge = s.Storm ? Blue : new Color(70, 131, 147, 255);
            Box(new(a.X, .04f, a.Z - a.D / 2), new(a.W, border, border), edge, false);
            Box(new(a.X, .04f, a.Z + a.D / 2), new(a.W, border, border), edge, false);
            Box(new(a.X - a.W / 2, .04f, a.Z), new(border, border, a.D), edge, false);
            Box(new(a.X + a.W / 2, .04f, a.Z), new(border, border, a.D), edge, false);
            if (s.Storm)
                for (int j = 0; j < 3; j++)
                {
                    float z = a.Z - a.D / 2 + ((s.Clock * .7f + j * 1.7f) % a.D);
                    Raylib.DrawLine3D(new(a.X - a.W / 2, .05f, z), new(a.X + a.W / 2, .05f, z), Fade(Blue, .45f));
                }
        }
        foreach (var cover in s.World.Covers)
        {
            var a = cover.Area;
            Box(a.Center + Vector3.UnitY * (cover.Height / 2), new(a.W, cover.Height, a.D), new Color(89, 105, 114, 255));
            Box(a.Center + Vector3.UnitY * (cover.Height + .04f), new(a.W + .12f, .08f, a.D + .12f), new Color(119, 137, 143, 255));
            Box(a.Center + new Vector3(0, .55f, -a.D / 2 - .025f), new(a.W * .7f, .12f, .04f), Fade(Gold, .75f), false);
        }
        foreach (var e in s.Enemies) if (e.Alive) DrawEnemy(e);
        foreach (var hazard in s.Hazards)
        {
            Color c = hazard.Pool >= 0 ? Gold : Red;
            Ring(hazard.Pos + Vector3.UnitY * .07f, hazard.Radius, c);
            Ring(hazard.Pos + Vector3.UnitY * .075f, hazard.Radius * (1 - hazard.Remaining / hazard.Duration), Fade(c, .9f));
            Raylib.DrawLine3D(hazard.Pos + new Vector3(-.5f, .08f, 0), hazard.Pos + new Vector3(.5f, .08f, 0), c);
            Raylib.DrawLine3D(hazard.Pos + new Vector3(0, .08f, -.5f), hazard.Pos + new Vector3(0, .08f, .5f), c);
        }
    }
    void DrawPlayer(Vector3 p, float alpha)
    {
        Color body = Fade(s.Invulnerable > 0 ? White : Teal, alpha);
        float bob = s.DashTime > 0 ? 0 : MathF.Sin(s.Clock * 5) * .02f;
        Raylib.DrawCylinder(p + Vector3.UnitY * .025f, .55f, .55f, .015f, 20, new Color(5, 17, 24, (int)(alpha * 120)));
        Raylib.DrawCapsule(p + Vector3.UnitY * (.45f + bob), p + Vector3.UnitY * (1.2f + bob), .32f, 4, 8, body);
        Box(p + Vector3.UnitY * 1.52f, new(.45f, .38f, .42f), Fade(White, alpha), false);
        Vector3 face = s.Facing;
        Raylib.DrawSphereEx(p + Vector3.UnitY * 1.53f + face * .24f, .11f, 4, 6, new Color(23, 63, 76, (int)(alpha * 255)));
        Vector3 hand = p + Vector3.UnitY * 1.0f + new Vector3(face.Z, 0, -face.X) * .37f;
        if (s.Weapon == Weapon.Bolt)
            Raylib.DrawCylinderEx(hand, hand + face * 1.05f, .12f, .08f, 6, Fade(Gold, alpha));
        else
            Raylib.DrawCylinderEx(hand, hand + face * 1.7f + Vector3.UnitY * .3f, .09f, .02f, 4, Fade(s.Build == Build.Impact ? Gold : Blue, alpha));
        if (s.Shield > 0)
            Raylib.DrawSphereWires(p + Vector3.UnitY * .9f, 1.05f, 5, 12, Fade(Gold, .65f));
        Ring(p + Vector3.UnitY * .045f, .72f, Fade(Teal, alpha * .6f));
    }
    void DrawEnemy(Enemy e)
    {
        Color color = e.HitFlash > 0 ? White : e.Kind == EnemyKind.Brute ? new Color(228, 130, 85, 255) : e.Kind == EnemyKind.Shooter ? new Color(187, 139, 243, 255) : Red;
        if (e.Room != s.CurrentRoom) color = new Color(color.R / 2, color.G / 2, color.B / 2, 255);
        float height = e.Kind == EnemyKind.Brute ? 2.4f : 1.25f;
        Raylib.DrawCylinder(e.Pos + Vector3.UnitY * .03f, e.Radius + .15f, e.Radius + .15f, .025f, 16, new Color(15, 21, 28, 150));
        if (e.Kind == EnemyKind.Shooter)
        {
            float bob = MathF.Sin(s.Clock * 3 + e.Pos.X) * .15f;
            Raylib.DrawSphereEx(e.Pos + Vector3.UnitY * (1.1f + bob), .64f, 4, 4, color);
            Raylib.DrawCylinderEx(e.Pos + Vector3.UnitY * .9f, e.Pos + Vector3.UnitY * 1.9f, .12f, .03f, 5, Gold);
        }
        else
        {
            Box(e.Pos + Vector3.UnitY * (height * .5f + .2f), new(e.Radius * 1.7f, height, e.Radius * 1.7f), color);
            Box(e.Pos + Vector3.UnitY * (height + .35f), new(e.Radius, .3f, e.Radius), new Color(61, 46, 53, 255));
        }
        if (e.Windup > 0 && e.Room == s.CurrentRoom)
        {
            if (e.Kind == EnemyKind.Shooter)
            {
                Raylib.DrawLine3D(e.Pos + Vector3.UnitY, e.AttackTarget + Vector3.UnitY, Gold);
                Ring(e.AttackTarget + Vector3.UnitY * .05f, .7f, Gold);
            }
            else
            {
                float max = e.Kind == EnemyKind.Brute ? 1.35f : .6f;
                float radius = e.Kind == EnemyKind.Brute ? 5.8f : 2.4f;
                Ring(e.Pos + Vector3.UnitY * .08f, radius, Fade(Red, .8f));
                Ring(e.Pos + Vector3.UnitY * .08f, radius * (1 - e.Windup / max), Red);
            }
        }
    }
    void DrawEffects()
    {
        foreach (var f in s.Fields)
        {
            Ring(f.Pos + Vector3.UnitY * .05f, 6, Fade(Blue, .7f));
            for (int i = 0; i < 3; i++) Ring(f.Pos + Vector3.UnitY * (.2f + i * .2f), (s.Clock * 2 + i * 2) % 6, Fade(Blue, .35f));
            Raylib.DrawSphereEx(f.Pos + Vector3.UnitY * .4f, .25f, 5, 8, Blue);
        }
        foreach (var b in s.Bolts)
        {
            Color color = b.Hostile ? b.Electric ? Gold : Red : s.Build == Build.Circuit ? Blue : Teal;
            Raylib.DrawSphereEx(b.Pos, b.Hostile ? .21f : .13f, 4, 6, color);
            Raylib.DrawCylinderEx(b.Pos, b.Pos - Vector3.Normalize(b.Velocity) * .7f, .035f, .01f, 4, Fade(color, .7f));
        }
        foreach (var f in s.Effects)
        {
            float t = 1 - f.Life / f.MaxLife, a = 1 - t;
            if (f.Kind == 0) Raylib.DrawSphereWires(f.A, .15f + t * f.Size, 3, 6, Fade(Gold, a));
            else if (f.Kind == 1 || f.Kind == 5)
            {
                Ring(f.A + Vector3.UnitY * (.12f + t * .15f), f.Size * t, Fade(f.Kind == 5 ? Teal : s.Build == Build.Impact ? Gold : Blue, a));
                Ring(f.A + Vector3.UnitY * .1f, f.Size * t * .8f, Fade(White, a * .5f));
            }
            else if (f.Kind == 2)
            {
                Vector3 start = f.A;
                for (int i = 1; i <= 5; i++)
                {
                    Vector3 end = Vector3.Lerp(f.A, f.B, i / 5f);
                    if (i < 5) end += new Vector3(MathF.Sin(i * 17) * .3f, MathF.Cos(i * 7) * .2f, MathF.Sin(i * 9) * .3f);
                    Raylib.DrawCylinderEx(start, end, .025f * f.Size, .025f * f.Size, 4, Fade(White, a)); start = end;
                }
            }
            else if (f.Kind == 3)
                Raylib.DrawCapsule(f.A + Vector3.UnitY * .45f, f.A + Vector3.UnitY * 1.2f, .3f, 3, 6, Fade(Teal, a * .18f));
            else if (f.Kind == 4)
            {
                Vector3 forward = Simulation.FlatNormal(f.B, -Vector3.UnitZ);
                float angle = MathF.Atan2(forward.X, forward.Z), span = 1.22f;
                Vector3 last = f.A + new Vector3(MathF.Sin(angle - span) * f.Size, .8f, MathF.Cos(angle - span) * f.Size);
                for (int i = 1; i <= 20; i++)
                {
                    float v = angle - span + span * 2 * i / 20;
                    Vector3 next = f.A + new Vector3(MathF.Sin(v) * f.Size, .8f, MathF.Cos(v) * f.Size);
                    Raylib.DrawCylinderEx(last, next, .045f, .045f, 4, Fade(Gold, a)); last = next;
                }
            }
        }
    }
    static void Ring(Vector3 pos, float radius, Color color)
    {
        for (int i = 0; i < 48; i++)
        {
            float a = i * MathF.Tau / 48, b = (i + 1) * MathF.Tau / 48;
            Raylib.DrawLine3D(pos + new Vector3(MathF.Sin(a) * radius, 0, MathF.Cos(a) * radius), pos + new Vector3(MathF.Sin(b) * radius, 0, MathF.Cos(b) * radius), color);
        }
    }
    void DrawRain()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        for (int i = 0; i < 65; i++)
        {
            float x = (i * 173.3f + s.Clock * 55) % w;
            float y = (i * 97.1f + s.Clock * 620) % h;
            Raylib.DrawLineEx(new(x, y), new(x - 7, y + 24), 1, new Color(139, 183, 220, 70));
        }
    }
    bool Project(Vector3 p, out Vector2 screen)
    {
        screen = default;
        if (Vector3.Dot(p - camera.Position, camera.Target - camera.Position) <= 0) return false;
        screen = Raylib.GetWorldToScreen(p, camera);
        return screen.X > -100 && screen.X < Raylib.GetScreenWidth() + 100 && screen.Y > 60 && screen.Y < Raylib.GetScreenHeight() - 100;
    }
    void DrawWorldLabels()
    {
        if (s.InHub)
        {
            if (Project(new(0, 5.7f, -8), out var p)) Text("F  前往试场", p.X - 64, p.Y, 21, Teal);
            return;
        }
        foreach (var room in s.World.Rooms)
        {
            if (Vector3.Distance(room.Relay, s.Player) > 45 || !Project(room.Relay + Vector3.UnitY * 4.2f, out var p)) continue;
            string label = s.Progress.Relays[room.Index] ? "已修复" : Vector3.Distance(room.Relay, s.Player) < 3.6f ? "F  激活中继" : "中继 " + (room.Index + 1);
            Text(label, p.X - 40, p.Y, 18, s.Progress.Relays[room.Index] ? Teal : Gold);
        }
        foreach (var e in s.Enemies)
        {
            if (!e.Alive || e.Room != s.CurrentRoom || !Project(e.Pos + Vector3.UnitY * (e.Kind == EnemyKind.Brute ? 3.2f : 2.3f), out var p)) continue;
            float w = e.Kind == EnemyKind.Brute ? 100 : 48;
            Raylib.DrawRectangle((int)(p.X - w / 2), (int)p.Y, (int)w, 5, new Color(14, 22, 31, 220));
            Raylib.DrawRectangle((int)(p.X - w / 2), (int)p.Y, (int)(w * Math.Max(0, e.Health) / e.MaxHealth), 5, e.Kind == EnemyKind.Brute ? Gold : Red);
            if (e.Kind == EnemyKind.Brute) Text("重装守卫", p.X - 36, p.Y - 21, 16, Gold);
        }
    }
    void DrawHud(bool tactical)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, 62, new Color(10, 20, 30, 245));
        Raylib.DrawRectangle(0, 61, w, 1, new Color(54, 87, 99, 255));
        Text("STORMFIELD", 25, 18, 23, Teal);
        Text("风暴试场 / 几何原型", 215, 21, 18, White);
        Text(s.InHub ? "SANCTUARY / 安全区" : "SECTOR 0" + (s.CurrentRoom + 1) + " / 试验区", w / 2f - 115, 22, 18, Muted);
        Text("F1 帮助   Esc 暂停", w - 210, 22, 17, Muted);

        Panel(24, 84, 304, s.InHub ? 232 : 166);
        Text(s.InHub ? "准备下一次探索" : "恢复地区中继", 42, 101, 22, White);
        if (s.InHub)
        {
            Text("F  出发", 42, 141, 28, Teal);
            Text("1 / 2 切换武器 · Tab 切换构筑", 42, 186, 17, Muted);
            Text("N 生成新的区域组合", 42, 215, 17, Muted);
            Text("修复进展  " + s.Progress.Relays.Count(x => x) + " / 3", 42, 252, 18, Gold);
            Text("累计完成  " + s.Progress.Victories + " 次", 42, 278, 17, Muted);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                Color color = s.Progress.Relays[i] ? Teal : i == s.CurrentRoom ? Gold : Muted;
                Raylib.DrawRectangle(43, 146 + i * 28, 8, 8, color);
                string[] names = ["入口平台", "导电中庭", "守卫平台"];
                Text("0" + (i + 1) + "  " + names[i] + (s.Progress.Relays[i] ? "  已修复" : ""), 64, 140 + i * 28, 17, color);
            }
        }
        Text("布局种子 " + s.Progress.Seed, 29, s.InHub ? 330 : 263, 15, Muted);
        if (!s.InHub)
        {
            int remaining = s.Enemies.Count(e => e.Alive && e.Room == s.CurrentRoom);
            Text("本区剩余 " + remaining + "    用时 " + FormatTime(s.EncounterTime), 29, 287, 16, Muted);
        }

        Panel(w - 327, 84, 303, 128);
        Text("当前装备", w - 309, 100, 15, Muted);
        Text(s.WeaponName + " / " + s.BuildName, w - 309, 123, 23, s.Build == Build.Impact ? Gold : Blue);
        Text(s.Build == Build.Impact ? "直击更强 · Q 提供短时护盾" : "攻击连锁 · 湿地扩大传导", w - 309, 158, 17, White);
        Text("1 / 2 武器    Tab 构筑", w - 309, 185, 15, Muted);
        if (!s.InHub) DrawMinimap(w - 240, 227, 216, 176);

        if (!s.InHub)
        {
            float x = w / 2f - 160;
            Panel(x, 78, 320, 77, s.Storm ? new Color(21, 47, 72, 242) : new Color(20, 39, 48, 240));
            string weather = s.Storm ? "风暴 / 湿地导电" : "晴天 / 地表稳定";
            Text(weather, x + 17, 91, 21, s.Storm ? Blue : Teal);
            string mode = s.ManualWeather ? "已固定 · T 切换 / F2 自动" : "变化倒计时 " + Math.Max(0, (int)(s.WeatherDuration - s.WeatherTime)) + " 秒 · T 手动";
            Text(mode, x + 17, 123, 15, Muted);
            if (s.Storm) Text("导电同时伤害敌我，注意干燥站位", w / 2f - 145, 169, 16, Gold);
        }

        Panel(24, h - 132, 304, 96);
        Text("生命", 42, h - 118, 16, Muted);
        Text(((int)s.Health).ToString("000") + " / 100", 204, h - 118, 19, White);
        Raylib.DrawRectangle(42, h - 88, 266, 10, new Color(45, 57, 67, 255));
        Raylib.DrawRectangle(42, h - 88, (int)(266 * s.Health / 100), 10, s.Health < 30 ? Red : Teal);
        Text(s.Shield > 0 ? "护盾 " + (int)s.Shield : s.InHub ? "安全区内生命恢复" : "闪避时短暂无敌", 42, h - 65, 15, s.Shield > 0 ? Gold : Muted);

        float skillX = w / 2f - 201;
        Skill(skillX, h - 115, "SPACE", "闪避 " + s.DashCharges + "/2", s.DashCharges > 0 ? 0 : s.Config.DashRecharge - s.DashRegen, s.Config.DashRecharge, Teal);
        Skill(skillX + 138, h - 115, "Q", "冲击波", s.PulseCooldown, 6, Gold);
        Skill(skillX + 276, h - 115, "E", "引力场", s.FieldCooldown, 10, Blue);
        Text("鼠标左键 攻击", w - 197, h - 104, 19, White);
        Text(tactical ? "V 近景镜头" : "V 高位镜头", w - 197, h - 74, 16, Muted);
        if (s.GodMode || s.SlowMotion < 1) Text((s.GodMode ? "无敌测试  " : "") + (s.SlowMotion < 1 ? "慢动作" : ""), w - 240, h - 155, 17, Gold);
        Text("WASD 移动   鼠标 镜头   滚轮 距离   F 交互   H 据点   R 重试", 26, h - 23, 15, Muted);

        if (s.ToastTime > 0)
        {
            Vector2 size = Raylib.MeasureTextEx(font, s.Toast, 19, .5f);
            float width = Math.Min(w - 60, size.X + 42);
            Panel(w / 2f - width / 2, h - 179, width, 43, new Color(12, 29, 39, 242));
            Text(s.Toast, w / 2f - size.X / 2, h - 168, 19, White);
        }
        int cx = w / 2, cy = h / 2;
        Color reticle = s.Weapon == Weapon.Blade ? Gold : Teal;
        Raylib.DrawCircleLines(cx, cy, 5, Fade(reticle, .9f));
        Raylib.DrawLine(cx - 13, cy, cx - 8, cy, reticle); Raylib.DrawLine(cx + 8, cy, cx + 13, cy, reticle);
        Raylib.DrawLine(cx, cy - 13, cx, cy - 8, reticle); Raylib.DrawLine(cx, cy + 8, cx, cy + 13, reticle);
    }
    void Skill(float x, float y, string key, string title, float cooldown, float maximum, Color color)
    {
        Panel(x, y, 126, 79);
        Text(key, x + 12, y + 10, 16, cooldown <= 0 ? color : Muted);
        Text(cooldown > 0 ? Math.Max(0, cooldown).ToString("0.0") + "s" : title, x + 12, y + 39, 19, cooldown <= 0 ? White : Muted);
        Raylib.DrawRectangle((int)x + 1, (int)y + 76, (int)(124 * (1 - Math.Clamp(cooldown / maximum, 0, 1))), 3, color);
    }
    void DrawMinimap(float x, float y, float width, float height)
    {
        Panel(x, y, width, height);
        Text("地区连接", x + 13, y + 10, 15, Muted);
        float minX = s.World.Rooms.Min(r => r.Center.X) - 15, maxX = s.World.Rooms.Max(r => r.Center.X) + 15;
        float minZ = -49, maxZ = 15;
        float scale = Math.Min((width - 28) / (maxX - minX), (height - 43) / (maxZ - minZ));
        Vector2 Map(Vector3 p) => new(x + 14 + (p.X - minX) * scale, y + 31 + (p.Z - minZ) * scale);
        foreach (var a in s.World.Floors)
        {
            Vector2 p = Map(new(a.X - a.W / 2, 0, a.Z - a.D / 2));
            Raylib.DrawRectangleV(p, new(a.W * scale, a.D * scale), new Color(48, 70, 84, 255));
        }
        foreach (var room in s.World.Rooms)
        {
            Vector2 p = Map(room.Relay);
            Raylib.DrawCircleV(p, 3.5f, s.Progress.Relays[room.Index] ? Teal : Gold);
        }
        foreach (var e in s.Enemies) if (e.Alive && e.Room == s.CurrentRoom) Raylib.DrawCircleV(Map(e.Pos), 2, Red);
        Raylib.DrawCircleV(Map(s.Player), 4, White);
        Raylib.DrawLineEx(Map(s.Player), Map(s.Player + s.Facing * 4), 1.5f, Teal);
    }
    static string FormatTime(float t) => ((int)t / 60).ToString("00") + ":" + ((int)t % 60).ToString("00");
    public void Dispose() { if (customFont) Raylib.UnloadFont(font); }
}
