using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 冲锋型敌人
    /// 蓄力后向玩家方向冲锋，冲锋前有红色范围预警
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyCharger : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 50f,
            currentHp = 50f,
            attackDamage = 15f,
            moveSpeed = 2f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 14f;
        [SerializeField] private float chargeRange = 10f;
        [SerializeField] private float chargePrepTime = 1.0f;  // 蓄力时间
        [SerializeField] private float chargeSpeed = 18f;
        [SerializeField] private float chargeDuration = 0.5f;
        [SerializeField] private float chargeInterval = 4f;
        [SerializeField] private float meleeDamageRange = 1.8f;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private SkillData[] possibleSkillDrops;
        [SerializeField] private int _roomLevel;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;
        private float _stunTimer;

        // 冲锋状态
        private enum ChargerState { Idle, Tracking, Preparing, Charging, Stunned }
        private ChargerState _state = ChargerState.Idle;
        private float _stateTimer;
        private Vector3 _chargeDirection;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;

        // 预警
        private GameObject _warningIndicator;
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
            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;
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

            // 硬直
            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            switch (_state)
            {
                case ChargerState.Idle:
                case ChargerState.Tracking:
                    if (distToTarget <= detectRange)
                    {
                        // 追踪
                        MoveTowards(_target.position);

                        _attackTimer -= Time.deltaTime;
                        if (_attackTimer <= 0 && distToTarget <= chargeRange)
                        {
                            StartPreparing();
                        }
                    }
                    break;

                case ChargerState.Preparing:
                    _stateTimer -= Time.deltaTime;
                    // 蓄力时持续更新冲锋方向
                    _chargeDirection = (_target.position - transform.position).normalized;
                    _chargeDirection.y = 0;
                    UpdateWarningIndicator();

                    // 蓄力时身体抖动
                    float shake = Mathf.Sin(Time.time * 30f) * 0.05f;
                    transform.position += new Vector3(shake, 0, shake);

                    if (_stateTimer <= 0)
                        StartCharging();
                    break;

                case ChargerState.Charging:
                    _stateTimer -= Time.deltaTime;
                    Vector3 vel = _chargeDirection * chargeSpeed;
                    vel.y = -9.8f;
                    _cc.Move(vel * Time.deltaTime);

                    // 冲锋中检测碰撞玩家
                    if (distToTarget < meleeDamageRange)
                    {
                        var damageable = _target.GetComponent<IDamageable>();
                        if (damageable != null)
                            damageable.OnDamage(stats.attackDamage, transform.position, gameObject);
                        EndCharge();
                    }

                    if (_stateTimer <= 0)
                        EndCharge();
                    break;

                case ChargerState.Stunned:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0)
                    {
                        _state = ChargerState.Tracking;
                        _attackTimer = chargeInterval;
                    }
                    break;
            }

            // 朝向
            Vector3 lookDir = _state == ChargerState.Charging ? _chargeDirection :
                (_target.position - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        private void StartPreparing()
        {
            _state = ChargerState.Preparing;
            _stateTimer = chargePrepTime;
            _chargeDirection = (_target.position - transform.position).normalized;
            _chargeDirection.y = 0;
            CreateWarningIndicator();

            // 变色提示
            SetAllRenderersColor(new Color(1f, 0.5f, 0.1f));
        }

        private void StartCharging()
        {
            _state = ChargerState.Charging;
            _stateTimer = chargeDuration;
            DestroyWarningIndicator();

            SetAllRenderersColor(new Color(1f, 0.2f, 0.1f));
        }

        private void EndCharge()
        {
            _state = ChargerState.Stunned;
            _stateTimer = 1f; // 冲锋后硬直1秒
            RestoreColors();
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            Vector3 velocity = dir * stats.moveSpeed;
            velocity.y = -9.8f;
            _cc.Move(velocity * Time.deltaTime);
        }

        // ========== 预警指示器 ==========

        private void CreateWarningIndicator()
        {
            DestroyWarningIndicator();
            _warningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _warningIndicator.name = "[Warning] ChargeZone";
            var col = _warningIndicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _warningIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.2f, 0.1f, 0.25f);
                rend.material = mat;
            }
        }

        private void UpdateWarningIndicator()
        {
            if (_warningIndicator == null) return;
            float length = chargeSpeed * chargeDuration;
            Vector3 center = transform.position + _chargeDirection * (length / 2f) + Vector3.up * 0.1f;
            _warningIndicator.transform.position = center;
            _warningIndicator.transform.localScale = new Vector3(1.5f, 0.1f, length);
            _warningIndicator.transform.rotation = Quaternion.LookRotation(_chargeDirection);

            // 闪烁
            var rend = _warningIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.PingPong(Time.time * 6f, 0.3f) + 0.1f;
                rend.material.color = new Color(1f, 0.2f, 0.1f, alpha);
            }
        }

        private void DestroyWarningIndicator()
        {
            if (_warningIndicator != null)
            {
                Destroy(_warningIndicator);
                _warningIndicator = null;
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

            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            _stunTimer = 0.4f;

            // 冲锋中被打断
            if (_state == ChargerState.Preparing || _state == ChargerState.Charging)
            {
                EndCharge();
                DestroyWarningIndicator();
            }

            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.3f;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerNormal();

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";

            DestroyWarningIndicator();
            TryDropItem();
            TryDropSkill();
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
            float chance = 0.08f;
            if (config != null) chance = config.debugMaxItemDropRate ? 1f : config.敌人掉落概率;
            if (Random.value > chance) return;
            ItemData item = possibleDrops[Random.Range(0, possibleDrops.Length)];
            if (item != null) ItemPickup.Spawn(item, transform.position);
        }

        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;
            var config = GameConfig.Instance;
            float chance = 0.03f;
            if (config != null)
                chance = config.debugMaxSkillDropRate ? 1f : config.功法掉落概率;
            if (Random.value > chance) return;
            var skill = possibleSkillDrops[Random.Range(0, possibleSkillDrops.Length)];
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

        /// <summary>工厂方法：生成冲锋型敌人</summary>
        public static EnemyCharger Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.冲锋敌人Prefab : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Charger");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            if (prefab == null)
            {
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.8f, 0.5f, 0.1f);
            }

            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.5f;
            cc.height = 2f;
            cc.center = new Vector3(0, 1f, 0);

            var enemy = go.AddComponent<EnemyCharger>();
            var config = GameConfig.Instance;
            if (config != null)
            {
                enemy.stats.maxHp = config.敌人基础血量 * 1.5f * hpMultiplier;
                enemy.stats.attackDamage = config.敌人基础攻击力 * 2f * dmgMultiplier;
            }
            else
            {
                enemy.stats.maxHp = 50f * hpMultiplier;
                enemy.stats.attackDamage = 15f * dmgMultiplier;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;
            enemy._roomLevel = roomLevel;
            if (drops != null) enemy.possibleDrops = drops;

            return enemy;
        }

        private void OnDestroy()
        {
            DestroyWarningIndicator();
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
    }
}
