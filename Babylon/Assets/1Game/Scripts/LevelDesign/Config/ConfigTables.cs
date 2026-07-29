using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    // ============================================================
    // GDD §12 表格数据结构定义 —— 与 JSON 文件一一对应
    // 所有表格放在 Resources/LevelDesign/ 下，文件名同 typeof(T).Name
    // ============================================================

    /// <summary>GDD §12.1.4 房间类型（数字编码）</summary>
    public enum LevelRoomType
    {
        Start = 0,
        Battle = 1,
        Elite = 2,
        Shop = 3,
        Event = 4,
        Boss = 5
    }

    /// <summary>GDD §12.2.3 事件类型</summary>
    public enum StoryEventType
    {
        MultiChoice = 1,
        SingleChoice = 2,
        ConditionalEvent = 3
    }

    // ------------------------------------------------------------
    // §12.2.1 Map_Structure_Config — 区域路线结构
    // ------------------------------------------------------------
    [Serializable]
    public class MapStructureRow
    {
        public int ID;
        public int ActID;
        public int MaxFloor;
        public int MinNodes;
        public int MaxNodes;
        public int NormalWeight = 75;
        public int SpecialWeight = 25;
        public int EliteMinCount;
        public int EliteMaxCount;
        public int EventMinCount;
        public int ShopMinCount;
        /// <summary>引用 Room_Socket_Group_Config 的 ID 列表</summary>
        public int[] RoomPoolID;
        /// <summary>每层敌人数值缩放倍率（长度=MaxFloor，缺省层=1.0）</summary>
        public float[] EnemyScaleMul;
        /// <summary>每层模块掉落稀有度偏移（Rare/Epic 权重百分比提升，缺省=0）</summary>
        public int[] ModuleRarityBias;
        /// <summary>每层结束后是否有阶段返回点（0=无，1=有）</summary>
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

    // ------------------------------------------------------------
    // §12.2.2 Room_Socket_Group_Config — 房间内容配置
    // ------------------------------------------------------------
    [Serializable]
    public class RoomSocketRow
    {
        public int ID;
        public string SceneName;
        public int RoomType;
        public int[] EnemySquadID;
        public int[] ItemDropIDs;
        public int[] ItemDropWeights;
        public int EventID;
        public int EventTriggerRate;
        public int Weight = 100;

        public LevelRoomType TypeEnum => (LevelRoomType)RoomType;
    }

    // ------------------------------------------------------------
    // §12.2.3 Event_Story_Config — 事件配置
    // ------------------------------------------------------------
    [Serializable]
    public class EventOption
    {
        public string Text;
        public string FlagName;
        public int FlagValue;
        public int RewardID;
        public int CostID;
        public int KarmaChange;
        public int DaoxinChange;
        public int LifespanChange;
    }

    [Serializable]
    public class StoryEventRow
    {
        public int ID;
        public string Name_CN;
        public int Type;
        public string PrereqFlag;
        public string Text_CN;
        public EventOption[] Options;

        public StoryEventType TypeEnum => (StoryEventType)Type;
    }

    // ------------------------------------------------------------
    // §12.3.1 Boss_Phase_Config — Boss 形态配置
    // ------------------------------------------------------------
    [Serializable]
    public class BossPhaseRow
    {
        public int ID;
        public int BossID;
        public string PhaseName;
        /// <summary>格式：flagA=1&amp;flagB>=2，支持 AND 组合；空 = 无前置</summary>
        public string RequiredFlags;
        public int Priority;
        public string[] DialogueLines;
        public int SkillSetID;
        /// <summary>格式：hp*1.2,atk*1.5,spd*0.8</summary>
        public string StatModifier;
        public int SummonSquadID;
    }

    // ------------------------------------------------------------
    // §12.2.3 Material_CaveRes_Config — 洞府素材
    // ------------------------------------------------------------
    [Serializable]
    public class MaterialCaveResRow
    {
        public int ID;
        public string Name_CN;
        public string Text_CN;
        /// <summary>1=灵植种子 / 2=灵药 / 3=灵矿 / 4=妖兽材料 / 5=古籍残页 / 6=阵法符</summary>
        public int Type;
        public string Icon;
        public int MaxStack = 99;
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
    // §6.9-2 Skill_Effect_Config — 被动 / BUFF / DEBUFF / 效果总库
    // ------------------------------------------------------------
    [Serializable]
    public class SkillEffectRow
    {
        public int ID;
        public string Name_CN;
        public string Desc_CN;
        /// <summary>1=BUFF / 2=被动 / 3=DEBUFF / 4=特殊被动(暂不实现)</summary>
        public int Type;
        public float BaseCooldown;
        public int BaseDamageRatio;
        /// <summary>可叠加层数上限（0=不可叠加）。技能型可叠加，与灵物叠加/质变分开结算（Q4）</summary>
        public int Charges;
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
    [Serializable] public class MapStructureTable { public MapStructureRow[] Rows; }
    [Serializable] public class RoomSocketTable { public RoomSocketRow[] Rows; }
    [Serializable] public class StoryEventTable { public StoryEventRow[] Rows; }
    [Serializable] public class BossPhaseTable { public BossPhaseRow[] Rows; }
    [Serializable] public class MaterialCaveResTable { public MaterialCaveResRow[] Rows; }
    [Serializable] public class SkillBaseTable { public SkillBaseRow[] Rows; }
    [Serializable] public class SkillEffectTable { public SkillEffectRow[] Rows; }
    [Serializable] public class SkillParamTable { public SkillParamRow[] Rows; }
    [Serializable] public class ModuleBaseTable { public ModuleBaseRow[] Rows; }
    [Serializable] public class ModuleTriggerParamTable { public ModuleTriggerParamRow[] Rows; }
    [Serializable] public class ModuleEffectParamTable { public ModuleEffectParamRow[] Rows; }
    [Serializable] public class ModuleModifierParamTable { public ModuleModifierParamRow[] Rows; }
    [Serializable] public class ModuleUniversalParamTable { public ModuleUniversalParamRow[] Rows; }
    [Serializable] public class EnemyBaseTable { public EnemyBaseRow[] Rows; }
    [Serializable] public class ConsumeKindBonusTable { public ConsumeKindBonusRow[] Rows; }
}
