using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 精英怪词缀类型
    /// </summary>
    public enum EliteAffix
    {
        /// <summary>狂暴：攻击速度+50%，移动速度+30%</summary>
        Berserk,
        /// <summary>铁壁：减伤40%，击退抗性</summary>
        Ironwall,
        /// <summary>分裂：死亡时分裂为2个小怪</summary>
        Splitting,
        /// <summary>雷电：攻击附带范围闪电链</summary>
        Lightning,
        /// <summary>吸血：攻击回复自身10%伤害的生命</summary>
        Vampiric,
        /// <summary>冰霜：攻击减速玩家30%持续2秒</summary>
        Frost
    }

    /// <summary>
    /// 精英怪 —— 带词缀的强化敌人
    /// 比普通怪更强，有特殊词缀效果
    /// 头顶有金色"精英"标识和词缀名称
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyElite : MonoBehaviour, IDamageable, IEnemyAbilityExecutor
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 90f,
            currentHp = 90f,
            attackDamage = 12f,
            moveSpeed = 3.5f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 15f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackInterval = 0.9f;
        [SerializeField] private int maxConcurrentMeleeAttackers = 3;
        [SerializeField] private EnemyAbilityProfile abilityProfile;
        [SerializeField] private float tacticalRangeMultiplier = 2.25f;
        [SerializeField, Range(0f, 1f)] private float tacticalPauseChance = 0.08f;
        [SerializeField] private Vector2 tacticalDurationRange = new(0.22f, 0.55f);

        [Header("精英闪避")]
        [SerializeField] private float dodgeOpeningProtection = 3f;
        [SerializeField] private Vector2 dodgeCooldownRange = new(6f, 8f);
        [SerializeField, Range(0f, 1f)] private float dodgeTriggerChance = 0.4f;
        [SerializeField] private float dodgeSilenceGuarantee = 10f;
        [SerializeField] private int dodgeMissesBeforeGuarantee = 2;
        [SerializeField] private float dodgeDistance = 2.75f;
        [SerializeField] private float dodgeDuration = 0.3f;
        [SerializeField] private Vector2 dodgeInvulnerableWindow = new(0.08f, 0.23f);
        [SerializeField] private float dodgeRecovery = 0.25f;
        [SerializeField] private float meleeThreatRange = 4.5f;

        [Header("精英词缀")]
        [SerializeField] private EliteAffix affix1;
        [SerializeField] private EliteAffix affix2;

        [Header("掉落")]
        [SerializeField] private SkillData[] possibleSkillDrops;

        private CharacterController _cc;
        private EnemyNavMotor _navMotor;
        private Transform _target;
        private float _attackTimer;
        private float _stunTimer;
        private float _spawnedAt;
        private EnemyAbilityPlanner _abilityPlanner;

        // 受控闪避：只由有效威胁触发，带冷却、概率与保底。
        private bool _isDodging;
        private float _dodgeElapsed;
        private float _dodgeCooldown;
        private float _timeSinceLastDodge;
        private float _dodgeRecoveryTimer;
        private int _eligibleThreatMisses;
        private bool _pendingHitThreat;
        private GameObject _pendingThreatSource;
        private int _recentHitCount;
        private float _recentHitWindow;
        private float _tacticalTimer;
        private float _tacticalDirection;
        private float _tacticalMoveScale;
        private Vector3 _dodgeDirection;
        private bool _hasAttackToken;
        private bool _counterReady;
        private bool _isUsingCoreAbility;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;

        // 攻击预警
        private GameObject _attackWarning;
        private float _attackPrepTimer;
        private bool _isPreparing;
        private float _warningDuration = 0.32f;

        // 血条和名牌
        private EnemyHealthBar _healthBar;
        private GameObject _eliteTag;

        // 词缀效果
        private bool _hasIronwall;
        private bool _hasSplitting;
        private bool _hasLightning;
        private bool _hasVampiric;
        private bool _hasFrost;

        // 闪电链CD
        private float _lightningTimer;
        private const float LIGHTNING_INTERVAL = 3f;

        // 减速效果追踪
        private float _slowTimer;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _navMotor = GetComponent<EnemyNavMotor>();
            if (_navMotor == null)
                _navMotor = gameObject.AddComponent<EnemyNavMotor>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = MaterialHelper.SafeGetColor(_renderers[i].material);
            _spawnedAt = Time.time;
            if (abilityProfile == null)
                abilityProfile = Resources.Load<EnemyAbilityProfile>("EnemyAI/Elite_Default");
            if (abilityProfile != null)
                _abilityPlanner = new EnemyAbilityPlanner(abilityProfile, this);
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.ComboStepChanged>(OnPlayerComboChanged);
            GameEvents.Subscribe<GameEvents.SkillCastStarted>(OnPlayerSkillCastStarted);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.ComboStepChanged>(OnPlayerComboChanged);
            GameEvents.Unsubscribe<GameEvents.SkillCastStarted>(OnPlayerSkillCastStarted);
        }

        private void Start()
        {
            var config = GameConfig.Instance;
            if (config != null)
            {
                attackInterval = config.精英近战攻击间隔;
                maxConcurrentMeleeAttackers = config.同时近战攻击上限;
                _warningDuration = config.精英近战预警时间;
                tacticalRangeMultiplier = config.近战战术距离倍率;
                tacticalPauseChance = config.精英观察停顿概率;
                tacticalDurationRange = config.战术动作持续时间;
                _abilityPlanner?.SetCooldownOverride("melee", attackInterval);
            }

            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);

            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            // 应用词缀效果
            ApplyAffixes();

            // 创建精英标识
            CreateEliteTag();
        }

        private void ApplyAffixes()
        {
            ApplyAffix(affix1);
            ApplyAffix(affix2);
        }

        private void ApplyAffix(EliteAffix affix)
        {
            switch (affix)
            {
                case EliteAffix.Berserk:
                    stats.attackSpeed *= 1.5f;
                    stats.moveSpeed *= 1.3f;
                    attackInterval *= 0.7f;
                    break;
                case EliteAffix.Ironwall:
                    _hasIronwall = true;
                    stats.damageReduction = 0.4f;
                    break;
                case EliteAffix.Splitting:
                    _hasSplitting = true;
                    break;
                case EliteAffix.Lightning:
                    _hasLightning = true;
                    break;
                case EliteAffix.Vampiric:
                    _hasVampiric = true;
                    break;
                case EliteAffix.Frost:
                    _hasFrost = true;
                    break;
            }
        }

        private void CreateEliteTag()
        {
            var canvas = new GameObject("EliteTagCanvas");
            canvas.transform.SetParent(transform);
            canvas.transform.localPosition = new Vector3(0, 3f, 0);
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 0.6f);
            canvas.transform.localScale = Vector3.one * 0.015f;

            var textGo = new GameObject("EliteText");
            textGo.transform.SetParent(canvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = $"⚔ 精英 · {GetAffixName(affix1)} / {GetAffixName(affix2)}";
            text.fontSize = 20;
            if (UGuiKit.CjkFont != null) text.font = UGuiKit.CjkFont;
            text.color = new Color(1f, 0.85f, 0.2f);
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            text.raycastTarget = false;

            _eliteTag = canvas;
        }

        private static string GetAffixName(EliteAffix affix)
        {
            return affix switch
            {
                EliteAffix.Berserk => "狂暴",
                EliteAffix.Ironwall => "铁壁",
                EliteAffix.Splitting => "分裂",
                EliteAffix.Lightning => "雷电",
                EliteAffix.Vampiric => "吸血",
                EliteAffix.Frost => "冰霜",
                _ => "未知"
            };
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            UpdateDodgeTimers();
            if (_isDodging)
            {
                UpdateDodge();
                return;
            }
            if (_isUsingCoreAbility)
                return;

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

            if (_pendingHitThreat)
            {
                _pendingHitThreat = false;
                RegisterDodgeThreat(_pendingThreatSource, false);
                _pendingThreatSource = null;
                if (_isDodging)
                    return;
            }

            // 名牌面向相机
            if (_eliteTag != null && Camera.main != null)
                _eliteTag.transform.rotation = Quaternion.LookRotation(
                    _eliteTag.transform.position - Camera.main.transform.position);

            // 闪电链CD
            if (_hasLightning && _lightningTimer > 0)
                _lightningTimer -= Time.deltaTime;

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            if (_abilityPlanner != null)
            {
                float hpRatio = stats.maxHp > 0f ? stats.currentHp / stats.maxHp : 1f;
                int nearbyAllies = CountNearbyAllies(8f);
                var context = new EnemyAbilityContext(
                    distToTarget,
                    hpRatio,
                    1,
                    nearbyAllies,
                    LevelDesign.LevelAPhaseRuntime.IsNightMapActive);
                if (_abilityPlanner.TryDecide(context))
                    return;
            }

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
                    ReleaseAttackToken();
                }
                return;
            }

            if (distToTarget <= attackRange)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                {
                    if (EnemyCombatCoordinator.TryAcquireAttackToken(
                        gameObject,
                        _target,
                        maxConcurrentMeleeAttackers))
                    {
                        _hasAttackToken = true;
                        StartAttackPrep();
                    }
                    else
                    {
                        StrafeAroundTarget();
                    }
                }
                else
                {
                    StrafeAroundTarget();
                }
            }
            else if (distToTarget <= detectRange)
            {
                if (distToTarget <= attackRange * tacticalRangeMultiplier)
                    StrafeAroundTarget();
                else
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
            _navMotor.MoveTo(targetPos, stats.moveSpeed, attackRange * 0.85f);
        }

        private void StrafeAroundTarget()
        {
            if (_target == null)
                return;

            _tacticalTimer -= Time.deltaTime;
            if (_tacticalTimer <= 0f)
            {
                _tacticalTimer = Random.Range(
                    Mathf.Min(tacticalDurationRange.x, tacticalDurationRange.y),
                    Mathf.Max(tacticalDurationRange.x, tacticalDurationRange.y));
                _tacticalDirection = Random.value < 0.5f ? -1f : 1f;
                _tacticalMoveScale = Random.value < tacticalPauseChance
                    ? 0f
                    : Random.Range(0.4f, 0.7f);
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
                return;

            float distance = toTarget.magnitude;
            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized) * _tacticalDirection;
            float inwardBias = distance > attackRange * 1.15f ? 0.3f : 0f;
            Vector3 direction = (side + toTarget.normalized * inwardBias).normalized;
            _navMotor.MoveTo(
                transform.position + direction * 2f,
                stats.moveSpeed * _tacticalMoveScale,
                0.05f);
        }

        private void UpdateDodgeTimers()
        {
            _timeSinceLastDodge += Time.deltaTime;
            if (_dodgeCooldown > 0f)
                _dodgeCooldown -= Time.deltaTime;
            if (_dodgeRecoveryTimer > 0f)
                _dodgeRecoveryTimer -= Time.deltaTime;
            if (_recentHitWindow > 0f)
            {
                _recentHitWindow -= Time.deltaTime;
                if (_recentHitWindow <= 0f)
                    _recentHitCount = 0;
            }
        }

        private void OnPlayerComboChanged(GameEvents.ComboStepChanged evt)
        {
            if (!evt.IsAttacking || !stats.IsAlive || PlayerController.Instance == null)
                return;
            RegisterDodgeThreat(PlayerController.Instance.gameObject, true);
        }

        private void OnPlayerSkillCastStarted(GameEvents.SkillCastStarted evt)
        {
            if (!stats.IsAlive || PlayerController.Instance == null || evt.Skill == null)
                return;

            float distance = Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
            bool threatensHere = evt.Skill.skillType switch
            {
                SkillType.AreaDamage => distance <= Mathf.Max(meleeThreatRange, evt.Skill.aoeRadius + 1.5f),
                SkillType.Zone => distance <= Mathf.Max(meleeThreatRange, evt.Skill.aoeRadius + 1.5f),
                SkillType.Projectile => distance <= detectRange && IsPlayerFacingThisElite(),
                SkillType.Dash => distance <= meleeThreatRange + 1.5f,
                _ => false
            };
            if (threatensHere)
            {
                bool requireFrontal = evt.Skill.skillType == SkillType.Projectile
                                      || evt.Skill.skillType == SkillType.Dash;
                RegisterDodgeThreat(PlayerController.Instance.gameObject, requireFrontal);
            }
        }

        private bool IsPlayerFacingThisElite()
        {
            Transform player = PlayerController.Instance != null
                ? PlayerController.Instance.transform
                : null;
            if (player == null)
                return false;

            Vector3 toElite = transform.position - player.position;
            toElite.y = 0f;
            return toElite.sqrMagnitude > 0.01f
                   && Vector3.Dot(player.forward, toElite.normalized) >= 0.25f;
        }

        private void RegisterDodgeThreat(GameObject source, bool requireFrontalThreat)
        {
            if (!CanStartDodge() || source == null)
                return;

            float distance = Vector3.Distance(transform.position, source.transform.position);
            if (distance > meleeThreatRange && requireFrontalThreat)
                return;
            if (requireFrontalThreat && !IsPlayerFacingThisElite())
                return;

            bool guaranteed = _eligibleThreatMisses >= dodgeMissesBeforeGuarantee
                              || _timeSinceLastDodge >= dodgeSilenceGuarantee;
            if (!guaranteed && Random.value > dodgeTriggerChance)
            {
                _eligibleThreatMisses++;
                return;
            }

            if (TryResolveSafeDodgeDirection(source.transform.position, out Vector3 direction))
                StartDodge(direction);
        }

        private bool CanStartDodge()
        {
            return stats.IsAlive
                   && Time.time - _spawnedAt >= dodgeOpeningProtection
                   && _dodgeCooldown <= 0f
                   && _dodgeRecoveryTimer <= 0f
                   && _stunTimer <= 0f
                   && !_isPreparing
                   && !_isDodging;
        }

        private bool TryResolveSafeDodgeDirection(Vector3 threatPosition, out Vector3 direction)
        {
            Vector3 away = transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = -transform.forward;
            away.Normalize();

            Vector3 side = Vector3.Cross(Vector3.up, away);
            if ((GetInstanceID() & 1) != 0)
                side = -side;

            Vector3[] candidates =
            {
                side,
                -side,
                (away + side * 0.35f).normalized,
                (away - side * 0.35f).normalized,
                away
            };

            foreach (Vector3 candidate in candidates)
            {
                if (IsDodgePathSafe(candidate))
                {
                    direction = candidate;
                    return true;
                }
            }

            direction = Vector3.zero;
            return false;
        }

        private bool IsDodgePathSafe(Vector3 direction)
        {
            CharacterController controller = _cc != null ? _cc : GetComponent<CharacterController>();
            if (controller == null)
                return false;

            float radius = Mathf.Max(0.15f, controller.radius * 0.85f);
            float halfHeight = Mathf.Max(radius, controller.height * 0.5f - radius);
            Vector3 center = transform.position + controller.center;
            Vector3 bottom = center + Vector3.down * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;
            RaycastHit[] hits = Physics.CapsuleCastAll(
                bottom,
                top,
                radius,
                direction,
                dodgeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.transform.IsChildOf(transform))
                    continue;
                return false;
            }

            Vector3 destination = transform.position + direction * dodgeDistance;
            return Physics.Raycast(
                destination + Vector3.up * 1.5f,
                Vector3.down,
                out _,
                3.5f,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        private void StartDodge(Vector3 direction)
        {
            ReleaseAttackToken();
            _isPreparing = false;
            DestroyAttackWarning();
            _isDodging = true;
            _dodgeElapsed = 0f;
            _dodgeDirection = direction;
            _eligibleThreatMisses = 0;
            _timeSinceLastDodge = 0f;
            _dodgeCooldown = Random.Range(
                Mathf.Min(dodgeCooldownRange.x, dodgeCooldownRange.y),
                Mathf.Max(dodgeCooldownRange.x, dodgeCooldownRange.y));
            SetAllRenderersColor(new Color(0.35f, 0.85f, 1f, 0.75f));
        }

        private void UpdateDodge()
        {
            if (_cc == null)
                _cc = GetComponent<CharacterController>();
            if (_cc == null)
            {
                _isDodging = false;
                return;
            }

            _dodgeElapsed += Time.deltaTime;
            float speed = dodgeDistance / Mathf.Max(0.05f, dodgeDuration);
            Vector3 velocity = _dodgeDirection * speed;
            velocity.y = -9.8f;
            _cc.Move(velocity * Time.deltaTime);

            if (_dodgeElapsed < dodgeDuration)
                return;

            _isDodging = false;
            _dodgeRecoveryTimer = dodgeRecovery;
            _counterReady = true;
            _navMotor.ResyncAfterForcedMove();
            RestoreColors();
        }

        private bool IsInDodgeInvulnerability()
        {
            return _isDodging
                   && _dodgeElapsed >= dodgeInvulnerableWindow.x
                   && _dodgeElapsed <= dodgeInvulnerableWindow.y;
        }

        private void ReleaseAttackToken()
        {
            if (!_hasAttackToken)
                return;
            EnemyCombatCoordinator.ReleaseAttackToken(gameObject, _target);
            _hasAttackToken = false;
        }

        private int CountNearbyAllies(float radius)
        {
            int count = 0;
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);
            var roots = new System.Collections.Generic.HashSet<Transform>();
            foreach (Collider hit in hits)
            {
                Transform root = hit.transform.root;
                if (root == transform.root || !roots.Add(root))
                    continue;
                if (root.GetComponent<EnemyBase>() != null
                    || root.GetComponent<EnemyElite>() != null
                    || root.GetComponent<EnemyBoss>() != null)
                    count++;
            }
            return count;
        }

        bool IEnemyAbilityExecutor.IsAbilityLocked =>
            _isPreparing
            || _isDodging
            || _isUsingCoreAbility
            || _stunTimer > 0f
            || _dodgeRecoveryTimer > 0f;

        bool IEnemyAbilityExecutor.TryExecuteAbility(EnemyAbilityRule rule)
        {
            switch (rule.Action)
            {
                case EnemyAbilityAction.Melee:
                    if (_target == null
                        || !EnemyCombatCoordinator.TryAcquireAttackToken(
                            gameObject,
                            _target,
                            maxConcurrentMeleeAttackers))
                        return false;
                    _hasAttackToken = true;
                    StartAttackPrep();
                    return true;
                case EnemyAbilityAction.Dodge:
                    if (_target == null
                        || !TryResolveSafeDodgeDirection(_target.position, out Vector3 direction))
                        return false;
                    StartDodge(direction);
                    return true;
                case EnemyAbilityAction.Custom:
                    if (rule.CustomActionKey != "elite_counter_lunge"
                        || !_counterReady
                        || _target == null)
                        return false;
                    StartCoroutine(CounterLunge());
                    return true;
                default:
                    return false;
            }
        }

        private System.Collections.IEnumerator CounterLunge()
        {
            _counterReady = false;
            _isUsingCoreAbility = true;
            ReleaseAttackToken();
            Vector3 direction = _target.position - transform.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;

            GameObject warning = CreateCounterWarning(direction);
            SetAllRenderersColor(new Color(0.3f, 0.9f, 1f));
            yield return new WaitForSeconds(0.55f);

            bool hit = false;
            float timer = 0.24f;
            while (timer > 0f && stats.IsAlive)
            {
                timer -= Time.deltaTime;
                Vector3 velocity = direction * stats.moveSpeed * 3.2f;
                velocity.y = -9.8f;
                _cc.Move(velocity * Time.deltaTime);

                if (!hit && _target != null
                    && Vector3.Distance(transform.position, _target.position) <= attackRange * 1.35f)
                {
                    IDamageable damageable = _target.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float defense = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (damage, _) = stats.CalcSkillDamage(defense, 1.35f);
                        damageable.OnDamage(damage, transform.position, gameObject);
                        hit = true;
                    }
                }
                yield return null;
            }

            if (warning != null)
                Destroy(warning);
            _navMotor.ResyncAfterForcedMove();
            RestoreColors();
            _dodgeRecoveryTimer = Mathf.Max(_dodgeRecoveryTimer, 0.35f);
            _isUsingCoreAbility = false;
        }

        private GameObject CreateCounterWarning(Vector3 direction)
        {
            var warning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            warning.name = "[Warning] EliteCounterLunge";
            warning.transform.position = transform.position + direction * 2.5f + Vector3.up * 0.06f;
            warning.transform.rotation = Quaternion.LookRotation(direction);
            warning.transform.localScale = new Vector3(0.8f, 0.08f, 5f);
            Collider collider = warning.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = warning.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(MaterialHelper.GetLitShader());
                material.color = new Color(0.2f, 0.85f, 1f, 0.35f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.1f, 0.7f, 1f) * 2f);
                renderer.material = material;
            }
            return warning;
        }

        private void StartAttackPrep()
        {
            _isPreparing = true;
            _attackPrepTimer = _warningDuration;
            CreateAttackWarning();
            SetAllRenderersColor(new Color(1f, 0.85f, 0.2f)); // 金色蓄力
        }

        private void Attack()
        {
            if (_target == null) return;
            RestoreColors();

            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > attackRange * 1.5f) return;

            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float targetDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                var (damage, _) = stats.CalcMeleeDamage(targetDef);
                damageable.OnDamage(damage, transform.position, gameObject);

                if (_hasVampiric)
                {
                    float heal = damage * 0.1f;
                    stats.Heal(heal);
                    if (_healthBar != null)
                        _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);
                }

                // 冰霜词缀：减速玩家
                if (_hasFrost && PlayerController.Instance != null)
                {
                    var playerStats = PlayerController.Instance.Stats;
                    if (_slowTimer <= 0)
                    {
                        playerStats.moveSpeed *= 0.7f;
                        _slowTimer = 2f;
                        StartCoroutine(FrostSlowCoroutine(playerStats));
                    }
                }
            }

            // 雷电词缀：攻击时释放闪电链
            if (_hasLightning && _lightningTimer <= 0)
            {
                CastLightningChain();
                _lightningTimer = LIGHTNING_INTERVAL;
            }
        }

        private System.Collections.IEnumerator FrostSlowCoroutine(CombatStats playerStats)
        {
            yield return new WaitForSeconds(2f);
            playerStats.moveSpeed /= 0.7f;
            _slowTimer = 0;
        }

        private void CastLightningChain()
        {
            // 闪电链：对玩家位置周围造成范围伤害
            Vector3 targetPos = _target.position;
            float radius = 2.5f;
            float damage = stats.CalcSkillDamage(0f, 0.6f).damage;

            // 视觉效果：闪电球
            var lightning = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightning.name = "[VFX] Lightning";
            lightning.transform.position = targetPos + Vector3.up * 1f;
            lightning.transform.localScale = Vector3.one * radius;
            var col = lightning.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = lightning.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.6f, 1f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 3f);
                rend.material = mat;
            }
            Destroy(lightning, 0.4f);

            // 画闪电线
            Debug.DrawLine(transform.position + Vector3.up, targetPos + Vector3.up,
                new Color(0.3f, 0.6f, 1f), 0.3f);

            // 范围伤害
            var hits = Physics.OverlapSphere(targetPos, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var dmg = hit.GetComponent<IDamageable>();
                    if (dmg != null)
                        dmg.OnDamage(damage, targetPos, gameObject);
                }
            }
        }

        // ========== 预警 ==========

        private void CreateAttackWarning()
        {
            DestroyAttackWarning();
            _attackWarning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _attackWarning.name = "[Warning] EliteZone";
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;
            _attackWarning.transform.localScale = new Vector3(attackRange * 2.5f, 0.05f, attackRange * 2.5f);

            var wCol = _attackWarning.GetComponent<Collider>();
            if (wCol != null) Destroy(wCol);

            var wRend = _attackWarning.GetComponent<Renderer>();
            if (wRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.85f, 0.2f, 0.2f);
                wRend.material = mat;
            }
        }

        private void UpdateAttackWarning()
        {
            if (_attackWarning == null) return;
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;
            float progress = 1f - (_attackPrepTimer / _warningDuration);
            var wRend = _attackWarning.GetComponent<Renderer>();
            if (wRend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.45f, progress);
                wRend.material.color = new Color(1f, 0.85f, 0.2f, alpha);
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

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;
            if (IsInDodgeInvulnerability())
            {
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 1.5f,
                    Damage = 0f,
                    IsCrit = false,
                    IsPlayerDamage = false,
                    SpecialTag = "闪避"
                });
                return;
            }

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

            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            // 铁壁词缀：硬直更短，击退更小
            float stunTime = _hasIronwall ? 0.15f : 0.25f;
            _stunTimer = stunTime;
            _isPreparing = false;
            DestroyAttackWarning();
            ReleaseAttackToken();

            if (_recentHitWindow <= 0f)
                _recentHitCount = 0;
            _recentHitCount++;
            _recentHitWindow = 1f;
            if (_recentHitCount >= 2)
            {
                _pendingHitThreat = true;
                _pendingThreatSource = attacker;
                _recentHitCount = 0;
            }

            if (attacker != null)
            {
                float knockbackForce = _hasIronwall ? 0.15f : 0.4f;
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * knockbackForce;
                knockback.y = 0;
                _cc.Move(knockback);
                _navMotor.ResyncAfterForcedMove();
            }

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerHeavy();

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";
            DestroyAttackWarning();

            TryDropSkill();

            // 搜打撤：精英怪 40% 概率额外掉一件【洞府素材】（独立 roll）
            float eliteCaveChance = 0.4f;
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f)), eliteCaveChance);

            // 分裂词缀：死亡时分裂为2个小怪
            if (_hasSplitting)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
                    var minion = EnemyBase.Spawn(transform.position + offset, 0.4f, 0.4f);
                    if (minion != null && possibleSkillDrops != null)
                        minion.SetSkillDrops(possibleSkillDrops);
                }
                Debug.Log("<color=yellow>⚔ 精英怪分裂！</color>");
            }

            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            // 击杀回复（模块系统处理）

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerKill();

            Debug.Log("<color=yellow>⚔ 精英怪被击败！</color>");
            StartCoroutine(DeathAnimation());
        }

        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;
            var skillConfig = GameConfig.Instance;
            bool forceDropSkill = skillConfig != null && skillConfig.debugMaxSkillDropRate;
            if (!forceDropSkill && Random.value > 0.25f) return;
            var skill = SkillPickup.PickValid(possibleSkillDrops);
            if (skill != null)
                SkillPickup.Spawn(skill, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            enabled = false;
            _cc.enabled = false;
            float timer = 0.5f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.5f;
                transform.localScale = startScale * t;
                transform.Rotate(Vector3.up * 180f * Time.deltaTime);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>工厂方法：生成精英怪</summary>
        public static EnemyElite Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            SkillData[] skillDrops = null)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.GetElitePrefab() : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Elite");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            // 精英怪体型更大
            go.transform.localScale = (prefab != null ? Vector3.one : Vector3.one) * 1.4f;

            // 如果是回退胶囊体，设置金色
            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1f, 0.85f, 0.2f);
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.1f) * 0.5f);
                }
            }

            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.6f;
            cc.height = 2.2f;
            cc.center = new Vector3(0, 1.1f, 0);

            var elite = go.AddComponent<EnemyElite>();

            // 随机选择2个不同的词缀
            var allAffixes = (EliteAffix[])System.Enum.GetValues(typeof(EliteAffix));
            elite.affix1 = allAffixes[Random.Range(0, allAffixes.Length)];
            do
            {
                elite.affix2 = allAffixes[Random.Range(0, allAffixes.Length)];
            } while (elite.affix2 == elite.affix1);

            // 精英怪属性：使用GameConfig的精英倍率
            var config = GameConfig.Instance;
            float eliteHpMul = config != null ? config.精英怪血量倍率 : 3f;
            float eliteDmgMul = config != null ? config.精英怪伤害倍率 : 1.5f;

            if (config != null)
            {
                elite.stats.maxHp = config.敌人基础血量 * eliteHpMul * hpMultiplier;
                elite.stats.attackDamage = config.敌人基础攻击力 * eliteDmgMul * dmgMultiplier;
                elite.stats.defense = config.敌人基础防御力 * 2f;
            }
            else
            {
                elite.stats.maxHp = 90f * hpMultiplier;
                elite.stats.attackDamage = 12f * dmgMultiplier;
                elite.stats.defense = 6f;
            }
            elite.stats.currentHp = elite.stats.maxHp;
            if (skillDrops != null) elite.possibleSkillDrops = skillDrops;

            Debug.Log($"<color=yellow>⚔ 精英怪出现！词缀：{GetAffixName(elite.affix1)} + {GetAffixName(elite.affix2)}</color>");
            return elite;
        }

        private void OnDestroy()
        {
            ReleaseAttackToken();
            EnemyCombatCoordinator.Unregister(gameObject);
            DestroyAttackWarning();
        }

        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) MaterialHelper.SafeSetColor(r.material, color);
        }

        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && i < _originalColors.Length)
                    MaterialHelper.SafeSetColor(_renderers[i].material, _originalColors[i]);
        }
    }
}
