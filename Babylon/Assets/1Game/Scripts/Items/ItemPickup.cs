using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// ?????????
    /// ?????????F???????????
    /// ??F????????
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        // ===== IInteractable: unified F-key interaction router =====
        // (avoids triggering this AND a nearby shop/skill at the same F press)
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 20;          // item pickup: lower than skill (25) and shop (40)
        public bool IsInteractionAvailable => !_pickedUp && _playerInRange;
        public bool IsRoutedActive { get; set; }

        [Header("????")]
        public ItemData itemData;

        [Header("??")]
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float pickupRadius = 2.5f;

        private Vector3 _startPos;
        private bool _pickedUp;

        // ???????????????
        private bool _playerInRange;
        private PlayerController _nearbyPlayer;
        private float _holdTimer;
        private const float HOLD_TO_DECOMPOSE = 1.5f;

        // ??UI
        private GameObject _promptUI;
        private UnityEngine.UI.Image _holdProgressFill;

        // ??????????? Keyboard.current ????
        private UnityEngine.InputSystem.Keyboard _keyboard;

        private void Awake()
        {
            _keyboard = UnityEngine.InputSystem.Keyboard.current;
        }

        private void Start()
        {
            _startPos = transform.position;

            // ?????
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = pickupRadius;

            // ????????????????Spawn???????????????
            if (itemData != null)
            {
                var renderer = GetComponentInChildren<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null 
                    && renderer.sharedMaterial.shader.name.Contains("Error"))
                {
                    ApplyMaterial(renderer);
                }
            }
        }

        /// <summary>????????????? + MPB???????</summary>
        private void ApplyMaterial(Renderer renderer)
        {
            if (itemData == null || renderer == null) return;
            Color rarityColor = itemData.GetRarityColor();
            MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.5f);
        }

        private void Update()
        {
            if (_pickedUp) return;

            // ?????????????UI???????
            float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, newY, _startPos.z);

            // ??
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // ??UI??XZ?Y??????????
            if (_promptUI != null)
            {
                _promptUI.transform.position = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);
            }

            // Sync prompt visibility with router-active state.
            // When several interactables overlap, only the routed-active one
            // shows its prompt and consumes F input.
            if (_playerInRange && IsRoutedActive)
            {
                if (_promptUI == null) ShowPrompt();
            }
            else
            {
                if (_promptUI != null) HidePrompt();
                _holdTimer = 0f;
            }

            if (_playerInRange && _nearbyPlayer != null && IsRoutedActive)
            {
                var kb = _keyboard ?? (_keyboard = UnityEngine.InputSystem.Keyboard.current);
                if (kb == null) return;

                if (kb.fKey.isPressed)
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = _holdTimer / HOLD_TO_DECOMPOSE;

                    // ????
                    if (_holdTimer >= HOLD_TO_DECOMPOSE)
                    {
                        Decompose();
                        return;
                    }
                }

                if (kb.fKey.wasReleasedThisFrame && _holdTimer < HOLD_TO_DECOMPOSE)
                {
                    // ????
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

            _nearbyPlayer = other.GetComponent<PlayerController>();
            _playerInRange = true;
            // Register with router; whether the prompt actually shows is decided
            // each frame in Update by checking IsRoutedActive.
            InteractionRouter.Register(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            _nearbyPlayer = null;
            _holdTimer = 0f;
            InteractionRouter.Unregister(this);
            HidePrompt();
        }

        /// <summary>按 F 拾取（v0.5 按 ItemScope 分叉为：洞府素材 → CaveInventory 缓冲 / 局内灵物 → SpiritSlotSystem + ItemInventory）</summary>
        private void ManualPickup()
        {
            if (itemData == null || _nearbyPlayer == null) return;

            // v0.5 搜打撤分叉：洞府素材走 CaveInventory，不走槽位 / 战斗背包
            if (itemData.scope == ItemScope.CaveMaterial)
            {
                CaveInventory.Instance.AddToBuffer(itemData, 1);
                GameEvents.Publish(new GameEvents.CaveMaterialPickedUp
                {
                    Item = itemData,
                    Amount = 1,
                    CurrentBufferTotal = CaveInventory.Instance.TotalPendingCount
                });
                // 洞府素材拾取后直接销毁拾取物（不进背包不进槽位）
                if (ObjectPool.Instance != null) ObjectPool.Instance.Return(gameObject);
                else Destroy(gameObject);
                return;
            }

            // 局内灵物：保持原有逻辑
            // ?????????????
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
                        // ???? ? ??SkillPickup???????????
                        Vector3 dropPos = transform.position + Vector3.up * 0.1f;
                        SkillPickup.Spawn(itemData.linkedSkill, dropPos);
                    }
                }
            }

            // ?????????????????????
            var spiritSlots = _nearbyPlayer.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
                // ?????????????????????????
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
                    // ???????????????
                    int emptySlot = spiritSlots.FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        spiritSlots.SetSlot(emptySlot, itemData);
                        Debug.Log($"<color=cyan>?????? {emptySlot}?{itemData.itemName}</color>");
                    }
                    else
                    {
                        // ?? ? ??????????????
                        int lastSlot = spiritSlots.Slots.Count - 1;
                        ItemData oldItem = spiritSlots.SetSlot(lastSlot, itemData);
                        Debug.Log($"<color=cyan>?????????? {lastSlot}?{itemData.itemName}???{oldItem?.itemName}?</color>");
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
                    // ?????????????????????
                    Debug.Log($"<color=cyan>?????{itemData.itemName}??? {existingSlot}?</color>");
                }
            }

            // ??????
            var inventory = _nearbyPlayer.GetComponent<ItemInventory>();
            if (inventory != null)
                inventory.AddItem(itemData);

            _pickedUp = true;
            HidePrompt();

            if (itemData != null && itemData.pickupVfxPrefab != null && ObjectPool.Instance != null)
            {
                var vfx = ObjectPool.Instance.Get(itemData.pickupVfxPrefab, transform.position, Quaternion.identity);
                ObjectPool.Instance.Return(vfx, 2f);
            }

            Destroy(gameObject);
        }

        /// <summary>???????F?</summary>
        private void Decompose()
        {
            if (itemData == null) return;

            // ????????
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>?????{itemData.itemName} ? ?? {shards} ????</color>");

            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        // ==================== ??UI ====================

        private void ShowPrompt()
        {
            if (_promptUI != null || itemData == null) return;

            // ??????
            string effectText = GetItemEffectText(itemData);
            bool hasEffect = !string.IsNullOrEmpty(effectText);
            // ????????????
            float panelHeight = hasEffect ? 160 : 100;

            var canvasGo = new GameObject("ItemPromptCanvas");
            // ????????????????
            canvasGo.transform.position = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, panelHeight);
            rt.localScale = Vector3.one * 0.00875f;

            // ??
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

            // ????
            string rarityName = itemData.rarity switch
            {
                ItemRarity.Fan => "??",
                ItemRarity.Ling => "??",
                ItemRarity.Xuan => "??",
                ItemRarity.Di => "??",
                ItemRarity.Tian => "??",
                _ => "??"
            };

            // ??
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            float nameTop = hasEffect ? 0.72f : 0.5f;
            nameRt.anchorMin = new Vector2(0, nameTop);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(8, 0);
            nameRt.offsetMax = new Vector2(-8, -4);
            var nameText = nameGo.AddComponent<UnityEngine.UI.Text>();
            nameText.text = $"{itemData.itemName}?{rarityName}?";
            nameText.fontSize = 28;
            nameText.font = UIBuiltins.LegacyFont;
            nameText.color = itemData.GetRarityColor();
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontStyle = FontStyle.Bold;
            var nameOutline = nameGo.AddComponent<UnityEngine.UI.Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // ???????
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
                descText.font = UIBuiltins.LegacyFont;
                descText.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
                descText.alignment = TextAnchor.MiddleCenter;
            }

            // ?????????
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
                effText.font = UIBuiltins.LegacyFont;
                effText.color = new Color(0.5f, 0.9f, 0.5f, 0.9f);
                effText.alignment = TextAnchor.MiddleCenter;
                effText.supportRichText = true;
            }

            // ????
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
            promptText.text = $"[F] ??  |  ??[F] ????{shards}?";
            promptText.fontSize = 18;
            promptText.font = UIBuiltins.LegacyFont;
            promptText.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            promptText.alignment = TextAnchor.MiddleCenter;

            // ?????
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

            // ???UI?????????????????
            var billboard = canvasGo.AddComponent<BillboardUI>();
            billboard.lerpFactor = 0.5f;

            _promptUI = canvasGo;
        }

        /// <summary>????????</summary>
        private string GetItemEffectText(ItemData item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (item.attackBonus > 0) parts.Add($"??+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"??+{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"??+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"??+{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"?+{item.moveSpeedBonusPercent * 100:0}%");
            if (item.attackSpeedBonusPercent > 0) parts.Add($"???+{item.attackSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"????+{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"???+{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"?????{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"??{item.burnDamagePerSecond}/s");
            if (item.linkedSkill != null) parts.Add($"???{item.linkedSkill.skillName}");
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

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
            HidePrompt();
        }

        /// <summary>
        /// ?????????????????
        /// </summary>
        public static ItemPickup Spawn(ItemData data, Vector3 position)
        {
            if (data == null)
            {
                Debug.LogWarning("[ItemPickup.Spawn] data ? null?????");
                return null;
            }

            // ????????????????
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"ItemPickup_{data.itemName}";
            // ??Y???????
            Vector3 spawnPos = position;
            spawnPos.y = Mathf.Max(spawnPos.y, 0f) + 0.5f;
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.5f;
            go.layer = LayerMask.NameToLayer("Default");

            // ???????URP?????Start??????????
            // ???? + MPB????????????????? Material
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color rarityColor = data.GetRarityColor();
                MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.8f);
            }

            // ?? CreatePrimitive ??? SphereCollider
            var existingSphere = go.GetComponent<SphereCollider>();
            if (existingSphere != null)
            {
                existingSphere.isTrigger = true;
            }

            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemData = data;

            Debug.Log($"<color=green>[ItemPickup] ? ?????{data.itemName}?{data.rarity}????={spawnPos}</color>");
            return pickup;
        }
    }
}
