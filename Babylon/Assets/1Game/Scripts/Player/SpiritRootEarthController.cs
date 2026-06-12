using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 土化身 · 山岳承负（v0.5 Week 7 重设计）
    ///
    /// 在 <see cref="SpiritRootController"/> 现有"地脉护盾（每 5 件灵物 1 层）"被动之上，
    /// 新增两套主动循环 + 全面视觉强化：
    ///
    /// 1. <b>地脉烙印 (EarthSigil)</b>
    ///    - 普攻命中敌人 → 该敌人脚下印一层"土印"（最多 5 层）
    ///    - 任意主动技能命中带烙印的敌人 → 镇压：
    ///       - 真伤 = 玩家攻击 × 0.6 × 层数
    ///       - 定身（ApplyFreeze）= 0.8s + 0.4s × 层数（5 层最高 2.8s）
    ///       - 1.8m AOE 内其他敌人各 +1 层（连锁）
    ///    - 视觉：敌人脚下旋转土黄圆盘 + N 圈内嵌（CaveVfx.SpawnEarthSigil）
    ///
    /// 2. <b>扎根 (Rooted Stance)</b>
    ///    - 玩家站立不动 ≥1.5s → 进入扎根 BUFF：攻击 +25% / 减伤 +30% / 移速 -50%
    ///    - 一旦移动立即解除
    ///    - 视觉：脚下展开八卦阵（CaveVfx.SpawnBaguaRune）+ 4 块上飘土块 + 周身土黄气场
    ///
    /// 3. <b>地脉护盾视觉强化</b>
    ///    - 旧机制（status 层数）保留在 <see cref="SpiritRootController"/>
    ///    - 本 Controller 监听护盾层数变化 → 围绕玩家旋转 N 块岩石板（每层一块）
    ///    - 破盾时（EarthShieldStackConsumed）→ 对应那块岩石板炸裂飞散
    ///
    /// 仅在 CurrentRoot == Earth 时激活；其它化身挂这个组件不会产生任何效果。
    /// </summary>
    public class SpiritRootEarthController : MonoBehaviour
    {
        // ============================ 平衡常量 ============================
        // 烙印
        private const int SigilMaxStacks = 5;
        private const float SigilLifetimeBase = 4f;
        private const float SigilLifetimeRooted = 6f;
        private const float SigilDetonateRatio = 0.6f;     // 真伤 = 攻击 × 0.6 × 层数
        private const float SigilDetonateRadius = 1.8f;
        private const float SigilStunBase = 0.8f;
        private const float SigilStunPerStack = 0.4f;
        private const int SigilChainStacks = 1;

        // 坐镇聚灵（v0.6 重写：站立蓄势 → 指挥官姿态，强化召物而非自身输出）
        private const float RootedThreshold = 1.2f;      // 站立 1.2s 触发坐镇
        private const float RootedDmgRedBonus = 0.40f;   // 坐镇时减伤（土·更肉）
        private const float RootedMoveSpeedMul = -0.50f;
        private const float RootedPuppetDmgMul = 1.6f;   // 坐镇时土傀增伤
        private const string RootedEffectId = "Root_EarthRooted";
        private const float StillVelocityThreshold = 0.08f; // 速度小于此值认为"站立"

        [Header("烙印 / 扎根（土化身重设计 · v0.5 Week 7）")]
        [SerializeField] private LayerMask enemyLayerOverride = 0;

        private PlayerController _player;
        private SpiritRootController _root;
        private StatusEffectController _status;
        private CharacterController _cc;

        // 扎根运行时
        private float _stillTimer;
        private bool _isRooted;
        private Vector3 _lastPos;
        private GameObject _rootedVfxRoot;

        // 护盾视觉
        private EarthShieldVfx _shieldVfx;
        private int _lastShieldStacks = -1;

        public bool IsRooted => _isRooted;

        // 兵阵合一（技能 19）运行时
        private readonly System.Collections.Generic.List<GameObject> _puppetTurrets = new System.Collections.Generic.List<GameObject>();
        [SerializeField] private int puppetArrayCount = 5;
        [SerializeField] private float puppetArrayDuration = 12f;
        [SerializeField] private float puppetArrayRadius = 3.5f;

        // v0.6 召物重心：被动自律土傀
        private readonly System.Collections.Generic.List<GameObject> _passivePuppets = new System.Collections.Generic.List<GameObject>();
        [SerializeField] private int passivePuppetMax = 2;
        [SerializeField] private float passivePuppetLife = 10f;
        [SerializeField] private float passiveSummonInterval = 4f;
        // 坐镇聚灵时：召唤更快、上限 +1（指挥官姿态强化召物）
        [SerializeField] private int rootedExtraPuppet = 1;
        [SerializeField] private float passiveSummonIntervalRooted = 2f;
        private float _passiveSummonTimer;

        /// <summary>
        /// 兵阵合一（技能 19）：成阵召唤 5 座土傀儡炮台（原地 AOE 炮击）；再次释放则撤阵。
        /// （注：当前土化身无常驻傀儡系统，此为自洽的"炮阵"实现，对应"炮击模式 + AOE"。）
        /// </summary>
        public void TogglePuppetArrayMode()
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Earth) return;

            // 已有阵 → 撤阵
            _puppetTurrets.RemoveAll(t => t == null);
            if (_puppetTurrets.Count > 0)
            {
                foreach (var t in _puppetTurrets) if (t != null) Destroy(t);
                _puppetTurrets.Clear();
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 2.6f,
                    Damage = 0,
                    SpecialTag = "兵阵·撤阵"
                });
                return;
            }

            LayerMask mask = enemyLayerOverride.value != 0 ? enemyLayerOverride : ResolvePuppetMask();
            for (int i = 0; i < puppetArrayCount; i++)
            {
                float ang = (360f / puppetArrayCount) * i * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * puppetArrayRadius;
                var go = new GameObject($"EarthPuppetTurret_{i}");
                go.transform.position = pos;
                go.AddComponent<EarthPuppetTurret>().Init(_player, mask, puppetArrayDuration);
                _puppetTurrets.Add(go);
            }
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = $"兵阵合一！土傀儡炮阵 ×{puppetArrayCount}"
            });
        }

        private LayerMask ResolvePuppetMask()
        {
            var pc = GetComponent<PlayerCombat>();
            if (pc != null && pc.EnemyLayer.value != 0) return pc.EnemyLayer;
            return ~0;
        }

        // ============================ 生命周期 ============================

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _root = GetComponent<SpiritRootController>();
            _status = GetComponent<StatusEffectController>();
            _cc = GetComponent<CharacterController>();
            _lastPos = transform.position;
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            GameEvents.Subscribe<GameEvents.EarthShieldStackConsumed>(OnShieldConsumed);
            GameEvents.Subscribe<GameEvents.SkillCastStarted>(OnPlayerSkillCast);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            GameEvents.Unsubscribe<GameEvents.EarthShieldStackConsumed>(OnShieldConsumed);
            GameEvents.Unsubscribe<GameEvents.SkillCastStarted>(OnPlayerSkillCast);

            // 失活时收一下视觉 + 复位土傀增伤
            EarthPuppetTurret.GlobalDamageMul = 1f;
            ClearRootedVfx();
            ClearShieldVfx();
        }

        private void Update()
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Earth)
            {
                // 非土化身：保险，确保视觉清空
                if (_isRooted) ExitRooted();
                if (_shieldVfx != null) ClearShieldVfx();
                return;
            }

            TickRooted();
            TickShieldVfx();
            TickPuppets();   // v0.6 召物重心：维持自律土傀
        }

        // 被动召物：附近有敌时维持最多 N 个自律土傀（御物的"召物"底子）
        private void TickPuppets()
        {
            _passivePuppets.RemoveAll(p => p == null);
            int maxPuppets = _isRooted ? passivePuppetMax + rootedExtraPuppet : passivePuppetMax;
            if (_passivePuppets.Count >= maxPuppets) return;
            _passiveSummonTimer -= Time.deltaTime;
            if (_passiveSummonTimer > 0f) return;
            _passiveSummonTimer = _isRooted ? passiveSummonIntervalRooted : passiveSummonInterval;
            if (_player == null) return;

            LayerMask mask = enemyLayerOverride.value != 0 ? enemyLayerOverride : ResolvePuppetMask();
            if (Physics.OverlapSphere(_player.transform.position, 12f, mask).Length == 0) return;

            Vector3 pos = _player.transform.position + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
            var go = new GameObject("EarthPuppet_passive");
            go.transform.position = pos;
            go.AddComponent<EarthPuppetTurret>().Init(_player, mask, passivePuppetLife);
            _passivePuppets.Add(go);
        }

        // ============================ GDD §4.3.5 / §6.7.4 技能同步 ============================
        // 玩家释放 Q/E/R → 所有活跃土傀立即跟随开火一次（伤害 = 玩家攻击 × SyncDmgRatio）
        private const float SyncDmgRatio = 0.10f;

        private void OnPlayerSkillCast(GameEvents.SkillCastStarted evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Earth) return;
            if (_player == null) return;

            _passivePuppets.RemoveAll(p => p == null);
            _puppetTurrets.RemoveAll(p => p == null);

            float dmg = _player.Stats.attackDamage * SyncDmgRatio * Mathf.Max(0.1f, EarthPuppetTurret.GlobalDamageMul);
            int syncCount = 0;

            foreach (var go in _passivePuppets)
            {
                if (go == null) continue;
                var turret = go.GetComponent<EarthPuppetTurret>();
                if (turret != null) { turret.SyncFire(dmg); syncCount++; }
            }
            foreach (var go in _puppetTurrets)
            {
                if (go == null) continue;
                var turret = go.GetComponent<EarthPuppetTurret>();
                if (turret != null) { turret.SyncFire(dmg); syncCount++; }
            }

            if (syncCount > 0)
            {
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 2.2f,
                    Damage = 0,
                    SpecialTag = $"傀儡同步 ×{syncCount}"
                });
            }
        }

        // ============================ 烙印 [v0.6 已移除] ============================
        // 地脉烙印（普攻叠印→技能引爆）与青囊"寄生种子"撞车，已收敛为青囊专属。
        // 御物重心转为"召物·自律土傀"（见 TickPuppets）。下列 DetonateSigils 等保留为死代码，待清理。

        private void OnMeleeHit(GameEvents.MeleeHitConnected evt) { }
        private void OnSkillHit(GameEvents.SkillHitConnected evt) { }

        private void DetonateSigils(GameObject target, Vector3 hitPoint, int stacks)
        {
            if (target == null || _player == null) return;

            // 1) 清掉中心目标的烙印
            EarthSigil.ClearStacks(target);

            // 2) 中心目标承伤 + 定身
            float dmg = _player.Stats.attackDamage * SigilDetonateRatio * stacks;
            float stun = SigilStunBase + SigilStunPerStack * stacks;

            var dmgable = target.GetComponent<IDamageable>();
            if (dmgable != null)
            {
                dmgable.OnDamage(dmg, hitPoint, _player.gameObject);
            }
            TryApplyEarthStun(target, stun);

            // 3) AOE 内其他敌人：50% 伤害 + 定身 0.6s + 连锁 +1 层
            LayerMask mask = ResolveEnemyLayer();
            var hits = Physics.OverlapSphere(target.transform.position, SigilDetonateRadius, mask);
            int affected = 1;
            foreach (var col in hits)
            {
                if (col == null) continue;
                if (col.gameObject == target) continue;
                if (col.CompareTag("Player")) continue;
                var d = col.GetComponent<IDamageable>();
                if (d == null || d.Stats == null || !d.Stats.IsAlive) continue;

                d.OnDamage(dmg * 0.5f, col.transform.position, _player.gameObject);
                TryApplyEarthStun(col.gameObject, 0.6f);
                EarthSigil.AddStacks(col.gameObject, SigilChainStacks, _isRooted);
                affected++;
            }

            // 4) 视觉：地面冲击波 + 元素爆发 + 8 道朝外土黄线 + 镜头中震
            Color earthColor = FxFactory.ElementColor(ElementTag.Earth);
            FxFactory.SpawnAOERing(target.transform.position + Vector3.up * 0.05f,
                SigilDetonateRadius, earthColor, lifetime: 0.55f);
            FxFactory.SpawnElementBurst(target.transform.position + Vector3.up * 0.6f,
                ElementTag.Earth, SigilDetonateRadius * 0.8f, 0.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a));
                FxFactory.SpawnSliceLine(target.transform.position + Vector3.up * 0.3f,
                    dir, SigilDetonateRadius * 1.1f, earthColor, 0.4f);
            }
            CameraShake.TriggerMedium();

            GameEvents.Publish(new GameEvents.EarthSigilDetonated
            {
                Position = target.transform.position,
                StacksConsumed = stacks,
                EnemiesAffected = affected
            });
        }

        private void TryApplyEarthStun(GameObject enemy, float duration)
        {
            if (enemy == null || duration <= 0f) return;
            var eb = enemy.GetComponent<EnemyBase>();
            if (eb != null)
            {
                eb.ApplyFreeze(duration);
                return;
            }
            // 降级：挂减速 status
            var ec = enemy.GetComponent<StatusEffectController>();
            if (ec == null) ec = enemy.AddComponent<StatusEffectController>();
            ec.Apply(new StatusEffect
            {
                id = "Stun_EarthSigil",
                isBuff = false,
                elementTag = ElementTag.Earth,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = duration,
                modifiers = new List<StatModifier> { StatModifier.Percent(StatType.MoveSpeed, -1f) },
                displayName = "镇压",
                description = "被地脉烙印镇压，无法移动",
                uiColor = FxFactory.ElementColor(ElementTag.Earth)
            });
        }

        // ============================ 扎根状态 ============================

        private void TickRooted()
        {
            // 估算这一帧玩家的移动速度
            float vel = (_cc != null) ? _cc.velocity.magnitude
                                      : (transform.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPos = transform.position;

            if (vel < StillVelocityThreshold)
            {
                _stillTimer += Time.deltaTime;
                if (!_isRooted && _stillTimer >= RootedThreshold)
                {
                    EnterRooted();
                }
            }
            else
            {
                _stillTimer = 0f;
                if (_isRooted) ExitRooted();
            }
        }

        private void EnterRooted()
        {
            _isRooted = true;
            if (_status != null)
            {
                _status.Apply(new StatusEffect
                {
                    id = RootedEffectId,
                    isBuff = true,
                    elementTag = ElementTag.Earth,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = -1f,
                    duration = -1f,
                    modifiers = new List<StatModifier>
                    {
                        StatModifier.Percent(StatType.MoveSpeed, RootedMoveSpeedMul),
                    },
                    displayName = "御物 · 坐镇聚灵",
                    description = $"减伤 +{RootedDmgRedBonus * 100:F0}% · 土傀增伤 +{(RootedPuppetDmgMul - 1f) * 100:F0}% · 召唤加速 · 移速 -50%",
                    uiColor = FxFactory.ElementColor(ElementTag.Earth)
                });
                // 减伤不能通过 StatModifier 直接做（CombatStats.damageReduction 是 0~1 clamp 字段），
                // 所以由 PlayerController.OnDamage 走 IsRooted 钩子读，本 Controller 暴露 IsRooted。
            }

            // 坐镇聚灵：强化全体自律土傀的炮击伤害
            EarthPuppetTurret.GlobalDamageMul = RootedPuppetDmgMul;
            // 立即重置召唤计时，让坐镇瞬间就能加速补傀
            _passiveSummonTimer = 0f;

            BuildRootedVfx();
            CameraShake.TriggerLight();

            GameEvents.Publish(new GameEvents.EarthRootedStateChanged
            {
                IsRooted = true,
                AttackBonus = 0f,
                DamageReduction = RootedDmgRedBonus
            });
        }

        private void ExitRooted()
        {
            _isRooted = false;
            _stillTimer = 0f;
            EarthPuppetTurret.GlobalDamageMul = 1f;   // 解除坐镇 → 土傀恢复常规伤害
            if (_status != null) _status.Remove(RootedEffectId);
            ClearRootedVfx();

            GameEvents.Publish(new GameEvents.EarthRootedStateChanged
            {
                IsRooted = false, AttackBonus = 0f, DamageReduction = 0f
            });
        }

        /// <summary>给 PlayerController.OnDamage 走的钩子：在扎根状态下额外减 30% 伤害</summary>
        public float ScaleIncomingDamage(float incoming)
        {
            if (!_isRooted) return incoming;
            return incoming * (1f - RootedDmgRedBonus);
        }

        // ============================ 扎根视觉 ============================

        private void BuildRootedVfx()
        {
            if (_rootedVfxRoot != null) ClearRootedVfx();

            _rootedVfxRoot = new GameObject("__RootedVfx");
            _rootedVfxRoot.transform.SetParent(transform, false);
            _rootedVfxRoot.transform.localPosition = Vector3.zero;

            Color earthColor = FxFactory.ElementColor(ElementTag.Earth);

            // 1) 脚下八卦阵
            CaveVfx.SpawnBaguaRune(_rootedVfxRoot.transform, Vector3.zero, 1.6f,
                earthColor, lineWidth: 0.08f);

            // 2) 4 块上飘的小土块
            CaveVfx.SpawnOrbitingParticles(_rootedVfxRoot.transform,
                Vector3.zero, count: 4, orbitRadius: 1.4f, orbitHeight: 0.3f,
                particleSize: 0.18f, color: earthColor,
                orbitSpeed: 22f, verticalBob: 0.4f);

            // 3) 周身土黄淡气场（用一颗大半透球作为氛围）
            var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aura.name = "Aura";
            aura.transform.SetParent(_rootedVfxRoot.transform, false);
            aura.transform.localPosition = Vector3.up * 1f;
            aura.transform.localScale = Vector3.one * 2.6f;
            var aCol = aura.GetComponent<Collider>();
            if (aCol != null) Destroy(aCol);
            var aRend = aura.GetComponent<Renderer>();
            if (aRend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(new Color(earthColor.r, earthColor.g, earthColor.b, 0.10f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", earthColor * 0.7f);
                }
                aRend.material = mat;
                aRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 4) 从地面飘起的 4 缕土黄烟雾（在玩家周围 4 个方位）
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 1.1f, 0f, Mathf.Sin(a) * 1.1f);
                CaveVfx.SpawnSmokeEmitter(_rootedVfxRoot.transform, p,
                    color: earthColor, particleSize: 0.20f, spawnInterval: 0.35f,
                    riseSpeed: 0.7f, lifetime: 1.3f, jitterRadius: 0.15f);
            }

            // 5) 进入瞬间脚下来一发大爆环
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.04f, 2.0f, earthColor, 0.55f);
        }

        private void ClearRootedVfx()
        {
            if (_rootedVfxRoot != null)
            {
                Destroy(_rootedVfxRoot);
                _rootedVfxRoot = null;
            }
        }

        // ============================ 护盾视觉 ============================

        private void TickShieldVfx()
        {
            // 监听 status 上的 EarthShield 层数
            if (_status == null) return;
            var eff = _status.Get("Root_EarthShield");
            int stacks = eff != null ? eff.stacks : 0;
            if (stacks != _lastShieldStacks)
            {
                _lastShieldStacks = stacks;
                if (stacks <= 0) ClearShieldVfx();
                else EnsureShieldVfx(stacks);
            }
        }

        private void EnsureShieldVfx(int stacks)
        {
            if (_shieldVfx == null)
            {
                var go = new GameObject("__EarthShieldVfx");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 1.0f;
                _shieldVfx = go.AddComponent<EarthShieldVfx>();
                _shieldVfx.Init(FxFactory.ElementColor(ElementTag.Earth));
            }
            _shieldVfx.SetStackCount(stacks);
        }

        private void ClearShieldVfx()
        {
            if (_shieldVfx != null)
            {
                Destroy(_shieldVfx.gameObject);
                _shieldVfx = null;
            }
            _lastShieldStacks = 0;
        }

        private void OnShieldConsumed(GameEvents.EarthShieldStackConsumed evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Earth) return;
            // 让视觉炸掉"最外一块"岩石板
            if (_shieldVfx != null) _shieldVfx.ShatterOuterMost();
            // 同步重设层数（应对外部源外加 1 层等场景）
            if (evt.StacksRemaining <= 0)
            {
                ClearShieldVfx();
            }
            else
            {
                EnsureShieldVfx(evt.StacksRemaining);
            }
            // 同步缓存避免下一帧 TickShieldVfx 再做一次（导致空 ensure）
            _lastShieldStacks = evt.StacksRemaining;
        }

        // ============================ 辅助 ============================

        private LayerMask ResolveEnemyLayer()
        {
            if (enemyLayerOverride.value != 0) return enemyLayerOverride;
            var pc = GetComponent<PlayerCombat>();
            if (pc != null && pc.EnemyLayer.value != 0) return pc.EnemyLayer;
            return ~0;
        }
    }
}
