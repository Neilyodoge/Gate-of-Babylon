namespace XianTu
{
    /// <summary>
    /// V.03 范围开关 —— 集中控制本版本暂时屏蔽 / 暂缓的子系统（详见 GDD「V.03 范围确认」Q7/Q8）。
    ///
    /// 设计目标：保留全部代码，只用开关让本版本不启用，随时可逆。
    /// - 默认值对应 V.03 决策：灵物整套屏蔽、局外洞府 meta 暂缓。
    /// - 可在 GameConfig 资产 Inspector 中覆盖（启用灵物系统 / 启用洞府meta）。
    /// - 运行时（DebugConsole）可临时覆盖，便于测试。
    /// </summary>
    public static class FeatureFlags
    {
        // 运行时覆盖：null = 跟随 GameConfig / 默认值
        private static bool? _spiritItemsOverride;
        private static bool? _caveMetaOverride;

        /// <summary>
        /// Q8：整套灵物功能（局内拾取 / 槽位 / 协同 / 质变）。V.03 默认关闭。
        /// </summary>
        public static bool EnableSpiritItems
        {
            get
            {
                if (_spiritItemsOverride.HasValue) return _spiritItemsOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg != null && cfg.启用灵物系统;
            }
            set => _spiritItemsOverride = value;
        }

        /// <summary>
        /// 局外洞府 meta（闭关石室·本体境界 / 灵脉 / 机缘事件 等 v0.5.4 系统）。常规启用（默认 true）。
        /// 注意：仅影响上述 v0.5.4 新增系统，不影响化身选择、进秘境传送门、以及炼器/藏经/灵田等既有模块。
        /// </summary>
        public static bool EnableCaveMeta
        {
            get
            {
                if (_caveMetaOverride.HasValue) return _caveMetaOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg == null || cfg.启用洞府meta;   // 无配置资产时默认启用
            }
            set => _caveMetaOverride = value;
        }
    }
}
