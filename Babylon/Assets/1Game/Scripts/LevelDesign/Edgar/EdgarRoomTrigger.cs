using UnityEngine;

namespace XianTu
{
    /// <summary>玩家步行进入 Edgar 实体房间时，通知 GameManager 激活该房遭遇。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EdgarRoomTrigger : MonoBehaviour
    {
        private int _roomIndex;

        public void Initialize(int roomIndex)
        {
            _roomIndex = roomIndex;
            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;

            GameManager.Instance?.EnterEdgarRoom(_roomIndex);
        }
    }
}
