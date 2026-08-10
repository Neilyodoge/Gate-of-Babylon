using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>白膜阶段使用的世界空间地标提示；正式美术替换后可整体移除。</summary>
    public sealed class DungeonLandmarkLabel : MonoBehaviour
    {
        private Camera _camera;

        public static void Create(
            Transform roomRoot,
            string text,
            Color color)
        {
            if (roomRoot == null || string.IsNullOrWhiteSpace(text))
                return;

            Transform existing = roomRoot.Find("__LandmarkLabel");
            if (existing != null)
                Destroy(existing.gameObject);

            Bounds bounds = GetRoomBounds(roomRoot);
            var labelObject = new GameObject(
                "__LandmarkLabel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(DungeonLandmarkLabel));
            RectTransform root = labelObject.GetComponent<RectTransform>();
            root.SetParent(roomRoot, true);
            root.position = new Vector3(
                bounds.center.x,
                bounds.max.y + 2f,
                bounds.center.z);
            root.sizeDelta = new Vector2(360f, 80f);
            float parentScale = Mathf.Max(
                0.001f,
                Mathf.Max(
                    Mathf.Abs(roomRoot.lossyScale.x),
                    Mathf.Abs(roomRoot.lossyScale.y),
                    Mathf.Abs(roomRoot.lossyScale.z)));
            root.localScale = Vector3.one * (0.01f / parentScale);

            var canvas = labelObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var background = backgroundObject.GetComponent<RectTransform>();
            background.SetParent(root, false);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            backgroundObject.GetComponent<Image>().color =
                new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.86f);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(root, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 34f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.color = color;
            if (UGuiKit.CjkFont != null)
                label.font = UGuiKit.CjkFont;
        }

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Vector3 awayFromCamera = transform.position - _camera.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(awayFromCamera.normalized, Vector3.up);
        }

        private static Bounds GetRoomBounds(Transform roomRoot)
        {
            var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(roomRoot.position, new Vector3(10f, 3f, 10f));

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
