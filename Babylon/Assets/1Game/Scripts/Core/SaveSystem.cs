using System;
using System.IO;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// v0.5 存档系统 —— JSON 文件持久化（Unity 推荐方案）。
    ///
    /// 单例模式：通过 <see cref="Instance"/> 全局访问。
    /// 自动加载：第一次访问时从磁盘加载；如果文件不存在则创建空存档。
    /// 自动保存：调用 <see cref="Save"/>，建议在重要变更后立即调用（如撤离成功 / 洞府消费）。
    ///
    /// 存档位置：<c>Application.persistentDataPath/save_v1.json</c>
    /// </summary>
    public class SaveSystem
    {
        private const string SaveFileName = "save_v1.json";
        private static SaveSystem _instance;

        public static SaveSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SaveSystem();
                    _instance.Load();
                }
                return _instance;
            }
        }

        private SaveDataV1 _data;
        public SaveDataV1 Data => _data;

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        // ========== 加载 / 保存 ==========

        public void Load()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    _data = JsonUtility.FromJson<SaveDataV1>(json);
                    if (_data == null) _data = new SaveDataV1();
                    Debug.Log($"<color=cyan>[SaveSystem] 加载存档：{SaveFilePath}（V{_data.schemaVersion}，洞府素材 {_data.caveInventory.Count} 种 / 灵气 {_data.caveQi} / 天赋 {_data.unlockedTalentIds.Count} 个）</color>");
                }
                else
                {
                    _data = new SaveDataV1();
                    Debug.Log($"<color=gray>[SaveSystem] 无存档文件，初始化为空存档：{SaveFilePath}</color>");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[SaveSystem] 加载存档失败：{e.Message}。初始化为空存档。</color>");
                _data = new SaveDataV1();
            }
        }

        public void Save()
        {
            if (_data == null) _data = new SaveDataV1();
            _data.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"<color=cyan>[SaveSystem] 已保存：{SaveFilePath}</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[SaveSystem] 保存失败：{e.Message}</color>");
            }
        }

        // ========== 调试 ==========

        /// <summary>清空所有存档（仅 Debug / 测试用）</summary>
        public void ResetAll()
        {
            _data = new SaveDataV1();
            Save();
            Debug.LogWarning("<color=yellow>[SaveSystem] 存档已重置</color>");
        }

        // ========== 洞府素材库存便捷接口 ==========

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
                    {
                        _data.caveInventory.RemoveAt(i);
                    }
                    else
                    {
                        _data.caveInventory[i] = e;
                    }
                    return;
                }
            }
            // 不存在则新增
            if (amount > 0)
            {
                _data.caveInventory.Add(new ItemCountEntry { itemName = itemName, count = amount });
            }
        }

        public bool ConsumeCaveItem(string itemName, int amount)
        {
            int current = GetCaveItemCount(itemName);
            if (current < amount) return false;
            AddCaveItem(itemName, -amount);
            return true;
        }
    }
}
