using System;
using UnityEngine;

namespace XianTu
{
    // ==================== 模块大类 ====================
    public enum ModuleCategory
    {
        Trigger,
        Effect,
        Modifier,
        Universal
    }

    // ==================== 执行模式 ====================
    public enum ExecutionMode
    {
        Passive,
        Active
    }

    // ==================== V.08 消费模型 ====================
    /// <summary>
    /// 增强链的消费模型，由触发器声明。
    /// Single/Window/Stacks 由玩家按键消费；Auto 在 Proc 时自动消费（自动释放绑定核心技能）。
    /// </summary>
    public enum ConsumeKind
    {
        Single,   // 下次按下生效 1 次，用完重新 Proc
        Window,   // 就绪后 N 秒内按下都生效，窗口结束重新 Proc
        Stacks,   // Proc 累层（上限 maxStacks），每按一次消耗 1 层
        Auto,     // Proc 即自动释放绑定核心技能（带增强），无需按键
    }

    // ==================== V.08 效果器角色 ====================
    /// <summary>
    /// 效果器在增强链中的角色。
    /// Enhancement 改核心技能本身（damage=倍率）；Addon spawn 独立世界效果（damage=附加伤害）。
    /// </summary>
    public enum EffectRole
    {
        Enhancement,
        Addon,
    }

    // ==================== 触发器子类 ====================
    public enum TriggerType
    {
        None,
        // 条件型
        MeleeHitCount,      // 近战命中 N 次
        SkillHitCount,       // 技能命中 N 次
        CriticalHit,         // 暴击后
        ComboFinisher,       // 连击终结段
        DodgeFinish,         // 闪避结束后
        MoveDistance,         // 移动 X 米
        OnDamaged,           // 受到伤害时
        ShieldBreak,         // 护盾破裂时
        LowHealth,           // 低血量时
        TimeInterval,        // 每 X 秒
        ChargeComplete,      // 蓄力满
        RoomEnter,           // 进入新房间
        EnemyKill,           // 击杀后
        EliteKill,           // 精英击杀
        // 状态型
        SeedPlant,           // 种子生成
        SeedDetonate,        // 种子引爆
        BackstabMark,        // 背击标记
        PuppetCount,         // 傀儡计数
    }

    // ==================== 效果器子类 ====================
    public enum EffectType
    {
        None,
        // 伤害输出
        AreaDamage,          // 范围伤害
        Projectile,          // 投射物
        SwordWave,           // 剑气
        DoT,                 // 持续伤害（毒雾等）
        // 控制/状态
        Slow,                // 减速
        Stun,                // 眩晕
        Knockback,           // 击退
        MarkVulnerable,      // 标记易伤
        // 防御/回复
        Heal,                // 治疗
        Shield,              // 护盾
        Cleanse,             // 净化
        Invincible,          // 短暂无敌
        // 位移/位置
        Dash,                // 突刺
        Pull,                // 拉拽
        Teleport,            // 传送
        // 召唤/场物
        SummonPuppet,        // 召唤傀儡
        SummonTurret,        // 召唤炮台
        PoisonPool,          // 毒池
        Trap,                // 陷阱
        // 资源/状态操作
        DetonateSeed,        // 引爆种子
        RefreshStacks,       // 刷新层数
        GainCharge,          // 获得充能
    }

    // ==================== 改造件子类 ====================
    public enum ModifierType
    {
        None,
        // 形态改造
        ShapeWall,           // 火球→火墙
        ShapeRing,           // 毒雾→毒环
        ShapeZone,           // 落雷→雷域
        // 目标改造
        TargetFarthest,      // 最近→最远
        TargetChain,         // 单体→链锁
        TargetSurround,      // 前方→环绕
        // 数量改造
        ExtraCount,          // 额外触发次数
        ExtraProjectile,     // 投射物 +N
        ExtraSummon,         // 召唤物 +N
        // 节奏改造
        DelayedBlast,        // 瞬发→延迟爆炸
        Sustained,           // 一次性→持续
        // 状态改造
        AddBurn,             // 附加灼烧
        AddFreeze,           // 附加冰冻
        AddLightning,        // 附加雷击
        AddPoison,           // 附加毒蚀
        AddKnockback,        // 附加击退
        AddVulnerable,       // 附加易伤
        // 数值改造（兼容旧代码）
        RadiusScale,         // 范围缩放
        CountScale,          // 数量缩放
        DurationScale,       // 持续时间缩放
        DamageScale,         // 伤害缩放
        // 代价改造
        CostHP,              // 消耗生命提高伤害
        CostCooldown,        // 延长冷却换范围
    }

    // ==================== 标签系统 ====================

    [Flags]
    public enum FunctionTag
    {
        None        = 0,
        Damage      = 1 << 0,
        Defense     = 1 << 1,
        Heal        = 1 << 2,
        Mobility    = 1 << 3,
        Summon      = 1 << 4,
        Control     = 1 << 5,
        State       = 1 << 6,
    }

    [Flags]
    public enum ShapeTag
    {
        None        = 0,
        Projectile  = 1 << 0,
        Area        = 1 << 1,
        Melee       = 1 << 2,
        Aura        = 1 << 3,
        Chain       = 1 << 4,
        Object      = 1 << 5,
    }

    [Flags]
    public enum StyleTag
    {
        None        = 0,
        Seed        = 1 << 0,
        Backstab    = 1 << 1,
        Puppet      = 1 << 2,
        Lightning   = 1 << 3,
        Poison      = 1 << 4,
        Fire        = 1 << 5,
        Ice         = 1 << 6,
    }

    // ==================== 模块定义 ====================

    [CreateAssetMenu(fileName = "NewModule", menuName = "秘境探索/模块定义")]
    public class ModuleDef : ScriptableObject
    {
        [Header("基础信息")]
        public string moduleId;
        public ModuleCategory category;
        public string displayName = "未命名模块";
        [TextArea(1, 3)]
        public string description;
        [TextArea(1, 2)]
        public string uiDescription;
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Fan;

        [Header("执行模式")]
        public ExecutionMode executionMode = ExecutionMode.Passive;

        [Header("标签")]
        public FunctionTag functionTags;
        public ShapeTag shapeTags;
        public StyleTag styleTags;

        // ==================== Trigger params ====================
        [Header("触发器参数（category == Trigger 或 Universal）")]
        public TriggerType triggerType;
        public int triggerThreshold = 1;
        public float triggerCooldown = 2f;
        public float triggerInterval = 5f;
        public bool consumeStacks = true;
        public float moveDistanceThreshold = 10f;
        public float healthThreshold = 0.3f;

        [Tooltip("V.08 消费模型：Single/Window/Stacks/Auto。决定 Proc 后如何消费增强。")]
        public ConsumeKind consumeKind = ConsumeKind.Single;
        [Tooltip("Window 模式的就绪窗口秒数")]
        public float windowSeconds = 5f;
        [Tooltip("Stacks 模式的最大层数")]
        public int maxStacks = 3;

        // ==================== Effect params ====================
        [Header("效果器参数（category == Effect 或 Universal）")]
        public EffectType effectType;
        [Tooltip("V.08 效果器角色：Enhancement 改核心技能（damage=倍率）；Addon spawn 独立效果（damage=附加伤害）")]
        public EffectRole effectRole = EffectRole.Enhancement;
        public float baseDamage = 25f;
        public float damageScaling = 0.5f;
        public float aoeRadius = 4f;
        public float healAmount = 20f;
        public float healScaling = 0.3f;
        public float shieldAmount = 30f;
        public float buffDuration = 5f;
        public float buffDamageReduction = 0.3f;
        public float projectileSpeed = 15f;
        public int projectileCount = 1;
        public float spreadAngle = 0f;
        public float slowPercent = 0.5f;
        public float stunDuration = 1.5f;
        public float knockbackForce = 8f;
        public float dashDistance = 5f;
        public float pullRadius = 6f;
        public float dotDPS = 8f;
        public float dotDuration = 4f;
        public float invincibleDuration = 1f;
        public float summonDuration = 10f;
        public float summonDamage = 10f;
        public float trapDuration = 8f;
        public float vulnerableMultiplier = 1.5f;
        public float vulnerableDuration = 3f;
        public GameObject vfxPrefab;
        public ElementTag elementTag = ElementTag.None;

        // ==================== Modifier params ====================
        [Header("改造件参数（category == Modifier）")]
        public ModifierType modifierType;
        public float modifierValue = 1.5f;
        public float burnDPS = 5f;
        public float burnDuration = 3f;
        public float freezeDuration = 2f;
        public float lightningDamage = 15f;
        public float poisonDPS = 4f;
        public float poisonDuration = 5f;
        public int extraCount = 1;
        public float costHPPercent = 0.1f;
        public float costDamageBonus = 0.5f;

        // ==================== Universal 双面 ====================
        [Header("万能件参数（category == Universal）")]
        [Tooltip("作为触发器时的触发类型")]
        public TriggerType universalTriggerType;
        public int universalTriggerThreshold = 1;
        public float universalTriggerCooldown = 2f;
        [Tooltip("作为效果器时的效果类型")]
        public EffectType universalEffectType;
        [Tooltip("作为效果器时的角色（Enhancement/Addon）")]
        public EffectRole universalEffectRole = EffectRole.Enhancement;
        [Tooltip("作为触发器时的消费模型")]
        public ConsumeKind universalConsumeKind = ConsumeKind.Single;
        [TextArea(1, 2)]
        public string universalTriggerDesc;
        [TextArea(1, 2)]
        public string universalEffectDesc;

        // ==================== 兼容性 ====================
        [Header("兼容条件")]
        [Tooltip("可连接的形态标签")]
        public ShapeTag compatibleShapes = (ShapeTag)~0;
        [Tooltip("不兼容的功能标签")]
        public FunctionTag incompatibleFunctions;

        /// <summary>获取当模块被当作触发器使用时的 TriggerType</summary>
        public TriggerType GetTriggerTypeForSlot()
        {
            if (category == ModuleCategory.Universal)
                return universalTriggerType;
            return triggerType;
        }

        /// <summary>获取当模块被当作效果器使用时的 EffectType</summary>
        public EffectType GetEffectTypeForSlot()
        {
            if (category == ModuleCategory.Universal)
                return universalEffectType;
            return effectType;
        }

        /// <summary>获取当模块被当作效果器使用时的 EffectRole</summary>
        public EffectRole GetEffectRoleForSlot()
        {
            if (category == ModuleCategory.Universal)
                return universalEffectRole;
            return effectRole;
        }

        /// <summary>获取当模块被当作触发器使用时的 ConsumeKind</summary>
        public ConsumeKind GetConsumeKindForSlot()
        {
            if (category == ModuleCategory.Universal)
                return universalConsumeKind;
            return consumeKind;
        }

        /// <summary>检查是否可放入指定槽位</summary>
        public bool CanFitSlot(int slotPosition)
        {
            return slotPosition switch
            {
                0 => category == ModuleCategory.Trigger || category == ModuleCategory.Universal,
                1 => category == ModuleCategory.Effect || category == ModuleCategory.Universal,
                2 or 3 => category == ModuleCategory.Modifier,
                _ => false
            };
        }
    }
}
