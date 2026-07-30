using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 Build 快照 —— 序列化玩家在局内构筑的完整 Build。
    /// 通关或主动退出时保存，存放在局外「Build 背包」中，用于大秘境装备。
    /// 只保存已装配部分（3 技能 + 3 增强链），不含背包散件。
    /// </summary>
    [Serializable]
    public class BuildSnapshot
    {
        public string buildName = "";
        public long savedTimestamp;

        // 3 技能槽位（通过 SkillData.skillName 引用，运行时从池中查找还原）
        public string skillQ;
        public string skillE;
        public string skillR;

        // 3 条增强链（每条最多 4 个模块：trigger + effect + modifier0 + modifier1）
        public ChainSnapshot chain0 = new();
        public ChainSnapshot chain1 = new();
        public ChainSnapshot chain2 = new();

        /// <summary>从当前玩家状态抓取快照</summary>
        public static BuildSnapshot CaptureFromPlayer()
        {
            var snap = new BuildSnapshot
            {
                savedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                snap.skillQ = combat.GetSkillInSlot(0)?.skillName ?? "";
                snap.skillE = combat.GetSkillInSlot(1)?.skillName ?? "";
                snap.skillR = combat.GetSkillInSlot(2)?.skillName ?? "";
            }

            var slots = PlayerController.Instance?.GetComponent<ModuleSlotManager>();
            if (slots != null)
            {
                snap.chain0 = ChainSnapshot.FromChain(slots.GetChain(0));
                snap.chain1 = ChainSnapshot.FromChain(slots.GetChain(1));
                snap.chain2 = ChainSnapshot.FromChain(slots.GetChain(2));
            }

            // 自动命名
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(snap.skillQ)) parts.Add(snap.skillQ);
            if (!string.IsNullOrEmpty(snap.skillE)) parts.Add(snap.skillE);
            if (!string.IsNullOrEmpty(snap.skillR)) parts.Add(snap.skillR);
            snap.buildName = parts.Count > 0
                ? string.Join("+", parts)
                : $"Build_{DateTime.Now:HHmm}";

            return snap;
        }

        /// <summary>判断快照是否为空（无技能、无链）</summary>
        public bool IsEmpty =>
            string.IsNullOrEmpty(skillQ) && string.IsNullOrEmpty(skillE) && string.IsNullOrEmpty(skillR)
            && chain0.IsEmpty && chain1.IsEmpty && chain2.IsEmpty;

        /// <summary>简短描述（用于 UI 展示）</summary>
        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(skillQ)) parts.Add($"Q:{skillQ}");
                if (!string.IsNullOrEmpty(skillE)) parts.Add($"E:{skillE}");
                if (!string.IsNullOrEmpty(skillR)) parts.Add($"R:{skillR}");
                int chainCount = 0;
                if (!chain0.IsEmpty) chainCount++;
                if (!chain1.IsEmpty) chainCount++;
                if (!chain2.IsEmpty) chainCount++;
                if (chainCount > 0) parts.Add($"链×{chainCount}");
                return parts.Count > 0 ? string.Join("  ", parts) : "空 Build";
            }
        }

        /// <summary>
        /// V0.4.1：把此 Build 快照装备到当前玩家（用于大秘境）。
        /// 从 Resources 还原技能（按 skillName）与模块链（按 moduleId）。
        /// </summary>
        public void ApplyToPlayer()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            var skillPool = LoadAllSkills();
            var modulePool = ModulePoolLoader.LoadAll();

            // 技能
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.EquipSkillToSlot(FindSkill(skillPool, skillQ), 0);
                combat.EquipSkillToSlot(FindSkill(skillPool, skillE), 1);
                combat.EquipSkillToSlot(FindSkill(skillPool, skillR), 2);

                // #5：装备 Build 后广播技能变更 → 下方技能栏(SkillBarUI)/HUD 同步刷新，
                //     让玩家能直观确认「已替换成功」。EquipSkillToSlot 自身不发此事件。
                for (int i = 0; i < 3; i++)
                    GameEvents.Publish(new GameEvents.SkillEquipped { Skill = combat.GetSkillInSlot(i), SlotIndex = i });
            }

            // 增强链
            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots != null)
            {
                slots.ClearAll();
                var c0 = chain0.ToChain(modulePool);
                var c1 = chain1.ToChain(modulePool);
                var c2 = chain2.ToChain(modulePool);
                if (c0 != null && c0.IsValid) slots.EquipChain(0, c0);
                if (c1 != null && c1.IsValid) slots.EquipChain(1, c1);
                if (c2 != null && c2.IsValid) slots.EquipChain(2, c2);
            }

            // #5：技能栏兜底刷新（即使无 SkillBarUI 事件订阅者也能对上）。
            if (SkillBarUI.Instance != null) SkillBarUI.Instance.RefreshSkillSlots();

            Debug.Log($"<color=#00ffcc>[BuildSnapshot] 已装备 Build「{buildName}」到玩家</color>");
        }

        private static SkillData FindSkill(SkillData[] pool, string skillName)
        {
            if (string.IsNullOrEmpty(skillName) || pool == null) return null;
            foreach (var s in pool)
                if (s != null && s.skillName == skillName) return s;
            return null;
        }

        private static SkillData[] LoadAllSkills()
        {
            var skills = Resources.LoadAll<SkillData>("Skills");
            if (skills == null || skills.Length == 0)
                skills = Resources.LoadAll<SkillData>("");
            return skills ?? new SkillData[0];
        }
    }

    /// <summary>单条增强链的序列化快照</summary>
    [Serializable]
    public class ChainSnapshot
    {
        public string triggerId = "";
        public string effectId = "";
        public string modifier0Id = "";
        public string modifier1Id = "";

        public bool IsEmpty => string.IsNullOrEmpty(triggerId) && string.IsNullOrEmpty(effectId);

        public static ChainSnapshot FromChain(ModuleChain chain)
        {
            if (chain == null) return new ChainSnapshot();
            return new ChainSnapshot
            {
                triggerId = chain.trigger != null ? chain.trigger.moduleId : "",
                effectId = chain.effect != null ? chain.effect.moduleId : "",
                modifier0Id = chain.modifier0 != null ? chain.modifier0.moduleId : "",
                modifier1Id = chain.modifier1 != null ? chain.modifier1.moduleId : "",
            };
        }

        /// <summary>从模块池中还原 ModuleChain（找不到的模块跳过）</summary>
        public ModuleChain ToChain(ModuleDef[] pool)
        {
            if (IsEmpty || pool == null) return null;

            var chain = new ModuleChain
            {
                trigger = FindModule(pool, triggerId),
                effect = FindModule(pool, effectId),
                modifier0 = FindModule(pool, modifier0Id),
                modifier1 = FindModule(pool, modifier1Id),
            };
            return chain;
        }

        private static ModuleDef FindModule(ModuleDef[] pool, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in pool)
                if (m != null && m.moduleId == id) return m;
            return null;
        }
    }
}
