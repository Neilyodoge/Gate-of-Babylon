using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    /// <summary>
    /// Lit_BentNormal shader 的自定义 Material GUI
    /// 基于 URP LitShader GUI，增加了 Bent Normal (Visibility Cone) 区域
    /// 由于 LitDetailGUI 是 internal 的，Detail 功能在此内联实现
    /// </summary>
    internal class LitBentNormalShaderGUI : BaseShaderGUI
    {
        // ====== Foldout 扩展标志 ======
        static readonly uint BentNormalFoldout = 1 << 4;

        static readonly string[] workflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));

        // 标准 Lit 属性
        private LitGUI.LitProperties litProperties;

        // Detail 属性（内联，替代 LitDetailGUI.LitProperties）
        private MaterialProperty detailMaskProp;
        private MaterialProperty detailAlbedoMapScaleProp;
        private MaterialProperty detailAlbedoMapProp;
        private MaterialProperty detailNormalMapScaleProp;
        private MaterialProperty detailNormalMapProp;

        // Bent Normal (Visibility Cone) 属性
        private MaterialProperty enableVisibilityProp;
        private MaterialProperty occlusionScaleProp;

        // ====== Detail 区域的 Styles（内联，替代 LitDetailGUI.Styles） ======
        static class DetailStyles
        {
            public static readonly GUIContent detailInputs = EditorGUIUtility.TrTextContent(
                "Detail Inputs",
                "These settings define the surface details by tiling and overlaying additional maps on the surface.");

            public static readonly GUIContent detailMaskText = EditorGUIUtility.TrTextContent("Mask",
                "Select a mask for the Detail map. The mask uses the alpha channel of the selected texture. The Tiling and Offset settings have no effect on the mask.");

            public static readonly GUIContent detailAlbedoMapText = EditorGUIUtility.TrTextContent("Base Map",
                "Select the surface detail texture.The alpha of your texture determines surface hue and intensity.");

            public static readonly GUIContent detailNormalMapText = EditorGUIUtility.TrTextContent("Normal Map",
                "Designates a Normal Map to create the illusion of bumps and dents in the details of this Material's surface.");

            public static readonly GUIContent detailAlbedoMapScaleInfo = EditorGUIUtility.TrTextContent(
                "Setting the scaling factor to a value other than 1 results in a less performant shader variant.");

            public static readonly GUIContent detailAlbedoMapFormatError = EditorGUIUtility.TrTextContent(
                "This texture is not in linear space.");
        }

        // ====== Bent Normal 区域的 Styles ======
        static class BentNormalStyles
        {
            public static readonly GUIContent bentNormalHeader = EditorGUIUtility.TrTextContent(
                "Bent Normal (Visibility Cone)",
                "Bent Normal 数据存储在 Mesh UV2 中，由烘焙工具写入。启用后将使用 Visibility Cone 改善间接光遮蔽和镜面反射遮蔽。");

            public static readonly GUIContent enableVisibilityText = EditorGUIUtility.TrTextContent(
                "Enable Visibility (UV Data)",
                "启用后从 Mesh UV2 读取烘焙的 Bent Normal 数据 (Visibility Cone)。需要先使用 Bent Normal Baker 工具烘焙数据到 Mesh 中。");

            public static readonly GUIContent occlusionScaleText = EditorGUIUtility.TrTextContent(
                "Occlusion Scale",
                "控制 Visibility Cone 遮蔽效果的强度。0 = 无遮蔽，1 = 完全遮蔽。");

            public static readonly GUIContent helpBoxText = EditorGUIUtility.TrTextContent(
                "请先使用 Bent Normal Baker 工具（菜单：Rendering > Art Toolkit > Bent Normal Baker）将 Bent Normal 数据烘焙到 Mesh UV2 中。");
        }

        // ====== 注册额外的 Foldout 区域 ======
        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            // 注册 Detail Inputs 区域（内联实现）
            materialScopesList.RegisterHeaderScope(
                DetailStyles.detailInputs,
                Expandable.Details,
                _ => DoDetailArea());

            // 注册 Bent Normal 区域
            materialScopesList.RegisterHeaderScope(
                BentNormalStyles.bentNormalHeader,
                BentNormalFoldout,
                DrawBentNormalArea);
        }

        // ====== 查找属性 ======
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            litProperties = new LitGUI.LitProperties(properties);

            // Detail 属性（内联查找）
            detailMaskProp = FindProperty("_DetailMask", properties, false);
            detailAlbedoMapScaleProp = FindProperty("_DetailAlbedoMapScale", properties, false);
            detailAlbedoMapProp = FindProperty("_DetailAlbedoMap", properties, false);
            detailNormalMapScaleProp = FindProperty("_DetailNormalMapScale", properties, false);
            detailNormalMapProp = FindProperty("_DetailNormalMap", properties, false);

            // Bent Normal 属性
            enableVisibilityProp = FindProperty("_EnableVisibility", properties, false);
            occlusionScaleProp = FindProperty("_OcclusionScale", properties, false);
        }

        // ====== 材质验证 & 关键字设置 ======
        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetDetailMaterialKeywords);

            // 设置 _VISIBILITY_ON 关键字
            if (material.HasProperty("_EnableVisibility"))
            {
                CoreUtils.SetKeyword(material, "_VISIBILITY_ON", material.GetFloat("_EnableVisibility") > 0.0f);
            }
        }

        // ====== Surface Options ======
        public override void DrawSurfaceOptions(Material material)
        {
            EditorGUIUtility.labelWidth = 0f;

            if (litProperties.workflowMode != null)
                DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, workflowModeNames);

            base.DrawSurfaceOptions(material);
        }

        // ====== Surface Inputs ======
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            LitGUI.Inputs(litProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        // ====== Advanced Options ======
        public override void DrawAdvancedOptions(Material material)
        {
            if (litProperties.reflections != null && litProperties.highlights != null)
            {
                materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
                materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
            }

            base.DrawAdvancedOptions(material);
        }

        // ====== Detail 区域绘制（内联，替代 LitDetailGUI.DoDetailArea）======
        private void DoDetailArea()
        {
            if (detailMaskProp == null || detailAlbedoMapProp == null || detailNormalMapProp == null)
                return;

            materialEditor.TexturePropertySingleLine(DetailStyles.detailMaskText, detailMaskProp);
            materialEditor.TexturePropertySingleLine(DetailStyles.detailAlbedoMapText, detailAlbedoMapProp,
                detailAlbedoMapProp.textureValue != null ? detailAlbedoMapScaleProp : null);

            if (detailAlbedoMapScaleProp != null && detailAlbedoMapScaleProp.floatValue != 1.0f)
            {
                EditorGUILayout.HelpBox(DetailStyles.detailAlbedoMapScaleInfo.text, MessageType.Info, true);
            }

            var detailAlbedoTexture = detailAlbedoMapProp.textureValue as Texture2D;
            if (detailAlbedoTexture != null && GraphicsFormatUtility.IsSRGBFormat(detailAlbedoTexture.graphicsFormat))
            {
                EditorGUILayout.HelpBox(DetailStyles.detailAlbedoMapFormatError.text, MessageType.Warning, true);
            }

            materialEditor.TexturePropertySingleLine(DetailStyles.detailNormalMapText, detailNormalMapProp,
                detailNormalMapProp.textureValue != null ? detailNormalMapScaleProp : null);
            materialEditor.TextureScaleOffsetProperty(detailAlbedoMapProp);
        }

        // ====== Detail 关键字设置（内联，替代 LitDetailGUI.SetMaterialKeywords）======
        private static void SetDetailMaterialKeywords(Material material)
        {
            if (material.HasProperty("_DetailAlbedoMap") && material.HasProperty("_DetailNormalMap") && material.HasProperty("_DetailAlbedoMapScale"))
            {
                bool isScaled = material.GetFloat("_DetailAlbedoMapScale") != 1.0f;
                bool hasDetailMap = material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap");
                CoreUtils.SetKeyword(material, "_DETAIL_MULX2", !isScaled && hasDetailMap);
                CoreUtils.SetKeyword(material, "_DETAIL_SCALED", isScaled && hasDetailMap);
            }
        }

        // ====== Bent Normal 区域绘制 ======
        private void DrawBentNormalArea(Material material)
        {
            if (enableVisibilityProp != null)
            {
                EditorGUI.BeginChangeCheck();
                bool enableVisibility = enableVisibilityProp.floatValue > 0.0f;
                enableVisibility = EditorGUILayout.Toggle(BentNormalStyles.enableVisibilityText, enableVisibility);
                if (EditorGUI.EndChangeCheck())
                {
                    enableVisibilityProp.floatValue = enableVisibility ? 1.0f : 0.0f;
                }

                if (enableVisibility)
                {
                    EditorGUI.indentLevel++;
                    if (occlusionScaleProp != null)
                    {
                        materialEditor.ShaderProperty(occlusionScaleProp, BentNormalStyles.occlusionScaleText);
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.HelpBox(BentNormalStyles.helpBoxText.text, MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("未找到 _EnableVisibility 属性，请检查 Shader 是否正确。", MessageType.Warning);
            }
        }

        // ====== Shader 切换处理 ======
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            if (material.HasProperty("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Blend", (float)blendMode);

            material.SetFloat("_Surface", (float)surfaceType);
            if (surfaceType == SurfaceType.Opaque)
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (oldShader.name.Equals("Standard (Specular setup)"))
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
                Texture texture = material.GetTexture("_SpecGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
                Texture texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }
    }
}
