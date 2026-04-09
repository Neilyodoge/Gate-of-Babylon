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

        // 用于每帧重置模型和根骨骼位置，防止 Generic 动画中的位移
        private Vector3 _initialLocalPosition;
        private Transform _rootBone;           // Bip001 根骨骼
        private Vector3 _rootBoneInitialLocalPos; // Bip001 的初始本地位置

        private void Awake()
        {
            // 向上查找 PlayerAnimator（父物体或更上层）
            _playerAnimator = GetComponentInParent<PlayerAnimator>();
            if (_playerAnimator == null)
            {
                Debug.LogWarning($"[AnimationEventRelay] 未找到 PlayerAnimator 组件！请确保父物体上挂载了 PlayerAnimator。");
            }

            // 查找 Bip001 根骨骼（递归搜索子物体）
            _rootBone = FindChildRecursive(transform, "Bip001");
            if (_rootBone == null)
            {
                Debug.LogWarning("[AnimationEventRelay] 未找到 Bip001 骨骼！动画位移锁定可能不完整。");
            }
        }

        private void Start()
        {
            // 记录初始本地位置
            _initialLocalPosition = transform.localPosition;
            if (_rootBone != null)
            {
                _rootBoneInitialLocalPos = _rootBone.localPosition;
            }
        }

        /// <summary>
        /// 每帧在动画更新后重置模型和根骨骼的位置。
        /// 
        /// Generic 动画中，即使 applyRootMotion = false，Bip001 骨骼的动画曲线
        /// 仍然会直接驱动骨骼位置。我们需要在每帧结束时：
        /// 1. 重置模型 Transform 的 localPosition（防止 Root Motion 残留）
        /// 2. 重置 Bip001 骨骼的 localPosition 的 XZ 分量（防止骨骼动画曲线导致的位移）
        /// 
        /// 使用 LateUpdate 而非 OnAnimatorMove，避免干扰 Animator 状态机的正常工作。
        /// </summary>
        private void LateUpdate()
        {
            // 1. 将模型 Transform 锁定在初始本地位置
            transform.localPosition = _initialLocalPosition;

            // 2. 将 Bip001 根骨骼的 XZ 位移锁定到初始位置
            //    保留 Y 轴（垂直方向），因为跳跃/下蹲等动画需要 Y 轴变化
            if (_rootBone != null)
            {
                Vector3 currentPos = _rootBone.localPosition;
                _rootBone.localPosition = new Vector3(
                    _rootBoneInitialLocalPos.x,  // 锁定 X
                    currentPos.y,                 // 保留 Y（垂直运动）
                    _rootBoneInitialLocalPos.z    // 锁定 Z
                );
            }
        }

        /// <summary>递归查找子物体</summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
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

        // ==================== 源项目动画事件兼容（空实现，防止报错） ====================
        // FBX中可能保留了源项目的动画事件，这些事件在我们项目中不需要处理
        // 但必须有接收方法，否则Unity会报 "has no receiver" 错误

        public void PlayFootSound() { }
        public void PlayFootBackSound() { }
        public void PlayVFX() { }
        public void ATK() { }
        public void EnablePreInput() { }
        public void CancelAttackColdTime() { }
        public void DisableLinkCombo() { }
        public void PlayWeaponBackSound() { }
        public void PlayWeaponEndSound() { }
    }
}
