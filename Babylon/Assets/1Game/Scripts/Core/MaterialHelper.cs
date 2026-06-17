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
        private static Material _sharedLitEmissive;

        // 通过 MPB 设值时使用的属性 ID（缓存避免每次字符串哈希）
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");

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

        /// <summary>
        /// 获取一个共享的 Lit + Emission 材质（全局唯一实例）。
        /// 配合 <see cref="ApplyEmissiveColor"/> 用 MaterialPropertyBlock 设置颜色，
        /// 避免每个 Renderer 各自 new Material（拾取物等高频生成对象的标准用法）。
        /// 注意：返回的是 sharedMaterial，不要修改其属性。
        /// </summary>
        public static Material GetSharedLitEmissive()
        {
            if (_sharedLitEmissive != null) return _sharedLitEmissive;

            var shader = GetLitShader();
            if (shader == null) return null;

            _sharedLitEmissive = new Material(shader)
            {
                name = "SharedLitEmissive(Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            _sharedLitEmissive.EnableKeyword("_EMISSION");
            _sharedLitEmissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return _sharedLitEmissive;
        }

        /// <summary>
        /// 给 Renderer 应用共享 LitEmissive 材质，并通过 MaterialPropertyBlock 设置 base/emission 颜色。
        /// 不会创建新的 Material 实例，适合大量拾取物 / 短生命周期对象。
        /// </summary>
        public static void ApplyEmissiveColor(Renderer renderer, Color baseColor, Color emissionColor)
        {
            if (renderer == null) return;

            var shared = GetSharedLitEmissive();
            if (shared != null)
                renderer.sharedMaterial = shared;

            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(_baseColorId, baseColor);
            mpb.SetColor(_colorId, baseColor);
            mpb.SetColor(_emissionColorId, emissionColor);
            renderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// 安全读取材质主色：URP/Lit 用 _BaseColor，旧版/内置用 _Color。
        /// 若两者皆无（如部分 Shader Graph / Legacy 粒子着色器），返回白色且不刷错误日志。
        /// </summary>
        public static Color SafeGetColor(Material m)
        {
            if (m == null) return Color.white;
            if (m.HasProperty(_baseColorId)) return m.GetColor(_baseColorId);
            if (m.HasProperty(_colorId)) return m.GetColor(_colorId);
            return Color.white;
        }

        /// <summary>
        /// 安全写入材质主色：找不到颜色属性时静默跳过，避免 "doesn't have a color property" 报错。
        /// </summary>
        public static void SafeSetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty(_baseColorId)) m.SetColor(_baseColorId, c);
            else if (m.HasProperty(_colorId)) m.SetColor(_colorId, c);
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
