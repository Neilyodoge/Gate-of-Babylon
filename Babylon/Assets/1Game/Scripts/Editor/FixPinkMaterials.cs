using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace XianTu.Editor
{
    /// <summary>
    /// 材质修复工具 —— 将所有使用 Built-in Shader 的材质转换为 URP Shader
    /// 菜单：Tools/修复粉色材质 (Built-in → URP)
    /// </summary>
    public static class FixPinkMaterials
    {
        [MenuItem("Tools/修复粉色材质 (Built-in → URP)")]
        public static void Fix()
        {
            // 查找 URP Shader
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            var urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var urpParticlesLit = Shader.Find("Universal Render Pipeline/Particles/Lit");

            // 目标 Shader（优先 Lit，fallback Simple Lit）
            var targetLit = urpLit != null ? urpLit : urpSimpleLit;
            var targetParticle = urpParticlesUnlit != null ? urpParticlesUnlit : urpUnlit;

            if (targetLit == null)
            {
                Debug.LogError("[FixPinkMaterials] 找不到 URP Lit Shader！请确认项目使用了 URP。");
                return;
            }

            // 搜索所有材质
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/1Game" });
            int fixedCount = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // 检查 Shader 是否丢失或是 Built-in
                bool needsFix = false;
                string shaderName = mat.shader != null ? mat.shader.name : "null";

                if (mat.shader == null || shaderName == "Hidden/InternalErrorShader")
                {
                    needsFix = true;
                }
                else if (shaderName.StartsWith("Standard") ||
                         shaderName.StartsWith("Legacy Shaders/") ||
                         shaderName.StartsWith("Mobile/") ||
                         shaderName == "Diffuse" ||
                         shaderName == "Specular")
                {
                    needsFix = true;
                }

                if (!needsFix) continue;

                // 保存原始属性
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
                Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                Texture emissionTex = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;

                // 判断是否是粒子材质
                bool isParticle = shaderName.Contains("Particle") || shaderName.Contains("Additive");

                // 替换 Shader
                Shader newShader;
                if (isParticle)
                    newShader = targetParticle;
                else
                    newShader = targetLit;

                Undo.RecordObject(mat, "Fix Pink Material");
                mat.shader = newShader;

                // 恢复属性
                if (mainTex != null)
                {
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", mainTex);
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", mainTex);
                }

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);

                if (hasEmission)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", emissionColor);
                    if (emissionTex != null && mat.HasProperty("_EmissionMap"))
                        mat.SetTexture("_EmissionMap", emissionTex);
                }

                EditorUtility.SetDirty(mat);
                fixedCount++;
                Debug.Log($"[FixPinkMaterials] 已修复: {path} ({shaderName} → {newShader.name})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fixedCount > 0)
                Debug.Log($"<color=green>[FixPinkMaterials] 共修复 {fixedCount} 个材质！</color>");
            else
                Debug.Log("[FixPinkMaterials] 没有找到需要修复的材质。");
        }
    }
}
