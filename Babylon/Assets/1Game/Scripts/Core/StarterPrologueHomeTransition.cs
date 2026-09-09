using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XianTu
{
    /// <summary>
    /// 救援完成后加载独立家园场景。
    /// 序章完成状态由家园场景成功启动后提交，避免加载失败时丢失恢复点。
    /// </summary>
    public sealed class StarterPrologueHomeTransition : MonoBehaviour
    {
        public const string HomeSceneName = "ProjectRHome";

        private static bool _loading;
        private Coroutine _routine;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loading = false;
        }

        public static bool CanTransition(StarterPrologueStep step)
        {
            return step >= StarterPrologueStep.RescueCompleted;
        }

        public static void Begin(float delaySeconds = 0f)
        {
            if (_loading ||
                SceneManager.GetActiveScene().name == HomeSceneName)
            {
                return;
            }

            StarterPrologueStep step =
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data);
            if (!CanTransition(step))
                return;

            StarterPrologueHomeTransition transition =
                FindFirstObjectByType<
                    StarterPrologueHomeTransition>();
            if (transition == null)
            {
                transition = new GameObject(
                        "StarterPrologueHomeTransition")
                    .AddComponent<
                        StarterPrologueHomeTransition>();
            }
            transition.StartTransition(delaySeconds);
        }

        private void StartTransition(float delaySeconds)
        {
            if (_routine != null)
                return;
            _routine = StartCoroutine(
                TransitionAfterDelay(delaySeconds));
        }

        private IEnumerator TransitionAfterDelay(
            float delaySeconds)
        {
            _loading = true;
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    delaySeconds);
            }

            if (!Application.CanStreamedLevelBeLoaded(
                    HomeSceneName))
            {
                _loading = false;
                _routine = null;
                Debug.LogError(
                    $"[新手序章] 无法加载家园场景 " +
                    $"{HomeSceneName}；保留救援完成检查点。");
                yield break;
            }

            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = "正在返回家园……",
                    HasWorldPosition = false
                });
            SceneManager.LoadScene(
                HomeSceneName,
                LoadSceneMode.Single);
        }
    }
}
