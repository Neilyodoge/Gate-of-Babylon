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
        [SerializeField] private Vector3 offset = new(0, 25f, -9f);
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private float lookDownAngle = 70f;

        private Transform _target;
        private EdgarDungeonRuntime _dungeonRuntime;
        private int _lastDungeonRotation = int.MinValue;

        private void Start()
        {
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

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

            int dungeonRotation = GetDungeonRotation();
            Quaternion layoutRotation = Quaternion.Euler(0f, dungeonRotation, 0f);
            Vector3 desiredPos = _target.position + layoutRotation * offset;
            Quaternion desiredRotation =
                layoutRotation * Quaternion.Euler(lookDownAngle, 0f, 0f);

            if (_lastDungeonRotation != dungeonRotation)
            {
                // 地牢生成/清理时整体朝向会跳变；相机同步瞬移，避免插值路径穿过墙体。
                transform.position = desiredPos;
                transform.rotation = desiredRotation;
                _lastDungeonRotation = dungeonRotation;
                return;
            }

            float interpolation = smoothSpeed * Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, desiredPos, interpolation);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, interpolation);
        }

        private int GetDungeonRotation()
        {
            if (_dungeonRuntime == null)
                _dungeonRuntime = FindFirstObjectByType<EdgarDungeonRuntime>();
            return _dungeonRuntime != null && _dungeonRuntime.IsReady
                ? _dungeonRuntime.WorldRotationDegrees
                : 0;
        }
    }
}
