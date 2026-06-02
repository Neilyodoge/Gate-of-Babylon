using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 掉落灵物 / 洞府素材的世界拾取物（v0.5.5 重构为 <see cref="PickupBase"/> 子类）。
    /// 靠近显示提示；[F] 拾取（按 <see cref="ItemScope"/> 分叉：洞府素材→缓冲，局内灵物→槽位/背包）；长按 [F] 分解。
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ItemPickup : PickupBase
    {
        public override int InteractionPriority => 20;   // 低于功法(25) / 商店(40)

        [Header("数据")]
        public ItemData itemData;

        private PlayerController _nearbyPlayer;

        protected override bool HasTarget => _nearbyPlayer != null;

        protected override bool AcquireTarget(Collider other)
        {
            _nearbyPlayer = other.GetComponent<PlayerController>();
            return true;
        }

        protected override void ReleaseTarget() => _nearbyPlayer = null;

        protected override void SetupVisual()
        {
            // Spawn 时已上色；这里仅兜底：若仍是 Error / 默认材质再补一次
            if (itemData == null) return;
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null
                && renderer.sharedMaterial.shader.name.Contains("Error"))
            {
                Color rarityColor = itemData.GetRarityColor();
                MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.5f);
            }
        }

        protected override PickupPromptData BuildPromptData()
        {
            string rarityName = itemData.rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            return new PickupPromptData
            {
                title = $"{itemData.itemName}（{rarityName}）",
                titleColor = itemData.GetRarityColor(),
                subLine = GetItemEffectText(itemData),
                subColor = new Color(0.5f, 0.9f, 0.5f, 0.9f),
                desc = itemData.description,
                promptHint = $"[F] 拾取  |  长按[F] 分解（{shards} 灵力碎片）"
            };
        }

        protected override void OnPrimaryAction() => ManualPickup();
        protected override void OnDecomposeAction() => Decompose();

        /// <summary>按 F 拾取（按 ItemScope 分叉：洞府素材 → CaveInventory 缓冲 / 局内灵物 → SpiritSlotSystem + ItemInventory）。</summary>
        private void ManualPickup()
        {
            if (itemData == null || _nearbyPlayer == null) return;

            // 洞府素材：走 CaveInventory，不进槽位 / 战斗背包
            if (itemData.scope == ItemScope.CaveMaterial)
            {
                CaveInventory.Instance.AddToBuffer(itemData, 1);
                GameEvents.Publish(new GameEvents.CaveMaterialPickedUp
                {
                    Item = itemData,
                    Amount = 1,
                    CurrentBufferTotal = CaveInventory.Instance.TotalPendingCount
                });
                _pickedUp = true;
                InteractionRouter.Unregister(this);
                HidePrompt();
                if (ObjectPool.Instance != null) ObjectPool.Instance.Return(gameObject);
                else Destroy(gameObject);
                return;
            }

            // 局内灵物：附带功法先尝试装备
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
                        Vector3 dropPos = transform.position + Vector3.up * 0.1f;
                        SkillPickup.Spawn(itemData.linkedSkill, dropPos);
                    }
                }
            }

            // 放入灵物槽位（满则替换最后一格，旧灵物掉回地面）
            var spiritSlots = _nearbyPlayer.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
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
                    int emptySlot = spiritSlots.FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        spiritSlots.SetSlot(emptySlot, itemData);
                        Debug.Log($"<color=cyan>灵物放入槽位 {emptySlot}：{itemData.itemName}</color>");
                    }
                    else
                    {
                        int lastSlot = spiritSlots.Slots.Count - 1;
                        ItemData oldItem = spiritSlots.SetSlot(lastSlot, itemData);
                        Debug.Log($"<color=cyan>灵物替换槽位 {lastSlot}：{itemData.itemName}（替下 {oldItem?.itemName}）</color>");
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
                    Debug.Log($"<color=cyan>灵物 {itemData.itemName} 已在槽位 {existingSlot}</color>");
                }
            }

            // 计入战斗背包（协同 / 质变 / 属性聚合）
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

        /// <summary>长按 F 分解为灵力碎片。</summary>
        private void Decompose()
        {
            if (itemData == null) return;
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>分解：{itemData.itemName} → 获得 {shards} 灵力碎片</color>");

            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        /// <summary>拼出灵物效果摘要文本（用于提示副行）。</summary>
        private string GetItemEffectText(ItemData item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (item.attackBonus > 0) parts.Add($"攻击+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻击+{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"生命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"生命+{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"移速+{item.moveSpeedBonusPercent * 100:0}%");
            if (item.attackSpeedBonusPercent > 0) parts.Add($"攻速+{item.attackSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤+{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"暴击+{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"击杀回血+{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            if (item.linkedSkill != null) parts.Add($"附带功法：{item.linkedSkill.skillName}");
            return parts.Count > 0 ? string.Join("  ", parts) : "";
        }

        // ==================== 工厂 ====================

        /// <summary>生成一个灵物 / 洞府素材掉落物。</summary>
        public static ItemPickup Spawn(ItemData data, Vector3 position)
        {
            if (data == null)
            {
                Debug.LogWarning("[ItemPickup.Spawn] data 为 null，跳过生成");
                return null;
            }

            // V.03（Q8）：整套灵物屏蔽时，不再生成局内灵物地面拾取（洞府素材 CaveMaterial 不受影响）
            if (!FeatureFlags.EnableSpiritItems && data.scope == ItemScope.RunOnly)
                return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"ItemPickup_{data.itemName}";
            Vector3 spawnPos = position;
            spawnPos.y = Mathf.Max(spawnPos.y, 0f) + 0.5f;
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.5f;
            go.layer = LayerMask.NameToLayer("Default");

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color rarityColor = data.GetRarityColor();
                MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.8f);
            }

            var existingSphere = go.GetComponent<SphereCollider>();
            if (existingSphere != null)
                existingSphere.isTrigger = true;

            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemData = data;

            Debug.Log($"<color=green>[ItemPickup] 生成掉落物：{data.itemName}（{data.rarity}）@ {spawnPos}</color>");
            return pickup;
        }
    }
}
