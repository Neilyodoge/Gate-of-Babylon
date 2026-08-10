using System.IO;
using Edgar.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Edgar.Unity
{
    public class DungeonRoomTemplateInitializerGrid3D
    {
        #if UNITY_EDITOR
        [MenuItem("Assets/Create/仙途秘境/Edgar Grid3D/房间模板")]
        public static void CreateRoomTemplatePrefab()
        {
            // Create empty game object
            var roomTemplate = new GameObject();

            var roomTemplateSettings = roomTemplate.AddComponent<RoomTemplateSettingsGrid3D>();
            roomTemplateSettings.GeneratorSettings = EdgarSettings.instance.Grid3D.DefaultGeneratorSettings;

            var blocks = new GameObject(GeneratorConstantsGrid3D.BlocksRootName);
            blocks.transform.parent = roomTemplate.transform;

            var doors = new GameObject(GeneratorConstantsGrid3D.DoorsRootName);
            doors.transform.parent = roomTemplate.transform;

            // Save prefab
            var currentPath = GetCurrentPath();
            PrefabUtility.SaveAsPrefabAsset(roomTemplate, AssetDatabase.GenerateUniqueAssetPath(currentPath + "/房间模板.prefab"));

            // Remove game object from scene
            Object.DestroyImmediate(roomTemplate);
        }
        #endif

        #if UNITY_EDITOR
        [MenuItem("Assets/Create/仙途秘境/Edgar Grid3D/门")]
        public static void CreateDoorPrefab()
        {
            // Create empty game object
            var roomTemplate = new GameObject();

            var doorHandler = roomTemplate.AddComponent<DoorHandlerGrid3D>();
            doorHandler.GeneratorSettings = EdgarSettings.instance.Grid3D.DefaultGeneratorSettings;

            // Save prefab
            var currentPath = GetCurrentPath();
            PrefabUtility.SaveAsPrefabAsset(roomTemplate, AssetDatabase.GenerateUniqueAssetPath(currentPath + "/门.prefab"));

            // Remove game object from scene
            Object.DestroyImmediate(roomTemplate);
        }
        #endif

        #if UNITY_EDITOR
        // TODO(Grid3D): Keep DRY later
        private static string GetCurrentPath()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (path == "")
            {
                path = "Assets";
            }
            else if (Path.GetExtension(path) != "")
            {
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }

            return path;
        }
        #endif
    }
}