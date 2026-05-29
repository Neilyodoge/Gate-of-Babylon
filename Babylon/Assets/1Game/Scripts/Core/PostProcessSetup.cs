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

    }
}
