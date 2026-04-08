using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Top-down 相机控制器
    /// 跟随玩家，固定俯视角度
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        [Header("跟随参数")]
        [SerializeField] private Vector3 offset = new(0, 15f, -8f);
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private float lookDownAngle = 60f;

        private Transform _target;

        private void Start()
        {
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            // 设置初始角度
            transform.rotation = Quaternion.Euler(lookDownAngle, 0, 0);
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                if (PlayerController.Instance != null)
                    _target = PlayerController.Instance.transform;
                return;
            }

            Vector3 desiredPos = _target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(lookDownAngle, 0, 0);
        }
    }
}
