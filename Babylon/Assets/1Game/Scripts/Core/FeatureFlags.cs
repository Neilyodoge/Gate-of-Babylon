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
        private static bool? _carrierRuntimeOverride;
        private static bool? _circuitRuntimeOverride;
        private static bool? _ecologyRuntimeOverride;

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

        /// <summary>ProjectR统一载体执行。P1完成后默认启用，仍可关闭回退。</summary>
        public static bool EnableCarrierRuntime
        {
            get
            {
                if (_carrierRuntimeOverride.HasValue) return _carrierRuntimeOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg == null || cfg.启用载体运行时;
            }
            set => _carrierRuntimeOverride = value;
        }

        /// <summary>ProjectR器灵回路运行时。P0默认关闭，仅建立可回滚开关。</summary>
        public static bool EnableCircuitRuntime
        {
            get
            {
                if (_circuitRuntimeOverride.HasValue) return _circuitRuntimeOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg != null && cfg.启用回路运行时;
            }
            set => _circuitRuntimeOverride = value;
        }

        /// <summary>ProjectR蜂巢生态运行时占位。关卡冻结期间禁止接线。</summary>
        public static bool EnableEcologyRuntime
        {
            get
            {
                if (_ecologyRuntimeOverride.HasValue) return _ecologyRuntimeOverride.Value;
                var cfg = GameConfig.Instance;
                return cfg != null && cfg.启用生态运行时;
            }
            set => _ecologyRuntimeOverride = value;
        }

        /// <summary>清除调试覆盖，恢复GameConfig值。供测试和Debug工具使用。</summary>
        public static void ResetRuntimeOverrides()
        {
            _caveMetaOverride = null;
            _carrierRuntimeOverride = null;
            _circuitRuntimeOverride = null;
            _ecologyRuntimeOverride = null;
        }
    }
}
