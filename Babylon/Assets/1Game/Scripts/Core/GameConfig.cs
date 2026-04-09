using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 全局游戏配置 —— ScriptableObject
    /// 集中管理所有可调属性，方便在 Inspector 中快速修改
    /// 菜单：Assets → Create → 仙途梦境 → 游戏配置
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "仙途梦境/游戏配置")]
    public class GameConfig : ScriptableObject
    {
        // ========== 单例访问 ==========
        private static GameConfig _instance;
        public static GameConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<GameConfig>("GameConfig");
                return _instance;
            }
        }

        // ==================== 玩家属性 ====================
        [Header("═══ 玩家基础属性 ═══")]
        [Tooltip("最大生命值")]
        public float playerMaxHp = 100f;

        [Tooltip("攻击力")]
        public float playerAttackDamage = 10f;

        [Tooltip("攻击速度倍率（1.0 = 基础速度）")]
        [Range(0.5f, 3f)]
        public float playerAttackSpeed = 1f;

        [Tooltip("移动速度")]
        [Range(2f, 15f)]
        public float playerMoveSpeed = 6f;

        [Tooltip("暴击率")]
        [Range(0f, 1f)]
        public float playerCritRate = 0.05f;

        [Tooltip("暴击伤害倍率")]
        [Range(1f, 5f)]
        public float playerCritDamage = 1.5f;

        [Tooltip("减伤比例")]
        [Range(0f, 0.9f)]
        public float playerDamageReduction = 0f;

        // ==================== 闪避 ====================
        [Header("═══ 闪避 ═══")]
        [Tooltip("闪避距离")]
        [Range(2f, 10f)]
        public float dashDistance = 5f;

        [Tooltip("闪避持续时间（秒）")]
        [Range(0.1f, 0.5f)]
        public float dashDuration = 0.2f;

        [Tooltip("闪避冷却时间（秒）")]
        [Range(0.5f, 5f)]
        public float dashCooldown = 1.5f;

        // ==================== 敌人属性 ====================
        [Header("═══ 敌人基础属性 ═══")]
        [Tooltip("敌人基础血量")]
        public float enemyBaseHp = 30f;

        [Tooltip("敌人基础攻击力")]
        public float enemyBaseAttack = 5f;

        [Tooltip("敌人移动速度")]
        [Range(1f, 8f)]
        public float enemyMoveSpeed = 3f;

        [Tooltip("敌人检测范围")]
        [Range(5f, 25f)]
        public float enemyDetectRange = 12f;

        [Tooltip("敌人攻击范围")]
        [Range(1f, 5f)]
        public float enemyAttackRange = 1.5f;

        [Tooltip("敌人攻击间隔（秒）")]
        [Range(0.5f, 5f)]
        public float enemyAttackInterval = 1.5f;

        // ==================== 难度曲线 ====================
        [Header("═══ 难度曲线 ═══")]
        [Tooltip("基础敌人数量")]
        public int baseEnemyCount = 3;

        [Tooltip("每层增加的敌人数")]
        public int enemyCountPerLevel = 2;

        [Tooltip("每层血量倍率增长")]
        [Range(0.1f, 1f)]
        public float hpScalePerLevel = 0.3f;

        [Tooltip("每层伤害倍率增长")]
        [Range(0.1f, 1f)]
        public float dmgScalePerLevel = 0.2f;

        // ==================== 房间 ====================
        [Header("═══ 房间尺寸 ═══")]
        [Tooltip("基础房间大小")]
        [Range(20f, 50f)]
        public float baseRoomSize = 30f;

        [Tooltip("每层房间增大")]
        [Range(0f, 10f)]
        public float roomSizePerLevel = 5f;

        [Tooltip("最大房间大小")]
        [Range(30f, 80f)]
        public float maxRoomSize = 55f;

        // ==================== 掉落 ====================
        [Header("═══ 掉落概率 ═══")]
        [Tooltip("凡品掉率权重")]
        public float dropWeight_Fan = 50f;

        [Tooltip("灵品掉率权重")]
        public float dropWeight_Ling = 30f;

        [Tooltip("玄品掉率权重")]
        public float dropWeight_Xuan = 15f;

        [Tooltip("地品掉率权重")]
        public float dropWeight_Di = 4f;

        [Tooltip("天品掉率权重")]
        public float dropWeight_Tian = 1f;

        [Tooltip("敌人掉落灵物的基础概率")]
        [Range(0f, 1f)]
        public float enemyDropChance = 0.3f;

        [Tooltip("每层掉率增加")]
        [Range(0f, 0.2f)]
        public float dropChancePerLevel = 0.05f;

        [Tooltip("房间通关额外掉落数量")]
        [Range(0, 5)]
        public int roomClearDropCount = 1;

        // ==================== 近战攻击 ====================
        [Header("═══ 近战攻击 ═══")]
        [Tooltip("近战攻击范围")]
        [Range(1f, 5f)]
        public float meleeRange = 2.5f;

        [Tooltip("近战攻击角度")]
        [Range(30f, 360f)]
        public float meleeAngle = 120f;

        [Tooltip("第1段伤害倍率")]
        [Range(0.5f, 3f)]
        public float combo1DamageMultiplier = 1.0f;

        [Tooltip("第2段伤害倍率")]
        [Range(0.5f, 3f)]
        public float combo2DamageMultiplier = 1.2f;

        [Tooltip("第3段伤害倍率")]
        [Range(0.5f, 3f)]
        public float combo3DamageMultiplier = 1.5f;

        // ==================== 技能释放 ====================
        [Header("═══ 技能释放 ═══")]
        [Tooltip("技能动画播放速度倍率（1.0 = 默认速度，越大越快）\n此为全局默认值，每个技能可在 SkillData 中单独覆盖")]
        [Range(0.5f, 3f)]
        public float skillCastSpeed = 1f;

        // ==================== 工具方法 ====================

        /// <summary>
        /// 根据品阶获取掉率权重
        /// </summary>
        public float GetDropWeight(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => dropWeight_Fan,
                ItemRarity.Ling => dropWeight_Ling,
                ItemRarity.Xuan => dropWeight_Xuan,
                ItemRarity.Di => dropWeight_Di,
                ItemRarity.Tian => dropWeight_Tian,
                _ => dropWeight_Fan
            };
        }

        /// <summary>
        /// 获取总权重
        /// </summary>
        public float GetTotalDropWeight()
        {
            return dropWeight_Fan + dropWeight_Ling + dropWeight_Xuan + dropWeight_Di + dropWeight_Tian;
        }

        /// <summary>
        /// 按权重随机选择一个品阶
        /// </summary>
        public ItemRarity RollRarity()
        {
            float total = GetTotalDropWeight();
            float roll = Random.Range(0f, total);

            float cumulative = 0f;
            cumulative += dropWeight_Fan;
            if (roll < cumulative) return ItemRarity.Fan;

            cumulative += dropWeight_Ling;
            if (roll < cumulative) return ItemRarity.Ling;

            cumulative += dropWeight_Xuan;
            if (roll < cumulative) return ItemRarity.Xuan;

            cumulative += dropWeight_Di;
            if (roll < cumulative) return ItemRarity.Di;

            return ItemRarity.Tian;
        }

        /// <summary>
        /// 将配置应用到玩家属性
        /// </summary>
        public void ApplyToPlayerStats(CombatStats stats)
        {
            stats.maxHp = playerMaxHp;
            stats.currentHp = playerMaxHp;
            stats.attackDamage = playerAttackDamage;
            stats.attackSpeed = playerAttackSpeed;
            stats.moveSpeed = playerMoveSpeed;
            stats.critRate = playerCritRate;
            stats.critDamage = playerCritDamage;
            stats.damageReduction = playerDamageReduction;
            stats.dashCooldown = dashCooldown;
        }

        /// <summary>
        /// 将配置应用到敌人属性
        /// </summary>
        public void ApplyToEnemyStats(CombatStats stats)
        {
            stats.maxHp = enemyBaseHp;
            stats.currentHp = enemyBaseHp;
            stats.attackDamage = enemyBaseAttack;
            stats.moveSpeed = enemyMoveSpeed;
        }
    }
}
