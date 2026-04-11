using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 场景中的灵物拾取物
    /// 靠近后显示提示，按F拾取自动放入第一个空位
    /// 长按F分解获得灵力碎片
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ItemPickup : MonoBehaviour
    {
        [Header("灵物数据")]
        public ItemData itemData;

        [Header("表现")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.3f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float pickupRadius = 2.5f;

        private Vector3 _startPos;
        private bool _pickedUp;

        // 交互状态（需要槽位的灵物才用）
        private bool _playerInRange;
        private PlayerController _nearbyPlayer;
        private float _holdTimer;
        private const float HOLD_TO_DECOMPOSE = 1.5f;

        // 提示UI
        private GameObject _promptUI;
        private UnityEngine.UI.Image _holdProgressFill;

        private void Start()
        {
            _startPos = transform.position;

            // 设置触发器
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupRadius;

            // 设置显示颜色（根据品阶）
            if (itemData != null)
            {
                var renderer = GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    var mat = renderer.material;
                    mat.color = itemData.GetRarityColor();
                    // 添加自发光
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", itemData.GetRarityColor() * 0.5f);
                }
            }
        }

        private void Update()
        {
            if (_pickedUp) return;

            // 上下浮动
            float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, newY, _startPos.z);

            // 旋转
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // 玩家在范围内：处理交互（按F拾取 / 长按F分解）
            if (_playerInRange && _nearbyPlayer != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return;

                if (kb.fKey.isPressed)
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = _holdTimer / HOLD_TO_DECOMPOSE;

                    // 长按分解
                    if (_holdTimer >= HOLD_TO_DECOMPOSE)
                    {
                        Decompose();
                        return;
                    }
                }

                if (kb.fKey.wasReleasedThisFrame && _holdTimer < HOLD_TO_DECOMPOSE)
                {
                    // 短按拾取
                    ManualPickup();
                }

                if (!kb.fKey.isPressed)
                {
                    _holdTimer = 0f;
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = 0f;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            // 所有灵物统一：显示提示，等待按F拾取 / 长按F分解
            _nearbyPlayer = other.GetComponent<PlayerController>();
            _playerInRange = true;
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            _nearbyPlayer = null;
            _holdTimer = 0f;
            HidePrompt();
        }

        /// <summary>拾取灵物（按F触发）→ 自动放入第一个空位</summary>
        private void ManualPickup()
        {
            if (itemData == null || _nearbyPlayer == null) return;

            // 功法类灵物：装备到技能槽位
            if (itemData.linkedSkill != null)
            {
                var combat = _nearbyPlayer.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    int emptySlot = combat.FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        combat.EquipSkillToSlot(itemData.linkedSkill, emptySlot);
                        GameEvents.Publish(new GameEvents.SkillEquipped
                        {
                            Skill = itemData.linkedSkill,
                            SlotIndex = emptySlot
                        });
                    }
                    else
                    {
                        // 满了 → 替换R槽位，旧技能掉落
                        SkillData oldSkill = combat.EquipSkillToSlot(itemData.linkedSkill, 2);
                        GameEvents.Publish(new GameEvents.SkillEquipped
                        {
                            Skill = itemData.linkedSkill,
                            SlotIndex = 2
                        });
                        if (oldSkill != null)
                        {
                            Vector3 dropPos = transform.position + Random.insideUnitSphere * 1.5f;
                            dropPos.y = _startPos.y;
                            SkillPickup.Spawn(oldSkill, dropPos);
                        }
                    }
                }
            }

            // 所有灵物都放入灵物槽位（技能下方的小圆槽）
            var spiritSlots = _nearbyPlayer.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
                // 先检查是否已有相同灵物（叠加到同一槽位，不占新位）
                int existingSlot = -1;
                for (int i = 0; i < spiritSlots.Slots.Count; i++)
                {
                    if (spiritSlots.Slots[i].item != null && spiritSlots.Slots[i].item == itemData)
                    {
                        existingSlot = i;
                        break;
                    }
                }

                if (existingSlot < 0)
                {
                    // 没有相同灵物，放入第一个空槽位
                    int emptySlot = spiritSlots.FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        spiritSlots.SetSlot(emptySlot, itemData);
                        Debug.Log($"<color=cyan>灵物放入槽位 {emptySlot}：{itemData.itemName}</color>");
                    }
                    else
                    {
                        // 满了 → 替换最后一个槽位，旧灵物掉落
                        int lastSlot = spiritSlots.Slots.Count - 1;
                        ItemData oldItem = spiritSlots.SetSlot(lastSlot, itemData);
                        Debug.Log($"<color=cyan>灵物槽已满，替换槽位 {lastSlot}：{itemData.itemName}（旧：{oldItem?.itemName}）</color>");
                        if (oldItem != null)
                        {
                            Vector3 dropPos = transform.position + Random.insideUnitSphere * 1.5f;
                            dropPos.y = _startPos.y;
                            Spawn(oldItem, dropPos);
                        }
                    }
                }
                else
                {
                    // 已有相同灵物，不占新槽位（背包计数会叠加）
                    Debug.Log($"<color=cyan>灵物叠加：{itemData.itemName}（槽位 {existingSlot}）</color>");
                }
            }

            // 加入背包记录
            var inventory = _nearbyPlayer.GetComponent<ItemInventory>();
            if (inventory != null)
                inventory.AddItem(itemData);

            _pickedUp = true;
            HidePrompt();

            if (itemData.pickupVfxPrefab != null && ObjectPool.Instance != null)
            {
                var vfx = ObjectPool.Instance.Get(itemData.pickupVfxPrefab, transform.position, Quaternion.identity);
                ObjectPool.Instance.Return(vfx, 2f);
            }

            Destroy(gameObject);
        }

        /// <summary>分解灵物（长按F）</summary>
        private void Decompose()
        {
            if (itemData == null) return;

            // 分解获得灵力碎片
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>分解灵物：{itemData.itemName} → 获得 {shards} 灵力碎片</color>");

            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        // ==================== 提示UI ====================

        private void ShowPrompt()
        {
            if (_promptUI != null || itemData == null) return;

            // 构建效果文本
            string effectText = GetItemEffectText(itemData);
            bool hasEffect = !string.IsNullOrEmpty(effectText);
            // 面板高度根据内容动态调整
            float panelHeight = hasEffect ? 160 : 100;

            var canvasGo = new GameObject("ItemPromptCanvas");
            canvasGo.transform.SetParent(transform);
            canvasGo.transform.localPosition = new Vector3(0, 2.8f, 0);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, panelHeight);
            rt.localScale = Vector3.one * 0.035f;

            // 背景
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

            // 品阶名称
            string rarityName = itemData.rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };

            // 名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            float nameTop = hasEffect ? 0.72f : 0.5f;
            nameRt.anchorMin = new Vector2(0, nameTop);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(8, 0);
            nameRt.offsetMax = new Vector2(-8, -4);
            var nameText = nameGo.AddComponent<UnityEngine.UI.Text>();
            nameText.text = $"{itemData.itemName}（{rarityName}）";
            nameText.fontSize = 28;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = itemData.GetRarityColor();
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontStyle = FontStyle.Bold;
            var nameOutline = nameGo.AddComponent<UnityEngine.UI.Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 描述（如果有）
            if (!string.IsNullOrEmpty(itemData.description))
            {
                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(canvasGo.transform, false);
                var descRt = descGo.AddComponent<RectTransform>();
                float descTop = hasEffect ? 0.55f : 0.35f;
                float descBot = hasEffect ? 0.72f : 0.5f;
                descRt.anchorMin = new Vector2(0, descTop);
                descRt.anchorMax = new Vector2(1, descBot);
                descRt.offsetMin = new Vector2(8, 0);
                descRt.offsetMax = new Vector2(-8, 0);
                var descText = descGo.AddComponent<UnityEngine.UI.Text>();
                descText.text = itemData.description;
                descText.fontSize = 18;
                descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                descText.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
                descText.alignment = TextAnchor.MiddleCenter;
            }

            // 效果属性（如果有）
            if (hasEffect)
            {
                var effectGo = new GameObject("Effect");
                effectGo.transform.SetParent(canvasGo.transform, false);
                var effectRt = effectGo.AddComponent<RectTransform>();
                effectRt.anchorMin = new Vector2(0, 0.3f);
                effectRt.anchorMax = new Vector2(1, 0.55f);
                effectRt.offsetMin = new Vector2(8, 0);
                effectRt.offsetMax = new Vector2(-8, 0);
                var effText = effectGo.AddComponent<UnityEngine.UI.Text>();
                effText.text = effectText;
                effText.fontSize = 16;
                effText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                effText.color = new Color(0.5f, 0.9f, 0.5f, 0.9f);
                effText.alignment = TextAnchor.MiddleCenter;
                effText.supportRichText = true;
            }

            // 操作提示
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(canvasGo.transform, false);
            var promptRt = promptGo.AddComponent<RectTransform>();
            float promptBot = hasEffect ? 0.12f : 0.15f;
            float promptTop = hasEffect ? 0.3f : 0.5f;
            promptRt.anchorMin = new Vector2(0, promptBot);
            promptRt.anchorMax = new Vector2(1, promptTop);
            promptRt.offsetMin = new Vector2(8, 0);
            promptRt.offsetMax = new Vector2(-8, 0);
            var promptText = promptGo.AddComponent<UnityEngine.UI.Text>();
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            promptText.text = $"[F] 拾取  |  长按[F] 分解（✦{shards}）";
            promptText.fontSize = 18;
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            promptText.alignment = TextAnchor.MiddleCenter;

            // 长按进度条
            var holdBgGo = new GameObject("HoldBg");
            holdBgGo.transform.SetParent(canvasGo.transform, false);
            var holdBgRt = holdBgGo.AddComponent<RectTransform>();
            holdBgRt.anchorMin = new Vector2(0.1f, 0.02f);
            holdBgRt.anchorMax = new Vector2(0.9f, 0.08f);
            holdBgRt.offsetMin = Vector2.zero;
            holdBgRt.offsetMax = Vector2.zero;
            var holdBgImg = holdBgGo.AddComponent<UnityEngine.UI.Image>();
            holdBgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            var holdFillGo = new GameObject("HoldFill");
            holdFillGo.transform.SetParent(holdBgGo.transform, false);
            var holdFillRt = holdFillGo.AddComponent<RectTransform>();
            holdFillRt.anchorMin = Vector2.zero;
            holdFillRt.anchorMax = Vector2.one;
            holdFillRt.offsetMin = Vector2.zero;
            holdFillRt.offsetMax = Vector2.zero;
            _holdProgressFill = holdFillGo.AddComponent<UnityEngine.UI.Image>();
            _holdProgressFill.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            _holdProgressFill.type = UnityEngine.UI.Image.Type.Filled;
            _holdProgressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _holdProgressFill.fillAmount = 0f;

            _promptUI = canvasGo;
            canvasGo.AddComponent<BillboardUI>();
        }

        /// <summary>获取灵物效果文本</summary>
        private string GetItemEffectText(ItemData item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (item.attackBonus > 0) parts.Add($"⚔攻+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"⚔攻+{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"♥命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"♥命+{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"速+{item.moveSpeedBonusPercent * 100:0}%");
            if (item.attackSpeedBonusPercent > 0) parts.Add($"⚡攻速+{item.attackSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"🛡减伤+{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"✧暴击+{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"♥击杀回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            if (item.linkedSkill != null) parts.Add($"功法：{item.linkedSkill.skillName}");
            return parts.Count > 0 ? string.Join("  ", parts) : "";
        }

        private void HidePrompt()
        {
            if (_promptUI != null)
            {
                Destroy(_promptUI);
                _promptUI = null;
            }
        }

        /// <summary>
        /// 工厂方法：在指定位置生成灵物拾取物
        /// </summary>
        public static ItemPickup Spawn(ItemData data, Vector3 position)
        {
            // 创建一个简单的几何体作为灵物表现
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"ItemPickup_{data.itemName}";
            go.transform.position = position + Vector3.up * 0.5f;
            go.transform.localScale = Vector3.one * 0.4f;
            go.layer = LayerMask.NameToLayer("Default");

            // 复用 CreatePrimitive 自带的 SphereCollider（RequireComponent 需要它）
            // 先移除默认的非 Sphere 碰撞体（如果有的话），保留 SphereCollider
            var existingSphere = go.GetComponent<SphereCollider>();
            if (existingSphere != null)
            {
                // 直接复用，不需要删除
                existingSphere.isTrigger = true;
            }

            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemData = data;

            return pickup;
        }
    }
}
