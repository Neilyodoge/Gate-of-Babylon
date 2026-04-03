using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通用贴图调试 Shader 的自定义 ShaderGUI
/// 提供分组按钮，方便切换调试模式
/// </summary>
public class TextureDebugShaderGUI : ShaderGUI
{
    // ====== Debug 模式枚举（与 Shader 中 Enum 顺序一致） ======
    private enum DebugMode
    {
        Tex_RGB = 0, Tex_R = 1, Tex_G = 2, Tex_B = 3, Tex_A = 4,
        VertexColor_RGB = 5, VertexColor_R = 6, VertexColor_G = 7, VertexColor_B = 8, VertexColor_A = 9,
        NormalMap = 10, MeshNormal = 11, SmoothNormal_UV3 = 12,
        UV0 = 13, UV1 = 14,
    }

    // 分组名称
    private static readonly string[] groupNames = new string[]
    {
        "贴图通道",
        "顶点色 (VertexColor)",
        "法线",
        "UV 坐标",
    };

    // 各分组的模式列表
    private static readonly DebugMode[][] groupModes = new DebugMode[][]
    {
        new DebugMode[] { DebugMode.Tex_RGB, DebugMode.Tex_R, DebugMode.Tex_G, DebugMode.Tex_B, DebugMode.Tex_A },
        new DebugMode[] { DebugMode.VertexColor_RGB, DebugMode.VertexColor_R, DebugMode.VertexColor_G, DebugMode.VertexColor_B, DebugMode.VertexColor_A },
        new DebugMode[] { DebugMode.NormalMap, DebugMode.MeshNormal, DebugMode.SmoothNormal_UV3 },
        new DebugMode[] { DebugMode.UV0, DebugMode.UV1 },
    };

    // 各模式的显示名称
    private static readonly string[] modeDisplayNames = new string[]
    {
        "Tex RGB", "Tex R", "Tex G", "Tex B", "Tex A",
        "VertexColor RGB", "VertexColor R", "VertexColor G", "VertexColor B", "VertexColor A",
        "NormalMap (切线空间→世界空间)", "Mesh Normal (模型原始法线)", "Smooth Normal UV3 (平滑法线)",
        "UV0 坐标", "UV1 坐标",
    };

    // 折叠状态
    private bool foldTexture = true;
    private bool foldSettings = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty debugModeProp   = FindProperty("_DebugMode", properties, false);
        MaterialProperty texProp         = FindProperty("_Tex", properties, false);
        MaterialProperty normalScaleProp = FindProperty("_NormalScale", properties, false);
        MaterialProperty cullProp        = FindProperty("_Cull", properties, false);
        MaterialProperty gammaCorrectProp = FindProperty("_GammaCorrect", properties, false);

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "通用贴图调试 Shader\n" +
            "以 Unlit 形式单独查看给定贴图的各通道、顶点色、法线等数据\n" +
            "将需要查看的贴图拖到 Texture 槽位即可，NormalMap 模式也复用同一张贴图",
            MessageType.Info);
        EditorGUILayout.Space(5);

        // ====== 纹理区域 ======
        foldTexture = EditorGUILayout.BeginFoldoutHeaderGroup(foldTexture, "纹理");
        if (foldTexture)
        {
            EditorGUI.indentLevel++;
            if (texProp != null)
                materialEditor.TextureProperty(texProp, "Texture", false);

            EditorGUILayout.Space(3);
            if (normalScaleProp != null)
                materialEditor.ShaderProperty(normalScaleProp, "Normal Scale (仅 NormalMap 模式)");

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // ====== Debug 模式选择 ======
        if (debugModeProp != null)
        {
            EditorGUILayout.LabelField("调试模式", EditorStyles.boldLabel);

            int currentMode = (int)debugModeProp.floatValue;

            // 绘制分组按钮
            for (int g = 0; g < groupNames.Length; g++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(groupNames[g], EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                for (int m = 0; m < groupModes[g].Length; m++)
                {
                    int modeIndex = (int)groupModes[g][m];
                    bool isSelected = (currentMode == modeIndex);

                    string btnLabel = GetShortName(groupModes[g][m]);

                    GUIStyle style = new GUIStyle(isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton);
                    if (isSelected)
                    {
                        style.normal.textColor = Color.cyan;
                        style.fontStyle = FontStyle.Bold;
                    }

                    if (GUILayout.Button(btnLabel, style))
                    {
                        debugModeProp.floatValue = modeIndex;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(3);
            if (currentMode >= 0 && currentMode < modeDisplayNames.Length)
            {
                EditorGUILayout.HelpBox("当前: " + modeDisplayNames[currentMode], MessageType.None);
            }
        }

        EditorGUILayout.Space(10);

        // ====== 设置区域 ======
        foldSettings = EditorGUILayout.BeginFoldoutHeaderGroup(foldSettings, "设置");
        if (foldSettings)
        {
            EditorGUI.indentLevel++;
            if (cullProp != null)
                materialEditor.ShaderProperty(cullProp, "Cull Mode");
            if (gammaCorrectProp != null)
                materialEditor.ShaderProperty(gammaCorrectProp, "Gamma 矫正 (线性数据可视化)");

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "提示：查看线性数据时，可开启 Gamma 矫正使暗部更清晰。",
                MessageType.None);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>
    /// 获取按钮上的简短显示名
    /// </summary>
    private string GetShortName(DebugMode mode)
    {
        switch (mode)
        {
            case DebugMode.Tex_RGB:  return "RGB";
            case DebugMode.Tex_R:    return "R";
            case DebugMode.Tex_G:    return "G";
            case DebugMode.Tex_B:    return "B";
            case DebugMode.Tex_A:    return "A";

            case DebugMode.VertexColor_RGB: return "RGB";
            case DebugMode.VertexColor_R:   return "R";
            case DebugMode.VertexColor_G:   return "G";
            case DebugMode.VertexColor_B:   return "B";
            case DebugMode.VertexColor_A:   return "A";

            case DebugMode.NormalMap:        return "法线贴图";
            case DebugMode.MeshNormal:       return "原始法线";
            case DebugMode.SmoothNormal_UV3: return "平滑法线(UV3)";

            case DebugMode.UV0: return "UV0";
            case DebugMode.UV1: return "UV1";

            default: return mode.ToString();
        }
    }
}
