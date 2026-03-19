using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class ToonOutlineSettings
    {
        [Header("描边设置")]
        [Tooltip("描边全局开关")]
        public bool enabled = true;

        [Tooltip("描边渲染排序 - 在不透明物体之后渲染")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("渲染层级过滤")]
        public LayerMask layerMask = -1;
    }

    [Serializable]
    public class ToonOutlineRenderFeature : ScriptableRendererFeature
    {
        [SerializeField]
        public ToonOutlineSettings settings = new ToonOutlineSettings();

        private ToonOutlinePass outlinePass;

        public override void Create()
        {
            outlinePass = new ToonOutlinePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enabled)
                return;

            // 仅对Game和SceneView摄像机生效
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            // ToonOutlinePass 不持有需要释放的资源
        }
    }

    public class ToonOutlinePass : ScriptableRenderPass
    {
        private ToonOutlineSettings settings;
        private FilteringSettings filteringSettings;
        private static readonly ShaderTagId outlineShaderTagId = new ShaderTagId("Outline");

        public ToonOutlinePass(ToonOutlineSettings settings)
        {
            this.settings = settings;
            this.profilingSampler = new ProfilingSampler("ToonOutline");
            this.renderPassEvent = settings.renderPassEvent;

            // 只渲染不透明物体的描边
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.layerMask);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 先执行空的 CommandBuffer 来设置 profiling scope
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 设置绘制参数：使用 "Outline" LightMode Tag 绘制
                var sortingCriteria = SortingCriteria.CommonOpaque;
                var drawingSettings = CreateDrawingSettings(outlineShaderTagId, ref renderingData, sortingCriteria);

                // 更新 layerMask（支持运行时修改）
                filteringSettings.layerMask = settings.layerMask;

                // 绘制所有带 "Outline" Pass 的物体
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
