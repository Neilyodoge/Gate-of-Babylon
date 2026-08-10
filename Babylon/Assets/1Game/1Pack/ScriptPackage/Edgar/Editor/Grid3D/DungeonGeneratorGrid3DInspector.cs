using UnityEditor;
using UnityEngine;

namespace Edgar.Unity.Editor.Grid3D
{
    [CustomEditor(typeof(DungeonGeneratorGrid3D), true)]
    public class DungeonGeneratorGrid3DInspector : UnityEditor.Editor
    {
        private ReorderableList customPostProcessingTasksList;

        public void OnEnable()
        {
            customPostProcessingTasksList = new ReorderableList(new UnityEditorInternal.ReorderableList(serializedObject,
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.CustomPostProcessingTasks)),
                true, true, true, true), "自定义后处理任务");
        }

        public override void OnInspectorGUI()
        {
            var generator = (DungeonGeneratorGrid3D) target;

            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth / 2f;

            EditorGUILayout.LabelField("输入配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.InputType)),
                new GUIContent("输入类型"));
            switch (generator.InputType)
            {
                case DungeonGeneratorInputTypeGrid2D.CustomInput:
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.CustomInput)),
                        new GUIContent("自定义输入"));
                    break;
                case DungeonGeneratorInputTypeGrid2D.FixedLevelGraph:
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.FixedLevelGraphConfig)),
                        new GUIContent("固定关卡图配置"));
                    break;
            }

            EditorGUILayout.LabelField("生成器配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.GeneratorConfig)),
                new GUIContent("生成参数"));

            if (generator.GeneratorConfig.GeneratorSettings == null)
            {
                EditorGUILayout.HelpBox("请指定“生成器设置”字段。", MessageType.Error);
            }

            EditorGUILayout.LabelField("后处理配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.PostProcessingConfig)),
                new GUIContent("后处理参数"));

            if (generator.DisableCustomPostProcessing)
            {
                EditorGUILayout.HelpBox("自定义后处理任务当前已禁用。取消勾选“禁用自定义后处理”即可重新启用。", MessageType.Warning);
            }
            else
            {
                customPostProcessingTasksList.DoLayoutList();
            }

            EditorGUILayout.LabelField("其他", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.UseRandomSeed)),
                new GUIContent("使用随机种子"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.RandomGeneratorSeed)),
                new GUIContent("生成种子"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.GenerateOn)),
                new GUIContent("生成时机"));

            EditorGUILayout.HelpBox("若生成器性能异常，可启用诊断。诊断会在关卡生成后运行并把结果输出到控制台；正式版本请勿启用。", MessageType.Info);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorBaseGrid2D.EnableDiagnostics)),
                new GUIContent("启用诊断"));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(DungeonGeneratorGrid3D.DisableCustomPostProcessing)),
                new GUIContent("禁用自定义后处理"));

            EditorGUILayout.Space();

            if (GUILayout.Button("生成关卡"))
            {
                generator.Generate();
            }

            EditorGUIUtility.labelWidth = 0;

            serializedObject.ApplyModifiedProperties();
        }
    }
}