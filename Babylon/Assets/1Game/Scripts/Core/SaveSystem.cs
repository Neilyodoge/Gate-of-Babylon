using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 存档系统 —— 支持 3 个存档槽位 + 自动存档。
    ///
    /// 存档位置：<c>Application.persistentDataPath/save_slot_{0-2}.json</c>
    /// 兼容旧存档：首次启动时如果发现 <c>save_v1.json</c> 则迁移到槽位 0。
    /// </summary>
    public class SaveSystem
    {
        public const int MaxSlots = 3;

        private static SaveSystem _instance;
        public static SaveSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SaveSystem();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private int _activeSlot = -1;
        private SaveDataV1 _data;
        private float _lastPlaytimeSyncRealtime;

        public SaveDataV1 Data => _data;
        public int ActiveSlot => _activeSlot;
        public bool HasActiveSlot => _activeSlot >= 0;

        private static string SlotFilePath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");

        private static readonly string LegacyFilePath =
            Path.Combine(Application.persistentDataPath, "save_v1.json");

        // ========== 初始化 ==========

        private void Initialize()
        {
            // 兼容旧单文件存档：迁移到槽位 0
            if (File.Exists(LegacyFilePath) && !File.Exists(SlotFilePath(0)))
            {
                try
                {
                    File.Copy(LegacyFilePath, SlotFilePath(0));
                    Debug.Log("<color=cyan>[SaveSystem] 旧存档已迁移到槽位 0</color>");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] 旧存档迁移失败：{e.Message}");
                }
            }

            // 默认尝试加载最近使用的槽位
            int lastSlot = PlayerPrefs.GetInt("GoB.LastSaveSlot", -1);
            if (lastSlot >= 0 && lastSlot < MaxSlots && SlotExists(lastSlot))
            {
                LoadSlot(lastSlot);
            }
            else
            {
                _data = new SaveDataV1();
                _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
            }
        }

        // ========== 槽位查询 ==========

        /// <summary>检查某个槽位是否有存档</summary>
        public bool SlotExists(int slot) => slot >= 0 && slot < MaxSlots && File.Exists(SlotFilePath(slot));

        /// <summary>读取某个槽位的存档数据（不激活，仅查看）</summary>
        public SaveDataV1 PeekSlot(int slot)
        {
            if (!SlotExists(slot)) return null;
            try
            {
                string json = File.ReadAllText(SlotFilePath(slot));
                var data = JsonUtility.FromJson<SaveDataV1>(json);
                NormalizeAndMigrate(data);
                return data;
            }
            catch { return null; }
        }

        /// <summary>获取槽位摘要信息（用于 UI 展示）</summary>
        public string GetSlotSummary(int slot)
        {
            var data = PeekSlot(slot);
            if (data == null) return "空 存 档";

            string name = string.IsNullOrEmpty(data.slotName) ? $"存档 {slot + 1}" : data.slotName;
            string time = data.lastSaveTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(data.lastSaveTimestamp).LocalDateTime.ToString("MM/dd HH:mm")
                : "未知";
            int skillCount = data.unlockedSkillIds?.Count ?? 0;
            int moduleCount = data.unlockedModuleIds?.Count ?? 0;
            string playtime = FormatPlaytime(data.totalPlayTimeSeconds);
            return $"{name}\n等级 {data.cultivationRealm + 1}　通关 {data.totalRunsCompleted}　阵亡 {data.totalDeaths}\n解锁 技能 {skillCount} / 模块 {moduleCount}　游玩 {playtime}\n最后保存 {time}";
        }

        // ========== 加载 / 保存 / 创建 / 删除 ==========

        /// <summary>创建新存档到指定槽位（覆盖已有数据）</summary>
        public void CreateSlot(int slot, string slotName = "")
        {
            _activeSlot = slot;
            _data = new SaveDataV1
            {
                slotName = string.IsNullOrEmpty(slotName) ? $"冒险者 {slot + 1}" : slotName,
                createdTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
            Save();
            PlayerPrefs.SetInt("GoB.LastSaveSlot", slot);
            PlayerPrefs.Save();
            Debug.Log($"<color=cyan>[SaveSystem] 创建存档槽位 {slot}：{_data.slotName}</color>");
        }

        /// <summary>加载指定槽位的存档</summary>
        public void LoadSlot(int slot)
        {
            if (slot < 0 || slot >= MaxSlots) return;

            _activeSlot = slot;
            string path = SlotFilePath(slot);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _data = JsonUtility.FromJson<SaveDataV1>(json) ?? new SaveDataV1();
                    bool migrated = NormalizeAndMigrate(_data);
                    Debug.Log($"<color=cyan>[SaveSystem] 加载存档槽位 {slot}：{_data.slotName}（技能 {_data.unlockedSkillIds.Count} / 模块 {_data.unlockedModuleIds.Count}）</color>");
                    _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
                    if (migrated) Save();
                }
                else
                {
                    _data = new SaveDataV1();
                    _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
                    Debug.Log($"<color=gray>[SaveSystem] 槽位 {slot} 无存档文件，初始化空数据</color>");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 加载槽位 {slot} 失败：{e.Message}");
                _data = new SaveDataV1();
                _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
            }

            PlayerPrefs.SetInt("GoB.LastSaveSlot", slot);
            PlayerPrefs.Save();
        }

        /// <summary>保存当前数据到活跃槽位（无槽位时自动创建槽位 0）</summary>
        public void Save()
        {
            if (_data == null) _data = new SaveDataV1();
            NormalizeAndMigrate(_data);
            if (_activeSlot < 0)
            {
                // 兼容旧代码直接调用 Save() 的情况：自动创建槽位 0
                _activeSlot = 0;
                if (string.IsNullOrEmpty(_data.slotName))
                    _data.slotName = "冒险者 1";
                Debug.Log("<color=cyan>[SaveSystem] 无活跃槽位，自动使用槽位 0</color>");
            }

            SyncPlaytime();
            _data.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(SlotFilePath(_activeSlot), json);
                Debug.Log($"<color=cyan>[SaveSystem] 已保存到槽位 {_activeSlot}</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存失败：{e.Message}");
            }
        }

        /// <summary>删除指定槽位</summary>
        public void DeleteSlot(int slot)
        {
            if (slot < 0 || slot >= MaxSlots) return;
            string path = SlotFilePath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"<color=yellow>[SaveSystem] 已删除槽位 {slot}</color>");
            }
            if (_activeSlot == slot)
            {
                _activeSlot = -1;
                _data = new SaveDataV1();
                _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>自动存档（进入关卡时调用）</summary>
        public void AutoSave()
        {
            if (!HasActiveSlot) return;
            Save();
            Debug.Log($"<color=cyan>[SaveSystem] 自动存档 → 槽位 {_activeSlot}</color>");
        }

        // ========== 永久解锁 ==========

        /// <summary>记录技能首次获取；新解锁时立即保存当前槽位。</summary>
        public bool UnlockSkill(SkillData skill)
        {
            return skill != null && UnlockId(skill.skillName, isSkill: true);
        }

        /// <summary>记录模块首次获取；新解锁时立即保存当前槽位。</summary>
        public bool UnlockModule(ModuleDef module)
        {
            return module != null && UnlockId(module.moduleId, isSkill: false);
        }

        private bool UnlockId(string id, bool isSkill)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (_data == null) _data = new SaveDataV1();
            NormalizeAndMigrate(_data);

            List<string> ids = isSkill ? _data.unlockedSkillIds : _data.unlockedModuleIds;
            if (ids.Contains(id)) return false;

            ids.Add(id);
            Save();
            Debug.Log($"<color=#66ff99>[永久解锁] {(isSkill ? "技能" : "模块")}：{id}</color>");
            return true;
        }

        // ========== 调试 ==========

        public void ResetAll()
        {
            _data = new SaveDataV1();
            _lastPlaytimeSyncRealtime = Time.realtimeSinceStartup;
            if (HasActiveSlot) Save();
            Debug.LogWarning("<color=yellow>[SaveSystem] 存档已重置</color>");
        }

        // ========== 洞府素材库存便捷接口（保持向后兼容）==========

        public int GetCaveItemCount(string itemName)
        {
            if (_data == null || _data.caveInventory == null) return 0;
            foreach (var entry in _data.caveInventory)
                if (entry.itemName == itemName) return entry.count;
            return 0;
        }

        public void AddCaveItem(string itemName, int amount)
        {
            if (_data == null) _data = new SaveDataV1();
            if (_data.caveInventory == null) _data.caveInventory = new System.Collections.Generic.List<ItemCountEntry>();

            for (int i = 0; i < _data.caveInventory.Count; i++)
            {
                if (_data.caveInventory[i].itemName == itemName)
                {
                    var e = _data.caveInventory[i];
                    e.count += amount;
                    if (e.count <= 0)
                        _data.caveInventory.RemoveAt(i);
                    else
                        _data.caveInventory[i] = e;
                    return;
                }
            }
            if (amount > 0)
                _data.caveInventory.Add(new ItemCountEntry { itemName = itemName, count = amount });
        }

        public bool ConsumeCaveItem(string itemName, int amount)
        {
            int current = GetCaveItemCount(itemName);
            if (current < amount) return false;
            AddCaveItem(itemName, -amount);
            return true;
        }

        // ========== 向后兼容：Load() 别名 ==========
        public void Load()
        {
            if (HasActiveSlot) LoadSlot(_activeSlot);
        }

        private void SyncPlaytime()
        {
            float now = Time.realtimeSinceStartup;
            if (_lastPlaytimeSyncRealtime > 0f && now >= _lastPlaytimeSyncRealtime)
                _data.totalPlayTimeSeconds += now - _lastPlaytimeSyncRealtime;
            _lastPlaytimeSyncRealtime = now;
        }

        private static string FormatPlaytime(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}时{span.Minutes:00}分"
                : $"{span.Minutes}分";
        }

        /// <summary>补齐缺失集合，并将 v2 Build 快照一次性迁移为发现记录。</summary>
        private static bool NormalizeAndMigrate(SaveDataV1 data)
        {
            if (data == null) return false;
            bool changed = data.schemaVersion < 3;

            data.unlockedSkillIds ??= new List<string>();
            data.unlockedModuleIds ??= new List<string>();
            data.caveInventory ??= new List<ItemCountEntry>();
            data.unlockedTalentIds ??= new List<string>();
            data.unlockedBeastIds ??= new List<string>();

#pragma warning disable CS0618
            if (data.buildBackpack != null && data.buildBackpack.Count > 0)
            {
                foreach (var build in data.buildBackpack)
                {
                    if (build == null) continue;
                    AddUnique(data.unlockedSkillIds, build.skillQ);
                    AddUnique(data.unlockedSkillIds, build.skillE);
                    AddUnique(data.unlockedSkillIds, build.skillR);
                    MigrateChain(build.chain0, data.unlockedModuleIds);
                    MigrateChain(build.chain1, data.unlockedModuleIds);
                    MigrateChain(build.chain2, data.unlockedModuleIds);
                }
                data.buildBackpack.Clear();
                changed = true;
            }
#pragma warning restore CS0618

            if (!string.IsNullOrEmpty(data.lastRunLegacyModuleId))
            {
                AddUnique(data.unlockedModuleIds, data.lastRunLegacyModuleId);
                data.lastRunLegacyModuleId = "";
                changed = true;
            }

            if (data.schemaVersion != 3)
            {
                data.schemaVersion = 3;
                changed = true;
            }
            return changed;
        }

        private static void MigrateChain(LegacyChainSnapshot chain, List<string> ids)
        {
            if (chain == null) return;
            AddUnique(ids, chain.triggerId);
            AddUnique(ids, chain.effectId);
            AddUnique(ids, chain.modifier0Id);
            AddUnique(ids, chain.modifier1Id);
        }

        private static void AddUnique(List<string> ids, string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                ids.Add(id);
        }
    }
}
