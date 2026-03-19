using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGUI
{
    /// <summary>
    /// PBRToon/Hair shader 的自定义 Material GUI
    /// 在 Base GUI 基础上增加各向异性高光区域
    /// </summary>
    internal class PBRToonHairShaderGUI : BaseShaderGUI
    {
        static readonly uint PBRPropsFoldout = 1 << 4;
        static readonly uint HairSpecFoldout = 1 << 5;
        static readonly uint DirectLightFoldout = 1 << 6;
        static readonly uint IndirectLightFoldout = 1 << 7;
        static readonly uint EmissRimFoldout = 1 << 8;
        static readonly uint OutlineFoldout = 1 << 9;

        // Properties
        private MaterialProperty baseColorProp;
        private MaterialProperty baseMapPropLocal;
        private MaterialProperty pbrMaskProp;
        private MaterialProperty normalMapProp;
        private MaterialProperty normalScaleProp;

        private MaterialProperty metallicProp;
        private MaterialProperty smoothnessProp;
        private MaterialProperty occlusionProp;

        // Hair Spec
        private MaterialProperty hairSpecTexProp;
        private MaterialProperty specColorProp;
        private MaterialProperty anisotropicSlideProp;
        private MaterialProperty anisotropicOffsetProp;
        private MaterialProperty blinnPhongPowProp;
        private MaterialProperty specMinimumProp;

        private MaterialProperty selfLightProp;
        private MaterialProperty mainLightColorLerpProp;
        private MaterialProperty directOcclusionProp;

        private MaterialProperty shadowColorProp;
        private MaterialProperty shadowOffsetProp;
        private MaterialProperty shadowSmoothNdotLProp;
        private MaterialProperty shadowSmoothSceneProp;
        private MaterialProperty shadowStrengthProp;

        private MaterialProperty enableShadowRampProp;
        private MaterialProperty shadowRampTexProp;

        private MaterialProperty selfEnvColorProp;
        private MaterialProperty envColorLerpProp;
        private MaterialProperty indirDiffUpDirSHProp;
        private MaterialProperty indirDiffIntensityProp;
        private MaterialProperty enableIndirCubemapProp;
        private MaterialProperty indirSpecCubemapProp;
        private MaterialProperty indirSpecCubeWeightProp;
        private MaterialProperty indirSpecIntensityProp;

        private MaterialProperty emissionColProp;
        private MaterialProperty directRimFrontColProp;
        private MaterialProperty directRimBackColProp;
        private MaterialProperty directRimWidthProp;
        private MaterialProperty punctualRimWidthProp;

        // Outline
        private MaterialProperty enableOutlineProp;
        private MaterialProperty outlineWidthProp;
        private MaterialProperty outlineColorProp;
        private MaterialProperty outlineDepthOldRangeProp;
        private MaterialProperty outlineDepthNewRangeProp;
        private MaterialProperty outlineNormalScaleProp;

        private MaterialProperty cullProp;
        private MaterialProperty alphaClipProp;
        private MaterialProperty cutoffProp;

        static class Styles
        {
            public static readonly GUIContent pbrPropsHeader = EditorGUIUtility.TrTextContent("PBR Properties");
            public static readonly GUIContent hairSpecHeader = EditorGUIUtility.TrTextContent("Hair Specular");
            public static readonly GUIContent directLightHeader = EditorGUIUtility.TrTextContent("Direct Light");
            public static readonly GUIContent indirectLightHeader = EditorGUIUtility.TrTextContent("Indirect Light");
            public static readonly GUIContent emissRimHeader = EditorGUIUtility.TrTextContent("Emission & Rim Light");
            public static readonly GUIContent outlineHeader = EditorGUIUtility.TrTextContent("Outline");
        }

        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);

            baseColorProp = FindProperty("_BaseColor", properties, false);
            baseMapPropLocal = FindProperty("_BaseMap", properties, false);
            pbrMaskProp = FindProperty("_PBRMask", properties, false);
            normalMapProp = FindProperty("_NormalMap", properties, false);
            normalScaleProp = FindProperty("_NormalScale", properties, false);

            metallicProp = FindProperty("_Metallic", properties, false);
            smoothnessProp = FindProperty("_Smoothness", properties, false);
            occlusionProp = FindProperty("_Occlusion", properties, false);

            hairSpecTexProp = FindProperty("_HairSpecTex", properties, false);
            specColorProp = FindProperty("_SpecColor", properties, false);
            anisotropicSlideProp = FindProperty("_AnisotropicSlide", properties, false);
            anisotropicOffsetProp = FindProperty("_AnisotropicOffset", properties, false);
            blinnPhongPowProp = FindProperty("_BlinnPhongPow", properties, false);
            specMinimumProp = FindProperty("_SpecMinimum", properties, false);

            selfLightProp = FindProperty("_SelfLight", properties, false);
            mainLightColorLerpProp = FindProperty("_MainLightColorLerp", properties, false);
            directOcclusionProp = FindProperty("_DirectOcclusion", properties, false);

            shadowColorProp = FindProperty("_ShadowColor", properties, false);
            shadowOffsetProp = FindProperty("_ShadowOffset", properties, false);
            shadowSmoothNdotLProp = FindProperty("_ShadowSmoothNdotL", properties, false);
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

            enableOutlineProp = FindProperty("_EnableOutline", properties, false);
            outlineWidthProp = FindProperty("_OutlineWidth", properties, false);
            outlineColorProp = FindProperty("_OutlineColor", properties, false);
            outlineDepthOldRangeProp = FindProperty("_OutlineDepthOldRange", properties, false);
            outlineDepthNewRangeProp = FindProperty("_OutlineDepthNewRange", properties, false);
            outlineNormalScaleProp = FindProperty("_OutlineNormalScale", properties, false);

            cullProp = FindProperty("_Cull", properties, false);
            alphaClipProp = FindProperty("_AlphaClip", properties, false);
            cutoffProp = FindProperty("_Cutoff", properties, false);
        }

        public override void DrawSurfaceOptions(Material material)
        {
            if (cullProp != null) materialEditor.ShaderProperty(cullProp, "Cull Mode");
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
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            if (baseColorProp != null) materialEditor.ShaderProperty(baseColorProp, "Base Color");
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
            materialScopesList.RegisterHeaderScope(Styles.hairSpecHeader, HairSpecFoldout, DrawHairSpec);
            materialScopesList.RegisterHeaderScope(Styles.directLightHeader, DirectLightFoldout, DrawDirectLight);
            materialScopesList.RegisterHeaderScope(Styles.indirectLightHeader, IndirectLightFoldout, DrawIndirectLight);
            materialScopesList.RegisterHeaderScope(Styles.emissRimHeader, EmissRimFoldout, DrawEmissRim);
            materialScopesList.RegisterHeaderScope(Styles.outlineHeader, OutlineFoldout, DrawOutline);
        }

        private void DrawPBRProps(Material material)
        {
            if (metallicProp != null) materialEditor.ShaderProperty(metallicProp, "Metallic");
            if (smoothnessProp != null) materialEditor.ShaderProperty(smoothnessProp, "Smoothness");
            if (occlusionProp != null) materialEditor.ShaderProperty(occlusionProp, "Occlusion");
        }

        private void DrawHairSpec(Material material)
        {
            if (hairSpecTexProp != null)
                materialEditor.TexturePropertySingleLine(EditorGUIUtility.TrTextContent("Hair Spec Texture"), hairSpecTexProp);
            if (specColorProp != null) materialEditor.ShaderProperty(specColorProp, "Spec Color");
            if (anisotropicSlideProp != null) materialEditor.ShaderProperty(anisotropicSlideProp, "Anisotropic Slide");
            if (anisotropicOffsetProp != null) materialEditor.ShaderProperty(anisotropicOffsetProp, "Anisotropic Offset");
            if (blinnPhongPowProp != null) materialEditor.ShaderProperty(blinnPhongPowProp, "Blinn Phong Power");
            if (specMinimumProp != null) materialEditor.ShaderProperty(specMinimumProp, "Spec Minimum");

            EditorGUILayout.HelpBox(
                "各向异性头发高光使用 UV1 采样。\n" +
                "HairSpecTex: 高光遮罩贴图\n" +
                "AnisotropicSlide: 视角偏移强度\n" +
                "AnisotropicOffset: 高光整体偏移",
                MessageType.Info);
        }

        private void DrawDirectLight(Material material)
        {
            if (selfLightProp != null) materialEditor.ShaderProperty(selfLightProp, "Self Light");
            if (mainLightColorLerpProp != null) materialEditor.ShaderProperty(mainLightColorLerpProp, "Unity Light ↔ Self Light");
            if (directOcclusionProp != null) materialEditor.ShaderProperty(directOcclusionProp, "Direct Occlusion");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Shadow", EditorStyles.boldLabel);
            if (shadowColorProp != null) materialEditor.ShaderProperty(shadowColorProp, "Shadow Color");
            if (shadowOffsetProp != null) materialEditor.ShaderProperty(shadowOffsetProp, "Shadow Offset");
            if (shadowSmoothNdotLProp != null) materialEditor.ShaderProperty(shadowSmoothNdotLProp, "Shadow Smooth NdotL");
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
            if (emissionColProp != null) materialEditor.ShaderProperty(emissionColProp, "Emission Color");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Rim Light", EditorStyles.boldLabel);
            if (directRimFrontColProp != null) materialEditor.ShaderProperty(directRimFrontColProp, "Front Rim Color");
            if (directRimBackColProp != null) materialEditor.ShaderProperty(directRimBackColProp, "Back Rim Color");
            if (directRimWidthProp != null) materialEditor.ShaderProperty(directRimWidthProp, "Direct Rim Width");
            if (punctualRimWidthProp != null) materialEditor.ShaderProperty(punctualRimWidthProp, "Punctual Rim Width");
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

        public override void DrawAdvancedOptions(Material material)
        {
            base.DrawAdvancedOptions(material);
        }

        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material);
        }

        private static new void SetMaterialKeywords(Material material)
        {
            if (material.HasProperty("_EnableShadowRamp"))
                CoreUtils.SetKeyword(material, "_SHADOW_RAMP", material.GetFloat("_EnableShadowRamp") > 0);
            if (material.HasProperty("_EnableIndirCubemap"))
                CoreUtils.SetKeyword(material, "_INDIR_CUBEMAP", material.GetFloat("_EnableIndirCubemap") > 0);
            if (material.HasProperty("_AlphaClip"))
                CoreUtils.SetKeyword(material, "_ALPHATEST_ON", material.GetFloat("_AlphaClip") > 0);
            if (material.HasProperty("_EnableOutline"))
                CoreUtils.SetKeyword(material, "_OUTLINE_ON", material.GetFloat("_EnableOutline") > 0);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (newShader != null)
                SetMaterialKeywords(material);
        }
    }
}
