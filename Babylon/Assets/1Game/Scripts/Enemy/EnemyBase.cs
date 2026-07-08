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

        // AI行为增强
        private float _strafeTimer;       // 绕行计时器
        private float _strafeDirection;   // 绕行方向 (1 或 -1)
        private float _dodgeTimer;        // 闪避CD
        private bool _isDodging;          // 是否在闪避中
        private float _dodgeDuration;     // 闪避持续时间
        private Vector3 _dodgeDirection;  // 闪避方向
        private enum AIState { Idle, Chase, Strafe, AttackPrep, Dodge }
        private AIState _aiState = AIState.Idle;

        [Header("掉落")]
        [SerializeField] private SkillData[] possibleSkillDrops;

        [Header("受击特效")]
        [SerializeField] private GameObject hitVFXPrefab;

        private CharacterController _cc;
        private Transform _target;
        private Transform _playerTarget; // 缓存真正的玩家目标（水镜分身嘲讽时临时改打分身）
        private float _attackTimer;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;
        private float _stunTimer; // 受击硬直计时器
        private float _slowTimer; // 减速剩余时间
        private float _slowMul = 1f; // 减速期间的移速倍率（1=无减速）

        /// <summary>当前有效移速（计入区域/技能减速）。</summary>
        private float MoveSpeed => _slowTimer > 0f ? stats.moveSpeed * _slowMul : stats.moveSpeed;

        /// <summary>
        /// 减速：在指定时长内降低移速。由持续区域技能（冥河/剑阵/黑洞等）调用。
        /// 取更强的减速 + 更长的时间（不叠乘，避免区域重叠时归零）。
        /// </summary>
        public void ApplySlow(float duration, float slowPct)
        {
            if (duration <= 0f || slowPct <= 0f) return;
            float mul = Mathf.Clamp01(1f - slowPct);
            _slowMul = Mathf.Min(_slowMul, mul);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        // 攻击预警
        private GameObject _attackWarning;
        private float _attackPrepTimer;
        private bool _isPreparing;
        private float _warningDuration = 0.5f;

        // 血条
        private EnemyHealthBar _healthBar;

        public CombatStats Stats => stats;

        /// <summary>
        /// 冻结：在 stunTimer 上叠加时间，使敌人在指定时长内不能行动 / 攻击。
        /// 由 <see cref="SkillModifierApplier"/>（GDD 6.5 寒冰玉髓修饰）调用。
        /// </summary>
        public void ApplyFreeze(float duration)
        {
            if (duration <= 0f) return;
            _stunTimer = Mathf.Max(_stunTimer, duration);
            // 简化视觉提示：闪一下蓝色
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) MaterialHelper.SafeSetColor(_renderers[i].material, new Color(0.4f, 0.8f, 1f, 1f));
            _hitFlashTimer = duration;
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 1.5f,
                Damage = 0,
                SpecialTag = "冻结"
            });
        }

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
            {
                _playerTarget = PlayerController.Instance.transform;
                _target = _playerTarget;
            }
        }

        private void Update()
        {
            if (!stats.IsAlive) return;

            // 水镜分身嘲讽：存在分身时改打分身，否则打玩家
            if (_playerTarget == null && PlayerController.Instance != null)
                _playerTarget = PlayerController.Instance.transform;
            _target = WaterMirrorDecoy.ActiveTransform != null ? WaterMirrorDecoy.ActiveTransform : _playerTarget;

            if (_target == null) return;

            // 受击闪烁恢复
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                    RestoreColors();
            }

            // 减速计时（到期恢复满速）
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f) _slowMul = 1f;
            }

            // 硬直中不行动
            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            // 闪避CD更新
            if (_dodgeTimer > 0) _dodgeTimer -= Time.deltaTime;

            // 闪避中
            if (_isDodging)
            {
                _dodgeDuration -= Time.deltaTime;
                Vector3 dodgeVel = _dodgeDirection * MoveSpeed * 3f;
                dodgeVel.y = -9.8f;
                _cc.Move(dodgeVel * Time.deltaTime);
                if (_dodgeDuration <= 0)
                {
                    _isDodging = false;
                    _aiState = AIState.Chase;
                }
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
                    _aiState = AIState.Chase;
                }
                return;
            }

            // AI状态机
            switch (_aiState)
            {
                case AIState.Idle:
                    if (distToTarget <= detectRange)
                        _aiState = AIState.Chase;
                    break;

                case AIState.Chase:
                    if (distToTarget <= attackRange)
                    {
                        _attackTimer -= Time.deltaTime;
                        if (_attackTimer <= 0)
                        {
                            StartAttackPrep();
                            _aiState = AIState.AttackPrep;
                        }
                        else
                        {
                            // 攻击CD中绕行
                            _aiState = AIState.Strafe;
                            _strafeTimer = Random.Range(0.5f, 1.5f);
                            _strafeDirection = Random.value > 0.5f ? 1f : -1f;
                        }
                    }
                    else if (distToTarget <= detectRange)
                    {
                        MoveTowards(_target.position);
                    }
                    break;

                case AIState.Strafe:
                    _strafeTimer -= Time.deltaTime;
                    if (_strafeTimer <= 0 || distToTarget > attackRange * 1.5f)
                    {
                        _aiState = AIState.Chase;
                        break;
                    }

                    // 绕行移动（围绕玩家侧向移动）
                    Vector3 toPlayer = (_target.position - transform.position).normalized;
                    Vector3 strafeDir = Vector3.Cross(Vector3.up, toPlayer) * _strafeDirection;
                    Vector3 strafeVel = strafeDir * MoveSpeed * 0.6f;
                    strafeVel.y = -9.8f;
                    _cc.Move(strafeVel * Time.deltaTime);

                    // 绕行中仍然检查攻击
                    if (distToTarget <= attackRange)
                    {
                        _attackTimer -= Time.deltaTime;
                        if (_attackTimer <= 0)
                        {
                            StartAttackPrep();
                            _aiState = AIState.AttackPrep;
                        }
                    }
                    break;

                case AIState.AttackPrep:
                    // 由_isPreparing处理
                    break;
            }

            // 朝向目标
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        /// <summary>尝试闪避（受击后概率触发）</summary>
        private void TryDodge(GameObject attacker)
        {
            if (attacker == null) return;
            _isDodging = true;
            _dodgeDuration = 0.2f;
            _dodgeTimer = 3f; // 闪避CD 3秒

            // 闪避方向：远离攻击者的侧向
            Vector3 awayDir = (transform.position - attacker.transform.position).normalized;
            float side = Random.value > 0.5f ? 1f : -1f;
            _dodgeDirection = (awayDir + Vector3.Cross(Vector3.up, awayDir) * side).normalized;
            _dodgeDirection.y = 0;

            // 闪避视觉：变半透明
            foreach (var r in _renderers)
                if (r != null)
                {
                    var c = MaterialHelper.SafeGetColor(r.material);
                    c.a = 0.4f;
                    MaterialHelper.SafeSetColor(r.material, c);
                }
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            Vector3 velocity = dir * MoveSpeed;
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
                float targetDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                var (damage, _) = stats.CalcMeleeDamage(targetDef);
                damageable.OnDamage(damage, transform.position, gameObject);
            }
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;

            float actual = stats.TakeDamage(damage);
            bool isCrit = actual > damage * 0.9f && damage > stats.attackDamage; // 简单判断是否暴击

            // 累计本局玩家总伤害（轮回一击按此结算）
            if (attacker != null && PlayerController.Instance != null && attacker == PlayerController.Instance.gameObject)
                RunCombatStats.AddPlayerDamage(actual);

            // 发布伤害飘字事件
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = isCrit,
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
            else
            {
                // 兜底打击火花：未配 hitVFXPrefab 时也保证每次命中都有打击点（手感一致性）
                Vector3 sparkPos = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up;
                FxFactory.SpawnElementBurst(sparkPos, ElementTag.None, isCrit ? 0.9f : 0.6f, 0.18f);
            }

            // 硬直
            _stunTimer = 0.3f;
            _isPreparing = false;
            DestroyAttackWarning();

            // 受击后有概率闪避（30%概率，当血量低于50%时提高到50%）
            float dodgeChance = stats.currentHp < stats.maxHp * 0.5f ? 0.5f : 0.3f;
            if (_dodgeTimer <= 0 && Random.value < dodgeChance)
            {
                _stunTimer = 0; // 取消硬直，立即闪避
                TryDodge(attacker);
            }

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

            // 暴击：轻微震屏强调
            if (isCrit)
                CameraShake.TriggerLight();

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            // 立即清除Tag，防止FindGameObjectsWithTag计数错误
            gameObject.tag = "Untagged";

            DestroyAttackWarning();

            // 掉落功法
            TryDropSkill();

            // 搜打撤：普通怪 8% 概率掉一件【洞府素材】（贯穿"搜"的随机喜悦）
            float caveChance = 0.08f;
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f)), caveChance);

            // 发布事件
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            // 击杀回复（模块系统处理）

            // 击杀顿帧
            if (HitStop.Instance != null)
                HitStop.Instance.TriggerKill();

            // 死亡表现（简单缩小消失）
            StartCoroutine(DeathAnimation());
        }

        /// <summary>尝试掉落功法</summary>
        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;

            var config = GameConfig.Instance;
            float chance = 0.03f;
            if (config != null)
            {
                chance = config.debugMaxSkillDropRate ? 1f : config.功法掉落概率;
            }

            if (Random.value > chance) return;

            var skill = SkillPickup.PickValid(possibleSkillDrops);
            if (skill != null)
            {
                SkillPickup.Spawn(skill, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
                Debug.Log($"<color=cyan>敌人掉落功法：{skill.skillName}</color>");
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
        public static EnemyBase Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f)
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
                var er = LevelDesign.ConfigDatabase.Instance?.GetEnemy(1); // Normal
                enemy.stats.maxHp = config.敌人基础血量 * (er?.HpMul ?? 1f) * hpMultiplier;
                enemy.stats.attackDamage = config.敌人基础攻击力 * (er?.DmgMul ?? 1f) * dmgMultiplier;
                enemy.stats.defense = config.敌人基础防御力 * (er?.DefMul ?? 1f);
            }
            else
            {
                enemy.stats.maxHp *= hpMultiplier;
                enemy.stats.attackDamage *= dmgMultiplier;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;

            return enemy;
        }

        /// <summary>设置功法掉落池</summary>
        public void SetSkillDrops(SkillData[] skills)
        {
            possibleSkillDrops = skills;
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

        /// <summary>递归设置所有子物体的Layer</summary>
        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
