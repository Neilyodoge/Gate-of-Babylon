using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectR.Effects
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("ProjectR/Effects/Local Volumetric Cloud")]
    public sealed class LocalVolumetricCloud : MonoBehaviour
    {
        public enum BoundaryMode
        {
            Box,
            MeshSdf,
            CloudBlob
        }

        private const string ShaderName = "ProjectR/Effects/Local Volumetric Cloud";

        [Header("渲染")]
        [SerializeField, InspectorName("材质")] private Material material;
        [Tooltip("Perlin-Worley 基础形状纹理；留空时自动生成。")]
        [FormerlySerializedAs("noiseTexture")]
        [SerializeField, InspectorName("基础形状纹理")] private Texture3D shapeNoiseTexture;
        [Tooltip("Worley 边缘细节纹理；留空时自动生成。")]
        [SerializeField, InspectorName("细节纹理")] private Texture3D detailNoiseTexture;
        [Tooltip("R 控制大范围覆盖，GB 储存 Curl 扰动；留空时自动生成。")]
        [SerializeField, InspectorName("天气图")] private Texture2D weatherMap;
        [Tooltip("自动生成基础形状纹理的边长。")]
        [SerializeField, Range(16, 128), InspectorName("生成噪声分辨率")] private int generatedNoiseResolution = 64;
        [SerializeField, Range(16, 64), InspectorName("生成细节分辨率")] private int generatedDetailResolution = 32;
        [SerializeField, Range(64, 512), InspectorName("生成天气图分辨率")] private int generatedWeatherResolution = 256;
        [SerializeField, InspectorName("生成噪声种子")] private int generatedNoiseSeed = 1337;

        [Header("体积边界")]
        [Tooltip("盒体适合标准 Cube；网格 SDF 可将密度限制在任意闭合静态网格内。")]
        [SerializeField, InspectorName("边界模式")] private BoundaryMode boundaryMode;
        [Tooltip("仅供编辑器烘焙 SDF 使用，不会在运行时参与渲染。")]
        [SerializeField, InspectorName("SDF 来源网格")] private Mesh sdfSourceMesh;
        [Tooltip("网格 SDF 每个轴的体素数量。")]
        [SerializeField, Range(16, 128), InspectorName("SDF 分辨率")] private int sdfResolution = 64;
        [Tooltip("由编辑器烘焙生成；运行时只读取该纹理。")]
        [SerializeField, InspectorName("网格 SDF 纹理")] private Texture3D meshSdfTexture;
        [Tooltip("网格表面向内的柔化宽度，以归一化包围盒为单位。")]
        [SerializeField, Range(0.001f, 0.25f), InspectorName("网格表面羽化")] private float meshSurfaceFade = 0.035f;
        [Tooltip("在保留来源网格大轮廓的前提下，向内外扰动 SDF 表面。")]
        [SerializeField, Range(0f, 0.15f), InspectorName("网格边缘变化")] private float meshEdgeVariation = 0.02f;
        [Tooltip("网格边缘起伏的疏密；较低值更能保留原始形体。")]
        [SerializeField, Range(0.2f, 4f), InspectorName("网格边缘尺度")] private float meshEdgeFrequency = 0.8f;
        [Tooltip("网格内部受云噪声影响的程度；越低越能保持完整形体。")]
        [SerializeField, Range(0f, 1f), InspectorName("网格内部变化")] private float meshInteriorVariation = 0.2f;
        [Tooltip("云团椭球轮廓被低频噪声推拉的幅度；零表示规则椭球。")]
        [SerializeField, Range(0f, 0.5f), InspectorName("云团边缘变化")] private float blobEdgeVariation = 0.18f;
        [Tooltip("云团轮廓起伏的疏密。较低值形成大块鼓包，较高值形成碎边。")]
        [SerializeField, Range(0.2f, 4f), InspectorName("云团边缘尺度")] private float blobEdgeFrequency = 0.65f;
        [Tooltip("云团轮廓由透明到实体的过渡宽度。")]
        [SerializeField, Range(0.005f, 0.3f), InspectorName("云团边缘羽化")] private float blobEdgeSoftness = 0.07f;
        [Tooltip("内部密度受云噪声影响的程度；零为连续填充，一为原有天空云分布。")]
        [SerializeField, Range(0f, 1f), InspectorName("云团内部变化")] private float blobInteriorVariation = 0.25f;

        [Header("形状")]
        [SerializeField, ColorUsage(true, true), InspectorName("云颜色")] private Color cloudColor = Color.white;
        [SerializeField, ColorUsage(true, true), InspectorName("阴影颜色")] private Color shadowColor = new(0.18f, 0.22f, 0.3f, 1f);
        [Tooltip("控制云块的覆盖量和空隙。零为空，一为使用完整的噪声分布。")]
        [SerializeField, Range(0f, 1f), InspectorName("云量")] private float cloudAmount = 1f;
        [Tooltip("控制已有云块的透光和厚重程度，不改变云块分布。")]
        [SerializeField, Range(0f, 8f), InspectorName("体积浓度")] private float density = 1.2f;
        [Tooltip("云量为一时使用的基础噪声阈值；提高会产生更碎、更少的云块。")]
        [SerializeField, Range(0f, 0.95f), InspectorName("形状阈值")] private float threshold = 0.48f;
        [SerializeField, Range(0.25f, 8f), InspectorName("噪声缩放")] private float noiseScale = 1f;
        [Tooltip("启用后，缩放代理盒只改变云的范围，不会拉伸云纹理。")]
        [SerializeField, InspectorName("使用世界空间采样")] private bool worldSpaceSampling;
        [Tooltip("世界空间中一整块基础 3D 噪声覆盖的尺寸。")]
        [SerializeField, Min(0.1f), InspectorName("基础噪声世界尺寸")] private float baseNoiseWorldSize = 60f;
        [Tooltip("细节纹理相对基础纹理的频率。")]
        [SerializeField, Range(1f, 16f), InspectorName("细节频率")] private float detailFrequency = 5f;
        [SerializeField, Range(0f, 1f), InspectorName("细节侵蚀强度")] private float detailStrength = 0.25f;
        [Tooltip("天气图控制大范围云量分布的强度。")]
        [SerializeField, Range(0f, 1f), InspectorName("天气图影响")] private float weatherInfluence = 0.45f;
        [SerializeField, Min(1f), InspectorName("天气图世界尺寸")] private float weatherWorldSize = 180f;
        [Tooltip("使用天气图中的 Curl 场扭曲细节纹理。")]
        [SerializeField, Range(0f, 2f), InspectorName("卷曲扰动")] private float curlStrength = 0.18f;
        [Tooltip("让云在代理盒边缘逐渐消散。")]
        [SerializeField, Range(0.001f, 0.5f), InspectorName("边缘羽化")] private float edgeFade = 0.12f;

        [Header("动画")]
        [Tooltip("使用世界空间采样时为世界方向，否则为代理盒局部方向。")]
        [SerializeField, InspectorName("风向")] private Vector3 windDirection = Vector3.right;
        [SerializeField, InspectorName("风速")] private float windSpeed = 0.01f;

        [Header("光照")]
        [Tooltip("云对光线的吸收强度。")]
        [SerializeField, Range(0.01f, 8f), InspectorName("消光强度")] private float extinction = 0.9f;
        [SerializeField, Range(0f, 1f), InspectorName("环境光强度")] private float ambient = 0.1f;
        [Tooltip("正值增强朝向主光源方向观察时的亮边。")]
        [SerializeField, Range(-0.8f, 0.8f), InspectorName("前向散射")] private float anisotropy = 0.3f;
        [Tooltip("让点光源和聚光灯参与云内散射。")]
        [SerializeField, InspectorName("启用局部光源")] private bool enableAdditionalLights = true;
        [Tooltip("局部光源对云的整体照明倍率。")]
        [SerializeField, Range(0f, 4f), InspectorName("局部光源强度")] private float additionalLightStrength = 1f;
        [Tooltip("单个云体积每个采样点最多计算的局部光源数量。")]
        [SerializeField, Range(0, 8), InspectorName("最大局部光源数")] private int maxAdditionalLights = 4;
        [Tooltip("单次精细步进的世界空间距离。越小越平滑，也越耗性能。")]
        [SerializeField, Min(0.02f), InspectorName("精细步进距离")] private float viewStepLength = 0.35f;
        [Tooltip("单条观察射线允许的最大采样次数。")]
        [SerializeField, Range(16, 256), InspectorName("最大视线步数")] private int viewSteps = 192;
        [Tooltip("空密度区域相对精细步进的倍率。")]
        [SerializeField, Range(1f, 8f), InspectorName("空区域步进倍率")] private float emptyStepMultiplier = 2f;
        [Tooltip("每个有效采样点向主光源采样的次数。")]
        [SerializeField, Range(1, 16), InspectorName("光照步进次数")] private int lightSteps = 4;
        [Tooltip("用随机偏移减轻步进分层；可能产生少量噪点。")]
        [SerializeField, Range(0f, 1f), InspectorName("步进抖动")] private float jitter = 0.1f;

        [Header("交互")]
        [Tooltip("在目标周围清除云密度，例如玩家或飞行物。")]
        [SerializeField, InspectorName("交互目标")] private Transform interactionTarget;
        [SerializeField, Min(0f), InspectorName("交互半径")] private float interactionRadius = 1.5f;
        [SerializeField, Min(0f), InspectorName("交互羽化")] private float interactionSoftness = 0.5f;
        [SerializeField, Range(0f, 1f), InspectorName("交互强度")] private float interactionAmount = 1f;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Material runtimeMaterial;
        private Material previousMaterial;
        private Texture3D generatedShapeNoise;
        private Texture3D generatedDetailNoise;
        private Texture2D generatedWeatherMap;
        private int activeShapeResolution = -1;
        private int activeDetailResolution = -1;
        private int activeWeatherResolution = -1;
        private int activeNoiseSeed;

        private static readonly int ShapeNoiseTextureId = Shader.PropertyToID("_ShapeNoiseTex");
        private static readonly int DetailNoiseTextureId = Shader.PropertyToID("_DetailNoiseTex");
        private static readonly int WeatherMapId = Shader.PropertyToID("_WeatherMap");
        private static readonly int MeshSdfTextureId = Shader.PropertyToID("_MeshSDFTex");
        private static readonly int UseMeshSdfId = Shader.PropertyToID("_UseMeshSDF");
        private static readonly int MeshSurfaceFadeId = Shader.PropertyToID("_MeshSurfaceFade");
        private static readonly int MeshEdgeVariationId = Shader.PropertyToID("_MeshEdgeVariation");
        private static readonly int MeshEdgeFrequencyId = Shader.PropertyToID("_MeshEdgeFrequency");
        private static readonly int MeshInteriorVariationId = Shader.PropertyToID("_MeshInteriorVariation");
        private static readonly int UseCloudBlobId = Shader.PropertyToID("_UseCloudBlob");
        private static readonly int BlobEdgeVariationId = Shader.PropertyToID("_BlobEdgeVariation");
        private static readonly int BlobEdgeFrequencyId = Shader.PropertyToID("_BlobEdgeFrequency");
        private static readonly int BlobEdgeSoftnessId = Shader.PropertyToID("_BlobEdgeSoftness");
        private static readonly int BlobInteriorVariationId = Shader.PropertyToID("_BlobInteriorVariation");
        private static readonly int NoiseResolutionId = Shader.PropertyToID("_NoiseResolution");
        private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
        private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int CloudAmountId = Shader.PropertyToID("_CloudAmount");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int WorldSpaceSamplingId = Shader.PropertyToID("_WorldSpaceSampling");
        private static readonly int BaseNoiseWorldSizeId = Shader.PropertyToID("_BaseNoiseWorldSize");
        private static readonly int DetailFrequencyId = Shader.PropertyToID("_DetailFrequency");
        private static readonly int DetailStrengthId = Shader.PropertyToID("_DetailStrength");
        private static readonly int WeatherInfluenceId = Shader.PropertyToID("_WeatherInfluence");
        private static readonly int WeatherWorldSizeId = Shader.PropertyToID("_WeatherWorldSize");
        private static readonly int CurlStrengthId = Shader.PropertyToID("_CurlStrength");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
        private static readonly int WindDirectionId = Shader.PropertyToID("_WindDirection");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int ExtinctionId = Shader.PropertyToID("_Extinction");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int AnisotropyId = Shader.PropertyToID("_Anisotropy");
        private static readonly int EnableAdditionalLightsId = Shader.PropertyToID("_EnableAdditionalLights");
        private static readonly int AdditionalLightStrengthId = Shader.PropertyToID("_AdditionalLightStrength");
        private static readonly int MaxAdditionalLightsId = Shader.PropertyToID("_MaxAdditionalLights");
        private static readonly int ViewStepLengthId = Shader.PropertyToID("_ViewStepLength");
        private static readonly int StepCountId = Shader.PropertyToID("_StepCount");
        private static readonly int EmptyStepMultiplierId = Shader.PropertyToID("_EmptyStepMultiplier");
        private static readonly int LightStepCountId = Shader.PropertyToID("_LightStepCount");
        private static readonly int JitterId = Shader.PropertyToID("_Jitter");
        private static readonly int InteractionCenterId = Shader.PropertyToID("_InteractionCenter");
        private static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
        private static readonly int InteractionSoftnessId = Shader.PropertyToID("_InteractionSoftness");
        private static readonly int InteractionAmountId = Shader.PropertyToID("_InteractionAmount");

        private void OnEnable()
        {
            EnsureRendererAndMaterial();
            EnsureNoise();
            ApplyProperties();
        }

        private void OnValidate()
        {
            generatedNoiseResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(generatedNoiseResolution, 16, 128));
            generatedDetailResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(generatedDetailResolution, 16, 64));
            generatedWeatherResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(generatedWeatherResolution, 64, 512));
            sdfResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(sdfResolution, 16, 128));
            EnsureRendererAndMaterial();
            EnsureNoise();
            ApplyProperties();
        }

        private void LateUpdate()
        {
            ApplyProperties();
        }

        private void OnDisable()
        {
            if (cachedRenderer != null && runtimeMaterial != null && cachedRenderer.sharedMaterial == runtimeMaterial)
            {
                cachedRenderer.sharedMaterial = previousMaterial;
            }

            DestroyOwnedObject(runtimeMaterial);
            DestroyOwnedObject(generatedShapeNoise);
            DestroyOwnedObject(generatedDetailNoise);
            DestroyOwnedObject(generatedWeatherMap);
            runtimeMaterial = null;
            generatedShapeNoise = null;
            generatedDetailNoise = null;
            generatedWeatherMap = null;
            activeShapeResolution = -1;
            activeDetailResolution = -1;
            activeWeatherResolution = -1;
        }

        [ContextMenu("重新生成噪声")]
        public void RegenerateNoise()
        {
            DestroyOwnedObject(generatedShapeNoise);
            DestroyOwnedObject(generatedDetailNoise);
            DestroyOwnedObject(generatedWeatherMap);
            generatedShapeNoise = null;
            generatedDetailNoise = null;
            generatedWeatherMap = null;
            activeShapeResolution = -1;
            activeDetailResolution = -1;
            activeWeatherResolution = -1;
            EnsureNoise();
            ApplyProperties();
        }

        public void SetInteraction(Transform target, float radius, float amount = 1f)
        {
            interactionTarget = target;
            interactionRadius = Mathf.Max(0f, radius);
            interactionAmount = Mathf.Clamp01(amount);
            ApplyProperties();
        }

        private void EnsureRendererAndMaterial()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }
            if (cachedRenderer == null)
            {
                return;
            }

            Material targetMaterial = material != null ? material : cachedRenderer.sharedMaterial;
            if (targetMaterial != null && targetMaterial.shader != null && targetMaterial.shader.name == ShaderName)
            {
                if (cachedRenderer.sharedMaterial != targetMaterial)
                {
                    cachedRenderer.sharedMaterial = targetMaterial;
                }
                return;
            }

            if (runtimeMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return;
            }

            previousMaterial = cachedRenderer.sharedMaterial;
            runtimeMaterial = new Material(shader)
            {
                name = "Local Volumetric Cloud (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            cachedRenderer.sharedMaterial = runtimeMaterial;
        }

        private void EnsureNoise()
        {
            bool configurationMatches =
                activeShapeResolution == generatedNoiseResolution &&
                activeDetailResolution == generatedDetailResolution &&
                activeWeatherResolution == generatedWeatherResolution &&
                activeNoiseSeed == generatedNoiseSeed;
            bool allTexturesReady =
                (shapeNoiseTexture != null || generatedShapeNoise != null) &&
                (detailNoiseTexture != null || generatedDetailNoise != null) &&
                (weatherMap != null || generatedWeatherMap != null);
            if (configurationMatches && allTexturesReady)
            {
                return;
            }

            if (shapeNoiseTexture == null)
            {
                DestroyOwnedObject(generatedShapeNoise);
                generatedShapeNoise = GenerateShapeNoise(generatedNoiseResolution, generatedNoiseSeed);
            }
            if (detailNoiseTexture == null)
            {
                DestroyOwnedObject(generatedDetailNoise);
                generatedDetailNoise = GenerateDetailNoise(generatedDetailResolution, generatedNoiseSeed + 101);
            }
            if (weatherMap == null)
            {
                DestroyOwnedObject(generatedWeatherMap);
                generatedWeatherMap = GenerateWeatherMap(generatedWeatherResolution, generatedNoiseSeed + 211);
            }

            activeShapeResolution = generatedNoiseResolution;
            activeDetailResolution = generatedDetailResolution;
            activeWeatherResolution = generatedWeatherResolution;
            activeNoiseSeed = generatedNoiseSeed;
        }

        private void ApplyProperties()
        {
            if (cachedRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(propertyBlock);
            Texture3D activeShapeNoise = shapeNoiseTexture != null ? shapeNoiseTexture : generatedShapeNoise;
            Texture3D activeDetailNoise = detailNoiseTexture != null ? detailNoiseTexture : generatedDetailNoise;
            Texture2D activeWeatherMap = weatherMap != null ? weatherMap : generatedWeatherMap;
            propertyBlock.SetTexture(ShapeNoiseTextureId, activeShapeNoise);
            propertyBlock.SetTexture(DetailNoiseTextureId, activeDetailNoise);
            propertyBlock.SetTexture(WeatherMapId, activeWeatherMap);
            if (meshSdfTexture != null)
            {
                propertyBlock.SetTexture(MeshSdfTextureId, meshSdfTexture);
            }
            propertyBlock.SetFloat(
                UseMeshSdfId,
                boundaryMode == BoundaryMode.MeshSdf && meshSdfTexture != null ? 1f : 0f);
            propertyBlock.SetFloat(MeshSurfaceFadeId, meshSurfaceFade);
            propertyBlock.SetFloat(MeshEdgeVariationId, meshEdgeVariation);
            propertyBlock.SetFloat(MeshEdgeFrequencyId, meshEdgeFrequency);
            propertyBlock.SetFloat(MeshInteriorVariationId, meshInteriorVariation);
            propertyBlock.SetFloat(
                UseCloudBlobId,
                boundaryMode == BoundaryMode.CloudBlob ? 1f : 0f);
            propertyBlock.SetFloat(BlobEdgeVariationId, blobEdgeVariation);
            propertyBlock.SetFloat(BlobEdgeFrequencyId, blobEdgeFrequency);
            propertyBlock.SetFloat(BlobEdgeSoftnessId, blobEdgeSoftness);
            propertyBlock.SetFloat(BlobInteriorVariationId, blobInteriorVariation);
            propertyBlock.SetVector(NoiseResolutionId, new Vector4(
                activeShapeNoise != null ? activeShapeNoise.width : 1,
                activeDetailNoise != null ? activeDetailNoise.width : 1,
                activeWeatherMap != null ? activeWeatherMap.width : 1,
                0f));
            propertyBlock.SetColor(CloudColorId, cloudColor);
            propertyBlock.SetColor(ShadowColorId, shadowColor);
            propertyBlock.SetFloat(CloudAmountId, cloudAmount);
            propertyBlock.SetFloat(DensityId, density);
            propertyBlock.SetFloat(ThresholdId, threshold);
            propertyBlock.SetFloat(NoiseScaleId, noiseScale);
            propertyBlock.SetFloat(WorldSpaceSamplingId, worldSpaceSampling ? 1f : 0f);
            propertyBlock.SetFloat(BaseNoiseWorldSizeId, baseNoiseWorldSize);
            propertyBlock.SetFloat(DetailFrequencyId, detailFrequency);
            propertyBlock.SetFloat(DetailStrengthId, detailStrength);
            propertyBlock.SetFloat(WeatherInfluenceId, weatherInfluence);
            propertyBlock.SetFloat(WeatherWorldSizeId, weatherWorldSize);
            propertyBlock.SetFloat(CurlStrengthId, curlStrength);
            propertyBlock.SetFloat(EdgeFadeId, edgeFade);
            propertyBlock.SetVector(WindDirectionId, windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector3.zero);
            propertyBlock.SetFloat(WindSpeedId, windSpeed);
            propertyBlock.SetFloat(ExtinctionId, extinction);
            propertyBlock.SetFloat(AmbientId, ambient);
            propertyBlock.SetFloat(AnisotropyId, anisotropy);
            propertyBlock.SetFloat(EnableAdditionalLightsId, enableAdditionalLights ? 1f : 0f);
            propertyBlock.SetFloat(AdditionalLightStrengthId, additionalLightStrength);
            propertyBlock.SetInt(MaxAdditionalLightsId, maxAdditionalLights);
            propertyBlock.SetFloat(ViewStepLengthId, viewStepLength);
            propertyBlock.SetInt(StepCountId, viewSteps);
            propertyBlock.SetFloat(EmptyStepMultiplierId, emptyStepMultiplier);
            propertyBlock.SetInt(LightStepCountId, lightSteps);
            propertyBlock.SetFloat(JitterId, jitter);

            bool interactionEnabled = interactionTarget != null && interactionRadius > 0f && interactionAmount > 0f;
            propertyBlock.SetVector(InteractionCenterId, interactionEnabled ? interactionTarget.position : Vector3.zero);
            propertyBlock.SetFloat(InteractionRadiusId, interactionEnabled ? interactionRadius : 0f);
            propertyBlock.SetFloat(InteractionSoftnessId, Mathf.Min(interactionSoftness, interactionRadius));
            propertyBlock.SetFloat(InteractionAmountId, interactionEnabled ? interactionAmount : 0f);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        private static Texture3D GenerateShapeNoise(int resolution, int seed)
        {
            var pixels = new Color32[resolution * resolution * resolution];
            int index = 0;

            for (int z = 0; z < resolution; z++)
            {
                float nz = z / (float)resolution;
                for (int y = 0; y < resolution; y++)
                {
                    float ny = y / (float)resolution;
                    for (int x = 0; x < resolution; x++)
                    {
                        float nx = x / (float)resolution;
                        float perlin =
                            TileablePerlinNoise(nx * 3f, ny * 3f, nz * 3f, 3, seed) * 0.625f +
                            TileablePerlinNoise(nx * 6f, ny * 6f, nz * 6f, 6, seed + 17) * 0.25f +
                            TileablePerlinNoise(nx * 12f, ny * 12f, nz * 12f, 12, seed + 31) * 0.125f;
                        float worleyLow = 1f - TileableWorleyNoise(nx * 4f, ny * 4f, nz * 4f, 4, seed + 47);
                        float worleyMid = 1f - TileableWorleyNoise(nx * 8f, ny * 8f, nz * 8f, 8, seed + 71);
                        float worleyHigh = 1f - TileableWorleyNoise(nx * 16f, ny * 16f, nz * 16f, 16, seed + 97);
                        float worleyFbm = worleyLow * 0.625f + worleyMid * 0.25f + worleyHigh * 0.125f;
                        float perlinWorley = RemapClamped(perlin, 1f - worleyFbm, 1f);

                        pixels[index++] = new Color(
                            perlinWorley,
                            worleyLow,
                            worleyMid,
                            worleyHigh);
                    }
                }
            }

            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, true)
            {
                name = $"Generated Perlin-Worley Shape {resolution} ({seed})",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture3D GenerateDetailNoise(int resolution, int seed)
        {
            var pixels = new Color32[resolution * resolution * resolution];
            int index = 0;

            for (int z = 0; z < resolution; z++)
            {
                float nz = z / (float)resolution;
                for (int y = 0; y < resolution; y++)
                {
                    float ny = y / (float)resolution;
                    for (int x = 0; x < resolution; x++)
                    {
                        float nx = x / (float)resolution;
                        float low = 1f - TileableWorleyNoise(nx * 4f, ny * 4f, nz * 4f, 4, seed);
                        float mid = 1f - TileableWorleyNoise(nx * 8f, ny * 8f, nz * 8f, 8, seed + 23);
                        float high = 1f - TileableWorleyNoise(nx * 16f, ny * 16f, nz * 16f, 16, seed + 53);
                        pixels[index++] = new Color(low, mid, high, 1f);
                    }
                }
            }

            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, true)
            {
                name = $"Generated Worley Detail {resolution} ({seed})",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D GenerateWeatherMap(int resolution, int seed)
        {
            var pixels = new Color32[resolution * resolution];
            float epsilon = 0.025f;
            int index = 0;

            for (int y = 0; y < resolution; y++)
            {
                float ny = y / (float)resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)resolution;
                    float coverage =
                        TileableValueNoise2D(nx * 3f, ny * 3f, 3, seed) * 0.6f +
                        TileableValueNoise2D(nx * 6f, ny * 6f, 6, seed + 19) * 0.3f +
                        TileableValueNoise2D(nx * 12f, ny * 12f, 12, seed + 41) * 0.1f;

                    float curlSourceX0 = TileableValueNoise2D((nx - epsilon) * 4f, ny * 4f, 4, seed + 73);
                    float curlSourceX1 = TileableValueNoise2D((nx + epsilon) * 4f, ny * 4f, 4, seed + 73);
                    float curlSourceY0 = TileableValueNoise2D(nx * 4f, (ny - epsilon) * 4f, 4, seed + 73);
                    float curlSourceY1 = TileableValueNoise2D(nx * 4f, (ny + epsilon) * 4f, 4, seed + 73);
                    float curlX = Mathf.Clamp((curlSourceY1 - curlSourceY0) * 2f, -1f, 1f);
                    float curlY = Mathf.Clamp(-(curlSourceX1 - curlSourceX0) * 2f, -1f, 1f);
                    float variation = TileableValueNoise2D(nx * 5f, ny * 5f, 5, seed + 109);

                    pixels[index++] = new Color(
                        Mathf.SmoothStep(0.15f, 0.85f, coverage),
                        curlX * 0.5f + 0.5f,
                        curlY * 0.5f + 0.5f,
                        variation);
                }
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = $"Generated Cloud Weather {resolution} ({seed})",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float TileablePerlinNoise(float x, float y, float z, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int z0 = Mathf.FloorToInt(z);
            float fx = x - Mathf.Floor(x);
            float fy = y - Mathf.Floor(y);
            float fz = z - Mathf.Floor(z);
            float tx = Fade(fx);
            float ty = Fade(fy);
            float tz = Fade(fz);

            float n000 = GradientDot(x0, y0, z0, fx, fy, fz, period, seed);
            float n100 = GradientDot(x0 + 1, y0, z0, fx - 1f, fy, fz, period, seed);
            float n010 = GradientDot(x0, y0 + 1, z0, fx, fy - 1f, fz, period, seed);
            float n110 = GradientDot(x0 + 1, y0 + 1, z0, fx - 1f, fy - 1f, fz, period, seed);
            float n001 = GradientDot(x0, y0, z0 + 1, fx, fy, fz - 1f, period, seed);
            float n101 = GradientDot(x0 + 1, y0, z0 + 1, fx - 1f, fy, fz - 1f, period, seed);
            float n011 = GradientDot(x0, y0 + 1, z0 + 1, fx, fy - 1f, fz - 1f, period, seed);
            float n111 = GradientDot(x0 + 1, y0 + 1, z0 + 1, fx - 1f, fy - 1f, fz - 1f, period, seed);

            float lower = Mathf.Lerp(Mathf.Lerp(n000, n100, tx), Mathf.Lerp(n010, n110, tx), ty);
            float upper = Mathf.Lerp(Mathf.Lerp(n001, n101, tx), Mathf.Lerp(n011, n111, tx), ty);
            return Mathf.Clamp01(Mathf.Lerp(lower, upper, tz) * 0.7f + 0.5f);
        }

        private static float GradientDot(int x, int y, int z, float dx, float dy, float dz, int period, int seed)
        {
            x = PositiveModulo(x, period);
            y = PositiveModulo(y, period);
            z = PositiveModulo(z, period);
            uint hash = Hash(x, y, z, seed);
            switch (hash % 12u)
            {
                case 0: return dx + dy;
                case 1: return -dx + dy;
                case 2: return dx - dy;
                case 3: return -dx - dy;
                case 4: return dx + dz;
                case 5: return -dx + dz;
                case 6: return dx - dz;
                case 7: return -dx - dz;
                case 8: return dy + dz;
                case 9: return -dy + dz;
                case 10: return dy - dz;
                default: return -dy - dz;
            }
        }

        private static float TileableWorleyNoise(float x, float y, float z, int period, int seed)
        {
            int cellX = Mathf.FloorToInt(x);
            int cellY = Mathf.FloorToInt(y);
            int cellZ = Mathf.FloorToInt(z);
            float minimumDistanceSquared = float.MaxValue;

            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int candidateX = cellX + offsetX;
                        int candidateY = cellY + offsetY;
                        int candidateZ = cellZ + offsetZ;
                        int wrappedX = PositiveModulo(candidateX, period);
                        int wrappedY = PositiveModulo(candidateY, period);
                        int wrappedZ = PositiveModulo(candidateZ, period);
                        float featureX = candidateX + Hash01(wrappedX, wrappedY, wrappedZ, seed);
                        float featureY = candidateY + Hash01(wrappedX, wrappedY, wrappedZ, seed + 1);
                        float featureZ = candidateZ + Hash01(wrappedX, wrappedY, wrappedZ, seed + 2);
                        float dx = featureX - x;
                        float dy = featureY - y;
                        float dz = featureZ - z;
                        minimumDistanceSquared = Mathf.Min(minimumDistanceSquared, dx * dx + dy * dy + dz * dz);
                    }
                }
            }

            return Mathf.Clamp01(Mathf.Sqrt(minimumDistanceSquared));
        }

        private static float TileableValueNoise2D(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = Smooth(Mathf.Repeat(x, 1f));
            float ty = Smooth(Mathf.Repeat(y, 1f));
            int x1 = PositiveModulo(x0 + 1, period);
            int y1 = PositiveModulo(y0 + 1, period);
            x0 = PositiveModulo(x0, period);
            y0 = PositiveModulo(y0, period);

            float lower = Mathf.Lerp(Hash01(x0, y0, 0, seed), Hash01(x1, y0, 0, seed), tx);
            float upper = Mathf.Lerp(Hash01(x0, y1, 0, seed), Hash01(x1, y1, 0, seed), tx);
            return Mathf.Lerp(lower, upper, ty);
        }

        private static float RemapClamped(float value, float sourceMinimum, float sourceMaximum)
        {
            return Mathf.Clamp01((value - sourceMinimum) / Mathf.Max(sourceMaximum - sourceMinimum, 0.0001f));
        }

        private static float TileableValueNoise(float x, float y, float z, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int z0 = Mathf.FloorToInt(z);
            float tx = Smooth(Mathf.Repeat(x, 1f));
            float ty = Smooth(Mathf.Repeat(y, 1f));
            float tz = Smooth(Mathf.Repeat(z, 1f));

            int x1 = x0 + 1;
            int y1 = y0 + 1;
            int z1 = z0 + 1;
            x0 = PositiveModulo(x0, period);
            y0 = PositiveModulo(y0, period);
            z0 = PositiveModulo(z0, period);
            x1 = PositiveModulo(x1, period);
            y1 = PositiveModulo(y1, period);
            z1 = PositiveModulo(z1, period);

            float c000 = Hash01(x0, y0, z0, seed);
            float c100 = Hash01(x1, y0, z0, seed);
            float c010 = Hash01(x0, y1, z0, seed);
            float c110 = Hash01(x1, y1, z0, seed);
            float c001 = Hash01(x0, y0, z1, seed);
            float c101 = Hash01(x1, y0, z1, seed);
            float c011 = Hash01(x0, y1, z1, seed);
            float c111 = Hash01(x1, y1, z1, seed);

            float lower = Mathf.Lerp(Mathf.Lerp(c000, c100, tx), Mathf.Lerp(c010, c110, tx), ty);
            float upper = Mathf.Lerp(Mathf.Lerp(c001, c101, tx), Mathf.Lerp(c011, c111, tx), ty);
            return Mathf.Lerp(lower, upper, tz);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Fade(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static float Hash01(int x, int y, int z, int seed)
        {
            return (Hash(x, y, z, seed) & 0x00FFFFFFu) / 16777215f;
        }

        private static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)x * 374761393u;
                hash ^= (uint)y * 668265263u;
                hash ^= (uint)z * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return hash;
            }
        }

        private static void DestroyOwnedObject(Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
