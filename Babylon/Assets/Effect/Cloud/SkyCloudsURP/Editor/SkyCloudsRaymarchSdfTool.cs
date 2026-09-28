using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectR.Effects.Editor
{
    internal static class SkyCloudsRaymarchSdfTool
    {
        private const int DefaultSdfResolution = 64;
        private const string ShaderPath =
            "Assets/Effect/Cloud/SkyCloudsURP/Runtime/Shaders/SkyCloudsRaymarch.shader";
        private const string TemplateMaterialPath =
            "Assets/Effect/Cloud/SkyCloudsURP/Runtime/Materials/SkyCloudsRaymarchPreview.mat";
        private const string GeneratedRoot =
            "Assets/Effect/Cloud/Generated/SkyCloudsRaymarch";

        [MenuItem("ProjectR/体积云/从选中网格创建 SDF Raymarch 代理")]
        private static void CreateFromSelection()
        {
            GameObject source = Selection.activeGameObject;
            MeshFilter sourceFilter = source != null
                ? source.GetComponent<MeshFilter>()
                : null;
            Mesh sourceMesh = sourceFilter != null
                ? sourceFilter.sharedMesh
                : null;
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "无法创建 SDF 体积云",
                    "请选择带有 MeshFilter 和闭合网格的场景对象。",
                    "确定");
                return;
            }

            string safeMeshName = MakeSafeFileName(sourceMesh.name);
            string safeObjectName = MakeSafeFileName(source.name);
            string sdfPath =
                $"{GeneratedRoot}/SDF/{safeMeshName}_SDF_{DefaultSdfResolution}.asset";
            string materialPath =
                $"{GeneratedRoot}/Materials/{safeObjectName}_RaymarchSDF.mat";

            try
            {
                EditorUtility.DisplayProgressBar(
                    "SDF Raymarch 体积云",
                    $"正在烘焙 {sourceMesh.name}…",
                    0.4f);
                MeshSdfBaker.BakeResult bakeResult =
                    MeshSdfBaker.BakeToAsset(
                        sourceMesh,
                        DefaultSdfResolution,
                        sdfPath,
                        0.1f);

                EditorUtility.DisplayProgressBar(
                    "SDF Raymarch 体积云",
                    "正在创建代理与材质…",
                    0.85f);
                Material material =
                    CreateOrUpdateMaterial(
                        bakeResult.Texture,
                        materialPath);
                GameObject proxy =
                    CreateOrUpdateProxy(
                        source,
                        bakeResult.PaddedBounds,
                        material);

                Renderer sourceRenderer =
                    source.GetComponent<Renderer>();
                if (sourceRenderer != null)
                {
                    Undo.RecordObject(
                        sourceRenderer,
                        "隐藏 SDF 来源网格");
                    sourceRenderer.enabled = false;
                    EditorUtility.SetDirty(sourceRenderer);
                }

                AssetDatabase.SaveAssets();
                Selection.activeGameObject = proxy;
                EditorGUIUtility.PingObject(
                    bakeResult.Texture);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, source);
                EditorUtility.DisplayDialog(
                    "SDF Raymarch 创建失败",
                    $"{exception.Message}\n\n来源应为闭合三角网格。",
                    "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(
            "ProjectR/体积云/从选中网格创建 SDF Raymarch 代理",
            true)]
        private static bool ValidateCreateFromSelection()
        {
            GameObject source = Selection.activeGameObject;
            MeshFilter filter = source != null
                ? source.GetComponent<MeshFilter>()
                : null;
            return filter != null && filter.sharedMesh != null;
        }

        private static Material CreateOrUpdateMaterial(
            Texture3D sdfTexture,
            string outputPath)
        {
            Shader shader =
                AssetDatabase.LoadAssetAtPath<Shader>(
                    ShaderPath);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"找不到 Raymarch Shader：{ShaderPath}");
            }

            EnsureFolder(
                Path.GetDirectoryName(outputPath)?
                    .Replace('\\', '/'));
            Material template =
                AssetDatabase.LoadAssetAtPath<Material>(
                    TemplateMaterialPath);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    outputPath);
            if (material == null)
            {
                material = new Material(shader);
                if (template != null)
                {
                    material.CopyPropertiesFromMaterial(
                        template);
                }
                AssetDatabase.CreateAsset(
                    material,
                    outputPath);
            }
            else
            {
                material.shader = shader;
                if (template != null)
                {
                    material.CopyPropertiesFromMaterial(
                        template);
                }
            }

            material.SetFloat("_UseMeshSDF", 1f);
            material.SetTexture(
                "_MeshSDFTex",
                sdfTexture);
            material.SetFloat("_SdfSurfaceFade", 0.018f);
            material.SetFloat("_SdfEdgeVariation", 0.012f);
            material.SetFloat("_SdfEdgeFrequency", 1.2f);
            material.SetFloat("_CloudAmount", 0.92f);
            material.SetFloat("_InteriorVariation", 0.18f);
            material.SetFloat("_Density", 1.65f);
            material.SetFloat("_Extinction", 1.55f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrUpdateProxy(
            GameObject source,
            Bounds paddedBounds,
            Material material)
        {
            string proxyName = $"{source.name}_SDF云代理";
            Transform existing =
                source.transform.Find(proxyName);
            GameObject proxy;
            if (existing != null)
            {
                proxy = existing.gameObject;
                Undo.RecordObject(
                    proxy.transform,
                    "更新 SDF 云代理");
            }
            else
            {
                proxy =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);
                proxy.name = proxyName;
                Undo.RegisterCreatedObjectUndo(
                    proxy,
                    "创建 SDF 云代理");
                Undo.SetTransformParent(
                    proxy.transform,
                    source.transform,
                    "挂接 SDF 云代理");
                Collider collider =
                    proxy.GetComponent<Collider>();
                if (collider != null)
                {
                    Undo.DestroyObjectImmediate(collider);
                }
            }

            proxy.transform.localPosition =
                paddedBounds.center;
            proxy.transform.localRotation =
                Quaternion.identity;
            proxy.transform.localScale =
                paddedBounds.size;

            MeshRenderer proxyRenderer =
                proxy.GetComponent<MeshRenderer>();
            Undo.RecordObject(
                proxyRenderer,
                "设置 SDF 云材质");
            proxyRenderer.sharedMaterial = material;
            proxyRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            EditorUtility.SetDirty(proxy);
            return proxy;
        }

        private static string MakeSafeFileName(string value)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();
            return new string(
                value.Select(character =>
                    invalid.Contains(character)
                        ? '_'
                        : character)
                    .ToArray());
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }
                current = next;
            }
        }
    }
}
