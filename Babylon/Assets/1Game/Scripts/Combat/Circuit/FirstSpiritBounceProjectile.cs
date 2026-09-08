using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 首批三宠的轻量追踪弹射物。命中由目标序列驱动，
    /// 不复用Legacy Projectile，避免把回路追加伤害再次视作载体命中。
    /// </summary>
    public sealed class FirstSpiritBounceProjectile : MonoBehaviour
    {
        private GameObject[] _targets;
        private Action<GameObject, Vector3, int> _onImpact;
        private float _speed;
        private float _remainingLifetime;
        private int _targetIndex;
        private int _resolvedHits;
        private bool _initialized;
        private float _bounceDelay;
        private float _remainingBounceDelay;
        private float _speedMultiplierPerHit = 1f;

        public int ResolvedHits => _resolvedHits;

        public void Initialize(
            GameObject primaryTarget,
            GameObject bounceTarget,
            float speed,
            float lifetime,
            Action<GameObject, Vector3, int> onImpact)
        {
            if (primaryTarget == null)
                throw new ArgumentNullException(nameof(primaryTarget));
            if (speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (lifetime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(lifetime));

            Initialize(
                bounceTarget != null &&
                bounceTarget != primaryTarget
                    ? new[] { primaryTarget, bounceTarget }
                    : new[] { primaryTarget },
                speed,
                lifetime,
                0f,
                0f,
                1f,
                onImpact);
        }

        public void Initialize(
            IReadOnlyList<GameObject> targets,
            float speed,
            float lifetime,
            float bounceDelay,
            float initialDelay,
            float speedMultiplierPerHit,
            Action<GameObject, Vector3, int> onImpact)
        {
            if (targets == null || targets.Count == 0)
                throw new ArgumentException(
                    "At least one projectile target is required.",
                    nameof(targets));
            if (speed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (lifetime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(lifetime));

            var unique = new HashSet<GameObject>();
            var normalized = new List<GameObject>();
            foreach (GameObject target in targets)
            {
                if (target != null && unique.Add(target))
                    normalized.Add(target);
            }
            if (normalized.Count == 0)
                throw new ArgumentException(
                    "At least one non-null target is required.",
                    nameof(targets));

            _targets = normalized.ToArray();
            _speed = speed;
            _remainingLifetime = lifetime;
            _bounceDelay = Mathf.Max(0f, bounceDelay);
            _remainingBounceDelay = Mathf.Max(0f, initialDelay);
            _speedMultiplierPerHit = Mathf.Max(
                1f,
                speedMultiplierPerHit);
            _onImpact = onImpact;
            _targetIndex = 0;
            _resolvedHits = 0;
            _initialized = true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized || deltaTime <= 0f)
                return;

            _remainingLifetime -= deltaTime;
            if (_remainingLifetime <= 0f)
            {
                Complete();
                return;
            }

            if (_remainingBounceDelay > 0f)
            {
                _remainingBounceDelay -= deltaTime;
                return;
            }

            while (_targetIndex < _targets.Length &&
                   !IsTargetAlive(_targets[_targetIndex]))
            {
                _targetIndex++;
            }
            if (_targetIndex >= _targets.Length)
            {
                Complete();
                return;
            }

            GameObject target = _targets[_targetIndex];
            Vector3 destination =
                target.transform.position + Vector3.up * 0.6f;
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                _speed * deltaTime);
            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);

            if ((transform.position - destination).sqrMagnitude > 0.0025f)
                return;

            _onImpact?.Invoke(
                target,
                transform.position,
                _resolvedHits);
            _resolvedHits++;
            _targetIndex++;
            _speed *= _speedMultiplierPerHit;
            if (_targetIndex < _targets.Length)
                _remainingBounceDelay = _bounceDelay;
            if (_targetIndex >= _targets.Length)
                Complete();
        }

        private static bool IsTargetAlive(GameObject target)
        {
            if (target == null)
                return false;

            IDamageable damageable = target.GetComponent<IDamageable>();
            return damageable != null &&
                (damageable.Stats == null || damageable.Stats.IsAlive);
        }

        private void Complete()
        {
            _initialized = false;
            _onImpact = null;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
