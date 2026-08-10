using Edgar.Unity;
using Edgar.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Assets.Edgar.Editor.Grid3D
{
    [CustomEditor(typeof(GeneratorSettingsGrid3D))]
    public class GeneratorSettingsGrid3DInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var generatorSettings = (GeneratorSettingsGrid3D) target;

            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 2f;

            DrawDefaultInspector();

            serializedObject.Update();

            EditorGUILayout.Space();

            if (EdgarSettings.instance.Grid3D.DefaultGeneratorSettings == generatorSettings)
            {
                EditorGUILayout.HelpBox("当前为默认设置：是\n\n创建新的房间模板和门 Prefab 时会自动使用此生成器设置。", MessageType.Info);
                if (GUILayout.Button("取消默认生成器设置"))
                {
                    EdgarSettings.instance.Grid3D.DefaultGeneratorSettings = null;
                    EdgarSettings.instance.Save();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("当前为默认设置：否\n\n可将此对象设为创建新房间模板和门 Prefab 时使用的默认生成器设置。", MessageType.Info);
                if (GUILayout.Button("设为默认生成器设置"))
                {
                    EdgarSettings.instance.Grid3D.DefaultGeneratorSettings = generatorSettings;
                    EdgarSettings.instance.Save();
                }
            }

            EditorGUILayout.Space();

            // TODO(Grid3D): How to handle changes of computation mode? Should we always recalculate outlines after every change?
            if (generatorSettings.OutlineComputationMode == RoomTemplateOutlineComputationModeGrid3D.InsideEditor)
            {
                EditorGUILayout.HelpBox("当前设置为仅在编辑器内计算房间模板轮廓，建议只由高级用户使用。\n\n下列操作耗时取决于项目规模。", MessageType.Warning);

                if (GUILayout.Button("重新计算轮廓"))
                {
                    RoomTemplateSaveHandlerGrid3D.RecalculateOutlines(generatorSettings, false);
                }

                if (GUILayout.Button("移除预计算轮廓"))
                {
                    RoomTemplateSaveHandlerGrid3D.RecalculateOutlines(generatorSettings, true);
                }
            }


            EditorGUIUtility.labelWidth = 0;
        }
    }
}