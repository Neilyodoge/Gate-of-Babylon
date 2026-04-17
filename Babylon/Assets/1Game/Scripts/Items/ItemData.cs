using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物品阶
    /// </summary>
    public enum ItemRarity
    {
        Fan,    // 凡品 ⚪白 50%
        Ling,   // 灵品 🟢绿 30%
        Xuan,   // 玄品 🔵蓝 15%
        Di,     // 地品 🟣紫 4%
        Tian    // 天品 🟡金 1%
    }

    /// <summary>
    /// 灵物分类（仅用于内部标签和掉落权重）
    /// </summary>
    public enum ItemCategory
    {
        Attack,     // ⚔️ 攻伐灵物
        Defense,    // 🛡️ 护体灵物
        Movement,   // 👟 身法灵物
        Anomaly,    // 🔮 异变灵物
        Pill,       // 💊 丹药
        Skill       // 📜 功法
    }

    /// <summary>
    /// 灵物数据定义（ScriptableObject，数据驱动）
    /// 所有灵物的静态数据都在这里定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "仙途梦境/灵物数据")]
    public class ItemData : ScriptableObject
    {
        [Header("基础信息")]
        public string itemName = "未命名灵物";
        [TextArea(2, 4)]
        public string description = "灵物描述";
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Fan;
        public ItemCategory category = ItemCategory.Attack;

        [Header("叠加规则")]
        /// <summary>是否可叠加（功法类不可叠加，最多3个不同功法）</summary>
        public bool stackable = true;
        /// <summary>质变阈值列表（如 3, 5 表示3个和5个时触发质变）</summary>
        public int[] qualitativeThresholds = { 3, 5 };

        [Header("效果参数")]
        /// <summary>攻击力加成（绝对值）</summary>
        public float attackBonus = 0f;
        /// <summary>攻击力加成（百分比）</summary>
        public float attackBonusPercent = 0f;
        /// <summary>最大生命加成（绝对值）</summary>
        public float maxHpBonus = 0f;
        /// <summary>最大生命加成（百分比）</summary>
        public float maxHpBonusPercent = 0f;
        /// <summary>移速加成（百分比）</summary>
        public float moveSpeedBonusPercent = 0f;
        /// <summary>减伤加成（绝对值，0~1）</summary>
        public float damageReductionBonus = 0f;
        /// <summary>暴击率加成</summary>
        public float critRateBonus = 0f;
        /// <summary>攻速加成（百分比）</summary>
        public float attackSpeedBonusPercent = 0f;
        /// <summary>穿透次数加成</summary>
        public int pierceBonus = 0;
        /// <summary>投射物速度加成（百分比）</summary>
        public float projectileSpeedBonusPercent = 0f;

        [Header("特殊效果")]
        /// <summary>攻击附带灼烧（每秒伤害）</summary>
        public float burnDamagePerSecond = 0f;
        /// <summary>受击冻结概率</summary>
        public float freezeChance = 0f;
        /// <summary>击杀回复生命</summary>
        public float healOnKill = 0f;

        [Header("充能与蓄力")]
        /// <summary>技能充能层数加成（+1=额外增加1层充能上限，仅对同槽位技能生效）</summary>
        public int skillChargeBonus = 0;
        /// <summary>蓄力速度加成（百分比，0.2=蓄力速度+20%，即蓄力时间缩短）</summary>
        public float chargeSpeedBonusPercent = 0f;
        /// <summary>蓄力伤害额外加成（百分比，0.15=蓄力伤害额外+15%）</summary>
        public float chargeDamageBonusPercent = 0f;
        /// <summary>CD缩减（百分比，0.1=CD缩短10%）</summary>
        public float cooldownReductionPercent = 0f;

        [Header("功法关联（仅功法类灵物）")]
        /// <summary>关联的功法数据（拾取后可装备到技能槽位）</summary>
        public SkillData linkedSkill;

        [Header("灵物槽位效果")]
        /// <summary>是否仅在装入灵物槽时生效（false=拾取即生效，true=需要放入槽位）</summary>
        public bool requiresSlot = false;
        /// <summary>是否针对特定技能生效（true=针对同槽位技能，false=全局生效）</summary>
        public bool isSkillSpecific = false;

        [Header("拾取表现")]
        public GameObject pickupVfxPrefab;
        /// <summary>拾取音效（为空则使用 AudioConfig 中按品阶的通用拾取音效）</summary>
        public AudioClip pickupSFX;

        /// <summary>
        /// 获取品阶对应的颜色
        /// </summary>
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ItemRarity.Fan => Color.white,
                ItemRarity.Ling => Color.green,
                ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                _ => Color.white
            };
        }
    }
}
