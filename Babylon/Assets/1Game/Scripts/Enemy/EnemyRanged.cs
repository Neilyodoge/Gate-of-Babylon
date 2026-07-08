using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 远程弓箭手敌人
    /// 保持距离，发射投射物攻击玩家
    /// 攻击前有红色预警线
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyRanged : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 20f,
            currentHp = 20f,
            attackDamage = 8f,
            moveSpeed = 2.5f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 18f;
        [SerializeField] private float attackRange = 12f;
        [SerializeField] private float preferredRange = 8f;  // 偏好保持的距离
        [SerializeField] private float fleeRange = 4f;       // 太近就后退
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private float warningDuration = 0.6f; // 攻击预警时间

        [Header("投射物")]
        [SerializeField] private float projectileSpeed = 10f;

        [Header("掉落")]
        [SerializeField] private SkillData[] possibleSkillDrops;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;
        private bool _isWarning;
        private float _warningTimer;
        private Vector3 _warningDirection;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;
        private float _stunTimer; // 硬直计时器

        // 闪避行为
        private float _dodgeTimer;
        private bool _isDodging;
        private float _dodgeDuration;
        private Vector3 _dodgeDirection;

        // 血条
        private EnemyHealthBar _healthBar;

        // 预警线
        private LineRenderer _warningLine;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = MaterialHelper.SafeGetColor(_renderers[i].material);
        }

        private void Start()
        {
            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);

            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            // 创建预警线
            CreateWarningLine();
        }

        private void CreateWarningLine()
        {
            var lineGo = new GameObject("WarningLine");
            lineGo.transform.SetParent(transform);
            _warningLine = lineGo.AddComponent<LineRenderer>();
            _warningLine.startWidth = 0.08f;
            _warningLine.endWidth = 0.08f;
            _warningLine.material = new Material(Shader.Find("Sprites/Default"));
            _warningLine.startColor = new Color(1f, 0.2f, 0.1f, 0.8f);
            _warningLine.endColor = new Color(1f, 0.2f, 0.1f, 0.2f);
            _warningLine.positionCount = 2;
            _warningLine.enabled = false;
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

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

            // 闪避CD
            if (_dodgeTimer > 0) _dodgeTimer -= Time.deltaTime;

            // 闪避中
            if (_isDodging)
            {
                _dodgeDuration -= Time.deltaTime;
                Vector3 dodgeVel = _dodgeDirection * stats.moveSpeed * 4f;
                dodgeVel.y = -9.8f;
                _cc.Move(dodgeVel * Time.deltaTime);
                if (_dodgeDuration <= 0)
                    _isDodging = false;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            // 攻击预警阶段
            if (_isWarning)
            {
                _warningTimer -= Time.deltaTime;
                UpdateWarningLine();
                if (_warningTimer <= 0)
                {
                    FireProjectile();
                    _isWarning = false;
                    _warningLine.enabled = false;
                    _attackTimer = attackInterval;
                }
                return;
            }

            // 太近就后退
            if (distToTarget < fleeRange)
            {
                Vector3 fleeDir = (transform.position - _target.position).normalized;
                fleeDir.y = 0;
                Vector3 velocity = fleeDir * stats.moveSpeed;
                velocity.y = -9.8f;
                _cc.Move(velocity * Time.deltaTime);
            }
            // 在攻击范围内
            else if (distToTarget <= attackRange && distToTarget >= fleeRange)
            {
                // 如果不在偏好距离，调整位置
                if (Mathf.Abs(distToTarget - preferredRange) > 2f)
                {
                    Vector3 dir = distToTarget < preferredRange
                        ? (transform.position - _target.position).normalized
                        : (_target.position - transform.position).normalized;
                    dir.y = 0;
                    Vector3 velocity = dir * stats.moveSpeed * 0.5f;
                    velocity.y = -9.8f;
                    _cc.Move(velocity * Time.deltaTime);
                }

                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                {
                    StartWarning();
                }
            }
            // 追踪
            else if (distToTarget <= detectRange)
            {
                Vector3 dir = (_target.position - transform.position).normalized;
                dir.y = 0;
                Vector3 velocity = dir * stats.moveSpeed;
                velocity.y = -9.8f;
                _cc.Move(velocity * Time.deltaTime);
            }

            // 朝向目标
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        private void StartWarning()
        {
            _isWarning = true;
            _warningTimer = warningDuration;
            _warningDirection = (_target.position - transform.position).normalized;
            _warningDirection.y = 0;
            _warningLine.enabled = true;
        }

        private void UpdateWarningLine()
        {
            Vector3 start = transform.position + Vector3.up * 0.8f;
            Vector3 end = start + _warningDirection * attackRange;
            _warningLine.SetPosition(0, start);
            _warningLine.SetPosition(1, end);

            // 闪烁效果
            float alpha = Mathf.PingPong(Time.time * 8f, 1f) * 0.8f;
            _warningLine.startColor = new Color(1f, 0.2f, 0.1f, alpha);
        }

        private void FireProjectile()
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.8f + _warningDirection * 0.5f;

            // 创建简单投射物
            var projGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projGo.name = "EnemyProjectile";
            projGo.transform.position = spawnPos;
            projGo.transform.localScale = Vector3.one * 0.3f;
            projGo.layer = gameObject.layer;

            var rend = projGo.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(1f, 0.3f, 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.1f) * 2f);
                rend.material = mat;
            }

            // 替换碰撞体为Trigger
            var col = projGo.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var sc = projGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.5f;

            var rb = projGo.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            var ep = projGo.AddComponent<EnemyProjectile>();
            ep.Initialize(stats.attackDamage, _warningDirection, projectileSpeed);

            Object.Destroy(projGo, 5f);
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;

            float actual = stats.TakeDamage(damage);

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = false,
                IsPlayerDamage = false
            });

            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            // 受击闪白
            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            // 硬直
            _stunTimer = 0.3f;
            _isWarning = false;
            if (_warningLine != null) _warningLine.enabled = false;

            // 远程敌人受击后40%概率后跳闪避
            if (_dodgeTimer <= 0 && Random.value < 0.4f && attacker != null)
            {
                _stunTimer = 0;
                _isDodging = true;
                _dodgeDuration = 0.25f;
                _dodgeTimer = 4f;
                _dodgeDirection = (transform.position - attacker.transform.position).normalized;
                _dodgeDirection.y = 0;
            }

            // 击退
            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.3f;
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
            gameObject.tag = "Untagged";

            TryDropSkill();
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            // 击杀回复（模块系统处理）

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerKill();

            StartCoroutine(DeathAnimation());
        }

        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;
            var config = GameConfig.Instance;
            float chance = 0.03f;
            if (config != null)
                chance = config.debugMaxSkillDropRate ? 1f : config.功法掉落概率;
            if (Random.value > chance) return;
            var skill = SkillPickup.PickValid(possibleSkillDrops);
            if (skill != null)
                SkillPickup.Spawn(skill, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
        }

        /// <summary>设置功法掉落池</summary>
        public void SetSkillDrops(SkillData[] skills) => possibleSkillDrops = skills;

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

        /// <summary>工厂方法：生成远程弓箭手</summary>
        public static EnemyRanged Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.远程敌人Prefab : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Ranged");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            if (prefab == null)
            {
                go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.2f, 0.6f, 0.2f);
            }

            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            var enemy = go.AddComponent<EnemyRanged>();
            var config = GameConfig.Instance;
            if (config != null)
            {
                var er = LevelDesign.ConfigDatabase.Instance?.GetEnemy(3); // Ranged
                enemy.stats.maxHp = config.敌人基础血量 * (er?.HpMul ?? 0.7f) * hpMultiplier;
                enemy.stats.attackDamage = config.敌人基础攻击力 * (er?.DmgMul ?? 1.2f) * dmgMultiplier;
                enemy.stats.defense = config.敌人基础防御力 * (er?.DefMul ?? 0.5f);
            }
            else
            {
                enemy.stats.maxHp = 20f * hpMultiplier;
                enemy.stats.attackDamage = 8f * dmgMultiplier;
                enemy.stats.defense = 1.5f;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;

            return enemy;
        }

        /// <summary>设置所有Renderer的颜色</summary>
        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) MaterialHelper.SafeSetColor(r.material, color);
        }

        /// <summary>恢复所有Renderer的原始颜色</summary>
        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && i < _originalColors.Length)
                    MaterialHelper.SafeSetColor(_renderers[i].material, _originalColors[i]);
        }
    }
}
