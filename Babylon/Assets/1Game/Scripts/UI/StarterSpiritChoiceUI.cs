using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 新手初契的场景实体选择界面。由序章场景显式打开，
    /// 不会在Demo1或其他战斗场景自动弹出。
    /// </summary>
    public sealed class StarterSpiritChoiceUI : MonoBehaviour
    {
        private static StarterSpiritChoiceUI _instance;

        private GameObject _root;
        private GameObject _confirmRoot;
        private TextMeshProUGUI _nameLabel;
        private TextMeshProUGUI _descriptionLabel;
        private TextMeshProUGUI _hintLabel;
        private TextMeshProUGUI _confirmLabel;
        private Image _profilePortrait;
        private Image _confirmPortrait;
        private readonly List<StarterSpiritChoiceWorldEntity>
            _entities = new();
        private readonly List<Canvas> _hiddenHudCanvases = new();
        private StarterSpiritChoiceSession _session;
        private Action<StableConfigId, SpiritInstanceState> _onChosen;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public static bool IsOpen =>
            _instance != null &&
            _instance._root != null &&
            _instance._root.activeSelf;

        public static bool ShowForScene(
            Action<StableConfigId, SpiritInstanceState> onChosen = null)
        {
            if (!string.IsNullOrWhiteSpace(
                    SaveSystem.Instance.Data.starterSpiritSpeciesId))
            {
                return false;
            }

            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsOfType<StarterSpiritChoiceWorldEntity>(true);
            EnsureInstance();
            return _instance.Open(entities, onChosen);
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance.HideInternal();
        }

        public static bool HasCompleteEntitySet(
            IReadOnlyList<StarterSpiritChoiceWorldEntity> entities)
        {
            return TryBuildEntitySet(entities, null);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;
            var go = new GameObject("StarterSpiritChoiceUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StarterSpiritChoiceUI>();
            _instance.Build();
        }

        private void OnEnable()
        {
            StarterSpiritChoiceWorldEntity.HoverEntered +=
                OnEntityHoverEntered;
            StarterSpiritChoiceWorldEntity.HoverExited +=
                OnEntityHoverExited;
            StarterSpiritChoiceWorldEntity.Clicked += OnEntityClicked;
        }

        private void OnDisable()
        {
            StarterSpiritChoiceWorldEntity.HoverEntered -=
                OnEntityHoverEntered;
            StarterSpiritChoiceWorldEntity.HoverExited -=
                OnEntityHoverExited;
            StarterSpiritChoiceWorldEntity.Clicked -= OnEntityClicked;
        }

        private void OnDestroy()
        {
            if (IsOpen)
                RestoreInteraction();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!IsOpen ||
                _session.Stage !=
                StarterSpiritChoiceStage.Confirming)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelConfirmation();
            }
        }

        private bool Open(
            IReadOnlyList<StarterSpiritChoiceWorldEntity> entities,
            Action<StableConfigId, SpiritInstanceState> onChosen)
        {
            if (!TryCollectEntities(entities))
            {
                Debug.LogWarning(
                    "[StarterSpiritChoiceUI] 需要且仅需要火花狸、响响鸮、弹弹胶各一个场景实体。");
                return false;
            }

            _session = new StarterSpiritChoiceSession();
            _onChosen = onChosen;
            _nameLabel.text = "看看它们";
            _descriptionLabel.text =
                "靠近或点击一只灵宠，只会看到少量性格与战斗倾向。";
            _hintLabel.text = "点击场景中的灵宠进行选择";
            _profilePortrait.gameObject.SetActive(false);
            _confirmPortrait.gameObject.SetActive(false);
            _confirmRoot.SetActive(false);
            _root.SetActive(true);
            HideCombatHud();

            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetFocused(default);
            return true;
        }

        private bool TryCollectEntities(
            IReadOnlyList<StarterSpiritChoiceWorldEntity> entities)
        {
            _entities.Clear();
            return TryBuildEntitySet(entities, _entities);
        }

        private static bool TryBuildEntitySet(
            IReadOnlyList<StarterSpiritChoiceWorldEntity> entities,
            List<StarterSpiritChoiceWorldEntity> output)
        {
            if (entities == null)
                return false;

            int configuredCount = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                StarterSpiritChoiceWorldEntity entity = entities[i];
                if (entity == null || !entity.IsConfigured)
                    continue;
                if (!StarterSpiritChoice.IsOption(entity.SpeciesId))
                    return false;
                configuredCount++;
            }
            if (configuredCount !=
                StarterSpiritChoicePresentation.Profiles.Count)
            {
                return false;
            }

            foreach (StarterSpiritChoiceProfile profile in
                     StarterSpiritChoicePresentation.Profiles)
            {
                StarterSpiritChoiceWorldEntity match = null;
                for (int i = 0; i < entities.Count; i++)
                {
                    StarterSpiritChoiceWorldEntity candidate = entities[i];
                    if (candidate == null ||
                        !candidate.IsConfigured ||
                        candidate.SpeciesId != profile.SpeciesId)
                    {
                        continue;
                    }
                    if (match != null)
                        return false;
                    match = candidate;
                }

                if (match == null)
                    return false;
                output?.Add(match);
            }
            return true;
        }

        private void OnEntityHoverEntered(
            StarterSpiritChoiceWorldEntity entity)
        {
            if (!IsOpen || entity == null ||
                !_session.TryFocus(entity.SpeciesId))
            {
                return;
            }
            ShowProfile(entity.SpeciesId);
        }

        private void OnEntityHoverExited(
            StarterSpiritChoiceWorldEntity entity)
        {
            if (!IsOpen || entity == null ||
                _session.Stage != StarterSpiritChoiceStage.Browsing ||
                _session.FocusedSpecies != entity.SpeciesId)
            {
                return;
            }
            SetFocused(default);
        }

        private void OnEntityClicked(
            StarterSpiritChoiceWorldEntity entity)
        {
            if (!IsOpen || entity == null ||
                !_session.BeginConfirmation(entity.SpeciesId))
            {
                return;
            }

            ShowProfile(entity.SpeciesId);
            StarterSpiritChoicePresentation.TryGetProfile(
                entity.SpeciesId,
                out StarterSpiritChoiceProfile profile);
            _confirmLabel.text =
                $"确定与 <color=#F2B45E>{profile.DisplayName}</color> 结契吗？\n" +
                "另外两只会离开，之后仍可在秘境中相遇。";
            SetPortrait(_confirmPortrait, entity.SpeciesId);
            _confirmRoot.SetActive(true);
        }

        private void ConfirmChoice()
        {
            StarterSpiritChoiceSubmitResult result =
                _session.Commit(
                    SaveSystem.Instance.Data,
                    out SpiritInstanceState spirit);
            if (result != StarterSpiritChoiceSubmitResult.Success)
            {
                _confirmRoot.SetActive(false);
                _hintLabel.text =
                    $"无法完成初契：{_session.LastChoiceResult}";
                return;
            }

            StableConfigId species = _session.FocusedSpecies;
            SaveSystem.Instance.Save();
            Action<StableConfigId, SpiritInstanceState> callback =
                _onChosen;
            HideInternal();
            callback?.Invoke(species, spirit);
        }

        private void CancelConfirmation()
        {
            if (!_session.CancelConfirmation())
                return;
            _confirmRoot.SetActive(false);
            ShowProfile(_session.FocusedSpecies);
            _hintLabel.text = "尚未结契，可以继续看看";
        }

        private void ShowProfile(StableConfigId species)
        {
            if (!StarterSpiritChoicePresentation.TryGetProfile(
                    species,
                    out StarterSpiritChoiceProfile profile))
            {
                return;
            }

            _nameLabel.text = profile.DisplayName;
            _descriptionLabel.text =
                $"{profile.PersonalityLine}\n" +
                $"<color=#D6C397>{profile.CombatTendency}</color>";
            _hintLabel.text = "点击它，查看结契确认";
            SetPortrait(_profilePortrait, species);
            SetFocused(species);
        }

        private void SetFocused(StableConfigId species)
        {
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity != null)
                    entity.SetFocused(
                        !species.IsEmpty &&
                        entity.SpeciesId == species);
            }
        }

        private void HideInternal()
        {
            if (_root == null || !_root.activeSelf)
                return;
            SetFocused(default);
            _confirmRoot.SetActive(false);
            _root.SetActive(false);
            _entities.Clear();
            _session = null;
            _onChosen = null;
            RestoreInteraction();
        }

        private void RestoreInteraction()
        {
            foreach (Canvas canvas in _hiddenHudCanvases)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }
            _hiddenHudCanvases.Clear();
            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private void HideCombatHud()
        {
            _hiddenHudCanvases.Clear();
            foreach (GameHUD hud in FindObjectsOfType<GameHUD>(true))
            {
                Canvas canvas = hud.GetComponent<Canvas>();
                if (canvas == null || !canvas.enabled)
                    continue;
                canvas.enabled = false;
                _hiddenHudCanvases.Add(canvas);
            }
        }

        private void Build()
        {
            Canvas canvas = UGuiKit.CreateOverlayCanvas(
                "StarterSpiritChoiceCanvas",
                145,
                transform);
            _root = canvas.gameObject;

            RectTransform titlePanel = UGuiKit.CreatePanel(
                _root.transform,
                "TitlePanel",
                new Vector2(720f, 104f),
                new Color(0.12f, 0.11f, 0.08f, 0.82f));
            titlePanel.anchorMin = titlePanel.anchorMax =
                new Vector2(0.5f, 1f);
            titlePanel.pivot = new Vector2(0.5f, 1f);
            titlePanel.anchoredPosition = new Vector2(0f, -18f);
            TextMeshProUGUI title = UGuiKit.CreateText(
                titlePanel,
                "选择与你结契的伙伴",
                30,
                new Color(0.96f, 0.82f, 0.52f),
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            SetRect(title.rectTransform, 20f, 44f, -20f, 96f);
            TextMeshProUGUI subtitle = UGuiKit.CreateText(
                titlePanel,
                "火花狸 · 响响鸮 · 弹弹胶",
                16,
                new Color(0.78f, 0.72f, 0.61f));
            SetRect(subtitle.rectTransform, 20f, 8f, -20f, 40f);

            RectTransform infoPanel = UGuiKit.CreatePanel(
                _root.transform,
                "InfoPanel",
                new Vector2(720f, 176f),
                new Color(0.11f, 0.10f, 0.08f, 0.90f));
            infoPanel.anchorMin = infoPanel.anchorMax =
                new Vector2(0.5f, 0f);
            infoPanel.pivot = new Vector2(0.5f, 0f);
            infoPanel.anchoredPosition = new Vector2(0f, 76f);

            _nameLabel = UGuiKit.CreateText(
                infoPanel,
                "",
                26,
                new Color(0.95f, 0.77f, 0.42f),
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            SetRect(_nameLabel.rectTransform, 146f, 120f, -20f, 162f);
            _profilePortrait = CreatePortrait(
                infoPanel,
                "SpiritPortrait",
                new Vector2(22f, 30f),
                112f);
            _descriptionLabel = UGuiKit.CreateText(
                infoPanel,
                "",
                17,
                new Color(0.92f, 0.88f, 0.78f));
            SetRect(
                _descriptionLabel.rectTransform,
                146f,
                48f,
                -20f,
                120f);
            _hintLabel = UGuiKit.CreateText(
                infoPanel,
                "",
                14,
                new Color(0.68f, 0.78f, 0.72f));
            SetRect(_hintLabel.rectTransform, 20f, 8f, -20f, 44f);

            BuildConfirmation();
            _root.SetActive(false);
        }

        private void BuildConfirmation()
        {
            _confirmRoot = UGuiKit.CreateStretch(
                _root.transform,
                "ConfirmRoot").gameObject;
            UGuiKit.CreateScrim(
                _confirmRoot.transform,
                new Color(0.04f, 0.035f, 0.025f, 0.72f));
            RectTransform panel = UGuiKit.CreatePanel(
                _confirmRoot.transform,
                "ConfirmPanel",
                new Vector2(620f, 280f),
                new Color(0.16f, 0.14f, 0.10f, 0.98f));

            _confirmLabel = UGuiKit.CreateText(
                panel,
                "",
                21,
                new Color(0.94f, 0.90f, 0.80f));
            SetRect(_confirmLabel.rectTransform, 166f, 112f, -40f, 242f);
            _confirmPortrait = CreatePortrait(
                panel,
                "ConfirmPortrait",
                new Vector2(38f, 118f),
                104f);

            Button confirm = UGuiKit.CreateButton(
                panel,
                "确认结契",
                ConfirmChoice,
                new Color(0.52f, 0.34f, 0.16f, 1f),
                18,
                new Vector2(210f, 52f));
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = confirmRect.anchorMax =
                new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(-120f, 42f);

            Button cancel = UGuiKit.CreateButton(
                panel,
                "再看看",
                CancelConfirmation,
                new Color(0.25f, 0.24f, 0.20f, 1f),
                18,
                new Vector2(210f, 52f));
            RectTransform cancelRect = cancel.GetComponent<RectTransform>();
            cancelRect.anchorMin = cancelRect.anchorMax =
                new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(120f, 42f);
            _confirmRoot.SetActive(false);
        }

        private static Image CreatePortrait(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor =
                new Color(0.83f, 0.66f, 0.34f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            go.SetActive(false);
            return image;
        }

        private static void SetPortrait(
            Image image,
            StableConfigId species)
        {
            Sprite sprite =
                ProjectRUITheme.Instance?.SpiritPortrait(species);
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
        }

        private static void SetRect(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            float parentHeight =
                ((RectTransform)rect.parent).rect.height;
            rect.offsetMax = new Vector2(
                right,
                top - parentHeight);
        }
    }
}
