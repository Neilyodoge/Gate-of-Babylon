using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府素材掉落拾取物（v0.5.5 重构为 <see cref="PickupBase"/> 子类）。
    /// 靠近显示提示；[F] 拾取 → CaveInventory 缓冲；长按 [F] 分解。
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
            int shards = PlayerResources.GetDecomposeShards(itemData.rarity);
            return new PickupPromptData
            {
                title = $"{itemData.itemName}",
                titleColor = itemData.GetRarityColor(),
                subLine = "洞府素材",
                subColor = new Color(0.5f, 0.9f, 0.5f, 0.9f),
                desc = itemData.description,
                promptHint = $"[F] 拾取  |  长按[F] 分解（{shards} 灵力碎片）"
            };
        }

        protected override void OnPrimaryAction() => ManualPickup();
        protected override void OnDecomposeAction() => Decompose();

        /// <summary>按 F 拾取 → CaveInventory 缓冲。</summary>
        private void ManualPickup()
        {
            if (itemData == null || _nearbyPlayer == null) return;

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

        // ==================== 工厂 ====================

        /// <summary>生成一个洞府素材掉落物。</summary>
        public static ItemPickup Spawn(ItemData data, Vector3 position)
        {
            if (data == null)
            {
                Debug.LogWarning("[ItemPickup.Spawn] data 为 null，跳过生成");
                return null;
            }

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
