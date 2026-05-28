using UnityEngine;
using UnityEditor;
using System.IO;

public class TLWaterGUI : ShaderGUI
{
    Gradient m_Gradient;
    bool     m_GradientLoaded;
    int      m_LutResolution = 256;

    bool m_FoldLUT     = true;
    bool m_FoldSpec    = true;
    bool m_FoldFresnel = true;
    bool m_FoldCaustic = true;
    bool m_FoldFoam    = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;

        if (!m_GradientLoaded)
        {
            m_Gradient       = LoadGradient(material);
            m_GradientLoaded = true;
        }

        // ==================== Water Depth Color ====================
        m_FoldLUT = EditorGUILayout.Foldout(m_FoldLUT, "Water Depth Color", true, EditorStyles.foldoutHeader);
        if (m_FoldLUT)
        {
            EditorGUI.indentLevel++;

            var lutProp = FindProperty("_WaterDepthLUT", properties);
            materialEditor.TexturePropertySingleLine(
                new GUIContent("Depth LUT", "深浅水颜色查找表，左侧=浅水 右侧=深水，Alpha通道控制该深度的水面透明度"),
                lutProp);

            EditorGUILayout.Space(4);
            GUILayout.Label("Gradient Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "左侧对应水面边缘(浅水)，右侧对应深水。\n" +
                "颜色条的 Alpha 通道控制对应深度的水面透明度。\n" +
                "编辑完成后点击 Bake LUT 生成贴图。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            m_Gradient = EditorGUILayout.GradientField(
                new GUIContent("深浅水渐变"), m_Gradient, true);
            if (EditorGUI.EndChangeCheck())
                SaveGradient(material, m_Gradient);

            m_LutResolution = EditorGUILayout.IntPopup("分辨率", m_LutResolution,
                new[] { "64", "128", "256" }, new[] { 64, 128, 256 });

            if (GUILayout.Button("Bake LUT", GUILayout.Height(28)))
            {
                BakeLUT(material, lutProp);
                SaveGradient(material, m_Gradient);
            }

            EditorGUILayout.Space(4);
            DrawProp(materialEditor, properties, "_WaterAlpha");
            DrawProp(materialEditor, properties, "_DepthIntensity");

            EditorGUI.indentLevel--;
        }

        // ==================== Toon Specular ====================
        m_FoldSpec = EditorGUILayout.Foldout(m_FoldSpec, "Toon Specular", true, EditorStyles.foldoutHeader);
        if (m_FoldSpec)
        {
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_CartoonSpecular");
            DrawProp(materialEditor, properties, "_ToonSpecMin");
            DrawProp(materialEditor, properties, "_ToonSpecMax");
            DrawProp(materialEditor, properties, "_ToonNoiseTex");
            DrawProp(materialEditor, properties, "_ToonNoiseSpeed");
            EditorGUI.indentLevel--;
        }

        // ==================== Fresnel ====================
        m_FoldFresnel = EditorGUILayout.Foldout(m_FoldFresnel, "Fresnel", true, EditorStyles.foldoutHeader);
        if (m_FoldFresnel)
        {
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_fresnelScale");
            DrawProp(materialEditor, properties, "_fresnelColor");
            EditorGUI.indentLevel--;
        }

        // ==================== Caustic ====================
        m_FoldCaustic = EditorGUILayout.Foldout(m_FoldCaustic, "Caustic", true, EditorStyles.foldoutHeader);
        if (m_FoldCaustic)
        {
            EditorGUI.indentLevel++;
            DrawProp(materialEditor, properties, "_CausticTex");
            DrawProp(materialEditor, properties, "_CausticIntensity");
            DrawProp(materialEditor, properties, "_CausticScale");
            DrawProp(materialEditor, properties, "_CausticFacade");
            DrawProp(materialEditor, properties, "_CausticSpeed");
            EditorGUI.indentLevel--;
        }

        // ==================== Foam ====================
        m_FoldFoam = EditorGUILayout.Foldout(m_FoldFoam, "Foam (SDF)", true, EditorStyles.foldoutHeader);
        if (m_FoldFoam)
        {
            EditorGUI.indentLevel++;

            var sdfProp = FindProperty("_FoamSDF", properties, false);
            if (sdfProp != null)
                materialEditor.TexturePropertySingleLine(new GUIContent("SDF贴图", "从 Window > TLWater SDF Baker 烘焙"), sdfProp);

            if (GUILayout.Button("Open SDF Baker", GUILayout.Height(24)))
                EditorWindow.GetWindow<TLWaterSDFBaker>("TLWater SDF Baker");

            EditorGUILayout.Space(4);
            DrawProp(materialEditor, properties, "_FoamTint");
            DrawProp(materialEditor, properties, "_FoamEdgeWidth");
            DrawProp(materialEditor, properties, "_FoamScope");
            DrawProp(materialEditor, properties, "_FoamInterval");
            DrawProp(materialEditor, properties, "_FoamAnimSpeed");
            DrawProp(materialEditor, properties, "_FoamNoiseTex");
            DrawProp(materialEditor, properties, "_FoamNoiseAmp");
            DrawProp(materialEditor, properties, "_FoamFadePower");
            DrawProp(materialEditor, properties, "_FoamNoiseSpeed");
            EditorGUI.indentLevel--;
        }
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name)
    {
        var prop = FindProperty(name, props, false);
        if (prop != null)
            editor.ShaderProperty(prop, prop.displayName);
    }

    // ----------------------------------------------------------------
    //  LUT Baking
    // ----------------------------------------------------------------

    void BakeLUT(Material material, MaterialProperty lutProp)
    {
        var tex = new Texture2D(m_LutResolution, 1, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var colors = new Color[m_LutResolution];
        for (int x = 0; x < m_LutResolution; x++)
            colors[x] = m_Gradient.Evaluate((float)x / (m_LutResolution - 1));
        tex.SetPixels(colors);
        tex.Apply();

        string matPath = AssetDatabase.GetAssetPath(material);
        string dir     = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath);
        string texPath = Path.Combine(dir, material.name + "_DepthLUT.asset").Replace("\\", "/");

        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(tex, existing);
            Object.DestroyImmediate(tex);
            tex = existing;
            EditorUtility.SetDirty(tex);
        }
        else
        {
            AssetDatabase.CreateAsset(tex, texPath);
        }

        AssetDatabase.SaveAssets();
        lutProp.textureValue = tex;
        Debug.Log("[TLWater] LUT saved: " + texPath);
    }

    // ----------------------------------------------------------------
    //  Gradient Serialization (EditorPrefs)
    // ----------------------------------------------------------------

    string GetGradientKey(Material mat)
    {
        string path = AssetDatabase.GetAssetPath(mat);
        if (string.IsNullOrEmpty(path))
            return "TLWater_" + mat.GetInstanceID();
        return "TLWater_Gradient_" + AssetDatabase.AssetPathToGUID(path);
    }

    void SaveGradient(Material mat, Gradient gradient)
    {
        var data = new GradientData();
        data.FromGradient(gradient);
        EditorPrefs.SetString(GetGradientKey(mat), JsonUtility.ToJson(data));
    }

    Gradient LoadGradient(Material mat)
    {
        string json = EditorPrefs.GetString(GetGradientKey(mat), "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonUtility.FromJson<GradientData>(json).ToGradient();
            }
            catch { /* fall through to default */ }
        }

        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.4f, 0.75f, 0.85f), 0f),
                new GradientColorKey(new Color(0.05f, 0.15f, 0.4f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(1f,   1f)
            });
        return g;
    }

    // ----------------------------------------------------------------
    //  Serializable Gradient
    // ----------------------------------------------------------------

    [System.Serializable]
    class GradientData
    {
        public float[] ct, cr, cg, cb;   // color keys: time, r, g, b
        public float[] at, av;            // alpha keys: time, alpha
        public int     mode;

        public void FromGradient(Gradient g)
        {
            var ck = g.colorKeys;
            ct = new float[ck.Length];
            cr = new float[ck.Length];
            cg = new float[ck.Length];
            cb = new float[ck.Length];
            for (int i = 0; i < ck.Length; i++)
            {
                ct[i] = ck[i].time;
                cr[i] = ck[i].color.r;
                cg[i] = ck[i].color.g;
                cb[i] = ck[i].color.b;
            }

            var ak = g.alphaKeys;
            at = new float[ak.Length];
            av = new float[ak.Length];
            for (int i = 0; i < ak.Length; i++)
            {
                at[i] = ak[i].time;
                av[i] = ak[i].alpha;
            }

            mode = (int)g.mode;
        }

        public Gradient ToGradient()
        {
            var ck = new GradientColorKey[ct.Length];
            for (int i = 0; i < ck.Length; i++)
                ck[i] = new GradientColorKey(new Color(cr[i], cg[i], cb[i]), ct[i]);

            var ak = new GradientAlphaKey[at.Length];
            for (int i = 0; i < ak.Length; i++)
                ak[i] = new GradientAlphaKey(av[i], at[i]);

            var g = new Gradient();
            g.SetKeys(ck, ak);
            g.mode = (GradientMode)mode;
            return g;
        }
    }
}
