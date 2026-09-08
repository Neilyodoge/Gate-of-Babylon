#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu.Editor
{
    /// <summary>在Unity播放控制左侧提供ProjectR的一键场景启动入口。</summary>
    [InitializeOnLoad]
    internal static class ProjectRGameLauncher
    {
        private const string GameScenePath =
            "Assets/1Game/Scenes/StarterPrologue.unity";
        private const string ButtonName = "ProjectRGameLauncherButton";

        static ProjectRGameLauncher()
        {
            EditorApplication.update += TryInjectToolbarButton;
        }

        private static void TryInjectToolbarButton()
        {
            Type toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null)
                return;

            UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars.Length == 0)
                return;

            FieldInfo rootField = toolbarType.GetField(
                "m_Root",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (rootField?.GetValue(toolbars[0]) is not VisualElement toolbarRoot)
                return;

            VisualElement playModeZone = toolbarRoot.Q("ToolbarZonePlayMode");
            if (playModeZone == null)
                return;

            if (playModeZone.Q(ButtonName) == null)
                playModeZone.Insert(0, CreateLaunchButton());

            EditorApplication.update -= TryInjectToolbarButton;
        }

        private static VisualElement CreateLaunchButton()
        {
            var button = new IMGUIContainer(() =>
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
                var content = new GUIContent(
                    "启动游戏",
                    "切换到ProjectR新手序章并开始播放；播放中不会重启。");
                if (GUILayout.Button(
                        content,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(88f),
                        GUILayout.Height(22f)))
                {
                    StartGame();
                }

                GUI.enabled = previousEnabled;
            })
            {
                name = ButtonName
            };
            button.style.width = 88f;
            button.style.height = 22f;
            button.style.marginRight = 6f;
            return button;
        }

        private static void StartGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SceneAsset gameScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath);
            if (gameScene == null)
            {
                Debug.LogError($"[启动游戏] 找不到游戏场景：{GameScenePath}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!string.Equals(
                    EditorSceneManager.GetActiveScene().path,
                    GameScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(GameScenePath);
            }

            EditorApplication.isPlaying = true;
        }
    }
}
#endif
