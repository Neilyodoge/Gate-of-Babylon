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
        AgitatedSpirits = 1,
        PossessedHost = 2,
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

    /// <summary>序章救援战状态：先击退两只躁动灵宠，再打断一名失控附身者。</summary>
    public sealed class StarterPrologueTrainingRuntime
    {
        public const int RequiredAgitatedSpiritCount = 2;
        public const int RequiredPossessedHostCount = 1;
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
                BeginRoster(
                    enemyIds,
                    RequiredAgitatedSpiritCount);
            if (result == StarterPrologueTrainingStartResult.Success)
            {
                CurrentWave =
                    StarterPrologueTrainingWave.AgitatedSpirits;
            }
            return result;
        }

        public StarterPrologueTrainingStartResult BeginPossessedHost(
            IReadOnlyList<int> enemyIds)
        {
            if (CurrentWave !=
                    StarterPrologueTrainingWave.AgitatedSpirits ||
                IsRunning ||
                Remaining != 0)
            {
                return StarterPrologueTrainingStartResult
                    .InvalidTransition;
            }
            StarterPrologueTrainingStartResult result =
                BeginRoster(
                    enemyIds,
                    RequiredPossessedHostCount);
            if (result == StarterPrologueTrainingStartResult.Success)
            {
                CurrentWave =
                    StarterPrologueTrainingWave.PossessedHost;
            }
            return result;
        }

        private StarterPrologueTrainingStartResult BeginRoster(
            IReadOnlyList<int> enemyIds,
            int requiredCount)
        {
            if (IsRunning)
                return StarterPrologueTrainingStartResult.AlreadyRunning;
            if (enemyIds == null ||
                enemyIds.Count != requiredCount)
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
                    StarterPrologueTrainingWave.PossessedHost)
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
