using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// P1 状态型触发器「种子生成 / 种子引爆」的世界状态载体。
    ///
    /// 种子 = 世界坐标上的无伤害标记，带落点 / 存续时间 / 数量上限：
    /// - <see cref="TriggerType.SeedPlant"/> 触发器：核心技能 / 普攻命中时 <see cref="Plant"/> 一颗种子。
    /// - <see cref="TriggerType.SeedDetonate"/> 触发器：场上有种子即可 Proc，消费时 <see cref="DetonateAll"/>
    ///   在每颗种子位置触发接入效果器的伤害 / 元素 / 状态（种子本身不造成伤害）。
    ///
    /// 运行时按需创建（<see cref="Ensure"/>），随场景卸载自然销毁——不跨场景常驻。
    /// </summary>
    public class SeedSystem : MonoBehaviour
    {
        public static SeedSystem Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        // ── 调参常量 ──
        public const int MaxSeeds = 16;                 // 世界种子上限（超出替换最旧）
        public const float SeedDuration = 8f;           // 单颗种子存续秒数
        public const float MergeDistance = 1.0f;        // 落点合并距离：靠近已有种子则刷新时长而非新增
        public const float DefaultDetonateRadius = 3f;  // 效果器未提供范围时的引爆半径

        private class Seed
        {
            public Vector3 pos;
            public float expireAt;
            public GameObject marker;
        }

        private readonly List<Seed> _seeds = new List<Seed>();

        /// <summary>当前世界种子数量。</summary>
        public int ActiveCount => _seeds.Count;

        public static SeedSystem Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("[SeedSystem]");
                Instance = go.AddComponent<SeedSystem>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            ClearMarkers();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _seeds.Count - 1; i >= 0; i--)
            {
                if (now >= _seeds[i].expireAt)
                {
                    if (_seeds[i].marker != null) Destroy(_seeds[i].marker);
                    _seeds.RemoveAt(i);
                }
            }
        }

        /// <summary>种下一颗种子。靠近已有种子则刷新其存续时间；满上限则替换最旧。element 仅决定标记颜色。</summary>
        public void Plant(Vector3 pos, ElementTag element)
        {
            pos.y = Mathf.Max(0.1f, pos.y);

            for (int i = 0; i < _seeds.Count; i++)
            {
                if ((_seeds[i].pos - pos).sqrMagnitude <= MergeDistance * MergeDistance)
                {
                    _seeds[i].expireAt = Time.time + SeedDuration;
                    return;
                }
            }

            if (_seeds.Count >= MaxSeeds)
            {
                int oldest = 0;
                for (int i = 1; i < _seeds.Count; i++)
                    if (_seeds[i].expireAt < _seeds[oldest].expireAt) oldest = i;
                if (_seeds[oldest].marker != null) Destroy(_seeds[oldest].marker);
                _seeds.RemoveAt(oldest);
            }

            Color c = SeedColor(element);
            var marker = FxFactory.SpawnPrimitive(pos + Vector3.up * 0.15f, PrimitiveType.Sphere, 0.35f, c, -1f, false);
            _seeds.Add(new Seed { pos = pos, expireAt = Time.time + SeedDuration, marker = marker });
        }

        /// <summary>引爆全部种子：在每颗种子位置对范围敌人施加 cfg 的伤害 / 元素 / 状态。返回引爆数量。</summary>
        public int DetonateAll(ChainConfig cfg, PlayerController owner, LayerMask enemyLayer)
        {
            if (_seeds.Count == 0) return 0;

            int count = _seeds.Count;
            float radius = cfg.radius > 0.1f ? cfg.radius : DefaultDetonateRadius;
            float dmg = cfg.damage;
            if (owner != null) dmg += cfg.damageScaling * owner.Stats.attackDamage;
            Color c = SeedColor(cfg.elementTag);

            foreach (var s in _seeds)
            {
                FxFactory.SpawnElementBurst(s.pos + Vector3.up * 0.1f, cfg.elementTag, radius, 0.6f);
                FxFactory.SpawnAOERing(s.pos + Vector3.up * 0.05f, radius, c, 0.5f);

                var hits = Physics.OverlapSphere(s.pos, radius, enemyLayer);
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    var dmgable = h.GetComponent<IDamageable>();
                    if (dmgable != null && dmg > 0f)
                    {
                        dmgable.OnDamage(dmg, h.transform.position, owner != null ? owner.gameObject : null);
                        GameEvents.Publish(new GameEvents.DamageNumberRequested
                        {
                            WorldPosition = h.transform.position + Vector3.up * 1.5f,
                            Damage = dmg,
                            SpecialTag = "引爆"
                        });
                    }
                    ApplyStatus(cfg, h.gameObject);
                }

                if (s.marker != null) Destroy(s.marker);
            }

            _seeds.Clear();
            return count;
        }

        /// <summary>清空所有种子（换局 / 撤离时可调）。</summary>
        public void ClearAll()
        {
            ClearMarkers();
            _seeds.Clear();
        }

        private void ClearMarkers()
        {
            foreach (var s in _seeds)
                if (s.marker != null) Destroy(s.marker);
        }

        private static void ApplyStatus(ChainConfig cfg, GameObject target)
        {
            if (target == null) return;
            if (cfg.addBurn && cfg.burnDPS > 0f) SkillModifierApplier.ApplyBurn(target, cfg.burnDPS, cfg.burnDuration);
            if (cfg.addFreeze && cfg.freezeDuration > 0f) SkillModifierApplier.ApplyFreeze(target, cfg.freezeDuration);
            if (cfg.addPoison && cfg.poisonDPS > 0f) SkillModifierApplier.ApplyBurn(target, cfg.poisonDPS, cfg.poisonDuration);
            if (cfg.effectType == EffectType.DoT && cfg.dotDPS > 0f) SkillModifierApplier.ApplyBurn(target, cfg.dotDPS, cfg.dotDuration);
        }

        private static Color SeedColor(ElementTag element)
        {
            if (element == ElementTag.None) return new Color(0.4f, 1f, 0.5f); // 自然绿
            return FxFactory.ElementColor(element);
        }
    }
}
