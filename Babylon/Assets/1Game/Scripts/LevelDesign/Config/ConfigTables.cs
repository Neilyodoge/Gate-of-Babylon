using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    // ============================================================
    // GDD §12 配置数据结构。关卡数据由关卡数据库 ScriptableObject 持有，
    // 战斗数据仍由 Combat 配表管线加载。
    // ============================================================

    public enum RoomRole
    {
        [InspectorName("普通战斗房")]
        Battle = 0,
        [InspectorName("精英房")]
        Elite = 1,
        [InspectorName("事件房")]
        Event = 2,
        [InspectorName("商店")]
        Shop = 3,
        [InspectorName("休息房")]
        Rest = 4,
        [InspectorName("首领房")]
        Boss = 5,
        [InspectorName("军械库")]
        Armory = 6,
        [InspectorName("降落房")]
        Landing = 7
    }

    public enum District
    {
        [InspectorName("外环")]
        Outer = 0,
        [InspectorName("连接区")]
        Transition = 1,
        [InspectorName("内环")]
        Inner = 2
    }

    public enum ActivationMode
    {
        [InspectorName("进入房间时")]
        OnEnter = 0,
        [InspectorName("进入核心区域时")]
        OnCoreEnter = 1,
        [InspectorName("交互时")]
        OnInteract = 2,
        [InspectorName("始终激活")]
        AlwaysActive = 3,
        [InspectorName("伏击触发时")]
        OnAmbush = 4
    }

    public enum LockPolicy
    {
        [InspectorName("不锁门")]
        None = 0,
        [InspectorName("战斗期间锁门")]
        CombatLock = 1,
        [InspectorName("事件选择期间锁门")]
        EventChoiceLock = 2,
        [InspectorName("首领战期间锁门")]
        BossLock = 3
    }

    public enum SpawnMode
    {
        [InspectorName("预置休眠")]
        PreplacedDormant = 0,
        [InspectorName("触发后分波")]
        WaveOnTrigger = 1,
        [InspectorName("伏击生成")]
        AmbushSpawn = 2,
        [InspectorName("常驻巡逻")]
        PatrolActive = 3,
        [InspectorName("脚本首领")]
        ScriptedBoss = 4
    }

    /// <summary>GDD §12.2.3 事件类型</summary>
    public enum StoryEventType
    {
        [InspectorName("多项选择")]
        MultiChoice = 1,
        [InspectorName("单项选择")]
        SingleChoice = 2,
        [InspectorName("条件事件")]
        ConditionalEvent = 3
    }

    // ------------------------------------------------------------
    // §12.2.1 Map_Structure_Config — 区域路线结构
    // ------------------------------------------------------------
    [Serializable]
    public class MapStructureRow
    {
        [InspectorName("配置编号")]
        public int ID;
        [InspectorName("秘境编号")]
        public int ActID;
        [InspectorName("各层敌人数值倍率")]
        [Tooltip("数组第1项对应第1层；未配置的层按1倍处理。")]
        public float[] EnemyScaleMul;
        [InspectorName("各层模块稀有度偏移")]
        [Tooltip("数组第1项对应第1层；填写稀有模块权重的百分比增量。")]
        public int[] ModuleRarityBias;
        [InspectorName("各层是否提供阶段返回")]
        [Tooltip("数组第1项对应第1层；0表示没有，1表示提供返回点。")]
        public int[] HasStageReturn;

        public float GetEnemyScale(int floor)
        {
            if (EnemyScaleMul == null || floor < 0 || floor >= EnemyScaleMul.Length) return 1f;
            return EnemyScaleMul[floor];
        }
        public int GetRarityBias(int floor)
        {
            if (ModuleRarityBias == null || floor < 0 || floor >= ModuleRarityBias.Length) return 0;
            return ModuleRarityBias[floor];
        }
        public bool GetHasStageReturn(int floor)
        {
            if (HasStageReturn == null || floor < 0 || floor >= HasStageReturn.Length) return false;
            return HasStageReturn[floor] != 0;
        }
    }

    [Serializable]
    public class RoomContentRow
    {
        [InspectorName("内容编号")]
        public int ID;
        [InspectorName("中文名称")]
        public string Name_CN;
        [InspectorName("房间类型")]
        public RoomRole Role;
        [InspectorName("所属分区")]
        public District District;
        [InspectorName("激活方式")]
        public ActivationMode ActivationMode;
        [InspectorName("锁门规则")]
        public LockPolicy LockPolicy;
        [InspectorName("遭遇编号")]
        [Tooltip("战斗房、精英房和首领房引用的战斗遭遇编号。")]
        public int ContentConfigID;
        [InspectorName("剧情事件编号")]
        [Tooltip("事件房引用的剧情事件编号；非事件房填写0。")]
        public int EventID;
        [InspectorName("事件触发概率")]
        [Range(0, 100)]
        public int EventTriggerRate;
        [InspectorName("房间标签")]
        [Tooltip("该内容要求房间模板拥有的标签；全部命中才进入候选，标签越专用优先级越高。")]
        public string[] PrefabTags;
        [InspectorName("最小图深度")]
        public int MinGraphDepth;
        [InspectorName("最大图深度")]
        public int MaxGraphDepth = 999;
        [InspectorName("抽取权重")]
        public int Weight = 100;

        public RoomRole RoleEnum => Role;
        public District DistrictEnum => District;
        public ActivationMode ActivationModeEnum => ActivationMode;
        public LockPolicy LockPolicyEnum => LockPolicy;
    }

    [Serializable]
    public class EncounterRow
    {
        [InspectorName("遭遇编号")]
        public int ID;
        [InspectorName("中文名称")]
        public string Name_CN;
        [InspectorName("刷怪方式")]
        public SpawnMode SpawnMode;
        [InspectorName("最大波数")]
        [Range(1, 2)]
        public int MaxWaves = 1;
        [InspectorName("增援触发剩余比例")]
        [Range(0, 100)]
        public int ReinforceAtPct;
        [InspectorName("增援延迟（秒）")]
        [Min(0f)]
        public float ReinforceDelaySec = 0.75f;
        [InspectorName("预置怪物数量")]
        [Min(0)]
        public int PreplacedCount;
        [InspectorName("中文备注")]
        [TextArea]
        public string Notes_CN;

        public SpawnMode SpawnModeEnum => SpawnMode;
    }

    // ------------------------------------------------------------
    // §12.2.3 Event_Story_Config — 事件配置
    // ------------------------------------------------------------
    [Serializable]
    public class EventOption
    {
        [InspectorName("选项文字")]
        public string Text;
        [InspectorName("写入的事件标记")]
        public string FlagName;
        [InspectorName("标记值")]
        public int FlagValue;
        [InspectorName("奖励编号")]
        public int RewardID;
        [InspectorName("消耗编号")]
        public int CostID;
        [InspectorName("因果变化")]
        public int KarmaChange;
        [InspectorName("道心变化")]
        public int DaoxinChange;
        [InspectorName("寿元变化")]
        public int LifespanChange;
        [InspectorName("本局场景结果")]
        public EventSceneResult SceneResult;
    }

    public enum EventSceneResult
    {
        [InspectorName("无场景变化")]
        None = 0,
        [InspectorName("开启通路")]
        OpenRoute = 1,
        [InspectorName("设施供能")]
        Power = 2,
        [InspectorName("封存危险")]
        Seal = 3,
        [InspectorName("桥梁机构已破坏")]
        BridgeSabotaged = 4,
        [InspectorName("召集阵核心已摧毁")]
        SummonArrayDestroyed = 5,
        [InspectorName("召集阵外环已破坏")]
        SummonArrayOuterBroken = 6,
        [InspectorName("冠光仪主镜已摧毁")]
        CrownLightDisabled = 7,
        [InspectorName("冠光仪镜组已偏转")]
        CrownLightMisaligned = 8,
        [InspectorName("永夜升降井已修复")]
        NightLiftRestored = 9,
        [InspectorName("永夜升降井已坠落")]
        NightLiftDropped = 10,
    }

    public enum StoryEventImpactKind
    {
        [InspectorName("单局强度 / Boss机制")]
        Strength = 0,
        [InspectorName("关卡布局")]
        Layout = 1,
        [InspectorName("道具获取")]
        Item = 2,
    }

    [Serializable]
    public class StoryEventRow
    {
        [InspectorName("事件编号")]
        public int ID;
        [InspectorName("事件名称")]
        public string Name_CN;
        [InspectorName("事件类型")]
        public StoryEventType Type;
        [InspectorName("主要玩法影响")]
        public StoryEventImpactKind ImpactKind;
        [InspectorName("前置条件")]
        [Tooltip("内部条件表达式；留空表示无前置条件。")]
        public string PrereqFlag;
        [InspectorName("事件正文")]
        [TextArea(3, 8)]
        public string Text_CN;
        [InspectorName("玩家选项")]
        public EventOption[] Options;

        public StoryEventType TypeEnum => Type;
    }

    // ------------------------------------------------------------
    // §12.3.1 Boss_Phase_Config — Boss 形态配置
    // ------------------------------------------------------------
    [Serializable]
    public class BossPhaseRow
    {
        [InspectorName("阶段编号")]
        public int ID;
        [InspectorName("首领编号")]
        public int BossID;
        [InspectorName("阶段名称")]
        public string PhaseName;
        [InspectorName("需要的事件条件")]
        [Tooltip("条件表达式；支持多个条件同时成立，留空表示无前置条件。")]
        public string RequiredFlags;
        [InspectorName("选择优先级")]
        public int Priority;
        [InspectorName("登场对白")]
        public string[] DialogueLines;
        [InspectorName("属性修正")]
        [Tooltip("内部表达式，例如生命、攻击和速度倍率。")]
        public string StatModifier;
    }

    // ------------------------------------------------------------
    // §6.9 Skill_Base_Config — 主动技能
    // ------------------------------------------------------------
    [Serializable]
    public class SkillBaseRow
    {
        public int ID;
        public string Name_CN;
        public string Desc_CN;
        /// <summary>品阶：1凡/2灵/3玄/4地/5天（v0.5.6 表新增列）</summary>
        public int Rarity;
        /// <summary>1=基础(伤害) / 2=增益 / 3=减益 / 4=特殊(暂不实现)</summary>
        public int Type;
        public float BaseCooldown;
        /// <summary>基础伤害 / 效果倍率：百分比与权重均以 10000=100% 计；纯数值直接填</summary>
        public int BaseDamageRatio;
        public string IconPath;
    }

    // ------------------------------------------------------------
    // §6.9-3 Skill_Param_Config — 技能参数仓库表（V0.1.18d）
    // 主键 ConfigId = Skill_Base_Config.ID（= SkillData.configId）。承载 SkillData SO 的
    // 完整数值/开关字段（充能/蓄力/投射/位移/治疗/召唤/Buff/Zone 等），主表只放身份/CD/伤害。
    // 均自 SkillData 资产真实导出；资产引用类字段（icon/prefab/vfx/audio/modifierDefs）不入表。
    // ------------------------------------------------------------
    [Serializable]
    public class SkillParamRow
    {
        public int ConfigId;
        public string Name_CN;
        public int SkillType;
        public int Element;
        public int Rarity;
        public float BaseDamage;
        public float DamageScaling;
        public float Cooldown;
        public float CastSpeed;
        public int MaxCharges;
        public float ChargeTime;
        public int CanCharge;
        public float ChargeLv2Time;
        public float ChargeLv3Time;
        public float ChargeLv2DmgMul;
        public float ChargeLv3DmgMul;
        public float ChargeLv2RadMul;
        public float ChargeLv3RadMul;
        public float ChargeMoveMul;
        public float AoeRadius;
        public float ProjectileSpeed;
        public int ProjectileCount;
        public float SpreadAngle;
        public float DashDistance;
        public int LeaveTrail;
        public float HealAmount;
        public float HealScaling;
        public float SummonDuration;
        public float SummonDamage;
        public int SummonIsDecoy;
        public float BuffDuration;
        public float BuffAtkSpeedPct;
        public float BuffMoveSpeedPct;
        public float BuffAtkPct;
        public float BuffDamageReduction;
        public float FreezeOnHitChance;
        public float FreezeOnHitDuration;
        public int DamageFromRunTotal;
        public float RunTotalDamageRatio;
        public int DashInvulnerable;
        public float DashInvulnDuration;
        public int ArmLethalGuard;
        public float LethalGuardDuration;
        public int HeavenEarthShift;
        public float ZoneDuration;
        public float ZoneRadius;
        public float ZoneTickInterval;
        public float ZoneDamagePerTick;
        public float ZoneSlowPct;
        public float ZonePullSpeed;
        public int ZoneFollowPlayer;
        public float ZoneBurnDPS;
        public int PlayAnimation;
        public float VfxDuration;
    }

    // ------------------------------------------------------------
    // §5.7 Module_Base_Config — 模块配置主表（V0.1.14）
    // 与 ModuleDef SO 字段一一对应，供策划以 CSV 批量查看/编辑。
    // 枚举列均以 int 存储（数值对照见 Combat_Table_Index.csv 图例）：
    //   Category:   0=Trigger 1=Effect 2=Modifier 3=Universal
    //   SubType:    按 Category 取 TriggerType/EffectType/ModifierType 的枚举序号；Universal 取 UniversalTriggerType
    //   Rarity:     0=凡 1=灵 2=玄 3=地 4=天
    //   ConsumeKind:0=Single 1=Window 2=Stacks 3=Auto
    //   EffectRole: 0=Enhancement(增强型) 1=Addon(附加型)
    //   FuncTags/ShapeTags/StyleTags: 位标志（Flags）整数值
    //   Element:    ElementTag 枚举序号
    // 说明：本表为 §5.7 单一真源快照，运行时模块系统当前仍以 SO 为准（GDD §11.3 wire 深度）。
    // ------------------------------------------------------------
    [Serializable]
    public class ModuleBaseRow
    {
        public string ModuleId;
        public string Name_CN;
        public int Category;
        public int SubType;
        public int Rarity;
        public int FuncTags;
        public int ShapeTags;
        public int StyleTags;
        public int ConsumeKind;
        public float WindowSeconds;
        public int MaxStacks;
        public int EffectRole;
        public int Threshold;
        public float Cooldown;
        public float Interval;
        public float BaseDamage;
        public float DamageScaling;
        public float AoeRadius;
        public int Element;
        public float ModifierValue;
        /// <summary>万能件作触发器时的 TriggerType；非万能件为 -1</summary>
        public int UniTrigType;
        /// <summary>万能件作效果器时的 EffectType；非万能件为 -1</summary>
        public int UniEffType;
        /// <summary>掉落来源（§5.7 新增策划字段，SO 未含）</summary>
        public string DropSource;
        /// <summary>解锁条件（§5.7 新增策划字段，SO 未含）</summary>
        public string UnlockCond;
        public string Desc_CN;
    }

    // ------------------------------------------------------------
    // §5.7 模块参数仓库表（V0.1.18b）—— 按 ModuleId 关联 Module_Base_Config，
    // 拆出各大类的完整数值参数（主表只放身份/标签/关键参数）。均从 ModuleDef 真实导出。
    // ------------------------------------------------------------
    [Serializable]
    public class ModuleTriggerParamRow
    {
        public string ModuleId;
        public int TriggerType;
        public int Threshold;
        public float Cooldown;
        public float Interval;
        /// <summary>是否消耗层数（1=true/0=false）</summary>
        public int ConsumeStacks;
        public float MoveDistanceThreshold;
        public float HealthThreshold;
        public int ConsumeKind;
        public float WindowSeconds;
        public int MaxStacks;
    }

    [Serializable]
    public class ModuleEffectParamRow
    {
        public string ModuleId;
        public int EffectType;
        public int EffectRole;
        public float BaseDamage;
        public float DamageScaling;
        public float AoeRadius;
        public int Element;
        public float HealAmount;
        public float HealScaling;
        public float ShieldAmount;
        public float BuffDuration;
        public float BuffDamageReduction;
        public float ProjectileSpeed;
        public int ProjectileCount;
        public float SpreadAngle;
        public float SlowPercent;
        public float StunDuration;
        public float KnockbackForce;
        public float DashDistance;
        public float PullRadius;
        public float DotDPS;
        public float DotDuration;
        public float InvincibleDuration;
        public float SummonDuration;
        public float SummonDamage;
        public float TrapDuration;
        public float VulnerableMultiplier;
        public float VulnerableDuration;
    }

    [Serializable]
    public class ModuleModifierParamRow
    {
        public string ModuleId;
        public int ModifierType;
        public float ModifierValue;
        public float BurnDPS;
        public float BurnDuration;
        public float FreezeDuration;
        public float LightningDamage;
        public float PoisonDPS;
        public float PoisonDuration;
        public int ExtraCount;
        public float CostHPPercent;
        public float CostDamageBonus;
    }

    [Serializable]
    public class ModuleUniversalParamRow
    {
        public string ModuleId;
        public int UniTriggerType;
        public int UniTriggerThreshold;
        public float UniTriggerCooldown;
        public int UniEffectType;
        public int UniEffectRole;
        public int UniConsumeKind;
        public string TriggerDesc;
        public string EffectDesc;
    }

    // ------------------------------------------------------------
    // §7.3 Enemy_Base_Config — 敌人分类基础表（V0.1.14）
    // 记录各敌人类型相对 GameConfig「敌人基础血量/攻击力/防御力」的倍率与 AI 参数。
    // 现值抽取自 Enemy* 脚本硬编码；本表为设计参考/未来读表来源，运行时当前仍走脚本+GameConfig。
    //   实际属性 = GameConfig.敌人基础值 × 本行 *Mul；精英/Boss 倍率精英取自 GameConfig 精英怪倍率。
    // ------------------------------------------------------------
    [Serializable]
    public class EnemyBaseRow
    {
        public int ID;
        /// <summary>类型键：Normal/Mage/Ranged/Charger/Elite/Boss</summary>
        public string TypeKey;
        public string Name_CN;
        public float HpMul;
        public float DmgMul;
        public float DefMul;
        public float MoveSpeed;
        public float DetectRange;
        public float AttackRange;
        public float AttackInterval;
        /// <summary>类型专属参数（投射/冲锋速度、AOE 半径等，自由文本）</summary>
        public string SpecialParam;
        public string Behavior_CN;
        public string Desc_CN;
    }

    // ------------------------------------------------------------
    // §5.6 ConsumeKind_Bonus_Config — 消费模型身份加成三角（V0.1.14）
    // 现值抽取自 ModuleChain 常量；运行时当前仍用常量，本表为设计真源/未来读表来源。
    // ------------------------------------------------------------
    [Serializable]
    public class ConsumeKindBonusRow
    {
        public int ID;
        /// <summary>Single/Window/Stacks/Auto（与 ConsumeKind 枚举同名）</summary>
        public string ConsumeKind;
        public string Name_CN;
        public float DamageMul;
        public float RadiusMul;
        public string Note_CN;
    }

    // ------------------------------------------------------------
    // JSON 顶层包装器（Unity JsonUtility 不支持顶级数组）
    // ------------------------------------------------------------
    [Serializable] public class SkillBaseTable { public SkillBaseRow[] Rows; }
    [Serializable] public class SkillParamTable { public SkillParamRow[] Rows; }
    [Serializable] public class ModuleBaseTable { public ModuleBaseRow[] Rows; }
    [Serializable] public class ModuleTriggerParamTable { public ModuleTriggerParamRow[] Rows; }
    [Serializable] public class ModuleEffectParamTable { public ModuleEffectParamRow[] Rows; }
    [Serializable] public class ModuleModifierParamTable { public ModuleModifierParamRow[] Rows; }
    [Serializable] public class ModuleUniversalParamTable { public ModuleUniversalParamRow[] Rows; }
    [Serializable] public class EnemyBaseTable { public EnemyBaseRow[] Rows; }
    [Serializable] public class ConsumeKindBonusTable { public ConsumeKindBonusRow[] Rows; }
}
