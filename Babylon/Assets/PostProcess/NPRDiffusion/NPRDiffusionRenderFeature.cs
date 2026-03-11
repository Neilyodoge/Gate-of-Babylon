using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class NPRDiffusionSettings
    {
        [Header("Diffusion 增强")]
        [Tooltip("扩散强度，为0时效果关闭")]
        [Range(0.0f, 2.0f)]
        public float intensity = 1.0f;

        [Tooltip("亮度阈值，仅亮度超过该值的像素参与扩散")]
        [Range(0.0f, 5.0f)]
        public float threshold = 1.0f;

        [Tooltip("阈值软过渡，值越大过渡越柔和")]
        [Range(0.0f, 1.0f)]
        public float thresholdKnee = 0.5f;

        [Tooltip("模糊迭代次数，值越大扩散范围越广")]
        [Range(2, 8)]
        public int blurIterations = 4;
    }

    [Serializable]
    public class NPRDiffusionRenderFeature : ScriptableRendererFeature
    {
        [SerializeField]
        public NPRDiffusionSettings settings = new NPRDiffusionSettings();

        private NPRDiffusionPass diffusionPass;

        public override void Create()
        {
            diffusionPass = new NPRDiffusionPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.intensity <= 0.0f)
                return;

            // 仅对Game和SceneView摄像机生效
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            renderer.EnqueuePass(diffusionPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game ||
                renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                diffusionPass.ConfigureInput(ScriptableRenderPassInput.Color);
                diffusionPass.SetTarget(renderer.cameraColorTargetHandle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            diffusionPass?.Dispose();
        }
    }

    public class NPRDiffusionPass : ScriptableRenderPass
    {
        // 最大支持的降采样层级数
        private const int MAX_PYRAMID_LEVELS = 8;

        private NPRDiffusionSettings settings;
        private Material diffusionMaterial;

        private RTHandle cameraColorTarget;
        private RTHandle tempColorRT;    // 用于最终合成的临时RT

        // Dual Kawase 金字塔RT
        private RTHandle[] downSampleRTs = new RTHandle[MAX_PYRAMID_LEVELS];
        private RTHandle[] upSampleRTs = new RTHandle[MAX_PYRAMID_LEVELS];

        // Shader属性ID缓存
        private static readonly int _Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int _Threshold = Shader.PropertyToID("_Threshold");
        private static readonly int _ThresholdKnee = Shader.PropertyToID("_ThresholdKnee");
        private static readonly int _DiffusionTex = Shader.PropertyToID("_DiffusionTex");

        public NPRDiffusionPass(NPRDiffusionSettings settings)
        {
            this.settings = settings;
            this.profilingSampler = new ProfilingSampler("NPRDiffusion");
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void SetTarget(RTHandle colorHandle)
        {
            cameraColorTarget = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            // 初始化材质
            if (diffusionMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/NPRDiffusion");
                if (shader != null)
                {
                    diffusionMaterial = new Material(shader);
                }
            }

            if (diffusionMaterial == null)
                return;

            int iterations = Mathf.Clamp(settings.blurIterations, 2, MAX_PYRAMID_LEVELS);

            // 分配金字塔RT - 每级分辨率减半
            int width = Mathf.Max(1, desc.width / 2);
            int height = Mathf.Max(1, desc.height / 2);

            for (int i = 0; i < iterations; i++)
            {
                var pyramidDesc = desc;
                pyramidDesc.width = Mathf.Max(1, width);
                pyramidDesc.height = Mathf.Max(1, height);

                RenderingUtils.ReAllocateIfNeeded(ref downSampleRTs[i], pyramidDesc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    name: $"_NPRDiffusionDown{i}");
                RenderingUtils.ReAllocateIfNeeded(ref upSampleRTs[i], pyramidDesc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    name: $"_NPRDiffusionUp{i}");

                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
            }

            // 全分辨率临时RT用于最终合成
            RenderingUtils.ReAllocateIfNeeded(ref tempColorRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_NPRDiffusionTempColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (diffusionMaterial == null)
                return;

            if (cameraColorTarget == null || cameraColorTarget.rt == null)
                return;

            int iterations = Mathf.Clamp(settings.blurIterations, 2, MAX_PYRAMID_LEVELS);

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 设置参数
                diffusionMaterial.SetFloat(_Intensity, settings.intensity);
                diffusionMaterial.SetFloat(_Threshold, settings.threshold);
                diffusionMaterial.SetFloat(_ThresholdKnee, settings.threshold * settings.thresholdKnee);

                // ===== Step 1: 预过滤（阈值提取 + 首次降采样）=====
                // SceneColor -> downSampleRTs[0] (1/2 分辨率, 带阈值过滤)
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, downSampleRTs[0],
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    diffusionMaterial, 0); // Pass 0: Prefilter

                // ===== Step 2: Dual Kawase 逐级降采样 =====
                for (int i = 1; i < iterations; i++)
                {
                    Blitter.BlitCameraTexture(cmd, downSampleRTs[i - 1], downSampleRTs[i],
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                        diffusionMaterial, 1); // Pass 1: DualKawaseDown
                }

                // ===== Step 3: Dual Kawase 逐级上采样 =====
                // 从最底层开始上采样
                // 最底层直接上采样到倒数第二层的upSampleRT
                Blitter.BlitCameraTexture(cmd, downSampleRTs[iterations - 1], upSampleRTs[iterations - 2],
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    diffusionMaterial, 2); // Pass 2: DualKawaseUp

                // 逐级上采样
                for (int i = iterations - 3; i >= 0; i--)
                {
                    Blitter.BlitCameraTexture(cmd, upSampleRTs[i + 1], upSampleRTs[i],
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                        diffusionMaterial, 2); // Pass 2: DualKawaseUp
                }

                // ===== Step 4: 最终合成 - 变亮(Lighten)方式叠加 =====
                // upSampleRTs[0] 是最终的模糊结果（1/2分辨率，会被自动双线性上采样）
                cmd.SetGlobalTexture(_DiffusionTex, upSampleRTs[0]);
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, tempColorRT,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    diffusionMaterial, 3); // Pass 3: Combine

                // 将合成结果拷贝回camera color
                Blitter.BlitCameraTexture(cmd, tempColorRT, cameraColorTarget);

                // 恢复camera color为当前render target
                CoreUtils.SetRenderTarget(cmd, cameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle由ReAllocateIfNeeded管理，不需要每帧清理
        }

        public void Dispose()
        {
            for (int i = 0; i < MAX_PYRAMID_LEVELS; i++)
            {
                downSampleRTs[i]?.Release();
                downSampleRTs[i] = null;
                upSampleRTs[i]?.Release();
                upSampleRTs[i] = null;
            }

            tempColorRT?.Release();
            tempColorRT = null;

            if (diffusionMaterial != null)
            {
                CoreUtils.Destroy(diffusionMaterial);
                diffusionMaterial = null;
            }
        }
    }
}
