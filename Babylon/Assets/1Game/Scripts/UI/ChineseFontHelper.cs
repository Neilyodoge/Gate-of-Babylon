using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace XianTu
{
    /// <summary>
    /// UITK 中文字体注入：确保所有面板使用 NotoSansSC SDF FontAsset，
    /// 解决部分电脑中文不显示的问题。
    ///
    /// 主修复在 PanelSettings.textSettings（XianTuTextSettings）已全局指定默认中文字体；
    /// 此 helper 作为兜底，在每个面板 root 上显式设置 unityFontDefinition（FontAsset 路径），
    /// 避免旧版 unityFont 路径在 2022.3 TextCore 引擎下不渲染 CJK 的问题。
    ///
    /// 字体资源：Assets/1Game/Resources/Fonts/NotoSansSC-Regular-SDF.asset
    /// </summary>
    public static class ChineseFontHelper
    {
        private static FontAsset _cachedFontAsset;
        private static bool _triedLoad;

        private static FontAsset LoadFontAsset()
        {
            if (_triedLoad) return _cachedFontAsset;
            _triedLoad = true;
            _cachedFontAsset = Resources.Load<FontAsset>("Fonts/NotoSansSC-Regular-SDF");
            if (_cachedFontAsset == null)
                Debug.LogWarning("[ChineseFontHelper] 未找到 Resources/Fonts/NotoSansSC-Regular-SDF，中文可能无法显示");
            return _cachedFontAsset;
        }

        /// <summary>
        /// 在 rootVisualElement 上注入中文 FontAsset。在每个 UITK 面板的 Awake 末尾调用。
        /// </summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            var fa = LoadFontAsset();
            if (fa == null) return;
            root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(fa));
        }
    }
}
