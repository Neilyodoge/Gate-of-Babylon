using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using Yarn.Unity;

namespace XianTu
{
    /// <summary>
    /// 序章Yarn对话表现层。逐字显示后由玩家点击继续，
    /// 并保留旧短字幕入口作为异常回退。
    /// </summary>
    public sealed class StarterPrologueDialogueHUD : MonoBehaviour
    {
        private static StarterPrologueDialogueHUD _instance;

        private RectTransform _panel;
        private CanvasGroup _group;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _line;
        private Button _continueButton;
        private TextMeshProUGUI _continueLabel;
        private float _visibleSeconds;
        private float _remaining;
        private float _typewriterProgress;
        private float _autoContinueRemaining;
        private Action _onContinue;
        private bool _manualLine;
        private bool _dialogueMode;
        private bool _fullyRevealed;
        private bool _advanceRequested;
        private float _inputArmTime;
        private IDisposable _anyButtonSubscription;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public const float CharactersPerSecond = 20f;
        public const float AutoContinueSeconds = 45f;

        public static void Show(
            string speaker,
            string line,
            float visibleSeconds = 2.2f)
        {
            EnsureInstance();
            _instance.ShowInternal(
                speaker,
                line,
                visibleSeconds);
        }

        public static StarterPrologueDialogueHUD EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject root = new(
                "StarterPrologueDialogueHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 131;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = root.AddComponent<
                StarterPrologueDialogueHUD>();
            _instance.Build();
            UGuiKit.EnsureEventSystem();
            return _instance;
        }

        public void BeginDialogueMode()
        {
            if (_dialogueMode)
                return;
            _dialogueMode = true;
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _anyButtonSubscription =
                InputSystem.onAnyButtonPress.Call(
                    OnAnyButtonPressed);
        }

        public void PresentLine(
            string speaker,
            string line,
            Action onContinue)
        {
            _speaker.text = speaker ?? string.Empty;
            _line.text = line ?? string.Empty;
            _line.maxVisibleCharacters = 0;
            _typewriterProgress = 0f;
            _fullyRevealed = string.IsNullOrEmpty(_line.text);
            _autoContinueRemaining = AutoContinueSeconds;
            _onContinue = onContinue;
            _manualLine = true;
            _advanceRequested = false;
            _inputArmTime = Time.unscaledTime + 0.12f;
            _remaining = -1f;
            _continueButton.gameObject.SetActive(true);
            _continueLabel.text =
                _fullyRevealed
                    ? "任意键继续  ▶"
                    : "任意键显示全文";
            _group.alpha = 1f;
            _panel.gameObject.SetActive(true);
        }

        public void EndDialogueMode()
        {
            _onContinue = null;
            _manualLine = false;
            _advanceRequested = false;
            _anyButtonSubscription?.Dispose();
            _anyButtonSubscription = null;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            if (!_dialogueMode)
                return;
            _dialogueMode = false;
            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private void Build()
        {
            GameObject panelObject = new(
                "DialoguePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax =
                new Vector2(0.5f, 0.18f);
            _panel.sizeDelta = new Vector2(820f, 132f);

            Image background = panelObject.GetComponent<Image>();
            background.color =
                new Color(0.045f, 0.055f, 0.05f, 0.88f);
            background.raycastTarget = false;
            _group = panelObject.GetComponent<CanvasGroup>();
            _group.interactable = true;
            _group.blocksRaycasts = true;

            _speaker = CreateText(
                _panel,
                "Speaker",
                new Vector2(28f, 88f),
                new Vector2(-24f, -8f),
                17f,
                FontStyles.Bold);
            _speaker.color =
                new Color(0.96f, 0.76f, 0.38f);
            _line = CreateText(
                _panel,
                "Line",
                new Vector2(28f, 18f),
                new Vector2(-190f, -42f),
                23f,
                FontStyles.Normal);
            _line.color =
                new Color(0.95f, 0.96f, 0.91f);
            _continueButton = UGuiKit.CreateButton(
                _panel,
                "继续  ▶",
                OnContinueRequested,
                out _continueLabel,
                new Color(0.24f, 0.34f, 0.28f, 1f),
                16,
                new Vector2(152f, 42f));
            RectTransform continueRect =
                (RectTransform)_continueButton.transform;
            continueRect.anchorMin = continueRect.anchorMax =
                new Vector2(1f, 0f);
            continueRect.pivot = new Vector2(1f, 0f);
            continueRect.anchoredPosition =
                new Vector2(-18f, 14f);
            _continueButton.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
        }

        private void ShowInternal(
            string speaker,
            string line,
            float visibleSeconds)
        {
            _speaker.text = speaker ?? string.Empty;
            _line.text = line ?? string.Empty;
            _line.maxVisibleCharacters = int.MaxValue;
            _visibleSeconds = Mathf.Max(0.5f, visibleSeconds);
            _remaining = _visibleSeconds;
            _manualLine = false;
            _continueButton.gameObject.SetActive(false);
            _group.alpha = 0f;
            _panel.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_manualLine && _panel.gameObject.activeSelf)
            {
                UpdateManualLine();
                return;
            }
            if (_remaining <= 0f ||
                !_panel.gameObject.activeSelf)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            float elapsed = _visibleSeconds - _remaining;
            float fadeIn = Mathf.Clamp01(elapsed / 0.15f);
            float fadeOut = Mathf.Clamp01(_remaining / 0.25f);
            _group.alpha = Mathf.Min(fadeIn, fadeOut);
            if (_remaining <= 0f)
                _panel.gameObject.SetActive(false);
        }

        private void UpdateManualLine()
        {
            if (_advanceRequested)
            {
                _advanceRequested = false;
                OnContinueRequested();
            }

            if (!_fullyRevealed)
            {
                _typewriterProgress +=
                    Time.unscaledDeltaTime * CharactersPerSecond;
                _line.maxVisibleCharacters =
                    Mathf.Min(
                        _line.textInfo.characterCount,
                        Mathf.FloorToInt(_typewriterProgress));
                if (_line.maxVisibleCharacters >=
                    _line.textInfo.characterCount)
                {
                    RevealAll();
                }
            }
            else
            {
                _autoContinueRemaining -= Time.unscaledDeltaTime;
                if (_autoContinueRemaining <= 0f)
                    CompleteManualLine();
            }

        }

        private void OnAnyButtonPressed(InputControl control)
        {
            if (!_manualLine ||
                Time.unscaledTime < _inputArmTime)
            {
                return;
            }
            _advanceRequested = true;
        }

        private void OnContinueRequested()
        {
            if (!_manualLine)
                return;
            if (!_fullyRevealed)
            {
                RevealAll();
                return;
            }
            CompleteManualLine();
        }

        private void RevealAll()
        {
            _fullyRevealed = true;
            _line.maxVisibleCharacters = int.MaxValue;
            _continueLabel.text = "任意键继续  ▶";
        }

        private void CompleteManualLine()
        {
            Action continuation = _onContinue;
            _onContinue = null;
            _manualLine = false;
            _continueButton.gameObject.SetActive(false);
            continuation?.Invoke();
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            FontStyles style)
        {
            GameObject go = new(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect =
                go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text =
                go.GetComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                text.font = UGuiKit.CjkFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment =
                TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private void OnDestroy()
        {
            _anyButtonSubscription?.Dispose();
            if (_instance == this)
                _instance = null;
        }
    }

    /// <summary>把Yarn行内容接入ProjectR序章对话面板。</summary>
    public sealed class StarterPrologueYarnPresenter :
        DialoguePresenterBase
    {
        public override YarnTask OnDialogueStartedAsync()
        {
            StarterPrologueDialogueHUD
                .EnsureInstance()
                .BeginDialogueMode();
            return YarnTask.CompletedTask;
        }

        public override YarnTask RunLineAsync(
            LocalizedLine line,
            LineCancellationToken token)
        {
            YarnTaskCompletionSource completion = new();
            void CompleteThisLine()
            {
                completion.TrySetResult();
            }
            string speaker = line.CharacterName ?? string.Empty;
            string text = line.TextWithoutCharacterName.Text;
            StarterPrologueDialogueHUD.EnsureInstance().PresentLine(
                speaker,
                text,
                CompleteThisLine);
            token.NextContentToken.Register(CompleteThisLine);
            return completion.Task;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            StarterPrologueDialogueHUD
                .EnsureInstance()
                .EndDialogueMode();
            return YarnTask.CompletedTask;
        }
    }
}
