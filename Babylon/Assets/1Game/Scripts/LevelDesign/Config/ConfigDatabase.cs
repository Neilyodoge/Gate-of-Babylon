using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.6 任务 1：JSON 表格解析框架。
    /// 单例 ConfigDatabase 在首次访问时自动从 Resources/LevelDesign/ 加载所有表格，
    /// 后续按 ID 查表 O(1)。
    ///
    /// JSON 文件命名约定（Resources 不带后缀）：
    ///   Map_Structure_Config.json
    ///   Room_Socket_Group_Config.json
    ///   Event_Story_Config.json
    ///   Boss_Phase_Config.json
    ///   Item_InRun_Config.json
    ///   Material_CaveRes_Config.json
    ///
    /// 每个 JSON 文件根对象为 { "Rows": [ ... ] }，符合 Unity JsonUtility 限制。
    /// </summary>
    public class ConfigDatabase
    {
        private static ConfigDatabase _instance;
        public static ConfigDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigDatabase();
                    _instance.LoadAll();
                }
                return _instance;
            }
        }

        public Dictionary<int, MapStructureRow> MapStructures { get; private set; } = new();
        public Dictionary<int, RoomSocketRow> RoomSockets { get; private set; } = new();
        public Dictionary<int, StoryEventRow> StoryEvents { get; private set; } = new();
        public Dictionary<int, BossPhaseRow> BossPhases { get; private set; } = new();
        public Dictionary<int, ItemInRunRow> ItemsInRun { get; private set; } = new();
        public Dictionary<int, MaterialCaveResRow> CaveMaterials { get; private set; } = new();
        // v0.5.5 战斗配表（GDD §6.9 / §6.9-2）
        public Dictionary<int, SkillBaseRow> SkillBases { get; private set; } = new();
        public Dictionary<int, SkillEffectRow> SkillEffects { get; private set; } = new();
        // V0.1.14 模块配置主表（GDD §5.7；按 ModuleId 字符串键）
        public Dictionary<string, ModuleBaseRow> Modules { get; private set; } = new();
        // V0.1.14 敌人分类基础表（GDD §7.3；按 ID）与消费模型系数表（GDD §5.6；按 ID）
        public Dictionary<int, EnemyBaseRow> Enemies { get; private set; } = new();
        public Dictionary<int, ConsumeKindBonusRow> ConsumeKindBonuses { get; private set; } = new();

        public bool Loaded { get; private set; }

        // ── 战斗配表查询（按 ID，O(1)；查不到返回 null）──
        public SkillBaseRow GetSkillBase(int id) => SkillBases.TryGetValue(id, out var r) ? r : null;
        public SkillEffectRow GetSkillEffect(int id) => SkillEffects.TryGetValue(id, out var r) ? r : null;
        public ItemInRunRow GetItem(int id) => ItemsInRun.TryGetValue(id, out var r) ? r : null;
        public ModuleBaseRow GetModule(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && Modules.TryGetValue(moduleId, out var r) ? r : null;
        public EnemyBaseRow GetEnemy(int id) => Enemies.TryGetValue(id, out var r) ? r : null;
        public ConsumeKindBonusRow GetConsumeKindBonus(int id) => ConsumeKindBonuses.TryGetValue(id, out var r) ? r : null;

        private void LoadAll()
        {
            MapStructures = LoadTable<MapStructureTable, MapStructureRow>(
                "LevelDesign/Map_Structure_Config", t => t.Rows, r => r.ID);
            RoomSockets = LoadTable<RoomSocketTable, RoomSocketRow>(
                "LevelDesign/Room_Socket_Group_Config", t => t.Rows, r => r.ID);
            StoryEvents = LoadTable<StoryEventTable, StoryEventRow>(
                "LevelDesign/Event_Story_Config", t => t.Rows, r => r.ID);
            BossPhases = LoadTable<BossPhaseTable, BossPhaseRow>(
                "LevelDesign/Boss_Phase_Config", t => t.Rows, r => r.ID);
            ItemsInRun = LoadTable<ItemInRunTable, ItemInRunRow>(
                "LevelDesign/Item_InRun_Config", t => t.Rows, r => r.ID);
            CaveMaterials = LoadTable<MaterialCaveResTable, MaterialCaveResRow>(
                "LevelDesign/Material_CaveRes_Config", t => t.Rows, r => r.ID);
            SkillBases = LoadTable<SkillBaseTable, SkillBaseRow>(
                "LevelDesign/Skill_Base_Config", t => t.Rows, r => r.ID);
            SkillEffects = LoadTable<SkillEffectTable, SkillEffectRow>(
                "LevelDesign/Skill_Effect_Config", t => t.Rows, r => r.ID);
            Modules = LoadTableStr<ModuleBaseTable, ModuleBaseRow>(
                "Combat/Module_Base_Config", t => t.Rows, r => r.ModuleId);
            Enemies = LoadTable<EnemyBaseTable, EnemyBaseRow>(
                "Combat/Enemy_Base_Config", t => t.Rows, r => r.ID);
            ConsumeKindBonuses = LoadTable<ConsumeKindBonusTable, ConsumeKindBonusRow>(
                "Combat/ConsumeKind_Bonus_Config", t => t.Rows, r => r.ID);

            Loaded = true;
            Debug.Log($"[ConfigDatabase] 已加载 — Maps:{MapStructures.Count} Rooms:{RoomSockets.Count} " +
                      $"Events:{StoryEvents.Count} BossPhases:{BossPhases.Count} " +
                      $"Items:{ItemsInRun.Count} Materials:{CaveMaterials.Count} " +
                      $"Skills:{SkillBases.Count} Effects:{SkillEffects.Count} Modules:{Modules.Count} " +
                      $"Enemies:{Enemies.Count} ConsumeKindBonuses:{ConsumeKindBonuses.Count}");
        }

        private Dictionary<string, TRow> LoadTableStr<TTable, TRow>(
            string resourcePath,
            System.Func<TTable, TRow[]> rowsAccessor,
            System.Func<TRow, string> idAccessor)
        {
            var dict = new Dictionary<string, TRow>();
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[ConfigDatabase] 缺失 {resourcePath}.json，跳过加载。");
                return dict;
            }

            try
            {
                var table = JsonUtility.FromJson<TTable>(asset.text);
                if (table == null) return dict;
                var rows = rowsAccessor(table);
                if (rows == null) return dict;
                foreach (var row in rows)
                {
                    if (row == null) continue;
                    string id = idAccessor(row);
                    if (string.IsNullOrEmpty(id)) continue;
                    if (dict.ContainsKey(id))
                    {
                        Debug.LogWarning($"[ConfigDatabase] {resourcePath} 中 ID={id} 重复，跳过。");
                        continue;
                    }
                    dict[id] = row;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ConfigDatabase] 解析 {resourcePath} 失败：{ex.Message}");
            }

            return dict;
        }

        private Dictionary<int, TRow> LoadTable<TTable, TRow>(
            string resourcePath,
            System.Func<TTable, TRow[]> rowsAccessor,
            System.Func<TRow, int> idAccessor)
        {
            var dict = new Dictionary<int, TRow>();
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[ConfigDatabase] 缺失 {resourcePath}.json，跳过加载。");
                return dict;
            }

            try
            {
                var table = JsonUtility.FromJson<TTable>(asset.text);
                if (table == null) return dict;

                var rows = rowsAccessor(table);
                if (rows == null) return dict;

                foreach (var row in rows)
                {
                    if (row == null) continue;
                    int id = idAccessor(row);
                    if (dict.ContainsKey(id))
                    {
                        Debug.LogWarning($"[ConfigDatabase] {resourcePath} 中 ID={id} 重复，跳过。");
                        continue;
                    }
                    dict[id] = row;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ConfigDatabase] 解析 {resourcePath} 失败：{ex.Message}");
            }

            return dict;
        }

        /// <summary>手动重载（编辑器中热更表格用）</summary>
        public static void Reload()
        {
            _instance = null;
            _ = Instance;
        }
    }
}
