using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// Boss 敌人 —— 多阶段行为模式
    /// 阶段1（>50%血）：近战连击 + 冲锋
    /// 阶段2（≤50%血）：增加AOE攻击 + 速度提升 + 召唤小怪
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBoss : MonoBehaviour, IDamageable, IEnemyAbilityExecutor
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 300f,
            currentHp = 300f,
            attackDamage = 20f,
            moveSpeed = 3.5f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 20f;
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeInterval = 1.2f;
        [SerializeField] private float chargeInterval = 6f;
        [SerializeField] private float aoeInterval = 5f;
        [SerializeField] private float aoeRadius = 3.5f;
        [SerializeField] private float aoeWarningTime = 1.2f;
        [SerializeField] private float chargeSpeed = 15f;
        [SerializeField] private float chargeDuration = 0.6f;
        [SerializeField] private float chargePrepTime = 0.8f;
        [SerializeField] private float tacticalRangeMultiplier = 2.2f;
        [SerializeField, Range(0f, 1f)] private float tacticalPauseChance = 0.08f;
        [SerializeField] private Vector2 tacticalDurationRange = new(0.25f, 0.6f);
        [Tooltip("可选。配置后优先按通用条件选技能，未命中时仍走现有保底逻辑。")]
        [SerializeField] private EnemyAbilityProfile abilityProfile;


        private CharacterController _cc;
        private EnemyNavMotor _navMotor;
        private Transform _target;

        // 计时器
        private float _meleeTimer;
        private float _chargeTimer;
        private float _aoeTimer;
        private float _stunTimer;

        // 状态
        private enum BossPhase { Phase1, Phase2 }
        private enum BossAction { Idle, Tracking, MeleeAttack, ChargePrepare, Charging, AOECast, Stunned, LeapPrepare, Leaping, ShockwaveCast, FinalSkill }
        private BossPhase _phase = BossPhase.Phase1;
        private BossAction _action = BossAction.Idle;
        private float _actionTimer;
        private Vector3 _chargeDirection;
        private Vector3 _aoeTargetPos;
        private float _tacticalTimer;
        private float _tacticalDirection;
        private float _tacticalMoveScale;

        // 跳跃攻击
        private Vector3 _leapTargetPos;
        private float _leapTimer;
        private const float LEAP_PREP_TIME = 0.8f;
        private const float LEAP_DURATION = 0.4f;
        private float _leapInterval = 8f;
        private float _leapCooldown;

        // 震荡波
        private float _shockwaveInterval = 7f;
        private float _shockwaveCooldown;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;
        private bool _phase2Triggered;
        private bool _nightSummon70Triggered;
        private bool _nightSummon35Triggered;
        private bool _isNightBoss;
        private string _displayName = "守关妖兽";
        private System.Action<GameObject> _summonRegistrar;
        private Transform _summonRoomRoot;
        private EnemyAbilityPlanner _abilityPlanner;

        // V0.2.4：配表驱动的 P2 形态（BossPhaseSelector 选出的次优先形态）
        private LevelDesign.BossPhaseRow _pendingPhase2Row;

        // 预警
        private GameObject _warningIndicator;
        private EnemyHealthBar _healthBar;

        // Boss名字标签
        private GameObject _nameTag;
        private TMPro.TextMeshProUGUI _nameText;

        public CombatStats Stats => stats;

        /// <summary>当前存活的境界 Boss 数量（心魔值乱入据此判断"是否正在打 Boss"）。</summary>
        public static int AliveCount { get; private set; }
        private bool _deadCounted;

        private void Awake()
        {
            AliveCount++;
            _cc = GetComponent<CharacterController>();
            _navMotor = GetComponent<EnemyNavMotor>();
            if (_navMotor == null)
                _navMotor = gameObject.AddComponent<EnemyNavMotor>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = MaterialHelper.SafeGetColor(_renderers[i].material);
            if (abilityProfile == null)
                abilityProfile = Resources.Load<EnemyAbilityProfile>("EnemyAI/Boss_Default");
            if (abilityProfile != null)
                _abilityPlanner = new EnemyAbilityPlanner(abilityProfile, this);
        }

        private void DecrementAlive()
        {
            if (_deadCounted) return;
            _deadCounted = true;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        private void Start()
        {
            var config = GameConfig.Instance;
            if (config != null)
            {
                meleeInterval = config.Boss近战攻击间隔;
                tacticalRangeMultiplier = config.近战战术距离倍率;
                tacticalPauseChance = config.Boss观察停顿概率;
                tacticalDurationRange = config.战术动作持续时间;
                _abilityPlanner?.SetCooldownOverride("melee", meleeInterval);
            }

            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            _chargeTimer = chargeInterval;
            _aoeTimer = aoeInterval;

            // 创建Boss名字标签
            CreateNameTag();
        }

        private void CreateNameTag()
        {
            // 使用世界空间Canvas显示Boss名字
            var canvas = new GameObject("BossNameCanvas");
            canvas.transform.SetParent(transform);
            canvas.transform.localPosition = new Vector3(0, 3.5f, 0);
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0.5f);
            canvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("BossName");
            textGo.transform.SetParent(canvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _nameText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            _nameText.text = $"★ {_displayName} ★";
            _nameText.fontSize = 24;
            if (UGuiKit.CjkFont != null) _nameText.font = UGuiKit.CjkFont;
            _nameText.color = new Color(1f, 0.4f, 0.1f);
            _nameText.alignment = TMPro.TextAlignmentOptions.Center;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TMPro.TextOverflowModes.Overflow;
            _nameText.raycastTarget = false;

            _nameTag = canvas;
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            // 受击闪烁
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                    RestoreColors();
            }

            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            // 名字标签面向相机
            if (_nameTag != null && Camera.main != null)
                _nameTag.transform.rotation = Quaternion.LookRotation(
                    _nameTag.transform.position - Camera.main.transform.position);

            // 阶段检查
            CheckPhaseTransition();

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            switch (_action)
            {
                case BossAction.Idle:
                case BossAction.Tracking:
                    UpdateTracking(distToTarget);
                    break;
                case BossAction.ChargePrepare:
                    UpdateChargePrepare();
                    break;
                case BossAction.Charging:
                    UpdateCharging(distToTarget);
                    break;
                case BossAction.AOECast:
                    UpdateAOECast();
                    break;
                case BossAction.LeapPrepare:
                    UpdateLeapPrepare();
                    break;
                case BossAction.Leaping:
                    UpdateLeaping();
                    break;
                case BossAction.ShockwaveCast:
                    UpdateShockwave();
                    break;
                case BossAction.FinalSkill:
                    break;
                case BossAction.Stunned:
                    _actionTimer -= Time.deltaTime;
                    if (_actionTimer <= 0)
                        _action = BossAction.Tracking;
                    break;
            }

            // 朝向
            if (_action != BossAction.Charging)
            {
                Vector3 lookDir = _target.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        private void CheckPhaseTransition()
        {
            if (_isNightBoss)
                CheckNightSummonThresholds();

            if (!_phase2Triggered && stats.currentHp <= stats.maxHp * 0.5f)
            {
                _phase2Triggered = true;
                _phase = BossPhase.Phase2;

                // V0.2.4：如果有配表 P2 形态，应用其数值修正
                if (_pendingPhase2Row != null)
                {
                    float hp = stats.maxHp;
                    float atk = stats.attackDamage;
                    float spd = stats.moveSpeed;
                    LevelDesign.BossPhaseSelector.ApplyStatModifier(_pendingPhase2Row, ref hp, ref atk, ref spd);
                    stats.attackDamage = atk;
                    stats.moveSpeed = spd;
                    Debug.Log($"<color=red>★ Boss P2 形态：{_pendingPhase2Row.PhaseName} | ATK→{atk:F1} SPD→{spd:F2} ★</color>");
                }
                else
                {
                    stats.moveSpeed *= 1.3f;
                }

                for (int i = 0; i < _originalColors.Length; i++)
                    _originalColors[i] = new Color(1f, 0.3f, 0.1f);

                Debug.Log("<color=red>★ Boss 进入狂暴阶段！★</color>");
                if (!_isNightBoss)
                    SpawnMinions(2);
            }
        }

        private void SpawnMinions(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                Vector3 pos = ResolveSummonPosition(transform.position + offset);
                RegisterSummon(EnemyBase.Spawn(pos, 0.5f, 0.5f).gameObject);
            }
        }

        public void ConfigureSummons(System.Action<GameObject> registrar, Transform roomRoot)
        {
            _summonRegistrar = registrar;
            _summonRoomRoot = roomRoot;
        }

        private void CheckNightSummonThresholds()
        {
            float hpRatio = stats.maxHp > 0f ? stats.currentHp / stats.maxHp : 1f;
            if (!_nightSummon70Triggered && hpRatio <= 0.70f)
            {
                _nightSummon70Triggered = true;
                SpawnNightGuardWave();
            }
            if (!_nightSummon35Triggered && hpRatio <= 0.35f)
            {
                _nightSummon35Triggered = true;
                SpawnNightGuardWave();
            }
        }

        private void SpawnNightGuardWave()
        {
            var flags = LevelDesign.BossFlagSet.Instance;
            if (flags.Evaluate("summon_array_destroyed=1"))
            {
                Debug.Log("[永夜首领] 禁卫召集阵已摧毁，本次召唤失败。");
                return;
            }

            if (flags.Evaluate("summon_array_outer_broken=1"))
            {
                Vector3 pos = ResolveSummonPosition(transform.position + transform.right * 3f);
                RegisterSummon(EnemyElite.Spawn(pos, 0.65f, 0.65f).gameObject);
                Debug.Log("[永夜首领] 召集阵外环损坏，仅投射 1 名禁卫队长。");
                return;
            }

            Vector3 left = ResolveSummonPosition(transform.position - transform.right * 3f);
            Vector3 right = ResolveSummonPosition(transform.position + transform.right * 3f);
            Vector3 back = ResolveSummonPosition(transform.position - transform.forward * 3f);
            RegisterSummon(EnemyBase.Spawn(left, 0.5f, 0.5f).gameObject);
            RegisterSummon(EnemyBase.Spawn(right, 0.5f, 0.5f).gameObject);
            RegisterSummon(EnemyRanged.Spawn(back, 0.5f, 0.5f).gameObject);
            Debug.Log("[永夜首领] 禁卫召集阵完整，投射 2 名近战禁卫与 1 名远程禁卫。");
        }

        private Vector3 ResolveSummonPosition(Vector3 candidate)
        {
            if (_summonRoomRoot != null
                && DungeonSpawnSafety.TryFindGroundedPoint(
                    _summonRoomRoot,
                    candidate,
                    0.45f,
                    1.8f,
                    0.1f,
                    out Vector3 grounded))
                return grounded;
            return candidate;
        }

        private void RegisterSummon(GameObject summon)
        {
            if (summon != null)
                _summonRegistrar?.Invoke(summon);
        }

        private void UpdateTracking(float distToTarget)
        {
            // 更新计时器
            _chargeTimer -= Time.deltaTime;
            _leapCooldown -= Time.deltaTime;
            _shockwaveCooldown -= Time.deltaTime;
            if (_phase == BossPhase.Phase2)
                _aoeTimer -= Time.deltaTime;

            if (_abilityPlanner != null)
            {
                float hpRatio = stats.maxHp > 0f ? stats.currentHp / stats.maxHp : 1f;
                var context = new EnemyAbilityContext(
                    distToTarget,
                    hpRatio,
                    _phase == BossPhase.Phase1 ? 1 : 2,
                    CountNearbyAllies(10f),
                    _isNightBoss);
                if (_abilityPlanner.TryDecide(context))
                    return;

                if (distToTarget > meleeRange && distToTarget <= detectRange)
                    UpdateTacticalMovement(distToTarget);
                return;
            }

            // 优先级：跳跃 > 冲锋 > 震荡波 > AOE > 近战

            // 跳跃攻击（距离较远时跳向玩家）
            if (_leapCooldown <= 0 && distToTarget > meleeRange * 2f && distToTarget <= detectRange)
            {
                StartLeapPrepare();
                return;
            }

            // 冲锋
            if (_chargeTimer <= 0 && distToTarget > meleeRange && distToTarget <= detectRange)
            {
                StartChargePrepare();
                return;
            }

            // 震荡波（近距离时释放）
            if (_shockwaveCooldown <= 0 && distToTarget <= meleeRange * 2f)
            {
                StartShockwave();
                return;
            }

            // AOE（仅第二阶段）
            bool crownLightDisabled = !_isNightBoss
                                      && LevelDesign.BossFlagSet.Instance.Evaluate(
                                          "crown_light_disabled=1");
            if (_phase == BossPhase.Phase2
                && _aoeTimer <= 0
                && !crownLightDisabled)
            {
                StartAOECast();
                return;
            }

            if (distToTarget <= meleeRange)
            {
                _meleeTimer -= Time.deltaTime;
                if (_meleeTimer <= 0)
                {
                    MeleeAttack();
                    _meleeTimer = meleeInterval;
                }
            }
            else if (distToTarget <= detectRange)
            {
                UpdateTacticalMovement(distToTarget);
            }
        }

        private void MeleeAttack()
        {
            if (_target == null) return;
            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                var (damage, _) = stats.CalcMeleeDamage(tDef);
                damage *= (_phase == BossPhase.Phase2 ? 1.3f : 1f);
                damageable.OnDamage(damage, transform.position, gameObject);
            }
        }

        // ========== 冲锋 ==========

        private void StartChargePrepare()
        {
            _action = BossAction.ChargePrepare;
            _actionTimer = chargePrepTime;
            _chargeDirection = (_target.position - transform.position).normalized;
            _chargeDirection.y = 0;

            CreateWarningIndicator(true);
            SetAllRenderersColor(new Color(1f, 0.6f, 0.1f));
        }

        private void UpdateChargePrepare()
        {
            _actionTimer -= Time.deltaTime;
            _chargeDirection = (_target.position - transform.position).normalized;
            _chargeDirection.y = 0;
            UpdateChargeWarning();

            float shake = Mathf.Sin(Time.time * 25f) * 0.06f;
            transform.position += new Vector3(shake, 0, shake);

            if (_actionTimer <= 0)
            {
                _action = BossAction.Charging;
                _actionTimer = chargeDuration;
                DestroyWarningIndicator();
                SetAllRenderersColor(new Color(1f, 0.2f, 0.1f));
            }
        }

        private void UpdateCharging(float distToTarget)
        {
            _actionTimer -= Time.deltaTime;
            Vector3 vel = _chargeDirection * chargeSpeed;
            vel.y = -9.8f;
            _cc.Move(vel * Time.deltaTime);

            if (distToTarget < meleeRange + 0.5f)
            {
                var damageable = _target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                    var (d, _) = stats.CalcMeleeDamage(tDef);
                    damageable.OnDamage(d * 1.5f, transform.position, gameObject);
                }
                EndAction(1.0f);
            }

            if (_actionTimer <= 0)
                EndAction(0.8f);
        }

        // ========== AOE ==========

        private void StartAOECast()
        {
            _action = BossAction.AOECast;
            _actionTimer = aoeWarningTime;
            bool crownLightMisaligned = !_isNightBoss
                                        && LevelDesign.BossFlagSet.Instance.Evaluate(
                                            "crown_light_misaligned=1");
            _aoeTargetPos = crownLightMisaligned
                ? transform.position
                : _target.position;
            CreateWarningIndicator(false);

            SetAllRenderersColor(new Color(0.6f, 0.1f, 0.8f));
        }

        private void UpdateAOECast()
        {
            _actionTimer -= Time.deltaTime;
            UpdateAOEWarning();

            if (_actionTimer <= 0)
            {
                ExecuteAOE();
                DestroyWarningIndicator();
                RestoreColors();
                _aoeTimer = aoeInterval;
                _action = BossAction.Tracking;
            }
        }

        private void ExecuteAOE()
        {
            // 爆炸视觉
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "[VFX] BossAOE";
            explosion.transform.position = _aoeTargetPos + Vector3.up * 0.5f;
            explosion.transform.localScale = Vector3.one * aoeRadius * 2f;
            var col = explosion.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = explosion.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.8f, 0.2f, 0.8f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 0.8f) * 3f);
                rend.material = mat;
            }
            Destroy(explosion, 0.5f);

            var hits = Physics.OverlapSphere(_aoeTargetPos, aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (aoeDmg, _) = stats.CalcSkillDamage(tDef, 1.2f);
                        damageable.OnDamage(aoeDmg, _aoeTargetPos, gameObject);
                    }
                }
            }
        }

        // ========== 跳跃攻击 ==========

        private void StartLeapPrepare()
        {
            _action = BossAction.LeapPrepare;
            _actionTimer = LEAP_PREP_TIME;
            _leapTargetPos = _target.position;

            // 蓄力跳跃：身体上下抖动
            SetAllRenderersColor(new Color(1f, 0.4f, 0.1f));

            // 创建落点预警圈
            CreateWarningIndicator(false);
            if (_warningIndicator != null)
            {
                _warningIndicator.transform.position = _leapTargetPos + Vector3.up * 0.05f;
                float scale = aoeRadius * 2.5f;
                _warningIndicator.transform.localScale = new Vector3(scale, 0.05f, scale);
            }
        }

        private void UpdateLeapPrepare()
        {
            _actionTimer -= Time.deltaTime;
            // 持续追踪目标位置
            _leapTargetPos = _target.position;

            // 更新预警圈位置
            if (_warningIndicator != null)
            {
                _warningIndicator.transform.position = _leapTargetPos + Vector3.up * 0.05f;
                float progress = 1f - (_actionTimer / LEAP_PREP_TIME);
                var rend = _warningIndicator.GetComponent<Renderer>();
                if (rend != null)
                {
                    float alpha = Mathf.Lerp(0.15f, 0.5f, progress);
                    rend.material.color = new Color(1f, 0.3f, 0.1f, alpha);
                }
            }

            // 蓄力抖动
            float shake = Mathf.Sin(Time.time * 30f) * 0.08f;
            transform.position += new Vector3(0, shake, 0);

            if (_actionTimer <= 0)
            {
                _action = BossAction.Leaping;
                _actionTimer = LEAP_DURATION;
                DestroyWarningIndicator();
                SetAllRenderersColor(new Color(1f, 0.2f, 0.1f));
            }
        }

        private void UpdateLeaping()
        {
            _actionTimer -= Time.deltaTime;
            float progress = 1f - (_actionTimer / LEAP_DURATION);

            // 抛物线跳跃
            Vector3 startPos = transform.position;
            Vector3 targetPos = _leapTargetPos;
            float height = 6f;
            float y = Mathf.Sin(progress * Mathf.PI) * height;

            Vector3 flatDir = (targetPos - startPos);
            flatDir.y = 0;
            Vector3 moveVel = flatDir.normalized * (flatDir.magnitude / LEAP_DURATION);
            moveVel.y = y > 0 ? y * 2f : -9.8f;
            _cc.Move(moveVel * Time.deltaTime);

            if (_actionTimer <= 0)
            {
                // 落地冲击
                ExecuteLeapImpact();
                _leapCooldown = _leapInterval;
                EndAction(0.6f);
            }
        }

        private void ExecuteLeapImpact()
        {
            float impactRadius = aoeRadius * 1.5f;

            // 冲击波视觉
            var impact = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            impact.name = "[VFX] LeapImpact";
            impact.transform.position = transform.position + Vector3.up * 0.1f;
            impact.transform.localScale = new Vector3(impactRadius * 2f, 0.1f, impactRadius * 2f);
            var col = impact.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = impact.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.4f, 0.1f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.1f) * 2f);
                rend.material = mat;
            }
            Destroy(impact, 0.5f);

            // 范围伤害
            var hits = Physics.OverlapSphere(transform.position, impactRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (leapDmg, _) = stats.CalcSkillDamage(tDef, 2f);
                        damageable.OnDamage(leapDmg, transform.position, gameObject);
                    }
                }
            }

            // 顿帧
            if (HitStop.Instance != null)
                HitStop.Instance.TriggerHeavy();

            Debug.Log("<color=red>★ Boss 跳跃冲击！★</color>");
        }

        // ========== 震荡波 ==========

        private void StartShockwave()
        {
            _action = BossAction.ShockwaveCast;
            _actionTimer = 0.6f; // 蓄力时间
            SetAllRenderersColor(new Color(0.2f, 0.8f, 1f)); // 蓝色蓄力
        }

        private void UpdateShockwave()
        {
            _actionTimer -= Time.deltaTime;

            // 蓄力抖动
            float shake = Mathf.Sin(Time.time * 40f) * 0.04f;
            transform.position += new Vector3(shake, 0, shake);

            if (_actionTimer <= 0)
            {
                ExecuteShockwave();
                _shockwaveCooldown = _shockwaveInterval;
                RestoreColors();
                _action = BossAction.Tracking;
            }
        }

        private void ExecuteShockwave()
        {
            float radius = 5f;

            StartCoroutine(ShockwaveVisual(radius));

            var hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (swDmg, _) = stats.CalcSkillDamage(tDef, 0.8f);
                        damageable.OnDamage(swDmg, transform.position, gameObject);
                    }

                    // 击退玩家
                    var playerCC = hit.GetComponent<CharacterController>();
                    if (playerCC != null)
                    {
                        Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                        pushDir.y = 0;
                        playerCC.Move(pushDir * 3f);
                    }
                }
            }

            Debug.Log("<color=cyan>★ Boss 震荡波！★</color>");
        }

        private IEnumerator ShockwaveVisual(float maxRadius)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "[VFX] Shockwave";
            ring.transform.position = transform.position + Vector3.up * 0.1f;
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

            var rend = ring.GetComponent<Renderer>();
            Material mat = null;
            if (rend != null)
            {
                mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.2f, 0.7f, 1f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 1f) * 2f);
                rend.material = mat;
            }

            float timer = 0f;
            float duration = 0.4f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                float scale = Mathf.Lerp(0.5f, maxRadius * 2f, t);
                ring.transform.localScale = new Vector3(scale, 0.05f, scale);
                if (mat != null)
                {
                    var c = mat.color;
                    c.a = (1f - t) * 0.5f;
                    mat.color = c;
                }
                yield return null;
            }

            Destroy(ring);
        }

        // ========== 通用 ==========

        private void EndAction(float stunTime)
        {
            _navMotor.ResyncAfterForcedMove();
            _action = BossAction.Stunned;
            _actionTimer = stunTime;
            _chargeTimer = chargeInterval;
            DestroyWarningIndicator();
            RestoreColors();
        }

        private void MoveTowards(Vector3 targetPos)
        {
            _navMotor.MoveTo(targetPos, stats.moveSpeed, meleeRange * 0.85f);
        }

        private void UpdateTacticalMovement(float distance)
        {
            if (_target == null)
                return;
            if (distance > meleeRange * tacticalRangeMultiplier)
            {
                MoveTowards(_target.position);
                return;
            }

            _tacticalTimer -= Time.deltaTime;
            if (_tacticalTimer <= 0f)
            {
                _tacticalTimer = Random.Range(
                    Mathf.Min(tacticalDurationRange.x, tacticalDurationRange.y),
                    Mathf.Max(tacticalDurationRange.x, tacticalDurationRange.y));
                _tacticalDirection = Random.value < 0.5f ? -1f : 1f;
                _tacticalMoveScale = Random.value < tacticalPauseChance
                    ? 0f
                    : Random.Range(0.3f, 0.55f);
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
                return;

            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized) * _tacticalDirection;
            float inwardBias = distance > meleeRange * 1.2f ? 0.25f : 0f;
            Vector3 direction = (side + toTarget.normalized * inwardBias).normalized;
            _navMotor.MoveTo(
                transform.position + direction * 2.5f,
                stats.moveSpeed * _tacticalMoveScale,
                0.05f);
        }

        bool IEnemyAbilityExecutor.IsAbilityLocked =>
            _action != BossAction.Idle && _action != BossAction.Tracking;

        bool IEnemyAbilityExecutor.TryExecuteAbility(EnemyAbilityRule rule)
        {
            switch (rule.Action)
            {
                case EnemyAbilityAction.Melee:
                    MeleeAttack();
                    _meleeTimer = meleeInterval;
                    return true;
                case EnemyAbilityAction.Charge:
                    StartChargePrepare();
                    return true;
                case EnemyAbilityAction.AreaAttack:
                    if (!_isNightBoss
                        && LevelDesign.BossFlagSet.Instance.Evaluate("crown_light_disabled=1"))
                        return false;
                    StartAOECast();
                    return true;
                case EnemyAbilityAction.Leap:
                    StartLeapPrepare();
                    return true;
                case EnemyAbilityAction.Shockwave:
                    StartShockwave();
                    return true;
                case EnemyAbilityAction.Summon:
                    SpawnNightGuardWave();
                    return true;
                default:
                    return TryExecuteCustomAbility(rule.CustomActionKey);
            }
        }

        private bool TryExecuteCustomAbility(string actionKey)
        {
            if (actionKey == "day_crown_sweep" && !_isNightBoss)
            {
                StartCoroutine(DayCrownSweep());
                return true;
            }
            if (actionKey == "night_prison_chains" && _isNightBoss)
            {
                StartCoroutine(NightPrisonChains());
                return true;
            }
            return false;
        }

        private IEnumerator DayCrownSweep()
        {
            _action = BossAction.FinalSkill;
            Vector3 origin = transform.position;
            Vector3 baseDirection = _target.position - origin;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude < 0.01f)
                baseDirection = transform.forward;
            baseDirection.Normalize();

            bool misaligned = LevelDesign.BossFlagSet.Instance.Evaluate(
                "crown_light_misaligned=1");
            if (misaligned)
                baseDirection = transform.forward;

            const float length = 12f;
            const float width = 1.2f;
            float[] angles = { -28f, 0f, 28f };
            var warnings = new List<GameObject>();
            foreach (float angle in angles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
                warnings.Add(CreateFinalSkillBeam(
                    origin,
                    direction,
                    length,
                    width,
                    new Color(1f, 0.75f, 0.15f, 0.35f),
                    "冠光裁决"));
            }

            yield return new WaitForSeconds(1.05f);
            if (stats.IsAlive && _target != null)
            {
                Vector3 playerPosition = _target.position;
                bool hit = false;
                foreach (float angle in angles)
                {
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
                    if (DistanceToSegment(playerPosition, origin, origin + direction * length)
                        <= width * 0.5f)
                    {
                        hit = true;
                        break;
                    }
                }
                if (hit)
                    DamageTarget(1.45f, origin);
            }

            foreach (GameObject warning in warnings)
                if (warning != null)
                    Destroy(warning);
            _action = BossAction.Stunned;
            _actionTimer = 0.65f;
        }

        private IEnumerator NightPrisonChains()
        {
            _action = BossAction.FinalSkill;
            Vector3 center = _target.position;
            const float radius = 4f;
            GameObject warning = CreateFinalSkillRing(
                center,
                radius,
                new Color(0.45f, 0.15f, 0.8f, 0.4f),
                "狱链封步预警");

            yield return new WaitForSeconds(1.1f);
            if (warning != null)
                Destroy(warning);

            if (stats.IsAlive && _target != null
                && Vector3.Distance(Flatten(_target.position), Flatten(center)) <= radius)
            {
                DamageTarget(0.65f, center);
                ApplyPrisonSlow(3.5f);
                CreateChainPrison(center, radius, 3.5f);
            }

            _action = BossAction.Stunned;
            _actionTimer = 0.55f;
        }

        private void DamageTarget(float multiplier, Vector3 hitPoint)
        {
            if (_target == null)
                return;
            IDamageable damageable = _target.GetComponent<IDamageable>();
            if (damageable == null)
                return;
            float defense = damageable.Stats != null ? damageable.Stats.defense : 0f;
            var (damage, _) = stats.CalcSkillDamage(defense, multiplier);
            damageable.OnDamage(damage, hitPoint, gameObject);
        }

        private void ApplyPrisonSlow(float duration)
        {
            if (_target == null)
                return;
            StatusEffectController controller = _target.GetComponent<StatusEffectController>();
            if (controller == null)
                return;
            controller.Apply(new StatusEffect
            {
                id = "boss_night_prison_slow",
                displayName = "狱链束缚",
                description = "移速降低 20%",
                isBuff = false,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = duration,
                duration = duration,
                uiColor = new Color(0.55f, 0.25f, 0.9f),
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MoveSpeed, -0.2f)
                }
            });
        }

        private void CreateChainPrison(Vector3 center, float radius, float duration)
        {
            var root = new GameObject("[BossSkill] NightChainPrison");
            const int segmentCount = 12;
            float segmentLength = 2f * Mathf.PI * radius / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * 360f / segmentCount;
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Chain_{i:00}";
                segment.transform.SetParent(root.transform);
                segment.transform.position = center + radial * radius + Vector3.up * 1.1f;
                segment.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                segment.transform.localScale = new Vector3(segmentLength * 1.08f, 2.2f, 0.18f);
                Renderer renderer = segment.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var material = new Material(MaterialHelper.GetLitShader());
                    material.color = new Color(0.3f, 0.08f, 0.5f);
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(0.55f, 0.12f, 0.9f) * 2.5f);
                    renderer.material = material;
                }
            }
            Destroy(root, duration);
        }

        private GameObject CreateFinalSkillBeam(
            Vector3 origin,
            Vector3 direction,
            float length,
            float width,
            Color color,
            string label)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = $"[Warning] {label}";
            beam.transform.position = origin + direction * length * 0.5f + Vector3.up * 0.08f;
            beam.transform.rotation = Quaternion.LookRotation(direction);
            beam.transform.localScale = new Vector3(width, 0.08f, length);
            Collider collider = beam.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            ApplyFinalSkillMaterial(beam.GetComponent<Renderer>(), color);
            return beam;
        }

        private GameObject CreateFinalSkillRing(
            Vector3 center,
            float radius,
            Color color,
            string label)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = $"[Warning] {label}";
            ring.transform.position = center + Vector3.up * 0.05f;
            ring.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
            Collider collider = ring.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            ApplyFinalSkillMaterial(ring.GetComponent<Renderer>(), color);
            return ring;
        }

        private static void ApplyFinalSkillMaterial(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;
            var material = new Material(MaterialHelper.GetLitShader());
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 2.2f);
            renderer.material = material;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            point = Flatten(point);
            start = Flatten(start);
            end = Flatten(end);
            Vector3 segment = end - start;
            if (segment.sqrMagnitude < 0.001f)
                return Vector3.Distance(point, start);
            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector3.Distance(point, start + segment * t);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
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

        // ========== 预警 ==========

        private void CreateWarningIndicator(bool isCharge)
        {
            DestroyWarningIndicator();
            if (isCharge)
            {
                _warningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _warningIndicator.name = "[Warning] BossCharge";
            }
            else
            {
                _warningIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _warningIndicator.name = "[Warning] BossAOE";
            }

            var wCol = _warningIndicator.GetComponent<Collider>();
            if (wCol != null) Destroy(wCol);

            var wRend = _warningIndicator.GetComponent<Renderer>();
            if (wRend != null)
            {
                Color warnColor = isCharge
                    ? new Color(1f, 0.2f, 0.08f, 0.35f)   // 冲锋：橙红
                    : new Color(0.95f, 0.15f, 0.2f, 0.32f); // AOE：血红
                var mat = MaterialHelper.CreateLitTransparent(warnColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    Color emissive = isCharge
                        ? new Color(1f, 0.25f, 0.1f) * 1.8f
                        : new Color(1f, 0.2f, 0.25f) * 1.6f;
                    mat.SetColor("_EmissionColor", emissive);
                }
                wRend.material = mat;
                wRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Boss telegraph 起手就先爆一个大爆环，更强烈的"危险"信号
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f,
                isCharge ? 2.0f : 3.0f,
                isCharge ? new Color(1f, 0.3f, 0.1f, 1f) : new Color(1f, 0.2f, 0.3f, 1f),
                lifetime: 0.6f);
        }

        private void UpdateChargeWarning()
        {
            if (_warningIndicator == null) return;
            float length = chargeSpeed * chargeDuration;
            Vector3 center = transform.position + _chargeDirection * (length / 2f) + Vector3.up * 0.1f;
            _warningIndicator.transform.position = center;
            _warningIndicator.transform.localScale = new Vector3(2f, 0.1f, length);
            _warningIndicator.transform.rotation = Quaternion.LookRotation(_chargeDirection);

            var rend = _warningIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.PingPong(Time.time * 6f, 0.3f) + 0.1f;
                rend.material.color = new Color(1f, 0.3f, 0.1f, alpha);
            }
        }

        private void UpdateAOEWarning()
        {
            if (_warningIndicator == null) return;
            _warningIndicator.transform.position = _aoeTargetPos + Vector3.up * 0.05f;

            float progress = 1f - (_actionTimer / aoeWarningTime);
            float scale = aoeRadius * 2f * (1f + Mathf.Sin(Time.time * 8f) * 0.03f);
            _warningIndicator.transform.localScale = new Vector3(scale, 0.05f, scale);

            var rend = _warningIndicator.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.5f, progress);
                rend.material.color = new Color(0.8f, 0.1f, 0.8f, alpha);
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

            // Boss硬直较短
            _stunTimer = 0.15f;

            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.15f;
                knockback.y = 0;
                _cc.Move(knockback);
                _navMotor.ResyncAfterForcedMove();
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerHeavy();
            if (!stats.IsAlive) OnDeath();
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";
            DecrementAlive();

            DestroyWarningIndicator();

            // v0.5 搜打撤：Boss 必定掉一件【洞府素材】（境界 Boss 是搜打撤的关键收益点）
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(1.5f, 0, 0), 1f);
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(-1.5f, 0, 0), 1f);

            // v0.5 Week 6：境界 Boss 必定掉 1 颗"妖丹"（高价值素材，专属 Boss 掉落）
            var yaodan = Resources.Load<ItemData>("CaveMaterials/妖丹");
            if (yaodan != null)
            {
                ItemPickup.Spawn(yaodan, transform.position + new Vector3(0, 0, 1.8f));
            }

            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject, Position = transform.position
            });

            // 击杀回复（模块系统处理）

            if (HitStop.Instance != null) HitStop.Instance.TriggerKill();
            Debug.Log("<color=yellow>★★★ Boss 被击败！★★★</color>");
            StartCoroutine(DeathAnimation());
        }

        private IEnumerator DeathAnimation()
        {
            enabled = false;
            _cc.enabled = false;

            // Boss死亡更戏剧化
            float timer = 0.8f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.8f;
                transform.localScale = startScale * t;
                // 旋转
                transform.Rotate(Vector3.up * 360f * Time.deltaTime, Space.World);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>工厂方法：生成Boss</summary>
        public static EnemyBoss Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f, int bossID = 1)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.GetBossPrefab(bossID) : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Boss");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            if (prefab == null)
            {
                go.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.6f, 0.1f, 0.1f);
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.05f));
                }
            }
            else
            {
                go.transform.localScale = Vector3.one * 2f;
            }

            var existingCols = go.GetComponents<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);
            var childCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in childCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 1.0f;
            cc.height = 3.6f;
            cc.center = new Vector3(0, 1.8f, 0);

            var boss = go.AddComponent<EnemyBoss>();
            boss._isNightBoss = LevelDesign.LevelAPhaseRuntime.IsNightMapActive;
            boss._displayName = boss._isNightBoss ? "无暮王残念" : "最后摄政官";
            var config = GameConfig.Instance;
            if (config != null)
            {
                var er = LevelDesign.ConfigDatabase.Instance?.GetEnemy(6); // Boss
                boss.stats.maxHp = config.敌人基础血量 * (er?.HpMul ?? 8f) * hpMultiplier;
                boss.stats.attackDamage = config.敌人基础攻击力 * (er?.DmgMul ?? 3f) * dmgMultiplier;
                boss.stats.defense = config.敌人基础防御力 * (er?.DefMul ?? 3f);
            }
            else
            {
                boss.stats.maxHp = 300f * hpMultiplier;
                boss.stats.attackDamage = 20f * dmgMultiplier;
                boss.stats.defense = 9f;
            }
            boss.stats.currentHp = boss.stats.maxHp;

            // V0.2.4：按传入 bossID 应用形态修正 + 出场对白
            var phaseResult = LevelDesign.LevelDesignDirector.Instance.ApplyBossPhase(boss, bossID);
            if (phaseResult != null)
                boss._pendingPhase2Row = LevelDesign.BossPhaseSelector.Select(bossID)?.Phase2;

            return boss;
        }

        private void OnDestroy()
        {
            DecrementAlive();   // 兜底：未经 OnDeath 直接销毁（场景切换 / 重开）也要扣减
            DestroyWarningIndicator();
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
