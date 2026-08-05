using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace XianTu.LevelDesign.Editor
{
    /// <summary>
    /// CSV → JSON 一键导表工具。
    /// 战斗策划用 Excel 编辑 Assets/1Game/RawData/Combat/ 下的 CSV 文件，
    /// 然后在 Unity 菜单中一键导出为 JSON。关卡配置统一由中文关卡工具编辑 Asset。
    /// </summary>
    public static class CsvToJsonImporter
    {
        private const string CombatCsvRoot = "Assets/1Game/RawData/Combat";
        private const string CombatJsonRoot = "Assets/1Game/Resources/Combat";

        [MenuItem("修仙图/导表 — CSV → JSON %#t")]
        public static void ImportAll()
        {
            int count = 0;
            count += ImportFlat<SkillBaseRow>(
                "Skill_Base_Config", ParseSkillBaseRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleBaseRow>("Module_Base_Config", ParseModuleBaseRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleTriggerParamRow>("Module_Trigger_Param_Config", ParseModuleTriggerParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleEffectParamRow>("Module_Effect_Param_Config", ParseModuleEffectParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleModifierParamRow>("Module_Modifier_Param_Config", ParseModuleModifierParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleUniversalParamRow>("Module_Universal_Param_Config", ParseModuleUniversalParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<SkillParamRow>("Skill_Param_Config", ParseSkillParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<EnemyBaseRow>("Enemy_Base_Config", ParseEnemyBaseRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ConsumeKindBonusRow>("ConsumeKind_Bonus_Config", ParseConsumeKindBonusRow, CombatCsvRoot, CombatJsonRoot);

            AssetDatabase.Refresh();
            Debug.Log($"[导表] 完成 — 共 {count} 张战斗表已更新（{CombatJsonRoot}/）");
        }

        // ── flat table generic pipeline ──────────────────────────────
        private static int ImportFlat<TRow>(string tableName, Func<string[], string[], TRow> parser,
            string csvRoot = CombatCsvRoot, string jsonRoot = CombatJsonRoot)
        {
            string csvPath = Path.Combine(csvRoot, tableName + ".csv");
            string fullCsv = Path.GetFullPath(csvPath);
            if (!File.Exists(fullCsv))
            {
                Debug.LogWarning($"[导表] 找不到 {csvPath}，跳过。");
                return 0;
            }

            var lines = ReadCsvLines(fullCsv);
            if (lines.Count < 2) { Debug.LogWarning($"[导表] {csvPath} 无数据行。"); return 0; }

            string[] headers = ParseCsvLine(lines[0]);
            var rows = new List<TRow>();
            var idField = typeof(TRow).GetField("ID");
            var seenIds = new HashSet<object>();
            for (int i = 1; i < lines.Count; i++)
            {
                string[] cols = ParseCsvLine(lines[i]);
                if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;
                try
                {
                    var row = parser(headers, cols);
                    if (idField != null)
                    {
                        object id = idField.GetValue(row);
                        if (!seenIds.Add(id))
                            throw new InvalidDataException($"ID={id} 重复。");
                    }
                    rows.Add(row);
                }
                catch (Exception ex) { Debug.LogError($"[导表] {tableName} 第{i + 1}行解析失败：{ex.Message}"); }
            }

            WriteJson(tableName, rows.ToArray(), jsonRoot);
            Debug.Log($"[导表] {tableName} → {rows.Count} 行");
            return 1;
        }

        // ── row parsers ──────────────────────────────────────────────
        private static SkillBaseRow ParseSkillBaseRow(string[] h, string[] c)
        {
            return new SkillBaseRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                Name_CN = GetCol(h, c, "Name_CN"),
                Desc_CN = GetCol(h, c, "Desc_CN"),
                Rarity = ParseInt(GetCol(h, c, "Rarity")),
                Type = ParseInt(GetCol(h, c, "Type")),
                BaseCooldown = ParseFloat(GetCol(h, c, "BaseCooldown")),
                BaseDamageRatio = ParseInt(GetCol(h, c, "BaseDamageRatio")),
                IconPath = GetCol(h, c, "IconPath")
            };
        }

        private static ModuleBaseRow ParseModuleBaseRow(string[] h, string[] c)
        {
            return new ModuleBaseRow
            {
                ModuleId = GetCol(h, c, "ModuleId"),
                Name_CN = GetCol(h, c, "Name_CN"),
                Category = ParseInt(GetCol(h, c, "Category")),
                SubType = ParseInt(GetCol(h, c, "SubType")),
                Rarity = ParseInt(GetCol(h, c, "Rarity")),
                FuncTags = ParseInt(GetCol(h, c, "FuncTags")),
                ShapeTags = ParseInt(GetCol(h, c, "ShapeTags")),
                StyleTags = ParseInt(GetCol(h, c, "StyleTags")),
                ConsumeKind = ParseInt(GetCol(h, c, "ConsumeKind")),
                WindowSeconds = ParseFloat(GetCol(h, c, "WindowSeconds")),
                MaxStacks = ParseInt(GetCol(h, c, "MaxStacks")),
                EffectRole = ParseInt(GetCol(h, c, "EffectRole")),
                Threshold = ParseInt(GetCol(h, c, "Threshold")),
                Cooldown = ParseFloat(GetCol(h, c, "Cooldown")),
                Interval = ParseFloat(GetCol(h, c, "Interval")),
                BaseDamage = ParseFloat(GetCol(h, c, "BaseDamage")),
                DamageScaling = ParseFloat(GetCol(h, c, "DamageScaling")),
                AoeRadius = ParseFloat(GetCol(h, c, "AoeRadius")),
                Element = ParseInt(GetCol(h, c, "Element")),
                ModifierValue = ParseFloat(GetCol(h, c, "ModifierValue")),
                UniTrigType = ParseInt(GetCol(h, c, "UniTrigType"), -1),
                UniEffType = ParseInt(GetCol(h, c, "UniEffType"), -1),
                DropSource = GetCol(h, c, "DropSource"),
                UnlockCond = GetCol(h, c, "UnlockCond"),
                Desc_CN = GetCol(h, c, "Desc_CN")
            };
        }

        private static ModuleTriggerParamRow ParseModuleTriggerParamRow(string[] h, string[] c)
        {
            return new ModuleTriggerParamRow
            {
                ModuleId = GetCol(h, c, "ModuleId"),
                TriggerType = ParseInt(GetCol(h, c, "TriggerType")),
                Threshold = ParseInt(GetCol(h, c, "Threshold")),
                Cooldown = ParseFloat(GetCol(h, c, "Cooldown")),
                Interval = ParseFloat(GetCol(h, c, "Interval")),
                ConsumeStacks = ParseInt(GetCol(h, c, "ConsumeStacks")),
                MoveDistanceThreshold = ParseFloat(GetCol(h, c, "MoveDistanceThreshold")),
                HealthThreshold = ParseFloat(GetCol(h, c, "HealthThreshold")),
                ConsumeKind = ParseInt(GetCol(h, c, "ConsumeKind")),
                WindowSeconds = ParseFloat(GetCol(h, c, "WindowSeconds")),
                MaxStacks = ParseInt(GetCol(h, c, "MaxStacks"))
            };
        }

        private static ModuleEffectParamRow ParseModuleEffectParamRow(string[] h, string[] c)
        {
            return new ModuleEffectParamRow
            {
                ModuleId = GetCol(h, c, "ModuleId"),
                EffectType = ParseInt(GetCol(h, c, "EffectType")),
                EffectRole = ParseInt(GetCol(h, c, "EffectRole")),
                BaseDamage = ParseFloat(GetCol(h, c, "BaseDamage")),
                DamageScaling = ParseFloat(GetCol(h, c, "DamageScaling")),
                AoeRadius = ParseFloat(GetCol(h, c, "AoeRadius")),
                Element = ParseInt(GetCol(h, c, "Element")),
                HealAmount = ParseFloat(GetCol(h, c, "HealAmount")),
                HealScaling = ParseFloat(GetCol(h, c, "HealScaling")),
                ShieldAmount = ParseFloat(GetCol(h, c, "ShieldAmount")),
                BuffDuration = ParseFloat(GetCol(h, c, "BuffDuration")),
                BuffDamageReduction = ParseFloat(GetCol(h, c, "BuffDamageReduction")),
                ProjectileSpeed = ParseFloat(GetCol(h, c, "ProjectileSpeed")),
                ProjectileCount = ParseInt(GetCol(h, c, "ProjectileCount")),
                SpreadAngle = ParseFloat(GetCol(h, c, "SpreadAngle")),
                SlowPercent = ParseFloat(GetCol(h, c, "SlowPercent")),
                StunDuration = ParseFloat(GetCol(h, c, "StunDuration")),
                KnockbackForce = ParseFloat(GetCol(h, c, "KnockbackForce")),
                DashDistance = ParseFloat(GetCol(h, c, "DashDistance")),
                PullRadius = ParseFloat(GetCol(h, c, "PullRadius")),
                DotDPS = ParseFloat(GetCol(h, c, "DotDPS")),
                DotDuration = ParseFloat(GetCol(h, c, "DotDuration")),
                InvincibleDuration = ParseFloat(GetCol(h, c, "InvincibleDuration")),
                SummonDuration = ParseFloat(GetCol(h, c, "SummonDuration")),
                SummonDamage = ParseFloat(GetCol(h, c, "SummonDamage")),
                TrapDuration = ParseFloat(GetCol(h, c, "TrapDuration")),
                VulnerableMultiplier = ParseFloat(GetCol(h, c, "VulnerableMultiplier")),
                VulnerableDuration = ParseFloat(GetCol(h, c, "VulnerableDuration"))
            };
        }

        private static ModuleModifierParamRow ParseModuleModifierParamRow(string[] h, string[] c)
        {
            return new ModuleModifierParamRow
            {
                ModuleId = GetCol(h, c, "ModuleId"),
                ModifierType = ParseInt(GetCol(h, c, "ModifierType")),
                ModifierValue = ParseFloat(GetCol(h, c, "ModifierValue")),
                BurnDPS = ParseFloat(GetCol(h, c, "BurnDPS")),
                BurnDuration = ParseFloat(GetCol(h, c, "BurnDuration")),
                FreezeDuration = ParseFloat(GetCol(h, c, "FreezeDuration")),
                LightningDamage = ParseFloat(GetCol(h, c, "LightningDamage")),
                PoisonDPS = ParseFloat(GetCol(h, c, "PoisonDPS")),
                PoisonDuration = ParseFloat(GetCol(h, c, "PoisonDuration")),
                ExtraCount = ParseInt(GetCol(h, c, "ExtraCount")),
                CostHPPercent = ParseFloat(GetCol(h, c, "CostHPPercent")),
                CostDamageBonus = ParseFloat(GetCol(h, c, "CostDamageBonus"))
            };
        }

        private static ModuleUniversalParamRow ParseModuleUniversalParamRow(string[] h, string[] c)
        {
            return new ModuleUniversalParamRow
            {
                ModuleId = GetCol(h, c, "ModuleId"),
                UniTriggerType = ParseInt(GetCol(h, c, "UniTriggerType")),
                UniTriggerThreshold = ParseInt(GetCol(h, c, "UniTriggerThreshold")),
                UniTriggerCooldown = ParseFloat(GetCol(h, c, "UniTriggerCooldown")),
                UniEffectType = ParseInt(GetCol(h, c, "UniEffectType")),
                UniEffectRole = ParseInt(GetCol(h, c, "UniEffectRole")),
                UniConsumeKind = ParseInt(GetCol(h, c, "UniConsumeKind")),
                TriggerDesc = GetCol(h, c, "TriggerDesc"),
                EffectDesc = GetCol(h, c, "EffectDesc")
            };
        }

        private static SkillParamRow ParseSkillParamRow(string[] h, string[] c)
        {
            return new SkillParamRow
            {
                ConfigId = ParseInt(GetCol(h, c, "ConfigId")),
                Name_CN = GetCol(h, c, "Name_CN"),
                SkillType = ParseInt(GetCol(h, c, "SkillType")),
                Element = ParseInt(GetCol(h, c, "Element")),
                Rarity = ParseInt(GetCol(h, c, "Rarity")),
                BaseDamage = ParseFloat(GetCol(h, c, "BaseDamage")),
                DamageScaling = ParseFloat(GetCol(h, c, "DamageScaling")),
                Cooldown = ParseFloat(GetCol(h, c, "Cooldown")),
                CastSpeed = ParseFloat(GetCol(h, c, "CastSpeed")),
                MaxCharges = ParseInt(GetCol(h, c, "MaxCharges")),
                ChargeTime = ParseFloat(GetCol(h, c, "ChargeTime")),
                CanCharge = ParseInt(GetCol(h, c, "CanCharge")),
                ChargeLv2Time = ParseFloat(GetCol(h, c, "ChargeLv2Time")),
                ChargeLv3Time = ParseFloat(GetCol(h, c, "ChargeLv3Time")),
                ChargeLv2DmgMul = ParseFloat(GetCol(h, c, "ChargeLv2DmgMul")),
                ChargeLv3DmgMul = ParseFloat(GetCol(h, c, "ChargeLv3DmgMul")),
                ChargeLv2RadMul = ParseFloat(GetCol(h, c, "ChargeLv2RadMul")),
                ChargeLv3RadMul = ParseFloat(GetCol(h, c, "ChargeLv3RadMul")),
                ChargeMoveMul = ParseFloat(GetCol(h, c, "ChargeMoveMul")),
                AoeRadius = ParseFloat(GetCol(h, c, "AoeRadius")),
                ProjectileSpeed = ParseFloat(GetCol(h, c, "ProjectileSpeed")),
                ProjectileCount = ParseInt(GetCol(h, c, "ProjectileCount")),
                SpreadAngle = ParseFloat(GetCol(h, c, "SpreadAngle")),
                DashDistance = ParseFloat(GetCol(h, c, "DashDistance")),
                LeaveTrail = ParseInt(GetCol(h, c, "LeaveTrail")),
                HealAmount = ParseFloat(GetCol(h, c, "HealAmount")),
                HealScaling = ParseFloat(GetCol(h, c, "HealScaling")),
                SummonDuration = ParseFloat(GetCol(h, c, "SummonDuration")),
                SummonDamage = ParseFloat(GetCol(h, c, "SummonDamage")),
                SummonIsDecoy = ParseInt(GetCol(h, c, "SummonIsDecoy")),
                BuffDuration = ParseFloat(GetCol(h, c, "BuffDuration")),
                BuffAtkSpeedPct = ParseFloat(GetCol(h, c, "BuffAtkSpeedPct")),
                BuffMoveSpeedPct = ParseFloat(GetCol(h, c, "BuffMoveSpeedPct")),
                BuffAtkPct = ParseFloat(GetCol(h, c, "BuffAtkPct")),
                BuffDamageReduction = ParseFloat(GetCol(h, c, "BuffDamageReduction")),
                FreezeOnHitChance = ParseFloat(GetCol(h, c, "FreezeOnHitChance")),
                FreezeOnHitDuration = ParseFloat(GetCol(h, c, "FreezeOnHitDuration")),
                DamageFromRunTotal = ParseInt(GetCol(h, c, "DamageFromRunTotal")),
                RunTotalDamageRatio = ParseFloat(GetCol(h, c, "RunTotalDamageRatio")),
                DashInvulnerable = ParseInt(GetCol(h, c, "DashInvulnerable")),
                DashInvulnDuration = ParseFloat(GetCol(h, c, "DashInvulnDuration")),
                ArmLethalGuard = ParseInt(GetCol(h, c, "ArmLethalGuard")),
                LethalGuardDuration = ParseFloat(GetCol(h, c, "LethalGuardDuration")),
                HeavenEarthShift = ParseInt(GetCol(h, c, "HeavenEarthShift")),
                ZoneDuration = ParseFloat(GetCol(h, c, "ZoneDuration")),
                ZoneRadius = ParseFloat(GetCol(h, c, "ZoneRadius")),
                ZoneTickInterval = ParseFloat(GetCol(h, c, "ZoneTickInterval")),
                ZoneDamagePerTick = ParseFloat(GetCol(h, c, "ZoneDamagePerTick")),
                ZoneSlowPct = ParseFloat(GetCol(h, c, "ZoneSlowPct")),
                ZonePullSpeed = ParseFloat(GetCol(h, c, "ZonePullSpeed")),
                ZoneFollowPlayer = ParseInt(GetCol(h, c, "ZoneFollowPlayer")),
                ZoneBurnDPS = ParseFloat(GetCol(h, c, "ZoneBurnDPS")),
                PlayAnimation = ParseInt(GetCol(h, c, "PlayAnimation")),
                VfxDuration = ParseFloat(GetCol(h, c, "VfxDuration"))
            };
        }

        private static EnemyBaseRow ParseEnemyBaseRow(string[] h, string[] c)
        {
            return new EnemyBaseRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                TypeKey = GetCol(h, c, "TypeKey"),
                Name_CN = GetCol(h, c, "Name_CN"),
                HpMul = ParseFloat(GetCol(h, c, "HpMul"), 1f),
                DmgMul = ParseFloat(GetCol(h, c, "DmgMul"), 1f),
                DefMul = ParseFloat(GetCol(h, c, "DefMul"), 1f),
                MoveSpeed = ParseFloat(GetCol(h, c, "MoveSpeed")),
                DetectRange = ParseFloat(GetCol(h, c, "DetectRange")),
                AttackRange = ParseFloat(GetCol(h, c, "AttackRange")),
                AttackInterval = ParseFloat(GetCol(h, c, "AttackInterval")),
                SpecialParam = GetCol(h, c, "SpecialParam"),
                Behavior_CN = GetCol(h, c, "Behavior_CN"),
                Desc_CN = GetCol(h, c, "Desc_CN")
            };
        }

        private static ConsumeKindBonusRow ParseConsumeKindBonusRow(string[] h, string[] c)
        {
            return new ConsumeKindBonusRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                ConsumeKind = GetCol(h, c, "ConsumeKind"),
                Name_CN = GetCol(h, c, "Name_CN"),
                DamageMul = ParseFloat(GetCol(h, c, "DamageMul"), 1f),
                RadiusMul = ParseFloat(GetCol(h, c, "RadiusMul"), 1f),
                Note_CN = GetCol(h, c, "Note_CN")
            };
        }

        // ── JSON writer ──────────────────────────────────────────────
        private static void WriteJson<TRow>(
            string tableName,
            TRow[] rows,
            string jsonRoot = CombatJsonRoot)
        {
            string dir = Path.GetFullPath(jsonRoot);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = BuildJson(rows);
            string outPath = Path.Combine(dir, tableName + ".json");
            File.WriteAllText(outPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 手工拼 JSON（避免引入第三方库，也避免 JsonUtility 不支持序列化字典等问题）。
        /// 输出 { "Rows": [ ... ] } 格式，与 ConfigDatabase 兼容。
        /// </summary>
        private static string BuildJson<TRow>(TRow[] rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Rows\": [");

            for (int i = 0; i < rows.Length; i++)
            {
                string rowJson = JsonUtility.ToJson(rows[i], false);
                sb.Append("    ");
                sb.Append(rowJson);
                if (i < rows.Length - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.Append("}");
            return sb.ToString();
        }

        // ── CSV parsing ──────────────────────────────────────────────
        private static List<string> ReadCsvLines(string fullPath)
        {
            var result = new List<string>();
            using var reader = new StreamReader(fullPath, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    result.Add(line);
            }
            return result;
        }

        /// <summary>RFC 4180 compatible CSV line parser (handles quoted fields with commas and escaped quotes).</summary>
        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length) { fields.Add(""); break; }

                if (line[i] == '"')
                {
                    var sb = new StringBuilder();
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                            }
                            else { i++; break; }
                        }
                        else { sb.Append(line[i]); i++; }
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int next = line.IndexOf(',', i);
                    if (next < 0)
                    {
                        fields.Add(line.Substring(i));
                        break;
                    }
                    fields.Add(line.Substring(i, next - i));
                    i = next + 1;
                }
            }
            return fields.ToArray();
        }

        // ── helpers ──────────────────────────────────────────────────
        private static string GetCol(string[] headers, string[] cols, string name)
        {
            int idx = Array.IndexOf(headers, name);
            if (idx < 0 || idx >= cols.Length) return "";
            return cols[idx].Trim();
        }

        private static int ParseInt(string s, int fallback = 0)
            => int.TryParse(s, out int v) ? v : fallback;

        private static float ParseFloat(string s, float fallback = 0f)
            => float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

    }
}
