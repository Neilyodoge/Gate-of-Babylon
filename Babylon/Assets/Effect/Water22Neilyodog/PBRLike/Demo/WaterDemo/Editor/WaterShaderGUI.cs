using UnityEditor;
using UnityEngine;

public sealed class WaterShaderGUI : ShaderGUI
{
    bool _foldGeneral   = true;
    bool _foldVertex    = true;
    bool _foldNormal    = true;
    bool _foldFresnel   = true;
    bool _foldShadow    = true;
    bool _foldSparkle   = true;
    bool _foldHighlight = true;
    bool _foldDistort   = true;
    bool _foldCaustic   = true;
    bool _foldFoam      = true;

    static readonly Color k_DimColor = new Color(0.55f, 0.55f, 0.55f);

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        // ── General ──
        _foldGeneral = DrawFoldout("General / 基础", _foldGeneral);
        if (_foldGeneral)
        {
            editor.ShaderProperty(FindProp("_SHIntensity", props), "环境光比例");
            editor.ShaderProperty(FindProp("_waveSpeed", props), "Wave 速度");
            editor.ShaderProperty(FindProp("_WaveA", props), "WaveA (xy方向 z强度 w tiling)");
            editor.ShaderProperty(FindProp("_WaveB", props), "WaveB");

            EditorGUILayout.Space(4f);
            editor.ShaderProperty(FindProp("_UseBlend", props), "开启混色");
            editor.ShaderProperty(FindProp("_NoTiling", props), "NoTiling");
            editor.ShaderProperty(FindProp("_WaterAlpha", props), "水整体透明度");
            editor.ShaderProperty(FindProp("_WaterSideColor", props), "边缘深度颜色");
            editor.ShaderProperty(FindProp("_WaterColor", props), "水颜色");
            editor.ShaderProperty(FindProp("_WaterDepthWSColor", props), "深水颜色");
            editor.ShaderProperty(FindProp("_DepthForCol", props), "深水颜色范围");
        }

        // ── Custom Light Direction ──
        DrawCustomLightSection(editor, props);

        // ── Vertex Anim ──
        _foldVertex = DrawFoldout("Vertex Anim / 顶点动画", _foldVertex);
        if (_foldVertex)
        {
            DrawTexWithST(editor, FindProp("_VertexAnim", props), "顶点动画贴图");
            editor.ShaderProperty(FindProp("_VertexAnimSpeed", props), "顶点动画速度");
            editor.ShaderProperty(FindProp("_VertexIntensity", props), "顶点动画强度");
        }

        // ── Normal ──
        _foldNormal = DrawFoldout("Normal / 法线", _foldNormal);
        if (_foldNormal)
        {
            editor.ShaderProperty(FindProp("_flatNormal", props), "法线平整距离");
            DrawTexWithST(editor, FindProp("_BumpTex", props), "Normal");
            editor.ShaderProperty(FindProp("_WaterBumpScale", props), "法线强度");
            editor.ShaderProperty(FindProp("_NormalSpeed", props), "法线速度 (xy:N  zw:Detail)");
            DrawTexWithST(editor, FindProp("_DetailBumpTex", props), "Detail Normal");
            editor.ShaderProperty(FindProp("_DetailBumpScale", props), "Detail法线强度");
        }

        // ── Fresnel ──
        _foldFresnel = DrawFoldout("Fresnel / 菲尼尔", _foldFresnel);
        if (_foldFresnel)
        {
            editor.ShaderProperty(FindProp("_fresnelScale", props), "菲尼尔范围");
            editor.ShaderProperty(FindProp("_fresnelColor", props), "菲尼尔颜色");
        }

        // ── Shadow ──
        _foldShadow = DrawFoldout("Shadow / 阴影", _foldShadow);
        if (_foldShadow)
        {
            editor.ShaderProperty(FindProp("_ShadowColor", props), "阴影颜色 (A=阴影内DF强度)");
        }

        // ── Sparkle ──
        _foldSparkle = DrawFoldout("Sparkle / 闪烁", _foldSparkle);
        if (_foldSparkle)
        {
            editor.ShaderProperty(FindProp("_SparkleTint", props), "闪烁颜色");
            DrawTexWithST(editor, FindProp("_SparkleTex", props), "闪烁贴图");
            editor.ShaderProperty(FindProp("_SparkleIntensity", props), "闪烁亮度");
            editor.ShaderProperty(FindProp("_SparkleSpeed", props), "闪烁速度");
        }

        // ── HighLight ──
        _foldHighlight = DrawFoldout("HighLight / 高光", _foldHighlight);
        if (_foldHighlight)
        {
            editor.ShaderProperty(FindProp("_CartoonSpecular", props), "Toon高光颜色");
            editor.ShaderProperty(FindProp("_SpecularColor", props), "高光颜色");
            editor.ShaderProperty(FindProp("_Specular", props), "高光强度");
            editor.ShaderProperty(FindProp("_HeightScale", props), "高光范围");
        }

        // ── Distortion ──
        _foldDistort = DrawFoldout("Distortion / 扭曲", _foldDistort);
        if (_foldDistort)
        {
            DrawTexWithST(editor, FindProp("_DistortionTex", props), "扭曲贴图");
            editor.ShaderProperty(FindProp("_DistortionIntensity", props), "扭曲强度");
            editor.ShaderProperty(FindProp("_DistortionSpeed", props), "扭曲速度");
        }

        // ── Caustic ──
        _foldCaustic = DrawFoldout("Caustic / 焦散", _foldCaustic);
        if (_foldCaustic)
        {
            DrawTexWithST(editor, FindProp("_CausticTex", props), "焦散贴图");
            editor.ShaderProperty(FindProp("_CausticIntensity", props), "焦散强度");
            editor.ShaderProperty(FindProp("_CausticScale", props), "焦散范围");
            editor.ShaderProperty(FindProp("_CausticFacade", props), "焦散立面");
        }

        // ── Foam & WaterSide ──
        _foldFoam = DrawFoldout("Foam & WaterSide / 泡沫 & 岸边", _foldFoam);
        if (_foldFoam)
        {
            editor.ShaderProperty(FindProp("_UseWaterSide", props), "使用 WaterSide");
            editor.ShaderProperty(FindProp("_FoamSpeed", props), "泡沫速度 (Y水边/ZW焦散)");
            DrawTexWithST(editor, FindProp("_FoamTex", props), "泡沫纹理");
            editor.ShaderProperty(FindProp("_FoamTint", props), "泡沫颜色");
            editor.ShaderProperty(FindProp("_FoamRange", props), "泡沫范围");
            editor.ShaderProperty(FindProp("_DepthIntensity", props), "深度强度");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("WaterSide", EditorStyles.boldLabel);
            editor.ShaderProperty(FindProp("_WaterSideTint", props), "岸边潮湿颜色");
            editor.ShaderProperty(FindProp("_FoamSide", props), "水边范围");
            editor.ShaderProperty(FindProp("_DampSide", props), "潮湿范围");
            editor.ShaderProperty(FindProp("_FoamHeight", props), "水边高度修正值");
        }

        EditorGUILayout.Space(8f);
        editor.RenderQueueField();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Custom Light Direction
    // ─────────────────────────────────────────────────────────────────────────

    void DrawCustomLightSection(MaterialEditor editor, MaterialProperty[] props)
    {
        var toggleProp    = FindProp("_UseCustomLightDir", props);
        var dirProp       = FindProp("_TLEnvLightDir", props);
        var colorProp     = FindProp("_CustomLightColor", props);
        var intensityProp = FindProp("_CustomLightIntensity", props);

        bool isOn = toggleProp.floatValue > 0.5f;

        EditorGUILayout.Space(6f);
        var lineRect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(lineRect, new Color(0f, 0f, 0f, isOn ? 0.25f : 0.12f));
        EditorGUILayout.Space(2f);

        var titleStyle = new GUIStyle(EditorStyles.boldLabel);
        if (!isOn) titleStyle.normal.textColor = k_DimColor;
        EditorGUILayout.LabelField("Custom Light Direction / 自定义灯光", titleStyle);

        editor.ShaderProperty(toggleProp, "自定义灯光方向");

        if (!isOn) return;

        EditorGUI.indentLevel++;

        Vector4 dirV4    = dirProp.vectorValue;
        Vector3 lightFwd = -new Vector3(dirV4.x, dirV4.y, dirV4.z);
        if (lightFwd.sqrMagnitude < 1e-6f) lightFwd = Vector3.down;
        Vector3 euler = Quaternion.LookRotation(lightFwd.normalized).eulerAngles;
        float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        float yaw   = euler.y;

        EditorGUI.BeginChangeCheck();
        float newPitch = EditorGUILayout.Slider(
            new GUIContent("Pitch (高度角)", "0=平射, 90=顶光, -90=仰光"),
            pitch, -90f, 90f);
        float newYaw = EditorGUILayout.Slider(
            new GUIContent("Yaw (水平角)", "光照水平朝向"),
            yaw, 0f, 360f);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 newFwd = Quaternion.Euler(newPitch, newYaw, 0f) * Vector3.forward;
            Vector3 newDir = -newFwd.normalized;
            dirProp.vectorValue = new Vector4(newDir.x, newDir.y, newDir.z, 0f);
        }

        editor.ShaderProperty(colorProp, "灯光颜色");
        editor.ShaderProperty(intensityProp, "灯光强度");

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("获取场景主光方向",
            "把场景中第一个 enabled 的 Directional Light 的方向写入材质")))
        {
            PickFromSceneLight(editor, dirProp);
        }

        if (GUILayout.Button(new GUIContent("同步到选中材质",
            "把当前方向应用到 Project 视图中选中的其它同 shader 材质")))
        {
            ApplyToSelected(editor, dirProp);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  工具按钮
    // ─────────────────────────────────────────────────────────────────────────

    static void PickFromSceneLight(MaterialEditor editor, MaterialProperty dirProp)
    {
        Light picked = null;
        foreach (var l in Object.FindObjectsOfType<Light>(includeInactive: false))
        {
            if (l.type == LightType.Directional && l.enabled)
            {
                picked = l;
                break;
            }
        }
        if (picked == null)
        {
            Debug.LogWarning("[WaterShaderGUI] 场景中未找到 enabled 的 Directional Light");
            return;
        }

        Vector3 dir   = -picked.transform.forward.normalized;
        Vector4 dirV4 = new Vector4(dir.x, dir.y, dir.z, 0f);

        foreach (var t in editor.targets)
        {
            var m = (Material)t;
            Undo.RecordObject(m, "Water: Pick Light Dir From Scene");
            m.SetVector("_TLEnvLightDir", dirV4);
            EditorUtility.SetDirty(m);
        }
        Debug.Log($"[WaterShaderGUI] 从 \"{picked.name}\" 拾取方向 = ({dir.x:F3}, {dir.y:F3}, {dir.z:F3})");
    }

    static void ApplyToSelected(MaterialEditor editor, MaterialProperty dirProp)
    {
        var src = (Material)editor.target;
        Vector4 dir = src.GetVector("_TLEnvLightDir");

        int count = 0;
        foreach (var obj in Selection.objects)
        {
            if (obj is Material m && m != src && m.shader == src.shader)
            {
                Undo.RecordObject(m, "Water: Apply Light Dir To Selected");
                m.SetVector("_TLEnvLightDir", dir);
                EditorUtility.SetDirty(m);
                count++;
            }
        }
        Debug.Log(count > 0
            ? $"[WaterShaderGUI] 方向已应用到 {count} 个材质"
            : "[WaterShaderGUI] Project 视图中没有选中其它同 shader 材质");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    static MaterialProperty FindProp(string name, MaterialProperty[] props)
    {
        return FindProperty(name, props, false);
    }

    static void DrawTexWithST(MaterialEditor editor, MaterialProperty texProp, string label)
    {
        if (texProp == null) return;
        editor.TexturePropertySingleLine(new GUIContent(label), texProp);
        editor.TextureScaleOffsetProperty(texProp);
    }

    static bool DrawFoldout(string title, bool current)
    {
        EditorGUILayout.Space(4f);
        var lineRect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(lineRect, new Color(0f, 0f, 0f, 0.2f));
        return EditorGUILayout.Foldout(current, title, true, EditorStyles.foldoutHeader);
    }
}
