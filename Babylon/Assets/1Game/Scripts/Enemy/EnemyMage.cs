using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// AOE 法师敌人
    /// 在玩家脚下释放范围攻击，攻击前有红色圆形预警
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyMage : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 25f,
            currentHp = 25f,
            attackDamage = 12f,
            moveSpeed = 2f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 16f;
        [SerializeField] private float castRange = 13f;
        [SerializeField] private float preferredRange = 9f;
        [SerializeField] private float attackInterval = 3.5f;
        [SerializeField] private float warningDuration = 1.0f;
        [SerializeField] private float aoeRadius = 2.5f;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private int _roomLevel;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;
        private float _stunTimer;

        // 施法状态
        private bool _isCasting;
        private float _castTimer;
        private Vector3 _castTargetPos;
        private GameObject _warningCircle;

        // 受击表现
        private Renderer _renderer;
        private Color _originalColor;
        private float _hitFlashTimer;

        private EnemyHealthBar _healthBar;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                _originalColor = _renderer.material.color;
        }

        private void Start()
        {
            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;
            _attackTimer = attackInterval * 0.5f; // 首次攻击快一些
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0 && _renderer != null)
                    _renderer.material.color = _originalColor;
            }

            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            // 施法中
            if (_isCasting)
            {
                _castTimer -= Time.deltaTime;
                UpdateWarningCircle();

                if (_castTimer <= 0)
                {
                    ExecuteAOE();
                    _isCasting = false;
                    DestroyWarningCircle();
                    _attackTimer = attackInterval;
                }
                return;
            }

            // 保持距离
            if (distToTarget < preferredRange - 2f)
            {
                Vector3 fleeDir = (transform.position - _target.position).normalized;
                fleeDir.y = 0;
                Vector3 velocity = fleeDir * stats.moveSpeed;
                velocity.y = -9.8f;
                _cc.Move(velocity * Time.deltaTime);
            }
            else if (distToTarget > castRange)
            {
                MoveTowards(_target.position);
            }

            // 攻击
            if (distToTarget <= castRange)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                    StartCasting();
            }

            // 朝向
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

        private void StartCasting()
        {
            _isCasting = true;
            _castTimer = warningDuration;
            _castTargetPos = _target.position; // 锁定目标位置
            CreateWarningCircle();

            if (_renderer != null)
                _renderer.material.color = new Color(0.6f, 0.1f, 0.8f); // 紫色蓄力
        }

        private void CreateWarningCircle()
        {
            DestroyWarningCircle();
            _warningCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _warningCircle.name = "[Warning] AOEZone";
            _warningCircle.transform.position = _castTargetPos + Vector3.up * 0.05f;
            _warningCircle.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);

            var col = _warningCircle.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _warningCircle.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.8f, 0.1f, 0.1f, 0.2f);
                rend.material = mat;
            }
        }

        private void UpdateWarningCircle()
        {
            if (_warningCircle == null) return;

            // 逐渐变红变不透明
            float progress = 1f - (_castTimer / warningDuration);
            var rend = _warningCircle.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.5f, progress);
                float pulse = Mathf.PingPong(Time.time * 5f, 0.15f);
                rend.material.color = new Color(0.9f, 0.1f, 0.1f, alpha + pulse);
            }

            // 缩放脉冲
            float scale = aoeRadius * 2f * (1f + Mathf.Sin(Time.time * 8f) * 0.03f);
            _warningCircle.transform.localScale = new Vector3(scale, 0.05f, scale);
        }

        private void ExecuteAOE()
        {
            if (_renderer != null)
                _renderer.material.color = _originalColor;

            // 创建爆炸视觉效果
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "[VFX] AOE_Explosion";
            explosion.transform.position = _castTargetPos + Vector3.up * 0.5f;
            explosion.transform.localScale = Vector3.one * aoeRadius * 2f;
            var expCol = explosion.GetComponent<Collider>();
            if (expCol != null) Destroy(expCol);

            var expRend = explosion.GetComponent<Renderer>();
            if (expRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.8f, 0.2f, 0.8f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 0.8f) * 3f);
                expRend.material = mat;
            }
            Destroy(explosion, 0.5f);

            // 范围伤害
            int playerLayer = LayerMask.GetMask("Default"); // 玩家在Default层
            var hits = Physics.OverlapSphere(_castTargetPos, aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                        damageable.OnDamage(stats.attackDamage, _castTargetPos, gameObject);
                }
            }
        }

        private void DestroyWarningCircle()
        {
            if (_warningCircle != null)
            {
                Destroy(_warningCircle);
                _warningCircle = null;
            }
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;
            float actual = stats.TakeDamage(damage);

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual, IsCrit = false, IsPlayerDamage = false
            });

            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            if (_renderer != null)
            {
                _renderer.material.color = Color.white;
                _hitFlashTimer = 0.1f;
            }

            _stunTimer = 0.35f;
            if (_isCasting)
            {
                _isCasting = false;
                DestroyWarningCircle();
                if (_renderer != null) _renderer.material.color = _originalColor;
            }

            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.4f;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerNormal();
            if (!stats.IsAlive) OnDeath();
        }

        public void OnDeath()
        {
            DestroyWarningCircle();
            TryDropItem();
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject, Position = transform.position
            });

            if (PlayerController.Instance != null)
            {
                float healAmount = PlayerController.Instance.Inventory.GetHealOnKill();
                if (healAmount > 0) PlayerController.Instance.Stats.Heal(healAmount);
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerKill();
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem()
        {
            if (possibleDrops == null || possibleDrops.Length == 0) return;
            var config = GameConfig.Instance;
            float chance = 0.3f;
            if (config != null) chance = config.enemyDropChance + _roomLevel * config.dropChancePerLevel;
            if (Random.value > chance) return;
            ItemData item = possibleDrops[Random.Range(0, possibleDrops.Length)];
            if (item != null) ItemPickup.Spawn(item, transform.position);
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            enabled = false;
            _cc.enabled = false;
            float timer = 0.3f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                transform.localScale = startScale * (timer / 0.3f);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>工厂方法：生成AOE法师</summary>
        public static EnemyMage Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Enemy_Mage";
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            go.transform.position = position;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.5f, 0.2f, 0.8f); // 紫色

            var defaultCol = go.GetComponent<Collider>();
            if (defaultCol != null) Object.Destroy(defaultCol);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 2f;

            var enemy = go.AddComponent<EnemyMage>();
            var config = GameConfig.Instance;
            if (config != null)
            {
                enemy.stats.maxHp = config.enemyBaseHp * 0.8f * hpMultiplier;
                enemy.stats.attackDamage = config.enemyBaseAttack * 1.5f * dmgMultiplier;
            }
            else
            {
                enemy.stats.maxHp = 25f * hpMultiplier;
                enemy.stats.attackDamage = 12f * dmgMultiplier;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;
            enemy._roomLevel = roomLevel;
            if (drops != null) enemy.possibleDrops = drops;

            return enemy;
        }

        private void OnDestroy()
        {
            DestroyWarningCircle();
        }
    }
}
