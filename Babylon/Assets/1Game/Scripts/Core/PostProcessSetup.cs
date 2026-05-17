using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace XianTu
{
    /// <summary>
    /// 后处理效果设置
    /// 自动添加 URP 后处理 Volume（Bloom、Vignette、Color Grading）
    /// 环境氛围随层数变化
    /// </summary>
    public class PostProcessSetup : MonoBehaviour
    {
        public static PostProcessSetup Instance { get; private set; }

        private UnityEngine.Rendering.Volume _volume;
        private Bloom _bloom;
        private Vignette _vignette;
        private ColorAdjustments _colorAdjustments;

        private void Awake()
        {
            Instance = this;
            SetupPostProcess();
        }

        private void SetupPostProcess()
        {
            // 确保相机启用后处理
            var cam = Camera.main;
            if (cam != null)
            {
                var urpCamData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (urpCamData == null)
                    urpCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                urpCamData.renderPostProcessing = true;
            }

            // 创建 Global Volume
            var volumeGo = new GameObject("PostProcessVolume");
            volumeGo.transform.SetParent(transform);
            volumeGo.layer = 0;

            _volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1;

            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            _volume.profile = profile;

            // Bloom
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.9f);
            _bloom.intensity.Override(1.5f);
            _bloom.scatter.Override(0.7f);
            _bloom.tint.Override(new Color(1f, 0.95f, 0.9f));

            // Vignette
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.3f);
            _vignette.smoothness.Override(0.5f);
            _vignette.color.Override(new Color(0.1f, 0.05f, 0.15f));

            // Color Adjustments
            _colorAdjustments = profile.Add<ColorAdjustments>(true);
            _colorAdjustments.postExposure.Override(0.2f);
            _colorAdjustments.contrast.Override(10f);
            _colorAdjustments.saturation.Override(10f);
        }

        /// <summary>
        /// 根据层数更新环境氛围
        /// 层数越高，画面越暗越红
        /// </summary>
        public void UpdateAtmosphere(int level, int maxLevel)
        {
            float t = (float)level / Mathf.Max(1, maxLevel - 1);

            // Vignette 随层数加深
            if (_vignette != null)
            {
                _vignette.intensity.Override(Mathf.Lerp(0.25f, 0.5f, t));
                _vignette.color.Override(Color.Lerp(
                    new Color(0.1f, 0.05f, 0.15f),
                    new Color(0.3f, 0.05f, 0.05f), t));
            }

            // Bloom 随层数变化
            if (_bloom != null)
            {
                _bloom.intensity.Override(Mathf.Lerp(1.2f, 2.5f, t));
                _bloom.tint.Override(Color.Lerp(
                    new Color(1f, 0.95f, 0.9f),
                    new Color(1f, 0.7f, 0.6f), t));
            }

            // 色调随层数变暗变红
            if (_colorAdjustments != null)
            {
                _colorAdjustments.postExposure.Override(Mathf.Lerp(0.2f, -0.1f, t));
                _colorAdjustments.saturation.Override(Mathf.Lerp(10f, -5f, t));
            }

            // 更新环境光
            UpdateLighting(t);

            // 更新雾效
            UpdateFog(level, t);
        }

        private void UpdateLighting(float t)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    light.color = Color.Lerp(
                        new Color(1f, 0.95f, 0.85f),
                        new Color(0.8f, 0.6f, 0.5f), t);
                    light.intensity = Mathf.Lerp(1.2f, 0.8f, t);
                }
            }
        }

        private void UpdateFog(int level, float t)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Mathf.Lerp(0.005f, 0.02f, t);
            RenderSettings.fogColor = Color.Lerp(
                new Color(0.08f, 0.08f, 0.12f),
                new Color(0.12f, 0.05f, 0.05f), t);

            // 环境光也随之变化
            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.15f, 0.15f, 0.2f),
                new Color(0.12f, 0.08f, 0.08f), t);
        }

        /// <summary>受击时短暂加深 Vignette（视觉反馈）</summary>
        public void PulseVignette()
        {
            StartCoroutine(VignettePulse());
        }

        private System.Collections.IEnumerator VignettePulse()
        {
            if (_vignette == null) yield break;

            float originalIntensity = _vignette.intensity.value;
            _vignette.intensity.Override(0.6f);
            _vignette.color.Override(new Color(0.5f, 0.05f, 0.05f));

            yield return new WaitForSeconds(0.15f);

            float timer = 0.3f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.3f;
                _vignette.intensity.Override(Mathf.Lerp(originalIntensity, 0.6f, t));
                yield return null;
            }
            _vignette.intensity.Override(originalIntensity);
        }

        // ====================================================================
        //  灵气浓度氛围联动（v0.5 Week 5）
        //
        //  Sparse  → 灰冷（vignette 偏蓝灰 / bloom 弱 / 饱和度低）
        //  Normal  → 还原由 UpdateAtmosphere 设定的"层数氛围"
        //  Rich    → 翠绿（vignette 偏绿 / bloom 强）
        //  Vein    → 金光（vignette 偏金 / bloom 极强 / 高饱和）
        //
        //  实现策略：缓存上次 UpdateAtmosphere 的"层数基准值"，
        //  当浓度切换时在基准上叠加 Tint；切回 Normal 时复原到基准。
        // ====================================================================

        private float _basePostExposure = 0.2f;
        private float _baseSaturation = 10f;
        private float _baseBloomIntensity = 1.5f;
        private Color _baseBloomTint = new Color(1f, 0.95f, 0.9f);
        private float _baseVignetteIntensity = 0.3f;
        private Color _baseVignetteColor = new Color(0.1f, 0.05f, 0.15f);
        private Color _baseFogColor = new Color(0.08f, 0.08f, 0.12f);
        private Color _baseAmbient = new Color(0.15f, 0.15f, 0.2f);

        private Coroutine _densityTween;

        /// <summary>
        /// 由 BattleRoom 进入新房间时调用，根据当前 SpiritDensity 在"层数氛围"基础上叠加 tint。
        /// 在 UpdateAtmosphere 之后调用，否则 base 值会丢失。
        /// </summary>
        public void ApplyDensityAura(SpiritDensityLevel level)
        {
            // 第一次调用时缓存当前作为基准
            CacheBaseFromCurrent();

            // 选目标值
            Color vignetteColor;
            float vignetteIntensity;
            float bloomIntensity;
            Color bloomTint;
            float postExposure;
            float saturation;
            Color fogColor;
            Color ambient;

            switch (level)
            {
                case SpiritDensityLevel.Sparse:
                    vignetteColor      = Color.Lerp(_baseVignetteColor, new Color(0.1f, 0.12f, 0.18f), 0.6f);
                    vignetteIntensity  = _baseVignetteIntensity + 0.08f;
                    bloomIntensity     = _baseBloomIntensity * 0.7f;
                    bloomTint          = Color.Lerp(_baseBloomTint, new Color(0.8f, 0.85f, 0.95f), 0.5f);
                    postExposure       = _basePostExposure - 0.10f;
                    saturation         = _baseSaturation - 12f;
                    fogColor           = Color.Lerp(_baseFogColor, new Color(0.10f, 0.12f, 0.16f), 0.6f);
                    ambient            = Color.Lerp(_baseAmbient, new Color(0.13f, 0.14f, 0.18f), 0.6f);
                    break;

                case SpiritDensityLevel.Rich:
                    vignetteColor      = Color.Lerp(_baseVignetteColor, new Color(0.06f, 0.16f, 0.10f), 0.55f);
                    vignetteIntensity  = _baseVignetteIntensity * 0.9f;
                    bloomIntensity     = _baseBloomIntensity * 1.4f;
                    bloomTint          = Color.Lerp(_baseBloomTint, new Color(0.85f, 1f, 0.85f), 0.5f);
                    postExposure       = _basePostExposure + 0.08f;
                    saturation         = _baseSaturation + 10f;
                    fogColor           = Color.Lerp(_baseFogColor, new Color(0.06f, 0.14f, 0.08f), 0.45f);
                    ambient            = Color.Lerp(_baseAmbient, new Color(0.13f, 0.20f, 0.15f), 0.55f);
                    break;

                case SpiritDensityLevel.Vein:
                    vignetteColor      = Color.Lerp(_baseVignetteColor, new Color(0.18f, 0.12f, 0.04f), 0.5f);
                    vignetteIntensity  = _baseVignetteIntensity * 0.7f;     // 灵脉房整体偏亮，vignette 弱
                    bloomIntensity     = _baseBloomIntensity * 2.2f;
                    bloomTint          = Color.Lerp(_baseBloomTint, new Color(1f, 0.85f, 0.4f), 0.75f);
                    postExposure       = _basePostExposure + 0.18f;
                    saturation         = _baseSaturation + 20f;
                    fogColor           = Color.Lerp(_baseFogColor, new Color(0.18f, 0.14f, 0.06f), 0.6f);
                    ambient            = Color.Lerp(_baseAmbient, new Color(0.28f, 0.22f, 0.12f), 0.7f);
                    break;

                default:    // Normal：复原到 base
                    vignetteColor      = _baseVignetteColor;
                    vignetteIntensity  = _baseVignetteIntensity;
                    bloomIntensity     = _baseBloomIntensity;
                    bloomTint          = _baseBloomTint;
                    postExposure       = _basePostExposure;
                    saturation         = _baseSaturation;
                    fogColor           = _baseFogColor;
                    ambient            = _baseAmbient;
                    break;
            }

            if (_densityTween != null) StopCoroutine(_densityTween);
            _densityTween = StartCoroutine(TweenAura(
                vignetteColor, vignetteIntensity,
                bloomIntensity, bloomTint,
                postExposure, saturation,
                fogColor, ambient,
                duration: 0.8f));
        }

        /// <summary>第一次进入新场景时把"当前由 UpdateAtmosphere 设置好的层数值"作为氛围基准缓存。</summary>
        private void CacheBaseFromCurrent()
        {
            if (_vignette != null)
            {
                _baseVignetteColor = _vignette.color.value;
                _baseVignetteIntensity = _vignette.intensity.value;
            }
            if (_bloom != null)
            {
                _baseBloomIntensity = _bloom.intensity.value;
                _baseBloomTint = _bloom.tint.value;
            }
            if (_colorAdjustments != null)
            {
                _basePostExposure = _colorAdjustments.postExposure.value;
                _baseSaturation = _colorAdjustments.saturation.value;
            }
            _baseFogColor = RenderSettings.fogColor;
            _baseAmbient = RenderSettings.ambientLight;
        }

        private System.Collections.IEnumerator TweenAura(
            Color targetVignetteColor, float targetVignetteIntensity,
            float targetBloomIntensity, Color targetBloomTint,
            float targetPostExposure, float targetSaturation,
            Color targetFogColor, Color targetAmbient,
            float duration)
        {
            Color fromVignetteColor = _vignette != null ? _vignette.color.value : targetVignetteColor;
            float fromVignetteIntensity = _vignette != null ? _vignette.intensity.value : targetVignetteIntensity;
            float fromBloomIntensity = _bloom != null ? _bloom.intensity.value : targetBloomIntensity;
            Color fromBloomTint = _bloom != null ? _bloom.tint.value : targetBloomTint;
            float fromPostExposure = _colorAdjustments != null ? _colorAdjustments.postExposure.value : targetPostExposure;
            float fromSaturation = _colorAdjustments != null ? _colorAdjustments.saturation.value : targetSaturation;
            Color fromFog = RenderSettings.fogColor;
            Color fromAmbient = RenderSettings.ambientLight;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

                if (_vignette != null)
                {
                    _vignette.color.Override(Color.Lerp(fromVignetteColor, targetVignetteColor, k));
                    _vignette.intensity.Override(Mathf.Lerp(fromVignetteIntensity, targetVignetteIntensity, k));
                }
                if (_bloom != null)
                {
                    _bloom.intensity.Override(Mathf.Lerp(fromBloomIntensity, targetBloomIntensity, k));
                    _bloom.tint.Override(Color.Lerp(fromBloomTint, targetBloomTint, k));
                }
                if (_colorAdjustments != null)
                {
                    _colorAdjustments.postExposure.Override(Mathf.Lerp(fromPostExposure, targetPostExposure, k));
                    _colorAdjustments.saturation.Override(Mathf.Lerp(fromSaturation, targetSaturation, k));
                }
                RenderSettings.fogColor = Color.Lerp(fromFog, targetFogColor, k);
                RenderSettings.ambientLight = Color.Lerp(fromAmbient, targetAmbient, k);
                yield return null;
            }
            _densityTween = null;
        }
    }
}
