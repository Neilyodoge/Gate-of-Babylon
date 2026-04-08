using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 动画事件转发器 —— 挂在带有 Animator 的模型子物体上
    /// 将动画事件转发给父物体上的 PlayerAnimator
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        private PlayerAnimator _playerAnimator;

        private void Awake()
        {
            // 向上查找 PlayerAnimator（父物体或更上层）
            _playerAnimator = GetComponentInParent<PlayerAnimator>();
            if (_playerAnimator == null)
            {
                Debug.LogWarning($"[AnimationEventRelay] 未找到 PlayerAnimator 组件！请确保父物体上挂载了 PlayerAnimator。");
            }
        }

        // ==================== 动画事件回调 ====================
        // 这些方法名必须与动画剪辑中的 AnimationEvent 函数名完全一致

        public void OnHitWindowOpen()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnHitWindowOpen();
        }

        public void OnHitWindowClose()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnHitWindowClose();
        }

        public void OnComboWindowOpen()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnComboWindowOpen();
        }

        public void OnComboWindowClose()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnComboWindowClose();
        }

        public void OnAttackEnd()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnAttackEnd();
        }

        public void OnSlashVFX()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnSlashVFX();
        }

        public void OnEvadeEnd()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnEvadeEnd();
        }

        public void OnHitEnd()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnHitEnd();
        }

        public void OnSkillEnd()
        {
            if (_playerAnimator != null)
                _playerAnimator.OnSkillEnd();
        }
    }
}
