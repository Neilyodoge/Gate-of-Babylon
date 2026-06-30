using UnityEngine;
using System.Collections;

namespace XianTu
{
    /// <summary>
    /// Boss 敌人 —— 多阶段行为模式
    /// 阶段1（>50%血）：近战连击 + 冲锋
    /// 阶段2（≤50%血）：增加AOE攻击 + 速度提升 + 召唤小怪
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBoss : MonoBehaviour, IDamageable
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


        private CharacterController _cc;
        private Transform _target;

        // 计时器
        private float _meleeTimer;
        private float _chargeTimer;
        private float _aoeTimer;
        private float _stunTimer;

        // 状态
        private enum BossPhase { Phase1, Phase2 }
        private enum BossAction { Idle, Tracking, MeleeAttack, ChargePrepare, Charging, AOECast, Stunned, LeapPrepare, Leaping, ShockwaveCast }
        private BossPhase _phase = BossPhase.Phase1;
        private BossAction _action = BossAction.Idle;
        private float _actionTimer;
        private Vector3 _chargeDirection;
        private Vector3 _aoeTargetPos;

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

        // 预警
        private GameObject _warningIndicator;
        private EnemyHealthBar _healthBar;

        // Boss名字标签
        private GameObject _nameTag;

        public CombatStats Stats => stats;

        /// <summary>当前存活的境界 Boss 数量（心魔值乱入据此判断"是否正在打 Boss"）。</summary>
        public static int AliveCount { get; private set; }
        private bool _deadCounted;

        private void Awake()
        {
            AliveCount++;
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = MaterialHelper.SafeGetColor(_renderers[i].material);
        }

        private void DecrementAlive()
        {
            if (_deadCounted) return;
            _deadCounted = true;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        private void Start()
        {
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
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "★ 守关妖兽 ★";
            text.fontSize = 24;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(1f, 0.4f, 0.1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

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
            if (!_phase2Triggered && stats.currentHp <= stats.maxHp * 0.5f)
            {
                _phase2Triggered = true;
                _phase = BossPhase.Phase2;
                stats.moveSpeed *= 1.3f;

                // 阶段转换视觉效果：所有Renderer变红
                for (int i = 0; i < _originalColors.Length; i++)
                    _originalColors[i] = new Color(1f, 0.3f, 0.1f);

                Debug.Log("<color=red>★ Boss 进入狂暴阶段！★</color>");

                // 召唤2只小怪
                SpawnMinions(2);
            }
        }

        private void SpawnMinions(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                Vector3 pos = transform.position + offset;
                EnemyBase.Spawn(pos, 0.5f, 0.5f);
            }
        }

        private void UpdateTracking(float distToTarget)
        {
            // 更新计时器
            _chargeTimer -= Time.deltaTime;
            _leapCooldown -= Time.deltaTime;
            _shockwaveCooldown -= Time.deltaTime;
            if (_phase == BossPhase.Phase2)
                _aoeTimer -= Time.deltaTime;

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
            if (_phase == BossPhase.Phase2 && _aoeTimer <= 0)
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
                MoveTowards(_target.position);
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
            _aoeTargetPos = _target.position;
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
            _action = BossAction.Stunned;
            _actionTimer = stunTime;
            _chargeTimer = chargeInterval;
            DestroyWarningIndicator();
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

            // v0.5.5：灵脉道具（秘境专属掉落 → 灵脉经验）。Boss 50% 掉"聚灵珠"（灵潮汹涌异象 → 必掉）
            if (Random.value < 0.5f * RealmAnomalySystem.Instance.SpiritVeinDropMul)
                SpiritVeinPickup.Spawn("聚灵珠", 150, transform.position + new Vector3(0, 0, -1.8f));

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
        public static EnemyBoss Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.Boss敌人Prefab : null;
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
                // Prefab模型的Boss适当放大
                go.transform.localScale = Vector3.one * 2f;
            }

            var existingCols = go.GetComponents<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);
            // 也移除子物体上的碰撞体，避免冲突
            var childCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in childCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 1.0f;   // 匹配大体型
            cc.height = 3.6f;   // 匹配大体型
            cc.center = new Vector3(0, 1.8f, 0);

            var boss = go.AddComponent<EnemyBoss>();
            var config = GameConfig.Instance;
            if (config != null)
            {
                boss.stats.maxHp = config.敌人基础血量 * 8f * hpMultiplier;
                boss.stats.attackDamage = config.敌人基础攻击力 * 3f * dmgMultiplier;
                boss.stats.defense = config.敌人基础防御力 * 3f;
            }
            else
            {
                boss.stats.maxHp = 300f * hpMultiplier;
                boss.stats.attackDamage = 20f * dmgMultiplier;
                boss.stats.defense = 9f;
            }
            boss.stats.currentHp = boss.stats.maxHp;

            // GDD §12.3：根据 BossFlagSet 应用形态修正 + 出场对白
            // 仅在 LevelDesign 系统就绪时生效；不就绪则保持原有数值（向下兼容）。
            LevelDesign.LevelDesignDirector.Instance.ApplyBossPhase(boss, bossID: 1);

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
