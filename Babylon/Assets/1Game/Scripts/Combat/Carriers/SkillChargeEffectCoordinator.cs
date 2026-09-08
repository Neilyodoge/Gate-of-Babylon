using System;

namespace XianTu
{
    /// <summary>蓄力会话的移动与表现副作用边界。</summary>
    public interface ISkillChargeEffectHost
    {
        bool CanAdjustMovement { get; }
        float MoveSpeed { get; set; }
        void PublishChargeProgress(
            int slotIndex,
            float chargeTime,
            int chargeLevel,
            bool isCharging);
        void LogChargeEffect(string message);
    }

    /// <summary>
    /// 蓄力开始、完成和取消的副作用协调器。
    /// 蓄力状态仍由SkillChargeInputRuntime维护，本类只保证移速恢复和事件顺序。
    /// </summary>
    public sealed class SkillChargeEffectCoordinator
    {
        private readonly ISkillChargeEffectHost _host;
        private float _originalMoveSpeed;
        private bool _movementAdjusted;

        public bool SessionActive { get; private set; }

        public SkillChargeEffectCoordinator(ISkillChargeEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void Begin(int slotIndex, SkillData skill)
        {
            SessionActive = true;
            _movementAdjusted = false;
            if (skill.chargeMoveSpeedMultiplier < 1f &&
                _host.CanAdjustMovement)
            {
                _originalMoveSpeed = _host.MoveSpeed;
                _host.MoveSpeed *= skill.chargeMoveSpeedMultiplier;
                _movementAdjusted = true;
            }

            _host.LogChargeEffect(
                $"<color=yellow>开始蓄力：{skill.skillName}</color>");
            _host.PublishChargeProgress(slotIndex, 0f, 1, true);
        }

        public void Complete(int slotIndex)
        {
            RestoreMovement();
            SessionActive = false;
            _host.PublishChargeProgress(slotIndex, 0f, 1, false);
        }

        public void Cancel(int slotIndex, Action resetChargeState)
        {
            RestoreMovement();
            resetChargeState?.Invoke();
            SessionActive = false;
            _host.PublishChargeProgress(slotIndex, 0f, 1, false);
            _host.LogChargeEffect("<color=gray>蓄力被中断</color>");
        }

        private void RestoreMovement()
        {
            if (!_movementAdjusted || !_host.CanAdjustMovement)
                return;

            _host.MoveSpeed = _originalMoveSpeed;
            _movementAdjusted = false;
        }
    }
}
