using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// ProjectR动态角色的临时统一渲染策略。
    /// 正式性能验收前优先保证角色不被场景遮挡数据或蒙皮剔除误隐藏。
    /// </summary>
    public static class DynamicCharacterRendering
    {
        public static void Apply(GameObject root)
        {
            if (root == null)
                return;

            foreach (Animator animator in
                     root.GetComponentsInChildren<Animator>(true))
            {
                animator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (SkinnedMeshRenderer renderer in
                     root.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                renderer.allowOcclusionWhenDynamic = false;
                renderer.updateWhenOffscreen = true;
            }
        }
    }
}
