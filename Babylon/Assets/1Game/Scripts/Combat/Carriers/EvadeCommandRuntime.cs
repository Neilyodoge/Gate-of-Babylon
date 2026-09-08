using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>身法命令需要的动画、事件和无敌副作用边界。</summary>
    public interface IEvadeCommandHost
    {
        bool TryPlayEvade();
        void BufferEvade();
        void SetEvadeInvincible(float duration);
        void PublishEvadeCharge(
            int currentCharges,
            int maxCharges,
            float rechargeProgress);
        void PublishEvadeFinished(Vector3 endPosition, Vector3 direction);
    }

    /// <summary>
    /// 闪避输入、方向、位移状态和逐格充能运行时。
    /// CharacterController移动、动画、事件和无敌由宿主处理。
    /// </summary>
    public sealed class EvadeCommandRuntime
    {
        private const float InvincibleDuration = 0.3f;
        private readonly IEvadeCommandHost _host;
        private float _dashDistance = 5f;
        private float _dashDuration = 0.2f;
        private int _charges = 2;
        private int _maxCharges = 2;
        private float _rechargeTimer;
        private float _rechargeDuration = 1.5f;
        private float _dashTimer;

        public bool IsDashing { get; private set; }
        public bool RequestedThisFrame { get; private set; }
        public Vector3 Direction { get; private set; }
        public int Charges => _charges;
        public int MaxCharges => _maxCharges;

        public EvadeCommandRuntime(IEvadeCommandHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void Configure(
            float dashDistance,
            float dashDuration,
            int maxCharges,
            float rechargeDuration)
        {
            _dashDistance = dashDistance;
            _dashDuration = dashDuration;
            _maxCharges = Mathf.Max(1, maxCharges);
            _charges = _maxCharges;
            _rechargeDuration = rechargeDuration;
            _rechargeTimer = 0f;
            IsDashing = false;
        }

        public void BeginFrame()
        {
            RequestedThisFrame = false;
        }

        public bool HandleInput(
            bool pressedThisFrame,
            bool disabled,
            Vector3 movementDirection,
            Vector3 aimDirection)
        {
            if (IsDashing || disabled || !pressedThisFrame)
                return false;

            RequestedThisFrame = true;
            if (_charges <= 0)
            {
                _host.BufferEvade();
                return false;
            }

            return TryExecute(movementDirection, aimDirection);
        }

        public bool HandleBuffered(
            bool isAlive,
            Vector3 movementDirection,
            Vector3 aimDirection)
        {
            if (!isAlive || _charges <= 0)
                return false;

            return TryExecute(movementDirection, aimDirection);
        }

        public void TickRecharge(float deltaTime)
        {
            if (_charges >= _maxCharges)
                return;

            _rechargeTimer -= deltaTime;
            if (_rechargeTimer <= 0f)
            {
                _charges++;
                _rechargeTimer = _charges < _maxCharges
                    ? _rechargeDuration
                    : 0f;
            }

            _host.PublishEvadeCharge(
                _charges,
                _maxCharges,
                RechargeProgress());
        }

        public bool TickDash(
            float deltaTime,
            Vector3 currentPosition,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!IsDashing)
                return false;

            velocity = Direction *
                (_dashDistance / Mathf.Max(0.0001f, _dashDuration));
            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f)
            {
                IsDashing = false;
                _host.PublishEvadeFinished(currentPosition, Direction);
            }

            return true;
        }

        public void SetMaxCharges(int newMax)
        {
            _maxCharges = Mathf.Max(1, newMax);
            if (_charges > _maxCharges)
                _charges = _maxCharges;
        }

        public void RestoreCharges()
        {
            _charges = _maxCharges;
            _rechargeTimer = 0f;
        }

        private bool TryExecute(
            Vector3 movementDirection,
            Vector3 aimDirection)
        {
            if (!_host.TryPlayEvade())
                return false;

            IsDashing = true;
            _dashTimer = _dashDuration;
            Direction = movementDirection.sqrMagnitude > 0.01f
                ? movementDirection
                : aimDirection;
            _charges--;
            if (_charges < _maxCharges && _rechargeTimer <= 0f)
                _rechargeTimer = _rechargeDuration;

            _host.PublishEvadeCharge(
                _charges,
                _maxCharges,
                RechargeProgress());
            _host.SetEvadeInvincible(InvincibleDuration);
            return true;
        }

        private float RechargeProgress()
        {
            return _rechargeTimer > 0f && _rechargeDuration > 0f
                ? 1f - _rechargeTimer / _rechargeDuration
                : 1f;
        }
    }
}
