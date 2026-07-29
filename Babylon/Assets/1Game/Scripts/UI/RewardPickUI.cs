using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 三选一奖励面板。
    /// 战斗/精英/事件房清场后弹出，展示 3 张同类卡牌（全技能 或 全模块）。
    /// 玩家必须选择一张或点「跳过」后才可进入下一关。
    /// 技能栏满时自动弹出替换确认，被替换技能按稀有度折算货币。
    /// </summary>
    public class RewardPickUI : MonoBehaviour
    {
        private static RewardPickUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _cardsRow;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _skipBtn;
        private VisualElement _replaceOverlay;
        private Label _replaceInfo;
        private Button _replaceYes;
        private Button _replaceNo;

        private Action _onDone;
        private SkillData[] _skillCandidates;
        private ModuleDef[] _moduleCandidates;
        private bool _isSkillReward;
        private SkillData _pendingReplaceSkill;
        private int _pendingReplaceSlot = -1;

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

            // 决定出技能还是模块（50/50，但如果某池为空则只出另一种）
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

            _instance._overlay.style.display = DisplayStyle.Flex;
            _instance._replaceOverlay.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void ForceHide()
        {
            if (_instance != null && _instance._overlay != null)
                _instance._overlay.style.display = DisplayStyle.None;
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
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 20f;

            var root = _doc.rootVisualElement;

            // 主遮罩
            _overlay = new VisualElement { name = "reward-overlay" };
            SetFull(_overlay);
            _overlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.92f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            _titleLabel = new Label("战利品");
            _titleLabel.style.fontSize = 32;
            _titleLabel.style.color = new Color(1f, 0.92f, 0.55f);
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.marginBottom = 4;
            _overlay.Add(_titleLabel);

            _subtitleLabel = new Label("选择一项奖励，或跳过");
            _subtitleLabel.style.fontSize = 14;
            _subtitleLabel.style.color = new Color(0.6f, 0.63f, 0.7f);
            _subtitleLabel.style.marginBottom = 20;
            _overlay.Add(_subtitleLabel);

            _cardsRow = new VisualElement { name = "cards" };
            _cardsRow.style.flexDirection = FlexDirection.Row;
            _cardsRow.style.justifyContent = Justify.Center;
            _overlay.Add(_cardsRow);

            _skipBtn = new Button(OnSkip) { text = "跳  过" };
            _skipBtn.style.marginTop = 24;
            _skipBtn.style.width = 160;
            _skipBtn.style.height = 40;
            _skipBtn.style.fontSize = 18;
            _skipBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            _skipBtn.style.color = new Color(0.7f, 0.7f, 0.75f);
            SetBorder(_skipBtn, 1, new Color(0.4f, 0.4f, 0.45f), 8);
            _overlay.Add(_skipBtn);

            // 替换确认遮罩（叠在主遮罩上）
            _replaceOverlay = new VisualElement { name = "replace-overlay" };
            SetFull(_replaceOverlay);
            _replaceOverlay.style.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);
            _replaceOverlay.style.alignItems = Align.Center;
            _replaceOverlay.style.justifyContent = Justify.Center;
            _replaceOverlay.style.display = DisplayStyle.None;
            root.Add(_replaceOverlay);

            _replaceInfo = new Label();
            _replaceInfo.style.fontSize = 18;
            _replaceInfo.style.color = Color.white;
            _replaceInfo.style.whiteSpace = WhiteSpace.Normal;
            _replaceInfo.style.maxWidth = 500;
            _replaceInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            _replaceInfo.style.marginBottom = 20;
            _replaceOverlay.Add(_replaceInfo);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            _replaceOverlay.Add(btnRow);

            _replaceYes = new Button(OnReplaceYes) { text = "确认替换" };
            _replaceYes.style.width = 140;
            _replaceYes.style.height = 38;
            _replaceYes.style.fontSize = 16;
            _replaceYes.style.marginRight = 16;
            _replaceYes.style.backgroundColor = new Color(0.7f, 0.35f, 0.2f, 0.9f);
            _replaceYes.style.color = Color.white;
            SetBorder(_replaceYes, 1, new Color(0.9f, 0.5f, 0.3f), 6);
            btnRow.Add(_replaceYes);

            _replaceNo = new Button(OnReplaceNo) { text = "取消" };
            _replaceNo.style.width = 140;
            _replaceNo.style.height = 38;
            _replaceNo.style.fontSize = 16;
            _replaceNo.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            _replaceNo.style.color = new Color(0.7f, 0.7f, 0.75f);
            SetBorder(_replaceNo, 1, new Color(0.4f, 0.4f, 0.45f), 6);
            btnRow.Add(_replaceNo);

            ChineseFontHelper.Apply(root);
        }

        // ==================== 填充卡牌 ====================

        private void PopulateSkills(SkillData[] skills)
        {
            _cardsRow.Clear();
            _titleLabel.text = "技能奖励";
            _subtitleLabel.text = "选择一个技能装备（已满则需替换）";
            foreach (var s in skills)
            {
                if (s == null) continue;
                _cardsRow.Add(BuildSkillCard(s));
            }
        }

        private void PopulateModules(ModuleDef[] modules)
        {
            _cardsRow.Clear();
            _titleLabel.text = "模块奖励";
            _subtitleLabel.text = "选择一个模块装备到增强链";
            foreach (var m in modules)
            {
                if (m == null) continue;
                _cardsRow.Add(BuildModuleCard(m));
            }
        }

        private VisualElement BuildSkillCard(SkillData skill)
        {
            var card = MakeCardBase();
            var tc = SkillTypeColor(skill.skillType);
            SetBorder(card, 2, new Color(tc.r, tc.g, tc.b, 0.85f), 10);

            AddLabel(card, skill.skillName, 22, tc, FontStyle.Bold, 6);
            AddLabel(card, $"{SkillTypeName(skill.skillType)} · {RarityName(skill.rarity)}", 12, new Color(0.55f, 0.58f, 0.65f), FontStyle.Normal, 8);

            if (!string.IsNullOrEmpty(skill.description))
            {
                var desc = AddLabel(card, skill.description, 13, new Color(0.72f, 0.74f, 0.80f), FontStyle.Normal, 10);
                desc.style.whiteSpace = WhiteSpace.Normal;
            }

            AddLabel(card, $"伤害: {skill.baseDamage:F0}  |  CD: {skill.cooldown:F1}s", 12, new Color(0.78f, 0.80f, 0.85f), FontStyle.Normal, 2);

            var pick = new Button(() => OnPickSkill(skill)) { text = "选择" };
            StylePickButton(pick, tc);
            card.Add(pick);

            return card;
        }

        private VisualElement BuildModuleCard(ModuleDef mod)
        {
            var card = MakeCardBase();
            var rc = RarityColor(mod.rarity);
            SetBorder(card, 2, new Color(rc.r, rc.g, rc.b, 0.85f), 10);

            AddLabel(card, mod.displayName, 22, rc, FontStyle.Bold, 6);
            AddLabel(card, $"{CategoryName(mod.category)} · {RarityName(mod.rarity)}", 12, new Color(0.55f, 0.58f, 0.65f), FontStyle.Normal, 8);

            if (!string.IsNullOrEmpty(mod.description))
            {
                var desc = AddLabel(card, mod.description, 13, new Color(0.72f, 0.74f, 0.80f), FontStyle.Normal, 10);
                desc.style.whiteSpace = WhiteSpace.Normal;
            }

            var pick = new Button(() => OnPickModule(mod)) { text = "选择" };
            StylePickButton(pick, rc);
            card.Add(pick);

            return card;
        }

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
                // 技能栏满，弹替换确认
                ShowReplaceConfirm(skill);
            }
        }

        private void ShowReplaceConfirm(SkillData newSkill)
        {
            _pendingReplaceSkill = newSkill;
            _replaceOverlay.style.display = DisplayStyle.Flex;

            // 构建替换选择行
            _replaceOverlay.Clear();

            var title = new Label("技能栏已满 —— 选择一个槽位替换");
            title.style.fontSize = 22;
            title.style.color = new Color(1f, 0.85f, 0.4f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            _replaceOverlay.Add(title);

            var newInfo = new Label($"新技能：{newSkill.skillName}（{RarityName(newSkill.rarity)}）");
            newInfo.style.fontSize = 16;
            newInfo.style.color = new Color(0.5f, 1f, 0.7f);
            newInfo.style.marginBottom = 16;
            _replaceOverlay.Add(newInfo);

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            string[] slotNames = { "Q", "E", "R" };
            var slotRow = new VisualElement();
            slotRow.style.flexDirection = FlexDirection.Row;
            slotRow.style.justifyContent = Justify.Center;
            _replaceOverlay.Add(slotRow);

            for (int i = 0; i < 3; i++)
            {
                var sk = combat?.GetSkillInSlot(i);
                if (sk == null) continue;

                int slot = i;
                int refundShards = PlayerResources.GetDecomposeShards(sk.rarity);

                var btn = new Button(() => ConfirmReplace(slot))
                {
                    text = $"替换 [{slotNames[slot]}] {sk.skillName}\n→ 折算 ✦{refundShards}"
                };
                btn.style.width = 200;
                btn.style.height = 70;
                btn.style.fontSize = 14;
                btn.style.marginLeft = 8;
                btn.style.marginRight = 8;
                btn.style.backgroundColor = new Color(0.15f, 0.15f, 0.22f, 0.9f);
                btn.style.color = Color.white;
                btn.style.whiteSpace = WhiteSpace.Normal;
                SetBorder(btn, 1, RarityColor(sk.rarity), 8);
                slotRow.Add(btn);
            }

            var cancelBtn = new Button(OnReplaceNo) { text = "取消替换" };
            cancelBtn.style.marginTop = 16;
            cancelBtn.style.width = 160;
            cancelBtn.style.height = 38;
            cancelBtn.style.fontSize = 16;
            cancelBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            cancelBtn.style.color = new Color(0.7f, 0.7f, 0.75f);
            SetBorder(cancelBtn, 1, new Color(0.4f, 0.4f, 0.45f), 6);
            _replaceOverlay.Add(cancelBtn);

            ChineseFontHelper.Apply(_replaceOverlay);
        }

        private void ConfirmReplace(int slot)
        {
            if (_pendingReplaceSkill == null) { OnReplaceNo(); return; }

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) { OnReplaceNo(); return; }

            var oldSkill = combat.GetSkillInSlot(slot);
            int refund = oldSkill != null ? PlayerResources.GetDecomposeShards(oldSkill.rarity) : 0;

            combat.EquipSkillToSlot(_pendingReplaceSkill, slot);
            GameEvents.Publish(new GameEvents.SkillEquipped
            {
                Skill = _pendingReplaceSkill,
                SlotIndex = slot
            });

            if (refund > 0 && PlayerResources.Instance != null)
            {
                PlayerResources.Instance.AddShards(refund);
                Debug.Log($"<color=#ffcc33>[RewardPick] 替换技能，旧 {oldSkill.skillName} 折算 ✦{refund} 碎片</color>");
            }

            Debug.Log($"<color=#66ff99>[RewardPick] 替换装备 {_pendingReplaceSkill.skillName} → 槽位 {slot}</color>");
            _pendingReplaceSkill = null;
            _replaceOverlay.style.display = DisplayStyle.None;
            Close();
        }

        private void OnReplaceYes()
        {
            if (_pendingReplaceSlot >= 0)
                ConfirmReplace(_pendingReplaceSlot);
        }

        private void OnReplaceNo()
        {
            _pendingReplaceSkill = null;
            _replaceOverlay.style.display = DisplayStyle.None;
        }

        private void OnPickModule(ModuleDef mod)
        {
            var player = PlayerController.Instance;
            if (player == null) { Close(); return; }

            // 直接装备到 ModuleSlotManager —— 找到第一个空链槽或有空位的链
            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots != null)
            {
                bool equipped = TryAutoEquipModule(slots, mod);
                if (!equipped)
                {
                    // 无法自动装配，通知玩家打开装配 UI 手动处理
                    Debug.Log($"<color=#ffcc33>[RewardPick] 获得模块 {mod.displayName}，请打开装配界面 [M] 手动装配</color>");
                }
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
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            if (_replaceOverlay != null) _replaceOverlay.style.display = DisplayStyle.None;
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke();
        }

        // ==================== 样式工具 ====================

        private static VisualElement MakeCardBase()
        {
            var card = new VisualElement();
            card.style.width = 230;
            card.style.marginLeft = 12;
            card.style.marginRight = 12;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.backgroundColor = new Color(0.10f, 0.12f, 0.17f, 1f);
            return card;
        }

        private static Label AddLabel(VisualElement parent, string text, int fontSize, Color color, FontStyle style, float marginBottom)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = style;
            l.style.marginBottom = marginBottom;
            parent.Add(l);
            return l;
        }

        private static void StylePickButton(Button btn, Color accent)
        {
            btn.style.marginTop = 14;
            btn.style.height = 36;
            btn.style.fontSize = 16;
            btn.style.backgroundColor = new Color(accent.r * 0.4f, accent.g * 0.4f, accent.b * 0.4f, 0.9f);
            btn.style.color = Color.white;
            SetBorder(btn, 1, accent, 6);
        }

        private static void SetFull(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0;
            e.style.top = 0; e.style.bottom = 0;
        }

        private static void SetBorder(VisualElement e, float w, Color c, float r)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

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
