using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V0.1.13 起始模板 —— 开局差异化的数据载体（取代旧「化身开局差异化」）。
    /// 一个模板打包：开局 3 个核心技能(Q/E/R) + 关联主角档案 + 起手模块列表。
    /// 资产放在 Resources/StartTemplates/ 下，由 <see cref="StartTemplateRegistry"/> 统一加载。
    /// </summary>
    [CreateAssetMenu(fileName = "StartTemplate", menuName = "仙途秘境/起始模板")]
    public class StartTemplate : ScriptableObject
    {
        [Header("标识")]
        public string templateId = "template";
        public string displayName = "模板";
        [TextArea] public string description = "";
        public Color themeColor = new Color(0.8f, 0.7f, 0.3f);
        [Tooltip("选择面板排序，越小越靠前")]
        public int sortOrder = 0;

        [Header("开局核心技能（Q / E / R）")]
        public SkillData skillQ;
        public SkillData skillE;
        public SkillData skillR;

        [Header("关联主角档案（外观 + 普攻形态，可空）")]
        public PlayerCharacterProfile profile;

        [Header("起手模块（进包，玩家可立即装配一条链）")]
        public ModuleDef[] startingModules;

        /// <summary>把模板应用到已构建的玩家（重装 3 技能 + 应用档案 + 发起手模块）。</summary>
        public void ApplyToPlayer()
        {
            var player = PlayerController.Instance;
            if (player == null)
            {
                Debug.LogWarning("[StartTemplate] 无玩家实例，无法应用模板");
                return;
            }

            if (profile != null)
            {
                PlayerCharacterRegistry.Selected = profile;
                player.ApplyCharacterProfile(profile);
            }

            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                if (skillQ != null) combat.EquipSkillQ(skillQ);
                if (skillE != null) combat.EquipSkillE(skillE);
                if (skillR != null) combat.EquipSkillR(skillR);
            }

            if (startingModules != null && startingModules.Length > 0)
            {
                var inv = player.GetComponent<ModuleInventory>();
                if (inv == null) inv = player.gameObject.AddComponent<ModuleInventory>();
                if (player.GetComponent<ModuleSlotManager>() == null)
                    player.gameObject.AddComponent<ModuleSlotManager>();
                foreach (var m in startingModules)
                    if (m != null) inv.Add(m);
            }

            Debug.Log($"<color=#66d9ff>[StartTemplate] 已应用起始模板：{displayName}（模块 {(startingModules != null ? startingModules.Length : 0)}）</color>");
        }
    }

    /// <summary>
    /// 起始模板注册表 —— 从 Resources/StartTemplates/ 加载全部模板，持有当前选择（静态，跨场景存活）。
    /// 与 <see cref="PlayerCharacterRegistry"/> 同生命周期模式。
    /// </summary>
    public static class StartTemplateRegistry
    {
        private static List<StartTemplate> _templates;
        private static StartTemplate _selected;

        public static IReadOnlyList<StartTemplate> All
        {
            get { EnsureLoaded(); return _templates; }
        }

        /// <summary>当前选择的起始模板（可能为 null，表示未选择 → Demo1Setup 退回默认分配）。</summary>
        public static StartTemplate Selected
        {
            get { EnsureLoaded(); return _selected; }
            set => _selected = value;
        }

        private static void EnsureLoaded()
        {
            if (_templates != null) return;
            _templates = new List<StartTemplate>();
            var loaded = Resources.LoadAll<StartTemplate>("StartTemplates");
            if (loaded != null) _templates.AddRange(loaded);
            _templates.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        }

        public static void Invalidate() => _templates = null;
    }
}
