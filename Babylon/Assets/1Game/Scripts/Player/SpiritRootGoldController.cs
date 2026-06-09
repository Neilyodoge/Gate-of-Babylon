using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 金化身 · 灵压同步（完美收刀）—— v0.3.2 机制版核心机制
    ///
    /// 设计参考：GDD 4.3.1
    /// - 普攻三段连招的每一段收招瞬间出现"灵压窗口"
    /// - 主动技能释放后 0.3~0.8s 内出现"灵压窗口"
    /// - 闪避结束后 0.2~0.5s 内 30% 概率出现"灵压窗口"
    /// - 剑心通明期间每 1s 自动出现一次"灵压窗口"
    /// - 窗口内按下普攻键 → 触发【灵压爆发】（玩家前方扇形 AOE ×1.5 攻击 + 0.5s 硬直）
    /// - 连续 3 次完美 → 进入【剑心通明】4s（+30% 攻击 / +25% 暴击率）
    ///
    /// 仅在 CurrentRoot == Metal 时激活；其他化身挂这个组件也不会有任何效果。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(StatusEffectController))]
    public class SpiritRootGoldController : MonoBehaviour
    {
        [Header("窗口配置")]
        [SerializeField] private float windowDuration = 0.18f;
        [SerializeField] private float skillWindowDelay = 0.3f;  // 技能后多久开窗口
        [SerializeField] private float dodgeWindowDelay = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dodgeWindowChance = 0.3f;

        [Header("灵压爆发")]
        [SerializeField] private float burstRadius = 2.5f;
        [SerializeField] private float burstAngle = 120f;
        [SerializeField] private float burstDamageMultiplier = 1.5f;
        [SerializeField] private float burstStunDuration = 0.5f;
        /// <summary>留 0 = 自动从 PlayerCombat 取 enemyLayer，避免把玩家自己也打到</summary>
        [SerializeField] private LayerMask enemyLayerOverride = 0;
        private LayerMask _resolvedEnemyLayer;

        [Header("剑心通明")]
        [SerializeField] private float swordHeartDuration = 4f;
        [SerializeField] private float swordHeartAtkBonus = 0.30f;
        [SerializeField] private float swordHeartCritBonus = 0.25f;
        [SerializeField] private float swordHeartWindowInterval = 1f;  // 剑心期间每秒开 1 个窗口

        private const string SwordHeartEffectId = "Root_GoldSwordHeart";

        [Header("御金 · 飞剑环绕（v0.6 剑魄→御金）")]
        [SerializeField] private bool enableFlyingSwords = true;
        [SerializeField] private int flyingSwordCount = 3;
        [SerializeField] private float flyingSwordDmgRatio = 0.6f;
        private FlyingSwordSwarm _swordSwarm;

        [Header("御金 · 塑金形态 / 磁牵（v0.6 补全）")]
        [SerializeField] private UnityEngine.InputSystem.Key shapeMetalKey = UnityEngine.InputSystem.Key.V;
        [SerializeField] private float bladeAtkBonus = 0.25f;   // 塑金·刃（攻）
        [SerializeField] private float armorDmgRed = 0.35f;     // 塑金·甲（守）
        [SerializeField] private float magnetPullRadius = 4.0f; // 磁牵聚怪半径（灵压爆发前）
        [SerializeField] private float magnetPullMax = 2.2f;    // 单次最大拉近距离
        private int _goldForm;   // 0=无 1=刃(攻) 2=甲(守)
        private const string FormBladeId = "Gold_FormBlade";
        private const string FormArmorId = "Gold_FormArmor";

        private PlayerController _player;
        private StatusEffectController _status;
        private SpiritRootController _root;

        // 窗口运行时状态
        private float _windowRemaining = 0f;
        private string _windowSource = null;  // "Melee"/"Skill"/"Dodge"/"SwordHeart"
        private int _consecutivePerfects = 0;
        private float _swordHeartWindowTimer = 0f;
        private bool _attackInputConsumed = false;  // 防止同一帧重复触发

        public bool IsWindowOpen => _windowRemaining > 0f;
        public int ConsecutivePerfects => _consecutivePerfects;

        // v0.4 天赋节点（境界突破解锁；查询 StatusEffect 上的标记 id）
        private const string TalentPowerBreakId = "Talent_Gold_PowerBreak";
        public bool HasTalentPowerBreak => _status != null && _status.Has(TalentPowerBreakId);

        // 大破天赋触发后：标记下一次技能命中目标，使其受到 +50% 技能伤害
        private GameObject _powerBreakTarget;
        private float _powerBreakTimer;
        private const float PowerBreakWindow = 5f;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _status = GetComponent<StatusEffectController>();
            _root = GetComponent<SpiritRootController>();
        }

        /// <summary>
        /// 解析有效的 enemyLayer：
        /// - 优先用 Inspector 覆盖值（enemyLayerOverride）
        /// - 否则从 PlayerCombat 拿同一份 LayerMask（与近战攻击保持一致）
        /// - 都没有时退化为"全部层但运行时跳过玩家自身"
        /// </summary>
        private LayerMask ResolveEnemyLayer()
        {
            if (_resolvedEnemyLayer.value != 0) return _resolvedEnemyLayer;
            if (enemyLayerOverride.value != 0) { _resolvedEnemyLayer = enemyLayerOverride; return _resolvedEnemyLayer; }
            var pc = GetComponent<PlayerCombat>();
            if (pc != null && pc.EnemyLayer.value != 0)
            {
                _resolvedEnemyLayer = pc.EnemyLayer;
                return _resolvedEnemyLayer;
            }
            // fallback: 全部层（命中循环里会显式排除玩家自身的 IDamageable）
            _resolvedEnemyLayer = ~0;
            return _resolvedEnemyLayer;
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.SlashVFXRequested>(OnSlashVFX);
            GameEvents.Subscribe<GameEvents.SkillCastStarted>(OnSkillCast);
            GameEvents.Subscribe<GameEvents.DodgeFinished>(OnDodge);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(OnSkillHit);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.SlashVFXRequested>(OnSlashVFX);
            GameEvents.Unsubscribe<GameEvents.SkillCastStarted>(OnSkillCast);
            GameEvents.Unsubscribe<GameEvents.DodgeFinished>(OnDodge);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            if (_swordSwarm != null) { Destroy(_swordSwarm.gameObject); _swordSwarm = null; }
            ClearGoldForm();
        }

        // 大破天赋：技能命中标记目标时，额外造成 +50% 攻击力伤害
        private void OnSkillHit(GameEvents.SkillHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal) return;
            if (!HasTalentPowerBreak) return;
            if (_powerBreakTarget == null || _powerBreakTimer <= 0f) return;
            if (evt.Target != _powerBreakTarget) return;

            var dmgable = evt.Target.GetComponent<IDamageable>();
            if (dmgable != null && _player != null)
            {
                float bonus = _player.Stats.attackDamage * 0.5f;
                dmgable.OnDamage(bonus, evt.HitPoint, _player.gameObject);
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = evt.HitPoint + Vector3.up * 1.6f,
                    Damage = bonus,
                    SpecialTag = "大破！"
                });
            }
            _powerBreakTarget = null;   // 消耗一次
            _powerBreakTimer = 0f;
        }

        private void Update()
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal)
            {
                if (_swordSwarm != null) { Destroy(_swordSwarm.gameObject); _swordSwarm = null; }
                if (_goldForm != 0) ClearGoldForm();
                return;
            }

            // 御金：维持飞剑环绕
            if (enableFlyingSwords && _swordSwarm == null && _player != null)
            {
                var go = new GameObject("FlyingSwordSwarm");
                _swordSwarm = go.AddComponent<FlyingSwordSwarm>();
                _swordSwarm.Init(_player, ResolveEnemyLayer(), flyingSwordCount, flyingSwordDmgRatio);
            }

            // 塑金：V 键循环 无 → 刃(攻) → 甲(守) → 无
            {
                var kbForm = Keyboard.current;
                if (kbForm != null && kbForm[shapeMetalKey].wasPressedThisFrame)
                    CycleGoldForm();
            }

            // 大破天赋窗口推进
            if (_powerBreakTimer > 0f)
            {
                _powerBreakTimer -= Time.deltaTime;
                if (_powerBreakTimer <= 0f) _powerBreakTarget = null;
            }

            // 推进窗口倒计时
            if (_windowRemaining > 0f)
            {
                _windowRemaining -= Time.deltaTime;
                if (_windowRemaining <= 0f)
                {
                    _windowRemaining = 0f;
                    _consecutivePerfects = 0;  // 没在窗口内按 → 连击清零
                    _attackInputConsumed = false;
                }
            }

            // 剑心通明期间每秒自动开窗口
            if (_status != null && _status.Has(SwordHeartEffectId))
            {
                _swordHeartWindowTimer -= Time.deltaTime;
                if (_swordHeartWindowTimer <= 0f)
                {
                    OpenWindow("SwordHeart");
                    _swordHeartWindowTimer = swordHeartWindowInterval;
                }
            }
            else
            {
                _swordHeartWindowTimer = 0f;
            }

            // 监听窗口内的玩家左键输入 → 触发灵压爆发
            if (_windowRemaining > 0f && !_attackInputConsumed)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (SkillBarUI.Instance != null && SkillBarUI.Instance.IsMouseOverSlot) return;
                    TriggerPerfectStrike();
                }
            }
        }

        // ==================== 窗口触发钩子 ====================

        private void OnSlashVFX(GameEvents.SlashVFXRequested evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal) return;
            // 每段收招瞬间开窗口（动画事件触发时正好是连招中段）
            OpenWindow("Melee");
        }

        private void OnSkillCast(GameEvents.SkillCastStarted evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal) return;
            // 延迟 skillWindowDelay 后开窗口
            Invoke(nameof(OpenSkillWindow), skillWindowDelay);
        }

        private void OpenSkillWindow() => OpenWindow("Skill");

        private void OnDodge(GameEvents.DodgeFinished evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal) return;
            if (Random.value > dodgeWindowChance) return;
            Invoke(nameof(OpenDodgeWindow), dodgeWindowDelay);
        }

        private void OpenDodgeWindow() => OpenWindow("Dodge");

        // ==================== 窗口管理 ====================

        private void OpenWindow(string source)
        {
            _windowRemaining = windowDuration;
            _windowSource = source;
            _attackInputConsumed = false;

            // 视觉：玩家头顶冒一颗"金色三角"球 + 围绕一圈金环（窗口持续期间提示）
            Color gold = new Color(1f, 0.85f, 0.2f, 1f);
            Vector3 headPos = transform.position + Vector3.up * 2.2f;
            FxFactory.SpawnHeadHint(headPos, gold, 0.3f, windowDuration, PrimitiveType.Capsule);

            GameEvents.Publish(new GameEvents.PerfectStrikeWindowOpened
            {
                WindowDuration = windowDuration,
                PlayerHeadPos = headPos,
                SourceTag = source
            });
        }

        // ==================== 灵压爆发 ====================

        private void TriggerPerfectStrike()
        {
            _attackInputConsumed = true;
            _windowRemaining = 0f;
            _consecutivePerfects++;

            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 forward = _player != null ? _player.AimDirection : transform.forward;

            // 御金·磁牵：先把周围敌人拉向身前聚拢，再让爆发扇形一网打尽
            MagnetPull(transform.position);

            float baseDamage = _player.Stats.CalculateDamage() * burstDamageMultiplier;
            LayerMask mask = ResolveEnemyLayer();
            var hits = Physics.OverlapSphere(origin, burstRadius, mask);
            int hitCount = 0;
            Vector3 lastHitPos = origin;
            foreach (var col in hits)
            {
                // 关键：跳过玩家自身（防止"打自己掉血"bug）
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (col.CompareTag("Player")) continue;

                Vector3 dir = (col.transform.position - origin).normalized;
                dir.y = 0;
                if (Vector3.Angle(forward, dir) > burstAngle * 0.5f) continue;

                var dmgable = col.GetComponent<IDamageable>();
                if (dmgable == null) continue;

                Vector3 hp = col.ClosestPoint(origin);
                dmgable.OnDamage(baseDamage, hp, gameObject);
                lastHitPos = hp;
                hitCount++;

                // 大破天赋：标记本次命中目标，5s 内下一次技能命中该目标 +50% 攻击伤害
                if (HasTalentPowerBreak)
                {
                    _powerBreakTarget = col.gameObject;
                    _powerBreakTimer = PowerBreakWindow;
                }

                // 给敌人附加短暂硬直状态
                var enemyStatus = col.GetComponent<StatusEffectController>();
                if (enemyStatus != null)
                {
                    enemyStatus.Apply(new StatusEffect
                    {
                        id = "Stun_PerfectStrike",
                        isBuff = false,
                        elementTag = ElementTag.None,
                        stacks = 1,
                        maxStacks = 1,
                        defaultDuration = burstStunDuration,
                        displayName = "硬直",
                        description = $"灵压爆发短停 {burstStunDuration:F1}s",
                        uiColor = new Color(1f, 0.85f, 0.2f)
                    });
                }
            }

            // 连续 3 次 → 进入剑心通明
            bool entered = false;
            if (_consecutivePerfects >= 3)
            {
                ApplySwordHeart();
                _consecutivePerfects = 0;
                entered = true;
            }

            // 视觉：金色 AOE 圆环 + 一道沿瞄准方向的剑气
            Color gold = new Color(1f, 0.85f, 0.2f, 1f);
            FxFactory.SpawnAOERing(origin + Vector3.down * 0.6f, burstRadius, gold, 0.45f);
            FxFactory.SpawnSliceLine(transform.position, forward, burstRadius * 1.5f, gold, 0.4f);

            // 进入剑心时额外画一个金色大球当场冲击
            if (entered)
            {
                FxFactory.SpawnElementBurst(transform.position + Vector3.up * 1f, ElementTag.None, 2.0f, 0.6f);
            }

            // 飘字 + 屏幕反馈
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = entered ? "剑心通明！" : (hitCount > 0 ? $"完美 ×{_consecutivePerfects}" : "完美！")
            });

            GameEvents.Publish(new GameEvents.PerfectStrikeTriggered
            {
                HitPoint = lastHitPos,
                ConsecutiveCount = entered ? 3 : _consecutivePerfects,
                EnteredSwordHeart = entered
            });
        }

        /// <summary>
        /// 一念刹那（技能 16 · AvatarSpecial）：释放一次特殊强力剑斩；
        /// 处于【剑心通明】时威力大幅提升（"触发剑心通明状态时，下一次造成一次特殊强力攻击"）。
        /// </summary>
        public void UnleashOneThought()
        {
            if (_player == null) return;
            if (_root == null || _root.CurrentRoot != SpiritRootType.Metal) return;
            bool inSwordHeart = _status != null && _status.Has(SwordHeartEffectId);

            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 forward = _player.AimDirection;
            float radius = burstRadius * 1.5f;
            float mult = burstDamageMultiplier * (inSwordHeart ? 4f : 2f);
            float baseDamage = _player.Stats.CalculateDamage() * mult;
            LayerMask mask = ResolveEnemyLayer();

            var hits = Physics.OverlapSphere(origin, radius, mask);
            foreach (var col in hits)
            {
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform) || col.CompareTag("Player")) continue;
                Vector3 dir = (col.transform.position - origin).normalized;
                dir.y = 0;
                if (Vector3.Angle(forward, dir) > 70f) continue;
                var dmgable = col.GetComponent<IDamageable>();
                if (dmgable == null) continue;
                dmgable.OnDamage(baseDamage, col.ClosestPoint(origin), gameObject);
            }

            Color gold = new Color(1f, 0.85f, 0.2f, 1f);
            FxFactory.SpawnSliceLine(transform.position, forward, radius * 1.6f, gold, 0.5f);
            FxFactory.SpawnElementBurst(transform.position + Vector3.up * 1f, ElementTag.None, 2.5f, 0.7f);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.8f,
                Damage = 0,
                SpecialTag = inSwordHeart ? "一念刹那·通明斩！" : "一念刹那！"
            });
        }

        private void ApplySwordHeart()
        {
            if (_status == null) return;
            var def = _root != null ? _root.CurrentDef : null;
            _status.Apply(new StatusEffect
            {
                id = SwordHeartEffectId,
                isBuff = true,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = swordHeartDuration,
                modifiers = new System.Collections.Generic.List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, swordHeartAtkBonus),
                    StatModifier.Flat(StatType.CritRate, swordHeartCritBonus)
                },
                displayName = "剑心通明",
                description = $"普攻 +{swordHeartAtkBonus * 100:F0}% / 暴击率 +{swordHeartCritBonus * 100:F0}%",
                uiColor = def != null ? def.displayColor : new Color(1f, 0.85f, 0.2f)
            });
        }

        // ==================== 塑金形态（攻 / 守 切换）====================

        private void CycleGoldForm()
        {
            _goldForm = (_goldForm + 1) % 3;
            if (_status != null) { _status.Remove(FormBladeId); _status.Remove(FormArmorId); }

            Color gold = new Color(1f, 0.85f, 0.2f, 1f);
            string tag;
            if (_goldForm == 1)
            {
                _status?.Apply(new StatusEffect
                {
                    id = FormBladeId, isBuff = true, elementTag = ElementTag.None,
                    stacks = 1, maxStacks = 1, defaultDuration = -1f, duration = -1f,
                    modifiers = new System.Collections.Generic.List<StatModifier> { StatModifier.Percent(StatType.AttackDamage, bladeAtkBonus) },
                    displayName = "塑金·刃", description = $"塑金为刃：攻击 +{bladeAtkBonus * 100:F0}%", uiColor = gold
                });
                tag = "塑金·刃（攻）";
            }
            else if (_goldForm == 2)
            {
                _status?.Apply(new StatusEffect
                {
                    id = FormArmorId, isBuff = true, elementTag = ElementTag.None,
                    stacks = 1, maxStacks = 1, defaultDuration = -1f, duration = -1f,
                    displayName = "塑金·甲", description = $"塑金为甲：受伤 -{armorDmgRed * 100:F0}%", uiColor = gold
                });
                tag = "塑金·甲（守）";
            }
            else tag = "塑金·解";

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = tag
            });
        }

        private void ClearGoldForm()
        {
            _goldForm = 0;
            if (_status != null) { _status.Remove(FormBladeId); _status.Remove(FormArmorId); }
        }

        /// <summary>给 PlayerController.OnDamage 走的钩子：塑金·甲（守）形态额外减伤。</summary>
        public float ScaleIncomingDamage(float incoming)
        {
            if (_goldForm != 2) return incoming;
            return incoming * (1f - armorDmgRed);
        }

        // ==================== 磁牵：把范围内敌人拉向身前（灵压爆发前聚怪）====================

        private void MagnetPull(Vector3 origin)
        {
            LayerMask mask = ResolveEnemyLayer();
            var pulls = Physics.OverlapSphere(origin, magnetPullRadius, mask);
            foreach (var col in pulls)
            {
                if (col == null || col.CompareTag("Player")) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (col.GetComponent<IDamageable>() == null) continue;

                Vector3 to = origin - col.transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d <= 0.6f) continue;
                Vector3 step = to.normalized * Mathf.Min(d - 0.6f, magnetPullMax);
                col.transform.position += step;
            }
            FxFactory.SpawnAOERing(origin + Vector3.down * 0.6f, magnetPullRadius, new Color(0.8f, 0.85f, 1f, 0.9f), 0.3f);
        }
    }
}
