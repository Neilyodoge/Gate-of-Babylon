namespace XianTu
{
    /// <summary>
    /// 本局（run）战斗统计。轮回一击等"按累计伤害结算"的机制读这里。
    /// 进入新一局时由 GameManager 调 <see cref="Reset"/> 清零。
    /// </summary>
    public static class RunCombatStats
    {
        private sealed class ResultBuffer : IStructuredCombatResultSink
        {
            private readonly System.Collections.Generic.List<
                StructuredCombatResult> _results = new();

            public System.Collections.Generic.IReadOnlyList<
                StructuredCombatResult> Results => _results.AsReadOnly();

            public void Record(in StructuredCombatResult result)
            {
                _results.Add(result);
            }

            public void Reset()
            {
                _results.Clear();
            }
        }

        private static readonly StableConfigId PlayerDamageMetric =
            new("combat.player.damage");
        private static readonly StableConfigId PlayerDamageTakenMetric =
            new("combat.player.damage-taken");
        private static readonly StableConfigId PlayerHealingMetric =
            new("combat.player.healing");
        private static readonly StableConfigId PlayerDefenseMetric =
            new("combat.player.defense");
        private static readonly CircuitAttributionRecorder Attribution = new();
        private static readonly CircuitShadowRecorder Shadow = new();
        private static readonly ResultBuffer Buffer = new();
        private static readonly CircuitCombatResultBridge ResultBridge =
            new(Attribution, Shadow, Buffer);

        /// <summary>本局玩家对敌人造成的累计总伤害。</summary>
        public static float TotalPlayerDamage { get; private set; }
        public static System.Collections.Generic.IReadOnlyList<
            StructuredCombatResult> Results => Buffer.Results;

        public static void AddPlayerDamage(float amount)
        {
            AddPlayerDamage(amount, amount, default);
        }

        public static void AddPlayerDamage(
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            if (appliedAmount <= 0f ||
                float.IsNaN(appliedAmount) ||
                float.IsInfinity(appliedAmount))
            {
                return;
            }
            if (float.IsNaN(requestedAmount) ||
                float.IsInfinity(requestedAmount))
            {
                requestedAmount = appliedAmount;
            }

            TotalPlayerDamage += appliedAmount;
            RecordPlayerDamage(
                requestedAmount,
                appliedAmount,
                target);
        }

        public static void RecordPlayerDamage(
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            if (appliedAmount <= 0f ||
                float.IsNaN(appliedAmount) ||
                float.IsInfinity(appliedAmount))
            {
                return;
            }
            RecordLegacy(
                PlayerDamageMetric,
                CombatOutcomeKind.Damage,
                requestedAmount,
                appliedAmount,
                target);
        }

        public static void RecordLegacyDamageBaseline(
            StableConfigId metricId,
            float requestedAmount,
            float expectedAppliedAmount,
            in CircuitEntityRef target)
        {
            if (metricId.IsEmpty)
                return;

            RecordLegacy(
                metricId,
                CombatOutcomeKind.Damage,
                requestedAmount,
                expectedAppliedAmount,
                target);
        }

        public static void RecordCircuitDamage(
            StableConfigId metricId,
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target,
            in CircuitEvent origin)
        {
            if (metricId.IsEmpty ||
                float.IsNaN(requestedAmount) ||
                float.IsInfinity(requestedAmount) ||
                float.IsNaN(appliedAmount) ||
                float.IsInfinity(appliedAmount))
            {
                return;
            }

            ResultBridge.Record(StructuredCombatResult.FromCircuit(
                metricId,
                CombatOutcomeKind.Damage,
                requestedAmount,
                appliedAmount,
                target,
                origin));
        }

        public static void AddPlayerDamageTaken(
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            if (appliedAmount <= 0f)
                return;

            RecordLegacy(
                PlayerDamageTakenMetric,
                CombatOutcomeKind.Damage,
                requestedAmount,
                appliedAmount,
                target);
        }

        public static void AddPlayerHealing(
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            if (requestedAmount <= 0f || appliedAmount < 0f)
                return;

            RecordLegacy(
                PlayerHealingMetric,
                CombatOutcomeKind.Healing,
                requestedAmount,
                appliedAmount,
                target);
        }

        public static void AddPlayerDefense(
            float requestedAmount,
            float preventedAmount,
            in CircuitEntityRef target)
        {
            if (preventedAmount <= 0f)
                return;

            RecordLegacy(
                PlayerDefenseMetric,
                CombatOutcomeKind.Defense,
                requestedAmount,
                preventedAmount,
                target);
        }

        public static void AddPlayerResource(
            StableConfigId metricId,
            float requestedDelta,
            float appliedDelta,
            in CircuitEntityRef target)
        {
            if (metricId.IsEmpty ||
                (requestedDelta == 0f && appliedDelta == 0f))
            {
                return;
            }

            RecordLegacy(
                metricId,
                CombatOutcomeKind.Resource,
                requestedDelta,
                appliedDelta,
                target);
        }

        public static System.Collections.Generic.IReadOnlyList<
            ShadowMetricComparison> CompareDamageShadow(
            float absoluteTolerance,
            float relativeTolerance)
        {
            return CompareShadow(
                absoluteTolerance,
                relativeTolerance);
        }

        public static System.Collections.Generic.IReadOnlyList<
            ShadowMetricComparison> CompareShadow(
            float absoluteTolerance,
            float relativeTolerance)
        {
            return Shadow.Compare(
                absoluteTolerance,
                relativeTolerance);
        }

        private static void RecordLegacy(
            StableConfigId metricId,
            CombatOutcomeKind outcome,
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            if (float.IsNaN(requestedAmount) ||
                float.IsInfinity(requestedAmount))
            {
                requestedAmount = appliedAmount;
            }
            if (float.IsNaN(appliedAmount) ||
                float.IsInfinity(appliedAmount))
            {
                return;
            }

            ResultBridge.Record(StructuredCombatResult.FromLegacy(
                metricId,
                outcome,
                requestedAmount,
                appliedAmount,
                target));
        }

        public static void Reset()
        {
            TotalPlayerDamage = 0f;
            Attribution.Reset();
            Shadow.Reset();
            Buffer.Reset();
        }
    }
}
