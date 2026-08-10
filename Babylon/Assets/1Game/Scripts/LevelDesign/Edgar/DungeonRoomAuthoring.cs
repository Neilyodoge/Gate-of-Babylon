using System;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    [Flags]
    public enum EnemyCategoryMask
    {
        [InspectorName("近战")]
        Melee = 1 << 0,
        [InspectorName("远程")]
        Ranged = 1 << 1,
        [InspectorName("法术")]
        Magic = 1 << 2,
        [InspectorName("全部")]
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
        [Tooltip("用于自动筛选内容。配置键仍使用英文，例如 Combat（战斗）、Ritual（仪式）、Large（大型）；多个标签分别填写。")]
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

}
