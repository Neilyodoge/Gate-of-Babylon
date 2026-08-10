using UnityEditor;
using UnityEngine;

namespace XianTu.EditorTools
{
    /// <summary>
    /// P2 内容扩展：批量生成新的效果器 / 改造件模块，把模块池补到目标数量。
    /// 只使用 PlayerCombat 已实现的 EffectType / ModifierType，保证新模块开箱即用。
    /// 幂等：已存在同名资产则跳过。
    /// </summary>
    public static class PoolExpansionGenerator
    {
        private const string Dir = "Assets/1Game/Data/Modules";

        [MenuItem("仙途秘境/开发工具/扩容模块池")]
        public static int Generate()
        {
            int n = 0;

            // ---------- 效果器（EffectType 均已在 PlayerCombat 实现）----------
            n += Eff("E_JianSu", "减速", "对命中敌人施加减速，放缓其行动。",
                EffectType.Slow, EffectRole.Addon, ElementTag.Ice, FunctionTag.Control, StyleTag.Ice, ItemRarity.Fan);
            n += Eff("E_XuanYun", "眩晕", "对命中敌人施加短暂眩晕，打断其动作。",
                EffectType.Stun, EffectRole.Addon, ElementTag.Thunder, FunctionTag.Control, StyleTag.Lightning, ItemRarity.Ling);
            n += Eff("E_YiShang", "易伤", "标记命中敌人，使其受到的伤害提高。",
                EffectType.MarkVulnerable, EffectRole.Addon, ElementTag.None, FunctionTag.Damage | FunctionTag.Control, StyleTag.None, ItemRarity.Ling);
            n += Eff("E_WuDi", "无敌", "消费时获得短暂无敌，硬抗一波。",
                EffectType.Invincible, EffectRole.Enhancement, ElementTag.None, FunctionTag.Defense, StyleTag.None, ItemRarity.Xuan);
            n += Eff("E_JingHua", "净化", "消费时清除自身减益状态。",
                EffectType.Cleanse, EffectRole.Enhancement, ElementTag.Life, FunctionTag.Defense | FunctionTag.Heal, StyleTag.None, ItemRarity.Ling);
            n += Eff("E_HuoYu_Rain", "火雨", "在目标区域降下烈焰，大范围灼烧杀伤。",
                EffectType.AreaDamage, EffectRole.Addon, ElementTag.Fire, FunctionTag.Damage, StyleTag.Fire, ItemRarity.Xuan);
            n += Eff("E_BingZhui", "冰锥", "射出寒冰飞弹，命中减速。",
                EffectType.Projectile, EffectRole.Addon, ElementTag.Ice, FunctionTag.Damage, StyleTag.Ice, ItemRarity.Ling);
            n += Eff("E_JuDu", "剧毒", "释放剧毒，对范围敌人持续掉血。",
                EffectType.DoT, EffectRole.Addon, ElementTag.Wood, FunctionTag.Damage, StyleTag.Poison, ItemRarity.Ling);
            n += Eff("E_ZhenDangBo", "震荡波", "爆发冲击波击退周围敌人。",
                EffectType.Knockback, EffectRole.Addon, ElementTag.Wind, FunctionTag.Control, StyleTag.None, ItemRarity.Fan);
            n += Eff("E_LuoShi", "落石", "召唤巨石砸落，造成范围土系伤害。",
                EffectType.AreaDamage, EffectRole.Addon, ElementTag.Earth, FunctionTag.Damage, StyleTag.None, ItemRarity.Ling);

            // ---------- 改造件（ModifierType 均已在链编译/效果中处理）----------
            n += Mod("M_FuLei", "附雷", "命中附加雷击，追加雷系伤害。",
                ModifierType.AddLightning, FunctionTag.Damage, StyleTag.Lightning, ItemRarity.Ling);
            n += Mod("M_PoZhan", "破绽", "命中附加易伤，放大后续伤害。",
                ModifierType.AddVulnerable, FunctionTag.Damage | FunctionTag.Control, StyleTag.None, ItemRarity.Ling);
            n += Mod("M_YanLeng", "延冷", "延长冷却换取更大范围。",
                ModifierType.CostCooldown, FunctionTag.None, StyleTag.None, ItemRarity.Fan);

            if (n > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"<color=cyan>[P2 扩容] 新建 {n} 个模块</color>");
            return n;
        }

        private static int Eff(string id, string name, string desc, EffectType et, EffectRole role,
            ElementTag elem, FunctionTag fn, StyleTag st, ItemRarity rar)
        {
            string path = $"{Dir}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ModuleDef>(path) != null) return 0;
            var m = ScriptableObject.CreateInstance<ModuleDef>();
            m.moduleId = id; m.displayName = name; m.description = desc; m.uiDescription = name;
            m.category = ModuleCategory.Effect;
            m.effectType = et; m.effectRole = role; m.elementTag = elem;
            m.functionTags = fn; m.styleTags = st; m.rarity = rar;
            AssetDatabase.CreateAsset(m, path);
            return 1;
        }

        private static int Mod(string id, string name, string desc, ModifierType mt,
            FunctionTag fn, StyleTag st, ItemRarity rar)
        {
            string path = $"{Dir}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ModuleDef>(path) != null) return 0;
            var m = ScriptableObject.CreateInstance<ModuleDef>();
            m.moduleId = id; m.displayName = name; m.description = desc; m.uiDescription = name;
            m.category = ModuleCategory.Modifier;
            m.modifierType = mt;
            m.functionTags = fn; m.styleTags = st; m.rarity = rar;
            AssetDatabase.CreateAsset(m, path);
            return 1;
        }
    }
}
