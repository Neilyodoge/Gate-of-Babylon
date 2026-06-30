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
        private static bool? _caveMetaOverride;

        /// <summary>
        /// 局外洞府 meta（闭关石室·本体境界 / 灵脉 / 机缘事件 等 v0.5.4 系统）。常规启用（默认 true）。
        /// </summary>
        public static bool EnableCaveMeta
        {
            get
            {
                if (_caveMetaOverride.HasValue) return _caveMetaOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg == null || cfg.启用洞府meta;
            }
            set => _caveMetaOverride = value;
        }
    }
}
