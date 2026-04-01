using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    // ========================================================================
    // 设置
    // ========================================================================
    /// <summary>
    /// 角色 Shadow Atlas 设置。
    /// 注意：Atlas 阴影仅在 Runtime (Play Mode) 下生效。
    /// Editor 非运行时，shader 会自动回退到 URP 自带的 CSM 阴影，
    /// 因为 Atlas 纹理只在 RenderFeature 的 Execute 中创建和填充。
    /// </summary>
    [Serializable]
    public class CharacterShadowAtlasSettings
    {
        [Header("Atlas 设置")]
        [Tooltip("Shadow Atlas 总分辨率")]
        public AtlasResolution atlasResolution = AtlasResolution._2048;

        [Tooltip("Hero 优先级角色的 Atlas 区域大小")]
        public TileResolution heroTileSize = TileResolution._1024;

        [Tooltip("Important 优先级角色的 Atlas 区域大小")]
        public TileResolution importantTileSize = TileResolution._512;

        [Tooltip("Normal 优先级角色的 Atlas 区域大小")]
        public TileResolution normalTileSize = TileResolution._256;

        [Tooltip("Low 优先级角色的 Atlas 区域大小")]
        public TileResolution lowTileSize = TileResolution._128;

        [Header("阴影参数")]
        [Tooltip("深度偏移（与 URP Light 的 Bias 含义一致，值越大阴影越远离表面）")]
        [Range(0f, 10f)]
        public float depthBias = 1.0f;

        [Tooltip("法线偏移（与 URP Light 的 Normal Bias 含义一致，值越大沿法线方向偏移越多）")]
        [Range(0f, 10f)]
        public float normalBias = 1.0f;

        [Tooltip("光源空间近裁面额外 padding（从 Bounds 最近点向光源方向延伸）")]
        [Range(0.1f, 20f)]
        public float shadowNearExtent = 5f;

        [Tooltip("光源空间远裁面额外 padding（从 Bounds 最远点向远处延伸）")]
        [Range(0.1f, 20f)]
        public float shadowFarExtent = 5f;

        [Tooltip("正交投影 XY 方向的额外 padding 比例（0.1 = 10% 额外空间，防止动画极端姿势时阴影被裁切）")]
        [Range(0f, 0.5f)]
        public float orthoXYPadding = 0.1f;

        [Header("剔除")]
        [Tooltip("角色阴影渲染层级")]
        public LayerMask layerMask = -1;

        [Tooltip("超过此距离的角色不渲染阴影（相对于主摄像机）")]
        [Range(5f, 100f)]
        public float maxShadowDistance = 30f;

        [Header("调试")]
        [Tooltip("动态调整 Tile 大小：当可见角色数量较少时，自动放大 Tile 尺寸以充分利用 Atlas 空间")]
        public bool dynamicTileSize = true;

        public enum AtlasResolution
        {
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
        }

        public enum TileResolution
        {
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
        }

        /// <summary>根据优先级和可见角色数量获取对应的 tile 大小</summary>
        /// <param name="priority">角色优先级</param>
        /// <param name="visibleCount">当前可见角色数量</param>
        /// <returns>实际分配的 tile 大小（像素）</returns>
        public int GetTileSize(CharacterShadowAtlasTarget.ShadowPriority priority, int visibleCount = -1)
        {
            int baseSize;
            switch (priority)
            {
                case CharacterShadowAtlasTarget.ShadowPriority.Hero: baseSize = (int)heroTileSize; break;
                case CharacterShadowAtlasTarget.ShadowPriority.Important: baseSize = (int)importantTileSize; break;
                case CharacterShadowAtlasTarget.ShadowPriority.Normal: baseSize = (int)normalTileSize; break;
                case CharacterShadowAtlasTarget.ShadowPriority.Low: baseSize = (int)lowTileSize; break;
                default: baseSize = (int)normalTileSize; break;
            }

            // 动态调整：当可见角色数量较少时，自动放大 tile 尺寸
            if (dynamicTileSize && visibleCount > 0)
            {
                int atlas = (int)atlasResolution;
                // 计算在当前角色数量下，每个角色最大可以分配多大的 tile
                // 策略：将 Atlas 平均分配给所有角色，取最大的 2 的幂次方
                // 例如：2048 Atlas + 1 个角色 = 最大 2048
                //        2048 Atlas + 2 个角色 = 每个最大 1024
                //        2048 Atlas + 4 个角色 = 每个最大 1024
                //        2048 Atlas + 5 个角色 = 每个最大 512
                int tilesPerRow = Mathf.CeilToInt(Mathf.Sqrt(visibleCount));
                int maxTileSize = atlas / tilesPerRow;
                // 向下取整到 2 的幂次方（NextPowerOfTwo 向上取整，如果超过则除以 2）
                int pot = Mathf.NextPowerOfTwo(maxTileSize);
                if (pot > maxTileSize) pot /= 2;
                maxTileSize = Mathf.Max(pot, 128); // 最小 128

                // 取 baseSize 和 maxTileSize 中较大的值
                baseSize = Mathf.Max(baseSize, Mathf.Min(maxTileSize, atlas));
            }

            return baseSize;
        }
    }

    // ========================================================================
    // Render Feature
    // ========================================================================
    /// <summary>
    /// 角色高精度 Shadow Atlas RenderFeature。
    /// 仅在 Runtime (Play Mode) 下渲染角色 Atlas 阴影。
    /// Editor 非运行时，shader 自动回退到 URP CSM 阴影，不会出现粉色。
    /// </summary>
    [Serializable]
    public class CharacterShadowAtlasRenderFeature : ScriptableRendererFeature
    {
        [SerializeField]
        public CharacterShadowAtlasSettings settings = new CharacterShadowAtlasSettings();

        private CharacterShadowAtlasPass shadowPass;

        public override void Create()
        {
            shadowPass = new CharacterShadowAtlasPass(settings);

            // 设置全局默认值，确保 Editor 非运行时 shader 不会因为
            // _CharShadowAtlas 为 null 而显示粉色
            Shader.SetGlobalTexture("_CharShadowAtlas", Texture2D.whiteTexture);
            Shader.SetGlobalVector("_CharShadowAtlasParams", Vector4.zero);
            Shader.SetGlobalInt("_CharShadowCount", 0);
            // 默认禁用 Atlas keyword，仅在 Runtime 渲染成功后启用
            Shader.DisableKeyword("_CHAR_SHADOW_ATLAS_ON");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            renderer.EnqueuePass(shadowPass);
        }

        protected override void Dispose(bool disposing)
        {
            shadowPass?.Dispose();
        }
    }

    // ========================================================================
    // Burst 剔除 Job
    // 在光源空间对角色 Bounds 进行视锥剔除 + 距离剔除
    // ========================================================================
    [BurstCompile]
    public struct CharacterShadowCullJob : IJobParallelFor
    {
        // 输入
        [ReadOnly] public NativeArray<float3> boundsCenter;
        [ReadOnly] public NativeArray<float3> boundsExtents;
        [ReadOnly] public float3 cameraPosition;
        [ReadOnly] public float maxDistanceSq;

        // 输出：1 = 可见，0 = 被剔除
        [WriteOnly] public NativeArray<int> cullResults;

        public void Execute(int index)
        {
            float3 center = boundsCenter[index];
            float3 diff = center - cameraPosition;
            float distSq = math.lengthsq(diff);

            // 距离剔除
            if (distSq > maxDistanceSq)
            {
                cullResults[index] = 0;
                return;
            }

            cullResults[index] = 1;
        }
    }

    // ========================================================================
    // Burst VP 矩阵计算 Job
    // 根据角色 Bounds + 光源方向计算紧凑的正交投影 VP 矩阵
    // ========================================================================
    [BurstCompile]
    public struct CharacterShadowVPJob : IJobParallelFor
    {
        // 输入
        [ReadOnly] public NativeArray<float3> boundsCenter;
        [ReadOnly] public NativeArray<float3> boundsExtents;
        [ReadOnly] public NativeArray<int> cullResults;
        [ReadOnly] public float3 lightDir;
        [ReadOnly] public float nearExtent;
        [ReadOnly] public float farExtent;
        [ReadOnly] public float orthoXYPadding;

        // 输出
        public NativeArray<float4x4> viewMatrices;
        public NativeArray<float4x4> projMatrices;

        public void Execute(int index)
        {
            if (cullResults[index] == 0)
                return;

            float3 center = boundsCenter[index];
            float3 extents = boundsExtents[index];

            // 构建光源空间坐标系
            float3 forward = lightDir;
            float3 up = math.abs(math.dot(forward, new float3(0, 1, 0))) > 0.999f
                ? new float3(0, 0, 1)
                : new float3(0, 1, 0);
            float3 right = math.normalize(math.cross(up, forward));
            up = math.cross(forward, right);

            // 先以 Bounds 中心为参考点，将 8 个顶点投影到光源空间，求 XYZ 的 min/max
            float3 localMin = new float3(float.MaxValue);
            float3 localMax = new float3(float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                float3 corner = center + extents * new float3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f
                );

                float3 offset = corner - center;
                float3 local = new float3(
                    math.dot(offset, right),
                    math.dot(offset, up),
                    math.dot(offset, forward)
                );

                localMin = math.min(localMin, local);
                localMax = math.max(localMax, local);
            }

            // 用 Bounds 在光源方向上的实际 Z 范围来确定 near/far
            // nearExtent/farExtent 现在作为额外 padding（单位：米）
            // localMin.z / localMax.z 是 Bounds 相对于中心在光源方向上的最近/最远距离
            float zPadNear = nearExtent;  // 近平面额外 padding
            float zPadFar = farExtent;    // 远平面额外 padding

            // 光源位置：从 Bounds 中心沿光源反方向后退到 Bounds 最近点再加 padding
            float lightBackDist = -localMin.z + zPadNear;
            float3 lightPos = center - forward * lightBackDist;

            // 重新计算 8 个顶点相对于 lightPos 的光源空间坐标
            float3 localMin2 = new float3(float.MaxValue);
            float3 localMax2 = new float3(float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                float3 corner = center + extents * new float3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f
                );

                float3 offset = corner - lightPos;
                float3 local = new float3(
                    math.dot(offset, right),
                    math.dot(offset, up),
                    math.dot(offset, forward)
                );

                localMin2 = math.min(localMin2, local);
                localMax2 = math.max(localMax2, local);
            }

            // 正交投影范围：XY 用 Bounds 投影范围，Z 用紧凑的 near/far
            float orthoLeft = localMin2.x;
            float orthoRight = localMax2.x;
            float orthoBottom = localMin2.y;
            float orthoTop = localMax2.y;

            // 将 XY 范围强制为正方形（因为 tile 是正方形的）
            // 取 XY 中较大的维度作为统一尺寸，这样可以最大化利用 tile 空间
            float xRange = orthoRight - orthoLeft;
            float yRange = orthoTop - orthoBottom;
            float maxRange = math.max(xRange, yRange);

            // 添加 XY padding（防止动画极端姿势时阴影被裁切）
            maxRange *= (1f + orthoXYPadding);

            // 以中心为基准扩展为正方形
            float xCenter = (orthoLeft + orthoRight) * 0.5f;
            float yCenter = (orthoBottom + orthoTop) * 0.5f;
            float halfRange = maxRange * 0.5f;
            orthoLeft = xCenter - halfRange;
            orthoRight = xCenter + halfRange;
            orthoBottom = yCenter - halfRange;
            orthoTop = yCenter + halfRange;

            // near 从 0 开始（lightPos 已经在 Bounds 最近点之前）
            // far 到 Bounds 最远点 + padding
            float orthoNear = 0f;
            float orthoFar = localMax2.z + zPadFar;

            // 构建 View 矩阵（世界 -> 光源空间）
            // float4x4 构造函数参数是列向量（column-major）
            // c0 = 第0列, c1 = 第1列, c2 = 第2列, c3 = 第3列
            float tx = -math.dot(right, lightPos);
            float ty = -math.dot(up, lightPos);
            float tz = -math.dot(forward, lightPos);

            float4x4 view = new float4x4(
                new float4(right.x,   up.x,   forward.x, 0),  // c0
                new float4(right.y,   up.y,   forward.y, 0),  // c1
                new float4(right.z,   up.z,   forward.z, 0),  // c2
                new float4(tx,        ty,     tz,        1)   // c3
            );

            // 翻转第3行（forward/Z 行）使摄像机朝 -Z（Unity 惯例）
            view.c0.z = -view.c0.z;
            view.c1.z = -view.c1.z;
            view.c2.z = -view.c2.z;
            view.c3.z = -view.c3.z;

            // 构建 OpenGL 风格正交投影矩阵（列主序）
            float4x4 proj = float4x4.zero;
            proj.c0.x = 2f / (orthoRight - orthoLeft);
            proj.c1.y = 2f / (orthoTop - orthoBottom);
            proj.c2.z = -2f / (orthoFar - orthoNear);
            proj.c3.x = -(orthoRight + orthoLeft) / (orthoRight - orthoLeft);
            proj.c3.y = -(orthoTop + orthoBottom) / (orthoTop - orthoBottom);
            proj.c3.z = -(orthoFar + orthoNear) / (orthoFar - orthoNear);
            proj.c3.w = 1f;

            viewMatrices[index] = view;
            projMatrices[index] = proj;
        }
    }

    // ========================================================================
    // Atlas 分配器（简单的行式分配）
    // ========================================================================
    public struct AtlasAllocator
    {
        private int atlasSize;
        private int currentX;
        private int currentY;
        private int rowHeight;

        public AtlasAllocator(int atlasSize)
        {
            this.atlasSize = atlasSize;
            currentX = 0;
            currentY = 0;
            rowHeight = 0;
        }

        /// <summary>
        /// 尝试分配一个 tileSize × tileSize 的区域。
        /// 返回 true 表示成功，rect 为分配的像素区域。
        /// </summary>
        public bool TryAllocate(int tileSize, out RectInt rect)
        {
            rect = default;

            if (tileSize > atlasSize)
                return false;

            // 当前行放不下，换行
            if (currentX + tileSize > atlasSize)
            {
                currentX = 0;
                currentY += rowHeight;
                rowHeight = 0;
            }

            // 超出 Atlas 高度
            if (currentY + tileSize > atlasSize)
                return false;

            rect = new RectInt(currentX, currentY, tileSize, tileSize);
            currentX += tileSize;
            rowHeight = Mathf.Max(rowHeight, tileSize);
            return true;
        }

        public void Reset()
        {
            currentX = 0;
            currentY = 0;
            rowHeight = 0;
        }
    }

    // ========================================================================
    // Render Pass
    // ========================================================================
    public class CharacterShadowAtlasPass : ScriptableRenderPass, IDisposable
    {
        private CharacterShadowAtlasSettings settings;
        private RenderTexture shadowAtlas;
        private AtlasAllocator allocator;

        // 每帧的活跃角色列表
        private readonly List<CharacterShadowAtlasTarget> activeTargets = new List<CharacterShadowAtlasTarget>(16);

        // Shader 属性 ID
        private static readonly int _CharShadowAtlas = Shader.PropertyToID("_CharShadowAtlas");
        private static readonly int _CharShadowAtlasParams = Shader.PropertyToID("_CharShadowAtlasParams");
        private static readonly int _CharShadowCount = Shader.PropertyToID("_CharShadowCount");
        private static readonly int _CharShadowVPArray = Shader.PropertyToID("_CharShadowVPArray");
        private static readonly int _CharShadowAtlasRectArray = Shader.PropertyToID("_CharShadowAtlasRectArray");
        private static readonly int _ShadowBias = Shader.PropertyToID("_ShadowBias");

        // 最大支持的角色数量
        private const int MAX_SHADOW_CHARACTERS = 16;

        // VP 矩阵数组（传给 shader）
        private Matrix4x4[] vpMatrixArray = new Matrix4x4[MAX_SHADOW_CHARACTERS];
        // Atlas rect 数组（传给 shader）：(x/atlasSize, y/atlasSize, tileSize/atlasSize, tileSize/atlasSize)
        private Vector4[] atlasRectArray = new Vector4[MAX_SHADOW_CHARACTERS];

        // ShadowCaster 绘制
        private static readonly ShaderTagId shadowCasterTagId = new ShaderTagId("ShadowCaster");

        public CharacterShadowAtlasPass(CharacterShadowAtlasSettings settings)
        {
            this.settings = settings;
            this.profilingSampler = new ProfilingSampler("CharacterShadowAtlas");
            this.renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            allocator = new AtlasAllocator((int)settings.atlasResolution);
        }

        /// <summary>确保 Atlas RT 存在且分辨率正确</summary>
        private void EnsureAtlas(int resolution)
        {
            if (shadowAtlas != null && shadowAtlas.width == resolution)
                return;

            if (shadowAtlas != null)
            {
                shadowAtlas.Release();
                UnityEngine.Object.DestroyImmediate(shadowAtlas);
            }

            // 使用 RenderTextureDescriptor 创建 shadow map
            // 关键：使用 ShadowSamplingMode.RawDepth，允许 shader 端用
            //       TEXTURE2D_FLOAT + SAMPLE_TEXTURE2D 读取原始深度值
            //       然后手动做深度比较（因为 Unity inline sampler 的 Compare 比较函数
            //       固定为 LessEqual，在 Reversed-Z 平台上方向错误导致全白）
            var format = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetDepthStencilFormat(24, 0);
            var rtd = new RenderTextureDescriptor(resolution, resolution,
                UnityEngine.Experimental.Rendering.GraphicsFormat.None, format);
            rtd.shadowSamplingMode = ShadowSamplingMode.RawDepth;
            rtd.useMipMap = false;
            rtd.autoGenerateMips = false;

            shadowAtlas = new RenderTexture(rtd);
            shadowAtlas.name = "CharacterShadowAtlas";
            shadowAtlas.filterMode = FilterMode.Bilinear;
            shadowAtlas.wrapMode = TextureWrapMode.Clamp;
            shadowAtlas.Create();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var targets = CharacterShadowAtlasTarget.Instances;
            if (targets.Count == 0)
            {
                SetEmptyGlobals(context);
                return;
            }

            int atlasSize = (int)settings.atlasResolution;
            EnsureAtlas(atlasSize);

            // ================================================================
            // 阶段 1：收集 Bounds + Burst 剔除
            // ================================================================
            Camera cam = renderingData.cameraData.camera;
            Vector3 camPos = cam.transform.position;

            int count = Mathf.Min(targets.Count, MAX_SHADOW_CHARACTERS);

            // 分配 NativeArray
            var boundsCenter = new NativeArray<float3>(count, Allocator.TempJob);
            var boundsExtents = new NativeArray<float3>(count, Allocator.TempJob);
            var cullResults = new NativeArray<int>(count, Allocator.TempJob);

            // 收集 Bounds
            for (int i = 0; i < count; i++)
            {
                var target = targets[i];
                if (target == null || !target.castShadow || !target.gameObject.activeInHierarchy)
                {
                    boundsCenter[i] = float3.zero;
                    boundsExtents[i] = float3.zero;
                    cullResults[i] = 0; // 预标记为剔除
                    continue;
                }

                Bounds b = target.CollectBounds();
                boundsCenter[i] = b.center;
                boundsExtents[i] = b.extents;
            }

            // Burst 距离剔除 Job
            var cullJob = new CharacterShadowCullJob
            {
                boundsCenter = boundsCenter,
                boundsExtents = boundsExtents,
                cameraPosition = camPos,
                maxDistanceSq = settings.maxShadowDistance * settings.maxShadowDistance,
                cullResults = cullResults,
            };
            var cullHandle = cullJob.Schedule(count, 4);

            // ================================================================
            // 阶段 2：获取光源方向
            // ================================================================
            Light mainLight = RenderSettings.sun;
            if (mainLight == null)
            {
                int mainLightIndex = renderingData.lightData.mainLightIndex;
                if (mainLightIndex >= 0)
                {
                    var visibleLights = renderingData.lightData.visibleLights;
                    if (mainLightIndex < visibleLights.Length)
                        mainLight = visibleLights[mainLightIndex].light;
                }
            }

            if (mainLight == null)
            {
                cullHandle.Complete();
                boundsCenter.Dispose();
                boundsExtents.Dispose();
                cullResults.Dispose();
                SetEmptyGlobals(context);
                return;
            }

            Vector3 lightDir = mainLight.transform.forward;

            // ================================================================
            // 阶段 3：Burst VP 矩阵计算
            // ================================================================
            var viewMatrices = new NativeArray<float4x4>(count, Allocator.TempJob);
            var projMatrices = new NativeArray<float4x4>(count, Allocator.TempJob);

            var vpJob = new CharacterShadowVPJob
            {
                boundsCenter = boundsCenter,
                boundsExtents = boundsExtents,
                cullResults = cullResults,
                lightDir = ((float3)lightDir),
                nearExtent = settings.shadowNearExtent,
                farExtent = settings.shadowFarExtent,
                orthoXYPadding = settings.orthoXYPadding,
                viewMatrices = viewMatrices,
                projMatrices = projMatrices,
            };
            var vpHandle = vpJob.Schedule(count, 4, cullHandle);
            vpHandle.Complete();

            // ================================================================
            // 阶段 4：Atlas 分配
            // ================================================================
            allocator = new AtlasAllocator(atlasSize);
            activeTargets.Clear();
            int allocatedCount = 0;

            // 先统计可见角色数量（用于动态 tile 大小计算）
            int visibleCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (cullResults[i] != 0)
                    visibleCount++;
            }

            for (int i = 0; i < count; i++)
            {
                var target = targets[i];
                target.isAllocated = false;

                if (cullResults[i] == 0)
                    continue;

                int tileSize = settings.GetTileSize(target.priority, visibleCount);
                if (allocator.TryAllocate(tileSize, out RectInt rect))
                {
                    target.atlasRect = rect;
                    target.isAllocated = true;

                    // 从 Job 结果构建 Unity Matrix4x4
                    Matrix4x4 view = viewMatrices[i];
                    // 注意：这里使用原始 proj（OpenGL 风格），不经过 GL.GetGPUProjectionMatrix
                    // 参照 URP ShadowUtils.GetShadowTransform 的做法：
                    //   渲染时由 SetViewProjectionMatrices 内部处理平台转换
                    //   采样时手动翻转 Z 行并做 *0.5+0.5 映射
                    Matrix4x4 proj = projMatrices[i];

                    // 构建 shadow 采样用的变换矩阵（参照 URP ShadowUtils.GetShadowTransform）
                    // 1. 手动翻转 Z 行（Reversed-Z 平台需要）
                    Matrix4x4 projForSample = proj;
                    if (SystemInfo.usesReversedZBuffer)
                    {
                        projForSample.m20 = -projForSample.m20;
                        projForSample.m21 = -projForSample.m21;
                        projForSample.m22 = -projForSample.m22;
                        projForSample.m23 = -projForSample.m23;
                    }
                    Matrix4x4 worldToShadow = projForSample * view;

                    // 2. textureScaleBias: XYZ 都从 [-1,1] 映射到 [0,1]
                    //    （和 URP GetShadowTransform 完全一致）
                    Matrix4x4 textureScaleBias = Matrix4x4.identity;
                    textureScaleBias.m00 = 0.5f;
                    textureScaleBias.m11 = 0.5f;
                    textureScaleBias.m22 = 0.5f;
                    textureScaleBias.m03 = 0.5f;
                    textureScaleBias.m13 = 0.5f;
                    textureScaleBias.m23 = 0.5f;

                    target.shadowVP = textureScaleBias * worldToShadow;

                    // 填充 shader 数组
                    vpMatrixArray[allocatedCount] = target.shadowVP;
                    atlasRectArray[allocatedCount] = new Vector4(
                        (float)rect.x / atlasSize,
                        (float)rect.y / atlasSize,
                        (float)rect.width / atlasSize,
                        (float)rect.height / atlasSize
                    );

                    activeTargets.Add(target);
                    allocatedCount++;
                }
            }

            // ================================================================
            // 阶段 5：渲染阴影到 Atlas
            // ================================================================
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 清空 Atlas（只有深度缓冲，需要明确指定深度的 Load/Store 行为）
                cmd.SetRenderTarget(shadowAtlas,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare,  // 颜色：无
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);     // 深度：Store
                // 使用默认深度清除值 1.0（和 URP shadow 渲染一致）
                // SetViewProjectionMatrices 内部会处理 Reversed-Z 的 ZTest 翻转
                cmd.ClearRenderTarget(true, false, Color.black);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 逐角色渲染 ShadowCaster（每个角色设置自己的 Viewport + VP）
                for (int i = 0; i < activeTargets.Count; i++)
                {
                    var target = activeTargets[i];
                    int idx = -1;
                    // 找到在原始数组中的索引
                    for (int j = 0; j < count; j++)
                    {
                        if (targets[j] == target) { idx = j; break; }
                    }
                    if (idx < 0) continue;

                    Matrix4x4 view = viewMatrices[idx];
                    // 使用原始 proj（OpenGL 风格），让 SetViewProjectionMatrices 内部处理平台转换
                    // 这和 URP ShadowUtils.RenderShadowSlice 的做法一致
                    Matrix4x4 proj = projMatrices[idx];

                    // 设置 Viewport 到 Atlas 对应区域
                    cmd.SetViewport(new Rect(
                        target.atlasRect.x,
                        target.atlasRect.y,
                        target.atlasRect.width,
                        target.atlasRect.height
                    ));

                    // 参照 URP ShadowUtils.RenderShadowSlice：
                    // 直接使用原始 proj，SetViewProjectionMatrices 内部会调用
                    // GL.GetGPUProjectionMatrix 做平台转换（Y 翻转、Z 范围调整等）
                    cmd.SetViewProjectionMatrices(view, proj);

                    // 设置 shadow bias
                    // 注意：proj 是原始 OpenGL 风格矩阵，m00 = 2/(right-left)
                    float frustumSize = 2.0f / proj.m00;
                    float texelSize = frustumSize / target.atlasRect.width;
                    float depthBiasValue = -settings.depthBias * texelSize;
                    float normalBiasValue = -settings.normalBias * texelSize;
                    cmd.SetGlobalVector(_ShadowBias, new Vector4(depthBiasValue, normalBiasValue, 0, 0));
                    // URP 约定 _LightDirection 指向光源（-forward），ApplyShadowBias 据此偏移顶点
                    cmd.SetGlobalVector(Shader.PropertyToID("_LightDirection"), -lightDir);

                    // 参照 URP ShadowUtils.RenderShadowSlice，启用硬件深度偏移
                    // 这些值和 HDRP 默认值一致
                    cmd.SetGlobalDepthBias(1.0f, 2.5f);

                    // 启用 scissor rect 防止渲染溢出到相邻区域
                    cmd.EnableScissorRect(new Rect(
                        target.atlasRect.x,
                        target.atlasRect.y,
                        target.atlasRect.width,
                        target.atlasRect.height
                    ));

                    // 注意：不在这里 ExecuteCommandBuffer，将矩阵设置和 DrawRenderer
                    // 放在同一个 CommandBuffer 中提交，确保 DrawRenderer 使用正确的 VP 矩阵

                    // 绘制该角色的 ShadowCaster
                    // 逐 Renderer 提交 DrawCommand，只画该角色自身的 Renderer
                    var renderers = target.GetCachedRenderers();
                    if (renderers != null)
                    {
                        for (int r = 0; r < renderers.Length; r++)
                        {
                            var renderer = renderers[r];
                            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                                continue;

                            // 对 SkinnedMeshRenderer 和 MeshRenderer 分别处理
                            if (renderer is SkinnedMeshRenderer smr)
                            {
                                var mesh = smr.sharedMesh;
                                if (mesh == null) continue;
                                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                                {
                                    // 使用 ShadowCaster pass 的材质
                                    var mat = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                                    if (mat == null) continue;
                                    int passIdx = mat.FindPass("ShadowCaster");
                                    if (passIdx < 0) continue;
                                    cmd.DrawRenderer(renderer, mat, sub, passIdx);
                                }
                            }
                            else if (renderer is MeshRenderer mr)
                            {
                                var mf = mr.GetComponent<MeshFilter>();
                                if (mf == null || mf.sharedMesh == null) continue;
                                var mesh = mf.sharedMesh;
                                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                                {
                                    var mat = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                                    if (mat == null) continue;
                                    int passIdx = mat.FindPass("ShadowCaster");
                                    if (passIdx < 0) continue;
                                    cmd.DrawRenderer(renderer, mat, sub, passIdx);
                                }
                            }
                        }
                    }

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                // 恢复状态
                cmd.SetGlobalDepthBias(0.0f, 0.0f); // 恢复硬件深度偏移
                cmd.DisableScissorRect();

                // 恢复摄像机矩阵
                cmd.SetViewProjectionMatrices(
                    renderingData.cameraData.GetViewMatrix(),
                    renderingData.cameraData.GetProjectionMatrix()
                );

                // 设置全局 shader 参数
                cmd.SetGlobalTexture(_CharShadowAtlas, shadowAtlas);
                cmd.SetGlobalVector(_CharShadowAtlasParams, new Vector4(
                    1f / atlasSize, 1f / atlasSize, atlasSize, atlasSize
                ));
                cmd.SetGlobalInt(_CharShadowCount, allocatedCount);
                cmd.SetGlobalMatrixArray(_CharShadowVPArray, vpMatrixArray);
                cmd.SetGlobalVectorArray(_CharShadowAtlasRectArray, atlasRectArray);
                // 启用全局 keyword，让 shader 走 Atlas 采样路径
                cmd.EnableShaderKeyword("_CHAR_SHADOW_ATLAS_ON");
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // 释放 NativeArray
            boundsCenter.Dispose();
            boundsExtents.Dispose();
            cullResults.Dispose();
            viewMatrices.Dispose();
            projMatrices.Dispose();
        }

        private void SetEmptyGlobals(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.SetGlobalTexture(_CharShadowAtlas, Texture2D.whiteTexture);
            cmd.SetGlobalVector(_CharShadowAtlasParams, Vector4.zero);
            cmd.SetGlobalInt(_CharShadowCount, 0);
            cmd.DisableShaderKeyword("_CHAR_SHADOW_ATLAS_ON");
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            Shader.DisableKeyword("_CHAR_SHADOW_ATLAS_ON");
            if (shadowAtlas != null)
            {
                shadowAtlas.Release();
                UnityEngine.Object.DestroyImmediate(shadowAtlas);
                shadowAtlas = null;
            }
        }
    }
}
