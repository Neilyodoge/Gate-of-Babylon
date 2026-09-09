#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XianTu.Editor
{
    /// <summary>
    /// 使用临时槽位3从新档测试序章，并在退出PlayMode后恢复原存档文件。
    /// </summary>
    [InitializeOnLoad]
    internal static class StarterPrologueTestLauncher
    {
        private const int TemporarySlot = 2;
        private const string ScenePath =
            "Assets/1Game/Scenes/StarterPrologue.unity";
        private const string LastSlotKey = "GoB.LastSaveSlot";
        private const string BackupFolderName =
            "StarterPrologueTestBackup";
        private const string ManifestFileName = "manifest.json";

        [Serializable]
        private sealed class BackupManifest
        {
            public bool autoRestore;
            public bool hadLastSlotPreference;
            public int previousLastSlot;
            public bool[] slotExisted = new bool[SaveSystem.MaxSlots];
        }

        private static string ProjectLibraryPath =>
            Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library",
                "ProjectR");

        private static string BackupFolderPath =>
            Path.Combine(ProjectLibraryPath, BackupFolderName);

        private static string ManifestPath =>
            Path.Combine(BackupFolderPath, ManifestFileName);

        static StarterPrologueTestLauncher()
        {
            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
        }

        [MenuItem(
            "ProjectR/开发工具/新手序章/临时新档测试",
            false,
            20)]
        internal static void StartFreshTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (Directory.Exists(BackupFolderPath) &&
                !RestoreBackup(false))
            {
                return;
            }
            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                BackupCurrentSlots();
                SaveSystem.Instance.CreateSlot(
                    TemporarySlot,
                    "[临时] 新手序章测试");
                OpenAndPlayPrologue();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[新手序章测试] 启动失败：{exception}");
                RestoreBackup(true);
            }
        }

        [MenuItem(
            "ProjectR/开发工具/新手序章/恢复测试前存档",
            false,
            21)]
        private static void RestoreFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "无法恢复",
                    "请先退出PlayMode，再恢复测试前存档。",
                    "知道了");
                return;
            }
            RestoreBackup(true);
        }

        [MenuItem(
            "ProjectR/开发工具/新手序章/恢复测试前存档",
            true)]
        private static bool ValidateRestoreFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   Directory.Exists(BackupFolderPath);
        }

        private static void BackupCurrentSlots()
        {
            Directory.CreateDirectory(BackupFolderPath);
            try
            {
                var manifest = new BackupManifest
                {
                    autoRestore = true,
                    hadLastSlotPreference =
                        PlayerPrefs.HasKey(LastSlotKey),
                    previousLastSlot =
                        PlayerPrefs.GetInt(LastSlotKey, -1)
                };
                for (int slot = 0; slot < SaveSystem.MaxSlots; slot++)
                {
                    string source = SlotPath(slot);
                    bool exists = File.Exists(source);
                    manifest.slotExisted[slot] = exists;
                    if (exists)
                    {
                        File.Copy(
                            source,
                            BackupSlotPath(slot),
                            true);
                    }
                }
                File.WriteAllText(
                    ManifestPath,
                    JsonUtility.ToJson(manifest, true));
            }
            catch
            {
                if (Directory.Exists(BackupFolderPath))
                    Directory.Delete(BackupFolderPath, true);
                throw;
            }
        }

        private static bool RestoreBackup(
            bool requestScriptReload)
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError(
                    "[新手序章测试] 备份目录存在但清单缺失，已停止启动以避免覆盖存档。");
                return false;
            }

            try
            {
                BackupManifest manifest =
                    JsonUtility.FromJson<BackupManifest>(
                        File.ReadAllText(ManifestPath));
                if (manifest?.slotExisted == null ||
                    manifest.slotExisted.Length != SaveSystem.MaxSlots)
                {
                    throw new InvalidDataException(
                        "备份清单中的槽位信息无效。");
                }

                for (int slot = 0;
                     slot < SaveSystem.MaxSlots;
                     slot++)
                {
                    string destination = SlotPath(slot);
                    if (manifest.slotExisted[slot])
                    {
                        File.Copy(
                            BackupSlotPath(slot),
                            destination,
                            true);
                    }
                    else if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                }

                if (manifest.hadLastSlotPreference)
                {
                    PlayerPrefs.SetInt(
                        LastSlotKey,
                        manifest.previousLastSlot);
                }
                else
                {
                    PlayerPrefs.DeleteKey(LastSlotKey);
                }
                PlayerPrefs.Save();
                Directory.Delete(BackupFolderPath, true);
                Debug.Log(
                    "<color=#66FF99>[新手序章测试] 已恢复测试前的存档槽位。</color>");
                if (requestScriptReload)
                    EditorUtility.RequestScriptReload();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[新手序章测试] 恢复失败，备份目录已保留：" +
                    exception);
                return false;
            }
        }

        private static void OpenAndPlayPrologue()
        {
            SceneAsset scene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene == null)
                throw new FileNotFoundException(
                    "找不到新手序章场景。",
                    ScenePath);
            if (!string.Equals(
                    EditorSceneManager.GetActiveScene().path,
                    ScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode ||
                !File.Exists(ManifestPath))
            {
                return;
            }
            RestoreBackup(true);
        }

        private static string SlotPath(int slot)
        {
            return Path.Combine(
                Application.persistentDataPath,
                $"save_slot_{slot}.json");
        }

        private static string BackupSlotPath(int slot)
        {
            return Path.Combine(
                BackupFolderPath,
                $"save_slot_{slot}.json");
        }
    }
}
#endif
