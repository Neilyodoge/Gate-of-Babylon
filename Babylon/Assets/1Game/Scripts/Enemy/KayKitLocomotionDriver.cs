using UnityEngine;

namespace XianTu
{
    /// <summary>为没有动画接线的 KayKit 角色按实际位移驱动 Idle / Run 混合树。</summary>
    public sealed class KayKitLocomotionDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");

        [SerializeField] private float damping = 0.12f;

        private Animator _animator;
        private Vector3 _lastPosition;

        private void Awake()
        {
            DynamicCharacterRendering.Apply(gameObject);
            _animator = GetComponentInChildren<Animator>(true);
            _lastPosition = transform.position;
            if (_animator != null)
                _animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (_animator == null)
                return;

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            _animator.SetFloat(SpeedId, delta.magnitude / deltaTime, damping, deltaTime);
        }

        public void PlayAttack()
        {
            if (_animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in
                     _animator.parameters)
            {
                if (parameter.nameHash == AttackId &&
                    parameter.type ==
                    AnimatorControllerParameterType.Trigger)
                {
                    _animator.SetTrigger(AttackId);
                    return;
                }
            }
        }
    }
}
