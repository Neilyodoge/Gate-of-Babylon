using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 商店房间 —— 可以用击杀获得的灵力购买灵物
    /// </summary>
    public class ShopRoom : MonoBehaviour
    {
        private ItemData[] _shopItems;
        private int _roomIndex;
        private GameObject _roomVisuals;
        private GameObject _shopUI;
        private bool _purchased;

        public float RoomWidth => 20f;
        public float RoomDepth => 20f;

        public void Initialize(int roomIndex, ItemData[] itemPool)
        {
            _roomIndex = roomIndex;
            _shopItems = itemPool;
            BuildRoom();
            CreateShopDisplay();
        }

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, 20f, 20f, _roomIndex);

            // 商店装饰：中央柜台
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "ShopCounter";
            counter.transform.SetParent(transform);
            counter.transform.localPosition = new Vector3(0, 0.5f, 2f);
            counter.transform.localScale = new Vector3(6f, 1f, 1.5f);
            var rend = counter.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.35f, 0.25f, 0.15f);
                rend.material = mat;
            }

            // 商人NPC（简单的胶囊体）
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Shopkeeper";
            npc.transform.SetParent(transform);
            npc.transform.localPosition = new Vector3(0, 1f, 3.5f);
            var npcCol = npc.GetComponent<Collider>();
            if (npcCol != null) Destroy(npcCol);
            var npcRend = npc.GetComponent<Renderer>();
            if (npcRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.9f, 0.8f, 0.5f);
                npcRend.material = mat;
            }

            // 商人名字
            var nameCanvas = new GameObject("ShopkeeperName");
            nameCanvas.transform.SetParent(npc.transform);
            nameCanvas.transform.localPosition = new Vector3(0, 1.5f, 0);
            var c = nameCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 0.4f);
            nameCanvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("Name");
            textGo.transform.SetParent(nameCanvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "散修商人";
            text.fontSize = 20;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(1f, 0.9f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateShopDisplay()
        {
            if (_shopItems == null || _shopItems.Length == 0) return;

            // 在柜台上展示3个随机灵物
            int displayCount = Mathf.Min(3, _shopItems.Length);
            for (int i = 0; i < displayCount; i++)
            {
                var item = _shopItems[Random.Range(0, _shopItems.Length)];
                float xOffset = (i - 1) * 2f;

                // 灵物展示球
                var display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                display.name = $"ShopItem_{item.itemName}";
                display.transform.SetParent(transform);
                display.transform.localPosition = new Vector3(xOffset, 1.5f, 2f);
                display.transform.localScale = Vector3.one * 0.5f;

                var displayCol = display.GetComponent<Collider>();
                if (displayCol != null) Destroy(displayCol);

                var displayRend = display.GetComponent<Renderer>();
                if (displayRend != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = item.GetRarityColor();
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", item.GetRarityColor() * 1.5f);
                    displayRend.material = mat;
                }

                // 触发器：靠近拾取
                var triggerGo = new GameObject($"ShopTrigger_{i}");
                triggerGo.transform.SetParent(display.transform);
                triggerGo.transform.localPosition = Vector3.zero;
                var sc = triggerGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 3f;
                var rb = triggerGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                var pickup = triggerGo.AddComponent<ShopItemPickup>();
                pickup.Initialize(item, display);
            }
        }

        /// <summary>完成购物，通知进入下一层</summary>
        public void CompleteShop()
        {
            GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
        }

        private void Update()
        {
            // 商店房间按F键离开
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                CompleteShop();
            }
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }
    }

    /// <summary>商店灵物拾取</summary>
    public class ShopItemPickup : MonoBehaviour
    {
        private ItemData _item;
        private GameObject _display;
        private bool _taken;

        public void Initialize(ItemData item, GameObject display)
        {
            _item = item;
            _display = display;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_taken || !other.CompareTag("Player")) return;
            _taken = true;

            if (PlayerController.Instance != null)
                PlayerController.Instance.Inventory.AddItem(_item);

            if (_display != null) Destroy(_display);
            Destroy(gameObject);
        }
    }
}
