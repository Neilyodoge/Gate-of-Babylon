using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章固定回家点触发器。
    /// 救援完成后必须由玩家走到此处，才加载独立家园场景。
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
                !CanTrigger(
                    StarterPrologueProgression.GetStep(
                        SaveSystem.Instance.Data)))
            {
                return;
            }

            StarterPrologueHomeTransition.Begin();
        }

        public static bool CanTrigger(StarterPrologueStep step)
        {
            return step == StarterPrologueStep.RescueCompleted;
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
