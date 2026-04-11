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

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private int _roomLevel;

        private CharacterController _cc;
        private Transform _target;

        // 计时器
        private float _meleeTimer;
        private float _chargeTimer;
        private float _aoeTimer;
        private float _stunTimer;

        // 状态
        private enum BossPhase { Phase1, Phase2 }
        private enum BossAction { Idle, Tracking, MeleeAttack, ChargePrepare, Charging, AOECast, Stunned }
        private BossPhase _phase = BossPhase.Phase1;
        private BossAction _action = BossAction.Idle;
        private float _actionTimer;
        private Vector3 _chargeDirection;
        private Vector3 _aoeTargetPos;

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
                var enemy = EnemyBase.Spawn(pos, 0.5f, 0.5f, _roomLevel, possibleDrops);
                if (enemy != null)
                {
                    // 通知房间系统
                    // 小怪不计入房间敌人数
                }
            }
        }

        private void UpdateTracking(float distToTarget)
        {
            // 更新计时器
            _chargeTimer -= Time.deltaTime;
            if (_phase == BossPhase.Phase2)
                _aoeTimer -= Time.deltaTime;

            // 优先级：冲锋 > AOE > 近战
            if (_chargeTimer <= 0 && distToTarget > meleeRange && distToTarget <= detectRange)
            {
                StartChargePrepare();
                return;
            }

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
                float damage = stats.attackDamage * (_phase == BossPhase.Phase2 ? 1.3f : 1f);
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
                    damageable.OnDamage(stats.attackDamage * 1.5f, transform.position, gameObject);
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
                        damageable.OnDamage(stats.attackDamage * 1.2f, _aoeTargetPos, gameObject);
                }
            }
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
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.2f, 0.1f, 0.2f);
                wRend.material = mat;
            }
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

            DestroyWarningIndicator();
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
            Debug.Log("<color=yellow>★★★ Boss 被击败！★★★</color>");
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem()
        {
            // Boss 必定掉落
            if (possibleDrops == null || possibleDrops.Length == 0) return;
            for (int i = 0; i < 3; i++) // 掉3个
            {
                ItemData item = possibleDrops[Random.Range(0, possibleDrops.Length)];
                if (item != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
                    ItemPickup.Spawn(item, transform.position + offset);
                }
            }
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
        public static EnemyBoss Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null)
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
            }
            else
            {
                boss.stats.maxHp = 300f * hpMultiplier;
                boss.stats.attackDamage = 20f * dmgMultiplier;
            }
            boss.stats.currentHp = boss.stats.maxHp;
            boss._roomLevel = roomLevel;
            if (drops != null) boss.possibleDrops = drops;

            return boss;
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
