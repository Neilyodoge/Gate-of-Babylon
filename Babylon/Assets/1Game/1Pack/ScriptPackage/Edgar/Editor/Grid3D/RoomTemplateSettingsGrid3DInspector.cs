using System;
using System.Text;
using Edgar.Unity.Diagnostics;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;

namespace Edgar.Unity.Editor
{
    [CustomEditor(typeof(RoomTemplateSettingsGrid3D))]
    public class RoomTemplateSettingsGrid3DInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            var roomTemplate = (RoomTemplateSettingsGrid3D)target;
            var validityCheck = RoomTemplateDiagnosticsGrid3D.CheckAll(roomTemplate.gameObject);

            if (!validityCheck.HasErrors)
            {
                EditorGUILayout.HelpBox("房间模板有效。", MessageType.Info);
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("房间模板存在以下问题：");

                var errors = string.Join("\n", validityCheck.Errors);
                sb.Append(errors);

                EditorGUILayout.HelpBox(sb.ToString(), MessageType.Error);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            RemoveOnSceneGUIDelegate();
            AddOnSceneGUIDelegate();
        }

        private void OnSceneGUIPersistent(SceneView sceneView)
        {
            if (target == null || PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                RemoveOnSceneGUIDelegate();
                return;
            }

            ShowStatus();
        }

        private void ShowStatus()
        {
            var roomTemplate = (RoomTemplateSettingsGrid3D)target;
            var originalBackground = GUI.backgroundColor;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 180, 100));
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("房间模板状态", EditorStyles.boldLabel);

            var hasComponents = RoomTemplateDiagnosticsGrid3D.CheckComponents(roomTemplate.gameObject);
            var isOutlineValid = hasComponents.IsSuccessful && roomTemplate.ComputeOutline() != null;
            var outlineText = isOutlineValid ? "有效" : "<color=#870526ff>无效</color>";
            var areDoorsValid = false;
            var doorsText = "不可用";

            if (isOutlineValid)
            {
                var doorsCheck = RoomTemplateDiagnosticsGrid3D.CheckDoors(roomTemplate.gameObject);
                areDoorsValid = !doorsCheck.HasErrors;
                doorsText = !doorsCheck.HasErrors ? "有效" : "<color=#870526ff>无效</color>";
            }

            GUILayout.Label($"轮廓：<b>{outlineText}</b>", new GUIStyle(EditorStyles.label) { richText = true });
            GUILayout.Label($"门：<b>{doorsText}</b>", new GUIStyle(EditorStyles.label) { richText = true });

            if (!isOutlineValid || !areDoorsValid)
            {
                GUILayout.Label($"<size=9>详情请查看“房间模板设置”组件</size>", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();

            GUI.backgroundColor = originalBackground;
        }

        private void AddOnSceneGUIDelegate()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUIPersistent;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUIPersistent;
#endif
        }

        private void RemoveOnSceneGUIDelegate()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUIPersistent;
#else
            SceneView.onSceneGUIDelegate -= OnSceneGUIPersistent;
#endif
        }
    }
}