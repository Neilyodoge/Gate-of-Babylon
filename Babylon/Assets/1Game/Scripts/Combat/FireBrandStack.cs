using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 业焰印（火灵根专属叠层 DoT/Burst 机制 · v0.5 Week 6）
    ///
    /// 核心循环：
    /// 1. 火灵根玩家任意伤害命中敌人 → 该敌人挂 / 刷新 FireBrandStack（+1 层，最多 MaxStacks）
    /// 2. 每层 = 该敌人受到玩家的"火属性伤害" + brandFireDamageBonusPerStack（被动放大）
    /// 3. 满 MaxStacks 层 → 立即引爆：消耗所有层，造成 [攻击力 × explodeRatio × 当前层数] 一次性火 AOE
    /// 4. 引爆 AOE 内的其他敌人，各自 +chainStacksOnExplode 层（连锁滚雪球）
    /// 5. 狂火（SpiritRootFireController.InFrenzy）期间：
    ///    - 单次伤害命中 +2 层（而不是 +1）
    ///    - 层数过期时长延长（4 → 6s）
    ///    - 引爆 AOE 半径 × 1.5
    ///
    /// 与现有 <see cref="BurnEffect"/> 完全独立 ——
    ///   - BurnEffect 是"装备/技能/灵物"驱动的 DoT，dps 累加
    ///   - FireBrandStack 是"火灵根专属"的叠层引爆机制
    /// 两者可以同时存在；FireBrand 不写 dps，专注"叠层 → 引爆"power fantasy。
    /// </summary>
    [DisallowMultipleComponent]
    public class FireBrandStack : MonoBehaviour, IFireBrandReadable
    {
        // ===== 平衡常量（火灵根重设计的唯一一处数值聚合，方便后续 playtest 调）=====
        public const int MaxStacks = 5;
        public const float StackLifetimeBase = 4f;
        public const float StackLifetimeFrenzy = 6f;
        public const float BrandFireDamageBonusPerStack = 0.10f;   // 每层 +10% 火伤放大
        public const float ExplodeRatio = 1.5f;                    // 引爆系数：攻击力 × 1.5 × 层数
        public const float ExplodeRadiusBase = 1.8f;
        public const float ExplodeRadiusFrenzy = 2.7f;
        public const int ChainStacksOnExplode = 2;

        public int CurrentStacks { get; private set; }
        public float Multiplier => 1f + BrandFireDamageBonusPerStack * CurrentStacks;

        private float _expireTimer;
        private float _lifetime;
        private IDamageable _target;
        private GameObject _stackVfxHolder;

        // ============================== 公共 API ==============================

        /// <summary>给目标加 N 层业焰印。Inline 调用方式：FireBrandStack.AddStacks(go, 1, false)</summary>
        public static FireBrandStack AddStacks(GameObject target, int delta, bool inFrenzy)
        {
            if (target == null || delta <= 0) return null;
            // 已死的敌人不再挂
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && damageable.Stats != null && !damageable.Stats.IsAlive) return null;

            var brand = target.GetComponent<FireBrandStack>();
            if (brand == null) brand = target.AddComponent<FireBrandStack>();
            brand._target = damageable;
            brand._lifetime = inFrenzy ? StackLifetimeFrenzy : StackLifetimeBase;
            brand._expireTimer = brand._lifetime;
            brand.CurrentStacks = Mathf.Min(MaxStacks, brand.CurrentStacks + delta);
            brand.RefreshStackVfx();

            // 满层 → 引爆（在下一帧执行，避免在 OnDamage 流程里递归）
            if (brand.CurrentStacks >= MaxStacks)
            {
                brand.StartCoroutine(brand.ExplodeNextFrame(inFrenzy));
            }
            return brand;
        }

        /// <summary>读取当前层数（外部 UI / 伤害放大用）</summary>
        public static int GetStacks(GameObject target)
        {
            if (target == null) return 0;
            var brand = target.GetComponent<FireBrandStack>();
            return brand != null ? brand.CurrentStacks : 0;
        }

        /// <summary>读取火属性伤害放大系数（≥1）。火灵根的 Skill/普攻 在结算前可以乘上这个</summary>
        public static float GetFireDamageMultiplier(GameObject target)
        {
            if (target == null) return 1f;
            var brand = target.GetComponent<FireBrandStack>();
            return brand != null ? brand.Multiplier : 1f;
        }

        // ============================== 生命周期 ==============================

        private void Update()
        {
            if (CurrentStacks <= 0)
            {
                Destroy(this);
                return;
            }
            _expireTimer -= Time.deltaTime;
            if (_expireTimer <= 0f)
            {
                // 过期：每次掉一层而不是全清，类似 PoE 燃烧
                CurrentStacks = Mathf.Max(0, CurrentStacks - 1);
                _expireTimer = _lifetime;
                RefreshStackVfx();
                if (CurrentStacks <= 0)
                {
                    ClearStackVfx();
                    Destroy(this);
                }
            }
        }

        private System.Collections.IEnumerator ExplodeNextFrame(bool inFrenzy)
        {
            yield return null;
            if (this == null || _target == null) yield break;
            Explode(inFrenzy);
        }

        // ============================== 引爆 ==============================

        private void Explode(bool inFrenzy)
        {
            int stacksAtExplode = CurrentStacks;
            CurrentStacks = 0;
            ClearStackVfx();

            float radius = inFrenzy ? ExplodeRadiusFrenzy : ExplodeRadiusBase;
            Color fireColor = FxFactory.ElementColor(ElementTag.Fire);
            Vector3 origin = transform.position + Vector3.up * 0.6f;

            // 视觉：大火元素爆发 + 地面爆环 + 8 道火舌
            FxFactory.SpawnElementBurst(origin, ElementTag.Fire, radius * 1.1f, 0.55f);
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, radius,
                fireColor, lifetime: 0.5f);
            CameraShake.TriggerLight();

            // 计算伤害基数：玩家攻击力
            float playerAtk = 50f;
            var player = PlayerController.Instance;
            if (player != null && player.Stats != null) playerAtk = player.Stats.attackDamage;
            float coreDamage = playerAtk * ExplodeRatio * stacksAtExplode;

            // AOE 命中所有敌人
            var hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var col in hits)
            {
                if (col == null) continue;
                // 跳过玩家本身
                if (col.CompareTag("Player")) continue;
                var dmg = col.GetComponent<IDamageable>();
                if (dmg == null || dmg.Stats == null || !dmg.Stats.IsAlive) continue;

                // 中心目标承受全额引爆伤
                if (dmg == _target)
                {
                    dmg.OnDamage(coreDamage, transform.position, player != null ? player.gameObject : gameObject);
                }
                else
                {
                    // 周围目标：50% 伤害 + 自身追加 2 层（连锁）
                    dmg.OnDamage(coreDamage * 0.5f, col.transform.position,
                        player != null ? player.gameObject : gameObject);
                    AddStacks(col.gameObject, ChainStacksOnExplode, inFrenzy);
                }
            }

            // 通知 HUD / 玩家挂件
            GameEvents.Publish(new GameEvents.FireBrandExploded
            {
                EnemyPos = transform.position,
                StacksConsumed = stacksAtExplode,
                Radius = radius
            });

            // 引爆后本组件即刻销毁
            Destroy(this);
        }

        // ============================== 视觉（敌人头顶 N 个橙色小球）==============================

        private void RefreshStackVfx()
        {
            // 没有 vfx 容器时创建
            if (_stackVfxHolder == null)
            {
                _stackVfxHolder = new GameObject("__FireBrandIcons");
                _stackVfxHolder.transform.SetParent(transform, false);
                _stackVfxHolder.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            }

            // 用 FxFactory 的种子图标 helper —— 已有"RefreshHeadSeedIcons"现成的"敌人头顶 N 球"工具
            // 业焰印颜色：火橙红
            Color brand = FxFactory.ElementColor(ElementTag.Fire);
            FxFactory.RefreshHeadSeedIcons(transform, CurrentStacks, brand, yOffset: 2.1f);
        }

        private void ClearStackVfx()
        {
            FxFactory.ClearHeadSeedIcons(transform);
            if (_stackVfxHolder != null)
            {
                Destroy(_stackVfxHolder);
                _stackVfxHolder = null;
            }
        }

        private void OnDestroy()
        {
            ClearStackVfx();
        }
    }

    /// <summary>面向只读读取层数的接口（防止外部直接操纵）</summary>
    public interface IFireBrandReadable
    {
        int CurrentStacks { get; }
        float Multiplier { get; }
    }
}
