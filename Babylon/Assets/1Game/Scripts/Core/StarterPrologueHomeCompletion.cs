using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Legacy回家终点兼容触发器。
    /// V5不在序章场景提交完成，只从这里转入独立家园场景。
    /// </summary>
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

            StarterPrologueHomeTransition.Begin();
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
