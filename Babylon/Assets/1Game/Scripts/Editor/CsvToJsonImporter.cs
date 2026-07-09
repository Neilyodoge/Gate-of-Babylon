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
        // V0.1.18：战斗/模块表独立目录（与关卡类表区分）
        private const string CombatCsvRoot = "Assets/1Game/RawData/Combat";
        private const string CombatJsonRoot = "Assets/1Game/Resources/Combat";

        [MenuItem("修仙图/导表 — CSV → JSON %#t")]
        public static void ImportAll()
        {
            int count = 0;
            count += ImportFlat<MapStructureRow>("Map_Structure_Config", ParseMapStructureRow);
            count += ImportFlat<RoomSocketRow>("Room_Socket_Group_Config", ParseRoomSocketRow);
            count += ImportFlat<BossPhaseRow>("Boss_Phase_Config", ParseBossPhaseRow);
            count += ImportFlat<ItemInRunRow>("Item_InRun_Config", ParseItemInRunRow);
            count += ImportFlat<MaterialCaveResRow>("Material_CaveRes_Config", ParseMaterialCaveResRow);
            count += ImportFlat<SkillBaseRow>("Skill_Base_Config", ParseSkillBaseRow);
            count += ImportFlat<SkillEffectRow>("Skill_Effect_Config", ParseSkillEffectRow);
            count += ImportFlat<ModuleBaseRow>("Module_Base_Config", ParseModuleBaseRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleTriggerParamRow>("Module_Trigger_Param_Config", ParseModuleTriggerParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleEffectParamRow>("Module_Effect_Param_Config", ParseModuleEffectParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleModifierParamRow>("Module_Modifier_Param_Config", ParseModuleModifierParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ModuleUniversalParamRow>("Module_Universal_Param_Config", ParseModuleUniversalParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<SkillParamRow>("Skill_Param_Config", ParseSkillParamRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<EnemyBaseRow>("Enemy_Base_Config", ParseEnemyBaseRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportFlat<ConsumeKindBonusRow>("ConsumeKind_Bonus_Config", ParseConsumeKindBonusRow, CombatCsvRoot, CombatJsonRoot);
            count += ImportEventStory();

            AssetDatabase.Refresh();
            Debug.Log($"[导表] 完成 — 共 {count} 张表已更新（关卡表 {JsonRoot}/ · 战斗表 {CombatJsonRoot}/）");
        }

        // ── flat table generic pipeline ──────────────────────────────
        private static int ImportFlat<TRow>(string tableName, Func<string[], string[], TRow> parser,
            string csvRoot = CsvRoot, string jsonRoot = JsonRoot)
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
            for (int i = 1; i < lines.Count; i++)
            {
                string[] cols = ParseCsvLine(lines[i]);
                if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;
                try { rows.Add(parser(headers, cols)); }
                catch (Exception ex) { Debug.LogError($"[导表] {tableName} 第{i + 1}行解析失败：{ex.Message}"); }
            }

            WriteJson(tableName, rows.ToArray(), jsonRoot);
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
                RoomPoolID = ParseIntArray(GetCol(h, c, "RoomPoolID")),
                EnemyScaleMul = ParseFloatArray(GetCol(h, c, "EnemyScaleMul")),
                ModuleRarityBias = ParseIntArray(GetCol(h, c, "ModuleRarityBias")),
                HasStageReturn = ParseIntArray(GetCol(h, c, "HasStageReturn"))
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
                Desc_CN = GetCol(h, c, "Desc_CN"),
                Category = GetCol(h, c, "Category"),
                Rarity = GetCol(h, c, "Rarity"),
                AtkBonus = ParseFloat(GetCol(h, c, "AtkBonus")),
                AtkBonusPct = ParseFloat(GetCol(h, c, "AtkBonusPct")),
                MaxHpBonus = ParseFloat(GetCol(h, c, "MaxHpBonus")),
                MaxHpBonusPct = ParseFloat(GetCol(h, c, "MaxHpBonusPct")),
                MoveSpeedPct = ParseFloat(GetCol(h, c, "MoveSpeedPct")),
                DmgReduction = ParseFloat(GetCol(h, c, "DmgReduction")),
                CritRate = ParseFloat(GetCol(h, c, "CritRate")),
                CritDmg = ParseFloat(GetCol(h, c, "CritDmg")),
                AtkSpeedPct = ParseFloat(GetCol(h, c, "AtkSpeedPct")),
                PierceBonus = ParseInt(GetCol(h, c, "PierceBonus")),
                ProjSpeedPct = ParseFloat(GetCol(h, c, "ProjSpeedPct")),
                DefenseBonus = ParseFloat(GetCol(h, c, "DefenseBonus")),
                DmgBonusPct = ParseFloat(GetCol(h, c, "DmgBonusPct")),
                ArmorPenPct = ParseFloat(GetCol(h, c, "ArmorPenPct")),
                SkillDmgPct = ParseFloat(GetCol(h, c, "SkillDmgPct")),
                BurnDPS = ParseFloat(GetCol(h, c, "BurnDPS")),
                FreezeChance = ParseFloat(GetCol(h, c, "FreezeChance")),
                HealOnKill = ParseFloat(GetCol(h, c, "HealOnKill")),
                StackTag = GetCol(h, c, "StackTag"),
                MaxStack = ParseInt(GetCol(h, c, "MaxStack"), 99)
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

        private static SkillEffectRow ParseSkillEffectRow(string[] h, string[] c)
        {
            return new SkillEffectRow
            {
                ID = ParseInt(GetCol(h, c, "ID")),
                Name_CN = GetCol(h, c, "Name_CN"),
                Desc_CN = GetCol(h, c, "Desc_CN"),
                Type = ParseInt(GetCol(h, c, "Type")),
                BaseCooldown = ParseFloat(GetCol(h, c, "BaseCooldown")),
                BaseDamageRatio = ParseInt(GetCol(h, c, "BaseDamageRatio")),
                Charges = ParseInt(GetCol(h, c, "Charges")),
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
        private static void WriteJson<TRow>(string tableName, TRow[] rows, string jsonRoot = JsonRoot)
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

        private static int[] ParseIntArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
            return s.Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => int.TryParse(x.Trim(), out int v) ? v : 0)
                .ToArray();
        }

        private static float[] ParseFloatArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<float>();
            return s.Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => float.TryParse(x.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f)
                .ToArray();
        }

        private static string[] ParseStringArray(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            return s.Split('|').Select(x => x.Trim()).ToArray();
        }
    }
}
