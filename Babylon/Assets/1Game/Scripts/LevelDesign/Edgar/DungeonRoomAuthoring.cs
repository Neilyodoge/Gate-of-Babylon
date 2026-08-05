using System;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    [Flags]
    public enum EnemyCategoryMask
    {
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Magic = 1 << 2,
        All = Melee | Ranged | Magic
    }

    [ExecuteAlways]
    public sealed class DungeonRoomAuthoring : MonoBehaviour
    {
        [SerializeField, InspectorName("房间有效范围")]
        [Tooltip("绿色线框代表房间可用空间。建议引用工具创建的 BoxCollider，并手动拉伸到墙体内侧。")]
        private BoxCollider validBounds;

        [SerializeField, InspectorName("美术分区")]
        [Tooltip("用于工具检查和未来区域节点图。Outer=外环，Transition=连接区，Inner=内环。")]
        private District district;

        [SerializeField, InspectorName("房间标签")]
        [Tooltip("英文标签用于自动筛选内容，例如 Combat、Ritual、Large。多个标签分别填写。")]
        private string[] roomTags = Array.Empty<string>();

        [SerializeField, InspectorName("始终显示辅助范围")]
        [Tooltip("开启后，即使未选中房间，也在 Scene 视图显示有效范围。")]
        private bool alwaysShowBounds = true;

        public BoxCollider ValidBounds => validBounds;
        public District District => district;
        public string[] RoomTags => roomTags;

        public void Configure(BoxCollider bounds)
        {
            validBounds = bounds;
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowBounds)
                DrawBounds(new Color(0.2f, 0.9f, 0.35f, 0.8f));
        }

        private void OnDrawGizmosSelected()
        {
            DrawBounds(new Color(0.2f, 1f, 0.4f, 1f));
        }

        private void DrawBounds(Color color)
        {
            if (validBounds == null) return;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = validBounds.transform.localToWorldMatrix;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.08f);
            Gizmos.DrawCube(validBounds.center, validBounds.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(validBounds.center, validBounds.size);
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }

    [ExecuteAlways]
    public sealed class DungeonEnemySpawnArea : MonoBehaviour
    {
        [SerializeField, InspectorName("刷新范围碰撞体")]
        [Tooltip("可使用 BoxCollider、SphereCollider、CapsuleCollider 或 MeshCollider。Mesh 会按表面向下采样，不要求地图是方形。")]
        private Collider areaCollider;

        [SerializeField, InspectorName("允许的怪物分类")]
        [Tooltip("限制该区域允许生成近战、远程或法术怪物；可多选。")]
        private EnemyCategoryMask allowedCategories = EnemyCategoryMask.All;

        [SerializeField, InspectorName("本区域最大刷新数")]
        [Tooltip("单次遭遇最多从这个范围取多少个点。0表示不限制。")]
        [Min(0)] private int maxSpawnCount;

        [SerializeField, InspectorName("区域抽取权重")]
        [Tooltip("存在多个刷新范围时的相对选中概率。")]
        [Min(1)] private int weight = 100;

        [SerializeField, InspectorName("离玩家最小距离")]
        [Tooltip("候选点距离玩家小于该值时会被拒绝。项目基础安全距离为5米。")]
        [Min(0f)] private float minPlayerDistance = 5f;

        [SerializeField, InspectorName("怪物之间最小距离")]
        [Tooltip("同一波刷新点之间的最小间距，避免重叠。")]
        [Min(0f)] private float minSeparation = 1.5f;

        [SerializeField, InspectorName("离地偏移")]
        [Tooltip("在采样表面上方增加的高度，避免角色脚底陷入地面。")]
        private float groundOffset = 0.1f;

        [SerializeField, InspectorName("始终显示范围")]
        [Tooltip("开启后在Scene视图持续显示橙色范围；关闭后仅选中时显示。")]
        private bool alwaysShow = true;

        public Collider AreaCollider => areaCollider;
        public int MaxSpawnCount => maxSpawnCount;
        public int Weight => Mathf.Max(1, weight);
        public float MinPlayerDistance => minPlayerDistance;
        public float MinSeparation => minSeparation;

        public bool Allows(EnemyCombatCategory category)
        {
            EnemyCategoryMask mask = category switch
            {
                EnemyCombatCategory.Melee => EnemyCategoryMask.Melee,
                EnemyCombatCategory.Ranged => EnemyCategoryMask.Ranged,
                EnemyCombatCategory.Magic => EnemyCategoryMask.Magic,
                _ => EnemyCategoryMask.All
            };
            return (allowedCategories & mask) != 0;
        }

        public void Configure(Collider collider)
        {
            areaCollider = collider;
        }

        public bool TryGetRandomPoint(System.Random random, out Vector3 point)
        {
            point = transform.position;
            if (areaCollider == null)
                areaCollider = GetComponent<Collider>();
            if (areaCollider == null || !areaCollider.enabled)
                return false;

            if (areaCollider is BoxCollider box)
            {
                Vector3 local = box.center + new Vector3(
                    ((float)random.NextDouble() - 0.5f) * box.size.x,
                    -box.size.y * 0.5f + groundOffset,
                    ((float)random.NextDouble() - 0.5f) * box.size.z);
                point = box.transform.TransformPoint(local);
                return true;
            }

            Bounds bounds = areaCollider.bounds;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble());
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble());
                var ray = new Ray(new Vector3(x, bounds.max.y + 1f, z), Vector3.down);
                if (areaCollider.Raycast(ray, out var hit, bounds.size.y + 2f))
                {
                    point = hit.point + Vector3.up * groundOffset;
                    return true;
                }

                Vector3 candidate = new Vector3(x, bounds.center.y, z);
                Vector3 closest = areaCollider.ClosestPoint(candidate);
                if ((closest - candidate).sqrMagnitude < 0.001f)
                {
                    point = new Vector3(candidate.x, bounds.min.y + groundOffset, candidate.z);
                    return true;
                }
            }
            return false;
        }

        private void OnValidate()
        {
            if (areaCollider == null)
                areaCollider = GetComponent<Collider>();
        }

        private void OnDrawGizmos()
        {
            if (alwaysShow)
                DrawArea(new Color(1f, 0.45f, 0.05f, 0.8f));
        }

        private void OnDrawGizmosSelected()
        {
            DrawArea(new Color(1f, 0.65f, 0.1f, 1f));
        }

        private void DrawArea(Color color)
        {
            if (areaCollider == null)
                areaCollider = GetComponent<Collider>();
            if (areaCollider == null) return;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.color = color;
            if (areaCollider is BoxCollider box)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.color = new Color(color.r, color.g, color.b, 0.08f);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = color;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (areaCollider is SphereCollider sphere)
            {
                Gizmos.matrix = sphere.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (areaCollider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                Gizmos.matrix = meshCollider.transform.localToWorldMatrix;
                Gizmos.DrawWireMesh(meshCollider.sharedMesh);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(areaCollider.bounds.center, areaCollider.bounds.size);
            }
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }
}
