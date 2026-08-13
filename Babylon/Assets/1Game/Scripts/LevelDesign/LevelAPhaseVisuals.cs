using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>首版只切整体环境色与主光，正式 Volume 资产后续由美术替换。</summary>
    public static class LevelAPhaseVisuals
    {
        private static bool _captured;
        private static Light _directionalLight;
        private static Color _dayLightColor;
        private static float _dayLightIntensity;
        private static Color _dayAmbientColor;
        private static Color _dayFogColor;
        private static float _dayFogDensity;

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
            RenderSettings.ambientLight = new Color(0.12f, 0.22f, 0.28f);
            RenderSettings.fogColor = new Color(0.08f, 0.20f, 0.23f);
            RenderSettings.fogDensity = _dayFogDensity;
        }

        public static void RestoreDayDefaults()
        {
            if (!_captured)
                return;

            if (_directionalLight != null)
            {
                _directionalLight.color = _dayLightColor;
                _directionalLight.intensity = _dayLightIntensity;
            }
            RenderSettings.ambientLight = _dayAmbientColor;
            RenderSettings.fogColor = _dayFogColor;
            RenderSettings.fogDensity = _dayFogDensity;
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
            _dayAmbientColor = RenderSettings.ambientLight;
            _dayFogColor = RenderSettings.fogColor;
            _dayFogDensity = RenderSettings.fogDensity;
            _captured = true;
        }
    }
}
