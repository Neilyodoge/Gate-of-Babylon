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
    ///
    /// Disable 行为:
    ///   - RenderFeature 顶部的 enabled checkbox（即 ScriptableRendererFeature.SetActive）
    ///     仅改 m_Active 字段，URP 不触发任何回调，必须靠 EditorApplication.update 轮询
    ///   - settings.enabled 内层勾选：靠 OnValidate + AddRenderPasses 状态机检测
    ///   - RenderFeature 删除 / Pipeline 切换 / Domain Reload：靠 Dispose / OnDisable 兜底
    /// 三条路径任意一条触发，都会把 _HairShadowMask 全局贴图重置为黑色，
    /// 使 Face shader 在 _HAIR_SHADOW 仍开启时也不会再采样到旧 RT 残留。
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

        // 与 HairShadowMaskPass 共享的全局贴图 ID
        internal static readonly int s_HairShadowMaskId = Shader.PropertyToID("_HairShadowMask");

        // 上一帧 settings.enabled 是否真入队 Pass，用于检测 settings.enabled 切换瞬间
        private bool _wasActive;

#if UNITY_EDITOR
        // 上一帧 isActive 状态（即 RenderFeature 顶部 checkbox），用于轮询切换
        // 默认初始 true，与 ScriptableRendererFeature.m_Active 默认值保持一致
        private bool _lastIsActiveTracked = true;
#endif

        public override void Create()
        {
            hairShadowMaskPass = new HairShadowMaskPass(settings);

#if UNITY_EDITOR
            // 注册编辑器轮询：取消勾选 RenderFeature 顶部 checkbox 时不触发任何 Unity 回调，
            // 必须用 EditorApplication.update 主动观测 isActive 变化
            UnityEditor.EditorApplication.update -= EditorWatchIsActive;
            UnityEditor.EditorApplication.update += EditorWatchIsActive;
            _lastIsActiveTracked = isActive;
#endif
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.enabled)
            {
                // settings.enabled 切换帧：清理 RT + 重置全局贴图
                if (_wasActive)
                    Cleanup();
                return;
            }

            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            _wasActive = true;
            renderer.EnqueuePass(hairShadowMaskPass);
        }

        protected override void Dispose(bool disposing)
        {
            // RenderFeature 被删除 / Pipeline 切换 / Domain Reload 时由 URP 调用
            Cleanup();
        }

#if UNITY_EDITOR
        // 父类 ScriptableRendererFeature 没有定义 OnDisable，子类可以安全添加。
        // ScriptableObject 的 OnDisable 在 Domain Reload / 资源卸载 / 销毁前触发。
        private void OnDisable()
        {
            UnityEditor.EditorApplication.update -= EditorWatchIsActive;
            Cleanup();
        }

        private void EditorWatchIsActive()
        {
            // 对象已销毁则反注册自身（避免空引用）
            if (this == null)
            {
                UnityEditor.EditorApplication.update -= EditorWatchIsActive;
                return;
            }

            bool now = isActive;
            if (now != _lastIsActiveTracked)
            {
                _lastIsActiveTracked = now;
                // 仅在 active → inactive 的切换瞬间清理；反之 inactive → active 啥都不用做
                if (!now)
                    Cleanup();
            }
        }
#endif

        // 把 _HairShadowMask 设为内置黑色贴图（1×1 RGBA），Face shader 采样后等价于"无前发遮挡"
        // 同时释放 RTHandle 实际显存
        private void Cleanup()
        {
            hairShadowMaskPass?.Dispose();
            Shader.SetGlobalTexture(s_HairShadowMaskId, Texture2D.blackTexture);
            _wasActive = false;
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
                cmd.SetGlobalTexture(HairShadowRenderFeature.s_HairShadowMaskId, hairShadowMaskRT);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RT 由 RTHandle 管理跨帧复用，无需每帧释放；
            // 全局贴图 _HairShadowMask 的清理交给 RenderFeature.Cleanup 统一处理
        }

        public void Dispose()
        {
            hairShadowMaskRT?.Release();
            hairShadowMaskRT = null;
        }
    }
}
