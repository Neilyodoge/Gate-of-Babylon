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
    /// 策划用 Excel 编辑 Assets/1Game/RawData/LevelDesign/ 下的 CSV 文件，
    /// 然后在 Unity 菜单 "修仙图/导表" 或编辑器窗口中一键导出为 JSON。
    /// </summary>
    public static class CsvToJsonImporter
    {
        private const string CsvRoot = "Assets/1Game/RawData/LevelDesign";
        private const string JsonRoot = "Assets/1Game/Resources/LevelDesign";

        [MenuItem("修仙图/导表 — CSV → JSON %#t")]
        public static void ImportAll()
        {
            int count = 0;
            count += ImportFlat<MapStructureRow>("Map_Structure_Config", ParseMapStructureRow);
            count += ImportFlat<RoomSocketRow>("Room_Socket_Group_Config", ParseRoomSocketRow);
            count += ImportFlat<BossPhaseRow>("Boss_Phase_Config", ParseBossPhaseRow);
            count += ImportFlat<ItemInRunRow>("Item_InRun_Config", ParseItemInRunRow);
            count += ImportFlat<MaterialCaveResRow>("Material_CaveRes_Config", ParseMaterialCaveResRow);
            count += ImportEventStory();

            AssetDatabase.Refresh();
            Debug.Log($"[导表] 完成 — 共 {count} 张表已更新到 {JsonRoot}/");
        }

        // ── flat table generic pipeline ──────────────────────────────
        private static int ImportFlat<TRow>(string tableName, Func<string[], string[], TRow> parser)
        {
            string csvPath = Path.Combine(CsvRoot, tableName + ".csv");
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
            for (int i = 1; i < lines.Count; i++)
            {
                string[] cols = ParseCsvLine(lines[i]);
                if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;
                try { rows.Add(parser(headers, cols)); }
                catch (Exception ex) { Debug.LogError($"[导表] {tableName} 第{i + 1}行解析失败：{ex.Message}"); }
            }

            WriteJson(tableName, rows.ToArray());
            Debug.Log($"[导表] {tableName} → {rows.Count} 行");
            return 1;
        }

        // ── Event_Story_Config (nested Options) ─────────────────────
        private static int ImportEventStory()
        {
            const string tableName = "Event_Story_Config";
            string csvPath = Path.Combine(CsvRoot, tableName + ".csv");
            string fullCsv = Path.GetFullPath(csvPath);
            if (!File.Exists(fullCsv))
            {
                Debug.LogWarning($"[导表] 找不到 {csvPath}，跳过。");
                return 0;
            }

            var lines = ReadCsvLines(fullCsv);
            if (lines.Count < 2) return 0;

            string[] headers = ParseCsvLine(lines[0]);
            var eventMap = new Dictionary<int, StoryEventRow>();
            var orderedIds = new List<int>();

            for (int i = 1; i < lines.Count; i++)
            {
                string[] cols = ParseCsvLine(lines[i]);
                if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;

                int id = int.Parse(GetCol(headers, cols, "EventID"));
                string nameCn = GetCol(headers, cols, "Name_CN");

                if (!string.IsNullOrWhiteSpace(nameCn) && !eventMap.ContainsKey(id))
                {
                    var row = new StoryEventRow
                    {
                        ID = id,
                        Name_CN = nameCn,
                        Type = ParseInt(GetCol(headers, cols, "Type")),
                        PrereqFlag = GetCol(headers, cols, "PrereqFlag"),
                        Text_CN = GetCol(headers, cols, "Text_CN").Replace("\\n", "\n"),
                        Options = Array.Empty<EventOption>()
                    };
                    eventMap[id] = row;
                    orderedIds.Add(id);
                }

                if (!eventMap.ContainsKey(id)) continue;

                string optText = GetCol(headers, cols, "Opt_Text");
                if (string.IsNullOrWhiteSpace(optText)) continue;

                var opt = new EventOption
                {
                    Text = optText,
                    FlagName = GetCol(headers, cols, "Opt_FlagName"),
                    FlagValue = ParseInt(GetCol(headers, cols, "Opt_FlagValue")),
                    RewardID = ParseInt(GetCol(headers, cols, "Opt_RewardID")),
                    CostID = ParseInt(GetCol(headers, cols, "Opt_CostID")),
                    KarmaChange = ParseInt(GetCol(headers, cols, "Opt_KarmaChange")),
                    DaoxinChange = ParseInt(GetCol(headers, cols, "Opt_DaoxinChange")),
                    LifespanChange = ParseInt(GetCol(headers, cols, "Opt_LifespanChange"))
                };

                var list = new List<EventOption>(eventMap[id].Options) { opt };
                eventMap[id].Options = list.ToArray();
            }

            var result = orderedIds.Select(id => eventMap[id]).ToArray();
            WriteJson(tableName, result);
            Debug.Log($"[导表] {tableName} → {result.Length} 个事件");
            return 1;
        }

        // ── row parsers ──────────────────────────────────────────────
        private static MapStructureRow ParseMapStructureRow(string[] h, string[] c)
        {
            return new MapStructureRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                ActID = ParseInt(GetCol(h, c, "ActID")),
                MaxFloor = ParseInt(GetCol(h, c, "MaxFloor")),
                MinNodes = ParseInt(GetCol(h, c, "MinNodes")),
                MaxNodes = ParseInt(GetCol(h, c, "MaxNodes")),
                NormalWeight = ParseInt(GetCol(h, c, "NormalWeight"), 75),
                SpecialWeight = ParseInt(GetCol(h, c, "SpecialWeight"), 25),
                EliteMinCount = ParseInt(GetCol(h, c, "EliteMinCount")),
                EliteMaxCount = ParseInt(GetCol(h, c, "EliteMaxCount")),
                EventMinCount = ParseInt(GetCol(h, c, "EventMinCount")),
                ShopMinCount = ParseInt(GetCol(h, c, "ShopMinCount")),
                RoomPoolID = ParseIntArray(GetCol(h, c, "RoomPoolID"))
            };
        }

        private static RoomSocketRow ParseRoomSocketRow(string[] h, string[] c)
        {
            return new RoomSocketRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                SceneName = GetCol(h, c, "SceneName"),
                RoomType = ParseInt(GetCol(h, c, "RoomType")),
                EnemySquadID = ParseIntArray(GetCol(h, c, "EnemySquadID")),
                ItemDropIDs = ParseIntArray(GetCol(h, c, "ItemDropIDs")),
                ItemDropWeights = ParseIntArray(GetCol(h, c, "ItemDropWeights")),
                EventID = ParseInt(GetCol(h, c, "EventID")),
                EventTriggerRate = ParseInt(GetCol(h, c, "EventTriggerRate")),
                Weight = ParseInt(GetCol(h, c, "Weight"), 100)
            };
        }

        private static BossPhaseRow ParseBossPhaseRow(string[] h, string[] c)
        {
            return new BossPhaseRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                BossID = ParseInt(GetCol(h, c, "BossID")),
                PhaseName = GetCol(h, c, "PhaseName"),
                RequiredFlags = GetCol(h, c, "RequiredFlags"),
                Priority = ParseInt(GetCol(h, c, "Priority")),
                DialogueLines = ParseStringArray(GetCol(h, c, "DialogueLines")),
                SkillSetID = ParseInt(GetCol(h, c, "SkillSetID")),
                StatModifier = GetCol(h, c, "StatModifier"),
                SummonSquadID = ParseInt(GetCol(h, c, "SummonSquadID"))
            };
        }

        private static ItemInRunRow ParseItemInRunRow(string[] h, string[] c)
        {
            return new ItemInRunRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                Name_CN = GetCol(h, c, "Name_CN"),
                Text_CN = GetCol(h, c, "Text_CN"),
                Type = ParseInt(GetCol(h, c, "Type")),
                Icon = GetCol(h, c, "Icon"),
                MaxStack = ParseInt(GetCol(h, c, "MaxStack"), 1),
                EffectID = ParseInt(GetCol(h, c, "EffectID")),
                Duration = ParseFloat(GetCol(h, c, "Duration"))
            };
        }

        private static MaterialCaveResRow ParseMaterialCaveResRow(string[] h, string[] c)
        {
            return new MaterialCaveResRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                Name_CN = GetCol(h, c, "Name_CN"),
                Text_CN = GetCol(h, c, "Text_CN"),
                Type = ParseInt(GetCol(h, c, "Type")),
                Icon = GetCol(h, c, "Icon"),
                MaxStack = ParseInt(GetCol(h, c, "MaxStack"), 99)
            };
        }

        // ── JSON writer ──────────────────────────────────────────────
        private static void WriteJson<TRow>(string tableName, TRow[] rows)
        {
            string dir = Path.GetFullPath(JsonRoot);
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

        private static int[] ParseIntArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
            return s.Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => int.TryParse(x.Trim(), out int v) ? v : 0)
                .ToArray();
        }

        private static string[] ParseStringArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            return s.Split('|').Select(x => x.Trim()).ToArray();
        }
    }
}
