using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 技能升级房间（V0.4.6 UI 改 uGUI+TMP）—— 花费灵力碎片升级已装备的功法。
    /// 进入房间靠近修炼台按F打开升级面板。每个技能可升级：伤害+15%、CD-10%、充能+1层。
    /// </summary>
    public class UpgradeRoom : MonoBehaviour, IInteractable
    {
        private int _roomIndex;
        private GameObject _roomVisuals;
        private Transform _masterTransform;

        // ===== IInteractable：参与统一 F 交互路由 =====
        public Vector3 InteractionWorldPos =>
            _masterTransform != null ? _masterTransform.position : transform.position;
        public int InteractionPriority => 35;
        public bool IsInteractionAvailable => _playerInRange && !_panelOpen;
        public bool IsRoutedActive { get; set; }

        // 升级UI（uGUI+TMP）
        private GameObject _upgradeUI;
        private TextMeshProUGUI _shardsLabel;
        private RectTransform _cardsRow;
        private bool _panelOpen;

        private bool _playerInRange;
        private NpcHeadCard _headCard;

        private int[] _upgradeCount = new int[3]; // Q=0, E=1, R=2
        private readonly List<UpgradeCard> _cards = new();

        private class UpgradeCard
        {
            public int slotIndex;
            public TextMeshProUGUI skillNameLabel;
            public TextMeshProUGUI skillInfoLabel;
            public Button dmgBtn, cdBtn, chargeBtn;
            public TextMeshProUGUI dmgPrice, cdPrice, chargePrice;
            public TextMeshProUGUI chargeLabel;
        }

        public float RoomWidth => 20f;
        public float RoomDepth => 20f;

        public void Initialize(int roomIndex)
        {
            _roomIndex = roomIndex;
            BuildRoom();
            CreateUpgradeUI();

            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            InteractionRouter.Unregister(this);
            if (_roomVisuals != null) Destroy(_roomVisuals);
            if (_upgradeUI != null) Destroy(_upgradeUI);
        }

        private void OnResourceChanged(GameEvents.ResourceChanged evt)
        {
            RefreshShardsDisplay();
            RefreshAllCards();
        }

        // ==================== 房间构建 ====================

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, 20f, 20f, _roomIndex);

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "UpgradePlatform";
            platform.transform.SetParent(transform);
            platform.transform.localPosition = new Vector3(0, 0.2f, 0);
            platform.transform.localScale = new Vector3(4f, 0.2f, 4f);
            var platRend = platform.GetComponent<Renderer>();
            if (platRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.2f, 0.35f, 0.25f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.15f) * 1.5f);
                platRend.material = mat;
            }

            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "UpgradeMaster";
            npc.transform.SetParent(transform);
            npc.transform.localPosition = new Vector3(0, 1f, 2f);
            _masterTransform = npc.transform;
            var npcCol = npc.GetComponent<Collider>();
            if (npcCol != null) Destroy(npcCol);
            var npcRend = npc.GetComponent<Renderer>();
            if (npcRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.4f, 0.8f, 0.5f);
                npcRend.material = mat;
            }

            _headCard = NpcHeadCard.Attach(npc.transform, new NpcHeadCard.Config
            {
                displayName = "功法宗师",
                icon = "✦",
                roleSub = "功法修炼",
                hintText = "按 [F] 修炼功法",
                themeColor = new Color(0.4f, 1f, 0.55f),
                yOffset = 2.0f,
                showLongRangeMarker = true
            });

            var triggerGo = new GameObject("UpgradeInteractTrigger");
            triggerGo.transform.SetParent(npc.transform);
            triggerGo.transform.localPosition = Vector3.zero;
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 3f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var interactTrigger = triggerGo.AddComponent<UpgradeInteractTrigger>();
            interactTrigger.Initialize(this);

            CreateExitTrigger();
        }

        private void CreateExitTrigger()
        {
            var exitGo = new GameObject("ExitTrigger");
            exitGo.transform.SetParent(transform);
            exitGo.transform.localPosition = new Vector3(0, 0, RoomDepth / 2f - 2f);

            var sc = exitGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = exitGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var exitTrigger = exitGo.AddComponent<RoomExitTrigger>();
            exitTrigger.Initialize(() =>
            {
                GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
            });

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "ExitPillar";
            pillar.transform.SetParent(exitGo.transform);
            pillar.transform.localPosition = new Vector3(0, 1.5f, 0);
            pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            var pillarCol = pillar.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);
            var pillarRend = pillar.GetComponent<Renderer>();
            if (pillarRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 1f, 0.5f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 1f, 0.5f) * 1.5f);
                pillarRend.material = mat;
            }
        }

        // ==================== 升级UI（uGUI+TMP） ====================

        private void CreateUpgradeUI()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("UpgradeUI", 118);
            _upgradeUI = canvas.gameObject;
            UGuiKit.CreateScrim(_upgradeUI.transform, new Color(0.02f, 0.04f, 0.03f, 0.9f));

            var panel = UGuiKit.CreatePanel(_upgradeUI.transform, "Panel", new Vector2(820f, 620f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 12f, new RectOffset(24, 24, 18, 18), TextAnchor.UpperCenter);

            var header = UGuiKit.CreateRow(panel, 12f, 44f);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            var title = UGuiKit.CreateText(header, "功法修炼", 28, new Color(0.4f, 1f, 0.55f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(title, 40f); title.GetComponent<LayoutElement>().preferredWidth = 360f;
            _shardsLabel = UGuiKit.CreateText(header, "✦ 灵力碎片：0", 20, new Color(0.95f, 0.85f, 0.4f), TextAlignmentOptions.Right);
            UGuiKit.SetHeight(_shardsLabel, 40f); _shardsLabel.GetComponent<LayoutElement>().preferredWidth = 300f;
            var close = UGuiKit.CreateButton(header, "✕", ClosePanel, UGuiKit.BtnNormal, 20, new Vector2(44f, 40f));
            UGuiKit.SetHeight(close.GetComponent<RectTransform>(), 40f); close.GetComponent<LayoutElement>().preferredWidth = 44f;

            _cardsRow = UGuiKit.CreateCardRow(panel, 18f);
            UGuiKit.SetHeight(_cardsRow, 500f);

            GenerateSkillCards();
            _upgradeUI.SetActive(false);
        }

        private void GenerateSkillCards()
        {
            if (_cardsRow == null) return;
            for (int i = _cardsRow.childCount - 1; i >= 0; i--) Destroy(_cardsRow.GetChild(i).gameObject);
            _cards.Clear();
            string[] slotNames = { "Q", "E", "R" };
            var combat = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<PlayerCombat>()
                : null;
            for (int i = 0; i < 3; i++)
            {
                var skill = combat != null ? combat.GetSkillInSlot(i) : null;
                _cards.Add(BuildCard(i, slotNames[i], skill));
            }
        }

        private UpgradeCard BuildCard(int slotIndex, string slotName, SkillData skill)
        {
            var card = new UpgradeCard { slotIndex = slotIndex };

            Color slotColor = slotIndex switch
            {
                0 => new Color(0.3f, 0.7f, 1f),
                1 => new Color(1f, 0.7f, 0.3f),
                2 => new Color(0.8f, 0.3f, 0.8f),
                _ => Color.white
            };

            var content = UGuiKit.CreateCard(_cardsRow, new Vector2(250f, 480f), slotColor);

            var slot = UGuiKit.CreateText(content, $"【{slotName}】", 22, slotColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(slot, 30f);

            card.skillNameLabel = UGuiKit.CreateText(content, skill != null ? skill.skillName : "— 未装备 —", 18,
                skill != null ? UGuiKit.TextMain : new Color(0.45f, 0.45f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(card.skillNameLabel, 26f);

            card.skillInfoLabel = UGuiKit.CreateText(content, "", 13, new Color(0.7f, 0.72f, 0.78f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(card.skillInfoLabel, 44f);

            if (skill == null)
            {
                var empty = UGuiKit.CreateText(content, "装备功法后\n可在此升级", 14, new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center);
                var ele = empty.gameObject.AddComponent<LayoutElement>(); ele.flexibleHeight = 1f; ele.minHeight = 60f;
                return card;
            }

            int s = slotIndex;
            (card.dmgBtn, _, card.dmgPrice) = MakeUpgradeButton(content, "⚔ 伤害 +15%", new Color(0.5f, 0.25f, 0.2f, 0.95f), () => OnUpgradeDamage(s));
            (card.cdBtn, _, card.cdPrice) = MakeUpgradeButton(content, "⏱ CD -10%", new Color(0.2f, 0.35f, 0.5f, 0.95f), () => OnUpgradeCooldown(s));
            (card.chargeBtn, card.chargeLabel, card.chargePrice) = MakeUpgradeButton(content, "⚡ 充能 +1层", new Color(0.2f, 0.45f, 0.3f, 0.95f), () => OnUpgradeCharge(s));

            return card;
        }

        private (Button, TextMeshProUGUI, TextMeshProUGUI) MakeUpgradeButton(RectTransform parent, string label, Color bg, UnityAction onClick)
        {
            var go = new GameObject("UpBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var le = go.GetComponent<LayoutElement>(); le.preferredHeight = 48f; le.minHeight = 48f;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            var hl = UGuiKit.AddHLayout(rt, 8f, new RectOffset(12, 12, 4, 4), TextAnchor.MiddleLeft);
            var nameL = UGuiKit.CreateText(rt, label, 14, UGuiKit.TextMain, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(nameL, 40f); var nle = nameL.GetComponent<LayoutElement>(); nle.flexibleWidth = 1f; nle.preferredWidth = 140f;
            var priceL = UGuiKit.CreateText(rt, "", 13, new Color(0.95f, 0.85f, 0.4f), TextAlignmentOptions.Right);
            UGuiKit.SetHeight(priceL, 40f); priceL.GetComponent<LayoutElement>().preferredWidth = 64f;
            return (btn, nameL, priceL);
        }

        // ==================== 升级逻辑 ====================

        private int GetUpgradePrice(int slotIndex)
        {
            int basePrice = 30;
            int count = _upgradeCount[slotIndex];
            return Mathf.RoundToInt(basePrice * Mathf.Pow(1.5f, count));
        }

        private int GetChargePriceForSlot(int slotIndex)
        {
            return GetUpgradePrice(slotIndex) * 2;
        }

        private void OnUpgradeDamage(int slotIndex)
        {
            int price = GetUpgradePrice(slotIndex);
            if (!TrySpend(price)) return;

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) return;
            var skill = combat.GetSkillInSlot(slotIndex);
            if (skill == null) return;

            skill.baseDamage *= 1.15f;
            _upgradeCount[slotIndex]++;

            Debug.Log($"<color=green>✦ {skill.skillName} 伤害升级！基础伤害 → {skill.baseDamage:F1}</color>");
            RefreshAllCards();
        }

        private void OnUpgradeCooldown(int slotIndex)
        {
            int price = GetUpgradePrice(slotIndex);
            if (!TrySpend(price)) return;

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) return;
            var skill = combat.GetSkillInSlot(slotIndex);
            if (skill == null) return;

            skill.cooldown = Mathf.Max(1f, skill.cooldown * 0.9f);
            _upgradeCount[slotIndex]++;

            Debug.Log($"<color=green>✦ {skill.skillName} CD缩减！冷却时间 → {skill.cooldown:F1}s</color>");
            RefreshAllCards();
        }

        private void OnUpgradeCharge(int slotIndex)
        {
            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) return;
            var skill = combat.GetSkillInSlot(slotIndex);
            if (skill == null) return;

            int currentMax = combat.GetMaxCharges(slotIndex);
            if (currentMax >= 3)
            {
                Debug.Log("<color=yellow>充能已达上限（3层）！</color>");
                return;
            }

            int price = GetChargePriceForSlot(slotIndex);
            if (!TrySpend(price)) return;

            skill.maxCharges = Mathf.Min(3, skill.maxCharges + 1);
            switch (slotIndex)
            {
                case 0: combat.EquipSkillQ(skill); break;
                case 1: combat.EquipSkillE(skill); break;
                case 2: combat.EquipSkillR(skill); break;
            }
            _upgradeCount[slotIndex]++;

            Debug.Log($"<color=green>✦ {skill.skillName} 充能升级！最大充能 → {skill.maxCharges}层</color>");
            RefreshAllCards();
        }

        private bool TrySpend(int price)
        {
            if (PlayerResources.Instance == null || !PlayerResources.Instance.SpendShards(price))
            {
                Debug.Log("<color=red>灵力碎片不足！</color>");
                return false;
            }
            RefreshShardsDisplay();
            return true;
        }

        // ==================== 刷新UI ====================

        private void RefreshShardsDisplay()
        {
            if (_shardsLabel != null && PlayerResources.Instance != null)
                _shardsLabel.text = $"✦ 灵力碎片：{PlayerResources.Instance.SpiritShards}";
        }

        private void RefreshAllCards()
        {
            var combat = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<PlayerCombat>()
                : null;

            foreach (var card in _cards)
            {
                var skill = combat != null ? combat.GetSkillInSlot(card.slotIndex) : null;

                if (card.skillInfoLabel != null && skill != null)
                    card.skillInfoLabel.text = $"伤害：{skill.baseDamage:F0}  CD：{skill.cooldown:F1}s\n充能：{skill.maxCharges}层  已升级：{_upgradeCount[card.slotIndex]}次";

                int price = GetUpgradePrice(card.slotIndex);
                int chargePrice = GetChargePriceForSlot(card.slotIndex);
                bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(price);
                bool canAffordCharge = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(chargePrice);

                if (card.dmgBtn != null)
                {
                    card.dmgBtn.interactable = skill != null && canAfford;
                    if (card.dmgPrice != null) card.dmgPrice.text = $"✦ {price}";
                }
                if (card.cdBtn != null)
                {
                    card.cdBtn.interactable = skill != null && canAfford;
                    if (card.cdPrice != null) card.cdPrice.text = $"✦ {price}";
                }
                if (card.chargeBtn != null)
                {
                    int currentMax = combat != null ? combat.GetMaxCharges(card.slotIndex) : 1;
                    bool atMax = currentMax >= 3;
                    card.chargeBtn.interactable = skill != null && canAffordCharge && !atMax;
                    if (card.chargePrice != null) card.chargePrice.text = atMax ? "已满" : $"✦ {chargePrice}";
                    if (card.chargeLabel != null) card.chargeLabel.text = atMax ? "⚡ 充能已满" : "⚡ 充能 +1层";
                }
            }
        }

        // ==================== 开关面板 ====================

        public void OpenPanel()
        {
            _panelOpen = true;
            if (_upgradeUI != null) _upgradeUI.SetActive(true);
            RefreshShardsDisplay();
            RefreshAllCards();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void ClosePanel()
        {
            _panelOpen = false;
            if (_upgradeUI != null) _upgradeUI.SetActive(false);
        }

        private void Update()
        {
            if (_headCard != null)
            {
                bool wantHint = _playerInRange && !_panelOpen && IsRoutedActive;
                _headCard.SetHintVisible(wantHint);
            }

            if (_playerInRange && !_panelOpen && IsRoutedActive)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.fKey.wasPressedThisFrame)
                    OpenPanel();
            }

            if (_panelOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    ClosePanel();
            }
        }

        public void OnPlayerEnterRange()
        {
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        public void OnPlayerExitRange()
        {
            _playerInRange = false;
            InteractionRouter.Unregister(this);
            if (_headCard != null) _headCard.SetHintVisible(false);
        }
    }

    /// <summary>升级房间交互触发器</summary>
    public class UpgradeInteractTrigger : MonoBehaviour
    {
        private UpgradeRoom _room;
        private bool _inside;

        public void Initialize(UpgradeRoom room)
        {
            _room = room;
        }

        private void OnTriggerEnter(Collider other) => TryEnter(other);
        private void OnTriggerStay(Collider other) => TryEnter(other);

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!_inside) return;
            _inside = false;
            _room?.OnPlayerExitRange();
        }

        private void TryEnter(Collider other)
        {
            if (_inside) return;
            if (!other.CompareTag("Player")) return;
            _inside = true;
            _room?.OnPlayerEnterRange();
        }
    }
}
