using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 设置面板（v0.6 改 UI Toolkit）—— 音频 / 画质 / 控制。
    ///
    /// 结构 Resources/UI/SettingsUI.uxml，样式 SettingsUI.uss，复用 AvatarSelectPanelSettings。
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

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _tabsBar;
        private ScrollView _content;
        private readonly Button[] _tabButtons = new Button[3];

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._visible = true;
            _instance.LoadValues();
            _instance.RebuildAll();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance.SaveValues();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
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

        // ========== UITK 构建 ==========

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/SettingsUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 14f;   // 可从暂停/主菜单上方打开

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);
            // 样式经 UXML <Style src> 随 VisualTreeAsset 引用加载（避免 Resources 名称索引偶发空规则缓存）。

            _overlay = root.Q<VisualElement>("overlay");
            _tabsBar = root.Q<VisualElement>("tabs");
            _content = root.Q<ScrollView>("content");
            if (_content != null)
            {
                _content.mode = ScrollViewMode.Vertical;
                _content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            var note = root.Q<Label>("note");
            if (note != null) note.text = "设置自动保存";
            var close = root.Q<Button>("close");
            if (close != null) close.clicked += Hide;

            BuildTabs();
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        private void BuildTabs()
        {
            if (_tabsBar == null) return;
            _tabsBar.Clear();
            string[] labels = { "音频", "画质", "控制" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = new Button(() => { _tabIndex = idx; RebuildAll(); }) { text = labels[i] };
                b.AddToClassList("st-tab");
                _tabsBar.Add(b);
                _tabButtons[i] = b;
            }
        }

        private void RebuildAll()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null) _tabButtons[i].EnableInClassList("st-tab--active", i == _tabIndex);
            }
            if (_content == null) return;
            _content.Clear();
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
            _content.Add(VolumeRow("主音量", _master, v => { _master = v; ApplyAudio(); }));
            _content.Add(VolumeRow("音效 (SFX)", _sfx, v => { _sfx = v; ApplyAudio(); }));
            _content.Add(VolumeRow("背景音乐 (BGM)", _bgm, v => { _bgm = v; ApplyAudio(); }));
            _content.Add(VolumeRow("界面 (UI)", _ui, v => { _ui = v; ApplyAudio(); }));
            _content.Add(Hint("Demo2 音频资源尚未完整接入，部分音效暂未生效。"));
        }

        private VisualElement VolumeRow(string label, float val, System.Action<float> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("st-row");
            var l = new Label(label);
            l.AddToClassList("st-label");
            row.Add(l);
            var slider = new Slider(0f, 1f) { value = val };
            slider.AddToClassList("st-slider");
            var pct = new Label($"{val * 100f:F0}%");
            pct.AddToClassList("st-pct");
            slider.RegisterValueChangedCallback(e =>
            {
                onChange(e.newValue);
                pct.text = $"{e.newValue * 100f:F0}%";
            });
            row.Add(slider);
            row.Add(pct);
            return row;
        }

        // ========== 画质 ==========

        private void BuildGraphics()
        {
            _content.Add(Section("画质等级"));
            var chipRow = new VisualElement();
            chipRow.AddToClassList("st-chiprow");
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                int qi = i;
                var b = new Button(() => { _qualityIdx = qi; QualitySettings.SetQualityLevel(qi, true); RebuildAll(); }) { text = names[i] };
                b.AddToClassList("st-chip");
                if (_qualityIdx == i) b.AddToClassList("st-chip--active");
                chipRow.Add(b);
            }
            _content.Add(chipRow);

            _content.Add(Section("窗口模式"));
            var fs = new Toggle("全屏显示") { value = _fullscreen };
            fs.AddToClassList("st-toggle");
            fs.RegisterValueChangedCallback(e => { _fullscreen = e.newValue; Screen.fullScreen = e.newValue; });
            _content.Add(fs);

            _content.Add(Section("当前分辨率"));
            var res = new Label($"{Screen.currentResolution.width} × {Screen.currentResolution.height} @ {Screen.currentResolution.refreshRateRatio.value:F0} Hz");
            res.AddToClassList("st-label");
            res.style.width = StyleKeyword.Auto;
            _content.Add(res);
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
            ("Debug 控制台", "F1"),
            ("化身狂火激活（业火）", "V"),
            ("渡劫 / 心魔劫触发", "V / B"),
        };

        private void BuildControls()
        {
            _content.Add(Section("按键提示（暂不支持自定义键位）"));
            foreach (var (label, keys) in _bindings)
            {
                var row = new VisualElement();
                row.AddToClassList("st-keyrow");
                var l = new Label(label);
                l.AddToClassList("st-keyname");
                row.Add(l);
                var k = new Label(keys);
                k.AddToClassList("st-keyval");
                row.Add(k);
                _content.Add(row);
            }
            _content.Add(Hint("未来计划接入 InputSystem 的 PlayerInput rebind。"));
        }

        // ========== 小工具 ==========

        private static Label Section(string text)
        {
            var l = new Label(text);
            l.AddToClassList("st-section");
            return l;
        }

        private static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("st-hint");
            return l;
        }
    }
}
