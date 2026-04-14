using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 技能栏UI —— 底部居中
    /// 上排：圆形技能图标（Q/E/R + 闪避 + 普攻）
    /// 下排：每个技能下方2个灵物槽位
    /// 
    /// 交互：
    /// 1. 按F拾取灵物/功法 → 自动放入第一个空位
    /// 2. 鼠标按住灵物/技能槽位拖动 → 松开到其他槽位上交换位置
    /// 3. 拖到空白区域松开 → 物品掉落到地面
    /// 4. 悬停显示信息提示
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        // 技能图标引用（由Demo1Setup设置）
        [SerializeField] private RectTransform[] skillSlotRTs;     // 技能槽位RectTransform [Q,E,R,闪避,普攻]
        [SerializeField] private Image[] spiritSlotImages;          // 灵物槽位Image [0~5]
        [SerializeField] private RectTransform[] spiritSlotRTs;     // 灵物槽位RectTransform
        [SerializeField] private Image[] spiritSlotBorders;         // 灵物槽位边框
        [SerializeField] private Text[] spiritSlotLabels;           // 灵物槽位标签文字

        // ==================== 拖拽系统 ====================

        /// <summary>拖拽物品类型</summary>
        private enum DragType { None, Spirit, Skill }

        private DragType _dragType = DragType.None;
        private int _dragSourceSlot = -1;        // 拖拽来源槽位
        private bool _isDragging;

        /// <summary>是否正在拖拽UI（供外部查询，屏蔽战斗输入）</summary>
        public bool IsDragging => _isDragging;

        /// <summary>鼠标是否在任何槽位上方（供外部查询，屏蔽攻击输入）</summary>
        public bool IsMouseOverSlot
        {
            get
            {
                if (_isDragging) return true;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return false;
                Vector2 pos = mouse.position.ReadValue();
                return FindSpiritSlotAt(pos) >= 0 || FindSkillSlotAt(pos) >= 0;
            }
        }
        private bool _dragStarted;               // 是否已经开始移动（防止点击误触）
        private Vector2 _dragStartPos;            // 按下时的鼠标位置
        private const float DRAG_THRESHOLD = 5f;  // 拖拽启动阈值（像素）

        // 拖拽幽灵
        private GameObject _dragGhost;
        private Image _dragGhostImage;
        private Text _dragGhostLabel;
        private RectTransform _dragGhostRT;

        // ==================== 悬停提示 ====================

        private GameObject _tooltipPanel;
        private Text _tooltipTitle;
        private Text _tooltipDesc;
        private Text _tooltipEffect;
        private RectTransform _tooltipRT;

        // 悬停状态
        private int _hoverSpiritSlot = -1;
        private int _hoverSkillSlot = -1;

        private Canvas _parentCanvas;

        /// <summary>单例（方便Pickup脚本调用）</summary>
        public static SkillBarUI Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            CreateTooltip();
            CreateDragGhost();
            RefreshAllSlots();

            GameEvents.Subscribe<GameEvents.SpiritSlotChanged>(OnSpiritSlotChanged);
            GameEvents.Subscribe<GameEvents.SkillEquipped>(OnSkillEquipped);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.Unsubscribe<GameEvents.SpiritSlotChanged>(OnSpiritSlotChanged);
            GameEvents.Unsubscribe<GameEvents.SkillEquipped>(OnSkillEquipped);
        }

        private void Update()
        {
            HandleDrag();
            if (!_isDragging)
                HandleHover();
        }

        // ==================== 拖拽系统（鼠标按住拖动换位） ====================

        private void HandleDrag()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();

            if (!_isDragging)
            {
                // 检测鼠标按下 → 开始拖拽准备
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    // 检查是否按在灵物槽位上
                    int spiritSlot = FindSpiritSlotAt(mousePos);
                    if (spiritSlot >= 0)
                    {
                        var spiritSlots = PlayerController.Instance?.GetComponent<SpiritSlotSystem>();
                        if (spiritSlots != null && spiritSlot < spiritSlots.Slots.Count
                            && spiritSlots.Slots[spiritSlot].item != null)
                        {
                            _isDragging = true;
                            _dragStarted = false;
                            _dragStartPos = mousePos;
                            _dragType = DragType.Spirit;
                            _dragSourceSlot = spiritSlot;
                            HideTooltip();
                            return;
                        }
                    }

                    // 检查是否按在技能槽位上（Q/E/R）
                    int skillSlot = FindSkillSlotAt(mousePos);
                    if (skillSlot >= 0 && skillSlot < 3)
                    {
                        var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
                        if (combat != null && combat.GetSkillInSlot(skillSlot) != null)
                        {
                            _isDragging = true;
                            _dragStarted = false;
                            _dragStartPos = mousePos;
                            _dragType = DragType.Skill;
                            _dragSourceSlot = skillSlot;
                            HideTooltip();
                            return;
                        }
                    }
                }
            }
            else
            {
                // 拖拽中
                if (mouse.leftButton.isPressed)
                {
                    // 检测是否超过拖拽阈值
                    if (!_dragStarted)
                    {
                        float dist = Vector2.Distance(mousePos, _dragStartPos);
                        if (dist >= DRAG_THRESHOLD)
                        {
                            _dragStarted = true;
                            ShowDragGhost();
                            // 源槽位变暗
                            DimSourceSlot();
                        }
                    }

                    if (_dragStarted)
                    {
                        // 幽灵跟随鼠标
                        if (_dragGhostRT != null && _parentCanvas != null)
                        {
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                (RectTransform)_parentCanvas.transform, mousePos, null, out Vector2 localPos);
                            _dragGhostRT.anchoredPosition = localPos;
                        }

                        // 高亮可放置的目标槽位
                        HighlightDropTargets(mousePos);
                    }
                }

                // 鼠标松开 → 完成拖拽
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (_dragStarted)
                    {
                        // 真正拖拽了 → 尝试放置
                        TryDrop(mousePos);
                    }
                    // 没超过阈值就松开 → 当作普通点击，什么都不做
                    EndDrag();
                }
            }
        }

        /// <summary>显示拖拽幽灵</summary>
        private void ShowDragGhost()
        {
            if (_dragGhost == null) return;

            if (_dragType == DragType.Spirit)
            {
                var spiritSlots = PlayerController.Instance?.GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null && _dragSourceSlot < spiritSlots.Slots.Count)
                {
                    var item = spiritSlots.Slots[_dragSourceSlot].item;
                    if (item != null)
                    {
                        _dragGhostImage.color = item.GetRarityColor() * 0.9f;
                        _dragGhostLabel.text = item.itemName;
                        _dragGhostLabel.color = item.GetRarityColor();
                    }
                }
            }
            else if (_dragType == DragType.Skill)
            {
                var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    var skill = combat.GetSkillInSlot(_dragSourceSlot);
                    if (skill != null)
                    {
                        Color c = skill.rarity switch
                        {
                            ItemRarity.Fan => Color.white,
                            ItemRarity.Ling => Color.green,
                            ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                            ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                            ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                            _ => Color.white
                        };
                        _dragGhostImage.color = c * 0.9f;
                        _dragGhostLabel.text = skill.skillName;
                        _dragGhostLabel.color = c;
                    }
                }
            }

            _dragGhost.SetActive(true);
        }

        /// <summary>源槽位变暗</summary>
        private void DimSourceSlot()
        {
            if (_dragType == DragType.Spirit && _dragSourceSlot < spiritSlotImages.Length)
            {
                spiritSlotImages[_dragSourceSlot].color *= 0.3f;
            }
        }

        /// <summary>尝试放置到目标槽位</summary>
        private void TryDrop(Vector2 screenPos)
        {
            if (PlayerController.Instance == null) return;

            if (_dragType == DragType.Spirit)
            {
                int target = FindSpiritSlotAt(screenPos);
                if (target >= 0 && target != _dragSourceSlot)
                {
                    // 交换灵物槽位
                    var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
                    if (spiritSlots != null)
                    {
                        spiritSlots.SwapSlots(_dragSourceSlot, target);
                        Debug.Log($"<color=cyan>灵物槽位交换：{_dragSourceSlot} ↔ {target}</color>");
                    }
                }
                else if (target < 0)
                {
                    // 拖到空白区域 → 丢弃到地面
                    var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
                    if (spiritSlots != null)
                    {
                        var item = spiritSlots.Slots[_dragSourceSlot].item;
                        if (item != null)
                        {
                            spiritSlots.RemoveFromSlot(_dragSourceSlot);
                            Vector3 dropPos = PlayerController.Instance.transform.position +
                                Random.insideUnitSphere * 1.5f;
                            dropPos.y = PlayerController.Instance.transform.position.y + 0.5f;
                            ItemPickup.Spawn(item, dropPos);
                            Debug.Log($"<color=gray>丢弃灵物：{item.itemName}</color>");
                        }
                    }
                }
            }
            else if (_dragType == DragType.Skill)
            {
                int target = FindSkillSlotAt(screenPos);
                if (target >= 0 && target < 3 && target != _dragSourceSlot)
                {
                    // 交换技能槽位
                    var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                    if (combat != null)
                    {
                        combat.SwapSkills(_dragSourceSlot, target);
                        Debug.Log($"<color=cyan>技能槽位交换：{_dragSourceSlot} ↔ {target}</color>");
                    }
                }
                else if (target < 0 || target >= 3)
                {
                    // 拖到空白区域 → 丢弃到地面
                    var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                    if (combat != null)
                    {
                        var skill = combat.GetSkillInSlot(_dragSourceSlot);
                        if (skill != null)
                        {
                            combat.UnequipSkill(_dragSourceSlot);
                            Vector3 dropPos = PlayerController.Instance.transform.position +
                                Random.insideUnitSphere * 1.5f;
                            dropPos.y = PlayerController.Instance.transform.position.y + 0.5f;
                            SkillPickup.Spawn(skill, dropPos);
                            Debug.Log($"<color=gray>丢弃功法：{skill.skillName}</color>");
                        }
                    }
                }
            }
        }

        /// <summary>结束拖拽</summary>
        private void EndDrag()
        {
            _isDragging = false;
            _dragStarted = false;
            _dragType = DragType.None;
            _dragSourceSlot = -1;
            if (_dragGhost != null) _dragGhost.SetActive(false);
            ResetSlotHighlights();
            RefreshAllSlots();
        }

        /// <summary>高亮可放置的目标槽位</summary>
        private void HighlightDropTargets(Vector2 screenPos)
        {
            if (_dragType == DragType.Spirit)
            {
                for (int i = 0; i < spiritSlotRTs.Length; i++)
                {
                    if (i == _dragSourceSlot) continue;
                    if (spiritSlotBorders == null || i >= spiritSlotBorders.Length) continue;
                    bool isOver = spiritSlotRTs[i] != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(spiritSlotRTs[i], screenPos, null);
                    spiritSlotBorders[i].color = isOver
                        ? new Color(1f, 0.85f, 0.3f, 0.9f)   // 高亮金色
                        : new Color(0.5f, 0.5f, 0.6f, 0.4f);  // 普通
                }
            }
        }

        private void ResetSlotHighlights()
        {
            if (spiritSlotBorders == null) return;
            for (int i = 0; i < spiritSlotBorders.Length; i++)
            {
                if (spiritSlotBorders[i] != null)
                    spiritSlotBorders[i].color = new Color(0.3f, 0.3f, 0.35f, 0.4f);
            }
        }

        // ==================== 槽位查找 ====================

        private int FindSpiritSlotAt(Vector2 screenPos)
        {
            for (int i = 0; i < spiritSlotRTs.Length; i++)
            {
                if (spiritSlotRTs[i] != null && RectTransformUtility.RectangleContainsScreenPoint(
                    spiritSlotRTs[i], screenPos, null))
                    return i;
            }
            return -1;
        }

        private int FindSkillSlotAt(Vector2 screenPos)
        {
            for (int i = 0; i < skillSlotRTs.Length; i++)
            {
                if (skillSlotRTs[i] != null && RectTransformUtility.RectangleContainsScreenPoint(
                    skillSlotRTs[i], screenPos, null))
                    return i;
            }
            return -1;
        }

        // ==================== 刷新显示 ====================

        public void RefreshAllSlots()
        {
            if (PlayerController.Instance == null) return;
            var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
            if (spiritSlots == null) return;

            for (int i = 0; i < spiritSlotImages.Length && i < spiritSlots.Slots.Count; i++)
            {
                RefreshSpiritSlot(i, spiritSlots.Slots[i].item);
            }
        }

        private void RefreshSpiritSlot(int index, ItemData item)
        {
            if (index < 0 || index >= spiritSlotImages.Length) return;

            if (item != null)
            {
                spiritSlotImages[index].color = item.GetRarityColor() * 0.8f;
                if (spiritSlotBorders != null && index < spiritSlotBorders.Length)
                    spiritSlotBorders[index].color = item.GetRarityColor() * 0.6f;
                if (spiritSlotLabels != null && index < spiritSlotLabels.Length)
                {
                    // 大槽位可以显示完整名称（最多4字）
                    spiritSlotLabels[index].text = item.itemName.Length <= 5
                        ? item.itemName : item.itemName.Substring(0, 4);
                    spiritSlotLabels[index].color = Color.white;
                    spiritSlotLabels[index].fontStyle = FontStyle.Bold;
                }
            }
            else
            {
                spiritSlotImages[index].color = new Color(0.15f, 0.15f, 0.2f, 0.5f);
                if (spiritSlotBorders != null && index < spiritSlotBorders.Length)
                    spiritSlotBorders[index].color = new Color(0.3f, 0.3f, 0.35f, 0.4f);
                if (spiritSlotLabels != null && index < spiritSlotLabels.Length)
                {
                    spiritSlotLabels[index].text = "";
                    spiritSlotLabels[index].color = new Color(0.4f, 0.4f, 0.4f, 0.3f);
                }
            }
        }

        private void OnSpiritSlotChanged(GameEvents.SpiritSlotChanged evt) => RefreshAllSlots();
        private void OnSkillEquipped(GameEvents.SkillEquipped evt) { /* 可刷新技能图标 */ }

        // ==================== 悬停提示 ====================

        private void HandleHover()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();

            int newSpiritHover = FindSpiritSlotAt(mousePos);
            int newSkillHover = newSpiritHover < 0 ? FindSkillSlotAt(mousePos) : -1;

            if (newSpiritHover != _hoverSpiritSlot || newSkillHover != _hoverSkillSlot)
            {
                _hoverSpiritSlot = newSpiritHover;
                _hoverSkillSlot = newSkillHover;
                UpdateTooltip(mousePos);
            }

            if (_tooltipPanel != null && _tooltipPanel.activeSelf)
                PositionTooltip(mousePos);
        }

        private void UpdateTooltip(Vector2 mousePos)
        {
            if (PlayerController.Instance == null) { HideTooltip(); return; }

            if (_hoverSpiritSlot >= 0)
            {
                var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null && _hoverSpiritSlot < spiritSlots.Slots.Count)
                {
                    var item = spiritSlots.Slots[_hoverSpiritSlot].item;
                    if (item != null)
                    {
                        string rarityName = item.rarity switch
                        {
                            ItemRarity.Fan => "凡品",
                            ItemRarity.Ling => "灵品",
                            ItemRarity.Xuan => "玄品",
                            ItemRarity.Di => "地品",
                            ItemRarity.Tian => "天品",
                            _ => "凡品"
                        };
                        _tooltipTitle.text = $"{item.itemName}（{rarityName}）";
                        _tooltipTitle.color = item.GetRarityColor();
                        _tooltipDesc.text = item.description;
                        _tooltipEffect.text = GetItemEffectText(item) + "\n<color=#888>拖拽换位 | 拖出丢弃</color>";
                        _tooltipPanel.SetActive(true);
                        _tooltipPanel.transform.SetAsLastSibling();
                        PositionTooltip(mousePos);
                        return;
                    }
                    else
                    {
                        int skillIdx = _hoverSpiritSlot / SpiritSlotSystem.SLOTS_PER_SKILL;
                        string[] names = { "Q", "E", "R" };
                        _tooltipTitle.text = $"空灵物槽 ({(skillIdx < names.Length ? names[skillIdx] : "?")})";
                        _tooltipTitle.color = new Color(0.5f, 0.5f, 0.5f);
                        _tooltipDesc.text = "拾取灵物后自动填入";
                        _tooltipEffect.text = "";
                        _tooltipPanel.SetActive(true);
                        _tooltipPanel.transform.SetAsLastSibling();
                        PositionTooltip(mousePos);
                        return;
                    }
                }
            }

            if (_hoverSkillSlot >= 0 && _hoverSkillSlot < 3)
            {
                var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    var skill = combat.GetSkillInSlot(_hoverSkillSlot);
                    string[] keys = { "Q", "E", "R" };
                    if (skill != null)
                    {
                        string typeStr = skill.skillType switch
                        {
                SkillType.AreaDamage => "范围伤害",
                SkillType.Projectile => "投射物",
                SkillType.Dash => "位移",
                SkillType.Buff => "增益",
                SkillType.Heal => "治疗",
                SkillType.Summon => "召唤",
                            _ => "未知"
                        };
                        _tooltipTitle.text = $"[{keys[_hoverSkillSlot]}] {skill.skillName}";
                        _tooltipTitle.color = new Color(0.5f, 0.85f, 1f);
                        _tooltipDesc.text = skill.description;
                        _tooltipEffect.text = $"类型：{typeStr}  |  CD: {skill.cooldown}s  |  伤害: {skill.baseDamage}\n<color=#888>拖拽换位 | 拖出丢弃</color>";
                        _tooltipPanel.SetActive(true);
                        _tooltipPanel.transform.SetAsLastSibling();
                        PositionTooltip(mousePos);
                        return;
                    }
                    else
                    {
                        _tooltipTitle.text = $"[{keys[_hoverSkillSlot]}] 空技能槽";
                        _tooltipTitle.color = new Color(0.5f, 0.5f, 0.5f);
                        _tooltipDesc.text = "拾取功法后自动装备";
                        _tooltipEffect.text = "";
                        _tooltipPanel.SetActive(true);
                        _tooltipPanel.transform.SetAsLastSibling();
                        PositionTooltip(mousePos);
                        return;
                    }
                }
            }

            HideTooltip();
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }

        private void PositionTooltip(Vector2 screenPos)
        {
            if (_tooltipRT == null || _parentCanvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_parentCanvas.transform, screenPos, null, out Vector2 localPos);

            // 在鼠标上方显示，留出间距
            Vector2 offset = new Vector2(0, 40);
            Vector2 pos = localPos + offset;

            // 确保不超出屏幕边界
            var canvasRT = (RectTransform)_parentCanvas.transform;
            float halfW = _tooltipRT.sizeDelta.x / 2f;
            float tooltipH = _tooltipRT.sizeDelta.y;
            float canvasHalfW = canvasRT.sizeDelta.x / 2f;
            float canvasHalfH = canvasRT.sizeDelta.y / 2f;

            // 左右边界
            pos.x = Mathf.Clamp(pos.x, -canvasHalfW + halfW + 5, canvasHalfW - halfW - 5);
            // 上边界（如果超出顶部，改为显示在鼠标下方）
            if (pos.y + tooltipH > canvasHalfH)
                pos = localPos + new Vector2(0, -tooltipH - 10);

            _tooltipRT.anchoredPosition = pos;
        }

        private string GetItemEffectText(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"攻+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻+{item.attackBonusPercent * 100}%");
            if (item.maxHpBonus > 0) parts.Add($"命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"命+{item.maxHpBonusPercent * 100}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"速+{item.moveSpeedBonusPercent * 100}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤+{item.damageReductionBonus * 100}%");
            if (item.critRateBonus > 0) parts.Add($"暴击+{item.critRateBonus * 100}%");
            if (item.healOnKill > 0) parts.Add($"回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            return parts.Count > 0 ? string.Join(" ", parts) : "";
        }

        // ==================== UI创建 ====================

        private void CreateTooltip()
        {
            _tooltipPanel = new GameObject("SkillBarTooltip");
            // 挂到Canvas根节点下，避免坐标系不匹配导致定位错误
            var canvasRoot = _parentCanvas != null ? _parentCanvas.transform : transform;
            _tooltipPanel.transform.SetParent(canvasRoot, false);
            _tooltipRT = _tooltipPanel.AddComponent<RectTransform>();
            _tooltipRT.sizeDelta = new Vector2(280, 140);
            _tooltipRT.pivot = new Vector2(0.5f, 0);

            var bg = _tooltipPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
            bg.raycastTarget = false;

            // 确保tooltip在最上层
            _tooltipPanel.transform.SetAsLastSibling();

            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_tooltipPanel.transform, false);
            var borderRT = borderGo.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-1, -1);
            borderRT.offsetMax = new Vector2(1, 1);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
            borderImg.raycastTarget = false;
            borderGo.transform.SetAsFirstSibling();

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_tooltipPanel.transform, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.6f);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(8, 0);
            titleRT.offsetMax = new Vector2(-8, -4);
            _tooltipTitle = titleGo.AddComponent<Text>();
            _tooltipTitle.fontSize = 18;
            _tooltipTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipTitle.alignment = TextAnchor.MiddleCenter;
            _tooltipTitle.fontStyle = FontStyle.Bold;
            _tooltipTitle.raycastTarget = false;
            _tooltipTitle.supportRichText = true;
            _tooltipTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            var titleOutline = titleGo.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(1, -1);

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(_tooltipPanel.transform, false);
            var descRT = descGo.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0.25f);
            descRT.anchorMax = new Vector2(1, 0.6f);
            descRT.offsetMin = new Vector2(8, 0);
            descRT.offsetMax = new Vector2(-8, 0);
            _tooltipDesc = descGo.AddComponent<Text>();
            _tooltipDesc.fontSize = 13;
            _tooltipDesc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipDesc.alignment = TextAnchor.MiddleCenter;
            _tooltipDesc.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            _tooltipDesc.raycastTarget = false;
            _tooltipDesc.supportRichText = true;
            _tooltipDesc.horizontalOverflow = HorizontalWrapMode.Overflow;

            var effectGo = new GameObject("Effect");
            effectGo.transform.SetParent(_tooltipPanel.transform, false);
            var effectRT = effectGo.AddComponent<RectTransform>();
            effectRT.anchorMin = new Vector2(0, 0);
            effectRT.anchorMax = new Vector2(1, 0.25f);
            effectRT.offsetMin = new Vector2(8, 4);
            effectRT.offsetMax = new Vector2(-8, 0);
            _tooltipEffect = effectGo.AddComponent<Text>();
            _tooltipEffect.fontSize = 12;
            _tooltipEffect.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipEffect.alignment = TextAnchor.MiddleCenter;
            _tooltipEffect.color = new Color(0.6f, 0.9f, 0.6f, 0.8f);
            _tooltipEffect.raycastTarget = false;
            _tooltipEffect.supportRichText = true;
            _tooltipEffect.horizontalOverflow = HorizontalWrapMode.Overflow;

            _tooltipPanel.SetActive(false);
        }

        private void CreateDragGhost()
        {
            _dragGhost = new GameObject("DragGhost");
            _dragGhost.transform.SetParent(transform, false);
            _dragGhostRT = _dragGhost.AddComponent<RectTransform>();
            _dragGhostRT.sizeDelta = new Vector2(48, 48);

            _dragGhostImage = _dragGhost.AddComponent<Image>();
            _dragGhostImage.color = Color.white;
            _dragGhostImage.raycastTarget = false;

            var outline = _dragGhost.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            // 名称标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_dragGhost.transform, false);
            var labelRT = labelGo.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.5f, 1);
            labelRT.anchorMax = new Vector2(0.5f, 1);
            labelRT.pivot = new Vector2(0.5f, 0);
            labelRT.anchoredPosition = new Vector2(0, 4);
            labelRT.sizeDelta = new Vector2(120, 20);
            _dragGhostLabel = labelGo.AddComponent<Text>();
            _dragGhostLabel.fontSize = 13;
            _dragGhostLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _dragGhostLabel.alignment = TextAnchor.MiddleCenter;
            _dragGhostLabel.raycastTarget = false;
            var labelOutline = labelGo.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0, 0, 0, 0.9f);
            labelOutline.effectDistance = new Vector2(1, -1);

            _dragGhost.SetActive(false);
        }
    }
}
