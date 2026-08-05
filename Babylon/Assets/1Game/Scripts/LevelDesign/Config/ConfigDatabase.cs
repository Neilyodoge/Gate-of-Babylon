using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// 关卡数据从关卡数据库 Asset 加载，战斗批量配表继续从 Resources/Combat JSON 加载。
    /// 首次访问时建立按 ID 查询的字典，后续查表 O(1)。
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
        public Dictionary<int, RoomContentRow> RoomContents { get; private set; } = new();
        public Dictionary<int, EncounterRow> Encounters { get; private set; } = new();
        public Dictionary<int, StoryEventRow> StoryEvents { get; private set; } = new();
        public Dictionary<int, BossPhaseRow> BossPhases { get; private set; } = new();
        // v0.5.5 战斗配表（GDD §6.9 / §6.9-2）
        public Dictionary<int, SkillBaseRow> SkillBases { get; private set; } = new();
        // V0.1.18d 技能参数仓库表（GDD §6.9-3；按 ConfigId=Skill_Base_Config.ID）
        public Dictionary<int, SkillParamRow> SkillParams { get; private set; } = new();
        // V0.1.14 模块配置主表（GDD §5.7；按 ModuleId 字符串键）
        public Dictionary<string, ModuleBaseRow> Modules { get; private set; } = new();
        // V0.1.18 模块参数仓库表（GDD §5.7；按 ModuleId 字符串键）
        public Dictionary<string, ModuleTriggerParamRow> ModuleTriggerParams { get; private set; } = new();
        public Dictionary<string, ModuleEffectParamRow> ModuleEffectParams { get; private set; } = new();
        public Dictionary<string, ModuleModifierParamRow> ModuleModifierParams { get; private set; } = new();
        public Dictionary<string, ModuleUniversalParamRow> ModuleUniversalParams { get; private set; } = new();
        // V0.1.14 敌人分类基础表（GDD §7.3；按 ID）与消费模型系数表（GDD §5.6；按 ID）
        public Dictionary<int, EnemyBaseRow> Enemies { get; private set; } = new();
        public Dictionary<int, ConsumeKindBonusRow> ConsumeKindBonuses { get; private set; } = new();

        public bool Loaded { get; private set; }

        // ── 战斗配表查询（按 ID，O(1)；查不到返回 null）──
        public SkillBaseRow GetSkillBase(int id) => SkillBases.TryGetValue(id, out var r) ? r : null;
        public SkillParamRow GetSkillParam(int id) => SkillParams.TryGetValue(id, out var r) ? r : null;
        public ModuleBaseRow GetModule(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && Modules.TryGetValue(moduleId, out var r) ? r : null;
        public ModuleTriggerParamRow GetModuleTriggerParam(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && ModuleTriggerParams.TryGetValue(moduleId, out var r) ? r : null;
        public ModuleEffectParamRow GetModuleEffectParam(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && ModuleEffectParams.TryGetValue(moduleId, out var r) ? r : null;
        public ModuleModifierParamRow GetModuleModifierParam(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && ModuleModifierParams.TryGetValue(moduleId, out var r) ? r : null;
        public ModuleUniversalParamRow GetModuleUniversalParam(string moduleId) =>
            !string.IsNullOrEmpty(moduleId) && ModuleUniversalParams.TryGetValue(moduleId, out var r) ? r : null;
        public EnemyBaseRow GetEnemy(int id) => Enemies.TryGetValue(id, out var r) ? r : null;
        public ConsumeKindBonusRow GetConsumeKindBonus(int id) => ConsumeKindBonuses.TryGetValue(id, out var r) ? r : null;
        public RoomContentRow GetRoomContent(int id) => RoomContents.TryGetValue(id, out var r) ? r : null;
        public EncounterRow GetEncounter(int id) => Encounters.TryGetValue(id, out var r) ? r : null;

        private void LoadAll()
        {
            var levelAsset = Resources.Load<LevelDesignAssetDatabase>(
                LevelDesignAssetDatabase.ResourcePath);
            if (levelAsset == null)
                throw new System.InvalidOperationException(
                    $"缺少关卡数据库 Asset：Resources/{LevelDesignAssetDatabase.ResourcePath}.asset");

            MapStructures = BuildDictionary(levelAsset.MapStructures, r => r.ID, "秘境结构");
            RoomContents = BuildDictionary(levelAsset.RoomContents, r => r.ID, "房间内容");
            Encounters = BuildDictionary(levelAsset.Encounters, r => r.ID, "战斗遭遇");
            StoryEvents = BuildDictionary(levelAsset.StoryEvents, r => r.ID, "剧情事件");
            BossPhases = BuildDictionary(levelAsset.BossPhases, r => r.ID, "Boss阶段");
            SkillBases = LoadTable<SkillBaseTable, SkillBaseRow>(
                "Combat/Skill_Base_Config", t => t.Rows, r => r.ID);
            SkillParams = LoadTable<SkillParamTable, SkillParamRow>(
                "Combat/Skill_Param_Config", t => t.Rows, r => r.ConfigId);
            Modules = LoadTableStr<ModuleBaseTable, ModuleBaseRow>(
                "Combat/Module_Base_Config", t => t.Rows, r => r.ModuleId);
            ModuleTriggerParams = LoadTableStr<ModuleTriggerParamTable, ModuleTriggerParamRow>(
                "Combat/Module_Trigger_Param_Config", t => t.Rows, r => r.ModuleId);
            ModuleEffectParams = LoadTableStr<ModuleEffectParamTable, ModuleEffectParamRow>(
                "Combat/Module_Effect_Param_Config", t => t.Rows, r => r.ModuleId);
            ModuleModifierParams = LoadTableStr<ModuleModifierParamTable, ModuleModifierParamRow>(
                "Combat/Module_Modifier_Param_Config", t => t.Rows, r => r.ModuleId);
            ModuleUniversalParams = LoadTableStr<ModuleUniversalParamTable, ModuleUniversalParamRow>(
                "Combat/Module_Universal_Param_Config", t => t.Rows, r => r.ModuleId);
            Enemies = LoadTable<EnemyBaseTable, EnemyBaseRow>(
                "Combat/Enemy_Base_Config", t => t.Rows, r => r.ID);
            ConsumeKindBonuses = LoadTable<ConsumeKindBonusTable, ConsumeKindBonusRow>(
                "Combat/ConsumeKind_Bonus_Config", t => t.Rows, r => r.ID);

            ValidateRoomConfigs();
            Loaded = true;
            Debug.Log($"[ConfigDatabase] 已加载 — 秘境:{MapStructures.Count} " +
                      $"房间内容:{RoomContents.Count} 遭遇:{Encounters.Count} " +
                      $"事件:{StoryEvents.Count} Boss阶段:{BossPhases.Count} " +
                      $"Skills:{SkillBases.Count} SkillParams:{SkillParams.Count} Modules:{Modules.Count} " +
                      $"ModTrig:{ModuleTriggerParams.Count} ModEff:{ModuleEffectParams.Count} " +
                      $"ModMod:{ModuleModifierParams.Count} ModUni:{ModuleUniversalParams.Count} " +
                      $"Enemies:{Enemies.Count} ConsumeKindBonuses:{ConsumeKindBonuses.Count}");
        }

        private static Dictionary<int, TRow> BuildDictionary<TRow>(
            IEnumerable<TRow> rows,
            System.Func<TRow, int> idAccessor,
            string displayName)
            where TRow : class
        {
            var result = new Dictionary<int, TRow>();
            if (rows == null) return result;
            foreach (var row in rows)
            {
                if (row == null) continue;
                int id = idAccessor(row);
                if (result.ContainsKey(id))
                {
                    Debug.LogError($"[ConfigDatabase] {displayName}存在重复编号：{id}。");
                    continue;
                }
                result[id] = row;
            }
            return result;
        }

        private void ValidateRoomConfigs()
        {
            foreach (var pair in RoomContents)
            {
                var row = pair.Value;
                bool validRole = System.Enum.IsDefined(typeof(RoomRole), row.Role);
                bool validDistrict = System.Enum.IsDefined(typeof(District), row.District);
                bool validActivation = System.Enum.IsDefined(typeof(ActivationMode), row.ActivationMode);
                bool validLock = System.Enum.IsDefined(typeof(LockPolicy), row.LockPolicy);
                if (!validRole || !validDistrict || !validActivation || !validLock)
                    Debug.LogError($"[ConfigDatabase] RoomContent={row.ID} 含非法枚举值。");

                bool combat = row.RoleEnum == RoomRole.Battle
                              || row.RoleEnum == RoomRole.Elite
                              || row.RoleEnum == RoomRole.Boss;
                if (combat && !Encounters.ContainsKey(row.ContentConfigID))
                    Debug.LogError(
                        $"[ConfigDatabase] RoomContent={row.ID} 引用缺失 Encounter={row.ContentConfigID}。");
                if (row.RoleEnum == RoomRole.Event && row.EventID > 0
                    && !StoryEvents.ContainsKey(row.EventID))
                    Debug.LogError(
                        $"[ConfigDatabase] RoomContent={row.ID} 引用缺失 Event={row.EventID}。");
            }

            foreach (var pair in Encounters)
            {
                var row = pair.Value;
                if (!System.Enum.IsDefined(typeof(SpawnMode), row.SpawnMode))
                    Debug.LogError($"[ConfigDatabase] Encounter={row.ID} 的 SpawnMode={row.SpawnMode} 非法。");
            }
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
