using UnityEngine;
using UnityEngine.Rendering;

namespace XianTu.LevelDesign
{
    /// <summary>昼夜主光与三色环境光。随机拼图不依赖烘焙 Lightmap，以 SH 环境光提供稳定的间接照明。</summary>
    public static class LevelAPhaseVisuals
    {
        private static readonly Color DaySky = new(0.34f, 0.38f, 0.44f);
        private static readonly Color DayEquator = new(0.22f, 0.20f, 0.18f);
        private static readonly Color DayGround = new(0.09f, 0.075f, 0.06f);
        private static readonly Color NightSky = new(0.08f, 0.16f, 0.22f);
        private static readonly Color NightEquator = new(0.045f, 0.10f, 0.13f);
        private static readonly Color NightGround = new(0.015f, 0.035f, 0.045f);

        private static bool _captured;
        private static Light _directionalLight;
        private static Color _dayLightColor;
        private static float _dayLightIntensity;

        public static void Apply(LevelAPhase phase)
        {
            CaptureDefaults();
            if (!_captured)
                return;

            if (phase == LevelAPhase.Day)
            {
                RestoreDayDefaults();
                return;
            }

            if (_directionalLight != null)
            {
                _directionalLight.color = new Color(0.48f, 0.65f, 0.82f);
                _directionalLight.intensity = _dayLightIntensity * 0.58f;
            }
            ApplyAmbient(NightSky, NightEquator, NightGround);
        }

        public static void RestoreDayDefaults()
        {
            CaptureDefaults();
            if (!_captured)
                return;

            if (_directionalLight != null)
            {
                _directionalLight.color = _dayLightColor;
                _directionalLight.intensity = _dayLightIntensity;
            }
            ApplyAmbient(DaySky, DayEquator, DayGround);
        }

        private static void CaptureDefaults()
        {
            if (_captured)
                return;

            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type != LightType.Directional)
                    continue;
                _directionalLight = light;
                break;
            }

            _dayLightColor = _directionalLight != null
                ? _directionalLight.color
                : Color.white;
            _dayLightIntensity = _directionalLight != null
                ? _directionalLight.intensity
                : 1f;
            _captured = true;
        }

        private static void ApplyAmbient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = 1f;

            // Skybox 模式缓存的 SH 不会因仅改颜色自动更新；主动刷新确保 Editor 与 Player 一致。
            DynamicGI.UpdateEnvironment();
        }
    }
}
