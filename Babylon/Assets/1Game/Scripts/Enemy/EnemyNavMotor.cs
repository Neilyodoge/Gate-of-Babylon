using UnityEngine;
using UnityEngine.AI;

namespace XianTu
{
    /// <summary>
    /// 敌人共享导航 Motor。NavMeshAgent 只负责寻路与避障，
    /// 实际位移仍由 CharacterController 执行，避免与现有击退/技能位移冲突。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyNavMotor : MonoBehaviour
    {
        private const float SampleRadius = 3f;

        private CharacterController _controller;
        private NavMeshAgent _agent;
        private bool _warnedOffMesh;

        public bool IsOnNavMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
                _agent = gameObject.AddComponent<NavMeshAgent>();

            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.autoBraking = true;
            _agent.autoRepath = true;
            _agent.acceleration = 28f;
            _agent.angularSpeed = 720f;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            SyncShape();
        }

        public bool MoveTo(Vector3 destination, float speed, float stoppingDistance = 0.1f)
        {
            if (!EnsureOnNavMesh())
                return false;

            _agent.speed = Mathf.Max(0f, speed);
            _agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
            _agent.nextPosition = transform.position;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, SampleRadius, _agent.areaMask))
                destination = hit.position;
            if (!_agent.SetDestination(destination))
                return false;

            Vector3 velocity = _agent.desiredVelocity;
            velocity.y = -9.8f;
            _controller.Move(velocity * Time.deltaTime);
            _agent.nextPosition = transform.position;
            return true;
        }

        public void Stop()
        {
            if (!IsOnNavMesh)
                return;
            _agent.ResetPath();
            _agent.nextPosition = transform.position;
        }

        public void ResyncAfterForcedMove()
        {
            if (_agent == null || !_agent.enabled)
                return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, SampleRadius, _agent.areaMask))
                _agent.Warp(hit.position);
        }

        public bool TryGetNavigablePosition(
            Vector3 candidate,
            out Vector3 position,
            float maxDistance = SampleRadius)
        {
            int areaMask = _agent != null ? _agent.areaMask : NavMesh.AllAreas;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, maxDistance, areaMask))
            {
                position = hit.position;
                return true;
            }
            position = transform.position;
            return false;
        }

        private bool EnsureOnNavMesh()
        {
            if (_agent == null || !_agent.enabled)
                return false;
            SyncShape();
            if (_agent.isOnNavMesh)
                return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, SampleRadius, _agent.areaMask)
                && _agent.Warp(hit.position))
            {
                _warnedOffMesh = false;
                return true;
            }

            if (!_warnedOffMesh)
            {
                _warnedOffMesh = true;
                Debug.LogWarning($"[NavMesh] {name} 不在可导航表面，暂停追击而不是直线穿墙。");
            }
            return false;
        }

        private void SyncShape()
        {
            if (_controller == null || _agent == null)
                return;
            _agent.radius = Mathf.Max(0.1f, _controller.radius);
            _agent.height = Mathf.Max(_agent.radius * 2f, _controller.height);
            _agent.baseOffset = _controller.center.y - _controller.height * 0.5f;
        }
    }
}
