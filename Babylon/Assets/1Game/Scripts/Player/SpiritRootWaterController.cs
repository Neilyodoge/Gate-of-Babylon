using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 水化身 · 影息斩（位移即输出） —— v0.3.2 / v0.4 落地
    ///
    /// 设计参考：GDD 4.3.3
    /// - 闪避结束后 0.4s 内的"下一次普攻命中" → 升级为【影息斩】：
    ///     - 伤害 ×2
    ///     - 短暂前冲位移（自动锁向命中目标）
    ///     - 视觉：淡蓝色水痕拖尾
    /// - 影息斩命中目标 → 在该目标身上施加【水痕印】（5s，单敌 1 层，每次刷新时长）
    /// - 玩家释放任意主动技能命中带水痕的目标 → 技能伤害 ×1.5 + 消耗水痕
    ///
    /// 战斗循环：闪避 → 影息斩"标记目标" → 技能集中爆破 → 闪避换位 → 标记下一个
    /// 仅在 CurrentRoot == Water 时激活。
    /// </summary>
    public class SpiritRootWaterController : MonoBehaviour
    {
        public const string WaterMarkEffectId = "Water_Mark";
        // v0.4 天赋节点
        private const string TalentDoubleShadowId = "Talent_Water_DoubleShadow";

        [Header("影息蓄势窗口")]
        [SerializeField] private float shadowWindowDuration = 0.4f;

        [Header("影息斩参数")]
        [SerializeField] private float shadowDamageBonus = 1f;     // 总伤害 ×(1 + bonus)，1 = ×2
        [SerializeField] private float shadowLungeDistance = 4f;

        [Header("水痕印")]
        [SerializeField] private float waterMarkDuration = 5f;
        [SerializeField] private float skillDamageBonusOnMarked = 0.5f;  // ×1.5

        [Header("息影瞬步（技能 18 · AvatarSpecial）")]
        [SerializeField] private float shadowStepDuration = 5f;
        [SerializeField] private float shadowStepPathDamageRatio = 1f;   // 路径伤害 = 攻击 × 此
        [SerializeField] private float shadowStepPathLength = 5f;
        private float _shadowStepTimer = 0f;
        private GameObject _shadowAura;
        /// <summary>息影瞬步进行中：闪避无冷却（由 PlayerController 读取）。</summary>
        public static bool ShadowStepActive { get; private set; }

        private PlayerController _player;
        private SpiritRootController _root;
        private StatusEffectController _ownStatus;

        // 运行时状态
        private float _shadowWindowRemaining = 0f;
        public bool IsShadowStrikeReady => _shadowWindowRemaining > 0f;
        public float ShadowWindowProgress => shadowWindowDuration > 0f ? _shadowWindowRemaining / shadowWindowDuration : 0f;
        public bool HasTalentDoubleShadow => _ownStatus != null && _ownStatus.Has(TalentDoubleShadowId);

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _root = GetComponent<SpiritRootController>();
            _ownStatus = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.DodgeFinished>(OnDodge);
            GameEvents.Subscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(OnSkillHit);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.DodgeFinished>(OnDodge);
            GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(OnSkillHit);
            ShadowStepActive = false;
            _shadowStepTimer = 0f;
            DestroyShadowAura();
        }

        private void Update()
        {
            if (_shadowWindowRemaining > 0f)
                _shadowWindowRemaining = Mathf.Max(0f, _shadowWindowRemaining - Time.deltaTime);

            if (_shadowStepTimer > 0f)
            {
                _shadowStepTimer -= Time.deltaTime;
                if (_shadowStepTimer <= 0f)
                {
                    ShadowStepActive = false;
                    DestroyShadowAura();
                }
                else if (_shadowAura != null)
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
                    var lr = _shadowAura.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        Color c = new Color(0.3f, 0.5f, 1f, pulse * 0.6f);
                        lr.startColor = c; lr.endColor = c;
                    }
                }
            }
        }

        /// <summary>
        /// 息影瞬步（技能 18）：持续 5 秒，期间闪避无冷却（PlayerController 读 <see cref="ShadowStepActive"/>），
        /// 且闪避路径上的敌人受到伤害（见 <see cref="OnDodge"/>）。
        /// </summary>
        public void EnterShadowStep()
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Water) return;
            _shadowStepTimer = shadowStepDuration;
            ShadowStepActive = true;
            EnsureShadowAura();
            FxFactory.SpawnElementBurst(transform.position + Vector3.up * 0.5f, ElementTag.Water, 2.5f, 0.6f);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = "息影瞬步！"
            });
        }


        // ==================== 闪避后开窗口 ====================

        private void OnDodge(GameEvents.DodgeFinished evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Water) return;
            _shadowWindowRemaining = shadowWindowDuration;

            // 视觉：玩家脚下出一道浅蓝色 AOE 圆环作为"蓄势"提示
            Color water = new Color(0.3f, 0.7f, 1f, 1f);
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, 1.6f, water, 0.4f);

            // 息影瞬步：闪避路径上的敌人受到伤害
            if (ShadowStepActive && _player != null)
            {
                Vector3 dir = evt.EndDirection.sqrMagnitude > 0.01f ? evt.EndDirection.normalized : transform.forward;
                Vector3 end = evt.EndPosition;
                Vector3 start = end - dir * shadowStepPathLength;
                LayerMask mask = ResolveShadowMask();
                var hits = Physics.OverlapCapsule(start + Vector3.up * 0.5f, end + Vector3.up * 0.5f, 1.2f, mask);
                foreach (var c in hits)
                {
                    if (c == null || c.CompareTag("Player")) continue;
                    var d = c.GetComponent<IDamageable>();
                    if (d != null)
                    {
                        float tDef = d.Stats != null ? d.Stats.defense : 0f;
                        var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, shadowStepPathDamageRatio);
                        d.OnDamage(dmg, c.transform.position, _player.gameObject);
                    }
                }
                FxFactory.SpawnSliceLine(start, dir, shadowStepPathLength, water, 0.3f);
            }
        }

        private LayerMask ResolveShadowMask()
        {
            var pc = GetComponent<PlayerCombat>();
            if (pc != null && pc.EnemyLayer.value != 0) return pc.EnemyLayer;
            return ~0;
        }

        // ==================== 影息斩触发 ====================

        private void OnMeleeHit(GameEvents.MeleeHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Water) return;
            if (_shadowWindowRemaining <= 0f) return;
            if (evt.Target == null) return;

            // 消耗窗口，触发影息斩
            _shadowWindowRemaining = 0f;
            TriggerShadowStrike(evt.Target, evt.HitPoint);
        }

        private void TriggerShadowStrike(GameObject target, Vector3 hitPoint)
        {
            if (target == null || _player == null) return;

            // 影息斩额外伤害（与普攻本体的伤害叠加：本次命中实际是 1x + bonus x 普攻）
            var dmgable = target.GetComponent<IDamageable>();
            if (dmgable != null)
            {
                float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                var (bonusDmg, _) = _player.Stats.CalcMeleeDamage(tDef);
                bonusDmg *= shadowDamageBonus;
                dmgable.OnDamage(bonusDmg, hitPoint, gameObject);

                if (HasTalentDoubleShadow)
                {
                    var (splash, _s) = _player.Stats.CalcMeleeDamage(tDef);
                    splash *= 0.5f;
                    dmgable.OnDamage(splash, hitPoint, gameObject);
                    GameEvents.Publish(new GameEvents.DamageNumberRequested
                    {
                        WorldPosition = hitPoint + Vector3.up * 2.0f,
                        Damage = splash,
                        SpecialTag = "双影"
                    });
                }
            }

            // 在目标身上施加水痕印
            ApplyWaterMark(target);

            // 前冲位移：朝命中目标方向短距冲刺（仅当不在 dash 中）
            if (_player != null && !_player.IsDashing)
            {
                Vector3 dir = (target.transform.position - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Vector3 newPos = transform.position + dir.normalized * shadowLungeDistance;
                    // 简单瞬移（不走 dash 状态，避免冲突）
                    _player.transform.position = newPos;
                }
            }

            // 视觉：水痕拖尾（从原位置到命中点画一道淡蓝线 + 命中处水球爆发）
            Color water = new Color(0.3f, 0.7f, 1f, 1f);
            FxFactory.SpawnSliceLine(transform.position - (target.transform.position - transform.position).normalized * 2f,
                (target.transform.position - transform.position).normalized,
                shadowLungeDistance + 2f,
                water,
                0.35f);
            FxFactory.SpawnElementBurst(hitPoint, ElementTag.Water, 1.6f, 0.5f);

            // 飘字
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = "影息斩！"
            });

            GameEvents.Publish(new GameEvents.ShadowStrikeTriggered
            {
                HitPoint = hitPoint,
                Target = target,
                DamageDealt = _player.Stats.CalcMeleeDamage(0f).damage * (1f + shadowDamageBonus)
            });
        }

        private void ApplyWaterMark(GameObject enemy)
        {
            var ctrl = enemy.GetComponent<StatusEffectController>();
            if (ctrl == null) ctrl = enemy.AddComponent<StatusEffectController>();

            ctrl.Apply(new StatusEffect
            {
                id = WaterMarkEffectId,
                isBuff = false,
                elementTag = ElementTag.Water,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = waterMarkDuration,
                displayName = "水痕印",
                description = $"被影息斩标记 — 技能命中此目标 ×{1 + skillDamageBonusOnMarked:F1} 伤害",
                uiColor = new Color(0.3f, 0.7f, 1f, 1f),
                onExpire = OnWaterMarkExpired
            });

            // 视觉：敌人头顶飘一颗水珠（用 FxFactory 头顶图标）
            FxFactory.RefreshHeadSeedIcons(enemy.transform, 1, new Color(0.3f, 0.7f, 1f, 1f), 2.0f);
        }

        private void OnWaterMarkExpired(StatusEffect eff, GameObject host)
        {
            if (host != null) FxFactory.ClearHeadSeedIcons(host.transform);
        }

        // ==================== 技能命中带水痕目标 ×1.5 ====================

        private void OnSkillHit(GameEvents.SkillHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Water) return;
            if (evt.Target == null) return;

            var ctrl = evt.Target.GetComponent<StatusEffectController>();
            if (ctrl == null) return;
            var mark = ctrl.Get(WaterMarkEffectId);
            if (mark == null) return;

            // 已经命中过的目标，水痕被消耗
            ctrl.Remove(WaterMarkEffectId);
            FxFactory.ClearHeadSeedIcons(evt.Target.transform);

            // 追加 ×0.5 攻击力的额外伤害（相当于把技能本体的伤害放大到 ×1.5）
            var dmgable = evt.Target.GetComponent<IDamageable>();
            if (dmgable != null && _player != null)
            {
                float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                var (bonus, _) = _player.Stats.CalcSkillDamage(tDef, skillDamageBonusOnMarked);
                dmgable.OnDamage(bonus, evt.HitPoint, _player.gameObject);
            }

            // 视觉：命中处浅蓝色 AOE 圆环 + 飘字
            FxFactory.SpawnAOERing(evt.HitPoint + Vector3.up * 0.05f, 2.5f, new Color(0.3f, 0.7f, 1f, 1f), 0.4f);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = evt.HitPoint + Vector3.up * 1.6f,
                Damage = 0,
                SpecialTag = "水痕收割 ×1.5"
            });
        }

        // ==================== 息影光环 ====================

        private void EnsureShadowAura()
        {
            if (_shadowAura != null) return;
            _shadowAura = new GameObject("ShadowStep_Aura");
            _shadowAura.transform.SetParent(transform);
            _shadowAura.transform.localPosition = Vector3.up * 0.05f;
            var lr = _shadowAura.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 24;
            lr.widthMultiplier = 0.1f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Color c = new Color(0.3f, 0.5f, 1f, 0.6f);
            lr.startColor = c; lr.endColor = c;
            for (int i = 0; i < 24; i++)
            {
                float ang = i / 24f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * 1f, 0f, Mathf.Sin(ang) * 1f));
            }
        }

        private void DestroyShadowAura()
        {
            if (_shadowAura != null) { Destroy(_shadowAura); _shadowAura = null; }
        }
    }
}
