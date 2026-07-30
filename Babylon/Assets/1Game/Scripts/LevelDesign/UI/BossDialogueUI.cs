using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// Boss 出场对白播报 UI（V0.4.6 改 uGUI+TMP）——屏幕下方古风字幕，自动逐行播放。
    /// 横幅不阻挡输入（Canvas 不含 GraphicRaycaster 拦截 / raycastTarget=false）。
    /// 对外保持 Show(phaseName, lines, dur) / HideImmediate。
    /// </summary>
    public class BossDialogueUI : MonoBehaviour
    {
        private static BossDialogueUI _instance;

        private string _phaseName;
        private string[] _lines;
        private int _currentLineIdx;
        private float _lineStartTime;
        private float _lineDuration = 3.0f;
        private bool _visible;

        private GameObject _root;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _line;

        public static void Show(string phaseName, string[] lines, float lineDuration = 3f)
        {
            if (lines == null || lines.Length == 0) return;
            EnsureInstance();
            if (_instance == null) return;

            _instance._phaseName = phaseName;
            _instance._lines = lines;
            _instance._lineDuration = lineDuration;
            _instance._currentLineIdx = 0;
            _instance._lineStartTime = Time.unscaledTime;
            _instance._visible = true;
            _instance.RefreshLine();
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._root != null) _instance._root.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("BossDialogueUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BossDialogueUI>();
        }

        private void Awake()
        {
            var canvas = XianTu.UGuiKit.CreateOverlayCanvas("BossDialogueUI", 60, transform);
            _root = canvas.gameObject;
            // 不阻挡输入：移除 GraphicRaycaster
            var ray = _root.GetComponent<GraphicRaycaster>();
            if (ray != null) Destroy(ray);

            // 底部横幅
            var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var brt = (RectTransform)banner.transform;
            brt.SetParent(_root.transform, false);
            brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 120f);
            brt.sizeDelta = new Vector2(1100f, 120f);
            banner.color = new Color(0.03f, 0.03f, 0.05f, 0.72f);
            banner.raycastTarget = false;
            var bv = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.padding = new RectOffset(24, 24, 12, 12); bv.spacing = 6f;
            bv.childControlWidth = true; bv.childForceExpandWidth = true; bv.childControlHeight = true; bv.childForceExpandHeight = false;
            bv.childAlignment = TextAnchor.MiddleCenter;

            _speaker = XianTu.UGuiKit.CreateText(brt, "", 22, XianTu.UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            XianTu.UGuiKit.SetHeight(_speaker, 30f);
            _line = XianTu.UGuiKit.CreateText(brt, "", 20, new Color(0.9f, 0.9f, 0.95f), TextAlignmentOptions.Center);
            _line.enableWordWrapping = true;
            XianTu.UGuiKit.SetHeight(_line, 56f);

            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_visible || _lines == null) return;
            if (Time.unscaledTime - _lineStartTime >= _lineDuration)
            {
                _currentLineIdx++;
                if (_currentLineIdx >= _lines.Length)
                {
                    HideImmediate();
                    return;
                }
                _lineStartTime = Time.unscaledTime;
                RefreshLine();
            }
        }

        private void RefreshLine()
        {
            if (_lines == null || _currentLineIdx < 0 || _currentLineIdx >= _lines.Length) return;
            if (_speaker != null) _speaker.text = $"【{_phaseName}】";
            if (_line != null) _line.text = _lines[_currentLineIdx];
        }
    }
}
