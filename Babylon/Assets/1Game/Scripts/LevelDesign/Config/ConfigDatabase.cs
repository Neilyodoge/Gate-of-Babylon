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

        public bool Loaded { get; private set; }

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

            Loaded = true;
            Debug.Log($"[ConfigDatabase] 已加载 — Maps:{MapStructures.Count} Rooms:{RoomSockets.Count} " +
                      $"Events:{StoryEvents.Count} BossPhases:{BossPhases.Count} " +
                      $"Items:{ItemsInRun.Count} Materials:{CaveMaterials.Count}");
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
