using UnityEngine;

namespace XianTu
{
    /// <summary>首次附着完成前封锁训练场，检查点推进后自动开门。</summary>
    public sealed class StarterPrologueAttachmentGate : MonoBehaviour
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
                StarterPrologueStep.AttachmentChosen)
            {
                return;
            }
            gameObject.SetActive(false);
        }
    }
}
