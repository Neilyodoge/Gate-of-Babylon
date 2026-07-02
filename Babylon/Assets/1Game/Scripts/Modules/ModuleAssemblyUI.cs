using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace XianTu
{
    /// <summary>
    /// 技能装配台（M 键开关）——参考精修版式：
    /// - 左：模块背包，图标卡片网格（分类色块兜底 + 稀有度配色名 + 分类标签），可筛选/拖拽/选中。
    /// - 中：Q/E/R 三列竖式技能链。每列含：核心技能名 + 全息链状态框（人话预览）+ 4 竖槽
    ///       （触发器/效果器/改造1/改造2）+ 底部消费模型/效果角色角标 + 状态条（已激活/待补/未装配）。
    /// - 右：模块详情（大图标 + 名称 + 标签 + 说明）+ 帮助说明 + 卸下全部链 + 丢弃选中模块。
    /// 战斗中不可打开（ForceOpen 调试除外）。
    /// </summary>
    public class ModuleAssemblyUI : MonoBehaviour
    {
        // ---------- 状态 ----------
        private GameObject _root;
        private RectTransform _rootRT;
        private bool _isOpen;
        private ModuleDef _selected;
        private ModuleDef _hover;
        private int _filter = -1; // -1=全部，否则 (int)ModuleCategory

        // ---------- 拖拽 ----------
        private ModuleDef _dragModule;
        private ModuleDragHandle _dragHandle;
        private GameObject _ghost;
        private RectTransform _ghostRT;
        private Text _ghostLabel;
        private Image _ghostBg;

        // ---------- 提示 toast ----------
        private GameObject _toastGo;
        private Image _toastBg;
        private Text _toast;
        private float _toastTimer;
        private Color _toastColor = Color.white;

        // ---------- 背包卡片 ----------
        private class InvItem
        {
            public GameObject go;
            public RectTransform rt;
            public Image bg;
            public Image iconBg;
            public Image iconImg;
            public Text iconGlyph;
            public Text name;
            public Text tag;
            public Button btn;
            public ModuleDragHandle drag;
        }
        private readonly List<InvItem> _invItems = new();
        private RectTransform _invContent;
        private RectTransform _invViewport;
        private Text _invCountLabel;
        private Text _invEmptyLabel;

        // ---------- 筛选页签 ----------
        private readonly List<(Button btn, Image bg, Text label, int value)> _tabs = new();

        // ---------- 链列（竖式） ----------
        private readonly Image[] _colBorder = new Image[3];
        private readonly Image[] _colBg = new Image[3];
        private readonly Text[] _colHeader = new Text[3];
        private readonly Text[] _chainBoxTitle = new Text[3];
        private readonly Text[] _chainPreview = new Text[3];
        private readonly Image[,] _slotBorder = new Image[3, 4];
        private readonly Image[,] _slotIconBg = new Image[3, 4];
        private readonly Image[,] _slotIconImg = new Image[3, 4];
        private readonly Text[,] _slotIconGlyph = new Text[3, 4];
        private readonly Text[,] _slotLabel = new Text[3, 4];
        private readonly Text[,] _slotHint = new Text[3, 4];
        private readonly GameObject[,] _slotRemove = new GameObject[3, 4];
        private readonly Text[] _modeBadge = new Text[3];
        private readonly Text[] _roleBadge = new Text[3];
        private readonly Image[] _statusBarBg = new Image[3];
        private readonly Text[] _statusBar = new Text[3];

        // ---------- 详情 ----------
        private Image _descIconBg;
        private Image _descIconImg;
        private Text _descIconGlyph;
        private Text _descTitle;
        private Text _descTags;
        private Text _descBody;
        private Button _discardBtn;
        private Text _discardLabel;

        // ---------- 颜色常量 ----------
        private static readonly Color CTrigger = new Color(0.26f, 0.66f, 1f);
        private static readonly Color CEffect = new Color(1f, 0.46f, 0.26f);
        private static readonly Color CModifier = new Color(0.40f, 0.92f, 0.45f);
        private static readonly Color CUniversal = new Color(1f, 0.82f, 0.25f);
        private static readonly Color CGlow = new Color(0.35f, 1f, 0.55f);
        private static readonly Color CPanel = new Color(0.08f, 0.09f, 0.14f, 0.97f);
        private static readonly Color CSubPanel = new Color(0.06f, 0.07f, 0.11f, 0.92f);
        private static readonly Color CSlotEmpty = new Color(0.12f, 0.13f, 0.19f, 0.95f);

        public static ModuleAssemblyUI Instance { get; private set; }
        public static bool IsVisible => Instance != null && Instance._isOpen;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            BuildUI();
            if (_root != null) _root.SetActive(false);
            GameEvents.Subscribe<GameEvents.ModulePickedUp>(OnModulePickedUp);
        }

        private void Update()
        {
            if (_toastTimer > 0f && _toast != null)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(_toastTimer / 0.6f);
                _toast.color = new Color(_toastColor.r, _toastColor.g, _toastColor.b, a);
                if (_toastBg != null)
                {
                    var bc = _toastBg.color;
                    _toastBg.color = new Color(bc.r, bc.g, bc.b, 0.85f * a);
                }
                if (_toastTimer <= 0f && _toastGo != null) _toastGo.SetActive(false);
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) { if (_isOpen) Close(); else TryOpen(); }
            else if (_isOpen && kb.escapeKey.wasPressedThisFrame) Close();
        }

        private void ShowToast(string msg, Color color)
        {
            if (_toast == null) return;
            if (_toastGo != null) _toastGo.SetActive(true);
            _toast.text = msg;
            _toastColor = color;
            _toastTimer = 2.4f;
            _toast.color = color;
            if (_toastBg != null)
                _toastBg.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.85f);
        }

        // ==================== 开关 ====================

        private void TryOpen()
        {
            EnsurePlayerModuleComponents();
            var slots = PlayerSlots();
            if (slots != null && slots.InCombat)
            {
                Debug.Log("<color=red>战斗中无法打开模块装配！</color>");
                return;
            }
            OpenInternal();
        }

        public void Toggle() { if (_isOpen) Close(); else TryOpen(); }

        /// <summary>调试用：无视战斗状态强制打开。</summary>
        public void ForceOpen() { EnsurePlayerModuleComponents(); OpenInternal(); }

        private void OpenInternal()
        {
            _isOpen = true;
            _selected = null;
            _hover = null;
            if (_root != null) _root.SetActive(true);
            RefreshAll();
        }

        public void Close()
        {
            _isOpen = false;
            _selected = null;
            _hover = null;
            CancelDrag();
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>确保玩家身上有模块背包 + 槽位管理器（村庄 Hub 配置时这俩可能还没创建）。</summary>
        private static void EnsurePlayerModuleComponents()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            if (player.GetComponent<ModuleInventory>() == null)
                player.gameObject.AddComponent<ModuleInventory>();
            if (player.GetComponent<ModuleSlotManager>() == null)
                player.gameObject.AddComponent<ModuleSlotManager>();
        }

        private static ModuleInventory PlayerInv() => PlayerController.Instance != null
            ? PlayerController.Instance.GetComponent<ModuleInventory>() : null;
        private static ModuleSlotManager PlayerSlots() => PlayerController.Instance != null
            ? PlayerController.Instance.GetComponent<ModuleSlotManager>() : null;

        private void OnModulePickedUp(GameEvents.ModulePickedUp evt)
        {
            if (_isOpen) RefreshInventory();
        }

        // ==================== 刷新 ====================

        private void RefreshAll()
        {
            RefreshTabs();
            RefreshInventory();
            RefreshChains();
            RefreshDesc();
        }

        private void RefreshTabs()
        {
            foreach (var (btn, bg, label, value) in _tabs)
            {
                bool active = _filter == value;
                bg.color = active ? new Color(0.30f, 0.42f, 0.62f, 1f) : new Color(0.16f, 0.17f, 0.24f, 0.9f);
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                label.color = active ? Color.white : new Color(0.7f, 0.72f, 0.8f);
            }
        }

        private void RefreshInventory()
        {
            var inv = PlayerInv();
            foreach (var it in _invItems) it.go.SetActive(false);

            var all = inv != null ? inv.Modules : (IReadOnlyList<ModuleDef>)System.Array.Empty<ModuleDef>();
            var shown = new List<ModuleDef>();
            foreach (var m in all)
                if (m != null && (_filter < 0 || (int)m.category == _filter)) shown.Add(m);

            while (_invItems.Count < shown.Count) CreateInvItem();

            // 网格布局：3 列，按内容区宽度计算卡片尺寸
            const int cols = 3;
            const float gap = 6f;
            float vw = _invContent != null ? _invContent.rect.width : 0f;
            if (vw < 1f) vw = 420f;
            float cellW = (vw - gap * (cols + 1)) / cols;
            if (cellW < 40f) cellW = 40f;
            float cellH = cellW * 1.2f;

            for (int i = 0; i < shown.Count; i++)
            {
                var m = shown[i];
                var it = _invItems[i];
                int col = i % cols;
                int row = i / cols;
                it.go.SetActive(true);
                it.rt.sizeDelta = new Vector2(cellW, cellH);
                it.rt.anchoredPosition = new Vector2(gap + col * (cellW + gap), -(gap + row * (cellH + gap)));

                SetIconTile(it.iconBg, it.iconImg, it.iconGlyph, m);
                it.name.text = m.displayName;
                it.name.color = RarityColor(m.rarity);
                it.tag.text = $"{CategoryName(m.category)}·{RarityName(m.rarity)}";
                it.tag.color = CategoryColor(m.category);

                bool sel = _selected == m;
                it.bg.color = sel ? new Color(0.22f, 0.36f, 0.56f, 1f) : new Color(0.13f, 0.14f, 0.20f, 0.95f);

                int idx = i;
                it.btn.onClick.RemoveAllListeners();
                it.btn.onClick.AddListener(() => OnInvClicked(shown[idx]));
                if (it.drag != null) it.drag.bagModule = m;
                SetHover(it.go, m);
            }

            int rows = (shown.Count + cols - 1) / cols;
            if (_invContent != null)
                _invContent.sizeDelta = new Vector2(0, gap + rows * (cellH + gap));

            if (_invCountLabel != null)
                _invCountLabel.text = $"数量：{shown.Count}";
            if (_invEmptyLabel != null)
                _invEmptyLabel.gameObject.SetActive(shown.Count == 0);
        }

        private void RefreshChains()
        {
            var slots = PlayerSlots();
            var hl = _dragModule != null ? _dragModule : _selected;
            string[] posNames = { "触发器", "效果器", "改造件1", "改造件2" };

            for (int s = 0; s < 3; s++)
            {
                var chain = slots != null ? slots.GetChain(s) : null;
                ModuleDef[] parts = chain != null
                    ? new[] { chain.trigger, chain.effect, chain.modifier0, chain.modifier1 }
                    : new ModuleDef[4];

                bool valid = chain != null && chain.IsValid;
                bool hasAny = chain != null && ChainHasAny(chain);

                // 列头：核心技能名
                if (_colHeader[s] != null)
                    _colHeader[s].text = $"<color=#9aa0b0>{SlotKeyNames[s]} 技能：</color>{GetBoundCoreSkillName(s)}";

                // 列底色 + 外框
                if (_colBg[s] != null)
                    _colBg[s].color = valid
                        ? new Color(0.07f, 0.16f, 0.10f, 0.9f)
                        : (hasAny ? new Color(0.17f, 0.14f, 0.08f, 0.88f) : new Color(0.09f, 0.10f, 0.15f, 0.88f));
                if (_colBorder[s] != null)
                    _colBorder[s].color = valid
                        ? new Color(0.40f, 1f, 0.52f, 0.95f)
                        : (hasAny ? new Color(1f, 0.72f, 0.3f, 0.7f) : new Color(0.18f, 0.20f, 0.27f, 0.85f));

                // 全息链框：标题 + 人话预览
                if (_chainBoxTitle[s] != null)
                {
                    string chainPart = valid ? chain.DisplayName : (hasAny ? "装配中…" : "空链");
                    _chainBoxTitle[s].text = $"<color=#9aa0b0>全息链：</color>{chainPart}  {BuildChainStatus(chain)}";
                }
                if (_chainPreview[s] != null)
                {
                    _chainPreview[s].text = valid
                        ? BuildChainPreview(chain, s)
                        : (hasAny ? BuildChainMissingHint(chain) : "<color=#5a5d6b>放入 触发器 + 效果器 即可激活</color>");
                }

                // 底部角标
                if (_modeBadge[s] != null)
                {
                    if (valid)
                    {
                        var ck = chain.trigger.GetConsumeKindForSlot();
                        _modeBadge[s].text = $"消费 {KindBadge(ck)}";
                        _modeBadge[s].color = KindColor(ck);
                    }
                    else { _modeBadge[s].text = "消费 —"; _modeBadge[s].color = new Color(0.5f, 0.52f, 0.6f); }
                }
                if (_roleBadge[s] != null)
                {
                    if (valid)
                    {
                        var er = chain.effect.GetEffectRoleForSlot();
                        _roleBadge[s].text = $"效果 {RoleBadge(er)}";
                        _roleBadge[s].color = er == EffectRole.Addon ? new Color(1f, 0.6f, 0.35f) : new Color(0.55f, 1f, 0.6f);
                    }
                    else { _roleBadge[s].text = "效果 —"; _roleBadge[s].color = new Color(0.5f, 0.52f, 0.6f); }
                }

                // 状态条
                if (_statusBar[s] != null && _statusBarBg[s] != null)
                {
                    if (valid) { _statusBar[s].text = "● 已激活"; _statusBar[s].color = new Color(0.1f, 0.15f, 0.1f); _statusBarBg[s].color = new Color(0.30f, 0.85f, 0.40f, 0.95f); }
                    else if (hasAny)
                    {
                        string need = chain.trigger == null ? "触发器" : (chain.effect == null ? "效果器" : "类型");
                        _statusBar[s].text = $"待补 {need}"; _statusBar[s].color = new Color(0.2f, 0.15f, 0.05f); _statusBarBg[s].color = new Color(1f, 0.74f, 0.3f, 0.95f);
                    }
                    else { _statusBar[s].text = "未装配"; _statusBar[s].color = new Color(0.6f, 0.62f, 0.7f); _statusBarBg[s].color = new Color(0.22f, 0.24f, 0.32f, 0.9f); }
                }

                // 4 竖槽
                for (int p = 0; p < 4; p++)
                {
                    var m = parts[p];
                    bool canPlace = hl != null && hl.CanFitSlot(p);

                    if (m != null)
                    {
                        SetIconTile(_slotIconBg[s, p], _slotIconImg[s, p], _slotIconGlyph[s, p], m);
                        _slotIconBg[s, p].gameObject.SetActive(true);
                        _slotLabel[s, p].text = FormatModuleName(m);
                        _slotLabel[s, p].fontStyle = FontStyle.Bold;
                        _slotHint[s, p].text = "";
                    }
                    else
                    {
                        _slotIconBg[s, p].gameObject.SetActive(false);
                        _slotLabel[s, p].text = $"<color=#6a6d7b>{posNames[p]}</color>";
                        _slotLabel[s, p].fontStyle = FontStyle.Normal;
                        _slotHint[s, p].text = canPlace ? "<color=#7eff8a>↓ 放到这里</color>"
                            : (p <= 1 ? "<color=#4a4d5b>点击/拖拽装配</color>" : "<color=#4a4d5b>可选·改造</color>");
                    }

                    // 边框
                    if (hl != null)
                        _slotBorder[s, p].color = canPlace ? CGlow : new Color(0.12f, 0.12f, 0.16f, 0.9f);
                    else
                        _slotBorder[s, p].color = m != null
                            ? CategoryColor(m.category) * new Color(1f, 1f, 1f, 0.9f)
                            : new Color(0.20f, 0.21f, 0.28f, 0.9f);

                    if (_slotRemove[s, p] != null) _slotRemove[s, p].SetActive(m != null);
                    SetHover(_slotBorder[s, p].gameObject, m);
                }
            }
        }

        private void RefreshDesc()
        {
            var m = _hover ?? _selected;
            if (m == null)
            {
                if (_descIconBg != null) _descIconBg.gameObject.SetActive(false);
                if (_descTitle != null) _descTitle.text = "<color=#9fb3d0>模块详情</color>";
                if (_descTags != null) _descTags.text = "";
                if (_descBody != null) _descBody.text = "<color=#7a8090>把鼠标移到左侧模块或链槽位上查看说明。\n拖拽模块到发绿的槽位即可装入。</color>";
            }
            else
            {
                if (_descIconBg != null)
                {
                    _descIconBg.gameObject.SetActive(true);
                    SetIconTile(_descIconBg, _descIconImg, _descIconGlyph, m);
                }
                string mode = m.executionMode == ExecutionMode.Active ? "主动" : "被动";
                string desc = !string.IsNullOrEmpty(m.uiDescription) ? m.uiDescription : m.description;
                _descTitle.text = $"<b><color=#{ColorHex(RarityColor(m.rarity))}>{m.displayName}</color></b>";
                _descTags.text = $"<color=#{ColorHex(CategoryColor(m.category))}>{CategoryName(m.category)}</color>  <color=#9aa0b0>{mode} · {RarityName(m.rarity)}</color>";
                _descBody.text = string.IsNullOrEmpty(desc) ? "<color=#888>（暂无说明）</color>" : desc;
            }
            if (_discardBtn != null) _discardBtn.interactable = _selected != null;
            if (_discardLabel != null)
                _discardLabel.color = _selected != null ? new Color(1f, 0.7f, 0.7f) : new Color(0.5f, 0.4f, 0.4f);
        }

        // ==================== 交互 ====================

        private void SetFilter(int f)
        {
            _filter = f;
            RefreshTabs();
            RefreshInventory();
        }

        private void OnInvClicked(ModuleDef m)
        {
            _selected = (_selected == m) ? null : m;
            RefreshInventory();
            RefreshChains();
            RefreshDesc();
        }

        private static readonly string[] SlotKeyNames = { "Q", "E", "R" };
        private static readonly string[] PosFullNames = { "触发器", "效果器", "改造1", "改造2" };

        private void OnSlotClicked(int slot, int pos)
        {
            if (_selected == null) return;
            if (InstallToSlot(_selected, slot, pos, fromBag: true, srcSlot: -1, srcPos: -1))
                _selected = null;
            RefreshAll();
        }

        /// <summary>
        /// 把模块装入指定槽位。来源可为背包(fromBag)或另一个槽位(srcSlot/srcPos)。返回是否成功。
        /// </summary>
        private bool InstallToSlot(ModuleDef m, int slot, int pos, bool fromBag, int srcSlot, int srcPos)
        {
            var mgr = PlayerSlots();
            if (mgr == null || m == null) return false;

            if (!fromBag && srcSlot == slot && srcPos == pos) return false;

            if (!m.CanFitSlot(pos))
            {
                ShowToast($"✗ {PosFullNames[pos]} 槽不接受「{m.displayName}」", new Color(1f, 0.45f, 0.4f));
                return false;
            }

            var inv = PlayerInv();
            var chain = mgr.GetChain(slot) ?? new ModuleChain();
            var existing = GetChainPos(chain, pos);

            if (fromBag)
            {
                if (inv != null) inv.Remove(m);
            }
            else
            {
                var srcChain = mgr.GetChain(srcSlot);
                if (srcChain != null) ClearChainPos(srcChain, srcPos);
                if (existing != null && srcChain != null && existing.CanFitSlot(srcPos))
                {
                    SetChainPos(srcChain, srcPos, existing);
                    existing = null;
                }
                if (srcChain != null)
                    mgr.EquipChain(srcSlot, ChainHasAny(srcChain) ? srcChain : null);
            }

            if (existing != null && inv != null) inv.Add(existing);

            bool wasValid = chain.IsValid;
            SetChainPos(chain, pos, m);
            mgr.EquipChain(slot, ChainHasAny(chain) ? chain : null);
            bool nowValid = chain.IsValid;

            if (nowValid && !wasValid)
                ShowToast($"✓ {SlotKeyNames[slot]} 链激活！{BuildChainPreviewShort(chain, slot)}", CGlow);
            else if (!nowValid && ChainHasAny(chain))
            {
                string need = chain.trigger == null ? "触发器" : (chain.effect == null ? "效果器" : "合适类型件");
                ShowToast($"✓ 已装入 {SlotKeyNames[slot]} · {PosFullNames[pos]}：{m.displayName}   <color=#ffd140>· 还差 {need} 成链</color>",
                    new Color(1f, 0.82f, 0.4f));
            }
            else
                ShowToast($"✓ 已装入 {SlotKeyNames[slot]} · {PosFullNames[pos]}：「{m.displayName}」", CGlow);
            return true;
        }

        private static string BuildChainPreviewShort(ModuleChain chain, int slot)
        {
            if (chain == null || !chain.IsValid) return "";
            var ck = chain.trigger.GetConsumeKindForSlot();
            string proc = TriggerProcText(chain.trigger);
            string consume = ConsumeText(ck, SlotKeyNames[slot]);
            string effect = chain.effect != null ? chain.effect.displayName : "?";
            return $"\n{proc}{consume}{effect}";
        }

        private void OnSlotRemove(int slot, int pos)
        {
            var mgr = PlayerSlots();
            if (mgr == null) return;
            var chain = mgr.GetChain(slot);
            if (chain == null) return;
            var removed = GetChainPos(chain, pos);
            if (removed != null)
            {
                var inv = PlayerInv();
                if (inv != null) inv.Add(removed);
                ShowToast($"↩ 已卸下：「{removed.displayName}」", new Color(1f, 0.85f, 0.45f));
            }
            ClearChainPos(chain, pos);
            mgr.EquipChain(slot, ChainHasAny(chain) ? chain : null);
            RefreshAll();
        }

        /// <summary>供拖拽组件查询某槽位当前模块。</summary>
        public ModuleDef GetSlotModule(int slot, int pos)
        {
            var mgr = PlayerSlots();
            var chain = mgr != null ? mgr.GetChain(slot) : null;
            return chain != null ? GetChainPos(chain, pos) : null;
        }

        // ==================== 拖拽 ====================

        public void DragBegin(ModuleDragHandle h, PointerEventData e)
        {
            var m = h.fromBag ? h.bagModule : GetSlotModule(h.slot, h.pos);
            if (m == null) return;
            _dragModule = m;
            _dragHandle = h;
            _selected = null;
            EnsureGhost();
            _ghost.SetActive(true);
            _ghostLabel.text = $"{CategoryBadge(m.category)} {m.displayName}";
            _ghostBg.color = CategoryColor(m.category) * new Color(1f, 1f, 1f, 0.35f) + new Color(0.06f, 0.06f, 0.1f, 0.85f);
            MoveGhost(e);
            RefreshInventory();
            RefreshChains();
            RefreshDesc();
        }

        public void DragMove(PointerEventData e)
        {
            if (_dragModule == null) return;
            MoveGhost(e);
        }

        public void DragEnd(ModuleDragHandle h, PointerEventData e)
        {
            if (_dragModule == null) { CancelDrag(); return; }
            var m = _dragModule;
            bool handled = false;

            var results = new List<RaycastResult>();
            EventSystem.current?.RaycastAll(e, results);
            foreach (var r in results)
            {
                var slotT = r.gameObject.GetComponentInParent<ModuleSlotTarget>();
                if (slotT != null)
                {
                    InstallToSlot(m, slotT.slot, slotT.pos, h.fromBag, h.slot, h.pos);
                    handled = true;
                    break;
                }
                if (r.gameObject.GetComponentInParent<ModuleBagTarget>() != null)
                {
                    if (!h.fromBag) OnSlotRemove(h.slot, h.pos);
                    handled = true;
                    break;
                }
            }

            if (!handled)
                ShowToast("已取消", new Color(0.7f, 0.72f, 0.8f));

            CancelDrag();
            RefreshAll();
        }

        private void CancelDrag()
        {
            _dragModule = null;
            _dragHandle = null;
            if (_ghost != null) _ghost.SetActive(false);
        }

        private void MoveGhost(PointerEventData e)
        {
            if (_ghostRT == null || _rootRT == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRT, e.position, e.pressEventCamera, out var lp);
            _ghostRT.anchoredPosition = lp;
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;
            _ghost = new GameObject("DragGhost");
            _ghost.transform.SetParent(_root.transform, false);
            _ghostRT = _ghost.AddComponent<RectTransform>();
            _ghostRT.anchorMin = _ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostRT.pivot = new Vector2(0.08f, 0.92f);
            _ghostRT.sizeDelta = new Vector2(220, 34);
            var cg = _ghost.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            _ghostBg = _ghost.AddComponent<Image>();
            _ghostBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            AddOutline(_ghost, new Color(0.4f, 0.7f, 1f, 0.7f));
            _ghostLabel = CreateText(_ghost.transform, "L", "",
                Vector2.zero, Vector2.one, 14, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft).GetComponent<Text>();
            _ghost.transform.SetAsLastSibling();
        }

        private void UnequipAll()
        {
            var mgr = PlayerSlots();
            var inv = PlayerInv();
            if (mgr == null) return;
            for (int s = 0; s < 3; s++)
            {
                var chain = mgr.GetChain(s);
                if (chain == null) continue;
                for (int p = 0; p < 4; p++)
                {
                    var m = GetChainPos(chain, p);
                    if (m != null && inv != null) inv.Add(m);
                }
                mgr.EquipChain(s, null);
            }
            _selected = null;
            ShowToast("↩ 已卸下全部链", new Color(1f, 0.85f, 0.45f));
            RefreshAll();
        }

        private void DiscardSelected()
        {
            if (_selected == null) return;
            var inv = PlayerInv();
            if (inv == null) return;
            inv.Remove(_selected);

            var player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 dropPos = player.transform.position + player.transform.forward * 2f
                    + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                ModulePickup.Spawn(_selected, dropPos);
                Debug.Log($"<color=yellow>丢弃模块：{_selected.displayName}</color>");
            }
            _selected = null;
            RefreshAll();
        }

        private static bool ChainHasAny(ModuleChain c) =>
            c != null && (c.trigger != null || c.effect != null || c.modifier0 != null || c.modifier1 != null);

        // ==================== 链辅助 ====================

        private static ModuleDef GetChainPos(ModuleChain c, int p) => p switch
        {
            0 => c.trigger, 1 => c.effect, 2 => c.modifier0, 3 => c.modifier1, _ => null
        };
        private static void SetChainPos(ModuleChain c, int p, ModuleDef m)
        {
            switch (p) { case 0: c.trigger = m; break; case 1: c.effect = m; break; case 2: c.modifier0 = m; break; case 3: c.modifier1 = m; break; }
        }
        private static void ClearChainPos(ModuleChain c, int p) => SetChainPos(c, p, null);

        // ==================== 构建 UI ====================

        private void BuildUI()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _root = new GameObject("ModuleAssemblyRoot");
            _root.transform.SetParent(canvas.transform, false);
            _rootRT = _root.AddComponent<RectTransform>();
            Stretch(_rootRT);
            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);

            var card = CreatePanel(_root.transform, "Card",
                new Vector2(0.04f, 0.055f), new Vector2(0.96f, 0.945f), CPanel);
            AddOutline(card, new Color(0.28f, 0.5f, 0.85f, 0.55f));

            // 顶部标题栏
            var header = CreatePanel(card.transform, "Header",
                new Vector2(0f, 0.928f), new Vector2(1f, 1f), new Color(0.11f, 0.14f, 0.24f, 1f));
            CreateText(header.transform, "Title", "技能装配台",
                new Vector2(0.012f, 0.42f), new Vector2(0.45f, 1f), 22, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            CreateText(header.transform, "Sub", "拖拽模块到槽位装配 · 点击选中再点槽位 · M / Esc 关闭",
                new Vector2(0.012f, 0f), new Vector2(0.5f, 0.46f), 12, new Color(0.6f, 0.66f, 0.8f), FontStyle.Normal, TextAnchor.MiddleLeft);

            // 顶部居中 toast 药丸
            _toastGo = CreatePanel(header.transform, "Toast",
                new Vector2(0.34f, 0.18f), new Vector2(0.78f, 0.82f), new Color(0.1f, 0.3f, 0.16f, 0f));
            _toastBg = _toastGo.GetComponent<Image>();
            _toastBg.raycastTarget = false;
            _toast = CreateText(_toastGo.transform, "T", "",
                Vector2.zero, Vector2.one, 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter).GetComponent<Text>();
            _toast.color = new Color(1f, 1f, 1f, 0f);
            _toastGo.SetActive(false);

            CreateText(header.transform, "Hint", "M / Esc 关闭",
                new Vector2(0.8f, 0f), new Vector2(0.988f, 1f), 13, new Color(0.55f, 0.6f, 0.74f), FontStyle.Normal, TextAnchor.MiddleRight);

            BuildBag(card.transform);
            BuildChains(card.transform);
            BuildDetail(card.transform);
        }

        // ---------- 左：背包 ----------
        private void BuildBag(Transform card)
        {
            var panel = CreatePanel(card, "BagPanel",
                new Vector2(0.01f, 0.012f), new Vector2(0.255f, 0.918f), CSubPanel);

            CreateText(panel.transform, "BagTitle", "模块背包",
                new Vector2(0.04f, 0.955f), new Vector2(0.6f, 0.998f), 16, new Color(0.8f, 0.9f, 1f), FontStyle.Bold, TextAnchor.MiddleLeft);
            _invCountLabel = CreateText(panel.transform, "BagCount", "数量：0",
                new Vector2(0.55f, 0.955f), new Vector2(0.965f, 0.998f), 13, new Color(0.6f, 0.66f, 0.8f), FontStyle.Normal, TextAnchor.MiddleRight).GetComponent<Text>();

            // 筛选页签
            string[] tabNames = { "全部", "触发", "效果", "改造", "万能" };
            int[] tabVals = { -1, 0, 1, 2, 3 };
            float tw = 1f / tabNames.Length;
            for (int i = 0; i < tabNames.Length; i++)
            {
                float x0 = 0.02f + i * (0.96f * tw);
                float x1 = x0 + 0.96f * tw - 0.008f;
                var tab = CreatePanel(panel.transform, $"Tab_{i}",
                    new Vector2(x0, 0.905f), new Vector2(x1, 0.948f), new Color(0.16f, 0.17f, 0.24f, 0.9f));
                var btn = tab.AddComponent<Button>();
                var label = CreateText(tab.transform, "L", tabNames[i],
                    Vector2.zero, Vector2.one, 12, Color.white).GetComponent<Text>();
                int val = tabVals[i];
                btn.onClick.AddListener(() => SetFilter(val));
                _tabs.Add((btn, tab.GetComponent<Image>(), label, val));
            }

            // 滚动网格
            var scrollGo = CreatePanel(panel.transform, "Scroll",
                new Vector2(0.02f, 0.012f), new Vector2(0.98f, 0.895f), new Color(0f, 0f, 0f, 0.25f));
            scrollGo.AddComponent<ModuleBagTarget>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = CreatePanel(scrollGo.transform, "Viewport", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.001f));
            viewport.AddComponent<RectMask2D>();
            _invViewport = viewport.GetComponent<RectTransform>();
            scroll.viewport = _invViewport;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _invContent = content.AddComponent<RectTransform>();
            _invContent.anchorMin = new Vector2(0, 1);
            _invContent.anchorMax = new Vector2(1, 1);
            _invContent.pivot = new Vector2(0.5f, 1);
            _invContent.offsetMin = Vector2.zero;
            _invContent.offsetMax = Vector2.zero;
            _invContent.sizeDelta = Vector2.zero;
            scroll.content = _invContent;

            _invEmptyLabel = CreateText(viewport.transform, "Empty",
                "背包为空\n用 Debug「发放全部模块」\n或在关卡中拾取\n（已装槽位可拖回此处卸下）",
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.65f), 13, new Color(0.5f, 0.53f, 0.62f)).GetComponent<Text>();
            _invEmptyLabel.gameObject.SetActive(false);
        }

        private void CreateInvItem()
        {
            var go = new GameObject($"Inv_{_invItems.Count}");
            go.transform.SetParent(_invContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(120, 140);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.13f, 0.14f, 0.20f, 0.95f);
            var btn = go.AddComponent<Button>();
            var drag = go.AddComponent<ModuleDragHandle>();
            drag.ui = this; drag.fromBag = true;

            // 图标块（上方正方形）
            var iconBg = CreatePanel(go.transform, "IconBg",
                new Vector2(0.16f, 0.36f), new Vector2(0.84f, 0.95f), new Color(0.1f, 0.11f, 0.16f, 1f));
            iconBg.GetComponent<Image>().raycastTarget = false;
            var iconImg = new GameObject("Icon");
            iconImg.transform.SetParent(iconBg.transform, false);
            var iirt = iconImg.AddComponent<RectTransform>();
            iirt.anchorMin = new Vector2(0.1f, 0.1f); iirt.anchorMax = new Vector2(0.9f, 0.9f);
            iirt.offsetMin = Vector2.zero; iirt.offsetMax = Vector2.zero;
            var iimg = iconImg.AddComponent<Image>();
            iimg.raycastTarget = false; iimg.preserveAspect = true; iimg.enabled = false;
            var glyph = CreateText(iconBg.transform, "Glyph", "",
                Vector2.zero, Vector2.one, 26, Color.white, FontStyle.Bold).GetComponent<Text>();

            var name = CreateText(go.transform, "Name", "",
                new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.36f), 12, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter).GetComponent<Text>();
            var tag = CreateText(go.transform, "Tag", "",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.18f), 10, new Color(0.7f, 0.72f, 0.8f), FontStyle.Normal, TextAnchor.MiddleCenter).GetComponent<Text>();

            _invItems.Add(new InvItem
            {
                go = go, rt = rt, bg = bg, iconBg = iconBg.GetComponent<Image>(),
                iconImg = iimg, iconGlyph = glyph, name = name, tag = tag, btn = btn, drag = drag
            });
        }

        // ---------- 中：三列竖式链 ----------
        private void BuildChains(Transform card)
        {
            var panel = CreatePanel(card, "ChainPanel",
                new Vector2(0.262f, 0.012f), new Vector2(0.752f, 0.918f), CSubPanel);

            CreateText(panel.transform, "ChainTitle", "技能槽 & 链条",
                new Vector2(0.02f, 0.955f), new Vector2(0.6f, 0.998f), 16, new Color(1f, 0.85f, 0.55f), FontStyle.Bold, TextAnchor.MiddleLeft);
            CreateText(panel.transform, "Legend",
                "<color=#43a9ff>■</color>触发 <color=#ff7642>■</color>效果 <color=#66ea73>■</color>改造 <color=#ffd140>■</color>万能",
                new Vector2(0.45f, 0.955f), new Vector2(0.985f, 0.998f), 11, Color.white, FontStyle.Normal, TextAnchor.MiddleRight);

            float[] colX0 = { 0.012f, 0.342f, 0.672f };
            float[] colX1 = { 0.328f, 0.658f, 0.988f };

            for (int s = 0; s < 3; s++)
                BuildChainColumn(panel.transform, s, colX0[s], colX1[s]);
        }

        private void BuildChainColumn(Transform panel, int s, float x0, float x1)
        {
            Color[] keyColors = { new Color(0.4f, 0.8f, 1f), new Color(1f, 0.6f, 0.35f), new Color(0.7f, 0.65f, 1f) };
            string[] posNames = { "触发器", "效果器", "改造件1", "改造件2" };

            // 外框 + 内底
            var border = CreatePanel(panel, $"Col_{s}",
                new Vector2(x0, 0.01f), new Vector2(x1, 0.945f), new Color(0.18f, 0.20f, 0.27f, 0.85f));
            _colBorder[s] = border.GetComponent<Image>();
            border.GetComponent<Image>().raycastTarget = false;
            var col = CreatePanel(border.transform, "Bg",
                new Vector2(0.015f, 0.01f), new Vector2(0.985f, 0.99f), new Color(0.09f, 0.10f, 0.15f, 0.88f));
            _colBg[s] = col.GetComponent<Image>();
            _colBg[s].raycastTarget = false;

            // 列头：键圆 + 核心技能名
            var keyCircle = CreatePanel(col.transform, "Key",
                new Vector2(0.03f, 0.935f), new Vector2(0.17f, 0.99f), keyColors[s] * new Color(1f, 1f, 1f, 0.9f));
            CreateText(keyCircle.transform, "K", SlotKeyNames[s], Vector2.zero, Vector2.one, 18, Color.black, FontStyle.Bold);
            _colHeader[s] = CreateText(col.transform, "Header", "",
                new Vector2(0.19f, 0.935f), new Vector2(0.98f, 0.99f), 13, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft).GetComponent<Text>();

            // 全息链状态框
            var box = CreatePanel(col.transform, "ChainBox",
                new Vector2(0.03f, 0.70f), new Vector2(0.97f, 0.925f), new Color(0.06f, 0.07f, 0.12f, 0.9f));
            box.GetComponent<Image>().raycastTarget = false;
            _chainBoxTitle[s] = CreateText(box.transform, "BoxTitle", "",
                new Vector2(0.04f, 0.66f), new Vector2(0.97f, 0.97f), 12, new Color(0.85f, 0.88f, 0.95f), FontStyle.Bold, TextAnchor.UpperLeft).GetComponent<Text>();
            _chainBoxTitle[s].horizontalOverflow = HorizontalWrapMode.Wrap;
            var pv = CreateText(box.transform, "BoxPreview", "",
                new Vector2(0.04f, 0.03f), new Vector2(0.97f, 0.64f), 11, new Color(0.82f, 0.92f, 0.85f), FontStyle.Normal, TextAnchor.UpperLeft).GetComponent<Text>();
            pv.horizontalOverflow = HorizontalWrapMode.Wrap;
            _chainPreview[s] = pv;

            // 4 竖槽
            float[] slotTop = { 0.685f, 0.555f, 0.425f, 0.295f };
            for (int p = 0; p < 4; p++)
            {
                float top = slotTop[p];
                float bot = top - 0.12f;
                BuildSlot(col.transform, s, p, bot, top, posNames[p]);
                // 槽间向下箭头
                if (p < 3)
                    CreateText(col.transform, $"Arrow_{p}", "<color=#5a6070>▼</color>",
                        new Vector2(0.45f, bot - 0.012f), new Vector2(0.55f, bot + 0.008f), 11, Color.white);
            }

            // 底部角标
            _modeBadge[s] = CreateText(col.transform, "Mode", "",
                new Vector2(0.03f, 0.205f), new Vector2(0.5f, 0.265f), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft).GetComponent<Text>();
            _roleBadge[s] = CreateText(col.transform, "Role", "",
                new Vector2(0.5f, 0.205f), new Vector2(0.97f, 0.265f), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleRight).GetComponent<Text>();

            // 状态条
            var statusBg = CreatePanel(col.transform, "Status",
                new Vector2(0.03f, 0.025f), new Vector2(0.97f, 0.19f), new Color(0.22f, 0.24f, 0.32f, 0.9f));
            statusBg.GetComponent<Image>().raycastTarget = false;
            _statusBarBg[s] = statusBg.GetComponent<Image>();
            _statusBar[s] = CreateText(statusBg.transform, "L", "",
                Vector2.zero, Vector2.one, 14, Color.white, FontStyle.Bold).GetComponent<Text>();
        }

        private void BuildSlot(Transform col, int s, int p, float y0, float y1, string posName)
        {
            var border = CreatePanel(col, $"Slot_{s}_{p}",
                new Vector2(0.03f, y0), new Vector2(0.97f, y1), new Color(0.2f, 0.21f, 0.28f, 0.9f));
            _slotBorder[s, p] = border.GetComponent<Image>();
            var slotBtn = border.AddComponent<Button>();
            int slot = s, pos = p;
            slotBtn.onClick.AddListener(() => OnSlotClicked(slot, pos));

            var tgt = border.AddComponent<ModuleSlotTarget>();
            tgt.slot = slot; tgt.pos = pos;
            var sh = border.AddComponent<ModuleDragHandle>();
            sh.ui = this; sh.fromBag = false; sh.slot = slot; sh.pos = pos;

            var inner = new GameObject("Inner");
            inner.transform.SetParent(border.transform, false);
            var irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2.5f, 2.5f); irt.offsetMax = new Vector2(-2.5f, -2.5f);
            var ibg = inner.AddComponent<Image>();
            ibg.color = CSlotEmpty;
            ibg.raycastTarget = false;

            // 图标块（左侧正方形）
            var iconBg = CreatePanel(inner.transform, "IconBg",
                new Vector2(0.02f, 0.14f), new Vector2(0.24f, 0.86f), new Color(0.1f, 0.11f, 0.16f, 1f));
            iconBg.GetComponent<Image>().raycastTarget = false;
            _slotIconBg[s, p] = iconBg.GetComponent<Image>();
            var iconImg = new GameObject("Icon");
            iconImg.transform.SetParent(iconBg.transform, false);
            var iirt = iconImg.AddComponent<RectTransform>();
            iirt.anchorMin = new Vector2(0.1f, 0.1f); iirt.anchorMax = new Vector2(0.9f, 0.9f);
            iirt.offsetMin = Vector2.zero; iirt.offsetMax = Vector2.zero;
            var iimg = iconImg.AddComponent<Image>();
            iimg.raycastTarget = false; iimg.preserveAspect = true; iimg.enabled = false;
            _slotIconImg[s, p] = iimg;
            _slotIconGlyph[s, p] = CreateText(iconBg.transform, "Glyph", "",
                Vector2.zero, Vector2.one, 16, Color.white, FontStyle.Bold).GetComponent<Text>();
            iconBg.SetActive(false);

            // 名称 + 提示
            var label = CreateText(inner.transform, "Label", posName,
                new Vector2(0.27f, 0.42f), new Vector2(0.86f, 0.95f), 13, Color.white, FontStyle.Normal, TextAnchor.LowerLeft);
            label.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
            _slotLabel[s, p] = label.GetComponent<Text>();
            _slotHint[s, p] = CreateText(inner.transform, "Hint", "",
                new Vector2(0.27f, 0.05f), new Vector2(0.97f, 0.42f), 10, new Color(0.5f, 0.53f, 0.62f), FontStyle.Normal, TextAnchor.UpperLeft).GetComponent<Text>();

            // 卸下 ✕
            var rm = CreatePanel(border.transform, "Remove",
                new Vector2(0.86f, 0.58f), new Vector2(1.0f, 1.0f), new Color(0.6f, 0.18f, 0.18f, 0.95f));
            var rmBtn = rm.AddComponent<Button>();
            rmBtn.onClick.AddListener(() => OnSlotRemove(slot, pos));
            CreateText(rm.transform, "X", "✕", Vector2.zero, Vector2.one, 12, Color.white, FontStyle.Bold);
            rm.SetActive(false);
            _slotRemove[s, p] = rm;
        }

        // ---------- 右：详情 + 帮助 + 操作 ----------
        private void BuildDetail(Transform card)
        {
            var panel = CreatePanel(card, "DetailPanel",
                new Vector2(0.758f, 0.012f), new Vector2(0.99f, 0.918f), CSubPanel);

            CreateText(panel.transform, "DetailTitle", "模块详情",
                new Vector2(0.05f, 0.955f), new Vector2(0.95f, 0.998f), 16, new Color(0.85f, 0.92f, 1f), FontStyle.Bold, TextAnchor.MiddleLeft);

            // 大图标
            var iconBg = CreatePanel(panel.transform, "IconBg",
                new Vector2(0.07f, 0.78f), new Vector2(0.33f, 0.95f), new Color(0.1f, 0.11f, 0.16f, 1f));
            _descIconBg = iconBg.GetComponent<Image>();
            _descIconBg.raycastTarget = false;
            var iconImg = new GameObject("Icon");
            iconImg.transform.SetParent(iconBg.transform, false);
            var iirt = iconImg.AddComponent<RectTransform>();
            iirt.anchorMin = new Vector2(0.12f, 0.12f); iirt.anchorMax = new Vector2(0.88f, 0.88f);
            iirt.offsetMin = Vector2.zero; iirt.offsetMax = Vector2.zero;
            _descIconImg = iconImg.AddComponent<Image>();
            _descIconImg.raycastTarget = false; _descIconImg.preserveAspect = true; _descIconImg.enabled = false;
            _descIconGlyph = CreateText(iconBg.transform, "Glyph", "",
                Vector2.zero, Vector2.one, 30, Color.white, FontStyle.Bold).GetComponent<Text>();
            iconBg.SetActive(false);

            _descTitle = CreateText(panel.transform, "Name", "<color=#9fb3d0>模块详情</color>",
                new Vector2(0.37f, 0.88f), new Vector2(0.96f, 0.95f), 16, Color.white, FontStyle.Bold, TextAnchor.LowerLeft).GetComponent<Text>();
            _descTags = CreateText(panel.transform, "Tags", "",
                new Vector2(0.37f, 0.79f), new Vector2(0.96f, 0.87f), 12, new Color(0.8f, 0.82f, 0.9f), FontStyle.Normal, TextAnchor.UpperLeft).GetComponent<Text>();

            var bodyPanel = CreatePanel(panel.transform, "BodyBg",
                new Vector2(0.05f, 0.55f), new Vector2(0.96f, 0.76f), new Color(0.05f, 0.06f, 0.1f, 0.8f));
            bodyPanel.GetComponent<Image>().raycastTarget = false;
            _descBody = CreateText(bodyPanel.transform, "Body", "",
                new Vector2(0.04f, 0.04f), new Vector2(0.97f, 0.96f), 12, new Color(0.78f, 0.82f, 0.9f), FontStyle.Normal, TextAnchor.UpperLeft).GetComponent<Text>();
            _descBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descBody.verticalOverflow = VerticalWrapMode.Truncate;

            // 帮助说明
            CreateText(panel.transform, "HelpTitle", "帮助说明",
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.535f), 14, new Color(0.85f, 0.92f, 1f), FontStyle.Bold, TextAnchor.MiddleLeft);
            var helpBg = CreatePanel(panel.transform, "HelpBg",
                new Vector2(0.05f, 0.205f), new Vector2(0.96f, 0.495f), new Color(0.05f, 0.06f, 0.1f, 0.8f));
            helpBg.GetComponent<Image>().raycastTarget = false;
            CreateText(helpBg.transform, "Help",
                "• 拖拽装入：左键按住模块拖到槽位\n• 点击装入：先点模块选中，再点槽位\n• 卸下：点槽位的 ✕，或把槽位模块拖回背包\n• 互换：槽位之间互拖即可交换\n• 触发器→效果器 成链即激活；改造件可选",
                new Vector2(0.04f, 0.04f), new Vector2(0.97f, 0.96f), 11, new Color(0.7f, 0.75f, 0.85f), FontStyle.Normal, TextAnchor.UpperLeft);

            // 卸下全部链
            var uneqGo = CreatePanel(panel.transform, "UnequipAll",
                new Vector2(0.05f, 0.11f), new Vector2(0.96f, 0.185f), new Color(0.22f, 0.30f, 0.46f, 0.95f));
            uneqGo.AddComponent<Button>().onClick.AddListener(UnequipAll);
            CreateText(uneqGo.transform, "L", "卸下全部链", Vector2.zero, Vector2.one, 14, new Color(0.85f, 0.9f, 1f), FontStyle.Bold);

            // 丢弃选中模块
            var discardGo = CreatePanel(panel.transform, "Discard",
                new Vector2(0.05f, 0.025f), new Vector2(0.96f, 0.10f), new Color(0.45f, 0.16f, 0.16f, 0.95f));
            _discardBtn = discardGo.AddComponent<Button>();
            _discardBtn.onClick.AddListener(DiscardSelected);
            _discardLabel = CreateText(discardGo.transform, "L", "丢弃选中模块", Vector2.zero, Vector2.one, 14, new Color(1f, 0.7f, 0.7f), FontStyle.Bold).GetComponent<Text>();
        }

        // ==================== 图标块 ====================

        private static void SetIconTile(Image bg, Image img, Text glyph, ModuleDef m)
        {
            if (bg != null)
                bg.color = CategoryColor(m.category) * new Color(1f, 1f, 1f, 0.22f) + new Color(0.08f, 0.09f, 0.13f, 0.92f);
            if (m.icon != null)
            {
                if (img != null) { img.enabled = true; img.sprite = m.icon; }
                if (glyph != null) glyph.text = "";
            }
            else
            {
                // 无真图 → 按子类单字字形 + 元素/类别色兜底（每模块视觉可辨，占位图标）
                if (img != null) img.enabled = false;
                if (glyph != null) { glyph.text = SubtypeGlyph(m); glyph.color = SubtypeColor(m); }
            }
        }

        /// <summary>按模块子类返回单字占位字形（每模块视觉可辨；真图 m.icon 优先）。</summary>
        private static string SubtypeGlyph(ModuleDef m)
        {
            switch (m.category)
            {
                case ModuleCategory.Trigger:
                    return TriggerGlyph(m.triggerType);
                case ModuleCategory.Effect:
                    return EffectGlyph(m.effectType);
                case ModuleCategory.Modifier:
                    return ModifierGlyph(m.modifierType);
                case ModuleCategory.Universal:
                    // 万能件：优先用其效果面字形，无则触发面
                    if (m.universalEffectType != EffectType.None) return EffectGlyph(m.universalEffectType);
                    if (m.universalTriggerType != TriggerType.None) return TriggerGlyph(m.universalTriggerType);
                    return "万";
                default:
                    return "?";
            }
        }

        /// <summary>占位字形颜色：优先元素色，其次改造件状态色，最后类别色。</summary>
        private static Color SubtypeColor(ModuleDef m)
        {
            if (m.elementTag != ElementTag.None) return ElementColorLocal(m.elementTag);
            // 改造件按状态类型着色
            if (m.category == ModuleCategory.Modifier)
            {
                switch (m.modifierType)
                {
                    case ModifierType.AddBurn: case ModifierType.ShapeWall:
                        return ElementColorLocal(ElementTag.Fire);
                    case ModifierType.AddFreeze:
                        return ElementColorLocal(ElementTag.Ice);
                    case ModifierType.AddLightning: case ModifierType.ShapeZone:
                        return ElementColorLocal(ElementTag.Thunder);
                    case ModifierType.AddPoison: case ModifierType.ShapeRing:
                        return ElementColorLocal(ElementTag.Wood);
                }
            }
            // 风格标签兜底
            if ((m.styleTags & StyleTag.Fire) != 0) return ElementColorLocal(ElementTag.Fire);
            if ((m.styleTags & StyleTag.Ice) != 0) return ElementColorLocal(ElementTag.Ice);
            if ((m.styleTags & StyleTag.Lightning) != 0) return ElementColorLocal(ElementTag.Thunder);
            if ((m.styleTags & StyleTag.Poison) != 0) return ElementColorLocal(ElementTag.Wood);
            return CategoryColor(m.category);
        }

        private static Color ElementColorLocal(ElementTag e) => e switch
        {
            ElementTag.Fire    => new Color(1f, 0.45f, 0.2f),
            ElementTag.Ice     => new Color(0.5f, 0.85f, 1f),
            ElementTag.Thunder => new Color(0.7f, 0.6f, 1f),
            ElementTag.Wind    => new Color(0.6f, 1f, 0.7f),
            ElementTag.Wood    => new Color(0.5f, 0.85f, 0.4f),
            ElementTag.Water   => new Color(0.35f, 0.65f, 1f),
            ElementTag.Earth   => new Color(0.85f, 0.7f, 0.4f),
            ElementTag.Pierce  => new Color(0.85f, 0.9f, 1f),
            ElementTag.Life    => new Color(0.5f, 1f, 0.6f),
            _ => Color.white
        };

        private static string TriggerGlyph(TriggerType t) => t switch
        {
            TriggerType.MeleeHitCount => "拳",
            TriggerType.SkillHitCount => "术",
            TriggerType.CriticalHit => "暴",
            TriggerType.ComboFinisher => "连",
            TriggerType.DodgeFinish => "闪",
            TriggerType.MoveDistance => "步",
            TriggerType.OnDamaged => "伤",
            TriggerType.ShieldBreak => "破",
            TriggerType.LowHealth => "危",
            TriggerType.TimeInterval => "时",
            TriggerType.ChargeComplete => "蓄",
            TriggerType.RoomEnter => "门",
            TriggerType.EnemyKill => "杀",
            TriggerType.EliteKill => "精",
            TriggerType.SeedPlant => "种",
            TriggerType.SeedDetonate => "引",
            TriggerType.BackstabMark => "刺",
            TriggerType.PuppetCount => "傀",
            _ => "触"
        };

        private static string EffectGlyph(EffectType e) => e switch
        {
            EffectType.AreaDamage => "爆",
            EffectType.Projectile => "弹",
            EffectType.SwordWave => "剑",
            EffectType.DoT => "蚀",
            EffectType.Slow => "缓",
            EffectType.Stun => "晕",
            EffectType.Knockback => "击",
            EffectType.MarkVulnerable => "弱",
            EffectType.Heal => "疗",
            EffectType.Shield => "盾",
            EffectType.Cleanse => "净",
            EffectType.Invincible => "无",
            EffectType.Dash => "突",
            EffectType.Pull => "拉",
            EffectType.Teleport => "瞬",
            EffectType.SummonPuppet => "傀",
            EffectType.SummonTurret => "炮",
            EffectType.PoisonPool => "毒",
            EffectType.Trap => "陷",
            EffectType.DetonateSeed => "引",
            EffectType.RefreshStacks => "刷",
            EffectType.GainCharge => "充",
            _ => "效"
        };

        private static string ModifierGlyph(ModifierType m) => m switch
        {
            ModifierType.ShapeWall => "墙",
            ModifierType.ShapeRing => "环",
            ModifierType.ShapeZone => "域",
            ModifierType.TargetFarthest => "远",
            ModifierType.TargetChain => "锁",
            ModifierType.TargetSurround => "绕",
            ModifierType.ExtraCount => "数",
            ModifierType.ExtraProjectile => "增",
            ModifierType.ExtraSummon => "召",
            ModifierType.DelayedBlast => "延",
            ModifierType.Sustained => "续",
            ModifierType.AddBurn => "灼",
            ModifierType.AddFreeze => "冻",
            ModifierType.AddLightning => "雷",
            ModifierType.AddPoison => "毒",
            ModifierType.AddKnockback => "击",
            ModifierType.AddVulnerable => "弱",
            ModifierType.RadiusScale => "阔",
            ModifierType.CountScale => "倍",
            ModifierType.DurationScale => "久",
            ModifierType.DamageScale => "伤",
            ModifierType.CostHP => "血",
            ModifierType.CostCooldown => "却",
            _ => "改"
        };

        // ==================== 悬停 ====================

        private void SetHover(GameObject target, ModuleDef m)
        {
            var trig = target.GetComponent<EventTrigger>();
            if (trig == null) trig = target.AddComponent<EventTrigger>();
            trig.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { _hover = m; RefreshDesc(); });
            trig.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (_hover == m) { _hover = null; RefreshDesc(); } });
            trig.triggers.Add(exit);
        }

        // ==================== 样式辅助 ====================

        private static string FormatModuleName(ModuleDef m)
        {
            string star = m.category == ModuleCategory.Universal ? "☆" : "";
            return $"<color=#{ColorHex(RarityColor(m.rarity))}>{star}{m.displayName}</color>";
        }

        private static Color CategoryColor(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => CTrigger,
            ModuleCategory.Effect => CEffect,
            ModuleCategory.Modifier => CModifier,
            ModuleCategory.Universal => CUniversal,
            _ => Color.white
        };
        private static string CategoryBadge(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => "[触发]",
            ModuleCategory.Effect => "[效果]",
            ModuleCategory.Modifier => "[改造]",
            ModuleCategory.Universal => "[万能]",
            _ => "[?]"
        };
        private static string CategoryGlyph(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => "触",
            ModuleCategory.Effect => "效",
            ModuleCategory.Modifier => "改",
            ModuleCategory.Universal => "万",
            _ => "?"
        };
        private static string CategoryName(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => "触发器",
            ModuleCategory.Effect => "效果器",
            ModuleCategory.Modifier => "改造件",
            ModuleCategory.Universal => "万能件",
            _ => "未知"
        };

        // ---------- V.08 consumeKind / effectRole 角标 ----------

        private static string KindBadge(ConsumeKind k) => k switch
        {
            ConsumeKind.Single => "单发",
            ConsumeKind.Window => "窗口",
            ConsumeKind.Stacks => "叠层",
            ConsumeKind.Auto   => "自动",
            _ => "?"
        };
        private static Color KindColor(ConsumeKind k) => k switch
        {
            ConsumeKind.Single => new Color(1f, 0.7f, 0.3f),
            ConsumeKind.Window => new Color(0.4f, 0.9f, 1f),
            ConsumeKind.Stacks => new Color(0.7f, 0.65f, 1f),
            ConsumeKind.Auto   => new Color(0.55f, 1f, 0.6f),
            _ => Color.white
        };
        private static string RoleBadge(EffectRole r) => r switch
        {
            EffectRole.Enhancement => "增强",
            EffectRole.Addon       => "附加",
            _ => "?"
        };

        // ---------- V.08 链状态 + 人话预览 ----------

        private static string BuildChainStatus(ModuleChain chain)
        {
            if (chain == null || (chain.trigger == null && chain.effect == null))
                return "<color=#5a5d6b>○ 未装配</color>";
            if (chain.trigger == null) return "<color=#ffb74d>○ 待补 触发器</color>";
            if (chain.effect == null) return "<color=#ffb74d>○ 待补 效果器</color>";
            if (!chain.IsValid) return "<color=#ff6b6b>✗ 类型不匹配</color>";
            return "<color=#7eff8a>● 已激活</color>";
        }

        private static string BuildChainMissingHint(ModuleChain chain)
        {
            if (chain == null) return "<color=#5a5d6b>放入 触发器 + 效果器 即可激活</color>";
            if (chain.trigger == null)
                return "<color=#ffb74d>⚠ 已装效果器，再放一个 触发器 即可成链</color>";
            if (chain.effect == null)
                return "<color=#ffb74d>⚠ 已装触发器，再放一个 效果器 即可成链</color>";
            return "<color=#ff6b6b>✗ 触发器/效果器槽位类型不匹配</color>";
        }

        private string BuildChainPreview(ModuleChain chain, int slot)
        {
            if (chain == null || !chain.IsValid) return BuildChainMissingHint(chain);
            var ck = chain.trigger.GetConsumeKindForSlot();
            var er = chain.effect.GetEffectRoleForSlot();
            string proc = TriggerProcText(chain.trigger);
            string consume = ConsumeText(ck, SlotKeyNames[slot]);
            string effect = EffectSummary(chain.effect, er);
            string mods = BuildModifierSuffix(chain.modifier0, chain.modifier1);
            string ckBonus = ConsumeKindBonusText(ck);
            return $"<color=#9fe6c0>✓ {proc}{consume}</color>{effect}{mods}{ckBonus}";
        }

        /// <summary>consumeKind 身份加成说明（V0.1.13 联动，与 ModuleChain 数值同源）。</summary>
        private static string ConsumeKindBonusText(ConsumeKind ck)
        {
            float dmg = ModuleChain.ConsumeKindDamageMul(ck);
            float rad = ModuleChain.ConsumeKindRadiusMul(ck);
            string parts = "";
            if (!Mathf.Approximately(dmg, 1f))
            {
                int pct = Mathf.RoundToInt((dmg - 1f) * 100f);
                string sign = pct >= 0 ? "+" : "";
                string col = pct >= 0 ? "#7eff8a" : "#ff9a6b";
                parts += $"<color={col}>增伤 {sign}{pct}%</color>";
            }
            if (!Mathf.Approximately(rad, 1f))
            {
                int pct = Mathf.RoundToInt((rad - 1f) * 100f);
                if (parts.Length > 0) parts += " ";
                parts += $"<color=#7ec8ff>范围 +{pct}%</color>";
            }
            if (parts.Length == 0)
            {
                // Stacks/中性：说明收益在层数
                if (ck == ConsumeKind.Stacks)
                    return $"\n<color=#8a8f9c>◇ {KindBadge(ck)}：收益来自多次消费（每层各附加一次）</color>";
                return "";
            }
            return $"\n<color=#8a8f9c>◇ {KindBadge(ck)} 联动：</color>{parts}";
        }

        private static string TriggerProcText(ModuleDef t)
        {
            if (t == null) return "条件满足时";
            return t.GetTriggerTypeForSlot() switch
            {
                TriggerType.MeleeHitCount  => $"近战命中 {t.triggerThreshold} 次叠满",
                TriggerType.SkillHitCount  => $"技能命中 {t.triggerThreshold} 次叠满",
                TriggerType.CriticalHit    => $"暴击 {t.triggerThreshold} 次叠满",
                TriggerType.ComboFinisher  => "连击终结时",
                TriggerType.DodgeFinish    => "闪避完成后",
                TriggerType.MoveDistance   => $"移动 {t.moveDistanceThreshold:F0} 米",
                TriggerType.OnDamaged      => "受击时",
                TriggerType.ShieldBreak    => "护盾破裂时",
                TriggerType.LowHealth      => "血量过低时",
                TriggerType.TimeInterval   => $"每 {t.triggerInterval:F0} 秒",
                TriggerType.ChargeComplete => "蓄力满时",
                TriggerType.RoomEnter      => "进入新房间时",
                TriggerType.EnemyKill      => "击杀敌人后",
                TriggerType.EliteKill      => "击杀精英后",
                TriggerType.SeedPlant      => "种子生成时",
                TriggerType.SeedDetonate   => "种子引爆时",
                TriggerType.BackstabMark   => "背击标记时",
                TriggerType.PuppetCount    => $"场上有 {t.triggerThreshold} 傀儡",
                _ => "条件满足时",
            };
        }

        private static string ConsumeText(ConsumeKind ck, string key)
        {
            return ck switch
            {
                ConsumeKind.Single => $" → 按 {key} 时附加：",
                ConsumeKind.Window => $" → 窗口期内按 {key} 附加：",
                ConsumeKind.Stacks => $" → 叠满后按 {key} 附加：",
                ConsumeKind.Auto   => " → 自动释放并附加：",
                _ => " → ",
            };
        }

        private static string EffectSummary(ModuleDef e, EffectRole er)
        {
            if (e == null) return "?";
            string main = e.GetEffectTypeForSlot() switch
            {
                EffectType.AreaDamage      => $"范围伤害 {e.baseDamage:F0}",
                EffectType.Projectile      => $"{e.projectileCount} 发飞弹 ×{e.baseDamage:F0}",
                EffectType.SwordWave       => $"剑气 ×{e.baseDamage:F0}",
                EffectType.DoT             => $"持续毒伤 {e.dotDPS:F0}/s · {e.dotDuration:F0}s",
                EffectType.Slow            => $"减速 {e.slowPercent:F0}%",
                EffectType.Stun            => $"眩晕 {e.stunDuration:F1}s",
                EffectType.Knockback       => $"击退 {e.knockbackForce:F0}",
                EffectType.MarkVulnerable  => $"易伤 +{e.vulnerableMultiplier * 100:F0}% · {e.vulnerableDuration:F0}s",
                EffectType.Heal            => $"回复 {e.healAmount:F0} HP",
                EffectType.Shield          => $"护盾 {e.shieldAmount:F0}",
                EffectType.Cleanse         => "净化负面状态",
                EffectType.Invincible      => $"无敌 {e.invincibleDuration:F1}s",
                EffectType.Dash            => $"突进 {e.dashDistance:F1}m",
                EffectType.Pull            => $"拉拽 {e.pullRadius:F1}m 内敌人",
                EffectType.Teleport        => "传送",
                EffectType.SummonPuppet    => $"召唤傀儡 {e.summonDuration:F0}s",
                EffectType.SummonTurret    => $"召唤炮台 {e.summonDuration:F0}s",
                EffectType.PoisonPool      => $"毒池 {e.dotDPS:F0}/s",
                EffectType.Trap            => $"陷阱 {e.trapDuration:F0}s",
                EffectType.DetonateSeed    => "引爆所有种子",
                EffectType.RefreshStacks   => "刷新层数",
                EffectType.GainCharge      => "获得充能",
                _ => e.displayName,
            };
            string roleTag = er == EffectRole.Addon ? "（附加独立效果）" : "（增强命中）";
            return $"<color=#ffd140>{main}</color> <color=#888>{roleTag}</color>";
        }

        private static string BuildModifierSuffix(ModuleDef m0, ModuleDef m1)
        {
            string s = "";
            if (m0 != null) s += $" <color=#66ea73>+{m0.displayName}</color>";
            if (m1 != null) s += $" <color=#66ea73>+{m1.displayName}</color>";
            return s;
        }

        private static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Fan => new Color(0.85f, 0.87f, 0.92f),
            ItemRarity.Ling => new Color(0.45f, 0.95f, 0.5f),
            ItemRarity.Xuan => new Color(0.4f, 0.7f, 1f),
            ItemRarity.Di => new Color(0.78f, 0.5f, 1f),
            ItemRarity.Tian => new Color(1f, 0.82f, 0.3f),
            _ => Color.white
        };
        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡品", ItemRarity.Ling => "灵品", ItemRarity.Xuan => "玄品",
            ItemRarity.Di => "地品", ItemRarity.Tian => "天品", _ => ""
        };
        private static string ColorHex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>读取槽位 s 绑定的核心技能名（从 PlayerCombat.GetSkillInSlot）。</summary>
        private static string GetBoundCoreSkillName(int s)
        {
            var player = PlayerController.Instance;
            if (player == null) return "<color=#666>无</color>";
            var combat = player.GetComponent<PlayerCombat>();
            if (combat == null) return "<color=#666>无</color>";
            var skill = combat.GetSkillInSlot(s);
            return skill != null ? $"<color=#ffd140>{skill.skillName}</color>" : "<color=#666>无</color>";
        }

        // ==================== 基础 UI 构建 ====================

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(2, 2);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = bg.a > 0.01f;
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string text,
            Vector2 aMin, Vector2 aMax, int fontSize, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(4, 2); rt.offsetMax = new Vector2(-4, -2);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.font = UIBuiltins.LegacyFont;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = style;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return go;
        }
    }

    /// <summary>挂在背包卡片 / 链槽位上，转发拖拽事件给 ModuleAssemblyUI。</summary>
    public class ModuleDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ModuleAssemblyUI ui;
        public bool fromBag;           // true=背包来源；false=槽位来源
        public int slot;               // 槽位来源：链索引
        public int pos;                // 槽位来源：位置索引
        public ModuleDef bagModule;    // 背包来源：当前代表的模块

        public void OnBeginDrag(PointerEventData e) { if (ui != null) ui.DragBegin(this, e); }
        public void OnDrag(PointerEventData e) { if (ui != null) ui.DragMove(e); }
        public void OnEndDrag(PointerEventData e) { if (ui != null) ui.DragEnd(this, e); }
    }

    /// <summary>标记一个可接收模块放置的链槽位。</summary>
    public class ModuleSlotTarget : MonoBehaviour
    {
        public int slot;
        public int pos;
    }

    /// <summary>标记背包区域：把槽位模块拖到这里即卸下。</summary>
    public class ModuleBagTarget : MonoBehaviour { }
}
