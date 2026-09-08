using UnityEngine;

namespace XianTu
{
    /// <summary>验证战完成后开放洞府出口。</summary>
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
                StarterPrologueStep.Completed)
            {
                return;
            }
            gameObject.SetActive(false);
        }
    }
}
