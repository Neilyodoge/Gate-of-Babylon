using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 全局游戏配置 —— ScriptableObject
    /// 集中管理所有可调属性，方便在 Inspector 中快速修改
    /// 菜单：Assets → Create → 仙途秘境 → 游戏配置
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "仙途秘境/游戏配置")]
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
        [Tooltip("玩家的血量上限。受到伤害时从此值扣减，归零则死亡。灵物词条可额外增加。")]
        public float 玩家最大生命值 = 100f;

        [Tooltip("玩家普攻的基础伤害值。三段连招各段会乘以对应的伤害倍率。灵物词条可额外增加。")]
        public float 玩家攻击力 = 10f;

        [Tooltip("影响普攻动画播放速度。1.0=基础速度，2.0=两倍速。灵物攻速词条会在此基础上叠乘。")]
        [Range(0.5f, 3f)]
        public float 玩家攻击速度 = 1f;

        [Tooltip("角色移动时的速度（单位/秒）。灵物词条可额外增加。")]
        [Range(2f, 15f)]
        public float 玩家移动速度 = 6f;

        [Tooltip("普攻和技能触发暴击的概率。0.05=5%。暴击时伤害乘以暴击伤害倍率。")]
        [Range(0f, 1f)]
        public float 玩家暴击率 = 0.05f;

        [Tooltip("暴击时的伤害倍率。1.5=暴击造成150%伤害。与暴击率配合生效。")]
        [Range(1f, 5f)]
        public float 玩家暴击伤害 = 1.5f;

        [Tooltip("受到伤害时的减免比例。0.2=减免20%伤害。灵物词条可额外增加，上限90%。")]
        [Range(0f, 0.9f)]
        public float 玩家减伤比例 = 0f;

        // ==================== 闪避 ====================
        [Header("═══ 闪避 ═══")]
        [Tooltip("按下闪避键后角色位移的总距离（单位）。越大冲得越远。")]
        [Range(2f, 10f)]
        public float 闪避距离 = 5f;

        [Tooltip("闪避位移持续的时间（秒）。越短则冲刺越快、手感越灵敏。")]
        [Range(0.1f, 0.5f)]
        public float 闪避持续时间 = 0.2f;

        [Tooltip("每层闪避充能的恢复时间（秒）。充能耗尽后按此时间逐层恢复。")]
        [Range(0.5f, 5f)]
        public float 闪避冷却时间 = 1.5f;

        [Tooltip("闪避最大充能层数。2=可以连续闪避两次。充能耗尽后按冷却时间逐层恢复。")]
        [Range(1, 4)]
        public int 闪避充能层数 = 2;

        // ==================== 敌人属性 ====================
        [Header("═══ 敌人基础属性 ═══")]
        [Tooltip("普通小怪的基础血量。不同敌人类型会在此基础上乘以系数（如法师×0.8、冲锋×1.5、Boss×8）。")]
        public float 敌人基础血量 = 30f;

        [Tooltip("普通小怪的基础攻击力。不同敌人类型会在此基础上乘以系数（如法师×1.5、远程×1.2、Boss×3）。")]
        public float 敌人基础攻击力 = 5f;

        [Tooltip("敌人的移动速度（单位/秒）。所有敌人类型共用此值。")]
        [Range(1f, 8f)]
        public float 敌人移动速度 = 3f;

        [Tooltip("敌人发现玩家的最大距离（单位）。超出此范围敌人不会追击。")]
        [Range(5f, 25f)]
        public float 敌人检测范围 = 12f;

        [Tooltip("敌人发起攻击的距离（单位）。进入此范围后敌人开始攻击。")]
        [Range(1f, 5f)]
        public float 敌人攻击范围 = 1.5f;

        [Tooltip("敌人两次攻击之间的间隔时间（秒）。越小攻击越频繁。")]
        [Range(0.5f, 5f)]
        public float 敌人攻击间隔 = 1.5f;

        // ==================== 难度曲线 ====================
        [Header("═══ 难度曲线 ═══")]
        [Tooltip("第一层（练气期）战斗房间的敌人数量。")]
        public int 基础敌人数量 = 3;

        [Tooltip("每深入一层额外增加的敌人数。实际数量=基础数量+层数×此值。")]
        public int 每层增加敌人数 = 2;

        [Tooltip("每层敌人血量的增长系数。0.3表示每层血量增加30%（第2层=1.3倍，第3层=1.6倍…）。")]
        [Range(0.1f, 1f)]
        public float 每层血量倍率 = 0.3f;

        [Tooltip("每层敌人伤害的增长系数。0.2表示每层伤害增加20%。")]
        [Range(0.1f, 1f)]
        public float 每层伤害倍率 = 0.2f;

        // ==================== 房间 ====================
        [Header("═══ 房间尺寸 ═══")]
        [Tooltip("所有层战斗房间的固定边长（单位）。房间为正方形，不随层数变化。")]
        [Range(20f, 60f)]
        public float 房间大小 = 35f;

        // ==================== 精英怪 ====================
        [Header("═══ 精英怪 ═══")]
        [Tooltip("精英怪出现的最低层数（0=第1层就可能出现）。")]
        [Range(0, 5)]
        public int 精英怪最低层数 = 2;

        [Tooltip("每个战斗房间出现精英怪的概率。0.3=30%。")]
        [Range(0f, 1f)]
        public float 精英怪出现概率 = 0.3f;

        [Tooltip("精英怪血量倍率（相对于普通敌人）。3=三倍血量。")]
        [Range(1.5f, 10f)]
        public float 精英怪血量倍率 = 3f;

        [Tooltip("精英怪伤害倍率（相对于普通敌人）。1.5=1.5倍伤害。")]
        [Range(1f, 5f)]
        public float 精英怪伤害倍率 = 1.5f;

        // ==================== 可破坏物 ====================
        [Header("═══ 可破坏物 ═══")]
        [Tooltip("每个战斗房间生成的可破坏物数量。")]
        [Range(0, 10)]
        public int 可破坏物数量 = 3;

        [Tooltip("可破坏物被摧毁时掉落灵力碎片的概率。")]
        [Range(0f, 1f)]
        public float 可破坏物掉落概率 = 0.4f;

        // ==================== 掉落 ====================
        [Header("═══ 掉落概率 ═══")]
        [Tooltip("凡品灵物的掉落权重。权重越高出现概率越大。所有品阶权重之和为100%。")]
        public float 凡品掉率权重 = 50f;

        [Tooltip("灵品灵物的掉落权重。")]
        public float 灵品掉率权重 = 30f;

        [Tooltip("玄品灵物的掉落权重。")]
        public float 玄品掉率权重 = 15f;

        [Tooltip("地品灵物的掉落权重。稀有度较高。")]
        public float 地品掉率权重 = 4f;

        [Tooltip("天品灵物的掉落权重。最稀有品阶。")]
        public float 天品掉率权重 = 1f;

        [Tooltip("每只敌人死亡时掉落灵物的固定概率。0.05=5%。掉率不随层数变化，除非有灵物增幅。")]
        [Range(0f, 1f)]
        public float 敌人掉落概率 = 0.05f;

        [Tooltip("（已废弃，掉率现在固定不变）每深入一层掉落概率增加的值。设为0表示不增加。")]
        [Range(0f, 0.2f)]
        public float 每层掉率增加 = 0f;

        [Tooltip("通关战斗房间后额外掉落灵物的概率。0.25=25%概率掉一1个。设为0则通关不额外掉落。")]
        [Range(0f, 1f)]
        public float 通关掉落概率 = 0.25f;
        [Tooltip("通关战斗房间后额外掉落的灵物数量（在玩家附近生成，需先通过通关掉落概率判定）。")]
        [Range(0, 5)]
        public int 通关额外掉落数 = 1;

        [Header("═══ 功法掉落 ═══")]
        [Tooltip("敌人死亡时掉落功法的概率。0.03=3%。功法比灵物更稀有。")]
        [Range(0f, 1f)]
        public float 功法掉落概率 = 0.03f;

        [Tooltip("通关战斗房间后额外掉落功法的概率。0.25=25%。")]
        [Range(0f, 1f)]
        public float 通关功法掉落概率 = 0.25f;

        // ==================== V.03 范围开关 ====================
        [Header("═══ V.03 范围开关（详见 GDD「V.03 范围确认」）═══")]
        [Tooltip("Q8：整套灵物功能（局内拾取/槽位/协同/质变）。V.03 暂时屏蔽 → 取消勾选。勾选则恢复灵物系统。")]
        public bool 启用灵物系统 = false;

        [Tooltip("局外洞府 meta（闭关石室·本体境界 / 灵脉 / 机缘事件 等 v0.5.4 系统）。常规启用；取消勾选则整套暂缓。不影响化身选择/进秘境/炼器藏经等既有模块。")]
        public bool 启用洞府meta = true;

        // ==================== Debug 爆率覆盖 ====================
        /// <summary>Debug模式下是否拉满灵物爆率（运行时设置，不序列化）</summary>
        private static bool _debugMaxItemDropRate = false;
        public bool debugMaxItemDropRate
        {
            get => _debugMaxItemDropRate;
            set => _debugMaxItemDropRate = value;
        }

        /// <summary>Debug模式下是否拉满功法爆率（运行时设置，不序列化）</summary>
        private static bool _debugMaxSkillDropRate = false;
        public bool debugMaxSkillDropRate
        {
            get => _debugMaxSkillDropRate;
            set => _debugMaxSkillDropRate = value;
        }

        /// <summary>兼容旧接口：同时控制灵物和功法爆率</summary>
        public bool debugMaxDropRate
        {
            get => _debugMaxItemDropRate && _debugMaxSkillDropRate;
            set { _debugMaxItemDropRate = value; _debugMaxSkillDropRate = value; }
        }

        // ==================== 近战攻击 ====================
        [Header("═══ 近战攻击 ═══")]
        [Tooltip("普攻的判定半径（单位）。以玩家为圆心，此范围内的扇形区域为伤害区域。")]
        [Range(1f, 5f)]
        public float 近战攻击范围 = 2.5f;

        [Tooltip("普攻的判定扇形角度（度）。120=面前120°扇形。360=全方位攻击。")]
        [Range(30f, 360f)]
        public float 近战攻击角度 = 120f;

        [Tooltip("三段连招第1段的伤害倍率。实际伤害=玩家攻击力×此倍率。")]
        [Range(0.5f, 3f)]
        public float 第一段伤害倍率 = 1.0f;

        [Tooltip("三段连招第2段的伤害倍率。通常略高于第1段。")]
        [Range(0.5f, 3f)]
        public float 第二段伤害倍率 = 1.2f;

        [Tooltip("三段连招第3段（终结技）的伤害倍率。通常最高，配合更大的前冲位移。")]
        [Range(0.5f, 3f)]
        public float 第三段伤害倍率 = 1.5f;

        // ==================== 技能释放 ====================
        [Header("═══ 技能释放 ═══")]
        [Tooltip("技能动画的播放速度倍率。1.0=默认速度，2.0=两倍速。此为全局默认值，每个技能可在SkillData中单独覆盖。")]
        [Range(0.5f, 3f)]
        public float 技能释放速度 = 1f;

        // ==================== 工具方法 ====================

        /// <summary>
        /// 根据品阶获取掉率权重
        /// </summary>
        public float GetDropWeight(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => 凡品掉率权重,
                ItemRarity.Ling => 灵品掉率权重,
                ItemRarity.Xuan => 玄品掉率权重,
                ItemRarity.Di => 地品掉率权重,
                ItemRarity.Tian => 天品掉率权重,
                _ => 凡品掉率权重
            };
        }

        /// <summary>
        /// 获取总权重
        /// </summary>
        public float GetTotalDropWeight()
        {
            return 凡品掉率权重 + 灵品掉率权重 + 玄品掉率权重 + 地品掉率权重 + 天品掉率权重;
        }

        /// <summary>
        /// 按权重随机选择一个品阶（不考虑层数）
        /// </summary>
        public ItemRarity RollRarity()
        {
            return RollRarity(0);
        }

        /// <summary>
        /// 按权重随机选择一个品阶（考虑层数，层数越高高品质权重越大）
        /// 每层高品质权重提升：灵品+5, 玄品+3, 地品+1.5, 天品+0.5，凡品-10（最低5）
        /// </summary>
        public ItemRarity RollRarity(int floorLevel)
        {
            // 基于层数动态调整权重
            float fanW = Mathf.Max(5f, 凡品掉率权重 - floorLevel * 10f);
            float lingW = 灵品掉率权重 + floorLevel * 5f;
            float xuanW = 玄品掉率权重 + floorLevel * 3f;
            float diW = 地品掉率权重 + floorLevel * 1.5f;
            float tianW = 天品掉率权重 + floorLevel * 0.5f;

            float total = fanW + lingW + xuanW + diW + tianW;
            float roll = Random.Range(0f, total);

            float cumulative = 0f;
            cumulative += fanW;
            if (roll < cumulative) return ItemRarity.Fan;

            cumulative += lingW;
            if (roll < cumulative) return ItemRarity.Ling;

            cumulative += xuanW;
            if (roll < cumulative) return ItemRarity.Xuan;

            cumulative += diW;
            if (roll < cumulative) return ItemRarity.Di;

            return ItemRarity.Tian;
        }

        /// <summary>
        /// 将配置应用到玩家属性
        /// </summary>
        public void ApplyToPlayerStats(CombatStats stats)
        {
            stats.maxHp = 玩家最大生命值;
            stats.currentHp = 玩家最大生命值;
            stats.attackDamage = 玩家攻击力;
            stats.attackSpeed = 玩家攻击速度;
            stats.moveSpeed = 玩家移动速度;
            stats.critRate = 玩家暴击率;
            stats.critDamage = 玩家暴击伤害;
            stats.damageReduction = 玩家减伤比例;
            stats.dashCooldown = 闪避冷却时间;
        }

        /// <summary>
        /// 将配置应用到敌人属性
        /// </summary>
        public void ApplyToEnemyStats(CombatStats stats)
        {
            stats.maxHp = 敌人基础血量;
            stats.currentHp = 敌人基础血量;
            stats.attackDamage = 敌人基础攻击力;
            stats.moveSpeed = 敌人移动速度;
        }
    }
}
