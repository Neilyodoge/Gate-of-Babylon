#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace XianTu.EditorTools
{
    /// <summary>
    /// 一次性工具：从 NotoSansSC-Regular.otf 生成 TMP 动态字体资产（CJK 按需生成字形），
    /// 供 uGUI+TMP UI 使用。生成到 Resources/Fonts/NotoSansSC SDF.asset。
    /// 菜单：仙途秘境/UI/生成中文 TMP 字体资产。
    /// </summary>
    public static class TMPFontAssetCreator
    {
        private const string OtfPath = "Assets/1Game/Resources/Fonts/NotoSansSC-Regular.otf";
        private const string OutPath = "Assets/1Game/Resources/Fonts/NotoSansSC SDF.asset";

        private const string EssentialPkg = "Library/PackageCache/com.unity.textmeshpro@3.0.7/Package Resources/TMP Essential Resources.unitypackage";

        /// <summary>静默导入 TMP 必需资源（生成 Assets/TextMesh Pro/Resources/TMP Settings.asset 等）。</summary>
        [MenuItem("仙途秘境/UI/导入 TMP 必需资源")]
        public static void ImportTMPEssentials()
        {
            if (TMP_Settings.instance != null)
            {
                Debug.Log("[TMP] TMP Settings 已存在，无需导入。");
                return;
            }
            if (!System.IO.File.Exists(EssentialPkg))
            {
                Debug.LogError("[TMP] 未找到必需资源包: " + EssentialPkg);
                return;
            }
            AssetDatabase.ImportPackage(EssentialPkg, false);
            Debug.Log("[TMP] 已静默导入 TMP 必需资源: " + EssentialPkg);
        }

        [MenuItem("仙途秘境/UI/生成中文 TMP 字体资产")]
        public static void CreateChineseTMPFont()
        {
            var otf = AssetDatabase.LoadAssetAtPath<Font>(OtfPath);
            if (otf == null)
            {
                Debug.LogError("[TMPFont] 源字体未找到: " + OtfPath);
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutPath);
            if (existing != null)
            {
                Debug.Log("[TMPFont] 已存在，跳过: " + OutPath);
                Selection.activeObject = existing;
                return;
            }

            // 动态图集：运行时按需渲染字形，适合大字符集 CJK
            var fa = TMP_FontAsset.CreateFontAsset(
                otf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fa.name = "NotoSansSC SDF";

            AssetDatabase.CreateAsset(fa, OutPath);

            var tex = fa.atlasTextures[0];
            tex.name = "NotoSansSC SDF Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);

            var mat = fa.material;
            mat.name = "NotoSansSC SDF Material";
            AssetDatabase.AddObjectToAsset(mat, fa);

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TMPFont] 已生成: " + OutPath + " popMode=" + fa.atlasPopulationMode);
            Selection.activeObject = fa;
        }
    }
}
#endif
