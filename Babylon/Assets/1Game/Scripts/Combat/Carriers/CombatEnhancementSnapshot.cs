namespace XianTu
{
    public enum CarrierEnhancementRole
    {
        Enhancement = 0,
        Addon = 1,
    }

    public enum CarrierShapeMode
    {
        None = 0,
        Wall = 1,
        Ring = 2,
        Zone = 3,
    }

    /// <summary>
    /// 载体执行可读取的增强快照。字段与Legacy ChainConfig增强区隔离映射，
    /// 避免新载体直接依赖模块链生命周期。
    /// </summary>
    public readonly struct CombatEnhancementSnapshot
    {
        public CarrierEnhancementRole Role { get; }
        public ElementTag ElementTag { get; }
        public float DamageMultiplier { get; }
        public float RadiusMultiplier { get; }
        public float ProjectileMultiplier { get; }
        public int ExtraProjectiles { get; }
        public int ChainCount { get; }
        public bool Surround { get; }
        public bool Sustained { get; }
        public bool DelayedBlast { get; }
        public bool TargetFarthest { get; }
        public CarrierShapeMode Shape { get; }

        public CombatEnhancementSnapshot(
            CarrierEnhancementRole role,
            ElementTag elementTag,
            float damageMultiplier,
            float radiusMultiplier,
            float projectileMultiplier,
            int extraProjectiles,
            int chainCount,
            bool surround,
            bool sustained,
            bool delayedBlast,
            bool targetFarthest,
            CarrierShapeMode shape)
        {
            Role = role;
            ElementTag = elementTag;
            DamageMultiplier = damageMultiplier;
            RadiusMultiplier = radiusMultiplier;
            ProjectileMultiplier = projectileMultiplier;
            ExtraProjectiles = extraProjectiles;
            ChainCount = chainCount;
            Surround = surround;
            Sustained = sustained;
            DelayedBlast = delayedBlast;
            TargetFarthest = targetFarthest;
            Shape = shape;
        }
    }

    public interface ICombatEnhancementProvider
    {
        bool TryGetSnapshot(CarrierSlot slot, out CombatEnhancementSnapshot snapshot);
    }
}
