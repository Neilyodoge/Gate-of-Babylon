using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XianTu.LevelDesign
{
    [Flags]
    public enum DistrictMask
    {
        [InspectorName("外环")]
        Outer = 1 << 0,
        [InspectorName("连接区")]
        Transition = 1 << 1,
        [InspectorName("内环")]
        Inner = 1 << 2,
        [InspectorName("全部分区")]
        All = Outer | Transition | Inner
    }

    public enum EnemyCombatCategory
    {
        [InspectorName("近战")]
        Melee = 0,
        [InspectorName("远程")]
        Ranged = 1,
        [InspectorName("法术")]
        Magic = 2
    }

    public enum EnemySpawnKind
    {
        [InspectorName("基础近战")]
        Melee = 0,
        [InspectorName("远程弓手")]
        Ranged = 1,
        [InspectorName("冲锋怪")]
        Charger = 2,
        [InspectorName("法术怪")]
        Mage = 3
    }

    [Flags]
    public enum LevelPhaseMask
    {
        [InspectorName("白昼")]
        Day = 1 << 0,
        [InspectorName("永夜")]
        Night = 1 << 1,
        [InspectorName("昼夜均可")]
        Both = Day | Night,
    }

    [Serializable]
    public sealed class EnemyPoolEntry
    {
        [InspectorName("显示名称")]
        [Tooltip("只用于编辑器显示，建议填写中文，例如“基础近战”。")]
        public string DisplayName = "新怪物";

        [InspectorName("实际怪物类型")]
        [Tooltip("对应当前代码中的敌人实现。Melee=基础近战，Ranged=远程，Charger=冲锋，Mage=法术。")]
        public EnemySpawnKind EnemyKind;

        [InspectorName("战斗分类")]
        [Tooltip("用于计算近战、远程、法术的整体配比，不直接决定具体怪物。")]
        public EnemyCombatCategory Category;

        [InspectorName("威胁成本")]
        [Tooltip("自动组队时占用的预算。越强的怪物成本应越高。")]
        [Min(1)] public int Cost = 1;

        [InspectorName("同类抽取权重")]
        [Tooltip("同一战斗分类中抽到这个怪物的相对概率。")]
        [Min(0)] public int Weight = 100;

        [InspectorName("允许出现的分区")]
        [Tooltip("Outer=外环，Transition=连接区，Inner=内环；可多选。")]
        public DistrictMask AllowedDistricts = DistrictMask.All;

        [InspectorName("允许出现的阶段")]
        [Tooltip("关卡 A 白昼与永夜使用两组怪物；可多选。")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
    }

    [Serializable]
    public sealed class EnemyPopulationPreset
    {
        [InspectorName("预设名称")]
        [Tooltip("用于编辑器辨认，例如“外环普通战斗”。")]
        public string DisplayName = "新区域预设";

        [InspectorName("适用分区")]
        [Tooltip("该预设控制哪个地图分区的小怪数量与配比。")]
        public District District;

        [InspectorName("最小威胁预算")]
        [Tooltip("本房自动组队可使用的最小总成本。")]
        [Min(1)] public int MinBudget = 5;

        [InspectorName("最大威胁预算")]
        [Tooltip("本房自动组队可使用的最大总成本；固定随机种子会在区间内取值。")]
        [Min(1)] public int MaxBudget = 8;

        [InspectorName("最少怪物数量")]
        [Tooltip("预算不足时仍尽量达到这个数量。")]
        [Min(1)] public int MinCount = 3;

        [InspectorName("最多怪物数量")]
        [Tooltip("本房所有波次合计的怪物数量上限。")]
        [Min(1)] public int MaxCount = 6;

        [InspectorName("同时存活上限")]
        [Tooltip("单波最多生成数量；超出的怪物会分配到后续波次。")]
        [Min(1)] public int MaxAlive = 6;

        [InspectorName("近战比例")]
        [Tooltip("近战分类的相对权重，与远程、法术比例共同计算。无需总和等于100。")]
        [Min(0)] public int MeleeRatio = 60;

        [InspectorName("远程比例")]
        [Tooltip("远程分类的相对权重。")]
        [Min(0)] public int RangedRatio = 30;

        [InspectorName("法术比例")]
        [Tooltip("法术分类的相对权重。")]
        [Min(0)] public int MagicRatio = 10;

        [InspectorName("最少波数")]
        [Tooltip("首版建议1；需要增援时可设为2。")]
        [Range(1, 2)] public int MinWaves = 1;

        [InspectorName("最多波数")]
        [Tooltip("首版最多2，表示最多一批增援。")]
        [Range(1, 2)] public int MaxWaves = 1;

        [InspectorName("增援触发剩余比例")]
        [Tooltip("当前波剩余多少百分比时刷下一波；0表示清空后再刷。")]
        [Range(0, 100)] public int ReinforceAtPct;

        [InspectorName("增援延迟（秒）")]
        [Tooltip("满足增援条件后等待多少秒。")]
        [Min(0f)] public float ReinforceDelaySec = 0.75f;
    }

    [Serializable]
    public sealed class BossPoolEntry
    {
        [InspectorName("显示名称")]
        [Tooltip("只用于编辑器显示的中文名称。")]
        public string DisplayName = "新首领";

        [InspectorName("首领编号")]
        [Tooltip("首领的稳定编号，并用于匹配关卡数据库中的首领阶段。")]
        [Min(1)] public int BossID = 1;

        [InspectorName("抽取权重")]
        [Tooltip("满足条件的首领之间按该权重随机。")]
        [Min(0)] public int Weight = 100;

        [InspectorName("允许出现的分区")]
        [Tooltip("首领只能从所属分区的候选池中抽取。")]
        public DistrictMask AllowedDistricts = DistrictMask.All;

        [InspectorName("允许出现的阶段")]
        [Tooltip("用于区分无暮王城的白昼与永夜首领。")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;

        [InspectorName("需要的事件条件")]
        [Tooltip("事件条件表达式。可从剧情事件配置复制对应标记，留空表示没有前置条件。")]
        public string RequiredFlags;

        [InspectorName("排除条件")]
        [Tooltip("事件条件表达式；条件成立时该首领不会进入候选池。留空表示不排除。")]
        public string BlockedFlags;

        [InspectorName("需要的房间标签")]
        [Tooltip("房间标签。留空表示不限制；用于匹配关卡数据库中的房间标签。")]
        public string RequiredRoomTag;
    }

    public sealed class EnemyPopulationPlan
    {
        public readonly List<List<EnemySpawnKind>> Waves = new();
        public int ReinforceAtPct;
        public float ReinforceDelaySec;
        public int TotalCount => Waves.Sum(x => x.Count);
    }

    [CreateAssetMenu(
        fileName = "怪物与首领生成配置",
        menuName = "仙途秘境/关卡/关卡生成配置")]
    public sealed class DungeonLevelAuthoringConfig : ScriptableObject
    {
        private const string ResourcePath = "LevelDesign/怪物与首领生成配置";
        private static DungeonLevelAuthoringConfig _instance;

        [InspectorName("普通小怪池")]
        [Tooltip("登记可被地图自动抽取的小怪。精英怪不属于该池。")]
        public List<EnemyPoolEntry> EnemyPool = new();

        [InspectorName("区域数量与配比")]
        [Tooltip("每个分区配置一条，用于控制总量、预算、近战/远程/法术比例和波次。")]
        public List<EnemyPopulationPreset> PopulationPresets = new();

        [InspectorName("首领随机池")]
        [Tooltip("首领按分区、房间标签和事件条件筛选后，以固定房间随机种子抽取。")]
        public List<BossPoolEntry> BossPool = new();

        public static DungeonLevelAuthoringConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<DungeonLevelAuthoringConfig>(ResourcePath);
                return _instance;
            }
        }

        public EnemyPopulationPlan BuildPopulationPlan(District district, int seed)
        {
            var preset = PopulationPresets.FirstOrDefault(x => x != null && x.District == district);
            if (preset == null)
                throw new InvalidOperationException($"小怪自动生成缺少 {district} 分区预设，Seed={seed}。");

            DistrictMask districtMask = ToMask(district);
            LevelPhaseMask phaseMask = LevelAPhaseRuntime.IsNightMapActive
                ? LevelPhaseMask.Night
                : LevelPhaseMask.Day;
            var pool = EnemyPool
                .Where(x => x != null
                            && x.Cost > 0
                            && x.Weight > 0
                            && (x.AllowedPhases & phaseMask) != 0
                            && (x.AllowedDistricts & districtMask) != 0)
                .ToList();
            if (pool.Count == 0)
                throw new InvalidOperationException(
                    $"小怪池没有可用于 {district}/{phaseMask} 的条目，Seed={seed}。");

            var random = new System.Random(seed);
            int minBudget = Mathf.Max(1, Mathf.Min(preset.MinBudget, preset.MaxBudget));
            int maxBudget = Mathf.Max(minBudget, preset.MaxBudget);
            int remainingBudget = random.Next(minBudget, maxBudget + 1);
            int minCount = Mathf.Max(1, Mathf.Min(preset.MinCount, preset.MaxCount));
            int maxCount = Mathf.Max(minCount, preset.MaxCount);
            int waveCount = random.Next(
                Mathf.Clamp(Mathf.Min(preset.MinWaves, preset.MaxWaves), 1, 2),
                Mathf.Clamp(Mathf.Max(preset.MinWaves, preset.MaxWaves), 1, 2) + 1);
            maxCount = Mathf.Min(maxCount, Mathf.Max(1, preset.MaxAlive) * waveCount);

            var enemies = new List<EnemySpawnKind>();
            while (enemies.Count < maxCount)
            {
                var affordable = pool.Where(x => x.Cost <= remainingBudget).ToList();
                if (affordable.Count == 0)
                {
                    if (enemies.Count >= minCount) break;
                    int cheapest = pool.Min(x => x.Cost);
                    affordable = pool.Where(x => x.Cost == cheapest).ToList();
                }

                EnemyCombatCategory category = RollCategory(preset, affordable, random);
                var categoryPool = affordable.Where(x => x.Category == category).ToList();
                if (categoryPool.Count == 0) categoryPool = affordable;
                var selected = RollWeighted(categoryPool, random);
                enemies.Add(selected.EnemyKind);
                remainingBudget -= selected.Cost;
                if (remainingBudget <= 0 && enemies.Count >= minCount) break;
            }

            var plan = new EnemyPopulationPlan
            {
                ReinforceAtPct = preset.ReinforceAtPct,
                ReinforceDelaySec = preset.ReinforceDelaySec
            };
            for (int i = 0; i < waveCount; i++)
                plan.Waves.Add(new List<EnemySpawnKind>());
            for (int i = 0; i < enemies.Count; i++)
                plan.Waves[i % waveCount].Add(enemies[i]);
            plan.Waves.RemoveAll(x => x.Count == 0);
            return plan;
        }

        public int ResolveBossID(
            District district,
            int seed,
            IReadOnlyList<string> roomTags,
            int fallbackBossID)
        {
            DistrictMask districtMask = ToMask(district);
            LevelPhaseMask phaseMask = LevelAPhaseRuntime.IsNightMapActive
                ? LevelPhaseMask.Night
                : LevelPhaseMask.Day;
            var candidates = BossPool.Where(x =>
                x != null
                && x.BossID > 0
                && x.Weight > 0
                && (x.AllowedPhases & phaseMask) != 0
                && (x.AllowedDistricts & districtMask) != 0
                && (string.IsNullOrWhiteSpace(x.RequiredFlags)
                    || BossFlagSet.Instance.Evaluate(x.RequiredFlags))
                && (string.IsNullOrWhiteSpace(x.BlockedFlags)
                    || !BossFlagSet.Instance.Evaluate(x.BlockedFlags))
                && (string.IsNullOrWhiteSpace(x.RequiredRoomTag)
                    || (roomTags != null && roomTags.Contains(x.RequiredRoomTag))))
                .ToList();
            if (candidates.Count == 0) return fallbackBossID;
            return RollWeighted(candidates, new System.Random(seed)).BossID;
        }

        public static DistrictMask ToMask(District district)
        {
            return district switch
            {
                District.Outer => DistrictMask.Outer,
                District.Transition => DistrictMask.Transition,
                District.Inner => DistrictMask.Inner,
                _ => DistrictMask.All
            };
        }

        public static void ClearCache()
        {
            _instance = null;
        }

        private static EnemyCombatCategory RollCategory(
            EnemyPopulationPreset preset,
            List<EnemyPoolEntry> candidates,
            System.Random random)
        {
            int melee = candidates.Any(x => x.Category == EnemyCombatCategory.Melee)
                ? Mathf.Max(0, preset.MeleeRatio) : 0;
            int ranged = candidates.Any(x => x.Category == EnemyCombatCategory.Ranged)
                ? Mathf.Max(0, preset.RangedRatio) : 0;
            int magic = candidates.Any(x => x.Category == EnemyCombatCategory.Magic)
                ? Mathf.Max(0, preset.MagicRatio) : 0;
            int total = melee + ranged + magic;
            if (total <= 0) return candidates[random.Next(candidates.Count)].Category;
            int roll = random.Next(total);
            if (roll < melee) return EnemyCombatCategory.Melee;
            if (roll < melee + ranged) return EnemyCombatCategory.Ranged;
            return EnemyCombatCategory.Magic;
        }

        private static T RollWeighted<T>(List<T> entries, System.Random random)
        {
            int Weight(T entry)
            {
                return entry switch
                {
                    EnemyPoolEntry enemy => Mathf.Max(0, enemy.Weight),
                    BossPoolEntry boss => Mathf.Max(0, boss.Weight),
                    _ => 1
                };
            }

            int total = entries.Sum(Weight);
            int roll = random.Next(Mathf.Max(1, total));
            foreach (var entry in entries)
            {
                roll -= Weight(entry);
                if (roll < 0) return entry;
            }
            return entries[entries.Count - 1];
        }
    }
}
