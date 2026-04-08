using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 场景中的灵物拾取物
    /// 玩家靠近后自动拾取（或按键拾取）
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
        [SerializeField] private float pickupRadius = 1.5f;

        private Vector3 _startPos;
        private bool _pickedUp;

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
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            var inventory = other.GetComponent<ItemInventory>();
            if (inventory == null) return;

            _pickedUp = true;
            inventory.AddItem(itemData);

            // 播放拾取特效
            if (itemData.pickupVfxPrefab != null && ObjectPool.Instance != null)
            {
                var vfx = ObjectPool.Instance.Get(itemData.pickupVfxPrefab, transform.position, Quaternion.identity);
                ObjectPool.Instance.Return(vfx, 2f);
            }

            // 销毁拾取物
            Destroy(gameObject);
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
