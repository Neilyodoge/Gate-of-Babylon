using System.Numerics;
using System.Text.Json;

namespace Stormfield;

public sealed class Tuning
{
    public float MoveSpeed { get; set; } = 7.8f;
    public float DashSpeed { get; set; } = 25;
    public float DashDuration { get; set; } = .21f;
    public float DashRecharge { get; set; } = 1.5f;
    public float MouseSensitivity { get; set; } = .0025f;
    public float ProjectileDamage { get; set; } = 18;
    public float MeleeDamage { get; set; } = 34;
    public float EnemyDamageScale { get; set; } = 1;
    public float ClearDuration { get; set; } = 36;
    public float StormDuration { get; set; } = 30;
    public void Validate()
    {
        MoveSpeed = Bound(MoveSpeed, 3, 16, 7.8f); DashSpeed = Bound(DashSpeed, 10, 45, 25);
        DashDuration = Bound(DashDuration, .08f, .5f, .21f); DashRecharge = Bound(DashRecharge, .3f, 5, 1.5f);
        MouseSensitivity = Bound(MouseSensitivity, .0003f, .01f, .0025f);
        ProjectileDamage = Bound(ProjectileDamage, 1, 150, 18); MeleeDamage = Bound(MeleeDamage, 1, 200, 34);
        EnemyDamageScale = Bound(EnemyDamageScale, .1f, 3, 1);
        ClearDuration = Bound(ClearDuration, 5, 300, 36); StormDuration = Bound(StormDuration, 5, 300, 30);
    }
    static float Bound(float n, float min, float max, float fallback) => float.IsFinite(n) ? Math.Clamp(n, min, max) : fallback;
}

public sealed class Progress
{
    public int Version { get; set; } = 1;
    public int Seed { get; set; } = 1026;
    public bool[] Relays { get; set; } = new bool[3];
    public int Victories { get; set; }
    public float BestSeconds { get; set; }
    public bool CircuitUnlocked { get; set; }
    public void Validate()
    {
        if (Relays is null || Relays.Length != 3) Relays = new bool[3];
        Victories = Math.Max(0, Victories);
        if (!float.IsFinite(BestSeconds) || BestSeconds < 0) BestSeconds = 0;
    }
}

public sealed class SaveStore(string path)
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string LastError { get; private set; } = "";
    bool corrupt;
    public Progress Load()
    {
        if (!File.Exists(path)) return new();
        try
        {
            var value = JsonSerializer.Deserialize<Progress>(File.ReadAllText(path)) ?? throw new JsonException("Empty save");
            value.Validate(); return value;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { LastError = e.Message; corrupt = true; return new(); }
    }
    public bool Save(Progress progress)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (corrupt && File.Exists(path))
            {
                File.Copy(path, path + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
                corrupt = false;
            }
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(progress, JsonOptions));
            File.Move(path + ".tmp", path, true); LastError = ""; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { LastError = e.Message; return false; }
    }
}

public readonly record struct Area(float X, float Z, float W, float D)
{
    public Vector3 Center => new(X, 0, Z);
    public bool Contains(Vector3 p, float inset = 0) => MathF.Abs(p.X - X) <= W / 2 - inset && MathF.Abs(p.Z - Z) <= D / 2 - inset;
    public bool IntersectsCircle(Vector3 p, float radius)
    {
        var x = Math.Clamp(p.X, X - W / 2, X + W / 2); var z = Math.Clamp(p.Z, Z - D / 2, Z + D / 2);
        return (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z) < radius * radius;
    }
}

public sealed class Room(int index, Vector3 center, int variant)
{
    public int Index = index;
    public Vector3 Center = center;
    public int Variant = variant;
    public Vector3 Relay => Center + new Vector3(0, 0, -8.5f);
}
public sealed class Cover(Area area, float height) { public Area Area = area; public float Height = height; }
public sealed class Pool(Area area, int room) { public Area Area = area; public int Room = room; public float Flash; public float Cooldown; }

public sealed class World
{
    public readonly List<Room> Rooms = [];
    public readonly List<Area> Floors = [];
    public readonly List<Cover> Covers = [];
    public readonly List<Pool> Pools = [];
    public World(int seed)
    {
        var random = new Random(seed);
        int side = random.Next(2) == 0 ? -1 : 1;
        Vector3[] centers = [Vector3.Zero, new(0, 0, -34), new(side * 34, 0, -34)];
        for (int i = 0; i < 3; i++)
        {
            Vector3 c = centers[i]; int v = random.Next(3);
            Rooms.Add(new(i, c, v)); Floors.Add(new(c.X, c.Z, 27, 27));
            // Hand-authored cover patterns; all share safe entry, relay and corridor sockets.
            (float x, float z)[] pattern = v switch
            {
                0 => [(-7, -4), (7, 4), (5, -6)],
                1 => [(-7, 4), (7, -4), (-4, -6)],
                _ => [(-8, -2), (8, -2), (0, 5)]
            };
            foreach (var p in pattern) Covers.Add(new(new(c.X + p.x, c.Z + p.z, 2.8f, 2.8f), 1.8f));
            Pools.Add(new(new(c.X - 4.5f, c.Z + .5f, 5, 7), i));
            Pools.Add(new(new(c.X + 4, c.Z - 5.3f, 5.5f, 4), i));
        }
        Floors.Add(new(0, -17, 7, 11));
        Floors.Add(new(side * 17, -34, 11, 7));
    }
    public int RoomAt(Vector3 p) => Rooms.MinBy(r => Vector3.DistanceSquared(r.Center, p))!.Index;
    public bool Unobstructed(Vector3 from, Vector3 to)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(from, to) / .2f));
        for (int i = 1; i < steps; i++)
            if (Covers.Any(c => c.Area.IntersectsCircle(Vector3.Lerp(from, to, i / (float)steps), .03f))) return false;
        return true;
    }
    public bool Walkable(Vector3 p, float radius, bool hub = false)
    {
        if (hub) return MathF.Abs(p.X) < 11.5f - radius && MathF.Abs(p.Z) < 11.5f - radius;
        // Test footprint against the union, preserving the full width of shared floor edges.
        Vector3[] offsets = [new(radius, 0, 0), new(-radius, 0, 0), new(0, 0, radius), new(0, 0, -radius)];
        foreach (var offset in offsets) if (!Floors.Any(f => f.Contains(p + offset))) return false;
        return !Covers.Any(c => c.Area.IntersectsCircle(p, radius));
    }
    public Vector3 Move(Vector3 p, Vector3 delta, float radius, bool hub = false)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / .16f));
        Vector3 d = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 candidate = p + d;
            if (Walkable(candidate, radius, hub)) { p = candidate; continue; }
            candidate = p + new Vector3(d.X, 0, 0); if (Walkable(candidate, radius, hub)) p = candidate;
            candidate = p + new Vector3(0, 0, d.Z); if (Walkable(candidate, radius, hub)) p = candidate;
        }
        return p;
    }
}

public enum Weapon { Bolt, Blade }
public enum Build { Impact, Circuit }
public enum EnemyKind { Hunter, Shooter, Brute }
public sealed class Enemy(Vector3 pos, EnemyKind kind, int room)
{
    public Vector3 Pos = pos;
    public EnemyKind Kind = kind;
    public int Room = room;
    public float Health = kind == EnemyKind.Brute ? 280 : kind == EnemyKind.Hunter ? 68 : 78;
    public float MaxHealth => Kind == EnemyKind.Brute ? 280 : Kind == EnemyKind.Hunter ? 68 : 78;
    public float Radius => Kind == EnemyKind.Brute ? .9f : .55f;
    public float Cooldown = 1.8f + room * .25f;
    public float Windup;
    public Vector3 AttackTarget;
    public float HitFlash;
    public bool Alive => Health > 0;
}
public sealed class Bolt(Vector3 pos, Vector3 velocity, bool hostile, float damage, bool electric = false)
{
    public Vector3 Pos = pos, Previous = pos, Velocity = velocity;
    public bool Hostile = hostile, Electric = electric, Dead;
    public float Damage = damage, Life = 2;
}
public sealed class Fx(Vector3 a, Vector3 b, int kind, float life, float size = 1)
{
    public Vector3 A = a, B = b;
    public int Kind = kind; // 0 impact, 1 pulse, 2 chain, 3 ghost, 4 slash, 5 heal
    public float Life = life, MaxLife = life, Size = size;
}
public sealed class Hazard(Vector3 pos, float radius, float delay, int pool = -1)
{ public Vector3 Pos = pos; public float Radius = radius, Remaining = delay, Duration = delay; public int Pool = pool; }
public sealed class Field(Vector3 pos) { public Vector3 Pos = pos; public float Life = 5; }

public readonly record struct FrameInput(Vector3 Move, Vector3 Aim, bool Attack = false, bool Dash = false, bool Pulse = false, bool Field = false);

public sealed class Simulation
{
    public readonly Tuning Config;
    public readonly Progress Progress;
    readonly SaveStore? store;
    Random rng;
    public World World;
    public readonly List<Enemy> Enemies = [];
    public readonly List<Bolt> Bolts = [];
    public readonly List<Fx> Effects = [];
    public readonly List<Hazard> Hazards = [];
    public readonly List<Field> Fields = [];
    public Vector3 Player = new(0, 0, 4), Facing = -Vector3.UnitZ, DashDirection;
    public float Health = 100, Shield, ShieldTime, HurtFlash, Invulnerable, DashTime;
    public int DashCharges = 2;
    public float DashRegen, AttackCooldown, PulseCooldown, FieldCooldown;
    public bool InHub = true, Dead, Storm, ManualWeather, GodMode, Won;
    public Weapon Weapon;
    public Build Build;
    public float Clock, WeatherTime, EncounterTime, NextLightning = 5, SlowMotion = 1;
    public int Kills, Dodges, SkillsUsed, EnvironmentHits, HitsTaken;
    public string Toast = "F 出发 · 前往风暴试场";
    public float ToastTime = 7;
    public int CurrentRoom => World.RoomAt(Player);
    public float WeatherDuration => Storm ? Config.StormDuration : Config.ClearDuration;
    public string BuildName => Build == Build.Impact ? "破阵" : "回路";
    public string WeaponName => Weapon == Weapon.Bolt ? "脉冲枪" : "弧光刃";
    public Simulation(Tuning tuning, Progress progress, SaveStore? save = null)
    { Config = tuning; Progress = progress; store = save; World = new(progress.Seed); rng = new(progress.Seed + 31); }
    public void Notify(string text, float duration = 3) { Toast = text; ToastTime = duration; }
    public void Save()
    {
        if (store is not null && !store.Save(Progress)) Notify("存档写入失败，请检查 data 目录权限", 8);
    }
    public void EnterField()
    {
        InHub = false; Dead = false; Won = false; Player = new(0, 0, 10); Facing = -Vector3.UnitZ;
        Health = 100; Shield = 0; DashCharges = 2; DashRegen = DashTime = Invulnerable = 0;
        AttackCooldown = PulseCooldown = FieldCooldown = 0;
        Bolts.Clear(); Effects.Clear(); Fields.Clear(); Hazards.Clear(); Enemies.Clear();
        EncounterTime = 0; Kills = Dodges = SkillsUsed = EnvironmentHits = HitsTaken = 0;
        WeatherTime = 0; NextLightning = 5;
        rng = new(Progress.Seed + 31);
        foreach (var room in World.Rooms)
        {
            if (Progress.Relays[room.Index]) continue;
            Spawn(room.Center + new Vector3(-4, 0, -1), EnemyKind.Hunter, room.Index);
            Spawn(room.Center + new Vector3(5, 0, 2), EnemyKind.Hunter, room.Index);
            Spawn(room.Center + new Vector3(2, 0, -7), EnemyKind.Shooter, room.Index);
            if (room.Index > 0) Spawn(room.Center + new Vector3(-8, 0, -8), EnemyKind.Shooter, room.Index);
            if (room.Index == 2) Spawn(room.Center + new Vector3(0, 0, -3), EnemyKind.Brute, room.Index);
        }
        Notify("清除各区敌人，靠近中继塔按 F 激活 · T 切换风暴", 6);
    }
    void Spawn(Vector3 pos, EnemyKind kind, int room)
    {
        for (int i = 0; i < 30 && !World.Walkable(pos, .95f); i++)
            pos = World.Rooms[room].Center + new Vector3(rng.NextSingle() * 18 - 9, 0, rng.NextSingle() * 16 - 8);
        Enemies.Add(new(pos, kind, room));
    }
    public void ReturnHub()
    {
        InHub = true; Dead = false; Player = new(0, 0, 4); Facing = -Vector3.UnitZ;
        Health = 100; Shield = 0; DashCharges = 2; DashTime = Invulnerable = 0;
        Bolts.Clear(); Enemies.Clear(); Fields.Clear(); Hazards.Clear(); Effects.Clear(); Save();
        Notify("已回到安全区 · 修复进展保留 · F 再次出发", 5);
    }
    public void NewLayout()
    {
        if (!InHub) { Notify("返回安全区后按 N 生成新布局"); return; }
        Progress.Seed = Random.Shared.Next(1, 1_000_000); Progress.Relays = new bool[3];
        World = new(Progress.Seed); Save(); Notify("已生成新布局；通关记录与解锁保留", 5);
    }
    public void ResetTrial()
    {
        Progress.Relays = new bool[3]; Save(); EnterField(); Notify("同种子重新试验 · 中继重置，累计记录保留", 5);
    }
    public void Interact()
    {
        if (InHub) { EnterField(); return; }
        if (Dead) return;
        var room = World.Rooms.FirstOrDefault(r => Vector3.Distance(Player, r.Relay) < 3.6f);
        if (room is null) { Notify("靠近中继塔后按 F；H 可返回安全区"); return; }
        if (Progress.Relays[room.Index]) { Notify("中继已修复，进展已保存"); return; }
        if (Enemies.Any(e => e.Room == room.Index && e.Alive)) { Notify("先清除本区敌人，再激活中继"); return; }
        Progress.Relays[room.Index] = true; Health = Math.Min(100, Health + 35); Progress.CircuitUnlocked = true;
        Effects.Add(new(Player, Player, 5, .8f, 3));
        if (Progress.Relays.All(x => x))
        {
            Won = true; Progress.Victories++;
            if (Progress.BestSeconds == 0 || EncounterTime < Progress.BestSeconds) Progress.BestSeconds = EncounterTime;
            Notify("试场已恢复！H 返回据点 · R 同布局再试 · Tab 对比构筑", 10);
        }
        else Notify("中继已保存，恢复生命 · 沿连接桥前往下一区", 6);
        Save();
    }
    public void ToggleStorm()
    { Storm = !Storm; WeatherTime = 0; ManualWeather = true; Hazards.RemoveAll(h => h.Pool >= 0); NextLightning = 4; Notify(Storm ? "风暴：蓝色湿地导电，会伤害双方！" : "晴天：湿地停止导电", 5); }
    public void Tick(float dt, FrameInput input)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, .05f);
        Clock += dt; ToastTime -= dt; HurtFlash = Math.Max(0, HurtFlash - dt);
        foreach (var fx in Effects) fx.Life -= dt;
        Effects.RemoveAll(f => f.Life <= 0);
        if (Dead) return;
        Facing = FlatNormal(input.Aim, Facing);
        AttackCooldown -= dt; PulseCooldown -= dt; FieldCooldown -= dt; Invulnerable -= dt;
        ShieldTime -= dt; if (ShieldTime <= 0) Shield = 0;
        if (DashCharges < 2)
        {
            DashRegen += dt;
            while (DashRegen >= Config.DashRecharge && DashCharges < 2) { DashRegen -= Config.DashRecharge; DashCharges++; }
        }
        else DashRegen = 0;
        Vector3 move = input.Move; move.Y = 0; if (move.LengthSquared() > 1) move = Vector3.Normalize(move);
        if (input.Dash && DashCharges > 0 && DashTime <= 0)
        { DashCharges--; DashTime = Config.DashDuration; Invulnerable = Config.DashDuration + .04f; DashDirection = FlatNormal(move, Facing); Dodges++; }
        if (DashTime > 0)
        {
            float step = Math.Min(dt, DashTime); Player = World.Move(Player, DashDirection * Config.DashSpeed * step, .4f, InHub);
            DashTime -= dt; Effects.Add(new(Player, Facing, 3, .22f));
        }
        else Player = World.Move(Player, move * Config.MoveSpeed * dt, .4f, InHub);
        if (input.Attack && AttackCooldown <= 0) Attack();
        if (input.Pulse && PulseCooldown <= 0) Pulse();
        if (input.Field && FieldCooldown <= 0)
        { Fields.Add(new(Player + Facing * 3)); FieldCooldown = 10; SkillsUsed++; Notify("引力场：牵引并减速，持续 5 秒"); }
        foreach (var field in Fields) field.Life -= dt; Fields.RemoveAll(f => f.Life <= 0);
        foreach (var pool in World.Pools) { pool.Flash = Math.Max(0, pool.Flash - dt); pool.Cooldown -= dt; }
        if (!InHub)
        {
            if (!Won) EncounterTime += dt;
            WeatherTime += dt;
            if (!ManualWeather && WeatherTime >= WeatherDuration)
            { Storm = !Storm; WeatherTime = 0; Hazards.RemoveAll(h => h.Pool >= 0); NextLightning = 4; Notify(Storm ? "风暴到来：注意湿地与落雷预警" : "天气转晴，导电停止", 4); }
            if (Storm)
            {
                NextLightning -= dt;
                if (NextLightning <= 0)
                {
                    int room = CurrentRoom; var candidates = World.Pools.Select((p, i) => (p, i)).Where(x => x.p.Room == room).ToArray();
                    var chosen = candidates[rng.Next(candidates.Length)];
                    Hazards.Add(new(chosen.p.Area.Center, 3.5f, 1.35f, chosen.i)); NextLightning = 6;
                }
            }
            foreach (var enemy in Enemies) if (enemy.Alive) UpdateEnemy(enemy, dt);
            foreach (var hazard in Hazards.ToArray())
            {
                hazard.Remaining -= dt;
                if (hazard.Remaining > 0) continue;
                if (hazard.Pool >= 0) Discharge(hazard.Pool, 24, true);
                else
                {
                    Effects.Add(new(hazard.Pos, hazard.Pos, 1, .45f, hazard.Radius));
                    if (Vector3.Distance(Player, hazard.Pos) < hazard.Radius + .4f) DamagePlayer(19);
                }
                Hazards.Remove(hazard);
            }
        }
        UpdateBolts(dt);
    }
    void Attack()
    {
        if (Weapon == Weapon.Bolt)
        {
            AttackCooldown = .22f;
            Vector3 direction = Facing;
            // Narrow aim assist makes the greybox useful before investing in a full aiming rig.
            var target = Enemies.Where(e => e.Alive && Vector3.Distance(e.Pos, Player) < 27)
                .Where(e => Vector3.Dot(FlatNormal(e.Pos - Player, Facing), Facing) > .984f)
                .OrderBy(e => Vector3.DistanceSquared(e.Pos, Player)).FirstOrDefault();
            if (target is not null) direction = FlatNormal(target.Pos - Player, Facing);
            Bolts.Add(new(Player + Vector3.UnitY + direction * .7f, direction * 34, false,
                Config.ProjectileDamage * (Build == Build.Impact ? 1.2f : .85f), Build == Build.Circuit));
        }
        else
        {
            AttackCooldown = .46f; Effects.Add(new(Player, Facing, 4, .22f, 3.6f));
            foreach (var enemy in Enemies.ToArray())
            {
                Vector3 delta = enemy.Pos - Player; float distance = delta.Length();
                if (enemy.Alive && distance < 3.6f + enemy.Radius && (distance < 1 || Vector3.Dot(FlatNormal(delta, Facing), Facing) > .30f)
                    && World.Unobstructed(Player, enemy.Pos))
                { Hit(enemy, Config.MeleeDamage * (Build == Build.Impact ? 1.15f : .85f)); if (Build == Build.Circuit) Chain(enemy); }
            }
        }
    }
    void Pulse()
    {
        PulseCooldown = 6; SkillsUsed++;
        float radius = Build == Build.Impact ? 5.3f : 6.8f;
        Effects.Add(new(Player, Player, 1, .55f, radius));
        if (Build == Build.Impact) { Shield = 24; ShieldTime = 3; }
        foreach (var enemy in Enemies.ToArray())
        {
            if (!enemy.Alive || Vector3.Distance(Player, enemy.Pos) > radius + enemy.Radius) continue;
            Hit(enemy, Build == Build.Impact ? 42 : 28);
            enemy.Pos = World.Move(enemy.Pos, FlatNormal(enemy.Pos - Player, Facing) * 2.8f, enemy.Radius);
            if (Build == Build.Circuit) Chain(enemy);
        }
        if (Build == Build.Circuit && Storm)
        {
            for (int i = 0; i < World.Pools.Count; i++)
                if (World.Pools[i].Area.IntersectsCircle(Player, radius)) Discharge(i, 22, false);
        }
    }
    public void Hit(Enemy e, float damage)
    {
        if (!e.Alive) return;
        e.Health -= damage; e.HitFlash = .13f;
        Effects.Add(new(e.Pos + Vector3.UnitY, e.Pos, 0, .18f, .7f));
        if (!e.Alive) { Kills++; Health = Math.Min(100, Health + 3); Effects.Add(new(e.Pos, e.Pos, 1, .3f, 1.4f)); }
    }
    void Chain(Enemy source)
    {
        if (Storm)
        {
            int index = World.Pools.FindIndex(p => p.Area.Contains(source.Pos));
            if (index >= 0) { Discharge(index, 22, false); return; }
        }
        foreach (var target in Enemies.Where(e => e.Alive && e != source && Vector3.Distance(e.Pos, source.Pos) < 4.5f).Take(2).ToArray())
        { Effects.Add(new(source.Pos + Vector3.UnitY, target.Pos + Vector3.UnitY, 2, .25f)); Hit(target, 10); }
    }
    public void Discharge(int index, float damage, bool lightning)
    {
        if (!Storm || InHub || index < 0 || index >= World.Pools.Count) return;
        var pool = World.Pools[index]; if (pool.Cooldown > 0) return;
        pool.Cooldown = .65f; pool.Flash = .4f;
        Effects.Add(new(pool.Area.Center + new Vector3(0, 15, 0), pool.Area.Center, 2, .35f, 2));
        foreach (var enemy in Enemies.Where(e => e.Alive && pool.Area.Contains(e.Pos)).ToArray())
        { Hit(enemy, damage); EnvironmentHits++; Effects.Add(new(pool.Area.Center + Vector3.UnitY * .1f, enemy.Pos + Vector3.UnitY, 2, .3f)); }
        if (pool.Area.Contains(Player)) DamagePlayer(lightning ? 18 : 10);
    }
    public void DamagePlayer(float damage)
    {
        if (Dead || InHub || GodMode || Invulnerable > 0) return;
        damage *= Config.EnemyDamageScale;
        float absorbed = Math.Min(Shield, damage); Shield -= absorbed; Health -= damage - absorbed;
        HurtFlash = .32f; Invulnerable = .32f; HitsTaken++;
        if (Health <= 0) { Health = 0; Dead = true; Notify("倒下了 · R 重试同布局 · H 返回安全区", 100); Save(); }
    }
    void UpdateEnemy(Enemy e, float dt)
    {
        e.HitFlash -= dt;
        if (e.Room != CurrentRoom) { e.Windup = 0; return; }
        float distance = Vector3.Distance(Player, e.Pos);
        e.Cooldown -= dt;
        bool slowed = false;
        foreach (var f in Fields)
        {
            if (Vector3.Distance(f.Pos, e.Pos) > 6) continue;
            slowed = true; e.Pos = World.Move(e.Pos, FlatNormal(f.Pos - e.Pos, Vector3.Zero) * dt * 2.5f, e.Radius);
        }
        if (e.Windup > 0)
        {
            e.Windup -= dt * (slowed ? .7f : 1);
            if (e.Windup <= 0)
            {
                if (e.Kind == EnemyKind.Shooter)
                {
                    Vector3 dir = FlatNormal(e.AttackTarget - e.Pos, -Vector3.UnitZ);
                    Bolts.Add(new(e.Pos + Vector3.UnitY + dir * .8f, dir * 13, true, 11, true));
                }
                else if (e.Kind == EnemyKind.Hunter)
                {
                    Effects.Add(new(e.Pos, e.AttackTarget - e.Pos, 4, .22f, 2.4f));
                    if (Vector3.Distance(Player, e.Pos) < 2.5f) DamagePlayer(13);
                }
                else
                {
                    Hazards.Add(new(e.Pos, 5.8f, .05f));
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = i * MathF.Tau / 10;
                        Vector3 dir = new(MathF.Sin(angle), 0, MathF.Cos(angle));
                        Bolts.Add(new(e.Pos + Vector3.UnitY + dir * 1.1f, dir * 9, true, 10));
                    }
                }
                e.Cooldown = e.Kind == EnemyKind.Brute ? 3.3f : e.Kind == EnemyKind.Shooter ? 2.4f : 1.35f;
            }
            return;
        }
        float range = e.Kind == EnemyKind.Shooter ? 18 : e.Kind == EnemyKind.Brute ? 6.4f : 2.1f;
        if (distance < range && e.Cooldown <= 0)
        {
            e.Windup = e.Kind == EnemyKind.Brute ? 1.35f : e.Kind == EnemyKind.Shooter ? .95f : .6f;
            e.AttackTarget = Player; return;
        }
        float speed = (e.Kind == EnemyKind.Hunter ? 3.4f : e.Kind == EnemyKind.Shooter ? 2.2f : 2.1f) * (slowed ? .4f : 1);
        Vector3 dirMove = FlatNormal(Player - e.Pos, Vector3.Zero);
        if (e.Kind == EnemyKind.Shooter && distance < 8) dirMove = -dirMove;
        else if (e.Kind == EnemyKind.Shooter && distance < 14) dirMove = new Vector3(dirMove.Z, 0, -dirMove.X) * .4f;
        foreach (var other in Enemies)
        {
            if (other == e || !other.Alive) continue;
            Vector3 separate = e.Pos - other.Pos; float d = separate.Length();
            if (d > .01f && d < e.Radius + other.Radius + .4f) dirMove += separate / d * .8f;
        }
        Vector3 before = e.Pos; e.Pos = World.Move(e.Pos, FlatNormal(dirMove, Vector3.Zero) * speed * dt, e.Radius);
        if (Vector3.DistanceSquared(before, e.Pos) < .00001f)
        {
            Vector3 side = new(dirMove.Z, 0, -dirMove.X);
            e.Pos = World.Move(e.Pos, FlatNormal(side, Vector3.UnitX) * speed * dt, e.Radius);
        }
    }
    void UpdateBolts(float dt)
    {
        foreach (var b in Bolts)
        {
            b.Life -= dt; b.Previous = b.Pos;
            int steps = Math.Max(1, (int)MathF.Ceiling(b.Velocity.Length() * dt / .2f));
            Vector3 step = b.Velocity * (dt / steps);
            for (int i = 0; i < steps && !b.Dead; i++)
            {
                b.Pos += step;
                if (!InHub && World.Covers.Any(c => c.Area.IntersectsCircle(b.Pos, .12f))) { b.Dead = true; break; }
                if (b.Hostile)
                {
                    if (Vector3.Distance(new(b.Pos.X, 0, b.Pos.Z), Player) < .55f) { DamagePlayer(b.Damage); b.Dead = true; }
                }
                else
                {
                    foreach (var e in Enemies)
                    {
                        if (!e.Alive || Vector3.Distance(new(b.Pos.X, 0, b.Pos.Z), e.Pos) > e.Radius + .2f) continue;
                        Hit(e, b.Damage); if (b.Electric) Chain(e); b.Dead = true; break;
                    }
                }
            }
        }
        Bolts.RemoveAll(b => b.Dead || b.Life <= 0);
    }
    public static Vector3 FlatNormal(Vector3 value, Vector3 fallback)
    { value.Y = 0; return value.LengthSquared() > .00001f ? Vector3.Normalize(value) : fallback; }
}
