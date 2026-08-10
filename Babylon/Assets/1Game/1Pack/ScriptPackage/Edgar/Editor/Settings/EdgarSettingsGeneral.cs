using System;
using UnityEditor;
using UnityEngine;

namespace Edgar.Unity.Editor
{
    [Serializable]
    public class EdgarSettingsGeneral
    {
        public bool SnapLevelGraphToGrid = true;

        public bool DoubleClickToConfigureRoom = true;

        internal class Inspector : EdgarSettingsInspectorBase
        {
            public Inspector(SerializedObject serializedObject) : base(serializedObject, nameof(EdgarSettings.General))
            {
            }

            public override void OnGUI()
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                Show = EditorGUILayout.Foldout(Show, "通用设置");
                if (Show)
                {
                    EditorGUILayout.PropertyField(
                        Property(nameof(SnapLevelGraphToGrid)),
                        new GUIContent("关卡图吸附网格"));
                    EditorGUILayout.PropertyField(
                        Property(nameof(DoubleClickToConfigureRoom)),
                        new GUIContent("双击配置房间"));
                }
                GUILayout.EndVertical();
            }
        }
    }
}