using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// 角色阴影 Atlas 注册组件。
    /// 挂载到角色 GameObject 上，自动注册到阴影 Atlas 系统。
    /// CharacterShadowAtlasRenderFeature 会根据优先级分配 Atlas 区域。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Character Shadow Atlas Target")]
    public class CharacterShadowAtlasTarget : MonoBehaviour
    {
        /// <summary>
        /// 角色阴影优先级。数值越小优先级越高，分配的 Atlas 区域越大。
        /// </summary>
        public enum ShadowPriority
        {
            /// <summary>主角 — 最高分辨率（如 1024×1024）</summary>
            Hero = 0,
            /// <summary>队友/重要NPC — 中等分辨率（如 512×512）</summary>
            Important = 1,
            /// <summary>普通NPC — 较低分辨率（如 256×256）</summary>
            Normal = 2,
            /// <summary>远处角色 — 最低分辨率（如 128×128）</summary>
            Low = 3,
        }

        [Header("阴影设置")]
        [Tooltip("角色阴影优先级，决定在 Atlas 中分配的区域大小")]
        public ShadowPriority priority = ShadowPriority.Important;

        [Tooltip("Bounds 膨胀量（米），防止动画极端姿势时阴影被裁切")]
        [Range(0f, 2f)]
        public float boundsPadding = 0.3f;

        [Tooltip("是否参与阴影投射（关闭后不占用 Atlas 区域）")]
        public bool castShadow = true;

        // ====================================================================
        // 静态注册表
        // ====================================================================
        private static readonly List<CharacterShadowAtlasTarget> s_Instances = new List<CharacterShadowAtlasTarget>(16);

        /// <summary>获取当前所有已注册的角色阴影目标（按优先级排序）</summary>
        public static IReadOnlyList<CharacterShadowAtlasTarget> Instances => s_Instances;

        /// <summary>注册表版本号，每次增删时递增，供外部检测变化</summary>
        public static int Version { get; private set; }

        // ====================================================================
        // 运行时数据（由 RenderFeature 填充）
        // ====================================================================

        /// <summary>当前帧计算的世界空间 Bounds（已膨胀）</summary>
        [NonSerialized] public Bounds worldBounds;

        /// <summary>在 Atlas 中分配的区域（像素坐标）</summary>
        [NonSerialized] public RectInt atlasRect;

        /// <summary>光源空间 VP 矩阵（世界 -> shadow UV）</summary>
        [NonSerialized] public Matrix4x4 shadowVP;

        /// <summary>当前帧是否被分配了 Atlas 区域</summary>
        [NonSerialized] public bool isAllocated;

        // 缓存的 Renderer 列表
        private Renderer[] cachedRenderers;

        private void OnEnable()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(false);
            if (!s_Instances.Contains(this))
            {
                s_Instances.Add(this);
                SortInstances();
                Version++;
            }
        }

        private void OnDisable()
        {
            if (s_Instances.Remove(this))
                Version++;
            isAllocated = false;
        }

        /// <summary>
        /// 收集所有子 Renderer 的合并 Bounds，并应用膨胀。
        /// 由 RenderFeature 每帧调用。
        /// </summary>
        public Bounds CollectBounds()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>(false);
            }

            bool first = true;
            Bounds bounds = default;

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;

                if (first)
                {
                    bounds = r.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (first)
            {
                // 没有有效 Renderer，使用 transform 位置
                bounds = new Bounds(transform.position, Vector3.one);
            }

            // 膨胀
            bounds.Expand(boundsPadding * 2f);
            worldBounds = bounds;
            return bounds;
        }

        /// <summary>获取缓存的 Renderer 列表（供 RenderPass 逐 Renderer 绘制）</summary>
        public Renderer[] GetCachedRenderers()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                cachedRenderers = GetComponentsInChildren<Renderer>(false);
            return cachedRenderers;
        }

        /// <summary>刷新 Renderer 缓存（角色换装/动态添加部件后调用）</summary>
        public void RefreshRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(false);
        }

        private static void SortInstances()
        {
            s_Instances.Sort((a, b) => ((int)a.priority).CompareTo((int)b.priority));
        }

#if UNITY_EDITOR
        // ====================================================================
        // Editor 可视化
        // ====================================================================

        [Header("Editor 可视化")]
        [Tooltip("在 Scene 视图中绘制阴影 debug 信息")]
        public bool showGizmos = true;

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            DrawShadowGizmos(false);
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            // 非选中时只画简单的 Bounds 线框
            if (!UnityEditor.Selection.Contains(gameObject))
            {
                if (worldBounds.size.sqrMagnitude > 0.001f)
                {
                    Gizmos.color = isAllocated ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
                    Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
                }
            }
        }

        private void DrawShadowGizmos(bool simple)
        {
            // 1. 绘制合并后的 Bounds（黄色线框）
            Bounds bounds = CollectBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // 2. 绘制每个子 Renderer 的单独 Bounds（青色半透明）
            if (cachedRenderers != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    var r = cachedRenderers[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                        continue;
                    Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
                }
            }

            // 3. 绘制光源方向（橙色箭头）
            Light mainLight = RenderSettings.sun;
            if (mainLight != null)
            {
                Vector3 lightDir = mainLight.transform.forward;
                Vector3 center = bounds.center;

                Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
                // 从 Bounds 中心画光源方向箭头
                Vector3 arrowEnd = center + lightDir * 3f;
                Gizmos.DrawLine(center, arrowEnd);
                // 箭头头部
                Vector3 arrowRight = Vector3.Cross(lightDir, Vector3.up).normalized;
                if (arrowRight.sqrMagnitude < 0.01f)
                    arrowRight = Vector3.Cross(lightDir, Vector3.forward).normalized;
                Gizmos.DrawLine(arrowEnd, arrowEnd - lightDir * 0.3f + arrowRight * 0.15f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - lightDir * 0.3f - arrowRight * 0.15f);

                // 4. 绘制光源空间的投影框（绿色 = 已分配，红色 = 未分配）
                if (isAllocated)
                {
                    DrawLightSpaceProjection(bounds, lightDir);
                }
            }

            // 5. 在 Scene 视图中显示文字信息
            DrawInfoLabel(bounds);
        }

        /// <summary>绘制光源空间的正交投影框</summary>
        private void DrawLightSpaceProjection(Bounds bounds, Vector3 lightDir)
        {
            Vector3 forward = lightDir;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right);

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // 将 8 个顶点投影到光源空间
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + Vector3.Scale(extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f
                ));

                Vector3 offset = corner - center;
                float lx = Vector3.Dot(offset, right);
                float ly = Vector3.Dot(offset, up);
                float lz = Vector3.Dot(offset, forward);

                minX = Mathf.Min(minX, lx); maxX = Mathf.Max(maxX, lx);
                minY = Mathf.Min(minY, ly); maxY = Mathf.Max(maxY, ly);
                minZ = Mathf.Min(minZ, lz); maxZ = Mathf.Max(maxZ, lz);
            }

            // 光源位置（从 Bounds 中心沿光源反方向后退）
            float lightBackDist = -minZ + 2f; // 使用默认 padding
            Vector3 lightPos = center - forward * lightBackDist;

            // 绘制投影框的 8 个顶点
            float orthoFar = (maxZ - minZ) + 4f; // near padding + far padding
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? minX : maxX;
                float y = (i & 2) == 0 ? minY : maxY;
                float z = (i & 4) == 0 ? 0f : orthoFar;
                corners[i] = lightPos + right * x + up * y + forward * z;
            }

            // 绘制投影框（绿色）
            Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
            // 近平面
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[3]);
            Gizmos.DrawLine(corners[3], corners[2]);
            Gizmos.DrawLine(corners[2], corners[0]);
            // 远平面
            Gizmos.DrawLine(corners[4], corners[5]);
            Gizmos.DrawLine(corners[5], corners[7]);
            Gizmos.DrawLine(corners[7], corners[6]);
            Gizmos.DrawLine(corners[6], corners[4]);
            // 连接线
            Gizmos.DrawLine(corners[0], corners[4]);
            Gizmos.DrawLine(corners[1], corners[5]);
            Gizmos.DrawLine(corners[2], corners[6]);
            Gizmos.DrawLine(corners[3], corners[7]);

            // 绘制光源位置（白色球）
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(lightPos, 0.1f);
        }

        /// <summary>在 Scene 视图中显示文字信息</summary>
        private void DrawInfoLabel(Bounds bounds)
        {
            string info = $"Shadow: {priority}";
            info += $"\nBounds Size: ({bounds.size.x:F2}, {bounds.size.y:F2}, {bounds.size.z:F2})";
            info += $"\nBounds Center: ({bounds.center.x:F2}, {bounds.center.y:F2}, {bounds.center.z:F2})";

            if (isAllocated)
            {
                info += $"\nAtlas Rect: ({atlasRect.x}, {atlasRect.y}, {atlasRect.width}x{atlasRect.height})";
                info += $"\n<color=green>已分配</color>";
            }
            else
            {
                info += $"\n<color=red>未分配</color>";
            }

            // 列出每个 Renderer 的 Bounds 大小
            if (cachedRenderers != null)
            {
                info += $"\n--- Renderers ({cachedRenderers.Length}) ---";
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    var r = cachedRenderers[i];
                    if (r == null) continue;
                    var rb = r.bounds;
                    info += $"\n  [{i}] {r.gameObject.name}: size=({rb.size.x:F2},{rb.size.y:F2},{rb.size.z:F2})";
                }
            }

            Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + 0.5f), info,
                new GUIStyle("label")
                {
                    fontSize = 11,
                    normal = { textColor = Color.white },
                    richText = true
                });
        }
#endif
    }
}
