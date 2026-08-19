using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>把事件选项转换为本房即时可见的实体结果。</summary>
    public static class EventSceneOutcome
    {
        private static readonly Color RouteColor = new(0.2f, 1.4f, 0.45f, 1f);
        private static readonly Color PowerColor = new(0.1f, 1.2f, 1.8f, 1f);
        private static readonly Color SealColor = new(1.4f, 0.25f, 0.55f, 1f);

        public static void Apply(
            EventOption option,
            Transform contentRoot,
            Vector3 interactionPosition)
        {
            if (option == null || option.SceneResult == EventSceneResult.None)
                return;

            bool authoredResult = false;
            if (contentRoot != null)
            {
                var objects = contentRoot.GetComponentsInChildren<DungeonEventSceneObject>(true);
                foreach (var sceneObject in objects)
                    authoredResult |= sceneObject.Apply(option.SceneResult);
            }

            if (!authoredResult)
                CreateDebugResult(contentRoot, interactionPosition, option.SceneResult);
        }

        private static void CreateDebugResult(
            Transform parent,
            Vector3 position,
            EventSceneResult result)
        {
            var root = new GameObject($"事件结果_{result}");
            root.transform.SetParent(parent, true);
            root.transform.position = position + Vector3.up * 0.15f;

            switch (result)
            {
                case EventSceneResult.OpenRoute:
                    CreatePart(root.transform, "左门柱", PrimitiveType.Cube,
                        new Vector3(-1.2f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.35f), RouteColor);
                    CreatePart(root.transform, "右门柱", PrimitiveType.Cube,
                        new Vector3(1.2f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.35f), RouteColor);
                    CreatePart(root.transform, "开启横梁", PrimitiveType.Cube,
                        new Vector3(0f, 2.3f, 0f), new Vector3(2.65f, 0.2f, 0.35f), RouteColor);
                    break;
                case EventSceneResult.Power:
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 3f;
                        CreatePart(root.transform, $"供能柱_{i + 1}", PrimitiveType.Cylinder,
                            new Vector3(Mathf.Cos(angle) * 1.2f, 0.65f, Mathf.Sin(angle) * 1.2f),
                            new Vector3(0.3f, 0.65f, 0.3f), PowerColor);
                    }
                    break;
                case EventSceneResult.Seal:
                    CreatePart(root.transform, "封存核心", PrimitiveType.Sphere,
                        new Vector3(0f, 0.9f, 0f), Vector3.one * 1.1f, SealColor);
                    CreatePart(root.transform, "封存基座", PrimitiveType.Cylinder,
                        new Vector3(0f, 0.15f, 0f), new Vector3(1.45f, 0.15f, 1.45f), SealColor);
                    break;
                case EventSceneResult.BridgeSabotaged:
                    CreatePart(root.transform, "损坏的桥梁机构", PrimitiveType.Cube,
                        new Vector3(0f, 0.45f, 0f), new Vector3(2.8f, 0.35f, 1.2f),
                        LevelAPhaseRuntime.IsNightMapActive ? SealColor : RouteColor);
                    break;
                case EventSceneResult.SummonArrayDestroyed:
                    CreatePart(root.transform, "破碎阵心", PrimitiveType.Sphere,
                        new Vector3(0f, 0.35f, 0f), Vector3.one * 0.75f, SealColor);
                    break;
                case EventSceneResult.SummonArrayOuterBroken:
                    CreatePart(root.transform, "残缺召集阵", PrimitiveType.Cylinder,
                        new Vector3(0f, 0.12f, 0f), new Vector3(1.8f, 0.12f, 1.8f), PowerColor);
                    break;
                case EventSceneResult.CrownLightDisabled:
                    CreatePart(root.transform, "破碎冠光主镜", PrimitiveType.Cube,
                        new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.35f, 1.4f), SealColor);
                    break;
                case EventSceneResult.CrownLightMisaligned:
                    CreatePart(root.transform, "偏转冠光镜组", PrimitiveType.Cube,
                        new Vector3(0.7f, 0.8f, 0f), new Vector3(0.35f, 1.6f, 1.1f), PowerColor);
                    break;
                case EventSceneResult.NightLiftRestored:
                    CreatePart(root.transform, "修复的升降井", PrimitiveType.Cylinder,
                        new Vector3(0f, 0.12f, 0f), new Vector3(2.2f, 0.12f, 2.2f), RouteColor);
                    break;
                case EventSceneResult.NightLiftDropped:
                    CreatePart(root.transform, "坠落的升降井", PrimitiveType.Cube,
                        new Vector3(0f, 0.3f, 0f), new Vector3(2.2f, 0.45f, 1.2f), SealColor);
                    break;
            }

            Debug.Log($"[事件场景] 已应用 {result} 的运行时回退表现。");
        }

        private static void CreatePart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = MaterialHelper.CreateLitEmissive(color * 0.35f, color);
        }
    }
}
