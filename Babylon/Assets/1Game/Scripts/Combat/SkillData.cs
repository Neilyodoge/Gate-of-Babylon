using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 技能类型
    /// </summary>
    public enum SkillType
    {
        AreaDamage,     // 范围伤害（落石术）
        Projectile,     // 投射物（御剑术）
        Dash,           // 位移（土遁术、缩地成寸）
        Buff            // 增益（金钟罩）
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

        [Header("范围伤害参数")]
        public float aoeRadius = 3f;

        [Header("投射物参数")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 12f;

        [Header("表现")]
        public GameObject vfxPrefab;
        public float vfxDuration = 1.5f;

        [Header("音效")]
        /// <summary>技能释放音效（为空则使用 AudioConfig 中的通用技能音效）</summary>
        public AudioClip castSFX;
        /// <summary>技能命中音效（为空则使用 AudioConfig 中的通用命中音效）</summary>
        public AudioClip hitSFX;
    }
}
