using UnityEngine;

namespace PBRToon
{
    /// <summary>
    /// 将物体的朝向信息传递给 PBRToon/Face shader
    /// 需要挂载在角色的头部（或包含 Face 材质的物体上）
    /// 通过 Transform 的 right 和 forward 方向自动设置 _FaceRightDirWS 和 _FaceFrontDirWS
    /// </summary>
    [ExecuteInEditMode]
    public class PBRToonFaceDirection : MonoBehaviour
    {
        [Header("面部朝向参考")]
        [Tooltip("面部朝向的参考 Transform（通常是头部骨骼）。如果为空则使用当前 Transform。")]
        public Transform faceTransform;

        [Header("方向翻转")]
        [Tooltip("翻转 Right 方向")]
        public bool flipRight = false;
        [Tooltip("翻转 Forward 方向")]
        public bool flipForward = false;

        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int FaceRightDirWSID = Shader.PropertyToID("_FaceRightDirWS");
        private static readonly int FaceFrontDirWSID = Shader.PropertyToID("_FaceFrontDirWS");

        private void OnEnable()
        {
            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            Transform refTransform = faceTransform != null ? faceTransform : transform;

            Vector3 rightDir = refTransform.right * (flipRight ? -1f : 1f);
            Vector3 frontDir = refTransform.forward * (flipForward ? -1f : 1f);

            if (renderers == null) return;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.HasProperty(FaceRightDirWSID))
                    {
                        renderer.GetPropertyBlock(propertyBlock);
                        propertyBlock.SetVector(FaceRightDirWSID, rightDir);
                        propertyBlock.SetVector(FaceFrontDirWSID, frontDir);
                        renderer.SetPropertyBlock(propertyBlock);
                        break;
                    }
                }
            }
        }
    }
}
