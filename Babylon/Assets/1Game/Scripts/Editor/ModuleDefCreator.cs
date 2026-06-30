#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace XianTu
{
    /// <summary>
    /// Editor 菜单：一键生成 GDD V.07 对齐的 Demo 测试模块 SO 资产。
    /// 菜单路径：秘境探索 / 创建测试模块（V.07）
    /// 共 35 个模块：10 触发器 + 10 效果器 + 10 改造件 + 5 万能件
    /// </summary>
    public static class ModuleDefCreator
    {
        private const string OutputDir = "Assets/1Game/Data/Modules";

        [MenuItem("秘境探索/创建测试模块（V.07）")]
        public static void CreateAllTestModules()
        {
            EnsureDirectory();

            // ==================== 触发器 (10) ====================

            // 条件型
            CreateTrigger("T_连击3次", "近战命中3次后触发",
                TriggerType.MeleeHitCount, 3, 2f, ExecutionMode.Passive,
                FunctionTag.Damage, StyleTag.None);

            CreateTrigger("T_闪避后", "闪避结束后触发",
                TriggerType.DodgeFinish, 1, 3f, ExecutionMode.Active,
                FunctionTag.Mobility, StyleTag.None);

            CreateTrigger("T_受击时", "受到伤害时触发",
                TriggerType.OnDamaged, 1, 4f, ExecutionMode.Passive,
                FunctionTag.Defense, StyleTag.None);

            CreateTrigger("T_每5秒", "每5秒自动触发",
                TriggerType.TimeInterval, 1, 0f, ExecutionMode.Passive,
                FunctionTag.None, StyleTag.None, 5f);

            CreateTrigger("T_击杀后", "击杀敌人后触发",
                TriggerType.EnemyKill, 1, 1f, ExecutionMode.Passive,
                FunctionTag.Damage, StyleTag.None);

            CreateTrigger("T_移动10米", "累计移动10米后触发",
                TriggerType.MoveDistance, 1, 1f, ExecutionMode.Passive,
                FunctionTag.Mobility, StyleTag.None, 0f, 10f);

            CreateTrigger("T_暴击后", "暴击命中后触发",
                TriggerType.CriticalHit, 1, 3f, ExecutionMode.Passive,
                FunctionTag.Damage, StyleTag.None);

            CreateTrigger("T_低血量", "生命低于30%时持续触发",
                TriggerType.LowHealth, 1, 5f, ExecutionMode.Passive,
                FunctionTag.Defense, StyleTag.None, 0f, 0f, 0.3f);

            CreateTrigger("T_连击5次", "近战命中5次后触发（高频）",
                TriggerType.MeleeHitCount, 5, 1.5f, ExecutionMode.Active,
                FunctionTag.Damage, StyleTag.None);

            CreateTrigger("T_每3秒", "每3秒自动触发（快节奏）",
                TriggerType.TimeInterval, 1, 0f, ExecutionMode.Passive,
                FunctionTag.None, StyleTag.None, 3f);

            // ==================== 效果器 (10) ====================

            // 伤害输出
            CreateEffect("E_范围爆炸", "在鼠标位置产生范围爆炸",
                EffectType.AreaDamage, ExecutionMode.Passive,
                damage: 30f, radius: 4f, element: ElementTag.Fire,
                function: FunctionTag.Damage, shape: ShapeTag.Area, style: StyleTag.Fire);

            CreateEffect("E_飞弹", "向瞄准方向发射飞弹",
                EffectType.Projectile, ExecutionMode.Active,
                damage: 18f, projCount: 1, projSpeed: 18f,
                function: FunctionTag.Damage, shape: ShapeTag.Projectile);

            CreateEffect("E_剑气", "释放前方扇形剑气",
                EffectType.SwordWave, ExecutionMode.Active,
                damage: 22f, projCount: 3, projSpeed: 20f, spreadAngle: 30f,
                function: FunctionTag.Damage, shape: ShapeTag.Melee);

            CreateEffect("E_毒雾", "在鼠标位置释放持续毒雾",
                EffectType.DoT, ExecutionMode.Passive,
                radius: 3.5f, element: ElementTag.Earth,
                function: FunctionTag.Damage, shape: ShapeTag.Area, style: StyleTag.Poison,
                dotDPS: 8f, dotDuration: 4f);

            CreateEffect("E_落雷", "向最近敌人落下雷击",
                EffectType.AreaDamage, ExecutionMode.Passive,
                damage: 35f, radius: 3f, element: ElementTag.Wind,
                function: FunctionTag.Damage, shape: ShapeTag.Area, style: StyleTag.Lightning);

            // 控制
            CreateEffect("E_冲击波", "击退周围所有敌人",
                EffectType.Knockback, ExecutionMode.Active,
                radius: 5f, knockbackForce: 12f,
                function: FunctionTag.Control, shape: ShapeTag.Area);

            // 防御/回复
            CreateEffect("E_治疗", "恢复生命值",
                EffectType.Heal, ExecutionMode.Passive,
                function: FunctionTag.Heal,
                healAmount: 25f, healScaling: 0.3f);

            CreateEffect("E_护盾", "获得减伤护盾",
                EffectType.Shield, ExecutionMode.Active,
                function: FunctionTag.Defense, shape: ShapeTag.Aura,
                buffDuration: 5f, buffDR: 0.3f);

            // 位移
            CreateEffect("E_突刺", "向瞄准方向突刺",
                EffectType.Dash, ExecutionMode.Active,
                damage: 15f, function: FunctionTag.Mobility | FunctionTag.Damage,
                shape: ShapeTag.Melee, dashDist: 6f);

            CreateEffect("E_引力场", "拉拽周围敌人到玩家位置",
                EffectType.Pull, ExecutionMode.Active,
                radius: 6f, knockbackForce: 10f,
                function: FunctionTag.Control, shape: ShapeTag.Area);

            // ==================== 改造件 (10) ====================

            CreateModifier("M_扩散", "范围增大50%", ModifierType.RadiusScale, 1.5f,
                FunctionTag.Damage, ShapeTag.Area);
            CreateModifier("M_连锁", "投射物翻倍", ModifierType.CountScale, 2f,
                FunctionTag.Damage, ShapeTag.Projectile);
            CreateModifier("M_灼烧", "附加灼烧效果", ModifierType.AddBurn, 0f,
                FunctionTag.Damage, ShapeTag.None, StyleTag.Fire,
                burnDPS: 6f, burnDuration: 3f);
            CreateModifier("M_冰冻", "附加冰冻减速", ModifierType.AddFreeze, 0f,
                FunctionTag.Control, ShapeTag.None, StyleTag.Ice,
                freezeDuration: 2f);
            CreateModifier("M_持续延长", "持续时间增加80%", ModifierType.DurationScale, 1.8f,
                FunctionTag.None, ShapeTag.None);
            CreateModifier("M_伤害强化", "伤害提升40%", ModifierType.DamageScale, 1.4f,
                FunctionTag.Damage, ShapeTag.None);
            CreateModifier("M_额外飞弹", "额外发射2枚投射物", ModifierType.ExtraProjectile, 0f,
                FunctionTag.Damage, ShapeTag.Projectile,
                extraCount: 2);
            CreateModifier("M_毒蚀", "附加毒蚀效果", ModifierType.AddPoison, 0f,
                FunctionTag.Damage, ShapeTag.None, StyleTag.Poison,
                poisonDPS: 4f, poisonDuration: 5f);
            CreateModifier("M_以血换力", "消耗10%生命，伤害+50%", ModifierType.CostHP, 0f,
                FunctionTag.Damage, ShapeTag.None,
                costHPPercent: 0.1f, costDamageBonus: 0.5f);
            CreateModifier("M_击退", "附加击退效果", ModifierType.AddKnockback, 0f,
                FunctionTag.Control, ShapeTag.None);

            // ==================== 万能件 (5) ====================

            CreateUniversal("U_闪电/落雷",
                "作为触发器：闪避后触发 | 作为效果器：范围落雷",
                TriggerType.DodgeFinish, 1, 3f,
                EffectType.AreaDamage,
                damage: 28f, radius: 3.5f, element: ElementTag.Wind,
                function: FunctionTag.Damage | FunctionTag.Mobility,
                style: StyleTag.Lightning);

            CreateUniversal("U_种子/引爆",
                "作为触发器：攻击3次积累种子 | 作为效果器：引爆种子造成范围伤害",
                TriggerType.MeleeHitCount, 3, 2f,
                EffectType.AreaDamage,
                damage: 40f, radius: 4f, element: ElementTag.Earth,
                function: FunctionTag.Damage | FunctionTag.State,
                style: StyleTag.Seed);

            CreateUniversal("U_冲刺/突刺",
                "作为触发器：移动8米后触发 | 作为效果器：突刺位移+伤害",
                TriggerType.MoveDistance, 1, 2f,
                EffectType.Dash,
                damage: 20f, element: ElementTag.None,
                function: FunctionTag.Mobility | FunctionTag.Damage,
                dashDist: 5f, moveDistThresh: 8f);

            CreateUniversal("U_护盾/治疗",
                "作为触发器：受击时触发 | 作为效果器：回复生命",
                TriggerType.OnDamaged, 1, 4f,
                EffectType.Heal,
                function: FunctionTag.Defense | FunctionTag.Heal,
                healAmount: 20f, healScaling: 0.25f);

            CreateUniversal("U_连击/剑气",
                "作为触发器：连击5次触发 | 作为效果器：释放扇形剑气",
                TriggerType.MeleeHitCount, 5, 1.5f,
                EffectType.SwordWave,
                damage: 25f, element: ElementTag.None,
                function: FunctionTag.Damage,
                projCount: 5, projSpeed: 22f, spreadAngle: 45f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>已创建 35 个 GDD V.07 测试模块到 {OutputDir}</color>");
        }

        // ==================== Factory Methods ====================

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/1Game/Data"))
                    AssetDatabase.CreateFolder("Assets/1Game", "Data");
                AssetDatabase.CreateFolder("Assets/1Game/Data", "Modules");
            }
        }

        private static ModuleDef CreateBase(string displayName, string desc, ModuleCategory cat,
            ItemRarity rarity, ExecutionMode mode, FunctionTag func, ShapeTag shape = ShapeTag.None, StyleTag style = StyleTag.None)
        {
            var asset = ScriptableObject.CreateInstance<ModuleDef>();
            asset.moduleId = displayName;
            asset.displayName = displayName;
            asset.description = desc;
            asset.uiDescription = desc;
            asset.category = cat;
            asset.rarity = rarity;
            asset.executionMode = mode;
            asset.functionTags = func;
            asset.shapeTags = shape;
            asset.styleTags = style;

            string safeName = displayName.Replace("/", "_");
            string path = $"{OutputDir}/{safeName}.asset";
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateTrigger(string name, string desc,
            TriggerType type, int threshold, float cd, ExecutionMode mode,
            FunctionTag func, StyleTag style,
            float interval = 0f, float moveDist = 0f, float healthThresh = 0.3f)
        {
            var m = CreateBase(name, desc, ModuleCategory.Trigger, ItemRarity.Fan, mode, func, style: style);
            m.triggerType = type;
            m.triggerThreshold = threshold;
            m.triggerCooldown = cd;
            m.triggerInterval = interval;
            m.moveDistanceThreshold = moveDist > 0f ? moveDist : 10f;
            m.healthThreshold = healthThresh;
            EditorUtility.SetDirty(m);
        }

        private static void CreateEffect(string name, string desc,
            EffectType type, ExecutionMode mode,
            FunctionTag function, ShapeTag shape = ShapeTag.None, StyleTag style = StyleTag.None,
            float damage = 0f, float radius = 0f, ElementTag element = ElementTag.None,
            int projCount = 1, float projSpeed = 15f, float spreadAngle = 0f,
            float healAmount = 0f, float healScaling = 0f,
            float buffDuration = 0f, float buffDR = 0f,
            float knockbackForce = 0f, float dashDist = 0f,
            float dotDPS = 0f, float dotDuration = 0f)
        {
            var rarity = damage >= 30f || type == EffectType.Dash ? ItemRarity.Ling : ItemRarity.Fan;
            var m = CreateBase(name, desc, ModuleCategory.Effect, rarity, mode, function, shape, style);
            m.effectType = type;
            m.baseDamage = damage;
            m.aoeRadius = radius;
            m.elementTag = element;
            m.projectileCount = projCount;
            m.projectileSpeed = projSpeed;
            m.spreadAngle = spreadAngle;
            m.healAmount = healAmount;
            m.healScaling = healScaling;
            m.buffDuration = buffDuration;
            m.buffDamageReduction = buffDR;
            m.knockbackForce = knockbackForce;
            m.dashDistance = dashDist;
            m.dotDPS = dotDPS;
            m.dotDuration = dotDuration;
            EditorUtility.SetDirty(m);
        }

        private static void CreateModifier(string name, string desc,
            ModifierType type, float value,
            FunctionTag func, ShapeTag shape, StyleTag style = StyleTag.None,
            float burnDPS = 0f, float burnDuration = 0f,
            float freezeDuration = 0f,
            float poisonDPS = 0f, float poisonDuration = 0f,
            int extraCount = 0,
            float costHPPercent = 0f, float costDamageBonus = 0f)
        {
            var rarity = type == ModifierType.CostHP ? ItemRarity.Xuan : ItemRarity.Ling;
            var m = CreateBase(name, desc, ModuleCategory.Modifier, rarity, ExecutionMode.Passive, func, shape, style);
            m.modifierType = type;
            m.modifierValue = value > 0f ? value : 1f;
            m.burnDPS = burnDPS;
            m.burnDuration = burnDuration;
            m.freezeDuration = freezeDuration;
            m.poisonDPS = poisonDPS;
            m.poisonDuration = poisonDuration;
            m.extraCount = extraCount;
            m.costHPPercent = costHPPercent;
            m.costDamageBonus = costDamageBonus;
            EditorUtility.SetDirty(m);
        }

        private static void CreateUniversal(string name, string desc,
            TriggerType trigType, int trigThreshold, float trigCooldown,
            EffectType effType,
            FunctionTag function, StyleTag style = StyleTag.None,
            float damage = 0f, float radius = 0f, ElementTag element = ElementTag.None,
            int projCount = 1, float projSpeed = 15f, float spreadAngle = 0f,
            float healAmount = 0f, float healScaling = 0f,
            float dashDist = 0f, float moveDistThresh = 10f)
        {
            var m = CreateBase(name, desc, ModuleCategory.Universal, ItemRarity.Ling,
                ExecutionMode.Passive, function, style: style);

            m.universalTriggerType = trigType;
            m.universalTriggerThreshold = trigThreshold;
            m.universalTriggerCooldown = trigCooldown;
            m.universalEffectType = effType;
            m.universalTriggerDesc = $"触发：{trigType}";
            m.universalEffectDesc = $"效果：{effType}";

            m.triggerType = trigType;
            m.triggerThreshold = trigThreshold;
            m.triggerCooldown = trigCooldown;
            m.moveDistanceThreshold = moveDistThresh;

            m.effectType = effType;
            m.baseDamage = damage;
            m.aoeRadius = radius;
            m.elementTag = element;
            m.projectileCount = projCount;
            m.projectileSpeed = projSpeed;
            m.spreadAngle = spreadAngle;
            m.healAmount = healAmount;
            m.healScaling = healScaling;
            m.dashDistance = dashDist;

            EditorUtility.SetDirty(m);
        }
    }
}
#endif
