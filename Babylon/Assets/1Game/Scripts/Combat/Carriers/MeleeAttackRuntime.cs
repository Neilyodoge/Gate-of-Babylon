using System;
using UnityEngine;

namespace XianTu
{
    public readonly struct MeleeHitResult
    {
        public bool HitAny { get; }
        public object FirstTarget { get; }
        public Vector3 FirstHitPoint { get; }

        public MeleeHitResult(
            bool hitAny,
            object firstTarget,
            Vector3 firstHitPoint)
        {
            HitAny = hitAny;
            FirstTarget = firstTarget;
            FirstHitPoint = firstHitPoint;
        }
    }

    /// <summary>普攻动画、命中查询与兼容事件的副作用边界。</summary>
    public interface IMeleeAttackHost
    {
        bool IsRangedBasic { get; }
        bool IsHitWindowOpen { get; }
        int ComboStep { get; }
        void RequestMeleeAttack(float attackSpeed);
        void DrawMeleeRange(bool activeWindow);
        MeleeHitResult ResolveMeleeHits(int comboStep);
        void PublishMeleeHit(int comboStep, MeleeHitResult result);
        void ReduceCooldownOnComboFinisher();
    }

    /// <summary>
    /// 普攻输入门控、单段命中锁和连段收尾编排。
    /// 动画、物理、伤害及VFX仍由宿主处理。
    /// </summary>
    public sealed class MeleeAttackRuntime
    {
        private readonly IMeleeAttackHost _host;
        private int _lastHitComboStep = -1;
        private bool _hasHitThisSwing;

        public MeleeAttackRuntime(IMeleeAttackHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool TryRequestAttack(
            bool attackPressed,
            bool dashRequestedThisFrame,
            bool pointerOverSkillSlot,
            float attackSpeed)
        {
            if (!attackPressed ||
                dashRequestedThisFrame ||
                pointerOverSkillSlot)
            {
                return false;
            }

            _host.RequestMeleeAttack(attackSpeed);
            return true;
        }

        public bool TickHitWindow()
        {
            if (_host.IsRangedBasic)
                return false;

            if (!_host.IsHitWindowOpen)
            {
                _host.DrawMeleeRange(false);
                return false;
            }

            _host.DrawMeleeRange(true);
            int comboStep = _host.ComboStep;
            if (_lastHitComboStep == comboStep && _hasHitThisSwing)
                return false;

            MeleeHitResult result = _host.ResolveMeleeHits(comboStep);
            if (!result.HitAny)
                return false;

            _hasHitThisSwing = true;
            _lastHitComboStep = comboStep;
            _host.PublishMeleeHit(comboStep, result);
            if (comboStep == 2)
                _host.ReduceCooldownOnComboFinisher();
            return true;
        }

        public void ResetSwing()
        {
            _hasHitThisSwing = false;
        }
    }
}
