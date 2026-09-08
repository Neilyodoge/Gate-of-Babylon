using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 首批局内天赋选择原型。获得天赋点后暂停战斗并依次处理出战灵宠。
    /// </summary>
    public sealed class SpiritTalentChoiceHUD : MonoBehaviour
    {
        private static SpiritTalentChoiceHUD _instance;
        private readonly List<Button> _buttons = new();
        private readonly List<Button> _treeTabs = new();
        private readonly List<Image> _treeTabPortraits = new();
        private readonly List<Button> _treeNodes = new();
        private readonly List<RawImage> _treeNodeIcons = new();
        private readonly List<TextMeshProUGUI> _treeNodeFallbacks = new();
        private readonly List<TextMeshProUGUI> _treeRouteLabels = new();
        private readonly List<TreeConnector> _treeConnectors = new();
        private GameObject _panel;
        private GameObject _treePanel;
        private GameObject _treeScrim;
        private RectTransform _treeGuideLineRoot;
        private RectTransform _treeLineRoot;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _progress;
        private TextMeshProUGUI _treeTitle;
        private TextMeshProUGUI _treeProgress;
        private TextMeshProUGUI _treeDetail;
        private Image _treeSpiritArt;
        private RawImage _treeDetailIcon;
        private Button _resetButton;
        private Button _confirmButton;
        private Button _viewTreeButton;
        private Button _treeResetButton;
        private Button _treeActivateButton;
        private SpiritTalentRunController _controller;
        private Guid _currentSpiritId;
        private StableConfigId _selectedTalentId;
        private float _previousTimeScale = 1f;
        private bool _selectionAllowed;
        private bool _choiceScanPending;
        private bool _treeOpenedFromChoice;
        private StableConfigId _renderedTreeSpecies;

        private sealed class TreeConnector
        {
            public StableConfigId From;
            public StableConfigId To;
            public int Branch;
            public Image Image;
        }

        public static bool TryOpenTree(Guid spiritInstanceId)
        {
            return _instance != null &&
                   _instance.OpenTree(spiritInstanceId, false);
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.SpiritTalentProgressed>(
                OnTalentProgressed);
            GameEvents.Subscribe<GameEvents.SpiritTalentActivated>(
                OnTalentActivated);
            GameEvents.Subscribe<GameEvents.SpiritTalentsReset>(
                OnTalentsReset);
            GameEvents.Subscribe<GameEvents.RoomCleared>(
                OnRoomCleared);
        }

        private void Start()
        {
            Build();
            FindController();
        }

        private void Update()
        {
            if (_controller == null)
                FindController();
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if ((_panel?.activeSelf == true ||
                 _treePanel?.activeSelf == true) &&
                keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_treePanel?.activeSelf == true)
                    ReturnFromTree();
                return;
            }
            if (_panel != null &&
                !_panel.activeSelf &&
                _controller != null &&
                _selectionAllowed &&
                _choiceScanPending)
            {
                TryShowNextChoice();
            }
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.SpiritTalentProgressed>(
                OnTalentProgressed);
            GameEvents.Unsubscribe<GameEvents.SpiritTalentActivated>(
                OnTalentActivated);
            GameEvents.Unsubscribe<GameEvents.SpiritTalentsReset>(
                OnTalentsReset);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(
                OnRoomCleared);
            Close();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnTalentProgressed(
            GameEvents.SpiritTalentProgressed evt)
        {
            if (evt.GainedLevels > 0 && _selectionAllowed)
            {
                _choiceScanPending = true;
                TryShowNextChoice();
            }
        }

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            _selectionAllowed = true;
            _choiceScanPending = true;
            TryShowNextChoice();
        }

        private void OnTalentActivated(
            GameEvents.SpiritTalentActivated evt)
        {
            if (evt.SpiritInstanceId != _currentSpiritId)
                return;

            RefreshCurrent();
            RefreshTree();
        }

        private void OnTalentsReset(GameEvents.SpiritTalentsReset evt)
        {
            if (evt.SpiritInstanceId == _currentSpiritId)
            {
                RefreshCurrent();
                RefreshTree();
            }
        }

        private void FindController()
        {
            _controller = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<
                    SpiritTalentRunController>()
                : FindObjectOfType<SpiritTalentRunController>();
        }

        private void TryShowNextChoice()
        {
            if (_panel == null ||
                _panel.activeSelf ||
                _controller == null)
            {
                return;
            }

            _choiceScanPending = false;
            foreach (KeyValuePair<Guid, SpiritRunTalentState> entry
                     in _controller.States)
            {
                if (entry.Value.UnspentPoints <= 0 ||
                    entry.Value.GetAvailableChoices().Count == 0)
                {
                    continue;
                }
                _currentSpiritId = entry.Key;
                Open();
                RefreshCurrent();
                return;
            }
        }

        private bool RefreshCurrent()
        {
            if (_controller == null ||
                _treePanel == null ||
                !_controller.TryGetState(
                    _currentSpiritId,
                    out SpiritRunTalentState state))
            {
                return false;
            }

            IReadOnlyList<SpiritTalentDefinition> choices =
                state.GetAvailableChoices();
            bool hasChoices =
                state.UnspentPoints > 0 && choices.Count > 0;
            if (!hasChoices && state.ActiveTalents.Count == 0)
                return false;

            _title.text =
                $"{SpeciesName(state.Spirit.Identity.SpeciesConfigId)} · 天赋成长";
            _progress.text =
                $"等级 {state.Level}　可用点数 {state.UnspentPoints}" +
                $"　已点亮 {state.ActiveTalents.Count}";
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button button = _buttons[i];
                if (!hasChoices || i >= choices.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.gameObject.SetActive(true);
                SpiritTalentDefinition definition = choices[i];
                TextMeshProUGUI label =
                    button.GetComponentInChildren<TextMeshProUGUI>();
                label.text =
                    $"<b>{definition.Name}</b>\n{definition.Description}";
                button.onClick.RemoveAllListeners();
                StableConfigId talentId = definition.ConfigId;
                button.onClick.AddListener(() =>
                    _controller.TryActivate(
                        _currentSpiritId,
                        talentId));
            }
            _resetButton.gameObject.SetActive(
                state.ActiveTalents.Count > 0);
            _confirmButton.gameObject.SetActive(!hasChoices);
            return true;
        }

        private void ConfirmCurrent()
        {
            Close();
            _choiceScanPending = true;
            TryShowNextChoice();
        }

        private void ResetCurrent()
        {
            if (_controller != null &&
                _currentSpiritId != Guid.Empty)
            {
                _controller.ResetTalents(_currentSpiritId);
            }
        }

        private bool OpenTree(
            Guid spiritInstanceId,
            bool openedFromChoice)
        {
            if (!_selectionAllowed)
                return false;
            if (_controller == null)
                FindController();
            if (_controller == null ||
                !_controller.TryGetState(
                    spiritInstanceId,
                    out _))
            {
                return false;
            }

            Pause();
            _currentSpiritId = spiritInstanceId;
            _selectedTalentId = default;
            _treeOpenedFromChoice = openedFromChoice;
            if (_panel != null)
                _panel.SetActive(false);
            if (_treeScrim != null)
                _treeScrim.SetActive(true);
            _treePanel.SetActive(true);
            RefreshTree();
            return true;
        }

        private void OpenTreeFromChoice()
        {
            OpenTree(_currentSpiritId, true);
        }

        private void ReturnFromTree()
        {
            if (_treePanel == null || !_treePanel.activeSelf)
                return;

            if (_treeOpenedFromChoice)
            {
                _treePanel.SetActive(false);
                if (_treeScrim != null)
                    _treeScrim.SetActive(false);
                _panel.SetActive(true);
                RefreshCurrent();
                return;
            }
            Close();
        }

        private void Open()
        {
            Pause();
            _panel.SetActive(true);
        }

        private void Pause()
        {
            if (_panel?.activeSelf == true ||
                _treePanel?.activeSelf == true)
            {
                return;
            }
            _previousTimeScale = Time.timeScale;
            if (_previousTimeScale > 0f)
                Time.timeScale = 0f;
        }

        private void Close()
        {
            bool choiceActive = _panel?.activeSelf == true;
            bool treeActive = _treePanel?.activeSelf == true;
            if (!choiceActive && !treeActive)
                return;

            if (_panel != null)
                _panel.SetActive(false);
            if (_treePanel != null)
                _treePanel.SetActive(false);
            if (_treeScrim != null)
                _treeScrim.SetActive(false);
            if (_previousTimeScale > 0f)
                Time.timeScale = _previousTimeScale;
            _currentSpiritId = Guid.Empty;
            _selectedTalentId = default;
            _treeOpenedFromChoice = false;
        }

        private void Build()
        {
            _panel = new GameObject("SpiritTalentChoicePanel");
            _panel.transform.SetParent(transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680f, 470f);
            var background = _panel.AddComponent<Image>();
            background.sprite =
                SpiritTalentTreeVisuals.RoundedPanel;
            background.type = Image.Type.Sliced;
            background.color = SpiritTalentTreeVisuals.Paper;
            var outline = _panel.AddComponent<Outline>();
            outline.effectColor =
                new Color(0.30f, 0.20f, 0.10f, 0.72f);
            outline.effectDistance = new Vector2(3f, -3f);

            _title = CreateText(
                _panel.transform,
                "Title",
                25f,
                new Vector2(0f, 0.82f),
                new Vector2(1f, 1f));
            _title.color = SpiritTalentTreeVisuals.Ink;
            _title.fontStyle = FontStyles.Bold;
            _progress = CreateText(
                _panel.transform,
                "Progress",
                14f,
                new Vector2(0f, 0.72f),
                new Vector2(1f, 0.84f));
            _progress.color = SpiritTalentTreeVisuals.InkDim;

            for (int i = 0; i < 3; i++)
                _buttons.Add(CreateChoiceButton(i));
            _resetButton = CreateActionButton(
                "ResetTalents",
                "重选本局天赋",
                new Vector2(0.08f, 0.03f),
                new Vector2(0.38f, 0.11f));
            _resetButton.onClick.AddListener(ResetCurrent);
            _viewTreeButton = CreateActionButton(
                "ViewTalentTree",
                "查看整棵树",
                new Vector2(0.40f, 0.03f),
                new Vector2(0.66f, 0.11f));
            _viewTreeButton.onClick.AddListener(OpenTreeFromChoice);
            _confirmButton = CreateActionButton(
                "ConfirmTalents",
                "确认",
                new Vector2(0.68f, 0.03f),
                new Vector2(0.92f, 0.11f));
            _confirmButton.onClick.AddListener(ConfirmCurrent);
            _panel.SetActive(false);
            BuildTree();
        }

        private void BuildTree()
        {
            _treeScrim = new GameObject("SpiritTalentTreeScrim");
            _treeScrim.transform.SetParent(transform, false);
            var scrimRect = _treeScrim.AddComponent<RectTransform>();
            scrimRect.anchorMin = Vector2.zero;
            scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = Vector2.zero;
            scrimRect.offsetMax = Vector2.zero;
            var scrim = _treeScrim.AddComponent<Image>();
            scrim.color = new Color(0.09f, 0.08f, 0.06f, 0.58f);

            _treePanel = new GameObject("SpiritTalentTreePanel");
            _treePanel.transform.SetParent(transform, false);
            var rect = _treePanel.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1500f, 820f);
            rect.localScale = Vector3.one * 1.22f;
            var background = _treePanel.AddComponent<Image>();
            Sprite panelSprite =
                ProjectRUITheme.Instance?.TalentTreePanel;
            background.sprite = panelSprite != null
                ? panelSprite
                : SpiritTalentTreeVisuals.RoundedPanel;
            background.type = panelSprite != null
                ? Image.Type.Simple
                : Image.Type.Sliced;
            background.color = panelSprite != null
                ? Color.white
                : SpiritTalentTreeVisuals.Paper;
            var outline = _treePanel.AddComponent<Outline>();
            outline.effectColor =
                new Color(0.30f, 0.20f, 0.10f, 0.72f);
            outline.effectDistance = new Vector2(3f, -3f);

            var titleBrush = new GameObject("TitleBrush");
            titleBrush.transform.SetParent(_treePanel.transform, false);
            var titleBrushRect =
                titleBrush.AddComponent<RectTransform>();
            titleBrushRect.anchorMin = new Vector2(0.075f, 0.895f);
            titleBrushRect.anchorMax = new Vector2(0.34f, 0.975f);
            titleBrushRect.offsetMin = Vector2.zero;
            titleBrushRect.offsetMax = Vector2.zero;
            var titleBrushImage = titleBrush.AddComponent<Image>();
            titleBrushImage.sprite = SpiritTalentTreeVisuals.Pill;
            titleBrushImage.type = Image.Type.Sliced;
            titleBrushImage.color =
                new Color(0.24f, 0.17f, 0.10f, 0.96f);
            titleBrushImage.raycastTarget = false;

            _treeTitle = CreateText(
                _treePanel.transform,
                "TreeTitle",
                34f,
                new Vector2(0.095f, 0.90f),
                new Vector2(0.33f, 0.975f));
            _treeTitle.fontStyle = FontStyles.Bold;
            _treeTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _treeProgress = CreateText(
                _treePanel.transform,
                "TreeProgress",
                18f,
                new Vector2(0.35f, 0.90f),
                new Vector2(0.76f, 0.97f));
            _treeProgress.color = SpiritTalentTreeVisuals.InkDim;
            _treeProgress.alignment =
                TextAlignmentOptions.MidlineLeft;

            string[] tierNames =
            {
                "初醒", "分化", "共鸣", "显化"
            };
            for (int tier = 0; tier < 4; tier++)
            {
                TextMeshProUGUI label = CreateText(
                    _treePanel.transform,
                    $"Tier_{tier + 1}",
                    18f,
                    new Vector2(0.17f + tier * 0.17f, 0.80f),
                    new Vector2(0.27f + tier * 0.17f, 0.855f));
                label.text = tierNames[tier];
                label.color = SpiritTalentTreeVisuals.InkDim;
            }
            for (int branch = 0; branch < 3; branch++)
            {
                TextMeshProUGUI routeLabel = CreateText(
                    _treePanel.transform,
                    $"Route_{branch + 1}",
                    20f,
                    new Vector2(0.105f, 0.68f - branch * 0.23f),
                    new Vector2(0.19f, 0.75f - branch * 0.23f));
                routeLabel.fontStyle = FontStyles.Bold;
                routeLabel.color =
                    SpiritTalentTreeVisuals.RouteColor(branch);
                _treeRouteLabels.Add(routeLabel);
            }

            var guideLineRootGo =
                new GameObject("FutureTalentConnections");
            guideLineRootGo.transform.SetParent(
                _treePanel.transform,
                false);
            _treeGuideLineRoot =
                guideLineRootGo.AddComponent<RectTransform>();
            _treeGuideLineRoot.anchorMin = Vector2.zero;
            _treeGuideLineRoot.anchorMax = Vector2.one;
            _treeGuideLineRoot.offsetMin = Vector2.zero;
            _treeGuideLineRoot.offsetMax = Vector2.zero;

            var lineRootGo = new GameObject("TalentConnections");
            lineRootGo.transform.SetParent(_treePanel.transform, false);
            _treeLineRoot = lineRootGo.AddComponent<RectTransform>();
            _treeLineRoot.anchorMin = Vector2.zero;
            _treeLineRoot.anchorMax = Vector2.one;
            _treeLineRoot.offsetMin = Vector2.zero;
            _treeLineRoot.offsetMax = Vector2.zero;

            for (int tier = 0; tier < 4; tier++)
            {
                int count = tier == 1 || tier == 2 ? 6 : 3;
                for (int visualIndex = 0;
                     visualIndex < count;
                     visualIndex++)
                {
                    int branch = count == 3
                        ? visualIndex
                        : visualIndex / 2;
                    _treeNodes.Add(CreateTreeNode(
                        tier,
                        visualIndex,
                        branch));
                }
            }
            CreateTalentScaffold();
            for (int i = 0; i < 3; i++)
                _treeTabs.Add(CreateTreeTab(i));

            var spiritArtGo = new GameObject("SelectedSpiritArt");
            spiritArtGo.transform.SetParent(_treePanel.transform, false);
            var spiritArtRect =
                spiritArtGo.AddComponent<RectTransform>();
            spiritArtRect.anchorMin = spiritArtRect.anchorMax =
                new Vector2(0f, 0f);
            spiritArtRect.pivot = new Vector2(0f, 0f);
            spiritArtRect.anchoredPosition = new Vector2(0f, 0f);
            spiritArtRect.sizeDelta = new Vector2(500f, 500f);
            _treeSpiritArt = spiritArtGo.AddComponent<Image>();
            _treeSpiritArt.preserveAspect = true;
            _treeSpiritArt.raycastTarget = false;
            spiritArtGo.transform.SetSiblingIndex(0);
            CreateActionMedallions();

            var detailGo = new GameObject("TreeDetailPanel");
            detailGo.transform.SetParent(_treePanel.transform, false);
            var detailRect = detailGo.AddComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.775f, 0.22f);
            detailRect.anchorMax = new Vector2(0.965f, 0.84f);
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;
            var detailBackground = detailGo.AddComponent<Image>();
            detailBackground.sprite =
                SpiritTalentTreeVisuals.RoundedPanel;
            detailBackground.type = Image.Type.Sliced;
            detailBackground.color =
                SpiritTalentTreeVisuals.PaperInset;

            _treeDetail = CreateText(
                detailGo.transform,
                "TreeDetail",
                15f,
                Vector2.zero,
                Vector2.one);
            _treeDetail.alignment = TextAlignmentOptions.TopLeft;
            _treeDetail.margin = new Vector4(22f, 118f, 22f, 18f);
            _treeDetail.color = SpiritTalentTreeVisuals.Ink;

            var detailIconGo = new GameObject("SelectedTalentIcon");
            detailIconGo.transform.SetParent(detailGo.transform, false);
            var detailIconRect =
                detailIconGo.AddComponent<RectTransform>();
            detailIconRect.anchorMin = detailIconRect.anchorMax =
                new Vector2(0.5f, 1f);
            detailIconRect.pivot = new Vector2(0.5f, 1f);
            detailIconRect.anchoredPosition = new Vector2(0f, -22f);
            detailIconRect.sizeDelta = new Vector2(86f, 86f);
            _treeDetailIcon = detailIconGo.AddComponent<RawImage>();
            _treeDetailIcon.raycastTarget = false;

            _treeResetButton = CreateTreeActionButton(
                "TreeReset",
                "重选本局天赋",
                new Vector2(0.57f, 0.045f),
                new Vector2(0.70f, 0.115f));
            _treeResetButton.onClick.AddListener(ResetCurrent);
            _treeActivateButton = CreateTreeActionButton(
                "TreeActivate",
                "点亮节点",
                new Vector2(0.815f, 0.125f),
                new Vector2(0.94f, 0.195f));
            _treeActivateButton.onClick.AddListener(
                ActivateSelectedTalent);
            Button backButton = CreateTreeActionButton(
                "TreeBack",
                "返回",
                new Vector2(0.82f, 0.045f),
                new Vector2(0.94f, 0.115f));
            backButton.onClick.AddListener(ReturnFromTree);
            _treeScrim.SetActive(false);
            _treePanel.SetActive(false);
        }

        private Button CreateTreeNode(
            int tier,
            int visualIndex,
            int branch)
        {
            var go = new GameObject(
                $"TalentNode_{tier + 1}_{visualIndex + 1}");
            go.transform.SetParent(_treePanel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition =
                NodePosition(tier, visualIndex);
            float size = tier switch
            {
                2 => 112f,
                3 => 150f,
                _ => 92f
            };
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = tier switch
            {
                2 => SpiritTalentTreeVisuals.Star,
                3 => SpiritTalentTreeVisuals.Ultimate,
                _ => SpiritTalentTreeVisuals.Circle
            };
            image.preserveAspect = true;
            image.color = TreeNodeColor(
                SpiritTalentTreeNodeState.PermanentlyLocked,
                branch);
            if (tier <= 1)
                CreateOrdinaryNodeInset(go.transform);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateText(
                go.transform,
                "Label",
                tier == 3 ? 25f : 21f,
                Vector2.zero,
                Vector2.one);
            label.fontStyle = FontStyles.Bold;
            label.color = SpiritTalentTreeVisuals.Ink;
            label.margin = new Vector4(9f, 9f, 9f, 9f);
            _treeNodeFallbacks.Add(label);

            var iconGo = new GameObject("TalentIcon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.20f, 0.20f);
            iconRect.anchorMax = new Vector2(0.80f, 0.80f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconGo.AddComponent<RawImage>();
            icon.raycastTarget = false;
            _treeNodeIcons.Add(icon);
            return button;
        }

        private void CreateTalentScaffold()
        {
            Vector2[] trunkStarts =
            {
                new Vector2(-540f, 90f),
                new Vector2(-540f, -70f),
                new Vector2(-540f, -215f)
            };
            Vector2[] trunkBends =
            {
                new Vector2(-455f, 175f),
                new Vector2(-455f, -15f),
                new Vector2(-455f, -220f)
            };
            for (int branch = 0; branch < 3; branch++)
            {
                CreateRouteTrunkLine(
                    trunkStarts[branch],
                    trunkBends[branch],
                    branch);
                CreateRouteTrunkLine(
                    trunkBends[branch],
                    NodePosition(0, branch),
                    branch);
            }
            int[,] tierOneToTwo =
            {
                { 0, 0 }, { 0, 1 },
                { 1, 2 }, { 1, 3 },
                { 2, 4 }, { 2, 5 }
            };
            for (int i = 0; i < tierOneToTwo.GetLength(0); i++)
            {
                int root = tierOneToTwo[i, 0];
                int fork = tierOneToTwo[i, 1];
                CreateGuideLine(
                    NodePosition(0, root),
                    NodePosition(1, fork),
                    root);
            }

            int[,] tierTwoToThree =
            {
                { 0, 0 }, { 1, 0 },
                { 1, 1 }, { 2, 1 },
                { 2, 2 }, { 3, 2 },
                { 3, 3 }, { 4, 3 },
                { 4, 4 }, { 5, 4 },
                { 5, 5 }, { 0, 5 }
            };
            for (int i = 0; i < tierTwoToThree.GetLength(0); i++)
            {
                int fork = tierTwoToThree[i, 0];
                int key = tierTwoToThree[i, 1];
                CreateGuideLine(
                    NodePosition(1, fork),
                    NodePosition(2, key),
                    key / 2);
            }

            int[,] tierThreeToFour =
            {
                { 0, 0 }, { 1, 0 }, { 5, 0 },
                { 2, 1 }, { 1, 1 }, { 3, 1 },
                { 4, 2 }, { 3, 2 }, { 5, 2 }
            };
            for (int i = 0; i < tierThreeToFour.GetLength(0); i++)
            {
                int key = tierThreeToFour[i, 0];
                int ultimate = tierThreeToFour[i, 1];
                CreateGuideLine(
                    NodePosition(2, key),
                    NodePosition(3, ultimate),
                    ultimate);
            }
        }

        private void CreateRouteTrunkLine(
            Vector2 from,
            Vector2 to,
            int branch)
        {
            CreateGuideLine(
                from,
                to,
                branch,
                12f,
                0.80f);
        }

        private void CreateFutureNode(
            string name,
            Vector2 position,
            Sprite sprite,
            float size,
            int branch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_treePanel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            Color route =
                SpiritTalentTreeVisuals.RouteColor(branch);
            image.color = new Color(
                route.r,
                route.g,
                route.b,
                0.22f);
            image.raycastTarget = false;
            if (sprite == SpiritTalentTreeVisuals.Circle)
                CreateOrdinaryNodeInset(go.transform);

            TextMeshProUGUI mark = CreateText(
                go.transform,
                "FutureMark",
                13f,
                new Vector2(0f, -0.20f),
                new Vector2(1f, 0.18f));
            mark.text = "未开放";
            mark.color = new Color(
                SpiritTalentTreeVisuals.Ink.r,
                SpiritTalentTreeVisuals.Ink.g,
                SpiritTalentTreeVisuals.Ink.b,
                0.42f);
        }

        private static void CreateOrdinaryNodeInset(
            Transform parent)
        {
            var insetGo = new GameObject("NodeInset");
            insetGo.transform.SetParent(parent, false);
            var insetRect = insetGo.AddComponent<RectTransform>();
            insetRect.anchorMin = new Vector2(0.17f, 0.17f);
            insetRect.anchorMax = new Vector2(0.83f, 0.83f);
            insetRect.offsetMin = Vector2.zero;
            insetRect.offsetMax = Vector2.zero;
            var inset = insetGo.AddComponent<Image>();
            inset.sprite = SpiritTalentTreeVisuals.Circle;
            inset.color = SpiritTalentTreeVisuals.PaperInset;
            inset.raycastTarget = false;
        }

        private void CreateGuideLine(
            Vector2 from,
            Vector2 to,
            int branch,
            float thickness = 3f,
            float alpha = 0.14f)
        {
            var go = new GameObject("FutureLink");
            go.transform.SetParent(_treeGuideLineRoot, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            Vector2 delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta =
                new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) *
                Mathf.Rad2Deg);
            var image = go.AddComponent<Image>();
            image.sprite = SpiritTalentTreeVisuals.Pill;
            image.type = Image.Type.Sliced;
            Color route =
                SpiritTalentTreeVisuals.RouteColor(branch);
            image.color = new Color(
                route.r,
                route.g,
                route.b,
                alpha);
            image.raycastTarget = false;
        }

        private Button CreateTreeTab(int index)
        {
            var go = new GameObject($"SpiritTab_{index}");
            go.transform.SetParent(_treePanel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition =
                new Vector2(18f, -112f - index * 98f);
            rect.sizeDelta = new Vector2(84f, 84f);
            var image = go.AddComponent<Image>();
            image.sprite = SpiritTalentTreeVisuals.Circle;
            image.color = SpiritTalentTreeVisuals.PaperInset;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(go.transform, false);
            var portraitRect =
                portraitGo.AddComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.13f, 0.13f);
            portraitRect.anchorMax = new Vector2(0.87f, 0.87f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            var portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            _treeTabPortraits.Add(portrait);
            return button;
        }

        private Button CreateTreeActionButton(
            string name,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_treePanel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = SpiritTalentTreeVisuals.Pill;
            image.type = Image.Type.Sliced;
            image.color = name == "TreeActivate"
                ? SpiritTalentTreeVisuals.Ember
                : new Color(0.56f, 0.49f, 0.34f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateText(
                go.transform,
                "Label",
                14f,
                Vector2.zero,
                Vector2.one);
            label.text = labelText;
            label.color = name == "TreeActivate"
                ? SpiritTalentTreeVisuals.Paper
                : SpiritTalentTreeVisuals.Paper;
            return button;
        }

        private void CreateActionMedallions()
        {
            string[] labels = { "武", "术", "移" };
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject($"ActionMedallion_{i}");
                go.transform.SetParent(_treePanel.transform, false);
                var rect = go.AddComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax =
                    new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition =
                    new Vector2(175f + i * 58f, 44f);
                rect.sizeDelta = new Vector2(48f, 48f);
                var image = go.AddComponent<Image>();
                image.sprite = SpiritTalentTreeVisuals.Circle;
                image.color = i == 0
                    ? SpiritTalentTreeVisuals.Ember
                    : i == 1
                        ? SpiritTalentTreeVisuals.Moss
                        : SpiritTalentTreeVisuals.Teal;
                image.raycastTarget = false;
                TextMeshProUGUI label = CreateText(
                    go.transform,
                    "Glyph",
                    17f,
                    Vector2.zero,
                    Vector2.one);
                label.text = labels[i];
                label.fontStyle = FontStyles.Bold;
                label.color = SpiritTalentTreeVisuals.Paper;
            }
        }

        private static Vector2 NodePosition(
            int tier,
            int visualIndex)
        {
            float[] x = { -380f, -120f, 120f, 365f };
            if (tier == 0 || tier == 3)
            {
                float[] endpointY = { 220f, 0f, -220f };
                return new Vector2(
                    x[tier],
                    endpointY[visualIndex]);
            }
            float[] networkY =
            {
                275f, 165f, 55f, -55f, -165f, -275f
            };
            return new Vector2(
                x[tier],
                networkY[visualIndex]);
        }

        private static int VisualSlotBranch(int visualSlot)
        {
            if (visualSlot < 3)
                return visualSlot;
            if (visualSlot < 9)
                return (visualSlot - 3) / 2;
            if (visualSlot < 15)
                return (visualSlot - 9) / 2;
            return visualSlot - 15;
        }

        private void RefreshTree()
        {
            if (_treePanel == null ||
                !_treePanel.activeSelf ||
                _controller == null ||
                !_controller.TryGetState(
                    _currentSpiritId,
                    out SpiritRunTalentState state))
            {
                return;
            }

            _treeTitle.text = "灵宠天赋";
            _treeProgress.text =
                $"{SpeciesName(state.Spirit.Identity.SpeciesConfigId)}　" +
                $"等级 {state.Level}　经验 {state.Experience}/" +
                $"{state.ExperienceToNextPoint}　可用点数 " +
                $"{state.UnspentPoints}　永久开放至第" +
                $"{SpiritTalentPermanentProgression.GetPermanentlyAvailableTier(state.Spirit)}层";
            if (_treeSpiritArt != null)
            {
                ProjectRUITheme theme = ProjectRUITheme.Instance;
                Sprite illustration = theme?.TalentIllustration(
                    state.Spirit.Identity.SpeciesConfigId);
                bool hasFullIllustration = illustration != null;
                _treeSpiritArt.sprite = hasFullIllustration
                    ? illustration
                    : theme?.SpiritPortrait(
                        state.Spirit.Identity.SpeciesConfigId);
                RectTransform artRect =
                    (RectTransform)_treeSpiritArt.transform;
                artRect.anchoredPosition = hasFullIllustration
                    ? Vector2.zero
                    : new Vector2(54f, 50f);
                artRect.sizeDelta = hasFullIllustration
                    ? new Vector2(500f, 500f)
                    : new Vector2(190f, 190f);
            }
            for (int branch = 0;
                 branch < _treeRouteLabels.Count;
                 branch++)
            {
                _treeRouteLabels[branch].text = RouteName(
                    state.Spirit.Identity.SpeciesConfigId,
                    branch);
            }
            RefreshTreeTabs();

            IReadOnlyList<SpiritTalentTreeNodePresentation> nodes =
                SpiritTalentTreePresentation.Build(state);
            if (_renderedTreeSpecies !=
                state.Spirit.Identity.SpeciesConfigId)
            {
                RebuildTreeConnectors(nodes);
                _renderedTreeSpecies =
                    state.Spirit.Identity.SpeciesConfigId;
            }
            if (_selectedTalentId.IsEmpty && nodes.Count > 0)
            {
                SpiritTalentTreeNodePresentation initial = nodes[0];
                foreach (SpiritTalentTreeNodePresentation node in nodes)
                {
                    if (node.State ==
                        SpiritTalentTreeNodeState.Available)
                    {
                        initial = node;
                        break;
                    }
                }
                _selectedTalentId = initial.Definition.ConfigId;
            }

            var nodesBySlot = new Dictionary<
                int,
                SpiritTalentTreeNodePresentation>();
            foreach (SpiritTalentTreeNodePresentation node in nodes)
                nodesBySlot[node.VisualSlot] = node;

            for (int i = 0; i < _treeNodes.Count; i++)
            {
                Button button = _treeNodes[i];
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                RawImage icon = i < _treeNodeIcons.Count
                    ? _treeNodeIcons[i]
                    : null;
                TextMeshProUGUI fallback =
                    i < _treeNodeFallbacks.Count
                        ? _treeNodeFallbacks[i]
                        : null;
                if (!nodesBySlot.TryGetValue(
                        i,
                        out SpiritTalentTreeNodePresentation node))
                {
                    int branch = VisualSlotBranch(i);
                    Color route =
                        SpiritTalentTreeVisuals.RouteColor(branch);
                    button.interactable = false;
                    button.GetComponent<Image>().color = new Color(
                        route.r,
                        route.g,
                        route.b,
                        0.20f);
                    if (icon != null)
                        icon.gameObject.SetActive(false);
                    if (fallback != null)
                    {
                        fallback.text = "未开放";
                        fallback.fontSize = 12f;
                        fallback.color = new Color(
                            SpiritTalentTreeVisuals.Ink.r,
                            SpiritTalentTreeVisuals.Ink.g,
                            SpiritTalentTreeVisuals.Ink.b,
                            0.36f);
                        fallback.gameObject.SetActive(true);
                    }
                    Outline placeholderOutline =
                        button.GetComponent<Outline>();
                    if (placeholderOutline != null)
                        placeholderOutline.enabled = false;
                    continue;
                }
                button.interactable = true;
                Image image = button.GetComponent<Image>();
                image.color = TreeNodeColor(
                    node.State,
                    node.Branch);
                Outline selected =
                    button.GetComponent<Outline>();
                bool isSelected =
                    node.Definition.ConfigId == _selectedTalentId;
                if (isSelected && selected == null)
                    selected = button.gameObject.AddComponent<Outline>();
                if (selected != null)
                {
                    selected.enabled = isSelected;
                    selected.effectColor =
                        new Color(1f, 0.82f, 0.34f, 1f);
                    selected.effectDistance = new Vector2(2f, -2f);
                }
                Texture2D atlas =
                    ProjectRUITheme.Instance?.TalentIconAtlas(
                        node.Definition.SpeciesId);
                if (icon != null)
                {
                    int atlasIndex = TalentCatalogIndex(
                        node.Definition.SpeciesId,
                        node.Definition.ConfigId);
                    icon.texture = atlas;
                    if (atlasIndex >= 0)
                    {
                        icon.uvRect = TalentIconUv(
                            atlasIndex,
                            6);
                    }
                    icon.color = new Color(
                        1f,
                        1f,
                        1f,
                        node.State ==
                        SpiritTalentTreeNodeState.PermanentlyLocked
                            ? 0.34f
                            : 1f);
                    icon.gameObject.SetActive(
                        atlas != null && atlasIndex >= 0);
                }
                if (fallback != null)
                {
                    fallback.fontSize =
                        node.Definition.Tier == 4 ? 25f : 21f;
                    fallback.text = NodeGlyph(node.Definition);
                    fallback.color =
                        node.State ==
                        SpiritTalentTreeNodeState.PermanentlyLocked
                            ? new Color(
                                SpiritTalentTreeVisuals.Ink.r,
                                SpiritTalentTreeVisuals.Ink.g,
                                SpiritTalentTreeVisuals.Ink.b,
                                0.38f)
                            : SpiritTalentTreeVisuals.Ink;
                    fallback.gameObject.SetActive(
                        atlas == null ||
                        TalentCatalogIndex(
                            node.Definition.SpeciesId,
                            node.Definition.ConfigId) < 0);
                }
                button.onClick.RemoveAllListeners();
                StableConfigId talentId = node.Definition.ConfigId;
                button.onClick.AddListener(() =>
                    SelectTalent(talentId));
            }
            RefreshConnectorFocus(nodes);
            RefreshTreeDetail(state, nodes);
            _treeResetButton.gameObject.SetActive(
                state.ActiveTalents.Count > 0);
        }

        private void RebuildTreeConnectors(
            IReadOnlyList<SpiritTalentTreeNodePresentation> nodes)
        {
            for (int i = _treeLineRoot.childCount - 1; i >= 0; i--)
                Destroy(_treeLineRoot.GetChild(i).gameObject);
            _treeConnectors.Clear();

            var positions =
                new Dictionary<StableConfigId, Vector2>();
            foreach (SpiritTalentTreeNodePresentation node in nodes)
            {
                if (node.VisualSlot < 0 ||
                    node.VisualSlot >= _treeNodes.Count)
                {
                    continue;
                }
                positions[node.Definition.ConfigId] =
                    ((RectTransform)_treeNodes[node.VisualSlot].transform)
                    .anchoredPosition;
            }

            foreach (SpiritTalentTreeNodePresentation node in nodes)
            {
                if (!positions.TryGetValue(
                        node.Definition.ConfigId,
                        out Vector2 to))
                {
                    continue;
                }
                foreach (StableConfigId prerequisite
                         in node.Definition.PrerequisiteIds)
                {
                    if (!positions.TryGetValue(
                            prerequisite,
                            out Vector2 from))
                    {
                        continue;
                    }
                    var lineGo = new GameObject(
                        $"Link_{prerequisite}_" +
                        $"{node.Definition.ConfigId}");
                    lineGo.transform.SetParent(_treeLineRoot, false);
                    var rect = lineGo.AddComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax =
                        new Vector2(0.5f, 0.5f);
                    Vector2 delta = to - from;
                    rect.anchoredPosition = (from + to) * 0.5f;
                    rect.sizeDelta =
                        new Vector2(delta.magnitude, 7f);
                    rect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Atan2(delta.y, delta.x) *
                        Mathf.Rad2Deg);
                    var image = lineGo.AddComponent<Image>();
                    image.sprite = SpiritTalentTreeVisuals.Pill;
                    image.type = Image.Type.Sliced;
                    image.color = new Color(
                        SpiritTalentTreeVisuals.InkDim.r,
                        SpiritTalentTreeVisuals.InkDim.g,
                        SpiritTalentTreeVisuals.InkDim.b,
                        0.18f);
                    image.raycastTarget = false;
                    _treeConnectors.Add(new TreeConnector
                    {
                        From = prerequisite,
                        To = node.Definition.ConfigId,
                        Branch = node.Branch,
                        Image = image
                    });
                }
            }
        }

        private void RefreshConnectorFocus(
            IReadOnlyList<SpiritTalentTreeNodePresentation> nodes)
        {
            var definitions = new Dictionary<
                StableConfigId,
                SpiritTalentDefinition>();
            var active = new HashSet<StableConfigId>();
            foreach (SpiritTalentTreeNodePresentation node in nodes)
            {
                definitions[node.Definition.ConfigId] =
                    node.Definition;
                if (node.State == SpiritTalentTreeNodeState.Active)
                    active.Add(node.Definition.ConfigId);
            }

            var focused = new HashSet<StableConfigId>();
            if (!_selectedTalentId.IsEmpty)
            {
                var pending = new Stack<StableConfigId>();
                pending.Push(_selectedTalentId);
                while (pending.Count > 0)
                {
                    StableConfigId current = pending.Pop();
                    if (current.IsEmpty ||
                        !focused.Add(current) ||
                        !definitions.TryGetValue(
                            current,
                            out SpiritTalentDefinition definition))
                    {
                        continue;
                    }
                    foreach (StableConfigId prerequisite
                             in definition.PrerequisiteIds)
                    {
                        pending.Push(prerequisite);
                    }
                }
                bool changed;
                do
                {
                    changed = false;
                    foreach (SpiritTalentTreeNodePresentation node in nodes)
                    {
                        bool touchesFocused = false;
                        foreach (StableConfigId prerequisite
                                 in node.Definition.PrerequisiteIds)
                        {
                            touchesFocused |= focused.Contains(prerequisite);
                        }
                        if (touchesFocused &&
                            focused.Add(node.Definition.ConfigId))
                        {
                            changed = true;
                        }
                    }
                } while (changed);
            }

            foreach (TreeConnector connector in _treeConnectors)
            {
                bool selectedPath =
                    focused.Contains(connector.From) &&
                    focused.Contains(connector.To);
                bool activePath =
                    active.Contains(connector.From) &&
                    active.Contains(connector.To);
                Color route =
                    SpiritTalentTreeVisuals.RouteColor(
                        connector.Branch);
                connector.Image.color = selectedPath || activePath
                    ? new Color(
                        route.r,
                        route.g,
                        route.b,
                        activePath ? 0.92f : 0.64f)
                    : new Color(
                        SpiritTalentTreeVisuals.InkDim.r,
                        SpiritTalentTreeVisuals.InkDim.g,
                        SpiritTalentTreeVisuals.InkDim.b,
                        0.12f);
                RectTransform rect =
                    (RectTransform)connector.Image.transform;
                rect.sizeDelta = new Vector2(
                    rect.sizeDelta.x,
                    activePath ? 9f : selectedPath ? 7f : 4f);
            }
        }

        private void RefreshTreeTabs()
        {
            StableConfigId[] starterSpecies =
            {
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                FirstSpiritCircuitContent.BounceGelSpecies
            };
            var bySpecies = new Dictionary<
                StableConfigId,
                KeyValuePair<Guid, SpiritRunTalentState>>();
            foreach (KeyValuePair<Guid, SpiritRunTalentState> entry
                     in _controller.States)
            {
                bySpecies[
                    entry.Value.Spirit.Identity.SpeciesConfigId] =
                    entry;
            }
            for (int index = 0;
                 index < _treeTabs.Count;
                 index++)
            {
                StableConfigId species = starterSpecies[index];
                Button tab = _treeTabs[index];
                tab.gameObject.SetActive(true);
                tab.onClick.RemoveAllListeners();
                if (index < _treeTabPortraits.Count)
                {
                    _treeTabPortraits[index].sprite =
                        ProjectRUITheme.Instance?.SpiritPortrait(
                            species);
                }
                Image image = tab.GetComponent<Image>();
                if (bySpecies.TryGetValue(
                        species,
                        out KeyValuePair<
                            Guid,
                            SpiritRunTalentState> entry))
                {
                    tab.interactable = true;
                    if (index < _treeTabPortraits.Count)
                        _treeTabPortraits[index].color = Color.white;
                    image.color = entry.Key == _currentSpiritId
                        ? SpiritTalentTreeVisuals.Ember
                        : SpiritTalentTreeVisuals.PaperInset;
                    Guid spiritId = entry.Key;
                    tab.onClick.AddListener(() =>
                    {
                        _currentSpiritId = spiritId;
                        _selectedTalentId = default;
                        RefreshTree();
                    });
                }
                else
                {
                    tab.interactable = false;
                    if (index < _treeTabPortraits.Count)
                    {
                        _treeTabPortraits[index].color =
                            new Color(1f, 1f, 1f, 0.48f);
                    }
                    image.color = new Color(
                        SpiritTalentTreeVisuals.PaperInset.r,
                        SpiritTalentTreeVisuals.PaperInset.g,
                        SpiritTalentTreeVisuals.PaperInset.b,
                        0.56f);
                }
            }
        }

        private void SelectTalent(StableConfigId talentId)
        {
            _selectedTalentId = talentId;
            RefreshTree();
        }

        private void ActivateSelectedTalent()
        {
            if (_controller == null || _selectedTalentId.IsEmpty)
                return;
            _controller.TryActivate(
                _currentSpiritId,
                _selectedTalentId);
            RefreshTree();
        }

        private void RefreshTreeDetail(
            SpiritRunTalentState state,
            IReadOnlyList<SpiritTalentTreeNodePresentation> nodes)
        {
            SpiritTalentTreeNodePresentation? selected = null;
            foreach (SpiritTalentTreeNodePresentation node in nodes)
            {
                if (node.Definition.ConfigId == _selectedTalentId)
                {
                    selected = node;
                    break;
                }
            }
            if (!selected.HasValue)
            {
                _treeDetail.text = "选择一个节点查看详情";
                _treeActivateButton.gameObject.SetActive(false);
                if (_treeDetailIcon != null)
                    _treeDetailIcon.gameObject.SetActive(false);
                return;
            }

            SpiritTalentTreeNodePresentation nodeValue =
                selected.Value;
            SpiritTalentDefinition definition =
                nodeValue.Definition;
            if (_treeDetailIcon != null)
            {
                Texture2D atlas =
                    ProjectRUITheme.Instance?.TalentIconAtlas(
                        definition.SpeciesId);
                int atlasIndex = TalentCatalogIndex(
                    definition.SpeciesId,
                    definition.ConfigId);
                _treeDetailIcon.texture = atlas;
                if (atlasIndex >= 0)
                    _treeDetailIcon.uvRect =
                        TalentIconUv(
                            atlasIndex,
                            6);
                _treeDetailIcon.gameObject.SetActive(
                    atlas != null && atlasIndex >= 0);
            }
            string prerequisite = "无";
            if (definition.PrerequisiteIds.Count > 0)
            {
                var names = new List<string>();
                foreach (StableConfigId prerequisiteId
                         in definition.PrerequisiteIds)
                {
                    if (StarterSpiritTalentCatalog.TryGet(
                            prerequisiteId,
                            out SpiritTalentDefinition required))
                    {
                        names.Add(required.Name);
                    }
                }
                prerequisite = string.Join(
                    definition.PrerequisiteMode ==
                    SpiritTalentPrerequisiteMode.All
                        ? " ＋ "
                        : " ／ ",
                    names);
            }
            string applicability =
                _controller.States.Count == 1 &&
                definition.Tier >= 3
                    ? "当前编队：以单宠适配效果生效"
                    : "当前编队：可生效";
            _treeDetail.text =
                $"<size=22><b>{definition.Name}</b></size>\n\n" +
                $"{definition.Description}\n\n" +
                $"层级：{definition.Tier}\n" +
                $"状态：{NodeStateText(nodeValue.State)}\n" +
                $"前置：{prerequisite}\n" +
                $"开放条件：{PermanentCondition(definition.Tier)}\n\n" +
                applicability;
            _treeActivateButton.gameObject.SetActive(
                nodeValue.State ==
                SpiritTalentTreeNodeState.Available);
        }

        private static Color TreeNodeColor(
            SpiritTalentTreeNodeState state,
            int branch)
        {
            Color route = SpiritTalentTreeVisuals.RouteColor(branch);
            return state switch
            {
                SpiritTalentTreeNodeState.Active =>
                    route,
                SpiritTalentTreeNodeState.Available =>
                    new Color(route.r, route.g, route.b, 0.82f),
                SpiritTalentTreeNodeState.OpenWithoutPoint =>
                    new Color(route.r, route.g, route.b, 0.46f),
                SpiritTalentTreeNodeState.MissingPrerequisite =>
                    new Color(0.58f, 0.54f, 0.43f, 0.55f),
                SpiritTalentTreeNodeState.ConflictingCapstone =>
                    new Color(0.44f, 0.35f, 0.34f, 0.58f),
                _ => new Color(0.54f, 0.51f, 0.43f, 0.42f)
            };
        }

        private static string NodeGlyph(
            SpiritTalentDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.Name)
                ? "·"
                : definition.Name.Substring(0, 1);
        }

        private static int TalentCatalogIndex(
            StableConfigId species,
            StableConfigId talentId)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                if (!StarterSpiritTalentCatalog.TryGet(
                        talentId,
                        out SpiritTalentDefinition definition))
                {
                    return -1;
                }
                int tierOffset = definition.Tier switch
                {
                    1 => 0,
                    2 => 3,
                    3 => 9,
                    _ => 15
                };
                return tierOffset + definition.VisualIndex;
            }
            IReadOnlyList<SpiritTalentDefinition> definitions =
                StarterSpiritTalentCatalog.ForSpecies(species);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].ConfigId == talentId)
                    return i;
            }
            return -1;
        }

        private static Rect TalentIconUv(int index, int rowCount)
        {
            index = Mathf.Clamp(index, 0, rowCount * 3 - 1);
            int column = index % 3;
            int rowFromTop = index / 3;
            const float cellWidth = 1f / 3f;
            float cellHeight = 1f / rowCount;
            const float padX = 0.025f;
            float padY = rowCount == 6 ? 0.009f : 0.018f;
            return new Rect(
                column * cellWidth + padX,
                (rowCount - 1 - rowFromTop) * cellHeight + padY,
                cellWidth - padX * 2f,
                cellHeight - padY * 2f);
        }

        private static string RouteName(
            StableConfigId species,
            int branch)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                return branch switch
                {
                    0 => "速燃",
                    1 => "爆火",
                    _ => "蓄热"
                };
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return branch switch
                {
                    0 => "清响",
                    1 => "远听",
                    _ => "留声"
                };
            }
            return branch switch
            {
                0 => "连弹",
                1 => "重击",
                _ => "回弹"
            };
        }

        private static string NodeStateGlyph(
            SpiritTalentTreeNodeState state)
        {
            return state switch
            {
                SpiritTalentTreeNodeState.Active => "●",
                SpiritTalentTreeNodeState.Available => "◇",
                SpiritTalentTreeNodeState.ConflictingCapstone => "×",
                SpiritTalentTreeNodeState.PermanentlyLocked => "锁",
                _ => "○"
            };
        }

        private static string NodeStateText(
            SpiritTalentTreeNodeState state)
        {
            return state switch
            {
                SpiritTalentTreeNodeState.Active => "本局已点亮",
                SpiritTalentTreeNodeState.Available => "现在可点亮",
                SpiritTalentTreeNodeState.OpenWithoutPoint => "永久开放，点数不足",
                SpiritTalentTreeNodeState.MissingPrerequisite => "缺少前置节点",
                SpiritTalentTreeNodeState.ConflictingCapstone => "与已选终极节点互斥",
                _ => "尚未永久开放"
            };
        }

        private static string PermanentCondition(int tier)
        {
            return tier switch
            {
                1 => "初始开放",
                2 => "共同探索1次",
                3 => "共同探索3次",
                _ => "共同探索6次并取得1次关键胜利"
            };
        }

        private Button CreateChoiceButton(int index)
        {
            var go = new GameObject($"TalentChoice_{index}");
            go.transform.SetParent(_panel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.14f);
            rect.anchorMax = new Vector2(0.92f, 0.14f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, index * 82f);
            rect.sizeDelta = new Vector2(0f, 70f);
            var image = go.AddComponent<Image>();
            image.sprite = SpiritTalentTreeVisuals.Pill;
            image.type = Image.Type.Sliced;
            image.color = SpiritTalentTreeVisuals.PaperInset;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = CreateText(
                go.transform,
                "Label",
                14f,
                Vector2.zero,
                Vector2.one);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(16f, 7f, 16f, 7f);
            label.color = SpiritTalentTreeVisuals.Ink;
            return button;
        }

        private Button CreateActionButton(
            string name,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_panel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = SpiritTalentTreeVisuals.Pill;
            image.type = Image.Type.Sliced;
            image.color = name == "ConfirmTalents"
                ? SpiritTalentTreeVisuals.Ember
                : new Color(0.56f, 0.49f, 0.34f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = CreateText(
                go.transform,
                "Label",
                14f,
                Vector2.zero,
                Vector2.one);
            label.text = labelText;
            label.color = SpiritTalentTreeVisuals.Paper;
            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            float size,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                text.font = UGuiKit.CjkFont;
            text.fontSize = size;
            text.color = new Color(0.95f, 0.91f, 0.78f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            return text;
        }

        private static string SpeciesName(StableConfigId species)
        {
            if (species == FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "火花狸";
            if (species == FirstSpiritCircuitContent.EchoOwlSpecies)
                return "响响鸮";
            return "弹弹胶";
        }

        private static string SpeciesShortName(StableConfigId species)
        {
            if (species == FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "狸";
            if (species == FirstSpiritCircuitContent.EchoOwlSpecies)
                return "鸮";
            return "胶";
        }
    }
}
