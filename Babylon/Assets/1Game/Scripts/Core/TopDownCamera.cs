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
        [SerializeField] private Vector3 offset = new(0, 22f, -12.7f);
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private float lookDownAngle = 60f;
        [SerializeField, Range(20f, 80f)] private float verticalFov = 48f;

        private Transform _target;
        private EdgarDungeonRuntime _dungeonRuntime;
        private int _lastDungeonRotation = int.MinValue;
        private Vector3 _focusPoint;
        private float _focusRemaining;
        private bool _interactionFocus;

        public bool IsFocusActive => _focusRemaining > 0f;
        public bool IsInteractionFocusActive => _interactionFocus;

        private void Awake()
        {
            ApplyLens();
        }

        private void Start()
        {
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            transform.rotation = Quaternion.Euler(lookDownAngle, 0, 0);
        }

        private void OnValidate()
        {
            ApplyLens();
        }

        private void ApplyLens()
        {
            Camera attachedCamera = GetComponent<Camera>();
            if (attachedCamera != null)
                attachedCamera.fieldOfView = verticalFov;
        }

        private void LateUpdate()
        {
            UpdateLens();
            if (_target == null)
            {
                if (PlayerController.Instance != null)
                    _target = PlayerController.Instance.transform;
                return;
            }

            if (_focusRemaining > 0f)
                _focusRemaining -= Time.unscaledDeltaTime;

            int dungeonRotation = GetDungeonRotation();
            Quaternion layoutRotation = Quaternion.Euler(0f, dungeonRotation, 0f);
            Vector3 followPoint = IsFocusActive
                ? _focusPoint
                : _target.position;
            Vector3 desiredPos = followPoint + layoutRotation * offset;
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

            float interpolation = smoothSpeed *
                (IsFocusActive
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, interpolation);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, interpolation);
        }

        /// <summary>
        /// 短暂聚焦序章关键构图，结束后自动回到玩家。
        /// 使用非缩放时间，选择界面暂停时也能完成推镜。
        /// </summary>
        public void FocusOn(Vector3 worldPoint, float duration)
        {
            _focusPoint = worldPoint;
            _focusRemaining = Mathf.Max(0f, duration);
        }

        /// <summary>
        /// 首次附着等短交互期间轻微收紧构图，退出交互后自动恢复。
        /// 镜头移动使用非缩放时间，选择流程暂停时仍能平滑完成。
        /// </summary>
        public void SetInteractionFocus(bool active)
        {
            _interactionFocus = active;
        }

        private void UpdateLens()
        {
            Camera attachedCamera = GetComponent<Camera>();
            if (attachedCamera == null)
                return;
            float targetFov = Mathf.Max(
                20f,
                verticalFov - (_interactionFocus ? 4f : 0f));
            attachedCamera.fieldOfView = Mathf.MoveTowards(
                attachedCamera.fieldOfView,
                targetFov,
                18f * Time.unscaledDeltaTime);
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
