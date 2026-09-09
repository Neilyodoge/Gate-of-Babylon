using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V5序章取得操作权后的最短开场节拍。
    /// 只在全新序章播放，不新增可跳过流程或永久存档字段。
    /// </summary>
    public sealed class StarterPrologueOpeningBeat : MonoBehaviour
    {
        public const string ProtagonistLine =
            "照料员怎么还没来……";
        public const string MovementObjective =
            "沿林间小路去看看";

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(0.45f);
            if (!ShouldPlay(
                    StarterPrologueProgression.GetStep(
                        SaveSystem.Instance.Data)))
            {
                yield break;
            }

            StarterPrologueDialogueHUD.Show(
                "主角",
                ProtagonistLine,
                2.2f);
        }

        public static bool ShouldPlay(
            StarterPrologueStep step)
        {
            return step == StarterPrologueStep.NotStarted;
        }

        public static StarterPrologueOpeningBeat EnsureExists()
        {
            StarterPrologueOpeningBeat existing =
                FindFirstObjectByType<
                    StarterPrologueOpeningBeat>();
            if (existing != null)
                return existing;
            if (!ShouldPlay(
                    StarterPrologueProgression.GetStep(
                        SaveSystem.Instance.Data)))
            {
                return null;
            }
            return new GameObject(
                    "StarterPrologueOpeningBeat")
                .AddComponent<StarterPrologueOpeningBeat>();
        }
    }
}
