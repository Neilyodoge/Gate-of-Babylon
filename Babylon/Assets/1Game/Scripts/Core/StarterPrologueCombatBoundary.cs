using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章救援战软边界。越界后将玩家放回边缘内侧，不造成伤害或重置战斗。
    /// </summary>
    public sealed class StarterPrologueCombatBoundary : MonoBehaviour
    {
        private const int RingSegments = 72;

        private Vector3 _center;
        private float _radius;
        private float _warningCooldown;
        private LineRenderer _ring;

        public float Radius => _radius;
        public Vector3 Center => _center;

        private void Update()
        {
            if (PlayerController.Instance == null)
                return;
            _warningCooldown -= Time.deltaTime;

            Transform player =
                PlayerController.Instance.transform;
            Vector3 offset = player.position - _center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= _radius * _radius)
                return;

            Vector3 direction = offset.sqrMagnitude > 0.01f
                ? offset.normalized
                : Vector3.forward;
            Vector3 safePosition =
                _center + direction * (_radius - 1.25f);
            safePosition.y = player.position.y;
            CharacterController controller =
                player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.position = safePosition;
            if (controller != null)
                controller.enabled = true;

            if (_warningCooldown <= 0f)
            {
                _warningCooldown = 2f;
                GameEvents.Publish(
                    new GameEvents.DamageNumberRequested
                    {
                        WorldPosition =
                            safePosition + Vector3.up * 1.8f,
                        Damage = 0f,
                        SpecialTag = "先处理眼前的危险"
                    });
            }
        }

        public void End()
        {
            Destroy(gameObject);
        }

        public static StarterPrologueCombatBoundary Begin(
            Vector3 center,
            float radius)
        {
            StarterPrologueCombatBoundary existing =
                FindObjectOfType<StarterPrologueCombatBoundary>();
            if (existing != null)
                existing.End();

            GameObject root =
                new("StarterPrologueCombatBoundary");
            StarterPrologueCombatBoundary boundary =
                root.AddComponent<StarterPrologueCombatBoundary>();
            boundary._center = center;
            boundary._radius = Mathf.Max(5f, radius);
            boundary.BuildRing();
            return boundary;
        }

        private void BuildRing()
        {
            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.positionCount = RingSegments;
            _ring.startWidth = 0.055f;
            _ring.endWidth = 0.055f;
            _ring.material =
                new Material(Shader.Find("Sprites/Default"));
            _ring.startColor =
                new Color(0.45f, 0.82f, 1f, 0.55f);
            _ring.endColor =
                new Color(0.45f, 0.82f, 1f, 0.55f);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle =
                    i * Mathf.PI * 2f / RingSegments;
                _ring.SetPosition(
                    i,
                    _center +
                    new Vector3(
                        Mathf.Cos(angle) * _radius,
                        0.08f,
                        Mathf.Sin(angle) * _radius));
            }
        }
    }
}
