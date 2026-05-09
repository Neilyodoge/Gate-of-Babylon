using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    /// <summary>
    /// PBRToon/Base shader 的自定义 Material GUI
    /// </summary>
    internal class PBRToonBaseShaderGUI : BaseShaderGUI
    {
        // ====== Play Mode 状态跟踪（修复运行时 Inspector 点击无响应） ======
        private bool _lastPlaying;

        // ====== Foldout 标记 ======
        static readonly uint PBRPropsFoldout = 1 << 4;
        static readonly uint DirectLightFoldout = 1 << 5;
        static readonly uint IndirectLightFoldout = 1 << 6;
        static readonly uint EmissRimFoldout = 1 << 7;
        static readonly uint OutlineFoldout = 1 << 8;
        static readonly uint DebugFoldout = 1 << 9;

        // ====== Material Properties ======
        private MaterialProperty baseMapPropLocal;
        private MaterialProperty pbrMaskProp;
        private MaterialProperty normalMapProp;
        private MaterialProperty normalScaleProp;

        // PBR
        private MaterialProperty metallicProp;
        private MaterialProperty smoothnessProp;
        private MaterialProperty occlusionProp;

        // Direct Light
        private MaterialProperty selfLightProp;
        private MaterialProperty mainLightColorLerpProp;
        private MaterialProperty directOcclusionProp;

        // Shadow
        private MaterialProperty shadowColorProp;
        private MaterialProperty shadowOffsetProp;
        private MaterialProperty shadowSharpnessProp;
        private MaterialProperty shadowSmoothSceneProp;
        private MaterialProperty shadowStrengthProp;

        // Shadow Ramp
        private MaterialProperty enableShadowRampProp;
        private MaterialProperty shadowRampTexProp;

        // Indirect
        private MaterialProperty selfEnvColorProp;
        private MaterialProperty envColorLerpProp;
        private MaterialProperty indirDiffUpDirSHProp;
        private MaterialProperty indirDiffIntensityProp;
        private MaterialProperty enableIndirCubemapProp;
        private MaterialProperty indirSpecCubemapProp;
        private MaterialProperty indirSpecCubeWeightProp;
        private MaterialProperty indirSpecIntensityProp;

        // Emission & Rim
        private MaterialProperty emissionColProp;
        private MaterialProperty directRimFrontColProp;
        private MaterialProperty directRimBackColProp;
        private MaterialProperty directRimWidthProp;
        private MaterialProperty punctualRimWidthProp;

        // SSS Skin
        private MaterialProperty enableSkinProp;
        private MaterialProperty sssColorProp;
        private MaterialProperty sssAreaProp;

        // Outline
        private MaterialProperty enableOutlineProp;
        private MaterialProperty outlineWidthProp;
        private MaterialProperty outlineColorProp;
        private MaterialProperty outlineDepthOldRangeProp;
        private MaterialProperty outlineDepthNewRangeProp;
        private MaterialProperty outlineNormalScaleProp;

        // PCF
        private MaterialProperty toonShadowProp;

        // PCSS
        private MaterialProperty pcssSoftnessProp;
        private MaterialProperty pcssSoftnessFalloffProp;
        private MaterialProperty pcssBlockerSamplesProp;
        private MaterialProperty pcssFilterSamplesProp;
        private MaterialProperty pcssBlockerGradientBiasProp;
        private MaterialProperty pcssPCFGradientBiasProp;

        // Shadow Edge Color
        private MaterialProperty enableShadowEdgeColorProp;
        private MaterialProperty shadowEdgeBeginProp;
        private MaterialProperty shadowEdgeEndProp;
        private MaterialProperty shadowEdgeBeginColorProp;
        private MaterialProperty shadowEdgeEndColorProp;
        private MaterialProperty shadowEdgeDarkColorProp;
        private MaterialProperty shadowEdgeLightColorProp;
        private MaterialProperty shadowEdgeFadeBeginWidthProp;
        private MaterialProperty shadowEdgeFadeEndWidthProp;

        // Debug
        private MaterialProperty debugShadowProp;
        private MaterialProperty debugShadowModeProp;

        // Other
        private MaterialProperty cullProp;
        private MaterialProperty cutoffProp;

        // ====== Styles ======
        new static class Styles
        {
            public static readonly GUIContent pbrPropsHeader = EditorGUIUtility.TrTextContent("PBR Properties");
            public static readonly GUIContent directLightHeader = EditorGUIUtility.TrTextContent("Direct Light");
            public static readonly GUIContent indirectLightHeader = EditorGUIUtility.TrTextContent("Indirect Light");
            public static readonly GUIContent emissRimHeader = EditorGUIUtility.TrTextContent("Emission & Rim Light");
            public static readonly GUIContent outlineHeader = EditorGUIUtility.TrTextContent("Outline");
            public static readonly GUIContent debugHeader = EditorGUIUtility.TrTextContent("⚠ Debug (Editor Only)");
        }

        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);

            baseMapPropLocal = FindProperty("_BaseMap", properties, false);
            pbrMaskProp = FindProperty("_PBRMask", properties, false);
            normalMapProp = FindProperty("_NormalMap", properties, false);
            normalScaleProp = FindProperty("_NormalScale", properties, false);

            metallicProp = FindProperty("_Metallic", properties, false);
            smoothnessProp = FindProperty("_Smoothness", properties, false);
            occlusionProp = FindProperty("_Occlusion", properties, false);

            selfLightProp = FindProperty("_SelfLight", properties, false);
            mainLightColorLerpProp = FindProperty("_MainLightColorLerp", properties, false);
            directOcclusionProp = FindProperty("_DirectOcclusion", properties, false);

            shadowColorProp = FindProperty("_ShadowColor", properties, false);
            shadowOffsetProp = FindProperty("_ShadowOffset", properties, false);
            shadowSharpnessProp = FindProperty("_ShadowSharpness", properties, false);
            shadowSmoothSceneProp = FindProperty("_ShadowSmoothScene", properties, false);
            shadowStrengthProp = FindProperty("_ShadowStrength", properties, false);

            enableShadowRampProp = FindProperty("_EnableShadowRamp", properties, false);
            shadowRampTexProp = FindProperty("_ShadowRampTex", properties, false);

            selfEnvColorProp = FindProperty("_SelfEnvColor", properties, false);
            envColorLerpProp = FindProperty("_EnvColorLerp", properties, false);
            indirDiffUpDirSHProp = FindProperty("_IndirDiffUpDirSH", properties, false);
            indirDiffIntensityProp = FindProperty("_IndirDiffIntensity", properties, false);
            enableIndirCubemapProp = FindProperty("_EnableIndirCubemap", properties, false);
            indirSpecCubemapProp = FindProperty("_IndirSpecCubemap", properties, false);
            indirSpecCubeWeightProp = FindProperty("_IndirSpecCubeWeight", properties, false);
            indirSpecIntensityProp = FindProperty("_IndirSpecIntensity", properties, false);

            emissionColProp = FindProperty("_EmissionCol", properties, false);
            directRimFrontColProp = FindProperty("_DirectRimFrontCol", properties, false);
            directRimBackColProp = FindProperty("_DirectRimBackCol", properties, false);
            directRimWidthProp = FindProperty("_DirectRimWidth", properties, false);
            punctualRimWidthProp = FindProperty("_PunctualRimWidth", properties, false);

            enableSkinProp = FindProperty("_EnableSkin", properties, false);
            sssColorProp = FindProperty("_SSSColor", properties, false);
            sssAreaProp = FindProperty("_SSSArea", properties, false);

            enableOutlineProp = FindProperty("_EnableOutline", properties, false);
            outlineWidthProp = FindProperty("_OutlineWidth", properties, false);
            outlineColorProp = FindProperty("_OutlineColor", properties, false);
            outlineDepthOldRangeProp = FindProperty("_OutlineDepthOldRange", properties, false);
            outlineDepthNewRangeProp = FindProperty("_OutlineDepthNewRange", properties, false);
            outlineNormalScaleProp = FindProperty("_OutlineNormalScale", properties, false);

            toonShadowProp = FindProperty("_ToonShadow", properties, false);

            pcssSoftnessProp = FindProperty("_PcssSoftness", properties, false);
            pcssSoftnessFalloffProp = FindProperty("_PcssSoftnessFalloff", properties, false);
            pcssBlockerSamplesProp = FindProperty("_PcssBlockerSamples", properties, false);
            pcssFilterSamplesProp = FindProperty("_PcssFilterSamples", properties, false);
            pcssBlockerGradientBiasProp = FindProperty("_PcssBlockerGradientBias", properties, false);
            pcssPCFGradientBiasProp = FindProperty("_PcssPCFGradientBias", properties, false);

            enableShadowEdgeColorProp = FindProperty("_EnableShadowEdgeColor", properties, false);
            shadowEdgeBeginProp = FindProperty("_ShadowEdgeBegin", properties, false);
            shadowEdgeEndProp = FindProperty("_ShadowEdgeEnd", properties, false);
            shadowEdgeBeginColorProp = FindProperty("_ShadowEdgeBeginColor", properties, false);
            shadowEdgeEndColorProp = FindProperty("_ShadowEdgeEndColor", properties, false);
            shadowEdgeDarkColorProp = FindProperty("_ShadowEdgeDarkColor", properties, false);
            shadowEdgeLightColorProp = FindProperty("_ShadowEdgeLightColor", properties, false);
            shadowEdgeFadeBeginWidthProp = FindProperty("_ShadowEdgeFadeBeginWidth", properties, false);
            shadowEdgeFadeEndWidthProp = FindProperty("_ShadowEdgeFadeEndWidth", properties, false);

            debugShadowProp = FindProperty("_DebugShadow", properties, false);
            debugShadowModeProp = FindProperty("_DebugShadowMode", properties, false);

            cullProp = FindProperty("_Cull", properties, false);
            cutoffProp = FindProperty("_Cutoff", properties, false);
        }

        public override void DrawSurfaceOptions(Material material)
        {
            // Cull Mode
            if (cullProp != null)
                materialEditor.ShaderProperty(cullProp, "Cull Mode");

            // Alpha Clip
            if (alphaClipProp != null)
            {
                materialEditor.ShaderProperty(alphaClipProp, "Alpha Clip");
                if (alphaClipProp.floatValue > 0 && cutoffProp != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(cutoffProp, "Cutoff");
                    EditorGUI.indentLevel--;
                }
            }
        }

        public override void DrawSurfaceInputs(Material material)
        {
            // Textures
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            if (baseColorProp != null)
                materialEditor.ShaderProperty(baseColorProp, "Base Color");
            if (baseMapPropLocal != null)
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Base Map"), baseMapPropLocal);
            if (pbrMaskProp != null)
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("PBR Mask (M/S/AO/Emiss)"), pbrMaskProp);
            if (normalMapProp != null && normalScaleProp != null)
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Normal Map"), normalMapProp, normalScaleProp);

            if (baseMapPropLocal != null)
                DrawTileOffset(materialEditor, baseMapPropLocal);
        }

        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(Styles.pbrPropsHeader, PBRPropsFoldout, DrawPBRProps);
            materialScopesList.RegisterHeaderScope(Styles.directLightHeader, DirectLightFoldout, DrawDirectLight);
            materialScopesList.RegisterHeaderScope(Styles.indirectLightHeader, IndirectLightFoldout, DrawIndirectLight);
            materialScopesList.RegisterHeaderScope(Styles.emissRimHeader, EmissRimFoldout, DrawEmissRim);
            materialScopesList.RegisterHeaderScope(Styles.outlineHeader, OutlineFoldout, DrawOutline);
            materialScopesList.RegisterHeaderScope(Styles.debugHeader, DebugFoldout, DrawDebug);
        }

        private void DrawPBRProps(Material material)
        {
            if (metallicProp != null) materialEditor.ShaderProperty(metallicProp, "Metallic");
            if (smoothnessProp != null) materialEditor.ShaderProperty(smoothnessProp, "Smoothness");
            if (occlusionProp != null) materialEditor.ShaderProperty(occlusionProp, "Occlusion");
        }

        private void DrawDirectLight(Material material)
        {
            if (selfLightProp != null) materialEditor.ShaderProperty(selfLightProp, "Self Light");
            if (mainLightColorLerpProp != null) materialEditor.ShaderProperty(mainLightColorLerpProp, "Unity Light ↔ Self Light");
            if (directOcclusionProp != null) materialEditor.ShaderProperty(directOcclusionProp, "Direct Occlusion");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shadow", EditorStyles.boldLabel);
            if (shadowColorProp != null) materialEditor.ShaderProperty(shadowColorProp, "Shadow Color");
            if (shadowSmoothSceneProp != null) materialEditor.ShaderProperty(shadowSmoothSceneProp, "Shadow Smooth Scene");
            if (shadowStrengthProp != null) materialEditor.ShaderProperty(shadowStrengthProp, "Shadow Strength");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shadow Ramp", EditorStyles.boldLabel);
            if (enableShadowRampProp != null) materialEditor.ShaderProperty(enableShadowRampProp, "Enable Shadow Ramp");
            if (enableShadowRampProp != null && enableShadowRampProp.floatValue > 0 && shadowRampTexProp != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Shadow Ramp Texture"), shadowRampTexProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("明暗交界线", EditorStyles.boldLabel);
            if (shadowOffsetProp != null) materialEditor.ShaderProperty(shadowOffsetProp, "位置 (Shadow Offset)");
            if (shadowSharpnessProp != null) materialEditor.ShaderProperty(shadowSharpnessProp, "软硬 (Shadow Sharpness)");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shadow PCF", EditorStyles.boldLabel);
            if (toonShadowProp != null)
            {
                materialEditor.ShaderProperty(toonShadowProp, "Shadow Quality");
                int pcfMode = (int)toonShadowProp.floatValue;
                // PCSS 模式 (index=5) 显示额外参数
                if (pcfMode == 5)
                {
                    EditorGUI.indentLevel++;
                    if (pcssSoftnessProp != null) materialEditor.ShaderProperty(pcssSoftnessProp, "Softness");
                    if (pcssSoftnessFalloffProp != null) materialEditor.ShaderProperty(pcssSoftnessFalloffProp, "Softness Falloff");
                    if (pcssBlockerSamplesProp != null) materialEditor.ShaderProperty(pcssBlockerSamplesProp, "Blocker Samples");
                    if (pcssFilterSamplesProp != null) materialEditor.ShaderProperty(pcssFilterSamplesProp, "Filter Samples");
                    if (pcssBlockerGradientBiasProp != null) materialEditor.ShaderProperty(pcssBlockerGradientBiasProp, "Blocker Gradient Bias");
                    if (pcssPCFGradientBiasProp != null) materialEditor.ShaderProperty(pcssPCFGradientBiasProp, "PCF Gradient Bias");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.HelpBox(
                "使用 The Witness 优化 PCF 替代 URP 默认阴影滤波\n" +
                "Base: 1次硬件 2x2 PCF（最快，硬阴影）\n" +
                "PCF 2x2: 同 Base，显式选择\n" +
                "PCF 3x3: 4次采样（默认）\n" +
                "PCF 5x5: 9次采样，更柔和\n" +
                "PCF 7x7: 16次采样，最高质量固定核\n" +
                "PCSS: 可变半径软阴影（距离自适应）",
                MessageType.Info);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shadow Edge Color", EditorStyles.boldLabel);
            if (enableShadowEdgeColorProp != null)
            {
                materialEditor.ShaderProperty(enableShadowEdgeColorProp, "Enable Shadow Edge Color");
                if (enableShadowEdgeColorProp.floatValue > 0)
                {
                    EditorGUI.indentLevel++;
                    if (shadowEdgeBeginProp != null) materialEditor.ShaderProperty(shadowEdgeBeginProp, "渐变起始 (Begin)");
                    if (shadowEdgeEndProp != null) materialEditor.ShaderProperty(shadowEdgeEndProp, "渐变结束 (End)");
                    if (shadowEdgeBeginColorProp != null) materialEditor.ShaderProperty(shadowEdgeBeginColorProp, "暗端颜色 (Begin Color)");
                    if (shadowEdgeEndColorProp != null) materialEditor.ShaderProperty(shadowEdgeEndColorProp, "亮端颜色 (End Color)");
                    if (shadowEdgeDarkColorProp != null) materialEditor.ShaderProperty(shadowEdgeDarkColorProp, "全暗区颜色 (Dark)");
                    if (shadowEdgeLightColorProp != null) materialEditor.ShaderProperty(shadowEdgeLightColorProp, "全亮区颜色 (Light)");
                    if (shadowEdgeFadeBeginWidthProp != null) materialEditor.ShaderProperty(shadowEdgeFadeBeginWidthProp, "暗端过渡宽度");
                    if (shadowEdgeFadeEndWidthProp != null) materialEditor.ShaderProperty(shadowEdgeFadeEndWidthProp, "亮端过渡宽度");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.HelpBox(
                "在阴影边缘区域叠加多段渐变颜色，增强视觉层次\n" +
                "搬运自 V114 yarp 管线 GetShadowEdgeColor2\n" +
                "Begin/End: 核心渐变区域的阴影值起止\n" +
                "Dark/Light: 全暗/全亮区域的颜色\n" +
                "Fade Width: 暗端/亮端的平滑过渡宽度",
                MessageType.Info);
        }

        private void DrawIndirectLight(Material material)
        {
            EditorGUILayout.LabelField("Diffuse", EditorStyles.boldLabel);
            if (selfEnvColorProp != null) materialEditor.ShaderProperty(selfEnvColorProp, "Self Env Color");
            if (envColorLerpProp != null) materialEditor.ShaderProperty(envColorLerpProp, "Unity SH ↔ Self Env");
            if (indirDiffUpDirSHProp != null) materialEditor.ShaderProperty(indirDiffUpDirSHProp, "Up Dir SH Blend");
            if (indirDiffIntensityProp != null) materialEditor.ShaderProperty(indirDiffIntensityProp, "Diffuse Intensity");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Specular", EditorStyles.boldLabel);
            if (enableIndirCubemapProp != null) materialEditor.ShaderProperty(enableIndirCubemapProp, "Enable Custom Cubemap");
            if (enableIndirCubemapProp != null && enableIndirCubemapProp.floatValue > 0 && indirSpecCubemapProp != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Spec Cubemap"), indirSpecCubemapProp);
                if (indirSpecCubeWeightProp != null) materialEditor.ShaderProperty(indirSpecCubeWeightProp, "Cube Weight");
                EditorGUI.indentLevel--;
            }
            if (indirSpecIntensityProp != null) materialEditor.ShaderProperty(indirSpecIntensityProp, "Spec Intensity");
        }

        private void DrawEmissRim(Material material)
        {
            EditorGUILayout.LabelField("Emission", EditorStyles.boldLabel);
            if (emissionMapProp != null)
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Emission Map"), emissionMapProp);
            if (emissionColProp != null) materialEditor.ShaderProperty(emissionColProp, "Emission Color");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Rim Light", EditorStyles.boldLabel);
            if (directRimFrontColProp != null) materialEditor.ShaderProperty(directRimFrontColProp, "Front Rim Color");
            if (directRimBackColProp != null) materialEditor.ShaderProperty(directRimBackColProp, "Back Rim Color");
            if (directRimWidthProp != null) materialEditor.ShaderProperty(directRimWidthProp, "Direct Rim Width");
            if (punctualRimWidthProp != null) materialEditor.ShaderProperty(punctualRimWidthProp, "Punctual Rim Width");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("SSS Skin", EditorStyles.boldLabel);
            if (enableSkinProp != null)
            {
                materialEditor.ShaderProperty(enableSkinProp, "Enable Skin SSS");
                if (enableSkinProp.floatValue > 0)
                {
                    EditorGUI.indentLevel++;
                    if (sssColorProp != null) materialEditor.ShaderProperty(sssColorProp, "SSS Color (skin tint)");
                    if (sssAreaProp != null) materialEditor.ShaderProperty(sssAreaProp, "SSS Area / Strength");
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.HelpBox(
                "基于视角 (Fresnel) 的轻量级假 SSS：\n" +
                "掠射角处把 albedo 朝 SSS Color 偏移，模拟皮肤在耳廓 / 鼻翼 / 手指边缘的红透感\n" +
                "SSS Color: 皮肤次表面颜色，默认偏红肉色\n" +
                "SSS Area: 范围/强度，0 = 关闭染色，越大边缘红透越明显",
                MessageType.Info);
        }

        private void DrawOutline(Material material)
        {
            if (enableOutlineProp != null) materialEditor.ShaderProperty(enableOutlineProp, "Enable Outline");
            if (enableOutlineProp != null && enableOutlineProp.floatValue > 0)
            {
                EditorGUI.indentLevel++;
                if (outlineWidthProp != null) materialEditor.ShaderProperty(outlineWidthProp, "描边宽度");
                if (outlineColorProp != null) materialEditor.ShaderProperty(outlineColorProp, "描边颜色");
                if (outlineNormalScaleProp != null) materialEditor.ShaderProperty(outlineNormalScaleProp, "法线XY缩放");

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("视距自适应", EditorStyles.boldLabel);
                if (outlineDepthOldRangeProp != null) materialEditor.ShaderProperty(outlineDepthOldRangeProp, "深度旧范围 (near/mid/far)");
                if (outlineDepthNewRangeProp != null) materialEditor.ShaderProperty(outlineDepthNewRangeProp, "深度新范围 (near/mid/far)");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.HelpBox(
                "原神风格背面法线外扩描边\n" +
                "平滑法线固定从 UV3 (TEXCOORD3).xy 解码（2通道，z由勾股定理重建）\n" +
                "UV 通道分配: UV2=BentNormal, UV3=平滑法线\n" +
                "需用平滑法线烘焙工具烘焙，如未烘焙会自动回退到 Tangent 方向\n" +
                "顶点色 A 通道控制逐顶点描边深度缩放\n" +
                "需要在 URP Renderer 中添加 ToonOutlineRenderFeature",
                MessageType.Info);
        }

        private void DrawDebug(Material material)
        {
            EditorGUILayout.HelpBox(
                "仅用于编辑器调试，使用 shader_feature_local 变体\n" +
                "不会被打包进最终构建",
                MessageType.Warning);
            if (debugShadowProp != null) materialEditor.ShaderProperty(debugShadowProp, "Debug Shadow");
            if (debugShadowProp != null && debugShadowProp.floatValue > 0 && debugShadowModeProp != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(debugShadowModeProp, "可视化模式");
                EditorGUILayout.HelpBox(
                    "Shadow: 阴影区域灰度（shadowArea，含 PCF/PCSS + NdotL），开启 Edge Color 时带 Edge Color\n" +
                    "Ramp: Shadow Ramp 贴图采样的原始结果（Edge Color 之前）",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }

        public override void OnGUI(MaterialEditor materialEditorIn, MaterialProperty[] properties)
        {
            // Play Mode 切换时强制重新初始化 GUI，修复运行时 Inspector 点击无响应的问题
            bool currentPlaying = EditorApplication.isPlaying;
            if (currentPlaying != _lastPlaying)
            {
                _lastPlaying = currentPlaying;
                m_FirstTimeApply = true;
            }
            base.OnGUI(materialEditorIn, properties);
        }

        public override void DrawAdvancedOptions(Material material)
        {
            base.DrawAdvancedOptions(material);
        }

        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material);
        }

        private static void SetMaterialKeywords(Material material)
        {
            if (material.HasProperty("_EnableShadowRamp"))
                CoreUtils.SetKeyword(material, "_SHADOW_RAMP", material.GetFloat("_EnableShadowRamp") > 0);
            if (material.HasProperty("_EnableIndirCubemap"))
                CoreUtils.SetKeyword(material, "_INDIR_CUBEMAP", material.GetFloat("_EnableIndirCubemap") > 0);
            if (material.HasProperty("_AlphaClip"))
                CoreUtils.SetKeyword(material, "_ALPHATEST_ON", material.GetFloat("_AlphaClip") > 0);
            if (material.HasProperty("_EnableOutline"))
                CoreUtils.SetKeyword(material, "_OUTLINE_ON", material.GetFloat("_EnableOutline") > 0);
            if (material.HasProperty("_ToonShadow"))
            {
                int pcfMode = (int)material.GetFloat("_ToonShadow");
                // 先清除所有 PCF keyword
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_BASE", pcfMode == 0);
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_PCF_2X2", pcfMode == 1);
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_PCF_3X3", pcfMode == 2);
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_PCF_5X5", pcfMode == 3);
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_PCF_7X7", pcfMode == 4);
                CoreUtils.SetKeyword(material, "_TOON_SHADOW_PCSS", pcfMode == 5);
            }
            if (material.HasProperty("_EnableShadowEdgeColor"))
                CoreUtils.SetKeyword(material, "_SHADOW_EDGE_COLOR", material.GetFloat("_EnableShadowEdgeColor") > 0);
            if (material.HasProperty("_EnableSkin"))
                CoreUtils.SetKeyword(material, "_SKIN_ON", material.GetFloat("_EnableSkin") > 0);
            // _TOON_SHADOW KeywordEnum 已更新为多级选项
            if (material.HasProperty("_DebugShadow"))
                CoreUtils.SetKeyword(material, "_DEBUG_SHADOW", material.GetFloat("_DebugShadow") > 0);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (newShader != null)
            {
                SetMaterialKeywords(material);
            }
        }
    }
}
