using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 模块掉落的软性动态权重。
    ///
    /// 依据以下维度抬升相关模块的掉落权重，但每个模块的基础权重恒 &gt; 0——不硬锁任何模块，玩家随时可转向：
    /// 1. 半成型链补齐（某槽位有件但缺触发器/效果器 → 抬升对应大类）。
    /// 2. 本局构筑协同（与已拥有模块 styleTags 重叠）。
    /// 3. V0.2.1 层级稀有度偏移（rarityBias 提升高品阶模块被选中概率）。
    /// </summary>
    public static class ModuleDropWeighting
    {
        public const float BaseWeight = 1f;
        public const float GapFillBonus = 2f;
        public const float StyleSynergyBonus = 0.75f;

        /// <summary>按动态权重从池中随机抽取一个模块（软偏好，不硬锁）。</summary>
        public static ModuleDef PickWeighted(IReadOnlyList<ModuleDef> pool, int rarityBias = 0)
        {
            if (pool == null || pool.Count == 0) return null;

            var ctx = BuildContext();

            float total = 0f;
            var weights = new float[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                weights[i] = WeightOf(pool[i], ctx, rarityBias);
                total += weights[i];
            }
            if (total <= 0f) return pool[Random.Range(0, pool.Count)];

            float r = Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        private struct Ctx
        {
            public StyleTag ownedStyle;
            public bool needsTrigger;
            public bool needsEffect;
        }

        private static Ctx BuildContext()
        {
            var ctx = new Ctx();

            var player = PlayerController.Instance;
            if (player != null)
            {
                var inv = player.GetComponent<ModuleInventory>();
                if (inv != null)
                    foreach (var m in inv.Modules)
                        if (m != null) ctx.ownedStyle |= m.styleTags;

                var slots = player.GetComponent<ModuleSlotManager>();
                if (slots != null)
                {
                    for (int s = 0; s < 3; s++)
                    {
                        var chain = slots.GetChain(s);
                        if (chain == null) continue;
                        bool anyPart = chain.trigger != null || chain.effect != null;
                        if (!anyPart) continue;                 // 空槽不算半成型
                        if (chain.trigger == null) ctx.needsTrigger = true;
                        if (chain.effect == null) ctx.needsEffect = true;
                    }
                }
            }
            return ctx;
        }

        private static float WeightOf(ModuleDef m, Ctx ctx, int rarityBias)
        {
            if (m == null) return 0f;
            float w = BaseWeight;

            bool isTrigger = m.category == ModuleCategory.Trigger || m.category == ModuleCategory.Universal;
            bool isEffect = m.category == ModuleCategory.Effect || m.category == ModuleCategory.Universal;
            if (ctx.needsTrigger && isTrigger) w += GapFillBonus;
            if (ctx.needsEffect && isEffect) w += GapFillBonus;

            if (ctx.ownedStyle != StyleTag.None && (m.styleTags & ctx.ownedStyle) != 0)
                w += StyleSynergyBonus;

            // V0.2.1：稀有度偏移 — rarityBias > 0 时，高品阶模块权重提升
            if (rarityBias > 0)
            {
                int rarityOrd = (int)m.rarity;
                w += rarityOrd * rarityBias * 0.01f;
            }

            return w;
        }
    }
}
