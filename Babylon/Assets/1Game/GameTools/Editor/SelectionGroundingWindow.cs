using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XianTu.EditorTools
{
    public sealed class SelectionGroundingWindow : EditorWindow
    {
        private enum AnchorMode
        {
            BoundsBottom,
            TransformPivot
        }

        private const string MenuPath = "ProjectR/选中物体贴地";
        private const float DefaultRayStartOffset = 1f;
        private const float DefaultMaxDistance = 10000f;

        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private AnchorMode anchorMode = AnchorMode.BoundsBottom;
        [SerializeField] private float rayStartOffset = DefaultRayStartOffset;
        [SerializeField] private float maxDistance = DefaultMaxDistance;
        [SerializeField] private float surfaceOffset;
        [SerializeField] private bool alignToSurfaceNormal;
        [SerializeField] private bool enableHotkey;

        private string operationMessage;
        private MessageType operationMessageType;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<SelectionGroundingWindow>("物体贴地");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += HandleSceneViewHotkey;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= HandleSceneViewHotkey;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("选中物体贴地", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "从每个选中物体上方向下检测 TerrainCollider 或普通 Collider。"
                + "所选物体自身及其他选中物体的碰撞体会被忽略。",
                MessageType.Info);

            groundLayers = DrawLayerMask(
                new GUIContent("地面层", "只有位于这些 Layer 的碰撞体会被视为地面。"),
                groundLayers);
            anchorMode = (AnchorMode)EditorGUILayout.EnumPopup(
                new GUIContent("贴地点", "包围盒底部适合草木、石块；Transform Pivot 直接将轴心放到地面。"),
                anchorMode);
            rayStartOffset = Mathf.Max(
                0.01f,
                EditorGUILayout.FloatField(new GUIContent("射线起点余量"), rayStartOffset));
            maxDistance = Mathf.Max(
                0.01f,
                EditorGUILayout.FloatField(new GUIContent("最大检测距离"), maxDistance));
            surfaceOffset = EditorGUILayout.FloatField(
                new GUIContent("离地偏移", "正值抬高，负值下沉。"),
                surfaceOffset);
            alignToSurfaceNormal = EditorGUILayout.Toggle(
                new GUIContent("对齐地面法线", "保留物体朝向，并将自身 Up 轴对齐到命中面的法线。"),
                alignToSurfaceNormal);
            enableHotkey = EditorGUILayout.Toggle(
                new GUIContent("启用快捷键 1", "仅在此工具窗口打开时生效；在 Scene 视图按数字键 1 执行贴地。"),
                enableHotkey);

            EditorGUILayout.Space();

            Transform[] targets = GetSelectionRoots();
            EditorGUILayout.LabelField($"有效选中对象：{targets.Length}");

            using (new EditorGUI.DisabledScope(targets.Length == 0))
            {
                if (GUILayout.Button("贴到地面", GUILayout.Height(32f)))
                {
                    GroundSelection(targets);
                }
            }

            if (!string.IsNullOrEmpty(operationMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(operationMessage, operationMessageType);
            }
        }

        private void HandleSceneViewHotkey(SceneView sceneView)
        {
            if (!enableHotkey || EditorGUIUtility.editingTextField)
            {
                return;
            }

            Event current = Event.current;
            bool isNumberOne = current.keyCode == KeyCode.Alpha1
                || current.keyCode == KeyCode.Keypad1;
            if (current.type != EventType.KeyDown
                || !isNumberOne
                || current.alt
                || current.control
                || current.shift
                || current.command)
            {
                return;
            }

            Transform[] targets = GetSelectionRoots();
            if (targets.Length == 0)
            {
                operationMessage = "没有有效的场景对象可贴地。";
                operationMessageType = MessageType.Warning;
            }
            else
            {
                GroundSelection(targets);
            }

            current.Use();
            Repaint();
        }

        private void GroundSelection(IReadOnlyList<Transform> targets)
        {
            Physics.SyncTransforms();

            var ignoredRoots = new HashSet<Transform>(targets);
            var placements = new List<Placement>(targets.Count);
            var missedNames = new List<string>();

            foreach (Transform target in targets)
            {
                Bounds? bounds = TryGetWorldBounds(target);
                float anchorOffset = anchorMode == AnchorMode.BoundsBottom && bounds.HasValue
                    ? target.position.y - bounds.Value.min.y
                    : 0f;
                float rayStartY = Mathf.Max(target.position.y, bounds?.max.y ?? target.position.y)
                    + rayStartOffset;
                Vector3 origin = new Vector3(target.position.x, rayStartY, target.position.z);

                if (TryFindGround(origin, ignoredRoots, out RaycastHit hit))
                {
                    Vector3 position = target.position;
                    position.y = hit.point.y + anchorOffset + surfaceOffset;
                    Quaternion rotation = alignToSurfaceNormal
                        ? Quaternion.FromToRotation(target.up, hit.normal) * target.rotation
                        : target.rotation;
                    placements.Add(new Placement(target, position, rotation));
                }
                else
                {
                    missedNames.Add(target.name);
                }
            }

            if (placements.Count == 0)
            {
                operationMessage = "没有对象检测到地面。请检查地面 Collider、Layer 和检测距离。";
                operationMessageType = MessageType.Error;
                Repaint();
                return;
            }

            Undo.RecordObjects(placements.Select(placement => placement.Target).ToArray(), "选中物体贴地");
            foreach (Placement placement in placements)
            {
                placement.Target.SetPositionAndRotation(placement.Position, placement.Rotation);
                EditorUtility.SetDirty(placement.Target);
            }

            SceneView.RepaintAll();

            string result = $"已贴地：{placements.Count} 个";
            if (missedNames.Count > 0)
            {
                result += $"\n未命中：{missedNames.Count} 个\n\n{string.Join("、", missedNames.Take(10))}";
                if (missedNames.Count > 10)
                {
                    result += "……";
                }
            }

            operationMessage = result;
            operationMessageType = missedNames.Count > 0
                ? MessageType.Warning
                : MessageType.Info;
            Repaint();
        }

        private bool TryFindGround(
            Vector3 origin,
            IReadOnlyCollection<Transform> ignoredRoots,
            out RaycastHit groundHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                maxDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (!BelongsToAnyRoot(hit.collider.transform, ignoredRoots))
                {
                    groundHit = hit;
                    return true;
                }
            }

            groundHit = default;
            return false;
        }

        private static bool BelongsToAnyRoot(Transform candidate, IEnumerable<Transform> roots)
        {
            foreach (Transform root in roots)
            {
                if (candidate == root || candidate.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform[] GetSelectionRoots()
        {
            Transform[] selected = Selection.transforms
                .Where(transform => transform != null && transform.gameObject.scene.IsValid())
                .Distinct()
                .ToArray();

            return selected
                .Where(transform => !selected.Any(other => other != transform && transform.IsChildOf(other)))
                .ToArray();
        }

        private static LayerMask DrawLayerMask(GUIContent label, LayerMask layerMask)
        {
            string[] layerNames = UnityEditorInternal.InternalEditorUtility.layers;
            int compactMask = 0;

            for (int index = 0; index < layerNames.Length; index++)
            {
                int layer = LayerMask.NameToLayer(layerNames[index]);
                if ((layerMask.value & (1 << layer)) != 0)
                {
                    compactMask |= 1 << index;
                }
            }

            compactMask = EditorGUILayout.MaskField(label, compactMask, layerNames);

            int fullMask = 0;
            for (int index = 0; index < layerNames.Length; index++)
            {
                if ((compactMask & (1 << index)) != 0)
                {
                    fullMask |= 1 << LayerMask.NameToLayer(layerNames[index]);
                }
            }

            return fullMask;
        }

        private static Bounds? TryGetWorldBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(false);
            bool hasBounds = false;
            Bounds combined = default;

            foreach (Renderer renderer in renderers)
            {
                Encapsulate(renderer.bounds, ref combined, ref hasBounds);
            }

            if (!hasBounds)
            {
                foreach (Collider collider in colliders)
                {
                    Encapsulate(collider.bounds, ref combined, ref hasBounds);
                }
            }

            return hasBounds ? combined : null;
        }

        private static void Encapsulate(Bounds bounds, ref Bounds combined, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                combined = bounds;
                hasBounds = true;
                return;
            }

            combined.Encapsulate(bounds);
        }

        private readonly struct Placement
        {
            public Placement(Transform target, Vector3 position, Quaternion rotation)
            {
                Target = target;
                Position = position;
                Rotation = rotation;
            }

            public Transform Target { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }
    }
}
