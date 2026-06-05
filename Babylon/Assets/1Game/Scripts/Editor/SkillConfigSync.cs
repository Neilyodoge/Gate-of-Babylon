using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu.LevelDesign.Editor
{
    /// <summary>
    /// 技能配表 → SkillData SO 同步（v0.5.6）。
    ///
    /// 一键：① 重新导表（CSV→JSON）② 按 Skill_Base_Config 每行 创建/绑定 SkillData SO。
    /// 实现"以后填在表格中的技能默认直接制作 SO，无需过问"。
    ///
    /// 规则：
    /// - 按 configId 或 skillName 找现有 SO：找到 → 仅补 configId 绑定（不覆盖设计者已调的 skillType/数值）；
    ///   找不到 → 新建 SkillData（名/描述/品阶/CD/configId + 由表 Type 推默认 SkillType + 按品阶给默认基础伤害）。
    /// - CD / 伤害的最终生效仍走运行时表覆盖（SkillTuning），SO 只是载体。
    /// - 复杂机制（Type=4 特殊 / 黑洞 / 冥河 等）SO 为数据壳，真实行为待代码实现。
    /// </summary>
    public static class SkillConfigSync
    {
        private const string SkillDir = "Assets/1Game/Data/Skills";

        [MenuItem("仙途秘境/技能：表 → SO 同步（先导表）")]
        public static void SyncFromConfig()
        {
            // ① 先导表，保证 JSON 最新
            CsvToJsonImporter.ImportAll();
            ConfigDatabase.Reload();

            var db = ConfigDatabase.Instance;
            if (db.SkillBases == null || db.SkillBases.Count == 0)
            {
                Debug.LogWarning("[技能同步] Skill_Base_Config 为空，先检查 CSV / 导表。");
                return;
            }

            // ② 收集现有 SkillData
            var byId = new Dictionary<int, SkillData>();
            var byName = new Dictionary<string, SkillData>();
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (so == null) continue;
                if (so.configId > 0 && !byId.ContainsKey(so.configId)) byId[so.configId] = so;
                if (!string.IsNullOrEmpty(so.skillName) && !byName.ContainsKey(so.skillName)) byName[so.skillName] = so;
            }

            int created = 0, bound = 0;
            foreach (var kv in db.SkillBases)
            {
                var row = kv.Value;
                SkillData so = null;
                if (byId.TryGetValue(row.ID, out var s1)) so = s1;
                else if (byName.TryGetValue(row.Name_CN, out var s2)) so = s2;

                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<SkillData>();
                    so.skillName = row.Name_CN;
                    so.description = row.Desc_CN;
                    so.rarity = (ItemRarity)Mathf.Clamp(row.Rarity - 1, 0, 4);
                    so.skillType = MapType(row.Type);
                    so.cooldown = row.BaseCooldown;
                    so.baseDamage = DefaultDamage(row.Rarity);
                    so.aoeRadius = 3f;
                    so.configId = row.ID;

                    string path = AssetDatabase.GenerateUniqueAssetPath($"{SkillDir}/{Sanitize(row.Name_CN)}.asset");
                    AssetDatabase.CreateAsset(so, path);
                    created++;
                }
                else if (so.configId != row.ID)
                {
                    // 现有 SO 仅补/正绑定，不覆盖设计者已调的 skillType / 数值
                    so.configId = row.ID;
                    EditorUtility.SetDirty(so);
                    bound++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#9cc0ff>[技能同步] 完成 — 新建 {created} 个 SO，绑定 {bound} 个；表共 {db.SkillBases.Count} 行。" +
                      $"新建的复杂/特殊技能为数据壳，机制需后续代码实现。</color>");
        }

        /// <summary>表 Type（1基础/2增益/3减益/4特殊）→ 默认 SkillType（设计者可在 SO 上细调）。</summary>
        private static SkillType MapType(int tableType) => tableType switch
        {
            1 => SkillType.AreaDamage,
            2 => SkillType.Buff,
            3 => SkillType.AreaDamage,   // 减益（带伤害）默认按范围
            4 => SkillType.Buff,         // 特殊：占位
            _ => SkillType.AreaDamage
        };

        /// <summary>按品阶给新建技能一个默认基础伤害（设计者再调；表 BaseDamageRatio 会以此为基数按 % 生效）。</summary>
        private static float DefaultDamage(int rarity) => rarity switch
        {
            1 => 25f,
            2 => 40f,
            3 => 55f,
            4 => 75f,
            5 => 100f,
            _ => 30f
        };

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Skill";
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }
    }
}
