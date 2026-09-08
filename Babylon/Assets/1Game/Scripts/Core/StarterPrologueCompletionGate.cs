using UnityEngine;

namespace XianTu
{
    /// <summary>序章救援完成后开放回家路径。</summary>
    public sealed class StarterPrologueCompletionGate : MonoBehaviour
    {
        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            if (gameObject.activeSelf)
                Refresh();
        }

        private void Refresh()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) <
                StarterPrologueStep.RescueCompleted)
            {
                return;
            }
            gameObject.SetActive(false);
        }
    }
}
