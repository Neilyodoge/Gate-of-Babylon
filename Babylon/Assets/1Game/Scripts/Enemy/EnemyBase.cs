using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 基础敌人 AI
    /// Demo1: 简单追踪玩家 + 近战攻击
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 30f,
            currentHp = 30f,
            attackDamage = 5f,
            moveSpeed = 3f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 12f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackInterval = 1.5f;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private float dropChance = 0.3f;
        [SerializeField] private int _roomLevel; // 当前房间层数，用于掉率计算

        [Header("受击特效")]
        [SerializeField] private GameObject hitVFXPrefab;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;
        private float _stunTimer; // 受击硬直计时器

        // 攻击预警
        private GameObject _attackWarning;
        private float _attackPrepTimer;
        private bool _isPreparing;
        private float _warningDuration = 0.5f;

        // 血条
        private EnemyHealthBar _healthBar;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material.color;
        }

        private void Start()
        {
            // 应用 GameConfig 配置
            var config = GameConfig.Instance;
            if (config != null)
            {
                stats.moveSpeed = config.敌人移动速度;
                detectRange = config.敌人检测范围;
                attackRange = config.敌人攻击范围;
                attackInterval = config.敌人攻击间隔;
            }

            stats.ResetHp();

            // 创建头顶血条
            _healthBar = EnemyHealthBar.Create(gameObject);

            // 寻找玩家
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;
        }

        private void Update()
        {
            if (!stats.IsAlive) return;
            if (_target == null) return;

            // 受击闪烁恢复
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                    RestoreColors();
            }

            // 硬直中不行动
            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            // 攻击预警阶段
            if (_isPreparing)
            {
                _attackPrepTimer -= Time.deltaTime;
                UpdateAttackWarning();
                if (_attackPrepTimer <= 0)
                {
                    Attack();
                    _isPreparing = false;
                    DestroyAttackWarning();
                    _attackTimer = attackInterval;
                }
                return;
            }

            if (distToTarget <= attackRange)
            {
                // 攻击
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                {
                    StartAttackPrep();
                }
            }
            else if (distToTarget <= detectRange)
            {
                // 追踪
                MoveTowards(_target.position);
            }

            // 朝向目标
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            Vector3 velocity = dir * stats.moveSpeed;
            velocity.y = -9.8f;
            _cc.Move(velocity * Time.deltaTime);
        }

        private void StartAttackPrep()
        {
            _isPreparing = true;
            _attackPrepTimer = _warningDuration;
            CreateAttackWarning();

            // 蓄力时变色
            SetAllRenderersColor(new Color(1f, 0.4f, 0.2f));
        }

        private void CreateAttackWarning()
        {
            DestroyAttackWarning();
            _attackWarning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _attackWarning.name = "[Warning] MeleeZone";
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;
            _attackWarning.transform.localScale = new Vector3(attackRange * 2f, 0.05f, attackRange * 2f);

            var col = _attackWarning.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _attackWarning.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.2f, 0.1f, 0.2f);
                rend.material = mat;
            }
        }

        private void UpdateAttackWarning()
        {
            if (_attackWarning == null) return;
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;

            float progress = 1f - (_attackPrepTimer / _warningDuration);
            var rend = _attackWarning.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.45f, progress);
                rend.material.color = new Color(1f, 0.2f, 0.1f, alpha);
            }
        }

        private void DestroyAttackWarning()
        {
            if (_attackWarning != null)
            {
                Destroy(_attackWarning);
                _attackWarning = null;
            }
        }

        private void Attack()
        {
            if (_target == null) return;

            RestoreColors();

            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float damage = stats.CalculateDamage();
                damageable.OnDamage(damage, transform.position, gameObject);
            }
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;

            float actual = stats.TakeDamage(damage);

            // 发布伤害飘字事件
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = actual > damage * 0.9f && damage > stats.attackDamage, // 简单判断是否暴击
                IsPlayerDamage = false
            });

            // 更新血条
            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            // 受击闪白
            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            // 播放受击特效
            if (hitVFXPrefab != null)
            {
                Vector3 vfxPos = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up;
                GameObject vfx;
                if (ObjectPool.Instance != null)
                {
                    vfx = ObjectPool.Instance.Get(hitVFXPrefab, vfxPos, Quaternion.identity);
                    ObjectPool.Instance.Return(vfx, 1f);
                }
                else
                {
                    vfx = Instantiate(hitVFXPrefab, vfxPos, Quaternion.identity);
                    Destroy(vfx, 1f);
                }
            }

            // 硬直
            _stunTimer = 0.3f;
            _isPreparing = false;
            DestroyAttackWarning();

            // 击退
            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.5f;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            // 顿帧
            if (HitStop.Instance != null)
                HitStop.Instance.TriggerNormal();

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            // 立即清除Tag，防止FindGameObjectsWithTag计数错误
            gameObject.tag = "Untagged";

            DestroyAttackWarning();

            // 掉落灵物
            TryDropItem();

            // 发布事件
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            // 击杀回复
            if (PlayerController.Instance != null)
            {
                float healAmount = PlayerController.Instance.Inventory.GetHealOnKill();
                if (healAmount > 0)
                    PlayerController.Instance.Stats.Heal(healAmount);
            }

            // 击杀顿帧
            if (HitStop.Instance != null)
                HitStop.Instance.TriggerKill();

            // 死亡表现（简单缩小消失）
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem()
        {
            if (possibleDrops == null || possibleDrops.Length == 0) return;

            // 使用 GameConfig 的掉率
            var config = GameConfig.Instance;
            float chance = dropChance;
            if (config != null)
                chance = config.敌人掉落概率 + _roomLevel * config.每层掉率增加;

            if (Random.value > chance) return;

            // 按品阶权重选择灵物
            ItemData selectedItem = null;
            if (config != null)
            {
                // 先随机一个品阶，再从该品阶的灵物中选
                ItemRarity targetRarity = config.RollRarity();

                // 从可掉落列表中筛选该品阶的灵物
                var candidates = new System.Collections.Generic.List<ItemData>();
                foreach (var item in possibleDrops)
                {
                    if (item != null && item.rarity == targetRarity)
                        candidates.Add(item);
                }

                // 如果该品阶没有灵物，降级选择
                if (candidates.Count == 0)
                {
                    // 回退到随机选择
                    selectedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
                }
                else
                {
                    selectedItem = candidates[Random.Range(0, candidates.Count)];
                }
            }
            else
            {
                selectedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
            }

            if (selectedItem != null)
            {
                // 在敌人脚下掉落（当前位置）
                ItemPickup.Spawn(selectedItem, transform.position);
            }
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            // 禁用 AI
            enabled = false;
            _cc.enabled = false;

            float timer = 0.3f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.3f;
                transform.localScale = startScale * t;
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 工厂方法：生成一个基础敌人
        /// </summary>
        public static EnemyBase Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.普通小怪Prefab : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            // 递归设置所有子物体的Layer
            SetLayerRecursively(go, go.layer);

            // 如果是Prefab模型，不需要设置颜色；如果是回退胶囊体，设置红色
            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.8f, 0.2f, 0.2f);
            }

            // 移除所有碰撞体（Prefab可能自带），CharacterController 自带
            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 2f;
            cc.center = new Vector3(0, 1f, 0);

            var enemy = go.AddComponent<EnemyBase>();

            // 应用 GameConfig 基础属性
            var config = GameConfig.Instance;
            if (config != null)
            {
                enemy.stats.maxHp = config.敌人基础血量 * hpMultiplier;
                enemy.stats.attackDamage = config.敌人基础攻击力 * dmgMultiplier;
            }
            else
            {
                enemy.stats.maxHp *= hpMultiplier;
                enemy.stats.attackDamage *= dmgMultiplier;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;

            // 设置房间层数和掉落池
            enemy._roomLevel = roomLevel;
            if (drops != null)
                enemy.possibleDrops = drops;

            return enemy;
        }

        /// <summary>设置受击特效 Prefab</summary>
        public void SetHitVFXPrefab(GameObject prefab)
        {
            hitVFXPrefab = prefab;
        }

        /// <summary>设置所有Renderer的颜色</summary>
        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) r.material.color = color;
        }

        /// <summary>恢复所有Renderer的原始颜色</summary>
        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && i < _originalColors.Length)
                    _renderers[i].material.color = _originalColors[i];
        }

        /// <summary>递归设置所有子物体的Layer</summary>
        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
