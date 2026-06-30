using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 技能栏UI —— 底部居中
    /// 圆形技能图标（Q/E/R + 闪避 + 普攻）
    ///
    /// 交互：
    /// 1. 按F拾取功法 → 自动放入第一个空位
    /// 2. 鼠标按住技能槽位拖动 → 松开到其他槽位上交换位置
    /// 3. 拖到空白区域松开 → 功法掉落到地面
    /// 4. 悬停显示信息提示
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        [SerializeField] private RectTransform[] skillSlotRTs;     // 技能槽位RectTransform [Q,E,R,闪避,普攻]

        // ==================== 拖拽系统 ====================

        private bool _isDragging;
        private int _dragSourceSlot = -1;

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
                return FindSkillSlotAt(pos) >= 0;
            }
        }
        private bool _dragStarted;
        private Vector2 _dragStartPos;
        private const float DRAG_THRESHOLD = 5f;

        // 拖拽幽灵
        private GameObject _dragGhost;
        private Image _dragGhostImage;
        private Text _dragGhostLabel;
        private RectTransform _dragGhostRT;
        private Text _dragGhostKeyLabel;

        // ==================== 悬停提示 ====================

        private GameObject _tooltipPanel;
        private Text _tooltipTitle;
        private Text _tooltipDesc;
        private Text _tooltipEffect;
        private RectTransform _tooltipRT;

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
            RefreshSkillSlots();

            GameEvents.Subscribe<GameEvents.SkillEquipped>(OnSkillEquipped);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    int skillSlot = FindSkillSlotAt(mousePos);
                    if (skillSlot >= 0 && skillSlot < 3)
                    {
                        var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
                        if (combat != null && combat.GetSkillInSlot(skillSlot) != null)
                        {
                            _isDragging = true;
                            _dragStarted = false;
                            _dragStartPos = mousePos;
                            _dragSourceSlot = skillSlot;
                            HideTooltip();
                            return;
                        }
                    }
                }
            }
            else
            {
                if (mouse.leftButton.isPressed)
                {
                    if (!_dragStarted)
                    {
                        float dist = Vector2.Distance(mousePos, _dragStartPos);
                        if (dist >= DRAG_THRESHOLD)
                        {
                            _dragStarted = true;
                            ShowDragGhost();
                        }
                    }

                    if (_dragStarted)
                    {
                        if (_dragGhostRT != null && _parentCanvas != null)
                        {
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                (RectTransform)_parentCanvas.transform, mousePos, null, out Vector2 localPos);
                            _dragGhostRT.anchoredPosition = localPos;
                        }
                    }
                }

                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (_dragStarted)
                        TryDrop(mousePos);
                    EndDrag();
                }
            }
        }

        private void ShowDragGhost()
        {
            if (_dragGhost == null) return;

            var iconText = _dragGhost.transform.Find("IconText")?.GetComponent<Text>();
            var glow = _dragGhost.transform.Find("Glow")?.GetComponent<Image>();

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                var skill = combat.GetSkillInSlot(_dragSourceSlot);
                if (skill != null)
                {
                    Color c = GetRarityColor(skill.rarity);
                    _dragGhostImage.color = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.9f);
                    _dragGhostLabel.text = skill.skillName;
                    _dragGhostLabel.color = c;
                    if (_dragGhostKeyLabel != null)
                    {
                        string[] keys = { "Q", "E", "R" };
                        _dragGhostKeyLabel.text = _dragSourceSlot < keys.Length ? keys[_dragSourceSlot] : "";
                    }
                    if (iconText != null)
                    {
                        iconText.text = skill.skillName.Length > 0 ? skill.skillName.Substring(0, 1) : "?";
                        iconText.color = c;
                    }
                    if (glow != null)
                        glow.color = new Color(c.r, c.g, c.b, 0.4f);
                }
            }

            _dragGhost.SetActive(true);
            _dragGhost.transform.SetAsLastSibling();
        }

        private void TryDrop(Vector2 screenPos)
        {
            if (PlayerController.Instance == null) return;

            int target = FindSkillSlotAt(screenPos);
            if (target >= 0 && target < 3 && target != _dragSourceSlot)
            {
                var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    combat.SwapSkills(_dragSourceSlot, target);
                    Debug.Log($"<color=cyan>技能槽位交换：{_dragSourceSlot} ↔ {target}</color>");
                }
            }
            else if (target < 0 || target >= 3)
            {
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

        private void EndDrag()
        {
            _isDragging = false;
            _dragStarted = false;
            _dragSourceSlot = -1;
            if (_dragGhost != null) _dragGhost.SetActive(false);
            RefreshSkillSlots();
        }

        // ==================== 槽位查找 ====================

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

        private void OnSkillEquipped(GameEvents.SkillEquipped evt) => RefreshSkillSlots();

        /// <summary>刷新技能槽位显示（空槽暗色虚化，有技能显示品阶色+功法名+发光边框）</summary>
        public void RefreshSkillSlots()
        {
            if (PlayerController.Instance == null) return;
            var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
            if (combat == null) return;

            string[] keys = { "Q", "E", "R" };
            for (int i = 0; i < 3 && i < skillSlotRTs.Length; i++)
            {
                if (skillSlotRTs[i] == null) continue;
                var skill = combat.GetSkillInSlot(i);
                var slotImg = skillSlotRTs[i].GetComponent<Image>();

                var borderTf = skillSlotRTs[i].Find($"SkillBorder_{i}");
                var iconTf = skillSlotRTs[i].Find($"SkillIcon_{i}");
                var cdTextTf = skillSlotRTs[i].Find($"SkillCDText_{i}");
                var borderImg = borderTf?.GetComponent<Image>();
                var iconImg = iconTf?.GetComponent<Image>();
                var cdText = cdTextTf?.GetComponent<Text>();

                var nameLabelTf = skillSlotRTs[i].Find("SkillNameLabel");
                Text nameLabel = null;
                if (nameLabelTf == null)
                {
                    var nameLabelGo = new GameObject("SkillNameLabel");
                    nameLabelGo.transform.SetParent(skillSlotRTs[i], false);
                    var nlRT = nameLabelGo.AddComponent<RectTransform>();
                    nlRT.anchorMin = new Vector2(0.5f, 0);
                    nlRT.anchorMax = new Vector2(0.5f, 0);
                    nlRT.pivot = new Vector2(0.5f, 1);
                    nlRT.anchoredPosition = new Vector2(0, -2);
                    nlRT.sizeDelta = new Vector2(100, 16);
                    nameLabel = nameLabelGo.AddComponent<Text>();
                    nameLabel.fontSize = 11;
                    nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    nameLabel.alignment = TextAnchor.MiddleCenter;
                    nameLabel.fontStyle = FontStyle.Bold;
                    nameLabel.raycastTarget = false;
                    nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var nlOutline = nameLabelGo.AddComponent<Outline>();
                    nlOutline.effectColor = new Color(0, 0, 0, 0.9f);
                    nlOutline.effectDistance = new Vector2(1, -1);
                }
                else
                {
                    nameLabel = nameLabelTf.GetComponent<Text>();
                }

                if (skill != null)
                {
                    Color c = GetRarityColor(skill.rarity);

                    if (slotImg != null)
                        slotImg.color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.9f);
                    if (borderImg != null)
                        borderImg.color = new Color(c.r, c.g, c.b, 0.8f);
                    if (iconImg != null)
                        iconImg.color = new Color(c.r, c.g, c.b, 0.25f);
                    if (cdText != null)
                        cdText.color = Color.white;
                    if (nameLabel != null)
                    {
                        string displayName = skill.skillName.Length <= 4
                            ? skill.skillName : skill.skillName.Substring(0, 4);
                        nameLabel.text = displayName;
                        nameLabel.color = c;
                        nameLabel.gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (slotImg != null)
                        slotImg.color = new Color(0.08f, 0.08f, 0.12f, 0.35f);
                    if (borderImg != null)
                        borderImg.color = new Color(0.25f, 0.25f, 0.3f, 0.25f);
                    if (iconImg != null)
                        iconImg.color = new Color(0.3f, 0.3f, 0.3f, 0.05f);
                    if (cdText != null)
                        cdText.color = new Color(0.4f, 0.4f, 0.45f, 0.5f);
                    if (nameLabel != null)
                    {
                        nameLabel.text = "";
                        nameLabel.gameObject.SetActive(false);
                    }
                }
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Ling => new Color(0.3f, 0.85f, 0.3f),
                ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                _ => Color.white
            };
        }

        // ==================== 悬停提示 ====================

        private void HandleHover()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();

            int newSkillHover = FindSkillSlotAt(mousePos);

            if (newSkillHover != _hoverSkillSlot)
            {
                _hoverSkillSlot = newSkillHover;
                UpdateTooltip(mousePos);
            }

            if (_tooltipPanel != null && _tooltipPanel.activeSelf)
                PositionTooltip(mousePos);
        }

        private void UpdateTooltip(Vector2 mousePos)
        {
            if (PlayerController.Instance == null) { HideTooltip(); return; }

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

            Vector2 offset = new Vector2(0, 40);
            Vector2 pos = localPos + offset;

            var canvasRT = (RectTransform)_parentCanvas.transform;
            float halfW = _tooltipRT.sizeDelta.x / 2f;
            float tooltipH = _tooltipRT.sizeDelta.y;
            float canvasHalfW = canvasRT.sizeDelta.x / 2f;
            float canvasHalfH = canvasRT.sizeDelta.y / 2f;

            pos.x = Mathf.Clamp(pos.x, -canvasHalfW + halfW + 5, canvasHalfW - halfW - 5);
            if (pos.y + tooltipH > canvasHalfH)
                pos = localPos + new Vector2(0, -tooltipH - 10);

            _tooltipRT.anchoredPosition = pos;
        }

        // ==================== UI创建 ====================

        private void CreateTooltip()
        {
            _tooltipPanel = new GameObject("SkillBarTooltip");
            var canvasRoot = _parentCanvas != null ? _parentCanvas.transform : transform;
            _tooltipPanel.transform.SetParent(canvasRoot, false);
            _tooltipRT = _tooltipPanel.AddComponent<RectTransform>();
            _tooltipRT.sizeDelta = new Vector2(280, 140);
            _tooltipRT.pivot = new Vector2(0.5f, 0);

            var bg = _tooltipPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
            bg.raycastTarget = false;

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
            var canvasRoot = _parentCanvas != null ? _parentCanvas.transform : transform;
            _dragGhost.transform.SetParent(canvasRoot, false);
            _dragGhostRT = _dragGhost.AddComponent<RectTransform>();
            _dragGhostRT.sizeDelta = new Vector2(56, 56);

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(_dragGhost.transform, false);
            var glowRT = glowGo.AddComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-4, -4);
            glowRT.offsetMax = new Vector2(4, 4);
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.color = new Color(1f, 0.85f, 0.3f, 0.4f);
            glowImg.raycastTarget = false;

            _dragGhostImage = _dragGhost.AddComponent<Image>();
            _dragGhostImage.color = Color.white;
            _dragGhostImage.raycastTarget = false;

            var outline = _dragGhost.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            var keyGo = new GameObject("KeyLabel");
            keyGo.transform.SetParent(_dragGhost.transform, false);
            var keyRT = keyGo.AddComponent<RectTransform>();
            keyRT.anchorMin = new Vector2(0, 1);
            keyRT.anchorMax = new Vector2(0, 1);
            keyRT.pivot = new Vector2(0, 1);
            keyRT.anchoredPosition = new Vector2(2, -2);
            keyRT.sizeDelta = new Vector2(20, 16);
            _dragGhostKeyLabel = keyGo.AddComponent<Text>();
            _dragGhostKeyLabel.fontSize = 11;
            _dragGhostKeyLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _dragGhostKeyLabel.alignment = TextAnchor.UpperLeft;
            _dragGhostKeyLabel.fontStyle = FontStyle.Bold;
            _dragGhostKeyLabel.color = new Color(1, 1, 1, 0.7f);
            _dragGhostKeyLabel.raycastTarget = false;
            var keyOutline = keyGo.AddComponent<Outline>();
            keyOutline.effectColor = new Color(0, 0, 0, 0.9f);
            keyOutline.effectDistance = new Vector2(1, -1);

            var iconTextGo = new GameObject("IconText");
            iconTextGo.transform.SetParent(_dragGhost.transform, false);
            var iconTextRT = iconTextGo.AddComponent<RectTransform>();
            iconTextRT.anchorMin = Vector2.zero;
            iconTextRT.anchorMax = Vector2.one;
            iconTextRT.offsetMin = new Vector2(2, 2);
            iconTextRT.offsetMax = new Vector2(-2, -14);
            var iconText = iconTextGo.AddComponent<Text>();
            iconText.fontSize = 22;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.fontStyle = FontStyle.Bold;
            iconText.raycastTarget = false;
            iconText.color = Color.white;
            var iconOutline = iconTextGo.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0, 0, 0, 0.8f);
            iconOutline.effectDistance = new Vector2(1, -1);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_dragGhost.transform, false);
            var labelRT = labelGo.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.5f, 0);
            labelRT.anchorMax = new Vector2(0.5f, 0);
            labelRT.pivot = new Vector2(0.5f, 1);
            labelRT.anchoredPosition = new Vector2(0, -2);
            labelRT.sizeDelta = new Vector2(120, 18);
            _dragGhostLabel = labelGo.AddComponent<Text>();
            _dragGhostLabel.fontSize = 12;
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
