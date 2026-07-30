using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 设置面板（V0.4.6 改 uGUI+TMP）—— 音频 / 画质 / 控制。
    ///
    /// UI 用 UGuiKit 代码化构建（Canvas sortingOrder=140，可从暂停/主菜单上方打开）。
    /// 音量绑定 <see cref="AudioConfig"/>；画质/全屏用 Unity API；控制为只读键位提示。
    /// 设置用 PlayerPrefs 持久化（关闭时保存）。
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private static SettingsUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private int _tabIndex;

        private const string K_MASTER = "Setting.MasterVol";
        private const string K_SFX = "Setting.SfxVol";
        private const string K_BGM = "Setting.BgmVol";
        private const string K_UI = "Setting.UiVol";
        private const string K_QUALITY = "Setting.Quality";
        private const string K_FULLSCREEN = "Setting.Fullscreen";

        private float _master, _sfx, _bgm, _ui;
        private int _qualityIdx;
        private bool _fullscreen;
        private AudioConfig _audioConfig;

        private GameObject _root;
        private RectTransform _tabsBar;
        private RectTransform _content;   // scroll content
        private readonly Button[] _tabButtons = new Button[3];

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._visible = true;
            _instance.LoadValues();
            _instance.RebuildAll();
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance.SaveValues();
            if (_instance._root != null) _instance._root.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("SettingsUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsUI>();
        }

        // ========== 持久化 ==========

        private void LoadValues()
        {
            _audioConfig = Resources.Load<AudioConfig>("AudioConfig");
            _master = PlayerPrefs.GetFloat(K_MASTER, _audioConfig != null ? _audioConfig.masterVolume : 1f);
            _sfx = PlayerPrefs.GetFloat(K_SFX, _audioConfig != null ? _audioConfig.sfxVolume : 0.8f);
            _bgm = PlayerPrefs.GetFloat(K_BGM, _audioConfig != null ? _audioConfig.bgmVolume : 0.5f);
            _ui = PlayerPrefs.GetFloat(K_UI, _audioConfig != null ? _audioConfig.uiVolume : 0.7f);
            _qualityIdx = PlayerPrefs.GetInt(K_QUALITY, QualitySettings.GetQualityLevel());
            _fullscreen = PlayerPrefs.GetInt(K_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            ApplyAudio();
            QualitySettings.SetQualityLevel(_qualityIdx, true);
        }

        private void SaveValues()
        {
            PlayerPrefs.SetFloat(K_MASTER, _master);
            PlayerPrefs.SetFloat(K_SFX, _sfx);
            PlayerPrefs.SetFloat(K_BGM, _bgm);
            PlayerPrefs.SetFloat(K_UI, _ui);
            PlayerPrefs.SetInt(K_QUALITY, _qualityIdx);
            PlayerPrefs.SetInt(K_FULLSCREEN, _fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyAudio()
        {
            if (_audioConfig == null) return;
            _audioConfig.masterVolume = _master;
            _audioConfig.sfxVolume = _sfx;
            _audioConfig.bgmVolume = _bgm;
            _audioConfig.uiVolume = _ui;
        }

        // ========== uGUI 构建 ==========

        private void Awake()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("SettingsCanvas", 140, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform);

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(760f, 640f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 12f, new RectOffset(24, 24, 20, 20), TextAnchor.UpperCenter);

            // 标题行
            var header = UGuiKit.CreateRow(panel, 12f, 48f);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            var title = UGuiKit.CreateText(header, "设 置", 34, UGuiKit.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            title.GetComponent<LayoutElement>(); UGuiKit.SetHeight(title, 44f); title.GetComponent<LayoutElement>().preferredWidth = 380f;
            var note = UGuiKit.CreateText(header, "设置自动保存", 18, UGuiKit.TextDim, TextAlignmentOptions.Right);
            UGuiKit.SetHeight(note, 44f); note.GetComponent<LayoutElement>().preferredWidth = 200f;
            var close = UGuiKit.CreateButton(header, "✕", Hide, UGuiKit.BtnNormal, 26, new Vector2(48f, 44f));
            UGuiKit.SetHeight(close.GetComponent<RectTransform>(), 44f); close.GetComponent<LayoutElement>().preferredWidth = 48f;

            // 标签栏
            _tabsBar = UGuiKit.CreateRow(panel, 10f, 44f);
            _tabsBar.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            BuildTabs();

            // 内容滚动
            var scrollContent = UGuiKit.CreateScroll(panel, "Content", out _, 10f, new RectOffset(6, 6, 6, 6));
            _content = scrollContent;
            var scrollRoot = (RectTransform)scrollContent.parent;
            UGuiKit.SetHeight(scrollRoot, 440f);
            scrollRoot.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;

            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        private void BuildTabs()
        {
            string[] labels = { "音频", "画质", "控制" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = UGuiKit.CreateButton(_tabsBar, labels[i], () => { _tabIndex = idx; RebuildAll(); }, UGuiKit.BtnNormal, 22, new Vector2(120f, 40f));
                UGuiKit.SetHeight(b.GetComponent<RectTransform>(), 40f); b.GetComponent<LayoutElement>().preferredWidth = 120f;
                _tabButtons[i] = b;
            }
        }

        private void RebuildAll()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var img = _tabButtons[i] != null ? _tabButtons[i].targetGraphic as Image : null;
                if (img != null) img.color = (i == _tabIndex) ? UGuiKit.BtnPrimary : UGuiKit.BtnNormal;
            }
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            switch (_tabIndex)
            {
                case 0: BuildAudio(); break;
                case 1: BuildGraphics(); break;
                case 2: BuildControls(); break;
            }
        }

        // ========== 音频 ==========

        private void BuildAudio()
        {
            VolumeRow("主音量", _master, v => { _master = v; ApplyAudio(); });
            VolumeRow("音效 (SFX)", _sfx, v => { _sfx = v; ApplyAudio(); });
            VolumeRow("背景音乐 (BGM)", _bgm, v => { _bgm = v; ApplyAudio(); });
            VolumeRow("界面 (UI)", _ui, v => { _ui = v; ApplyAudio(); });
            Hint("Demo2 音频资源尚未完整接入，部分音效暂未生效。");
        }

        private void VolumeRow(string label, float val, System.Action<float> onChange)
        {
            var row = UGuiKit.CreateRow(_content, 12f, 38f);
            var hl = row.gameObject.GetComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;

            var l = UGuiKit.CreateText(row, label, 22, UGuiKit.TextMain, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(l, 34f); l.GetComponent<LayoutElement>().preferredWidth = 200f;

            var pct = UGuiKit.CreateText(row, $"{val * 100f:F0}%", 20, UGuiKit.TextDim, TextAlignmentOptions.Right);
            var slider = UGuiKit.CreateSlider(row, val, v =>
            {
                onChange(v);
                if (pct != null) pct.text = $"{v * 100f:F0}%";
            }, 340f, 16f);
            UGuiKit.SetHeight(slider, 34f); slider.GetComponent<LayoutElement>().preferredWidth = 340f;
            UGuiKit.SetHeight(pct, 34f); pct.GetComponent<LayoutElement>().preferredWidth = 70f;
        }

        // ========== 画质 ==========

        private void BuildGraphics()
        {
            Section("画质等级");
            var chipRow = UGuiKit.CreateRow(_content, 8f, 44f);
            var hl = chipRow.gameObject.GetComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                int qi = i;
                var b = UGuiKit.CreateButton(chipRow, names[i], () => { _qualityIdx = qi; QualitySettings.SetQualityLevel(qi, true); RebuildAll(); },
                    _qualityIdx == i ? UGuiKit.BtnPrimary : UGuiKit.BtnNormal, 20, new Vector2(120f, 40f));
                UGuiKit.SetHeight(b.GetComponent<RectTransform>(), 40f); b.GetComponent<LayoutElement>().preferredWidth = 120f;
            }

            Section("窗口模式");
            var t = UGuiKit.CreateToggle(_content, "全屏显示", _fullscreen, v => { _fullscreen = v; Screen.fullScreen = v; }, 22);
            UGuiKit.SetHeight(t, 30f);

            Section("当前分辨率");
            var res = UGuiKit.CreateText(_content, $"{Screen.currentResolution.width} × {Screen.currentResolution.height} @ {Screen.currentResolution.refreshRateRatio.value:F0} Hz", 20, UGuiKit.TextDim, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(res, 30f);
        }

        // ========== 控制 ==========

        private static readonly (string label, string keys)[] _bindings =
        {
            ("移动", "W / A / S / D"),
            ("普攻", "鼠标左键"),
            ("瞄准 / 朝向", "鼠标移动"),
            ("Q / E / R 技能", "Q / E / R"),
            ("闪避", "Space"),
            ("拾取 · 交互", "F（长按 F 分解）"),
            ("背包", "Tab"),
            ("服丹", "G"),
            ("暂停菜单", "Esc"),
            ("Debug 控制台", "Tab"),
            ("模块装配", "M"),
        };

        private void BuildControls()
        {
            Section("按键提示（暂不支持自定义键位）");
            foreach (var (label, keys) in _bindings)
            {
                var row = UGuiKit.CreateRow(_content, 12f, 32f);
                var hl = row.gameObject.GetComponent<HorizontalLayoutGroup>();
                hl.childControlWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
                var l = UGuiKit.CreateText(row, label, 20, UGuiKit.TextMain, TextAlignmentOptions.Left);
                UGuiKit.SetHeight(l, 28f); l.GetComponent<LayoutElement>().preferredWidth = 260f;
                var k = UGuiKit.CreateText(row, keys, 20, UGuiKit.Gold, TextAlignmentOptions.Left);
                UGuiKit.SetHeight(k, 28f); k.GetComponent<LayoutElement>().preferredWidth = 360f;
            }
            Hint("未来计划接入 InputSystem 的 PlayerInput rebind。");
        }

        // ========== 小工具 ==========

        private void Section(string text)
        {
            var l = UGuiKit.CreateText(_content, text, 22, UGuiKit.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(l, 34f);
        }

        private void Hint(string text)
        {
            var l = UGuiKit.CreateText(_content, text, 18, UGuiKit.TextDim, TextAlignmentOptions.Left);
            l.enableWordWrapping = true;
            UGuiKit.SetHeight(l, 44f);
        }
    }
}
