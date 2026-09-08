using UnityEngine;

namespace XianTu
{
    /// <summary>玩家走到回家路径终点后，正式提交序章完成状态。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueHomeCompletion : MonoBehaviour
    {
        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up;
            trigger.size = new Vector3(4f, 2f, 4f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") ||
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) !=
                StarterPrologueStep.RescueCompleted)
            {
                return;
            }

            StarterPrologueAdvanceResult result =
                StarterPrologueProgression.RecordPrologueCompleted(
                    SaveSystem.Instance.Data);
            if (result != StarterPrologueAdvanceResult.Success)
                return;

            SaveSystem.Instance.Save();
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = "序章完成 · 已回到家园",
                    HasWorldPosition = false
                });
            Debug.Log(
                "<color=#F2B45E>[新手序章] 已回到家园，序章正式完成。</color>");
        }

        public static void EnsureAt(Transform marker)
        {
            if (marker == null ||
                marker.GetComponent<
                    StarterPrologueHomeCompletion>() != null)
            {
                return;
            }
            marker.gameObject.AddComponent<
                StarterPrologueHomeCompletion>();
        }
    }
}
