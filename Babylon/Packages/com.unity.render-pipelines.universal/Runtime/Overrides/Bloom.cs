using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Bloom模式枚举
    /// </summary>
    public enum BloomMode
    {
        /// <summary>
        /// URP内置Bloom算法
        /// </summary>
        Default = 0,

        /// <summary>
        /// 自定义nBloom算法（Kawase模糊 + Kill Fireflies）
        /// </summary>
        n = 1,

        /// <summary>
        /// PC高品质Bloom（CasualBloom二维高斯金字塔）
        /// </summary>
        PC = 2,
    }

    /// <summary>
    /// BloomMode 的 VolumeParameter 封装
    /// </summary>
    [Serializable]
    public sealed class BloomModeParameter : VolumeParameter<BloomMode>
    {
        /// <summary>
        /// Creates a new <see cref="BloomModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public BloomModeParameter(BloomMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// This controls the size of the bloom texture.
    /// </summary>
    public enum BloomDownscaleMode
    {
        /// <summary>
        /// Use this to select half size as the starting resolution.
        /// </summary>
        Half,

        /// <summary>
        /// Use this to select quarter size as the starting resolution.
        /// </summary>
        Quarter,
    }

    /// <summary>
    /// A volume component that holds settings for the Bloom effect.
    /// </summary>
    [Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/Bloom", typeof(UniversalRenderPipeline))]
    [URPHelpURL("post-processing-bloom")]
    public sealed partial class Bloom : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// Bloom模式选择
        /// </summary>
        [Header("Bloom Mode")]
        [Tooltip("选择Bloom模式：Default为URP内置；n为轻量Kawase；PC为CasualBloom二维高斯金字塔")]
        public BloomModeParameter bloomMode = new BloomModeParameter(BloomMode.Default);

        /// <summary>
        /// Set the level of brightness to filter out pixels under this level.
        /// This value is expressed in gamma-space.
        /// A value above 0 will disregard energy conservation rules.
        /// </summary>
        [Header("Bloom")]
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);

        /// <summary>
        /// Controls the strength of the bloom filter.
        /// </summary>
        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

        /// <summary>
        /// Controls the extent of the veiling effect.
        /// </summary>
        [Tooltip("Set the radius of the bloom effect.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        /// <summary>
        /// Set the maximum intensity that Unity uses to calculate Bloom.
        /// If pixels in your Scene are more intense than this, URP renders them at their current intensity, but uses this intensity value for the purposes of Bloom calculations.
        /// </summary>
        [Tooltip("Set the maximum intensity that Unity uses to calculate Bloom. If pixels in your Scene are more intense than this, URP renders them at their current intensity, but uses this intensity value for the purposes of Bloom calculations.")]
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);

        /// <summary>
        /// Specifies the tint of the bloom filter.
        /// </summary>
        [Tooltip("Use the color picker to select a color for the Bloom effect to tint to.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        /// <summary>
        /// Controls whether to use bicubic sampling instead of bilinear sampling for the upsampling passes.
        /// This is slightly more expensive but helps getting smoother visuals.
        /// </summary>
        [Tooltip("Use bicubic sampling instead of bilinear sampling for the upsampling passes. This is slightly more expensive but helps getting smoother visuals.")]
        public BoolParameter highQualityFiltering = new BoolParameter(false);

        /// <summary>
        /// [nBloom模式] 阈值过渡的柔和度，控制Bloom边缘的软硬程度
        /// </summary>
        [Header("nBloom Mode Settings")]
        [Tooltip("[nBloom模式] 阈值过渡的柔和度")]
        public ClampedFloatParameter thresholdKnee = new ClampedFloatParameter(0.5f, 0f, 1f);

        /// <summary>
        /// [nBloom模式] 抑制萤火虫高亮像素，防止极亮像素造成闪烁
        /// </summary>
        [Tooltip("[nBloom模式] 抑制萤火虫高亮像素")]
        public BoolParameter killFireflies = new BoolParameter(true);

        /// <summary>
        /// [PC模式] 下采样二维高斯卷积核尺寸。
        /// </summary>
        [Header("PC Mode Settings")]
        [Tooltip("[PC模式] 下采样二维高斯核尺寸；运行时会修正为3~15之间的奇数")]
        public ClampedIntParameter pcDownsampleKernelSize = new ClampedIntParameter(5, 3, 15);

        /// <summary>
        /// [PC模式] 下采样二维高斯分布标准差。
        /// </summary>
        [Tooltip("[PC模式] 下采样二维高斯分布标准差")]
        public ClampedFloatParameter pcDownsampleSigma = new ClampedFloatParameter(1f, 0.01f, 10f);

        /// <summary>
        /// [PC模式] 上采样二维高斯卷积核尺寸。
        /// </summary>
        [Tooltip("[PC模式] 上采样二维高斯核尺寸；运行时会修正为3~15之间的奇数")]
        public ClampedIntParameter pcUpsampleKernelSize = new ClampedIntParameter(5, 3, 15);

        /// <summary>
        /// [PC模式] 上采样二维高斯分布标准差。
        /// </summary>
        [Tooltip("[PC模式] 上采样二维高斯分布标准差")]
        public ClampedFloatParameter pcUpsampleSigma = new ClampedFloatParameter(1f, 0.01f, 10f);

        /// <summary>
        /// [PC模式] 在阈值前压缩极亮像素，降低闪烁与局部过曝。
        /// </summary>
        [Tooltip("[PC模式] Danbaidong式亮度压缩强度；0表示关闭")]
        public ClampedFloatParameter pcLuminanceCompression = new ClampedFloatParameter(0.2f, 0f, 1f);

        /// <summary>
        /// [PC模式] 阈值过滤后的亮度增益。
        /// </summary>
        [Tooltip("[PC模式] 阈值过滤后的Bloom亮度增益")]
        public ClampedFloatParameter pcPrefilterScale = new ClampedFloatParameter(1f, 0f, 5f);

        /// <summary>
        /// [PC模式] 从高分辨率到低分辨率四层Bloom的独立合成权重。
        /// </summary>
        [Tooltip("[PC模式] X/Y/Z/W依次控制近、中、远、超远四层光晕")]
        public Vector4Parameter pcLayerWeights = new Vector4Parameter(Vector4.one);

        /// <summary>
        /// Controls the starting resolution that this effect begins processing.
        /// </summary>
        [Tooltip("The starting resolution that this effect begins processing."), AdditionalProperty]
        public DownscaleParameter downscale = new DownscaleParameter(BloomDownscaleMode.Half);

        /// <summary>
        /// Controls the maximum number of iterations in the effect processing sequence.
        /// </summary>
        [Tooltip("The maximum number of iterations in the effect processing sequence."), AdditionalProperty]
        public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 2, 8);

        /// <summary>
        /// Specifies a Texture to add smudges or dust to the bloom effect.
        /// </summary>
        [Header("Lens Dirt")]
        [Tooltip("Dirtiness texture to add smudges or dust to the bloom effect.")]
        public TextureParameter dirtTexture = new TextureParameter(null);

        /// <summary>
        /// Controls the strength of the lens dirt.
        /// </summary>
        [Tooltip("Amount of dirtiness.")]
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);

        /// <inheritdoc/>
        public bool IsActive() => intensity.value > 0f;

        /// <inheritdoc/>
        public bool IsTileCompatible() => false;
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="BloomDownscaleMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class DownscaleParameter : VolumeParameter<BloomDownscaleMode>
    {
        /// <summary>
        /// Creates a new <see cref="DownscaleParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public DownscaleParameter(BloomDownscaleMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}
