using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ProjectR.Effects.Editor
{
    [CustomEditor(typeof(LocalVolumetricCloud))]
    [CanEditMultipleObjects]
    public sealed class LocalVolumetricCloudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                Draw("m_Script", "脚本");
            }

            DrawSection("渲染");
            Draw("material", "材质", "可选；留空时组件会自动创建临时材质。");
            Draw("shapeNoiseTexture", "基础形状纹理", "Perlin-Worley 基础形状；留空时自动生成。");
            Draw("detailNoiseTexture", "细节纹理", "Worley 边缘细节；留空时自动生成。");
            Draw("weatherMap", "天气图", "R 控制大范围覆盖，GB 储存 Curl 扰动；留空时自动生成。");
            Draw("generatedNoiseResolution", "生成噪声分辨率", "自动生成基础形状纹理的边长。");
            Draw("generatedDetailResolution", "生成细节分辨率");
            Draw("generatedWeatherResolution", "生成天气图分辨率");
            Draw("generatedNoiseSeed", "生成噪声种子");

            DrawSection("体积边界");
            SerializedProperty boundaryMode = serializedObject.FindProperty("boundaryMode");
            DrawBoundaryMode(boundaryMode);
            if (boundaryMode != null)
            {
                if (boundaryMode.enumValueIndex == (int)LocalVolumetricCloud.BoundaryMode.MeshSdf)
                {
                    Draw("sdfSourceMesh", "SDF 来源网格", "只在编辑器烘焙时读取。");
                    Draw("sdfResolution", "SDF 分辨率", "提高可改善细薄部位，但会增加纹理体积和烘焙时间。");
                    Draw("meshSdfTexture", "网格 SDF 纹理");
                    Draw("meshSurfaceFade", "网格表面羽化", "控制密度从网格表面向内部渐入的宽度。");
                    Draw("meshEdgeVariation", "边缘变化", "只扰动 SDF 表面附近；零会完整保持来源网格轮廓。");
                    Draw("meshEdgeFrequency", "边缘尺度", "较低值为大块缓慢起伏，较高值产生更密的轮廓变化。");
                    Draw("meshInteriorVariation", "内部变化", "越低越完整保留形体；提高后内部云噪声和空隙更多。");
                    DrawSdfTools();
                }
                else if (boundaryMode.enumValueIndex ==
                         (int)LocalVolumetricCloud.BoundaryMode.CloudBlob)
                {
                    Draw("blobEdgeVariation", "边缘变化", "轮廓被低频噪声推拉的幅度；零为规则椭球。");
                    Draw("blobEdgeFrequency", "边缘尺度", "较低值形成大块鼓包，较高值形成细碎起伏。");
                    Draw("blobEdgeSoftness", "边缘羽化", "轮廓由透明到实体的过渡宽度。");
                    Draw("blobInteriorVariation", "内部变化", "零为连续填充；提高后内部会出现更多云噪声空隙。");
                    DrawCloudBlobTools();
                }
            }

            DrawSection("形状");
            Draw("cloudColor", "云颜色");
            Draw("shadowColor", "阴影颜色");
            Draw("cloudAmount", "云量", "直接控制云块的疏密和空隙；零为空，一为完整噪声分布。");
            Draw("density", "体积浓度", "只控制已有云块的透光和厚重程度，不改变云块分布，也不再随代理尺寸迅速饱和。");
            Draw("threshold", "云块阈值（高级）", "云量为一时的基础噪声阈值；通常先调整“云量”。");
            Draw("noiseScale", "局部噪声缩放", "仅在局部空间采样模式下使用。");
            Draw("worldSpaceSampling", "使用世界空间采样", "缩放代理盒只改变云的范围，不会拉伸云纹理；移动代理盒时云纹理保持锁定在世界坐标。");
            Draw("baseNoiseWorldSize", "基础噪声世界尺寸", "仅在世界空间采样模式下使用；应尽量大于单个云体积的主要尺寸。");
            Draw("detailFrequency", "细节频率");
            Draw("detailStrength", "细节侵蚀强度");
            Draw("weatherInfluence", "天气图影响", "控制天气图对大型云量分布的影响。");
            Draw("weatherWorldSize", "天气图世界尺寸", "世界空间下天气图完整平铺一次所覆盖的尺寸。");
            Draw("curlStrength", "卷曲扰动", "使用天气图中的 Curl 场扭曲细节纹理，不改变基础轮廓。");
            Draw("edgeFade", "边缘羽化", "让云在代理盒边缘逐渐消散。");

            DrawSection("动画");
            Draw("windDirection", "风向", "使用世界空间采样时为世界方向，否则为代理盒局部方向。");
            Draw("windSpeed", "风速");

            DrawSection("光照");
            Draw("extinction", "消光强度", "云对光线的吸收强度。");
            Draw("ambient", "环境光强度");
            Draw("anisotropy", "前向散射", "正值增强朝向主光源方向观察时的亮边。");
            Draw("enableAdditionalLights", "启用局部光源", "让 URP 点光源和聚光灯参与云内散射。");
            Draw("additionalLightStrength", "局部光源强度", "点光源和聚光灯对云的整体照明倍率。");
            Draw("maxAdditionalLights", "最大局部光源数", "每个有效采样点最多计算的局部光源数量；提高会线性增加开销。");
            Draw("viewStepLength", "精细步进距离", "云内单次前进的世界空间距离；越小越平滑，也越耗性能。");
            Draw("viewSteps", "最大视线步数", "限制单条射线的最坏采样成本。");
            Draw("emptyStepMultiplier", "空区域步进倍率", "密度为零时加速穿过；过高可能漏掉很薄的云。");
            Draw("lightSteps", "光照步进次数", "每个有效采样点向主光源采样的次数。");
            Draw("jitter", "步进抖动", "用随机偏移减轻步进分层；可能产生少量噪点。");

            DrawSection("交互");
            Draw("interactionTarget", "交互目标", "在目标周围清除云密度，例如玩家或飞行物。");
            Draw("interactionRadius", "交互半径");
            Draw("interactionSoftness", "交互羽化");
            Draw("interactionAmount", "交互强度");

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawBoundaryMode(SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            string[] labels = { "盒体", "网格 SDF", "云团" };
            property.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent("边界模式", "云团用于独立团块；网格 SDF 用于严格贴合闭合静态网格。"),
                property.enumValueIndex,
                labels);
        }

        private void DrawCloudBlobTools()
        {
            if (targets.Length != 1)
            {
                return;
            }

            var cloud = (LocalVolumetricCloud)target;
            var meshFilter = cloud.GetComponent<MeshFilter>();
            bool alreadyCube = meshFilter != null &&
                               meshFilter.sharedMesh != null &&
                               meshFilter.sharedMesh.name == "Cube";
            if (!alreadyCube && meshFilter != null && meshFilter.sharedMesh != null)
            {
                if (GUILayout.Button("转换为云团盒代理"))
                {
                    serializedObject.ApplyModifiedProperties();
                    ConvertToCloudBlobProxy(cloud);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.HelpBox(
                "云团模式以标准 Cube 为代理，缩放 Cube 即可得到球形或椭球形云团；边缘变化只扰动外轮廓，不会像天空云那样挖空整个内部。",
                MessageType.None);
        }

        private void DrawSdfTools()
        {
            EditorGUILayout.Space(3f);
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("SDF 烘焙工具一次只能处理一个对象。", MessageType.Info);
                return;
            }

            var cloud = (LocalVolumetricCloud)target;
            var meshFilter = cloud.GetComponent<MeshFilter>();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("烘焙 / 更新 SDF"))
                {
                    serializedObject.ApplyModifiedProperties();
                    BakeSdf(cloud, false);
                    serializedObject.Update();
                }

                using (new EditorGUI.DisabledScope(meshFilter == null || meshFilter.sharedMesh == null))
                {
                    if (GUILayout.Button("转换为 SDF 盒代理"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        BakeSdf(cloud, true);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "SDF 模式应由标准 Cube 代理执行 Ray March。若当前组件挂在造型网格上，使用“转换为 SDF 盒代理”；来源 Renderer 会关闭，网格资产不会被修改。",
                MessageType.None);
        }

        private static void BakeSdf(LocalVolumetricCloud cloud, bool convertToProxy)
        {
            var cloudObject = new SerializedObject(cloud);
            SerializedProperty sourceProperty = cloudObject.FindProperty("sdfSourceMesh");
            Mesh source = sourceProperty.objectReferenceValue as Mesh;
            var sourceFilter = cloud.GetComponent<MeshFilter>();
            if (source == null && sourceFilter != null)
            {
                source = sourceFilter.sharedMesh;
                sourceProperty.objectReferenceValue = source;
                cloudObject.ApplyModifiedPropertiesWithoutUndo();
            }

            if (source == null)
            {
                EditorUtility.DisplayDialog("无法烘焙 SDF", "请先指定“SDF 来源网格”。", "确定");
                return;
            }

            int resolution = cloudObject.FindProperty("sdfResolution").intValue;
            string safeName = MakeSafeFileName(source.name);
            string outputPath = $"Assets/Effect/Cloud/Generated/{safeName}_SDF_{resolution}.asset";

            try
            {
                EditorUtility.DisplayProgressBar("网格 SDF", $"正在烘焙 {source.name}…", 0.5f);
                MeshSdfBaker.BakeResult result =
                    MeshSdfBaker.BakeToAsset(source, resolution, outputPath);

                if (convertToProxy)
                {
                    ConvertToProxy(cloud, source, result);
                }
                else
                {
                    Undo.RecordObject(cloud, "烘焙网格 SDF");
                    cloudObject.Update();
                    cloudObject.FindProperty("boundaryMode").enumValueIndex =
                        (int)LocalVolumetricCloud.BoundaryMode.MeshSdf;
                    cloudObject.FindProperty("meshSdfTexture").objectReferenceValue = result.Texture;
                    cloudObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(cloud);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, cloud);
                EditorUtility.DisplayDialog(
                    "SDF 烘焙失败",
                    $"{exception.Message}\n\n来源网格必须允许编辑器读取，并且应为闭合三角网格。",
                    "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ConvertToProxy(
            LocalVolumetricCloud sourceCloud,
            Mesh sourceMesh,
            MeshSdfBaker.BakeResult result)
        {
            Transform sourceTransform = sourceCloud.transform;
            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.name = $"{sourceTransform.name}_SDF体积云";
            Undo.RegisterCreatedObjectUndo(proxy, "创建 SDF 体积云代理");
            proxy.transform.SetParent(sourceTransform, false);
            proxy.transform.localPosition = result.PaddedBounds.center;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = result.PaddedBounds.size;

            Collider proxyCollider = proxy.GetComponent<Collider>();
            if (proxyCollider != null)
            {
                Undo.DestroyObjectImmediate(proxyCollider);
            }

            ComponentUtility.CopyComponent(sourceCloud);
            ComponentUtility.PasteComponentAsNew(proxy);
            var proxyCloud = proxy.GetComponent<LocalVolumetricCloud>();
            var proxyObject = new SerializedObject(proxyCloud);
            proxyObject.FindProperty("boundaryMode").enumValueIndex =
                (int)LocalVolumetricCloud.BoundaryMode.MeshSdf;
            proxyObject.FindProperty("sdfSourceMesh").objectReferenceValue = sourceMesh;
            proxyObject.FindProperty("meshSdfTexture").objectReferenceValue = result.Texture;
            proxyObject.ApplyModifiedPropertiesWithoutUndo();

            Renderer sourceRenderer = sourceCloud.GetComponent<Renderer>();
            Undo.DestroyObjectImmediate(sourceCloud);
            if (sourceRenderer != null)
            {
                Undo.RecordObject(sourceRenderer, "关闭 SDF 来源网格渲染");
                sourceRenderer.enabled = false;
                EditorUtility.SetDirty(sourceRenderer);
            }

            Selection.activeGameObject = proxy;
            EditorGUIUtility.PingObject(result.Texture);
        }

        private static void ConvertToCloudBlobProxy(LocalVolumetricCloud sourceCloud)
        {
            var sourceFilter = sourceCloud.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return;
            }

            Bounds sourceBounds = sourceFilter.sharedMesh.bounds;
            Vector3 proxySize = sourceBounds.size * 1.05f;
            proxySize.x = Mathf.Max(proxySize.x, 0.0001f);
            proxySize.y = Mathf.Max(proxySize.y, 0.0001f);
            proxySize.z = Mathf.Max(proxySize.z, 0.0001f);

            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.name = $"{sourceCloud.name}_云团代理";
            Undo.RegisterCreatedObjectUndo(proxy, "创建局部云团代理");
            proxy.transform.SetParent(sourceCloud.transform, false);
            proxy.transform.localPosition = sourceBounds.center;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = proxySize;

            Collider proxyCollider = proxy.GetComponent<Collider>();
            if (proxyCollider != null)
            {
                Undo.DestroyObjectImmediate(proxyCollider);
            }

            ComponentUtility.CopyComponent(sourceCloud);
            ComponentUtility.PasteComponentAsNew(proxy);
            var proxyCloud = proxy.GetComponent<LocalVolumetricCloud>();
            var proxyObject = new SerializedObject(proxyCloud);
            proxyObject.FindProperty("boundaryMode").enumValueIndex =
                (int)LocalVolumetricCloud.BoundaryMode.CloudBlob;
            proxyObject.FindProperty("meshSdfTexture").objectReferenceValue = null;
            proxyObject.ApplyModifiedPropertiesWithoutUndo();

            Renderer sourceRenderer = sourceCloud.GetComponent<Renderer>();
            Undo.DestroyObjectImmediate(sourceCloud);
            if (sourceRenderer != null)
            {
                Undo.RecordObject(sourceRenderer, "关闭云团来源网格渲染");
                sourceRenderer.enabled = false;
                EditorUtility.SetDirty(sourceRenderer);
            }

            Selection.activeGameObject = proxy;
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return string.IsNullOrWhiteSpace(value) ? "Mesh" : value;
        }

        private void DrawSection(string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Draw(string propertyName, string label, string tooltip = null)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
            }
        }
    }
}
