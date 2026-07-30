using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// GDD V.07：运行时自动加载模块池，并在 StartNewRun 时给玩家种子 loadout。
    /// 种子 loadout = 1 被动链 (Q) + 1 主动链 (E) + 剩余模块入背包。
    /// </summary>
    public static class ModulePoolLoader
    {
        private static ModuleDef[] _cachedPool;

        public static ModuleDef[] LoadAll()
        {
            if (_cachedPool != null && _cachedPool.Length > 0) return _cachedPool;

            var catalog = Resources.Load<ModuleCatalog>("ModuleCatalog");
            if (catalog != null && catalog.modules != null && catalog.modules.Length > 0)
                _cachedPool = catalog.modules;

            if (_cachedPool != null && _cachedPool.Length > 0)
            {
                Debug.Log($"<color=cyan>[ModulePoolLoader] 从 ModuleCatalog 加载了 {_cachedPool.Length} 个模块定义</color>");
                return _cachedPool;
            }

            _cachedPool = Resources.LoadAll<ModuleDef>("Modules");
            if (_cachedPool == null || _cachedPool.Length == 0)
            {
                _cachedPool = Resources.LoadAll<ModuleDef>("");
                if (_cachedPool != null)
                {
                    var list = new List<ModuleDef>();
                    foreach (var m in _cachedPool)
                        if (m != null) list.Add(m);
                    _cachedPool = list.ToArray();
                }
            }
            Debug.Log($"<color=cyan>[ModulePoolLoader] 加载了 {(_cachedPool != null ? _cachedPool.Length : 0)} 个模块定义</color>");
            return _cachedPool ?? System.Array.Empty<ModuleDef>();
        }

        /// <summary>
        /// GDD V.07 种子 loadout：
        /// Q 槽 = 被动链（连击3次→范围爆炸+灼烧）
        /// E 槽 = 主动链（闪避后→飞弹）
        /// 所有模块同时入背包
        /// </summary>
        public static void GrantSeedLoadout(PlayerController player, ModuleDef[] pool)
        {
            if (player == null || pool == null || pool.Length == 0) return;

            var inv = player.GetComponent<ModuleInventory>();
            var slots = player.GetComponent<ModuleSlotManager>();
            if (inv == null || slots == null) return;

            // 所有模块入背包
            foreach (var m in pool)
            {
                if (m != null) inv.Add(m);
            }

            // 寻找种子链 Q（被动）：优先 MeleeHitCount 触发 + AreaDamage 效果 + AddBurn 改造
            ModuleDef qTrigger = FindBest(pool, ModuleCategory.Trigger, ExecutionMode.Passive,
                t => t.triggerType == TriggerType.MeleeHitCount);
            ModuleDef qEffect = FindBest(pool, ModuleCategory.Effect, ExecutionMode.Passive,
                e => e.effectType == EffectType.AreaDamage);
            ModuleDef qModifier = FindBest(pool, ModuleCategory.Modifier, null,
                m => m.modifierType == ModifierType.AddBurn);

            if (qTrigger != null && qEffect != null)
            {
                var chain = new ModuleChain
                {
                    trigger = qTrigger,
                    effect = qEffect,
                    modifier0 = qModifier
                };
                slots.EquipChain(0, chain);
                Debug.Log($"<color=#00ffcc>种子被动链装配到 Q：{chain.DisplayName}</color>");
            }

            // 寻找种子链 E（主动）：优先 DodgeFinish 触发 + Projectile 效果
            ModuleDef eTrigger = FindBest(pool, ModuleCategory.Trigger, ExecutionMode.Active,
                t => t.triggerType == TriggerType.DodgeFinish);
            ModuleDef eEffect = FindBest(pool, ModuleCategory.Effect, ExecutionMode.Active,
                e => e.effectType == EffectType.Projectile || e.effectType == EffectType.SwordWave);

            if (eTrigger == null)
                eTrigger = FindBest(pool, ModuleCategory.Trigger, null,
                    t => t != qTrigger);
            if (eEffect == null)
                eEffect = FindBest(pool, ModuleCategory.Effect, null,
                    e => e != qEffect);

            if (eTrigger != null && eEffect != null)
            {
                var chain = new ModuleChain
                {
                    trigger = eTrigger,
                    effect = eEffect
                };
                slots.EquipChain(1, chain);
                Debug.Log($"<color=#00ffcc>种子主动链装配到 E：{chain.DisplayName}</color>");
            }
        }

        private static ModuleDef FindBest(ModuleDef[] pool, ModuleCategory cat,
            ExecutionMode? preferredMode, System.Func<ModuleDef, bool> predicate)
        {
            ModuleDef best = null;
            ModuleDef fallback = null;

            foreach (var m in pool)
            {
                if (m == null) continue;
                bool catMatch = m.category == cat
                    || (cat == ModuleCategory.Trigger && m.category == ModuleCategory.Universal)
                    || (cat == ModuleCategory.Effect && m.category == ModuleCategory.Universal);
                if (!catMatch) continue;

                if (fallback == null) fallback = m;
                if (predicate != null && predicate(m))
                {
                    if (preferredMode == null || m.executionMode == preferredMode)
                        return m;
                    if (best == null) best = m;
                }
            }
            return best ?? fallback;
        }
    }
}
