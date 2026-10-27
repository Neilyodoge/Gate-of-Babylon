using System.Numerics;

namespace Stormfield;

public static class SelfTests
{
    static int passed;
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("FAILED: " + message); passed++; Console.WriteLine("PASS: " + message); }
    static Simulation Fresh()
    { var s = new Simulation(new Tuning(), new Progress()); s.EnterField(); s.Enemies.Clear(); return s; }
    static FrameInput Idle => new(Vector3.Zero, -Vector3.UnitZ);
    public static int Run()
    {
        try
        {
            var a = new World(1026); var b = new World(1026);
            Check(a.Rooms.Select(r => (r.Center, r.Variant)).SequenceEqual(b.Rooms.Select(r => (r.Center, r.Variant))), "seed recreates authored module layout");
            Check(Enumerable.Range(1, 20).Select(n => string.Join("/", new World(n).Rooms.Select(r => r.Center + ":" + r.Variant))).Distinct().Count() > 4, "seeds produce several constrained layouts");
            Check(a.Walkable(new(0, 0, -17), .4f), "north connector remains traversable");
            Check(a.Walkable((a.Rooms[1].Center + a.Rooms[2].Center) / 2, .4f), "east/west connector remains traversable");
            Vector3 edge = a.Move(new(0, 0, 12), new(0, 0, 20), .4f);
            Check(a.Walkable(edge, .4f) && edge.Z < 13.2f, "large movement cannot leave floor");
            var block = a.Covers[0];
            Vector3 from = block.Area.Center - Vector3.UnitX * 4;
            Vector3 to = a.Move(from, Vector3.UnitX * 8, .4f);
            Check(to.X < block.Area.X - block.Area.W / 2 && a.Walkable(to, .4f), "dash subdivision prevents tunnelling through cover");

            var s = Fresh(); Vector3 start = s.Player;
            s.Tick(.05f, new(new(1, 0, 1), -Vector3.UnitZ));
            Check(MathF.Abs(Vector3.Distance(start, s.Player) - s.Config.MoveSpeed * .05f) < .001f, "diagonal movement has no speed bonus");
            s = Fresh(); s.Tick(.01f, new(Vector3.UnitX, -Vector3.UnitZ, Dash: true));
            Check(s.DashCharges == 1 && s.Invulnerable > 0, "dash consumes one charge and grants invulnerability");
            s.DamagePlayer(20); Check(s.Health == 100, "damage is ignored during dash");
            for (int i = 0; i < 170; i++) s.Tick(.01f, Idle);
            Check(s.DashCharges == 2, "dash charge regenerates");
            s.DamagePlayer(20); Check(s.Health == 80, "damage resumes after dash");

            s = Fresh(); s.Player = s.World.Pools[0].Area.Center;
            var enemy = new Enemy(s.Player + new Vector3(1, 0, 0), EnemyKind.Hunter, 0); s.Enemies.Add(enemy);
            s.Discharge(0, 22, false);
            Check(enemy.Health == enemy.MaxHealth && s.Health == 100, "dry weather disables conduction");
            s.Storm = true; s.Discharge(0, 22, false);
            Check(enemy.Health == enemy.MaxHealth - 22 && s.Health == 90, "storm pool damages enemy and exposed player");
            s.Discharge(0, 22, false);
            Check(enemy.Health == enemy.MaxHealth - 22, "pool cooldown bounds chain feedback");
            s.World.Pools[0].Cooldown = 0; s.Invulnerable = 1; s.Discharge(0, 22, true);
            Check(s.Health == 90, "dodge protects against lightning");

            s = Fresh(); s.World.Covers.Clear(); s.Player = new(0, 0, 8);
            enemy = new(new(0, 0, 4), EnemyKind.Hunter, 0); s.Enemies.Add(enemy);
            s.Bolts.Add(new(new(0, 1, 8), new(0, 0, -150), false, 17));
            s.Tick(.05f, Idle);
            Check(enemy.Health == enemy.MaxHealth - 17, "fast projectile uses swept substeps and hits once");
            s = Fresh(); s.World.Covers.Clear(); s.Player = new(0, 0, 8); s.Weapon = Weapon.Blade;
            var front = new Enemy(new(0, 0, 5.5f), EnemyKind.Hunter, 0);
            var behind = new Enemy(new(0, 0, 10.5f), EnemyKind.Hunter, 0);
            s.Enemies.Add(front); s.Enemies.Add(behind); s.Tick(.01f, new(Vector3.Zero, -Vector3.UnitZ, Attack: true));
            Check(front.Health < front.MaxHealth && behind.Health == behind.MaxHealth, "melee respects the forward attack arc");
            s = Fresh(); s.World.Covers.Clear(); s.Player = new(0, 0, 8); s.Weapon = Weapon.Blade;
            s.World.Covers.Add(new(new(0, 6.6f, 2, .5f), 2));
            enemy = new(new(0, 0, 5.5f), EnemyKind.Hunter, 0); s.Enemies.Add(enemy);
            s.Tick(.01f, new(Vector3.Zero, -Vector3.UnitZ, Attack: true));
            Check(enemy.Health == enemy.MaxHealth, "solid cover blocks normal melee attacks");
            s.Bolts.Add(new(new(0, 1, 8), new(0, 0, -150), false, 17)); s.Tick(.05f, Idle);
            Check(enemy.Health == enemy.MaxHealth, "solid cover blocks high-speed projectiles");
            s = Fresh(); s.Tick(.01f, new(Vector3.Zero, -Vector3.UnitZ, Pulse: true));
            Check(s.PulseCooldown > 0 && s.Shield == 24, "impact pulse creates timed shield and cooldown");
            s.Tick(.01f, new(Vector3.Zero, -Vector3.UnitZ, Pulse: true));
            Check(s.SkillsUsed == 1, "ability cannot bypass cooldown");
            s.Tick(.01f, new(Vector3.Zero, -Vector3.UnitZ, Field: true));
            Check(s.Fields.Count == 1 && s.FieldCooldown > 0, "gravity field has bounded duration and cooldown");

            s = Fresh(); s.Player = s.World.Rooms[0].Relay;
            enemy = new(s.Player + Vector3.UnitX, EnemyKind.Hunter, 0); s.Enemies.Add(enemy); s.Interact();
            Check(!s.Progress.Relays[0], "relay rejects activation while defender lives");
            s.Hit(enemy, 500); s.Interact();
            Check(s.Progress.Relays[0] && s.Progress.CircuitUnlocked, "cleared relay records persistent progress");
            for (int i = 1; i < 3; i++) { s.Player = s.World.Rooms[i].Relay; s.Interact(); }
            Check(s.Won && s.Progress.Victories == 1, "three distinct relays complete a trial");
            s.Interact(); Check(s.Progress.Victories == 1, "repeat relay interaction cannot farm victory");
            s.ReturnHub(); s.EnterField();
            Check(s.Enemies.Count == 0 && s.Progress.Relays.All(v => v), "returning preserves repaired sectors");
            int seed = s.Progress.Seed; s.ResetTrial();
            Check(s.Progress.Seed == seed && s.Progress.Relays.All(v => !v) && s.Enemies.Count > 0 && s.Progress.Victories == 1, "explicit retry keeps layout and lifetime records");
            s.Invulnerable = 0; s.DamagePlayer(1000); Check(s.Dead && s.Health == 0, "lethal damage produces recoverable death state");
            s.ReturnHub(); Check(!s.Dead && s.Health == 100 && s.InHub, "hub recovers player after death");

            string directory = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "progress.json"); var store = new SaveStore(path);
            var progress = new Progress { Seed = 4567, Relays = [true, false, true], Victories = 3, CircuitUnlocked = true };
            bool saved = store.Save(progress);
            Check(saved, "save writes atomically" + (saved ? "" : ": " + store.LastError));
            var loaded = new SaveStore(path).Load();
            Check(loaded.Seed == 4567 && loaded.Relays.SequenceEqual(progress.Relays) && loaded.Victories == 3, "save round-trip preserves layout and progress");
            File.WriteAllText(path, "{bad json"); store = new SaveStore(path); loaded = store.Load();
            Check(loaded.Relays.Length == 3, "corrupt save has safe fallback");
            Check(store.Save(loaded) && Directory.GetFiles(directory, "*.invalid-*").Length == 1, "corrupt original is retained before replacement");
            File.WriteAllText(path, "{\"Relays\":null,\"Victories\":-3}"); loaded = store.Load();
            Check(loaded.Relays.Length == 3 && loaded.Victories == 0, "malformed progress values are normalized");
            var tuning = new Tuning { MoveSpeed = float.NaN, DashRecharge = -2, EnemyDamageScale = 100 };
            tuning.Validate(); Check(float.IsFinite(tuning.MoveSpeed) && tuning.DashRecharge > 0 && tuning.EnemyDamageScale <= 3, "invalid tuning cannot break simulation");
            Console.WriteLine($"{passed} checks passed."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
