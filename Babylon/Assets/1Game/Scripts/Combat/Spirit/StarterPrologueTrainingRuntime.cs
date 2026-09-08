using System.Collections.Generic;

namespace XianTu
{
    public enum StarterPrologueTrainingStartResult
    {
        Success = 0,
        AlreadyRunning = 1,
        InvalidRoster = 2,
        InvalidTransition = 3,
    }

    public enum StarterPrologueTrainingWave
    {
        None = 0,
        MechanismTargets = 1,
        CombatValidation = 2,
        Completed = 3,
    }

    public readonly struct StarterPrologueTrainingStep
    {
        public int Remaining { get; }
        public bool Completed { get; }

        public StarterPrologueTrainingStep(
            int remaining,
            bool completed)
        {
            Remaining = remaining;
            Completed = completed;
        }
    }

    /// <summary>验证战的纯状态合同：固定三个不同目标，仅最后一个有效击败完成。</summary>
    public sealed class StarterPrologueTrainingRuntime
    {
        public const int RequiredEnemyCount = 3;
        private readonly HashSet<int> _remaining = new();

        public bool IsRunning { get; private set; }
        public int Remaining => _remaining.Count;
        public StarterPrologueTrainingWave CurrentWave
        {
            get;
            private set;
        }

        public StarterPrologueTrainingStartResult Begin(
            IReadOnlyList<int> enemyIds)
        {
            if (CurrentWave != StarterPrologueTrainingWave.None)
                return StarterPrologueTrainingStartResult.InvalidTransition;
            StarterPrologueTrainingStartResult result =
                BeginRoster(enemyIds);
            if (result == StarterPrologueTrainingStartResult.Success)
            {
                CurrentWave =
                    StarterPrologueTrainingWave.MechanismTargets;
            }
            return result;
        }

        public StarterPrologueTrainingStartResult BeginValidation(
            IReadOnlyList<int> enemyIds)
        {
            if (CurrentWave !=
                    StarterPrologueTrainingWave.MechanismTargets ||
                IsRunning ||
                Remaining != 0)
            {
                return StarterPrologueTrainingStartResult
                    .InvalidTransition;
            }
            StarterPrologueTrainingStartResult result =
                BeginRoster(enemyIds);
            if (result == StarterPrologueTrainingStartResult.Success)
            {
                CurrentWave =
                    StarterPrologueTrainingWave.CombatValidation;
            }
            return result;
        }

        private StarterPrologueTrainingStartResult BeginRoster(
            IReadOnlyList<int> enemyIds)
        {
            if (IsRunning)
                return StarterPrologueTrainingStartResult.AlreadyRunning;
            if (enemyIds == null ||
                enemyIds.Count != RequiredEnemyCount)
            {
                return StarterPrologueTrainingStartResult.InvalidRoster;
            }

            _remaining.Clear();
            for (int i = 0; i < enemyIds.Count; i++)
            {
                if (enemyIds[i] == 0 ||
                    !_remaining.Add(enemyIds[i]))
                {
                    _remaining.Clear();
                    return StarterPrologueTrainingStartResult
                        .InvalidRoster;
                }
            }

            IsRunning = true;
            return StarterPrologueTrainingStartResult.Success;
        }

        public StarterPrologueTrainingStep RegisterDefeated(int enemyId)
        {
            if (!IsRunning || !_remaining.Remove(enemyId))
                return new StarterPrologueTrainingStep(Remaining, false);

            bool completed = _remaining.Count == 0;
            if (completed)
            {
                IsRunning = false;
                if (CurrentWave ==
                    StarterPrologueTrainingWave.CombatValidation)
                {
                    CurrentWave =
                        StarterPrologueTrainingWave.Completed;
                }
            }
            return new StarterPrologueTrainingStep(
                Remaining,
                completed);
        }

        public void Reset()
        {
            _remaining.Clear();
            IsRunning = false;
            CurrentWave = StarterPrologueTrainingWave.None;
        }
    }
}
