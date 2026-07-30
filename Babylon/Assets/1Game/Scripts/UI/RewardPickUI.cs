using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 三选一奖励面板（V0.4.6 改 uGUI+TMP）。
    /// 战斗/精英/事件房清场后弹出，展示 3 张同类卡牌（全技能 或 全模块）。
    /// 玩家必须选择一张或点「跳过」后才可进入下一关。
    /// 技能栏满时自动弹出替换确认，被替换技能按稀有度折算货币。
    /// </summary>
    public class RewardPickUI : MonoBehaviour
    {
        private static RewardPickUI _instance;

        private GameObject _root;
        private RectTransform _cardsRow;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _subtitleLabel;

        private GameObject _replaceRoot;
        private RectTransform _replaceContent;

        private Action _onDone;
        private SkillData[] _skillCandidates;
        private ModuleDef[] _moduleCandidates;
        private bool _isSkillReward;
        private SkillData _pendingReplaceSkill;

        /// <summary>
        /// 判定是否触发奖励并弹出 UI。
        /// 战斗房 70%、精英房 90%、事件房 100%。
        /// 无论是否触发，都会在完成后回调 onDone。
        /// </summary>
        public static void TryShow(bool isElite, bool isEvent,
            SkillData[] skillPool, ModuleDef[] modulePool, int rarityBias,
            Action onDone)
        {
            float chance = isEvent ? 1f : (isElite ? 0.9f : 0.7f);
            if (UnityEngine.Random.value > chance)
            {
                onDone?.Invoke();
                return;
            }

            bool canSkill = skillPool != null && skillPool.Length > 0;
            bool canModule = modulePool != null && modulePool.Length > 0;
            if (!canSkill && !canModule)
            {
                onDone?.Invoke();
                return;
            }

            bool pickSkill;
            if (!canSkill) pickSkill = false;
            else if (!canModule) pickSkill = true;
            else pickSkill = UnityEngine.Random.value < 0.5f;

            EnsureInstance();
            _instance._onDone = onDone;
            _instance._isSkillReward = pickSkill;

            if (pickSkill)
            {
                _instance._skillCandidates = PickRandomSkills(skillPool, 3);
                _instance._moduleCandidates = null;
                _instance.PopulateSkills(_instance._skillCandidates);
            }
            else
            {
                _instance._moduleCandidates = PickRandomModules(modulePool, 3, rarityBias);
                _instance._skillCandidates = null;
                _instance.PopulateModules(_instance._moduleCandidates);
            }

            _instance._root.SetActive(true);
            _instance._replaceRoot.SetActive(false);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void ForceHide()
        {
            if (_instance != null && _instance._root != null)
                _instance._root.SetActive(false);
        }

        // ==================== 随机选取 ====================

        private static SkillData[] PickRandomSkills(SkillData[] pool, int count)
        {
            var list = new List<SkillData>();
            foreach (var s in pool)
                if (s != null) list.Add(s);
            Shuffle(list);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list.ToArray();
        }

        private static ModuleDef[] PickRandomModules(ModuleDef[] pool, int count, int rarityBias)
        {
            var picked = new List<ModuleDef>();
            for (int i = 0; i < count; i++)
            {
                var m = ModuleDropWeighting.PickWeighted(pool, rarityBias);
                if (m != null) picked.Add(m);
            }
            return picked.ToArray();
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ==================== 构建 UI ====================

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("RewardPickUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RewardPickUI>();
            _instance.Build();
        }

        private void Build()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("RewardPickCanvas", 130, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.03f, 0.06f, 0.92f));

            var center = UGuiKit.CreatePanel(_root.transform, "Center", new Vector2(1000f, 10f), new Color(0, 0, 0, 0));
            var fit = center.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(center, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter, false, false);
            center.gameObject.GetComponent<VerticalLayoutGroup>().childControlWidth = false;

            _titleLabel = UGuiKit.CreateText(center, "战利品", 34, UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(_titleLabel, 46f);
            _subtitleLabel = UGuiKit.CreateText(center, "选择一项奖励，或跳过", 16, UGuiKit.TextDim, TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_subtitleLabel, 26f);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(center, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 16f;

            _cardsRow = UGuiKit.CreateCardRow(center, 24f);

            var spacer2 = new GameObject("Spacer2", typeof(RectTransform), typeof(LayoutElement));
            spacer2.transform.SetParent(center, false);
            spacer2.GetComponent<LayoutElement>().preferredHeight = 20f;

            var skip = UGuiKit.CreateButton(center, "跳  过", OnSkip, new Color(0.25f, 0.25f, 0.3f, 0.9f), 18, new Vector2(180f, 44f));
            UGuiKit.SetHeight(skip.GetComponent<RectTransform>(), 44f);

            BuildReplaceRoot();
        }

        private void BuildReplaceRoot()
        {
            _replaceRoot = UGuiKit.CreateStretch(_root.transform, "ReplaceRoot").gameObject;
            UGuiKit.CreateScrim(_replaceRoot.transform, new Color(0.02f, 0.02f, 0.05f, 0.95f));
            var holder = UGuiKit.CreatePanel(_replaceRoot.transform, "Holder", new Vector2(920f, 10f), new Color(0, 0, 0, 0));
            var fit = holder.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(holder, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter, false, false);
            holder.gameObject.GetComponent<VerticalLayoutGroup>().childControlWidth = false;
            _replaceContent = holder;
            _replaceRoot.SetActive(false);
        }

        // ==================== 填充卡牌 ====================

        private void ClearRow()
        {
            for (int i = _cardsRow.childCount - 1; i >= 0; i--) Destroy(_cardsRow.GetChild(i).gameObject);
        }

        private void PopulateSkills(SkillData[] skills)
        {
            ClearRow();
            _titleLabel.text = "技能奖励";
            _subtitleLabel.text = "选择一个技能装备（已满则需替换）";
            foreach (var s in skills)
            {
                if (s == null) continue;
                BuildSkillCard(s);
            }
        }

        private void PopulateModules(ModuleDef[] modules)
        {
            ClearRow();
            _titleLabel.text = "模块奖励";
            _subtitleLabel.text = "选择一个模块装备到增强链";
            foreach (var m in modules)
            {
                if (m == null) continue;
                BuildModuleCard(m);
            }
        }

        private void BuildSkillCard(SkillData skill)
        {
            var tc = SkillTypeColor(skill.skillType);
            var card = UGuiKit.CreateCard(_cardsRow, new Vector2(240f, 320f), tc);

            var name = UGuiKit.CreateText(card, skill.skillName, 22, tc, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(name, 30f);
            var type = UGuiKit.CreateText(card, $"{SkillTypeName(skill.skillType)} · {RarityName(skill.rarity)}", 12, new Color(0.55f, 0.58f, 0.65f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(type, 20f);

            var desc = UGuiKit.CreateText(card, string.IsNullOrEmpty(skill.description) ? "" : skill.description, 13, new Color(0.72f, 0.74f, 0.80f), TextAlignmentOptions.Top);
            desc.enableWordWrapping = true;
            var dle = desc.gameObject.AddComponent<LayoutElement>(); dle.flexibleHeight = 1f; dle.minHeight = 40f;

            var stat = UGuiKit.CreateText(card, $"伤害 {skill.baseDamage:F0}  |  CD {skill.cooldown:F1}s", 12, new Color(0.78f, 0.80f, 0.85f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(stat, 20f);

            var pick = UGuiKit.CreateButton(card, "选择", () => OnPickSkill(skill), CardBtnColor(tc), 16, new Vector2(200f, 38f));
            UGuiKit.SetHeight(pick.GetComponent<RectTransform>(), 38f);
        }

        private void BuildModuleCard(ModuleDef mod)
        {
            var rc = RarityColor(mod.rarity);
            var card = UGuiKit.CreateCard(_cardsRow, new Vector2(240f, 320f), rc);

            var name = UGuiKit.CreateText(card, mod.displayName, 22, rc, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(name, 30f);
            var cat = UGuiKit.CreateText(card, $"{CategoryName(mod.category)} · {RarityName(mod.rarity)}", 12, new Color(0.55f, 0.58f, 0.65f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(cat, 20f);

            var desc = UGuiKit.CreateText(card, string.IsNullOrEmpty(mod.description) ? "" : mod.description, 13, new Color(0.72f, 0.74f, 0.80f), TextAlignmentOptions.Top);
            desc.enableWordWrapping = true;
            var dle = desc.gameObject.AddComponent<LayoutElement>(); dle.flexibleHeight = 1f; dle.minHeight = 60f;

            var pick = UGuiKit.CreateButton(card, "选择", () => OnPickModule(mod), CardBtnColor(rc), 16, new Vector2(200f, 38f));
            UGuiKit.SetHeight(pick.GetComponent<RectTransform>(), 38f);
        }

        private static Color CardBtnColor(Color accent) => new Color(accent.r * 0.4f, accent.g * 0.4f, accent.b * 0.4f, 0.95f);

        // ==================== 选取回调 ====================

        private void OnPickSkill(SkillData skill)
        {
            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) { Close(); return; }

            int empty = combat.FindEmptySlot();
            if (empty >= 0)
            {
                combat.EquipSkillToSlot(skill, empty);
                GameEvents.Publish(new GameEvents.SkillEquipped { Skill = skill, SlotIndex = empty });
                Debug.Log($"<color=#66ff99>[RewardPick] 装备技能 {skill.skillName} → 槽位 {empty}</color>");
                Close();
            }
            else
            {
                ShowReplaceConfirm(skill);
            }
        }

        private void ShowReplaceConfirm(SkillData newSkill)
        {
            _pendingReplaceSkill = newSkill;
            _replaceRoot.SetActive(true);

            for (int i = _replaceContent.childCount - 1; i >= 0; i--) Destroy(_replaceContent.GetChild(i).gameObject);

            var title = UGuiKit.CreateText(_replaceContent, "技能栏已满 —— 选择一个槽位替换", 22, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 34f);
            var newInfo = UGuiKit.CreateText(_replaceContent, $"新技能：{newSkill.skillName}（{RarityName(newSkill.rarity)}）", 16, new Color(0.5f, 1f, 0.7f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(newInfo, 26f);

            var slotRow = UGuiKit.CreateCardRow(_replaceContent, 16f);
            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            string[] slotNames = { "Q", "E", "R" };
            for (int i = 0; i < 3; i++)
            {
                var sk = combat?.GetSkillInSlot(i);
                if (sk == null) continue;
                int slot = i;
                int refundShards = PlayerResources.GetDecomposeShards(sk.rarity);
                var btn = UGuiKit.CreateButton(slotRow, $"替换 [{slotNames[slot]}] {sk.skillName}\n→ 折算 ✦{refundShards}",
                    () => ConfirmReplace(slot), new Color(0.15f, 0.15f, 0.22f, 0.95f), 14, new Vector2(200f, 70f));
                UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 70f);
            }

            var cancelBtn = UGuiKit.CreateButton(_replaceContent, "取消替换", OnReplaceNo, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 40f));
            UGuiKit.SetHeight(cancelBtn.GetComponent<RectTransform>(), 40f);
        }

        private void ConfirmReplace(int slot)
        {
            if (_pendingReplaceSkill == null) { OnReplaceNo(); return; }

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) { OnReplaceNo(); return; }

            var oldSkill = combat.GetSkillInSlot(slot);
            int refund = oldSkill != null ? PlayerResources.GetDecomposeShards(oldSkill.rarity) : 0;

            combat.EquipSkillToSlot(_pendingReplaceSkill, slot);
            GameEvents.Publish(new GameEvents.SkillEquipped { Skill = _pendingReplaceSkill, SlotIndex = slot });

            if (refund > 0 && PlayerResources.Instance != null)
            {
                PlayerResources.Instance.AddShards(refund);
                Debug.Log($"<color=#ffcc33>[RewardPick] 替换技能，旧 {oldSkill.skillName} 折算 ✦{refund} 碎片</color>");
            }

            Debug.Log($"<color=#66ff99>[RewardPick] 替换装备 {_pendingReplaceSkill.skillName} → 槽位 {slot}</color>");
            _pendingReplaceSkill = null;
            _replaceRoot.SetActive(false);
            Close();
        }

        private void OnReplaceNo()
        {
            _pendingReplaceSkill = null;
            _replaceRoot.SetActive(false);
        }

        private void OnPickModule(ModuleDef mod)
        {
            var player = PlayerController.Instance;
            if (player == null) { Close(); return; }

            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots != null)
            {
                bool equipped = TryAutoEquipModule(slots, mod);
                if (!equipped)
                    Debug.Log($"<color=#ffcc33>[RewardPick] 获得模块 {mod.displayName}，请打开装配界面 [M] 手动装配</color>");
            }

            Debug.Log($"<color=#66ff99>[RewardPick] 选择模块 {mod.displayName}（{mod.category}）</color>");
            Close();
        }

        /// <summary>尝试自动装备模块到增强链。返回是否成功。供商店等外部调用。</summary>
        public static bool TryAutoEquipModule(ModuleSlotManager slots, ModuleDef mod)
        {
            for (int s = 0; s < 3; s++)
            {
                var chain = slots.GetChain(s);
                if (chain == null) chain = new ModuleChain();

                bool modified = false;
                switch (mod.category)
                {
                    case ModuleCategory.Trigger:
                    case ModuleCategory.Universal when chain.trigger == null:
                        if (chain.trigger == null)
                        {
                            chain.trigger = mod;
                            modified = true;
                        }
                        break;
                    case ModuleCategory.Effect:
                        if (chain.effect == null)
                        {
                            chain.effect = mod;
                            modified = true;
                        }
                        break;
                    case ModuleCategory.Modifier:
                        if (chain.modifier0 == null)
                        {
                            chain.modifier0 = mod;
                            modified = true;
                        }
                        else if (chain.modifier1 == null)
                        {
                            chain.modifier1 = mod;
                            modified = true;
                        }
                        break;
                }

                if (modified)
                {
                    slots.EquipChain(s, chain);
                    return true;
                }
            }
            return false;
        }

        private void OnSkip()
        {
            Debug.Log("<color=#aaaaaa>[RewardPick] 玩家跳过奖励</color>");
            Close();
        }

        private void Close()
        {
            if (_root != null) _root.SetActive(false);
            if (_replaceRoot != null) _replaceRoot.SetActive(false);
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke();
        }

        // ==================== 颜色 / 名称 ====================

        private static Color SkillTypeColor(SkillType t) => t switch
        {
            SkillType.AreaDamage => new Color(0.95f, 0.55f, 0.35f),
            SkillType.Projectile => new Color(0.45f, 0.75f, 1.0f),
            SkillType.Dash => new Color(0.50f, 0.95f, 0.65f),
            SkillType.Buff => new Color(1.0f, 0.85f, 0.30f),
            SkillType.Heal => new Color(0.45f, 0.95f, 0.45f),
            SkillType.Summon => new Color(0.80f, 0.55f, 0.95f),
            SkillType.Zone => new Color(0.90f, 0.45f, 0.60f),
            _ => new Color(0.75f, 0.75f, 0.80f),
        };

        private static string SkillTypeName(SkillType t) => t switch
        {
            SkillType.AreaDamage => "范围伤害", SkillType.Projectile => "投射物",
            SkillType.Dash => "位移", SkillType.Buff => "增益",
            SkillType.Heal => "治疗", SkillType.Summon => "召唤",
            SkillType.Zone => "持续区域", _ => "技能",
        };

        private static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Fan => Color.white,
            ItemRarity.Ling => Color.green,
            ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
            ItemRarity.Tian => new Color(1f, 0.85f, 0f),
            _ => Color.white
        };

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡品", ItemRarity.Ling => "灵品",
            ItemRarity.Xuan => "玄品", ItemRarity.Di => "地品",
            ItemRarity.Tian => "天品", _ => "凡品"
        };

        private static string CategoryName(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => "触发器", ModuleCategory.Effect => "效果器",
            ModuleCategory.Modifier => "改造件", ModuleCategory.Universal => "万能件",
            _ => "模块"
        };
    }
}
