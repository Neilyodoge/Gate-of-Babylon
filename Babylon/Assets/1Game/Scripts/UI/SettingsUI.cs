using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 设置面板（v0.5 Week 9）—— 音量 / 画质 / 键位提示。
    ///
    /// 音量直接绑定 <see cref="AudioConfig"/> 的 4 个 float 字段。
    /// 画质 / 分辨率用 Unity 内置 API。
    /// 键位提示是只读列表（v0.5 没做 InputSystem rebind）。
    ///
    /// 设置保存：使用 PlayerPrefs（音量 + 画质等级），下次启动时 OnEnable 自动加载。
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private static SettingsUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private int _tabIndex;  // 0=音量 1=画质 2=控制
        private Vector2 _scroll;

        // PlayerPrefs key
        private const string K_MASTER = "Setting.MasterVol";
        private const string K_SFX = "Setting.SfxVol";
        private const string K_BGM = "Setting.BgmVol";
        private const string K_UI = "Setting.UiVol";
        private const string K_QUALITY = "Setting.Quality";
        private const string K_FULLSCREEN = "Setting.Fullscreen";

        // 缓存当前值（编辑时实时刷新 AudioConfig）
        private float _master, _sfx, _bgm, _ui;
        private int _qualityIdx;
        private bool _fullscreen;

        private AudioConfig _audioConfig;

        // 样式
        private GUIStyle _titleStyle, _tabStyle, _tabActiveStyle, _labelStyle, _bigLabelStyle, _maskStyle;
        private Texture2D _maskTex;
        private bool _stylesReady;

        public static void Show()
        {
            if (_instance == null)
            {
                var go = new GameObject("SettingsUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SettingsUI>();
            }
            _instance._visible = true;
            _instance.LoadValues();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance.SaveValues();
        }

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

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _maskTex = new Texture2D(1, 1);
            _maskTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.78f));
            _maskTex.Apply();
            _maskStyle = new GUIStyle();
            _maskStyle.normal.background = _maskTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.92f, 0.78f);

            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _tabActiveStyle = new GUIStyle(_tabStyle);
            _tabActiveStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft, richText = true
            };
            _labelStyle.normal.textColor = new Color(0.88f, 0.90f, 0.95f);

            _bigLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _bigLabelStyle.normal.textColor = new Color(1f, 0.92f, 0.65f);

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _maskStyle);

            const float W = 620f, H = 480f;
            float x = (Screen.width - W) * 0.5f;
            float y = (Screen.height - H) * 0.5f;

            GUI.Box(new Rect(x, y, W, H), "");

            GUI.Label(new Rect(x, y + 14f, W, 36f), "⚙ 设置", _titleStyle);

            // Tab 切换
            const float TabW = 120f, TabH = 32f;
            float tabX = x + (W - TabW * 3 - 16f) * 0.5f;
            float tabY = y + 60f;
            DrawTab(tabX, tabY, TabW, TabH, "🔊 音频", 0);
            DrawTab(tabX + TabW + 8f, tabY, TabW, TabH, "🖥 画质", 1);
            DrawTab(tabX + (TabW + 8f) * 2, tabY, TabW, TabH, "⌨ 控制", 2);

            // 内容区
            var contentRect = new Rect(x + 20f, y + 110f, W - 40f, H - 170f);
            GUILayout.BeginArea(contentRect);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tabIndex)
            {
                case 0: DrawAudioTab(); break;
                case 1: DrawGraphicsTab(); break;
                case 2: DrawControlsTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 关闭按钮
            if (GUI.Button(new Rect(x + W - 120f, y + H - 50f, 100f, 36f), "关闭"))
            {
                Hide();
            }

            // ESC 关闭
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Hide();
                Event.current.Use();
            }
        }

        private void DrawTab(float x, float y, float w, float h, string label, int idx)
        {
            var style = _tabIndex == idx ? _tabActiveStyle : _tabStyle;
            if (GUI.Button(new Rect(x, y, w, h), label, style))
            {
                _tabIndex = idx;
                _scroll = Vector2.zero;
            }
            if (_tabIndex == idx)
            {
                // 底部高亮线
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        // ========== 音量 Tab ==========

        private void DrawAudioTab()
        {
            GUILayout.Label("主音量", _bigLabelStyle);
            DrawVolumeSlider(ref _master, () => ApplyAudio());

            GUILayout.Space(8);
            GUILayout.Label("音效（SFX）", _bigLabelStyle);
            DrawVolumeSlider(ref _sfx, () => ApplyAudio());

            GUILayout.Space(8);
            GUILayout.Label("背景音乐（BGM）", _bigLabelStyle);
            DrawVolumeSlider(ref _bgm, () => ApplyAudio());

            GUILayout.Space(8);
            GUILayout.Label("界面（UI）", _bigLabelStyle);
            DrawVolumeSlider(ref _ui, () => ApplyAudio());

            GUILayout.Space(14);
            GUILayout.Label("<i>Demo2 音频资源还未完整接入，部分音效暂未生效。</i>", _labelStyle);
        }

        private void DrawVolumeSlider(ref float val, System.Action onChange)
        {
            GUILayout.BeginHorizontal();
            float newVal = GUILayout.HorizontalSlider(val, 0f, 1f, GUILayout.Width(380));
            GUILayout.Space(8);
            GUILayout.Label($"{(newVal * 100f):F0}%", _labelStyle, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(newVal - val) > 0.001f)
            {
                val = newVal;
                onChange?.Invoke();
            }
        }

        // ========== 画质 Tab ==========

        private void DrawGraphicsTab()
        {
            GUILayout.Label("画质等级", _bigLabelStyle);
            string[] qualityNames = QualitySettings.names;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < qualityNames.Length; i++)
            {
                bool isActive = _qualityIdx == i;
                var prev = GUI.color;
                if (isActive) GUI.color = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button(qualityNames[i], GUILayout.Width(100), GUILayout.Height(34)))
                {
                    _qualityIdx = i;
                    QualitySettings.SetQualityLevel(_qualityIdx, true);
                }
                GUI.color = prev;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14);
            GUILayout.Label("窗口模式", _bigLabelStyle);
            bool newFs = GUILayout.Toggle(_fullscreen, "  全屏显示", _labelStyle);
            if (newFs != _fullscreen)
            {
                _fullscreen = newFs;
                Screen.fullScreen = newFs;
            }

            GUILayout.Space(14);
            GUILayout.Label("当前分辨率", _bigLabelStyle);
            GUILayout.Label($"{Screen.currentResolution.width} × {Screen.currentResolution.height} @ {Screen.currentResolution.refreshRateRatio.value:F0} Hz", _labelStyle);
        }

        // ========== 控制 Tab ==========

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

        private void DrawControlsTab()
        {
            GUILayout.Label("按键提示（v0.5 暂不支持自定义键位）", _bigLabelStyle);
            GUILayout.Space(6);

            foreach (var (label, keys) in _bindings)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(label, _labelStyle, GUILayout.Width(220));
                GUILayout.Label($"<color=#ffd47a>{keys}</color>", _labelStyle, GUILayout.Width(280));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("<i>未来 Demo3 计划接入 InputSystem 的 PlayerInput rebind。</i>", _labelStyle);
        }
    }
}
