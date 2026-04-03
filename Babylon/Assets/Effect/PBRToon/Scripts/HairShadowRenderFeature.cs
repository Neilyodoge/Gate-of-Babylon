using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// 前发投影 RenderFeature
    /// 通过额外的 RT (_HairShadowMask) 标记前发区域，供 Face shader 采样
    /// 
    /// 渲染流程:
    /// 1. HairShadowMask Pass (BeforeRenderingOpaques): 
    ///    创建 _HairShadowMask RT, 用 Hair mesh 渲染前发区域 (白色=前发, 黑色=无)
    /// 2. Face Forward Pass (Geometry-10): 
    ///    采样 _HairShadowMask, 前发区域的脸部像素进入阴影
    /// 3. Hair Forward Pass (Geometry+10): 正常渲染头发
    /// </summary>
    [Serializable]
    public class HairShadowSettings
    {
        [Header("前发投影设置")]
        [Tooltip("前发投影全局开关")]
        public bool enabled = true;

        [Tooltip("头发层级 (用于 HairShadowMask Pass)")]
        public LayerMask hairLayerMask = -1;
    }

    [Serializable]
    public class HairShadowRenderFeature : ScriptableRendererFeature
    {
        [SerializeField]
        public HairShadowSettings settings = new HairShadowSettings();

        private HairShadowMaskPass hairShadowMaskPass;

        public override void Create()
        {
            hairShadowMaskPass = new HairShadowMaskPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enabled)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            renderer.EnqueuePass(hairShadowMaskPass);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    /// HairShadowMask Pass:
    /// - 在不透明渲染之前执行
    /// - 创建 _HairShadowMask RT (R8), Clear 为黑色 (0)
    /// - 用 Hair mesh 渲染, 输出白色 (1) 标记前发区域
    /// - 渲染完成后恢复原来的 RenderTarget, 将 _HairShadowMask 设为全局纹理
    /// - Face shader 中采样此纹理判断前发遮挡
    /// </summary>
    public class HairShadowMaskPass : ScriptableRenderPass
    {
        private HairShadowSettings settings;
        private FilteringSettings filteringSettings;
        private static readonly ShaderTagId hairShadowMaskTagId = new ShaderTagId("HairShadowMask");
        private static readonly int s_HairShadowMaskId = Shader.PropertyToID("_HairShadowMask");

        private RTHandle hairShadowMaskRT;

        public HairShadowMaskPass(HairShadowSettings settings)
        {
            this.settings = settings;
            this.profilingSampler = new ProfilingSampler("HairShadowMask");
            // 在不透明渲染之前执行, 确保 Face Forward Pass 能采样到 mask
            this.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques - 1;

            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.hairLayerMask);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.R8;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(ref hairShadowMaskRT, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_HairShadowMask");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 设置 RT 并清除为黑色
                CoreUtils.SetRenderTarget(cmd, hairShadowMaskRT, ClearFlag.Color, Color.black);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 绘制 Hair mesh 的 HairShadowMask Pass
                var sortingCriteria = SortingCriteria.CommonOpaque;
                var drawingSettings = CreateDrawingSettings(hairShadowMaskTagId, ref renderingData, sortingCriteria);
                filteringSettings.layerMask = settings.hairLayerMask;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                // 恢复原来的 RenderTarget
                var renderer = renderingData.cameraData.renderer;
                CoreUtils.SetRenderTarget(cmd,
                    renderer.cameraColorTargetHandle,
                    renderer.cameraDepthTargetHandle);

                // 设置全局纹理, 供 Face shader 采样
                cmd.SetGlobalTexture(s_HairShadowMaskId, hairShadowMaskRT);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RT 由 RTHandle 管理, 不需要每帧释放
            // 但需要清除全局纹理引用
        }

        public void Dispose()
        {
            hairShadowMaskRT?.Release();
            hairShadowMaskRT = null;
        }
    }
}
