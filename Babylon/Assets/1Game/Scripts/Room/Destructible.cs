using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 可破坏物 —— 战斗房间中的可破坏障碍物
    /// 被玩家攻击后碎裂，有概率掉落灵力碎片和灵物
    /// 用Cube/Cylinder等基础几何体表示
    /// </summary>
    public class Destructible : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private float maxHp = 15f;
        [SerializeField] private float currentHp;

        [Header("掉落")]
        [SerializeField] private float dropChance = 0.4f;
        [SerializeField] private int shardMin = 1;
        [SerializeField] private int shardMax = 4;
        [SerializeField] private float itemDropChance = 0.08f; // 灵物掉落概率（8%）
        private ItemData[] _itemPool; // 灵物掉落池（由外部设置）

        private CombatStats _stats;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;
        private bool _destroyed;

        /// <summary>IDamageable 接口要求的属性</summary>
        public CombatStats Stats => _stats;

        private void Awake()
        {
            currentHp = maxHp;
            _stats = new CombatStats { maxHp = maxHp, currentHp = maxHp };
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material.color;
        }

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (_destroyed) return;

            currentHp -= damage;

            // 受击闪白
            foreach (var r in _renderers)
                if (r != null) r.material.color = Color.white;
            _hitFlashTimer = 0.08f;

            // 轻微击退/抖动
            transform.position += (transform.position - attacker.transform.position).normalized * 0.05f;

            if (currentHp <= 0)
            {
                _destroyed = true;
                OnDestroyed();
            }
        }

        public void OnDeath() { }

        private void Update()
        {
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                {
                    for (int i = 0; i < _renderers.Length; i++)
                        if (_renderers[i] != null && i < _originalColors.Length)
                            _renderers[i].material.color = _originalColors[i];
                }
            }
        }

        /// <summary>设置灵物掉落池（由BattleRoom在生成时传入）</summary>
        public void SetItemPool(ItemData[] pool)
        {
            _itemPool = pool;
        }

        private void OnDestroyed()
        {
            var config = GameConfig.Instance;
            bool forceAll = config != null && config.debugMaxItemDropRate;

            // 掉落灵力碎片
            float shardChance = config != null ? config.可破坏物掉落概率 : dropChance;
            if (forceAll) shardChance = 1f;
            if (Random.value < shardChance && PlayerResources.Instance != null)
            {
                int shards = Random.Range(shardMin, shardMax + 1);
                PlayerResources.Instance.AddShards(shards);
            }

            // 掉落灵物（小概率）
            TryDropItem(forceAll);

            // 碎裂动画
            StartCoroutine(DestroyAnimation());
        }

        /// <summary>尝试掉落灵物</summary>
        private void TryDropItem(bool forceDrop)
        {
            if (_itemPool == null || _itemPool.Length == 0) return;

            float chance = forceDrop ? 1f : itemDropChance;
            if (Random.value >= chance) return;

            var config = GameConfig.Instance;
            ItemData selectedItem;
            if (config != null)
            {
                // 可破坏物只掉凡品/灵品（低品质）
                ItemRarity targetRarity = Random.value < 0.7f ? ItemRarity.Fan : ItemRarity.Ling;
                var candidates = new System.Collections.Generic.List<ItemData>();
                foreach (var item in _itemPool)
                    if (item != null && item.rarity == targetRarity)
                        candidates.Add(item);
                selectedItem = candidates.Count > 0
                    ? candidates[Random.Range(0, candidates.Count)]
                    : _itemPool[Random.Range(0, _itemPool.Length)];
            }
            else
            {
                selectedItem = _itemPool[Random.Range(0, _itemPool.Length)];
            }

            if (selectedItem != null)
            {
                ItemPickup.Spawn(selectedItem, transform.position);
                Debug.Log($"<color=cyan>[Destructible] 掉落灵物：{selectedItem.itemName}</color>");
            }
        }

        private System.Collections.IEnumerator DestroyAnimation()
        {
            // 生成碎片（3~5个小方块飞散）
            int fragmentCount = Random.Range(3, 6);
            var fragments = new GameObject[fragmentCount];
            Color baseColor = _originalColors.Length > 0 ? _originalColors[0] : Color.gray;

            for (int i = 0; i < fragmentCount; i++)
            {
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.name = "[Fragment]";
                frag.transform.position = transform.position + Random.insideUnitSphere * 0.3f;
                float s = Random.Range(0.15f, 0.35f);
                frag.transform.localScale = new Vector3(s, s, s);
                frag.transform.rotation = Random.rotation;

                var col = frag.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = frag.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(baseColor.r * Random.Range(0.7f, 1f),
                                          baseColor.g * Random.Range(0.7f, 1f),
                                          baseColor.b * Random.Range(0.7f, 1f));
                    rend.material = mat;
                }

                fragments[i] = frag;
            }

            // 隐藏原物体
            foreach (var r in _renderers)
                if (r != null) r.enabled = false;

            // 碎片飞散动画
            var velocities = new Vector3[fragmentCount];
            for (int i = 0; i < fragmentCount; i++)
                velocities[i] = Random.insideUnitSphere * Random.Range(2f, 5f) + Vector3.up * Random.Range(2f, 4f);

            float timer = 0.6f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                for (int i = 0; i < fragmentCount; i++)
                {
                    if (fragments[i] == null) continue;
                    velocities[i] += Vector3.down * 12f * Time.deltaTime; // 重力
                    fragments[i].transform.position += velocities[i] * Time.deltaTime;
                    fragments[i].transform.Rotate(Vector3.one * 360f * Time.deltaTime);

                    // 淡出
                    var rend = fragments[i].GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var c = rend.material.color;
                        c.a = timer / 0.6f;
                        rend.material.color = c;
                    }
                }
                yield return null;
            }

            // 清理
            foreach (var frag in fragments)
                if (frag != null) Destroy(frag);

            Destroy(gameObject);
        }

        /// <summary>
        /// 工厂方法：在指定位置生成可破坏物
        /// </summary>
        public static Destructible Spawn(Vector3 position, int type = -1)
        {
            if (type < 0) type = Random.Range(0, 3);

            GameObject go;
            Color color;
            Vector3 scale;

            switch (type)
            {
                case 0: // 木箱
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Destructible_Crate";
                    scale = new Vector3(0.8f, 0.8f, 0.8f);
                    color = new Color(0.6f, 0.4f, 0.2f); // 木色
                    break;
                case 1: // 石柱
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Destructible_Pillar";
                    scale = new Vector3(0.5f, 1.2f, 0.5f);
                    color = new Color(0.5f, 0.5f, 0.5f); // 灰色
                    break;
                default: // 灵石堆
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Destructible_Crystal";
                    scale = new Vector3(0.6f, 1f, 0.6f);
                    go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    color = new Color(0.3f, 0.5f, 0.7f); // 蓝灰色
                    break;
            }

            go.transform.position = position + Vector3.up * (scale.y * 0.5f);
            go.transform.localScale = scale;

            // 设置层级为Enemy层，这样玩家攻击可以命中
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayer >= 0 ? enemyLayer : 0;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = color;
                rend.material = mat;
            }

            // 替换碰撞体为触发器（不阻挡移动，但可被攻击检测到）
            var existingCol = go.GetComponent<Collider>();
            if (existingCol != null)
                existingCol.isTrigger = false; // 保持实体碰撞，可以阻挡

            var destructible = go.AddComponent<Destructible>();
            return destructible;
        }

        /// <summary>
        /// 工厂方法：在指定位置生成可破坏物（带灵物掉落池）
        /// </summary>
        public static Destructible Spawn(Vector3 position, ItemData[] itemPool, int type = -1)
        {
            var d = Spawn(position, type);
            if (itemPool != null && itemPool.Length > 0)
                d.SetItemPool(itemPool);
            return d;
        }
    }
}
