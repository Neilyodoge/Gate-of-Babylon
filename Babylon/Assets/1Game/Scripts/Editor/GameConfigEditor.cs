using UnityEditor;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// GameConfig 编辑器工具
    /// </summary>
    public static class GameConfigEditor
    {
        [MenuItem("仙途梦境/⑤ 创建游戏配置 (GameConfig)")]
        public static void CreateGameConfig()
        {
            // 确保 Resources 目录存在
            if (!AssetDatabase.IsValidFolder("Assets/1Game/Resources"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/1Game"))
                    AssetDatabase.CreateFolder("Assets", "1Game");
                AssetDatabase.CreateFolder("Assets/1Game", "Resources");
            }

            const string path = "Assets/1Game/Resources/GameConfig.asset";

            // 检查是否已存在
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (existing != null)
            {
                Debug.Log("<color=yellow>GameConfig 已存在，直接选中</color>");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            // 创建新的 GameConfig
            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"<color=green>✅ GameConfig 已创建：{path}</color>");
            Debug.Log("<color=cyan>在 Inspector 中可以快速修改所有游戏属性！</color>");
        }

        [MenuItem("仙途梦境/⑥ 选中游戏配置")]
        public static void SelectGameConfig()
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            else
            {
                Debug.LogWarning("GameConfig 不存在，请先执行 '仙途梦境 → ⑤ 创建游戏配置'");
            }
        }
    }
}
