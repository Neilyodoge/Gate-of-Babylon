using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 宝箱房间 —— 开启宝箱获得灵物
    /// </summary>
    public class TreasureRoom : MonoBehaviour
    {
        private int _roomIndex;
        private ItemData[] _itemPool;
        private GameObject _roomVisuals;
        private GameObject _chest;
        private bool _opened;

        public float RoomWidth => 18f;
        public float RoomDepth => 18f;

        public void Initialize(int roomIndex, ItemData[] itemPool)
        {
            _roomIndex = roomIndex;
            _itemPool = itemPool;
            BuildRoom();
        }

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, 18f, 18f, _roomIndex);

            // 宝箱（用Cube组合）
            _chest = new GameObject("TreasureChest");
            _chest.transform.SetParent(transform);
            _chest.transform.localPosition = new Vector3(0, 0, 0);

            // 箱体
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChestBody";
            body.transform.SetParent(_chest.transform);
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            body.transform.localScale = new Vector3(1.5f, 1f, 1f);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);
            var bodyRend = body.GetComponent<Renderer>();
            if (bodyRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.6f, 0.4f, 0.15f);
                bodyRend.material = mat;
            }

            // 箱盖
            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "ChestLid";
            lid.transform.SetParent(_chest.transform);
            lid.transform.localPosition = new Vector3(0, 1.1f, 0);
            lid.transform.localScale = new Vector3(1.6f, 0.3f, 1.1f);
            var lidCol = lid.GetComponent<Collider>();
            if (lidCol != null) Destroy(lidCol);
            var lidRend = lid.GetComponent<Renderer>();
            if (lidRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.7f, 0.5f, 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.05f));
                lidRend.material = mat;
            }

            // 锁（小球）
            var lockObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lockObj.name = "ChestLock";
            lockObj.transform.SetParent(_chest.transform);
            lockObj.transform.localPosition = new Vector3(0, 0.7f, 0.55f);
            lockObj.transform.localScale = Vector3.one * 0.2f;
            var lockCol = lockObj.GetComponent<Collider>();
            if (lockCol != null) Destroy(lockCol);
            var lockRend = lockObj.GetComponent<Renderer>();
            if (lockRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(1f, 0.85f, 0.2f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 2f);
                lockRend.material = mat;
            }

            // 触发器
            var triggerGo = new GameObject("ChestTrigger");
            triggerGo.transform.SetParent(_chest.transform);
            triggerGo.transform.localPosition = Vector3.zero;
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 3f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<ChestTrigger>();
            trigger.Initialize(this);

            // 提示
            var hintCanvas = new GameObject("HintCanvas");
            hintCanvas.transform.SetParent(_chest.transform);
            hintCanvas.transform.localPosition = new Vector3(0, 2.5f, 0);
            var c = hintCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0.4f);
            hintCanvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(hintCanvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "宝箱 · 靠近开启";
            text.fontSize = 18;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(1f, 0.85f, 0.3f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 出口触发器（房间北侧）
            CreateExitTrigger();
        }

        /// <summary>在房间北侧创建出口触发器</summary>
        private void CreateExitTrigger()
        {
            var exitGo = new GameObject("ExitTrigger");
            exitGo.transform.SetParent(transform);
            exitGo.transform.localPosition = new Vector3(0, 0, RoomDepth / 2f - 2f); // 房间北侧

            var sc = exitGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = exitGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var exitTrigger = exitGo.AddComponent<RoomExitTrigger>();
            exitTrigger.Initialize(() =>
            {
                if (!_opened) return; // 必须先开箱
                GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
            });

            // 出口视觉标记（发光柱）
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "ExitPillar";
            pillar.transform.SetParent(exitGo.transform);
            pillar.transform.localPosition = new Vector3(0, 1.5f, 0);
            pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            var pillarCol = pillar.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);
            var pillarRend = pillar.GetComponent<Renderer>();
            if (pillarRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.5f);
                pillarRend.material = mat;
            }
        }

        public void OpenChest()
        {
            if (_opened) return;
            _opened = true;

            if (_itemPool == null || _itemPool.Length == 0) return;

            // 掉落2-3个灵物
            int count = Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                var config = GameConfig.Instance;
                ItemData item;
                if (config != null)
                {
                    ItemRarity rarity = config.RollRarity();
                    // 宝箱品阶提升一级
                    if (rarity == ItemRarity.Fan) rarity = ItemRarity.Ling;
                    else if (rarity == ItemRarity.Ling) rarity = ItemRarity.Xuan;

                    var candidates = new System.Collections.Generic.List<ItemData>();
                    foreach (var d in _itemPool)
                    {
                        if (d != null && d.rarity == rarity)
                            candidates.Add(d);
                    }
                    item = candidates.Count > 0
                        ? candidates[Random.Range(0, candidates.Count)]
                        : _itemPool[Random.Range(0, _itemPool.Length)];
                }
                else
                {
                    item = _itemPool[Random.Range(0, _itemPool.Length)];
                }

                if (item != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                    ItemPickup.Spawn(item, transform.position + offset);
                }
            }

            // v0.5 搜打撤：宝藏房必定额外出 2 件【洞府素材】（搜打撤"打"通宝藏的高收益锚点）
            for (int i = 0; i < 2; i++)
            {
                Vector3 caveOffset = new Vector3(Random.Range(-1.8f, 1.8f), 0, Random.Range(-1.8f, 1.8f));
                CaveMaterialPool.SpawnRandom(transform.position + caveOffset, 1f);
            }

            // 开箱动画：箱盖飞起
            if (_chest != null)
            {
                var lid = _chest.transform.Find("ChestLid");
                if (lid != null)
                    StartCoroutine(OpenLidAnimation(lid));
            }

            Debug.Log("<color=yellow>宝箱已开启！</color>");
        }

        private System.Collections.IEnumerator OpenLidAnimation(Transform lid)
        {
            float timer = 0;
            Vector3 startPos = lid.localPosition;
            Quaternion startRot = lid.localRotation;
            while (timer < 0.5f)
            {
                timer += Time.deltaTime;
                float t = timer / 0.5f;
                lid.localPosition = startPos + new Vector3(0, t * 2f, -t * 0.5f);
                lid.localRotation = startRot * Quaternion.Euler(-t * 120f, 0, 0);
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }
    }

    /// <summary>宝箱触发器</summary>
    public class ChestTrigger : MonoBehaviour
    {
        private TreasureRoom _room;

        public void Initialize(TreasureRoom room)
        {
            _room = room;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _room?.OpenChest();
        }
    }
}
