using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    public class NeilyodogBloomSettings
    {
        [Header("Bloom设置")]
        [Range(0.0f, 5.0f)]
        public float threshold = 1.0f;
        
        [Range(0.0f, 1.0f)]
        public float thresholdKnee = 0.5f;
        
        [Range(0.0f, 10.0f)]
        public float intensity = 1.0f;
        
        [Range(0.0f, 1.0f)]
        public float scatter = 0.7f;
        
        [Range(1, 16)]
        public int maxIterations = 6;
        
        [Range(1, 4)]
        public int downscaleLimit = 2;
        
        public bool highQualityFiltering = true;
        
        [Header("高级设置")]
        public bool killFireflies = true;
        
        [Range(0.0f, 10.0f)]
        public float clamp = 10.0f;
    }

    [Serializable]
    public class NeilyodogBloomRenderFeature : ScriptableRendererFeature
    {
        [SerializeField]
        public NeilyodogBloomSettings settings = new NeilyodogBloomSettings();
        
        private NeilyodogBloomPass bloomPass;
        
        public override void Create()
        {
            bloomPass = new NeilyodogBloomPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.intensity <= 0.0f)
                return;
            
            // 仅对Game和SceneView摄像机生效，避免Preview等摄像机触发null target
            if (renderingData.cameraData.cameraType != CameraType.Game && 
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;
                
            renderer.EnqueuePass(bloomPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game || 
                renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                // 请求CopyColor，确保可以正确读取camera color
                bloomPass.ConfigureInput(ScriptableRenderPassInput.Color);
                bloomPass.SetTarget(renderer.cameraColorTargetHandle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            bloomPass?.Dispose();
        }
    }

    public class NeilyodogBloomPass : ScriptableRenderPass
    {
        private NeilyodogBloomSettings settings;
        private Material bloomMaterial;
        
        // 使用RTHandle替代已废弃的RenderTextureHandle
        private RTHandle[] downSampleRT;
        private RTHandle[] upSampleRT;
        private RTHandle tempColorRT; // 用于最终合成的临时RT（避免同源同目标）
        private RTHandle cameraColorTarget;
        private int pyramidSize;

        // Shader属性ID缓存
        private static readonly int _Threshold = Shader.PropertyToID("_Threshold");
        private static readonly int _ThresholdKnee = Shader.PropertyToID("_ThresholdKnee");
        private static readonly int _Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int _Scatter = Shader.PropertyToID("_Scatter");
        private static readonly int _Clamp = Shader.PropertyToID("_Clamp");
        private static readonly int _KillFireflies = Shader.PropertyToID("_KillFireflies");
        private static readonly int _BloomTex = Shader.PropertyToID("_BloomTex");
        
        public NeilyodogBloomPass(NeilyodogBloomSettings settings)
        {
            this.settings = settings;
            this.profilingSampler = new ProfilingSampler("NeilyodogBloom");
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
            
            // 初始化Bloom材质
            if (bloomMaterial == null)
            {
                Shader bloomShader = Shader.Find("Hidden/NeilyodogBloom");
                if (bloomShader != null)
                {
                    bloomMaterial = new Material(bloomShader);
                }
            }
            
            if (bloomMaterial == null)
                return;
                
            // 计算金字塔层数
            int width = desc.width;
            int height = desc.height;
            pyramidSize = Mathf.Min(settings.maxIterations, (int)Mathf.Log(Mathf.Max(width, height), 2) - settings.downscaleLimit);
            pyramidSize = Mathf.Max(1, pyramidSize);
            
            // 分配临时颜色RT（全分辨率，用于最终合成中转）
            RenderingUtils.ReAllocateIfNeeded(ref tempColorRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_NeilyodogBloomTempColor");
            
            // 分配RTHandle（如果数量变化了需要重新分配）
            if (downSampleRT == null || downSampleRT.Length != pyramidSize)
            {
                ReleaseDownUpSamples();
                downSampleRT = new RTHandle[pyramidSize];
                upSampleRT = new RTHandle[pyramidSize];
            }
            
            for (int i = 0; i < pyramidSize; i++)
            {
                int div = 1 << (i + 1);
                var rtDesc = desc;
                rtDesc.width = Mathf.Max(1, width / div);
                rtDesc.height = Mathf.Max(1, height / div);
                
                RenderingUtils.ReAllocateIfNeeded(ref downSampleRT[i], rtDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_NeilyodogBloomDown{i}");
                RenderingUtils.ReAllocateIfNeeded(ref upSampleRT[i], rtDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_NeilyodogBloomUp{i}");
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (bloomMaterial == null || downSampleRT == null || downSampleRT.Length == 0)
                return;
            
            // 防止cameraColorTarget为null或其底层RT无效
            if (cameraColorTarget == null || cameraColorTarget.rt == null)
                return;
                
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 设置Bloom参数
                bloomMaterial.SetFloat(_Threshold, settings.threshold);
                bloomMaterial.SetFloat(_ThresholdKnee, settings.thresholdKnee);
                bloomMaterial.SetFloat(_Intensity, settings.intensity);
                bloomMaterial.SetFloat(_Scatter, settings.scatter);
                bloomMaterial.SetFloat(_Clamp, settings.clamp);
                bloomMaterial.SetInteger(_KillFireflies, settings.killFireflies ? 1 : 0);
                
                // Pass 0: 预过滤 - 从camera color提取高亮区域到downSampleRT[0]
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, downSampleRT[0], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 0);
                
                // Pass 1: 下采样金字塔
                for (int i = 1; i < downSampleRT.Length; i++)
                {
                    Blitter.BlitCameraTexture(cmd, downSampleRT[i - 1], downSampleRT[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 1);
                }
                
                // Pass 2: 上采样并合并金字塔
                // 从最低mip开始逐步往上合并（仅在bloom层内部合并，不涉及原始画面）
                RTHandle lastBloom = downSampleRT[downSampleRT.Length - 1];
                
                for (int i = downSampleRT.Length - 2; i >= 0; i--)
                {
                    // _BloomTex = 低层bloom（更低分辨率，将被上采样）
                    cmd.SetGlobalTexture(_BloomTex, lastBloom);
                    // _BlitTexture = downSampleRT[i]（当前层的高分辨率bloom数据，作为baseColor）
                    // 上采样pass: baseColor(_BlitTexture) + UpsampleKawase(_BloomTex)
                    Blitter.BlitCameraTexture(cmd, downSampleRT[i], upSampleRT[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 2);
                    lastBloom = upSampleRT[i];
                }
                
                // Pass 3: 最终合成 - 将bloom叠加回原始画面
                // _BlitTexture = cameraColor（原始画面）
                // _BloomTex = lastBloom（最终的bloom结果）
                // 合成: 原始画面 + bloom * intensity
                cmd.SetGlobalTexture(_BloomTex, lastBloom);
                Blitter.BlitCameraTexture(cmd, cameraColorTarget, tempColorRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 3);
                
                // 将合成结果copy回camera color（不带material的重载不支持loadAction/storeAction）
                Blitter.BlitCameraTexture(cmd, tempColorRT, cameraColorTarget);
                
                // 恢复camera color为当前render target，防止影响后续pass
                CoreUtils.SetRenderTarget(cmd, cameraColorTarget);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void ReleaseDownUpSamples()
        {
            if (downSampleRT != null)
            {
                for (int i = 0; i < downSampleRT.Length; i++)
                {
                    downSampleRT[i]?.Release();
                }
                downSampleRT = null;
            }
            
            if (upSampleRT != null)
            {
                for (int i = 0; i < upSampleRT.Length; i++)
                {
                    upSampleRT[i]?.Release();
                }
                upSampleRT = null;
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle由ReAllocateIfNeeded管理，不需要每帧清理
        }

        public void Dispose()
        {
            ReleaseDownUpSamples();
            tempColorRT?.Release();
            tempColorRT = null;
            
            if (bloomMaterial != null)
            {
                CoreUtils.Destroy(bloomMaterial);
                bloomMaterial = null;
            }
        }
    }
}