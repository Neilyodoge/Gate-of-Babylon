using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 材质创建辅助工具
    /// 统一管理 Shader 查找，带 fallback 逻辑，避免粉色材质
    /// </summary>
    public static class MaterialHelper
    {
        private static Shader _cachedLitShader;
        private static Shader _cachedUnlitShader;

        /// <summary>
        /// 获取可用的 Lit Shader（带 fallback）
        /// 优先级：URP/Lit → URP/Simple Lit → URP/Unlit → Standard → 内置 Diffuse
        /// </summary>
        public static Shader GetLitShader()
        {
            if (_cachedLitShader != null) return _cachedLitShader;

            _cachedLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_cachedLitShader != null) return _cachedLitShader;

            _cachedLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (_cachedLitShader != null) return _cachedLitShader;

            _cachedLitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_cachedLitShader != null) return _cachedLitShader;

            _cachedLitShader = Shader.Find("Standard");
            if (_cachedLitShader != null) return _cachedLitShader;

            _cachedLitShader = Shader.Find("Legacy Shaders/Diffuse");
            return _cachedLitShader;
        }

        /// <summary>
        /// 获取 Unlit Shader（带 fallback）
        /// </summary>
        public static Shader GetUnlitShader()
        {
            if (_cachedUnlitShader != null) return _cachedUnlitShader;

            _cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_cachedUnlitShader != null) return _cachedUnlitShader;

            _cachedUnlitShader = Shader.Find("Unlit/Color");
            return _cachedUnlitShader;
        }

        /// <summary>创建一个 Lit 材质（不透明）</summary>
        public static Material CreateLit(Color color)
        {
            var shader = GetLitShader();
            if (shader == null)
            {
                Debug.LogWarning("[MaterialHelper] 找不到任何可用的 Shader！");
                return new Material(Shader.Find("Hidden/InternalErrorShader")) { color = color };
            }
            var mat = new Material(shader);
            mat.color = color;
            // 兼容 URP 和 Standard 的 BaseColor
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            return mat;
        }

        /// <summary>创建一个 Lit 材质（带自发光）</summary>
        public static Material CreateLitEmissive(Color baseColor, Color emissionColor)
        {
            var mat = CreateLit(baseColor);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);
            // Standard shader 兼容
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        /// <summary>创建一个 Lit 材质（半透明）</summary>
        public static Material CreateLitTransparent(Color color)
        {
            var mat = CreateLit(color);
            // URP 透明设置
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            // Standard shader 兼容
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3); // Transparent
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
            return mat;
        }

        /// <summary>创建一个 Unlit 材质</summary>
        public static Material CreateUnlit(Color color)
        {
            var shader = GetUnlitShader();
            if (shader == null)
                return CreateLit(color); // fallback 到 Lit
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
