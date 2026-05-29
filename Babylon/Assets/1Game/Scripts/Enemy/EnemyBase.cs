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
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private SkillData[] possibleSkillDrops;
        [SerializeField] private float dropChance = 0.05f;
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
                if (_renderers[i] != null) _renderers[i].material.color = new Color(0.4f, 0.8f, 1f, 1f);
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

            // 闪避CD更新
            if (_dodgeTimer > 0) _dodgeTimer -= Time.deltaTime;

            // 闪避中
            if (_isDodging)
            {
                _dodgeDuration -= Time.deltaTime;
                Vector3 dodgeVel = _dodgeDirection * stats.moveSpeed * 3f;
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
                    Vector3 strafeVel = strafeDir * stats.moveSpeed * 0.6f;
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
                    var c = r.material.color;
                    c.a = 0.4f;
                    r.material.color = c;
                }
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

            // 掉落功法
            TryDropSkill();

            // v0.5 搜打撤：普通怪 8% 概率掉一件【洞府素材】（贯穿"搜"的随机喜悦）
            float caveChance = 0.08f;
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f)), caveChance);

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
            if (possibleDrops == null || possibleDrops.Length == 0)
            {
                Debug.LogWarning($"<color=red>[Drop] {gameObject.name} possibleDrops为空（null={possibleDrops == null}），跳过灵物掉落！请检查灵物池是否正确传入。</color>");
                return;
            }

            // 使用 GameConfig 的固定掉率（不随层数增加）
            var config = GameConfig.Instance;
            float chance = dropChance;
            if (config != null)
            {
                chance = config.debugMaxItemDropRate ? 1f : config.敌人掉落概率;
            }

            float roll = Random.value;
            Debug.Log($"<color=yellow>[Drop] {gameObject.name} 灵物掉落判定: chance={chance}, roll={roll}, debugMaxItemDropRate={config?.debugMaxItemDropRate}, possibleDrops={possibleDrops.Length}个</color>");
            if (roll > chance)
            {
                if (config != null && config.debugMaxItemDropRate)
                    Debug.LogWarning($"[Drop] 灵物爆率拉满但未掉落？chance={chance}, roll={roll}");
                return;
            }

            // 先过滤掉 null 元素
            var validDrops = new System.Collections.Generic.List<ItemData>();
            foreach (var d in possibleDrops)
                if (d != null) validDrops.Add(d);

            if (validDrops.Count == 0)
            {
                Debug.LogWarning($"[Drop] {gameObject.name} possibleDrops 全部为 null，跳过掉落");
                return;
            }

            // 按品阶权重选择灵物（层数越高高品质比重越大）
            ItemData selectedItem = null;
            if (config != null)
            {
                ItemRarity targetRarity = config.RollRarity(_roomLevel);

                var candidates = new System.Collections.Generic.List<ItemData>();
                foreach (var item in validDrops)
                {
                    if (item.rarity == targetRarity)
                        candidates.Add(item);
                }

                selectedItem = candidates.Count > 0
                    ? candidates[Random.Range(0, candidates.Count)]
                    : validDrops[Random.Range(0, validDrops.Count)];
            }
            else
            {
                selectedItem = validDrops[Random.Range(0, validDrops.Count)];
            }

            if (selectedItem != null)
            {
                // 在敌人脚下掉落（当前位置），确保Y坐标在地面上
                Vector3 dropPos = transform.position;
                dropPos.y = Mathf.Max(dropPos.y, 0.1f); // 确保不在地面以下
                var pickup = ItemPickup.Spawn(selectedItem, dropPos);
                Debug.Log($"<color=green>[Drop] ✓ 灵物已生成：{selectedItem.itemName}，位置={dropPos}，pickup={pickup != null}</color>");
            }
            else
            {
                Debug.LogWarning($"[Drop] selectedItem 为 null！possibleDrops 中可能有空元素");
            }
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

            var skill = possibleSkillDrops[Random.Range(0, possibleSkillDrops.Length)];
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
