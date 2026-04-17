using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 技能类型
    /// </summary>
    public enum SkillType
    {
        AreaDamage,     // 范围伤害（落石术、天雷引、寒冰诀）
        Projectile,     // 投射物（御剑术、烈焰掌）
        Dash,           // 位移（土遁术、缩地成寸）
        Buff,           // 增益（金钟罩）
        Heal,           // 治疗（回春术）
        Summon          // 召唤（傀儡术）
    }

    /// <summary>
    /// 功法（技能）数据定义
    /// Demo1 采用纯 CD 模型，不涉及灵力消耗
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "仙途梦境/功法数据")]
    public class SkillData : ScriptableObject
    {
        [Header("基础信息")]
        public string skillName = "未命名功法";
        [TextArea(2, 4)]
        public string description = "功法描述";
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Fan;
        public SkillType skillType = SkillType.AreaDamage;

        [Header("数值")]
        /// <summary>基础伤害</summary>
        public float baseDamage = 30f;
        /// <summary>攻击力缩放比例</summary>
        public float damageScaling = 0.5f;
        /// <summary>冷却时间（秒）</summary>
        public float cooldown = 8f;
        /// <summary>释放速度倍率（1.0 = 默认速度，越大越快）</summary>
        [Range(0.5f, 3f)]
        public float castSpeed = 1f;

        [Header("充能")]
        /// <summary>最大充能层数（1=无充能，普通CD；2+=多层充能）</summary>
        [Range(1, 5)]
        public int maxCharges = 1;
        /// <summary>每层充能恢复时间（秒），0=使用cooldown值</summary>
        public float chargeTime = 0f;

        [Header("范围伤害参数")]
        public float aoeRadius = 3f;

        [Header("投射物参数")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 12f;
        /// <summary>投射物数量（多发投射，如散射飞剑）</summary>
        public int projectileCount = 1;
        /// <summary>多发投射时的扩散角度</summary>
        public float spreadAngle = 0f;

        [Header("位移参数")]
        /// <summary>位移距离</summary>
        public float dashDistance = 8f;
        /// <summary>位移后是否留下伤害区域</summary>
        public bool leaveTrail = false;

        [Header("治疗参数")]
        /// <summary>治疗量</summary>
        public float healAmount = 30f;
        /// <summary>治疗量攻击力缩放</summary>
        public float healScaling = 0.3f;

        [Header("召唤参数")]
        /// <summary>召唤物持续时间</summary>
        public float summonDuration = 8f;
        /// <summary>召唤物攻击力</summary>
        public float summonDamage = 10f;

        [Header("表现")]
        /// <summary>是否播放技能动作动画（false = 无动作直接释放）</summary>
        public bool playAnimation = false;
        public GameObject vfxPrefab;
        public float vfxDuration = 1.5f;

        [Header("音效")]
        /// <summary>技能释放音效（为空则使用 AudioConfig 中的通用技能音效）</summary>
        public AudioClip castSFX;
        /// <summary>技能命中音效（为空则使用 AudioConfig 中的通用命中音效）</summary>
        public AudioClip hitSFX;
    }
}
