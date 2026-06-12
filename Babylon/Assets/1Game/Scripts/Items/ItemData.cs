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
    /// 灵物分类（GDD §5.2 V.05 重构：3 类局内灵物 + 功法 + 洞府素材）。
    /// 旧分类（Attack/Defense/Movement/Anomaly）已弃用。
    /// </summary>
    public enum ItemCategory
    {
        // === GDD §5.2 V.05 新分类 ===
        StatStacking    = 0,  // ⚔️ 数值堆叠类（攻击力/生命值等纯数值强化）
        MechanicEnhance = 1,  // 🛡️ 机制增强类（强化现有机制的数值，如冰冻+0.5s）
        MechanicModify  = 2,  // 🔮 机制修改类（改变机制逻辑，如攻击附带剑气）

        Skill           = 5,  // 📜 功法

        // === 洞府素材类（搜打撤核心，需活着撤离才能带回洞府）===
        Herb          = 6,  // 🌿 灵药
        Ore           = 7,  // 🪨 灵矿
        BeastMaterial = 8,  // 🐉 妖兽材料
        ScripturePage = 9,  // 📃 古籍残页
        PlantSeed     = 10, // 🌱 灵植种子
        ArraySigil    = 11, // 🪶 阵法符

        // === 向后兼容别名（旧 SO 读取不报错，运行时等同新类型）===
        [System.Obsolete("Use StatStacking")] Attack   = 20,
        [System.Obsolete("Use MechanicEnhance")] Defense  = 21,
        [System.Obsolete("Use StatStacking")] Movement = 22,
        [System.Obsolete("Use MechanicModify")] Anomaly  = 23
    }

    /// <summary>
    /// 灵物用途分类（v0.5 搜打撤核心）。
    /// - <see cref="RunOnly"/>：战斗中捡到立即生效，整局参与 build；梦醒消失。
    /// - <see cref="CaveMaterial"/>：战斗中捡到背在背包；必须活着撤离才能带回洞府转化为永久资源；死亡丢失。
    /// </summary>
    public enum ItemScope
    {
        /// <summary>局内灵物：捡到立即生效，梦醒消失（现有 17 件灵物默认归这里）</summary>
        RunOnly = 0,
        /// <summary>洞府素材：撤离才能带回，死亡丢失（灵药 / 灵矿 / 妖兽材料 / 古籍残页 / 灵植种子 / 阵法符）</summary>
        CaveMaterial = 1
    }

    /// <summary>
    /// 灵物数据定义（ScriptableObject，数据驱动）
    /// 所有灵物的静态数据都在这里定义
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "仙途秘境/灵物数据")]
    public class ItemData : ScriptableObject
    {
        [Header("基础信息")]
        /// <summary>对接 Item_InRun_Config 的行 ID（0=不接表）。用于 Avatar 的 DefaultItem_ID / Restriction 匹配。</summary>
        public int configId = 0;
        public string itemName = "未命名灵物";
        [TextArea(2, 4)]
        public string description = "灵物描述";
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Fan;
        public ItemCategory category = ItemCategory.StatStacking;
        /// <summary>
        /// v0.5 搜打撤核心：用途分类。
        /// 决定拾取后进入"局内背包（ItemInventory）" 还是"洞府素材背包（CaveInventory）"，
        /// 后者需要活着撤离才能持久化保留，死亡则全部丢失。
        /// </summary>
        [Tooltip("v0.5 搜打撤分类：局内灵物（梦醒消失）vs 洞府素材（撤离才能带回，死亡丢失）")]
        public ItemScope scope = ItemScope.RunOnly;
        /// <summary>
        /// 加工后的产物 itemName（v0.5 Week 8 技术债清理）。
        /// 链式管线：种子(<see cref="ItemCategory.PlantSeed"/>) → 灵药(<see cref="ItemCategory.Herb"/>)。
        /// 留空表示该物品没有下游产物（如丹药本身，或纯展示型素材）。
        /// 替代旧的 <c>seedName.Replace("种子","灵药")</c> 字符串硬替换。
        /// </summary>
        [Tooltip("加工链下游产物的 itemName（种子→灵药→丹药）。留空表示无下游产物。")]
        public string processedProductName = "";
        /// <summary>
        /// 元素 / 类型标签（GDD 5.6 元素反应、GDD 6.5 技能修饰共用）。
        /// 装入技能槽时，与该技能 modifierDefs 匹配可激活变体。
        /// </summary>
        [Tooltip("用于技能修饰匹配（GDD 6.5）和元素反应（GDD 5.6）")]
        public ElementTag modTag = ElementTag.None;

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
