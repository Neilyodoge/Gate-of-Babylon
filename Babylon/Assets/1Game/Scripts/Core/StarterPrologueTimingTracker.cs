using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章开发计时器。使用非缩放时间，包含初契界面的思考时间，
    /// 只输出验收日志，不进入玩家HUD或永久存档。
    /// </summary>
    public sealed class StarterPrologueTimingTracker : MonoBehaviour
    {
        public const float TargetMinimumSeconds = 6f * 60f;
        public const float TargetMaximumSeconds = 7.5f * 60f;

        private StarterPrologueStep _lastStep;
        private bool _reported;

        public bool IsFreshRun { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float StarterChosenSeconds { get; private set; } = -1f;
        public float AttachmentChosenSeconds { get; private set; } = -1f;
        public float RescueCompletedSeconds { get; private set; } = -1f;
        public float CompletedSeconds { get; private set; } = -1f;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _lastStep = StarterPrologueProgression.GetStep(
                SaveSystem.Instance.Data);
            IsFreshRun =
                _lastStep == StarterPrologueStep.NotStarted;
        }

        private void Update()
        {
            ElapsedSeconds += Time.unscaledDeltaTime;
            StarterPrologueStep current =
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data);
            if (current != _lastStep)
            {
                RecordMilestone(current);
                _lastStep = current;
            }
            if (current == StarterPrologueStep.Completed &&
                !_reported)
            {
                _reported = true;
                ReportCompletion();
            }
        }

        private void RecordMilestone(StarterPrologueStep step)
        {
            switch (step)
            {
                case StarterPrologueStep.StarterChosen:
                    StarterChosenSeconds = ElapsedSeconds;
                    break;
                case StarterPrologueStep.AttachmentChosen:
                    AttachmentChosenSeconds = ElapsedSeconds;
                    break;
                case StarterPrologueStep.RescueCompleted:
                    RescueCompletedSeconds = ElapsedSeconds;
                    break;
                case StarterPrologueStep.Completed:
                    CompletedSeconds = ElapsedSeconds;
                    break;
            }
            Debug.Log(
                $"<color=#8ED8FF>[新手序章计时]</color> " +
                $"{step} @ {Format(ElapsedSeconds)}");
        }

        private void ReportCompletion()
        {
            if (!IsFreshRun)
            {
                Debug.Log(
                    "<color=#8ED8FF>[新手序章计时]</color> " +
                    $"从检查点继续，本次会话 {Format(ElapsedSeconds)}；" +
                    "不用于6～7.5分钟总时长判定。");
                return;
            }

            bool inRange = IsTargetDuration(CompletedSeconds);
            string message =
                $"总时长 {Format(CompletedSeconds)}，" +
                $"初契 {Format(StarterChosenSeconds)}，" +
                $"附着 {Format(AttachmentChosenSeconds)}，" +
                $"救援 {Format(RescueCompletedSeconds)}，" +
                $"目标6～7.5分钟：{(inRange ? "通过" : "待调整")}";
            if (inRange)
            {
                Debug.Log(
                    "<color=#66FF99>[新手序章计时]</color> " +
                    message);
            }
            else
            {
                Debug.LogWarning("[新手序章计时] " + message);
            }
        }

        public static bool IsTargetDuration(float seconds)
        {
            return seconds >= TargetMinimumSeconds &&
                   seconds <= TargetMaximumSeconds;
        }

        public static string Format(float seconds)
        {
            if (seconds < 0f)
                return "--:--";
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        public static StarterPrologueTimingTracker EnsureExists()
        {
            StarterPrologueTimingTracker existing =
                FindFirstObjectByType<
                    StarterPrologueTimingTracker>();
            if (existing != null)
                return existing;
            return new GameObject(
                    "StarterPrologueTimingTracker")
                .AddComponent<StarterPrologueTimingTracker>();
        }
    }
}
