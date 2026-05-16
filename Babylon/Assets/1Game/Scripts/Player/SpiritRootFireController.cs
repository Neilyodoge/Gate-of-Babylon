using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 火化身 · 狂战之火（怒气主动键） —— v0.4 落地
    ///
    /// 设计参考：GDD 4.3.4
    /// - 怒气资源条（满 100），普攻命中 +5、受击 +10、每秒被动衰减 -2（命中 1s 内停止衰减）
    /// - 主动键 V（怒气 ≥ 50 时可释放）：消耗全部怒气进入【狂火】，时长 = 怒气 ÷ 20
    ///     - 攻速 +50% / 移速 +30% / 普攻附加 AOE 爆炸
    ///     - 期间击杀 +10 怒气（边狂边攒延长）
    /// - 强制爆发兜底：怒气满 100 后 5s 内不主动开 → 自动触发 6s
    /// - 融合点：技能命中回 +5 怒气；狂火期间所有技能 CD ×0.7（通过 _skillCdMultiplier 公开给 PlayerCombat）
    ///
    /// 仅在 CurrentRoot == Fire 时激活。
    /// </summary>
    public class SpiritRootFireController : MonoBehaviour
    {
        public const string FireFrenzyEffectId = "Root_FireFrenzy";
        // v0.4 天赋节点
        private const string TalentBurningChainId = "Talent_Fire_BurningChain";

        [Header("怒气资源条")]
        [SerializeField] private int maxRage = 100;
        [SerializeField] private int rageOnAttackHit = 5;
        [SerializeField] private int rageOnDamaged = 10;
        [SerializeField] private int rageOnKill = 10;
        [SerializeField] private int rageOnSkillHit = 5;
        [SerializeField] private float rageDecayPerSec = 2f;
        [SerializeField] private float decayStopWindow = 1f;

        [Header("狂火主动键")]
        [SerializeField] private Key frenzyHotkey = Key.V;
        [SerializeField] private int minRageToActivate = 50;
        [SerializeField] private float frenzyDurationPerRage = 1f / 20f;  // 100 怒气 = 5s
        [SerializeField] private float forceFrenzyDelay = 5f;             // 怒气满 100 闲置 5s 后强制爆发
        [SerializeField] private float forceFrenzyDuration = 6f;

        [Header("狂火 BUFF")]
        [SerializeField] private float frenzyAtkSpeedBonus = 0.5f;
        [SerializeField] private float frenzyMoveSpeedBonus = 0.3f;
        [SerializeField] private float frenzyExplosionRatio = 0.4f;       // 普攻附加 ×0.4 攻击 AOE
        [SerializeField] private float frenzyExplosionRadius = 1.2f;
        [SerializeField] private LayerMask enemyLayerOverride = 0;
        [SerializeField] private float frenzySkillCdMultiplier = 0.7f;    // 融合层：狂火期间所有技能 CD ×0.7

        private PlayerController _player;
        private SpiritRootController _root;
        private StatusEffectController _status;

        // 怒气运行时
        private float _rage;
        private float _lastHitTime;
        private float _fullRageIdleTimer = 0f;

        // 狂火运行时
        private bool _inFrenzy;
        private float _frenzyTimer;

        public int CurrentRage => Mathf.RoundToInt(_rage);
        public int MaxRage => maxRage;
        public bool InFrenzy => _inFrenzy;
        public float FrenzyTimer => _frenzyTimer;
        /// <summary>融合层：狂火期间技能 CD 乘数（PlayerCombat 在计算技能 CD 时乘上这个，1.0 = 不变）</summary>
        public float SkillCdMultiplier => _inFrenzy ? frenzySkillCdMultiplier : 1f;
        public bool HasTalentBurningChain => _status != null && _status.Has(TalentBurningChainId);

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _root = GetComponent<SpiritRootController>();
            _status = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            GameEvents.Subscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            GameEvents.Unsubscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }

        private void Update()
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Fire) return;

            // 推进狂火状态
            if (_inFrenzy)
            {
                _frenzyTimer -= Time.deltaTime;

                // 狂火期间持续普攻 AOE 检测（每 0.5s 触发一次）
                TickFrenzyAoe();

                if (_frenzyTimer <= 0f)
                    EndFrenzy();
                return;
            }

            // 怒气衰减（最近 decayStopWindow 内有命中则不衰减）
            if (Time.time - _lastHitTime > decayStopWindow && _rage > 0f)
            {
                _rage = Mathf.Max(0f, _rage - rageDecayPerSec * Time.deltaTime);
                PublishRageChanged();
            }

            // 怒气满 100 强制爆发兜底
            if (_rage >= maxRage)
            {
                _fullRageIdleTimer += Time.deltaTime;
                if (_fullRageIdleTimer >= forceFrenzyDelay)
                {
                    _fullRageIdleTimer = 0f;
                    StartFrenzy(forceFrenzyDuration, true);
                }
            }
            else
            {
                _fullRageIdleTimer = 0f;
            }

            // 主动按键
            var kb = Keyboard.current;
            if (kb != null && kb[frenzyHotkey].wasPressedThisFrame && _rage >= minRageToActivate)
            {
                float duration = _rage * frenzyDurationPerRage;
                StartFrenzy(duration, false);
            }
        }

        // ==================== 怒气累积 ====================

        private void OnMeleeHit(GameEvents.MeleeHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Fire) return;
            AddRage(rageOnAttackHit);
        }

        private void OnSkillHit(GameEvents.SkillHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Fire) return;
            // 融合点：技能命中也回怒气
            AddRage(rageOnSkillHit);
        }

        private void OnPlayerDamaged(GameEvents.PlayerDamaged evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Fire) return;
            AddRage(rageOnDamaged);
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Fire) return;
            if (_inFrenzy)
                AddRage(rageOnKill);
        }

        private void AddRage(int amount)
        {
            if (amount <= 0) return;
            _rage = Mathf.Clamp(_rage + amount, 0f, maxRage);
            _lastHitTime = Time.time;
            PublishRageChanged();
        }

        private void PublishRageChanged()
        {
            GameEvents.Publish(new GameEvents.RageChanged
            {
                CurrentRage = Mathf.RoundToInt(_rage),
                MaxRage = maxRage
            });
        }

        // ==================== 狂火状态 ====================

        private void StartFrenzy(float duration, bool isForced)
        {
            if (_inFrenzy) return;

            _inFrenzy = true;
            _frenzyTimer = duration;
            _rage = 0f;
            _fullRageIdleTimer = 0f;
            PublishRageChanged();

            // 应用 StatusEffect（攻速 +50% / 移速 +30%）
            if (_status != null)
            {
                _status.Apply(new StatusEffect
                {
                    id = FireFrenzyEffectId,
                    isBuff = true,
                    elementTag = ElementTag.Fire,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = duration,
                    modifiers = new List<StatModifier>
                    {
                        StatModifier.Percent(StatType.AttackSpeed, frenzyAtkSpeedBonus),
                        StatModifier.Percent(StatType.MoveSpeed, frenzyMoveSpeedBonus)
                    },
                    displayName = isForced ? "狂火（强制爆发）" : "狂火",
                    description = $"攻速 +{frenzyAtkSpeedBonus * 100:F0}% / 移速 +{frenzyMoveSpeedBonus * 100:F0}% / 普攻附加 AOE",
                    uiColor = new Color(1f, 0.4f, 0.1f, 1f)
                });
            }

            // 视觉：玩家位置火属性大爆发 + AOE 圆环
            FxFactory.SpawnElementBurst(transform.position + Vector3.up * 0.5f, ElementTag.Fire, 3f, 0.7f);
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, 3f, new Color(1f, 0.4f, 0.1f, 1f), 0.6f);

            GameEvents.Publish(new GameEvents.FireFrenzyState
            {
                IsActive = true,
                Duration = duration,
                IsForced = isForced
            });

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = isForced ? "强制狂火！" : "狂火！"
            });
        }

        private void EndFrenzy()
        {
            if (!_inFrenzy) return;
            _inFrenzy = false;
            _frenzyTimer = 0f;
            _status?.Remove(FireFrenzyEffectId);

            GameEvents.Publish(new GameEvents.FireFrenzyState
            {
                IsActive = false,
                Duration = 0f,
                IsForced = false
            });
        }

        // ==================== 狂火期间普攻 AOE ====================

        private float _frenzyAoeTimer = 0f;
        private const float FrenzyAoeInterval = 0.5f;

        private void TickFrenzyAoe()
        {
            _frenzyAoeTimer -= Time.deltaTime;
            if (_frenzyAoeTimer > 0f) return;
            _frenzyAoeTimer = FrenzyAoeInterval;
            if (_player == null) return;

            LayerMask mask = enemyLayerOverride.value != 0 ? enemyLayerOverride : GetEnemyLayerFromCombat();
            if (mask.value == 0) return;

            // 灼焰链天赋：AOE 半径 +50%、伤害 +30%
            float radius = frenzyExplosionRadius;
            float ratio = frenzyExplosionRatio;
            if (HasTalentBurningChain)
            {
                radius *= 1.5f;
                ratio *= 1.3f;
            }

            var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.6f, radius, mask);
            foreach (var col in hits)
            {
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform) || col.CompareTag("Player")) continue;
                var dmgable = col.GetComponent<IDamageable>();
                if (dmgable == null) continue;
                float dmg = _player.Stats.attackDamage * ratio;
                dmgable.OnDamage(dmg, col.transform.position, gameObject);
            }

            // 视觉：脚下一圈短暂火环
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, radius, new Color(1f, 0.4f, 0.1f, 1f), 0.3f);
        }

        private LayerMask GetEnemyLayerFromCombat()
        {
            var pc = GetComponent<PlayerCombat>();
            if (pc != null) return pc.EnemyLayer;
            return 0;
        }
    }
}
