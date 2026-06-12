using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 木化身 · 持续寄生（播种 / 收割）—— v0.3.2 机制版核心机制
    ///
    /// 设计参考：GDD 4.3.2
    /// - 普攻命中敌人 → 在敌人身上叠加【寄生种子】StatusEffect
    ///     - 单敌上限 5 颗、持续 6s、每次刷新最长一颗
    ///     - 自带微量 DoT：每秒 ×0.1 玩家攻击 / 颗
    /// - 任意主动技能命中带种子敌人 → 引爆所有种子：
    ///     - 每颗 ×0.5 玩家攻击 AOE 伤害
    ///     - AOE 半径 = 1.5 + 0.3 × N（颗数）
    ///     - 消耗所有种子；AOE 命中范围内的其他敌人不连锁引爆
    /// - 敌人死亡时种子迁移给最近 1 名敌人（最多 3 颗）
    /// </summary>
    public class SpiritRootWoodController : MonoBehaviour
    {
        public const string ParasiteSeedEffectId = "Parasite_Seed";
        // v0.4 天赋节点
        private const string TalentFertileSoilId = "Talent_Wood_FertileSoil";

        [Header("种子参数")]
        [SerializeField] private float seedDuration = 6f;
        [SerializeField] private int maxSeedsPerEnemy = 5;
        [SerializeField] private float seedDpsPerStack = 0.1f;
        [SerializeField] private float seedTickInterval = 1f;

        [Header("枯荣逆旅被动")]
        [SerializeField] private int globalSeedCap = 99;
        [SerializeField] private float passiveDmgPerSeed = 0.001f; // +0.1% per seed

        [Header("引爆参数")]
        [SerializeField] private float detonateDamagePerSeed = 0.5f;
        [SerializeField] private float detonateBaseRadius = 1.5f;
        [SerializeField] private float detonateRadiusPerSeed = 0.3f;
        [SerializeField] private LayerMask enemyLayer = ~0;

        private PlayerController _player;
        private SpiritRootController _root;
        private StatusEffectController _ownStatus;

        private int _totalSeedCount;
        private const string KuRongPassiveId = "KuRong_Passive";

        public int TotalSeedCount => _totalSeedCount;
        public bool HasTalentFertileSoil => _ownStatus != null && _ownStatus.Has(TalentFertileSoilId);
        private float EffectiveSeedDuration => HasTalentFertileSoil ? seedDuration * 2f : seedDuration;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _root = GetComponent<SpiritRootController>();
            _ownStatus = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(OnSkillHit);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(OnSkillHit);
        }

        // ==================== 普攻种种子 ====================

        private void OnMeleeHit(GameEvents.MeleeHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Wood) return;
            if (evt.Target == null) return;

            ApplySeedTo(evt.Target);
        }

        private void ApplySeedTo(GameObject enemy)
        {
            if (enemy == null) return;
            if (_totalSeedCount >= globalSeedCap) return;

            var statusCtrl = enemy.GetComponent<StatusEffectController>();
            if (statusCtrl == null)
                statusCtrl = enemy.AddComponent<StatusEffectController>();

            var existing = statusCtrl.Get(ParasiteSeedEffectId);
            if (existing != null && existing.stacks >= maxSeedsPerEnemy) return;

            var seedDpsRef = _player != null ? _player.Stats.attackDamage * seedDpsPerStack : seedDpsPerStack;

            var applied = statusCtrl.Apply(new StatusEffect
            {
                id = ParasiteSeedEffectId,
                isBuff = false,
                elementTag = ElementTag.Wood,
                stacks = 1,
                maxStacks = maxSeedsPerEnemy,
                defaultDuration = EffectiveSeedDuration,
                tickInterval = seedTickInterval,
                displayName = "寄生种子",
                description = $"每秒受 {seedDpsRef:F1} 木属性持续伤害",
                uiColor = new Color(0.4f, 0.9f, 0.4f),
                onTick = OnSeedTick,
                onExpire = OnSeedExpiredTracked
            });

            _totalSeedCount++;
            RefreshKuRongPassive();

            FxFactory.RefreshHeadSeedIcons(enemy.transform, applied != null ? applied.stacks : 1, new Color(0.4f, 0.95f, 0.4f, 1f));
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = enemy.transform.position + Vector3.up * 1.8f,
                Damage = 0,
                SpecialTag = $"+1 种子（{_totalSeedCount}/{globalSeedCap}）"
            });
        }

        private void OnSeedExpiredTracked(StatusEffect eff, GameObject host)
        {
            if (host != null) FxFactory.ClearHeadSeedIcons(host.transform);
            _totalSeedCount = Mathf.Max(0, _totalSeedCount - (eff != null ? eff.stacks : 1));
            RefreshKuRongPassive();
        }

        private void RefreshKuRongPassive()
        {
            if (_ownStatus == null) return;
            _ownStatus.Remove(KuRongPassiveId);
            if (_totalSeedCount <= 0) return;
            float bonus = passiveDmgPerSeed * _totalSeedCount;
            _ownStatus.Apply(new StatusEffect
            {
                id = KuRongPassiveId,
                isBuff = true,
                elementTag = ElementTag.Wood,
                stacks = _totalSeedCount,
                maxStacks = globalSeedCap,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = new System.Collections.Generic.List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, bonus)
                },
                displayName = $"枯荣 · 蓄势",
                description = $"全局 {_totalSeedCount} 颗种子 → 攻击 +{bonus * 100:F1}%",
                uiColor = new Color(0.4f, 0.9f, 0.4f)
            });
        }

        private void OnSeedTick(StatusEffect eff, GameObject host, float dt)
        {
            if (host == null || _player == null) return;
            var dmgable = host.GetComponent<IDamageable>();
            if (dmgable == null) return;
            float dmg = _player.Stats.attackDamage * seedDpsPerStack * Mathf.Max(1, eff.stacks) * dt;
            if (dmg > 0f) dmgable.OnDamage(dmg, host.transform.position, _player.gameObject);
        }

        // ==================== 枯荣逆旅（技能 17 · AvatarSpecial）====================

        /// <summary>
        /// 主动引爆周围所有带寄生种子的敌人，每层对其造成伤害（"主动释放可以引爆种子，每层造成 X 伤害"）。
        /// 注：99 层上限 / 攻击不消耗层数 / 每层 +0.1% 的被动部分需常驻装备机制，后续补。
        /// </summary>
        public void DetonateSeeds()
        {
            if (_player == null) return;
            if (_root == null || _root.CurrentRoot != SpiritRootType.Wood) return;
            const float searchRadius = 12f;
            var hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayer);
            int detonated = 0;
            foreach (var col in hits)
            {
                if (col == null || col.CompareTag("Player")) continue;
                var sc = col.GetComponent<StatusEffectController>();
                if (sc == null) continue;
                var seed = sc.Get(ParasiteSeedEffectId);
                if (seed == null || seed.stacks <= 0) continue;

                int n = seed.stacks;
                float dmg = _player.Stats.attackDamage * detonateDamagePerSeed * n;
                var d = col.GetComponent<IDamageable>();
                if (d != null) d.OnDamage(dmg, col.transform.position, _player.gameObject);
                _totalSeedCount = Mathf.Max(0, _totalSeedCount - n);
                sc.Remove(ParasiteSeedEffectId);
                FxFactory.ClearHeadSeedIcons(col.transform);
                FxFactory.SpawnElementBurst(col.transform.position + Vector3.up * 0.5f, ElementTag.Wood, 1.5f + 0.2f * n, 0.5f);
                detonated++;
            }
            RefreshKuRongPassive();
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = detonated > 0 ? $"枯荣逆旅·引爆 ×{detonated}" : "枯荣逆旅（无种子）"
            });
        }

        // ==================== 技能引爆 ====================

        private void OnSkillHit(GameEvents.SkillHitConnected evt)
        {
            if (_root == null || _root.CurrentRoot != SpiritRootType.Wood) return;
            if (evt.Target == null) return;

            var statusCtrl = evt.Target.GetComponent<StatusEffectController>();
            if (statusCtrl == null) return;
            var seed = statusCtrl.Get(ParasiteSeedEffectId);
            if (seed == null || seed.stacks <= 0) return;

            int seedCount = seed.stacks;
            float radius = detonateBaseRadius + detonateRadiusPerSeed * seedCount;
            float damage = _player.Stats.attackDamage * detonateDamagePerSeed * seedCount;

            var hits = Physics.OverlapSphere(evt.HitPoint, radius, enemyLayer);
            foreach (var col in hits)
            {
                var d = col.GetComponent<IDamageable>();
                if (d != null)
                    d.OnDamage(damage, col.transform.position, _player != null ? _player.gameObject : gameObject);
            }

            // 枯荣逆旅被动：技能命中不消耗种子，只造成额外伤害
            // 种子由自然过期或主动引爆（DetonateSeeds）消耗

            Color woodColor = new Color(0.4f, 0.95f, 0.4f, 1f);
            FxFactory.SpawnAOERing(evt.HitPoint + Vector3.up * 0.05f, radius, woodColor, 0.6f);
            FxFactory.SpawnElementBurst(evt.HitPoint, ElementTag.Wood, radius * 0.8f, 0.55f);

            GameEvents.Publish(new GameEvents.ParasiteSeedDetonated
            {
                Position = evt.HitPoint,
                SeedCount = seedCount,
                ExplosionRadius = radius
            });

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = evt.HitPoint + Vector3.up * 1.5f,
                Damage = damage,
                SpecialTag = $"种子×{seedCount} 共鸣"
            });
        }
    }
}
