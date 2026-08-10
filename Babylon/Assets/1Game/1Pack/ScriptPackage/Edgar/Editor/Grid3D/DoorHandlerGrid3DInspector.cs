using UnityEditor;
using UnityEngine;

namespace Edgar.Unity.Editor.Grid3D
{
    [CustomEditor(typeof(DoorHandlerGrid3D))]
    public class DoorHandlerGrid3DInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var doorHandler = (DoorHandlerGrid3D) target;

            var doorPrefab = doorHandler.gameObject;
            var hierarchyRoot = doorPrefab.transform.root.gameObject;
            var isInsideDoorPrefab = doorPrefab == hierarchyRoot;

            DrawDefaultInspector();

            HandleRotations(doorHandler);

            if (doorHandler.GeneratorSettings == null)
            {
                EditorGUILayout.HelpBox("请指定“生成器设置”字段。", MessageType.Error);
            }
            else if (!isInsideDoorPrefab)
            {
                HandleMisaligned(doorHandler);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void HandleRotations(DoorHandlerGrid3D doorHandler)
        {
            if (doorHandler.IsDirectionValid())
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("旋转 -90°"))
                {
                    doorHandler.Rotate90(false);
                    SceneView.RepaintAll();
                    EditorUtility.SetDirty(target);
                }

                if (GUILayout.Button("旋转 +90°"))
                {
                    doorHandler.Rotate90(true);
                    SceneView.RepaintAll();
                    EditorUtility.SetDirty(target);
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("该门的 Transform 旋转与 Edgar 内部方向不一致。请始终使用上方旋转按钮；若已手动旋转，请点击下方按钮同步。", MessageType.Error);

                if (GUILayout.Button("同步旋转"))
                {
                    doorHandler.SyncRotation();
                    SceneView.RepaintAll();
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private void HandleMisaligned(DoorHandlerGrid3D doorHandler)
        {
            var generatorSettings = doorHandler.GeneratorSettings;
            var doorPositionInterpolated = generatorSettings.LocalToCellInterpolated(doorHandler.transform.localPosition);
            var doorPositionSnapped = GridUtilsGrid3D.SnapInterpolatedToCell(doorPositionInterpolated);

            if (GridUtilsGrid3D.IsSnappedToCell(doorPositionInterpolated, doorPositionSnapped))
            {
                return;
            }

            var closestSnapCell = GridUtilsGrid3D.SnapInterpolatedToCellRound(doorPositionInterpolated);
            var closestSnapLocal = generatorSettings.CellToLocal(closestSnapCell);

            EditorGUILayout.HelpBox($"该门未正确对齐网格。最近的有效位置是 {closestSnapLocal}。", MessageType.Error);

            if (GUILayout.Button("吸附到网格"))
            {
                doorHandler.transform.localPosition = closestSnapLocal;
                SceneView.RepaintAll();
                EditorUtility.SetDirty(target);
            }
        }
    }
}