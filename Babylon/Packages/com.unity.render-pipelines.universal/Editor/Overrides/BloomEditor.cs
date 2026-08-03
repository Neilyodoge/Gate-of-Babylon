using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(Bloom))]
    sealed class BloomEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_BloomMode;
        SerializedDataParameter m_Threshold;
        SerializedDataParameter m_Intensity;
        SerializedDataParameter m_Scatter;
        SerializedDataParameter m_Clamp;
        SerializedDataParameter m_Tint;
        SerializedDataParameter m_HighQualityFiltering;
        SerializedDataParameter m_ThresholdKnee;
        SerializedDataParameter m_KillFireflies;
        SerializedDataParameter m_PCDownsampleKernelSize;
        SerializedDataParameter m_PCDownsampleSigma;
        SerializedDataParameter m_PCUpsampleKernelSize;
        SerializedDataParameter m_PCUpsampleSigma;
        SerializedDataParameter m_PCLuminanceCompression;
        SerializedDataParameter m_PCPrefilterScale;
        SerializedDataParameter m_PCLayerWeights;
        SerializedDataParameter m_Downsample;
        SerializedDataParameter m_MaxIterations;
        SerializedDataParameter m_DirtTexture;
        SerializedDataParameter m_DirtIntensity;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<Bloom>(serializedObject);

            m_BloomMode = Unpack(o.Find(x => x.bloomMode));
            m_Threshold = Unpack(o.Find(x => x.threshold));
            m_Intensity = Unpack(o.Find(x => x.intensity));
            m_Scatter = Unpack(o.Find(x => x.scatter));
            m_Clamp = Unpack(o.Find(x => x.clamp));
            m_Tint = Unpack(o.Find(x => x.tint));
            m_HighQualityFiltering = Unpack(o.Find(x => x.highQualityFiltering));
            m_ThresholdKnee = Unpack(o.Find(x => x.thresholdKnee));
            m_KillFireflies = Unpack(o.Find(x => x.killFireflies));
            m_PCDownsampleKernelSize = Unpack(o.Find(x => x.pcDownsampleKernelSize));
            m_PCDownsampleSigma = Unpack(o.Find(x => x.pcDownsampleSigma));
            m_PCUpsampleKernelSize = Unpack(o.Find(x => x.pcUpsampleKernelSize));
            m_PCUpsampleSigma = Unpack(o.Find(x => x.pcUpsampleSigma));
            m_PCLuminanceCompression = Unpack(o.Find(x => x.pcLuminanceCompression));
            m_PCPrefilterScale = Unpack(o.Find(x => x.pcPrefilterScale));
            m_PCLayerWeights = Unpack(o.Find(x => x.pcLayerWeights));
            m_Downsample = Unpack(o.Find(x => x.downscale));
            m_MaxIterations = Unpack(o.Find(x => x.maxIterations));
            m_DirtTexture = Unpack(o.Find(x => x.dirtTexture));
            m_DirtIntensity = Unpack(o.Find(x => x.dirtIntensity));
        }

        public override void OnInspectorGUI()
        {
            // Bloom模式选择
            PropertyField(m_BloomMode);

            EditorGUILayout.Space();

            // 通用Bloom参数
            PropertyField(m_Threshold);
            PropertyField(m_Intensity);
            if (m_BloomMode.value.intValue != (int)BloomMode.PC)
                PropertyField(m_Scatter);
            PropertyField(m_Tint);
            PropertyField(m_Clamp);
            PropertyField(m_HighQualityFiltering);

            if (m_HighQualityFiltering.overrideState.boolValue && m_HighQualityFiltering.value.boolValue && CoreEditorUtils.buildTargets.Contains(GraphicsDeviceType.OpenGLES2))
                EditorGUILayout.HelpBox("High Quality Bloom isn't supported on GLES2 platforms.", MessageType.Warning);

            // nBloom模式专有参数
            if (m_BloomMode.value.intValue == (int)BloomMode.n)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("nBloom Mode Settings", EditorStyles.boldLabel);
                PropertyField(m_ThresholdKnee);
                PropertyField(m_KillFireflies);
            }
            else if (m_BloomMode.value.intValue == (int)BloomMode.PC)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("PC Mode Settings", EditorStyles.boldLabel);
                PropertyField(m_PCDownsampleKernelSize);
                PropertyField(m_PCDownsampleSigma);
                PropertyField(m_PCUpsampleKernelSize);
                PropertyField(m_PCUpsampleSigma);
                PropertyField(m_PCLuminanceCompression);
                PropertyField(m_PCPrefilterScale);
                PropertyField(m_PCLayerWeights);
                PropertyField(m_KillFireflies);
            }

            EditorGUILayout.Space();

            PropertyField(m_Downsample);
            PropertyField(m_MaxIterations);

            PropertyField(m_DirtTexture);
            PropertyField(m_DirtIntensity);
        }
    }
}
