using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 掉落功法的世界拾取物（v0.5.5 重构为 <see cref="PickupBase"/> 子类）。
    /// 靠近显示提示；[F] 装备（空槽直接装，满槽弹 Q/E/R 换槽模态）；长按 [F] 分解。
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SkillPickup : PickupBase
    {
        public override int InteractionPriority => 25;   // 高于灵物(20) / 低于商店(40)
        protected override float BobSpeed => 1.5f;
        protected override float RotateSpeed => 60f;

        [Header("数据")]
        public SkillData skillData;

        private PlayerCombat _combat;
        private bool _waitingForSlotChoice;   // 满槽时进入换槽选择模态
        private GameObject _slotChoiceUI;

        protected override bool HasTarget => _combat != null;
        protected override bool KeepActiveOverride => _waitingForSlotChoice;

        protected override bool AcquireTarget(Collider other)
        {
            _combat = other.GetComponent<PlayerCombat>();
            return _combat != null;
        }

        protected override void ReleaseTarget()
        {
            _combat = null;
            _waitingForSlotChoice = false;
            HideSlotChoiceUI();
        }

        protected override void SetupVisual()
        {
            if (skillData == null) return;
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color rarityColor = GetRarityColor(skillData.rarity);
                MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.8f);
            }
        }

        protected override PickupPromptData BuildPromptData()
        {
            string typeStr = skillData.skillType switch
            {
                SkillType.AreaDamage => "范围伤害",
                SkillType.Projectile => "弹道",
                SkillType.Dash => "位移",
                SkillType.Buff => "增益",
                SkillType.Heal => "治疗",
                SkillType.Summon => "召唤",
                SkillType.AvatarSpecial => "化身专属",
                _ => "其他"
            };

            bool isExclusive = skillData.skillType == SkillType.AvatarSpecial;
            string exclusiveTag = isExclusive ? " [专属]" : "";
            string titleStr = $"{skillData.skillName}{exclusiveTag}（{GetRarityName(skillData.rarity)}）";

            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            string hint;
            if (isExclusive && !IsCurrentAvatarMatch())
                hint = "<color=#ff6666>化身不符，无法装备</color>  |  长按[F] 分解";
            else
                hint = $"[F] 装备  |  长按[F] 分解（{shards} 灵力碎片）";

            return new PickupPromptData
            {
                title = titleStr,
                titleColor = GetRarityColor(skillData.rarity),
                subLine = $"类型：{typeStr}  |  CD：{skillData.cooldown}s  |  伤害：{skillData.baseDamage}",
                subColor = new Color(0.5f, 0.9f, 0.5f, 0.9f),
                desc = skillData.description,
                promptHint = hint
            };
        }

        private bool IsCurrentAvatarMatch()
        {
            if (skillData.skillType != SkillType.AvatarSpecial) return true;
            if (skillData.RequiredRoot == SpiritRootType.None) return true;
            var pc = PlayerController.Instance;
            var root = pc != null ? pc.GetComponent<SpiritRootController>() : null;
            return root != null && root.CurrentRoot == skillData.RequiredRoot;
        }

        // 换槽模态中：拦截 Q/E/R/Esc，跳过本帧 F 逻辑
        protected override bool HandleExtraInput(Keyboard kb)
        {
            if (!_waitingForSlotChoice) return false;
            if (kb.qKey.wasPressedThisFrame) ConfirmSlotReplace(0);
            else if (kb.eKey.wasPressedThisFrame) ConfirmSlotReplace(1);
            else if (kb.rKey.wasPressedThisFrame) ConfirmSlotReplace(2);
            else if (kb.escapeKey.wasPressedThisFrame) CancelSlotChoice();
            return true;
        }

        protected override void OnPrimaryAction() => TryPickup();
        protected override void OnDecomposeAction() => Decompose();

        /// <summary>尝试拾取：有空槽直接装备；满槽则弹出 Q/E/R 换槽模态。</summary>
        private void TryPickup()
        {
            if (skillData == null || _combat == null) return;

            if (!IsCurrentAvatarMatch())
            {
                Debug.Log($"<color=#ff6666>[SkillPickup] {skillData.skillName} 为化身专属（需 {skillData.RequiredRoot}），当前化身不符 → 拒绝装备</color>");
                return;
            }

            int emptySlot = _combat.FindEmptySlot();
            if (emptySlot >= 0)
            {
                _combat.EquipSkillToSlot(skillData, emptySlot);
                Debug.Log($"<color=cyan>装备功法：{skillData.skillName} → {GetSlotKeyName(emptySlot)} 槽</color>");
                GameEvents.Publish(new GameEvents.SkillEquipped { Skill = skillData, SlotIndex = emptySlot });
                OnPickedUp();
            }
            else
            {
                ShowSlotChoiceUI();
            }
        }

        private void ShowSlotChoiceUI()
        {
            if (_waitingForSlotChoice) return;
            _waitingForSlotChoice = true;

            // 主提示改为换槽说明
            if (_prompt?.promptText != null)
                _prompt.promptText.text = "背包已满 · 选择替换槽位：[Q] [E] [R]  |  [Esc] 取消";

            // 单独的换槽选择面板（不挂父物体，避免继承拾取物旋转/缩放）
            var canvasGo = new GameObject("SlotChoiceCanvas");
            canvasGo.transform.position = new Vector3(_startPos.x, _startPos.y + 3.2f, _startPos.z);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 201;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 80);
            rt.localScale = Vector3.one * 0.00875f;

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.1f, 0.05f, 0.05f, 0.9f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 4);
            textRt.offsetMax = new Vector2(-8, -4);
            var text = textGo.AddComponent<Text>();

            string qName = _combat.GetSkillInSlot(0)?.skillName ?? "空";
            string eName = _combat.GetSkillInSlot(1)?.skillName ?? "空";
            string rName = _combat.GetSkillInSlot(2)?.skillName ?? "空";
            text.text = $"[Q]{qName}  [E]{eName}  [R]{rName}  [Esc]取消";
            text.fontSize = 22;
            text.font = UIBuiltins.LegacyFont;
            text.color = new Color(1f, 0.8f, 0.3f, 1f);
            text.alignment = TextAnchor.MiddleCenter;

            canvasGo.AddComponent<BillboardUI>().lerpFactor = 0.5f;
            _slotChoiceUI = canvasGo;
        }

        private void ConfirmSlotReplace(int slotIndex)
        {
            if (_combat == null) return;

            SkillData oldSkill = _combat.EquipSkillToSlot(skillData, slotIndex);
            Debug.Log($"<color=cyan>替换功法：{skillData.skillName} → {GetSlotKeyName(slotIndex)} 槽（替下 {oldSkill?.skillName ?? "空"}）</color>");
            GameEvents.Publish(new GameEvents.SkillEquipped { Skill = skillData, SlotIndex = slotIndex });

            if (oldSkill != null)
            {
                Vector3 dropPos = transform.position + Random.insideUnitSphere * 1.5f;
                dropPos.y = _startPos.y;
                Spawn(oldSkill, dropPos);
            }

            HideSlotChoiceUI();
            OnPickedUp();
        }

        private void CancelSlotChoice()
        {
            _waitingForSlotChoice = false;
            HideSlotChoiceUI();
            if (_prompt?.promptText != null)
            {
                int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
                _prompt.promptText.text = $"[F] 装备  |  长按[F] 分解（{shards} 灵力碎片）";
            }
        }

        private void HideSlotChoiceUI()
        {
            if (_slotChoiceUI != null)
            {
                Destroy(_slotChoiceUI);
                _slotChoiceUI = null;
            }
        }

        /// <summary>长按 F 分解为灵力碎片。</summary>
        private void Decompose()
        {
            if (skillData == null) return;
            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>分解功法：{skillData.skillName} → 获得 {shards} 灵力碎片</color>");
            GameEvents.Publish(new GameEvents.SkillDecomposed { Skill = skillData });
            OnPickedUp();
        }

        private void OnPickedUp()
        {
            _pickedUp = true;
            HidePrompt();
            HideSlotChoiceUI();
            Destroy(gameObject);
        }

        private string GetSlotKeyName(int slotIndex) => slotIndex switch
        {
            0 => "Q",
            1 => "E",
            2 => "R",
            _ => "?"
        };

        private Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Fan => Color.white,
            ItemRarity.Ling => Color.green,
            ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
            ItemRarity.Tian => new Color(1f, 0.85f, 0f),
            _ => Color.white
        };

        private string GetRarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Fan => "凡品",
            ItemRarity.Ling => "灵品",
            ItemRarity.Xuan => "玄品",
            ItemRarity.Di => "地品",
            ItemRarity.Tian => "天品",
            _ => "凡品"
        };

        // ==================== 工厂 ====================

        /// <summary>从技能池中随机选一个当前化身可用的技能（跳过其他化身专属），null = 池中无合法选项。</summary>
        public static SkillData PickValid(SkillData[] pool)
        {
            if (pool == null || pool.Length == 0) return null;

            var pc = PlayerController.Instance;
            var root = pc != null ? pc.GetComponent<SpiritRootController>() : null;
            var curRoot = root != null ? root.CurrentRoot : SpiritRootType.None;

            // Shuffle indices for fair randomness
            int[] idx = new int[pool.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            for (int i = idx.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }

            foreach (int i in idx)
            {
                var s = pool[i];
                if (s == null) continue;
                if (s.skillType == SkillType.AvatarSpecial && s.RequiredRoot != SpiritRootType.None
                    && s.RequiredRoot != curRoot)
                    continue;
                return s;
            }
            return null;
        }

        /// <summary>生成一个功法掉落物。</summary>
        public static SkillPickup Spawn(SkillData data, Vector3 position)
        {
            if (data == null) return null;

            // 化身专属技能：只对对应化身掉落；非该化身一律不生成（任何掉落来源都经此处）
            if (data.skillType == SkillType.AvatarSpecial && data.RequiredRoot != SpiritRootType.None)
            {
                var pc = PlayerController.Instance;
                var root = pc != null ? pc.GetComponent<SpiritRootController>() : null;
                if (root == null || root.CurrentRoot != data.RequiredRoot)
                {
                    Debug.Log($"<color=#999999>[SkillPickup] {data.skillName} 为化身专属（需 {data.RequiredRoot}），当前化身不符 → 跳过掉落</color>");
                    return null;
                }
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"SkillPickup_{data.skillName}";
            go.transform.position = position + Vector3.up * 0.15f;
            go.transform.localScale = new Vector3(0.5f, 0.1f, 0.35f);
            go.layer = LayerMask.NameToLayer("Default");

            // 用 SphereCollider 触发器替换默认 BoxCollider
            var boxCol = go.GetComponent<BoxCollider>();
            if (boxCol != null) Object.Destroy(boxCol);
            go.AddComponent<SphereCollider>();

            var pickup = go.AddComponent<SkillPickup>();
            pickup.skillData = data;
            return pickup;
        }
    }
}
