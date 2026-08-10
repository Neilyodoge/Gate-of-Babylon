#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace XianTu.Editor
{
    public sealed class CameraLocatorWindow : EditorWindow
    {
        private Camera _targetCamera;

        [MenuItem("仙途秘境/开发工具/相机定位器")]
        private static void Open()
        {
            var window = GetWindow<CameraLocatorWindow>("相机定位器");
            window.minSize = new Vector2(320f, 170f);
            window.Show();
        }

        [MenuItem("仙途秘境/开发工具/场景视图定位到主相机 _F8")]
        private static void LocateMainCamera()
        {
            Camera camera = FindMainCamera();
            if (camera == null)
            {
                Debug.LogWarning("[相机定位器] 当前场景没有可用相机。");
                return;
            }

            AlignSceneViewToCamera(camera);
        }

        private void OnEnable()
        {
            if (_targetCamera == null)
                _targetCamera = FindMainCamera();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("场景相机", EditorStyles.boldLabel);
            _targetCamera = (Camera)EditorGUILayout.ObjectField(
                "目标相机",
                _targetCamera,
                typeof(Camera),
                true);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_targetCamera == null))
            {
                if (GUILayout.Button("场景视图定位到相机（F8）", GUILayout.Height(34f)))
                    AlignSceneViewToCamera(_targetCamera);

                if (GUILayout.Button("选中并在层级中显示相机", GUILayout.Height(26f)))
                {
                    Selection.activeGameObject = _targetCamera.gameObject;
                    EditorGUIUtility.PingObject(_targetCamera.gameObject);
                }

                if (GUILayout.Button("将相机对齐到当前场景视图", GUILayout.Height(26f)))
                    AlignCameraToSceneView(_targetCamera);
            }

            if (_targetCamera == null)
                EditorGUILayout.HelpBox("当前场景没有找到 MainCamera，请手动指定目标相机。", MessageType.Warning);
        }

        private static Camera FindMainCamera()
        {
            if (Camera.main != null)
                return Camera.main;

            var cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
            return cameras.Length > 0 ? cameras[0] : null;
        }

        private static void AlignSceneViewToCamera(Camera target)
        {
            if (target == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SceneView.FocusWindowIfItsOpen(typeof(SceneView));
                sceneView = SceneView.lastActiveSceneView;
            }
            if (sceneView == null || sceneView.camera == null)
            {
                Debug.LogWarning("[相机定位器] 没有可用的 Scene 视图。");
                return;
            }

            sceneView.camera.transform.SetPositionAndRotation(
                target.transform.position,
                target.transform.rotation);
            sceneView.AlignViewToObject(sceneView.camera.transform);
            sceneView.Repaint();
            Selection.activeGameObject = target.gameObject;
        }

        private static void AlignCameraToSceneView(Camera target)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (target == null || sceneView == null || sceneView.camera == null)
                return;

            Undo.RecordObject(target.transform, "相机对齐到场景视图");
            target.transform.SetPositionAndRotation(
                sceneView.camera.transform.position,
                sceneView.camera.transform.rotation);
            EditorUtility.SetDirty(target.transform);
            Selection.activeGameObject = target.gameObject;
        }
    }
}
#endif
