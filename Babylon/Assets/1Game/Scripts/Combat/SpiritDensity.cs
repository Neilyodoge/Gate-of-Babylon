using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵气浓度等级（v0.5 修仙独有战斗机制 #1）。
    ///
    /// 每个战斗房间出生时随机一个等级，叠加到敌人 / 掉落 / 视觉氛围上。
    /// 让"修仙世界"在战斗中能被玩家感受到，而不只是 NPC 嘴上说说。
    /// </summary>
    public enum SpiritDensityLevel
    {
        /// <summary>灵气稀薄 —— 敌人弱，掉落少。新手友好房</summary>
        Sparse = 0,
        /// <summary>普通灵气 —— 默认状态</summary>
        Normal = 1,
        /// <summary>灵气浓郁 —— 敌人强 20~30%，但局内灵物 +1，CaveMaterial 概率翻倍</summary>
        Rich = 2,
        /// <summary>灵脉所在 —— 敌人精英化 + 必出 1 件洞府素材 + 房间金光特效</summary>
        Vein = 3
    }

    /// <summary>
    /// 灵气浓度上下文（当前房间的属性，影响敌人 spawn / 掉落 / 氛围）。
    /// </summary>
    public static class SpiritDensity
    {
        public static SpiritDensityLevel Current { get; private set; } = SpiritDensityLevel.Normal;

        // ========== 等级数值（被 EnemyBase / EnemyBoss / CaveMaterialPool 读取）==========

        public static float EnemyHpMultiplier => Current switch
        {
            SpiritDensityLevel.Sparse => 0.80f,
            SpiritDensityLevel.Rich => 1.25f,
            SpiritDensityLevel.Vein => 1.40f,
            _ => 1f
        };

        public static float EnemyDamageMultiplier => Current switch
        {
            SpiritDensityLevel.Sparse => 0.85f,
            SpiritDensityLevel.Rich => 1.20f,
            SpiritDensityLevel.Vein => 1.35f,
            _ => 1f
        };

        public static float ItemDropMultiplier => Current switch
        {
            SpiritDensityLevel.Sparse => 0.70f,
            SpiritDensityLevel.Rich => 1.50f,
            SpiritDensityLevel.Vein => 2.00f,
            _ => 1f
        };

        public static float CaveMaterialBonusChance => Current switch
        {
            SpiritDensityLevel.Sparse => 0.00f,
            SpiritDensityLevel.Rich => 0.10f,    // +10% 额外洞府素材掉率
            SpiritDensityLevel.Vein => 0.30f,    // +30% 额外洞府素材掉率
            _ => 0f
        };

        public static Color AmbientTint => Current switch
        {
            SpiritDensityLevel.Sparse => new Color(0.85f, 0.85f, 0.92f),
            SpiritDensityLevel.Rich => new Color(0.70f, 0.95f, 0.85f),
            SpiritDensityLevel.Vein => new Color(1.0f, 0.92f, 0.55f),
            _ => Color.white
        };

        public static string DisplayName => Current switch
        {
            SpiritDensityLevel.Sparse => "灵气稀薄",
            SpiritDensityLevel.Rich => "灵气浓郁",
            SpiritDensityLevel.Vein => "灵脉所在",
            _ => "灵气平和"
        };

        // ========== 切换（房间初始化时调用）==========

        /// <summary>按权重随机一个等级（默认偏向 Normal）</summary>
        public static SpiritDensityLevel Roll(int roomLevel = 0)
        {
            float r = Random.value;
            // 基础分布：Sparse 15% / Normal 55% / Rich 25% / Vein 5%
            // 高阶境界 Vein/Rich 概率小幅提升
            float veinThreshold = 0.95f - roomLevel * 0.01f;   // 5%~10%
            float richThreshold = 0.70f - roomLevel * 0.01f;   // 25%~30%
            float normalThreshold = 0.15f;                      // Sparse 始终 15%

            if (r >= veinThreshold) return SpiritDensityLevel.Vein;
            if (r >= richThreshold) return SpiritDensityLevel.Rich;
            if (r >= normalThreshold) return SpiritDensityLevel.Normal;
            return SpiritDensityLevel.Sparse;
        }

        public static void Set(SpiritDensityLevel level)
        {
            Current = level;
            GameEvents.Publish(new GameEvents.SpiritDensityChanged
            {
                NewLevel = level,
                DisplayName = DisplayName,
                Tint = AmbientTint
            });
            Debug.Log($"<color=#ffd47a>[SpiritDensity] 当前房间灵气浓度：{DisplayName}（HP×{EnemyHpMultiplier:F2} / DMG×{EnemyDamageMultiplier:F2} / DROP×{ItemDropMultiplier:F2}）</color>");
        }

        public static void Reset() => Set(SpiritDensityLevel.Normal);
    }
}
