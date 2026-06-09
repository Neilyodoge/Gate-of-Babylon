using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 技能升级房间 —— 花费灵力碎片升级已装备的功法
    /// 进入房间靠近修炼台按F打开升级面板
    /// 每个技能可升级：伤害+15%、CD-10%、充能+1层（三选一）
    /// </summary>
    public class UpgradeRoom : MonoBehaviour, IInteractable
    {
        private int _roomIndex;
        private GameObject _roomVisuals;
        private Transform _masterTransform; // 升级宗师 NPC，用作距离锚点

        // ===== IInteractable：参与统一 F 交互路由 =====
        public Vector3 InteractionWorldPos =>
            _masterTransform != null ? _masterTransform.position : transform.position;
        public int InteractionPriority => 35;
        public bool IsInteractionAvailable => _playerInRange && !_panelOpen;
        public bool IsRoutedActive { get; set; }

        // 升级UI（UITK）
        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _shardsLabel;
        private VisualElement _cardsRow;
        private bool _panelOpen;

        // 交互
        private bool _playerInRange;
        private NpcHeadCard _headCard; // 统一头顶 UI（v0.3.3）

        // 技能升级追踪（运行时，每个槽位的升级次数）
        private int[] _upgradeCount = new int[3]; // Q=0, E=1, R=2

        // 升级卡片
        private readonly List<UpgradeCard> _cards = new();

        private class UpgradeCard
        {
            public int slotIndex;
            public Label skillNameLabel;
            public Label skillInfoLabel;
            public Button dmgBtn, cdBtn, chargeBtn;
            public Label dmgPrice, cdPrice, chargePrice;
            public Label chargeLabel;
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
            if (_doc != null) Destroy(_doc.gameObject);
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

            // 修炼台（中央平台）
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

            // 修炼台NPC（道人）
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

            // 统一 NPC 头顶卡片（绿色主题 · 功法修炼）
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

            // 交互触发器
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

            // 出口触发器
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

            // 出口视觉标记
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


        // ==================== 升级UI ====================

        private void CreateUpgradeUI()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/UpgradeRoom");

            var go = new GameObject("UpgradeUITK");
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _shardsLabel = root.Q<Label>("shards");
            _cardsRow = root.Q<VisualElement>("cards");
            var close = root.Q<Button>("close");
            if (close != null) close.clicked += ClosePanel;

            GenerateSkillCards();
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void GenerateSkillCards()
        {
            if (_cardsRow == null) return;
            _cardsRow.Clear();
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
            var cardEl = new VisualElement();
            cardEl.AddToClassList("up-card");

            Color slotColor = slotIndex switch
            {
                0 => new Color(0.3f, 0.7f, 1f),
                1 => new Color(1f, 0.7f, 0.3f),
                2 => new Color(0.8f, 0.3f, 0.8f),
                _ => Color.white
            };
            var slot = new Label($"【{slotName}】");
            slot.AddToClassList("up-slot");
            slot.style.color = slotColor;
            cardEl.Add(slot);

            card.skillNameLabel = new Label(skill != null ? skill.skillName : "— 未装备 —");
            card.skillNameLabel.AddToClassList("up-skill");
            if (skill == null) card.skillNameLabel.style.color = new Color(0.45f, 0.45f, 0.45f);
            cardEl.Add(card.skillNameLabel);

            card.skillInfoLabel = new Label();
            card.skillInfoLabel.AddToClassList("up-info");
            cardEl.Add(card.skillInfoLabel);

            if (skill == null)
            {
                var empty = new Label("装备功法后\n可在此升级");
                empty.AddToClassList("up-empty");
                cardEl.Add(empty);
                _cardsRow.Add(cardEl);
                return card;
            }

            int s = slotIndex;
            (card.dmgBtn, _, card.dmgPrice) = MakeUpgradeButton(cardEl, "⚔ 伤害 +15%", new Color(0.5f, 0.25f, 0.2f, 0.95f), () => OnUpgradeDamage(s));
            (card.cdBtn, _, card.cdPrice) = MakeUpgradeButton(cardEl, "⏱ CD -10%", new Color(0.2f, 0.35f, 0.5f, 0.95f), () => OnUpgradeCooldown(s));
            (card.chargeBtn, card.chargeLabel, card.chargePrice) = MakeUpgradeButton(cardEl, "⚡ 充能 +1层", new Color(0.2f, 0.45f, 0.3f, 0.95f), () => OnUpgradeCharge(s));

            _cardsRow.Add(cardEl);
            return card;
        }

        private (Button, Label, Label) MakeUpgradeButton(VisualElement parent, string label, Color bg, System.Action onClick)
        {
            var btn = new Button(onClick) { text = "" };
            btn.AddToClassList("up-btn");
            btn.style.backgroundColor = bg;
            var nameL = new Label(label);
            nameL.AddToClassList("up-btn__label");
            btn.Add(nameL);
            var priceL = new Label();
            priceL.AddToClassList("up-btn__price");
            btn.Add(priceL);
            parent.Add(btn);
            return (btn, nameL, priceL);
        }

        // ==================== 升级逻辑 ====================

        /// <summary>获取升级价格（随升级次数递增）</summary>
        private int GetUpgradePrice(int slotIndex)
        {
            int basePrice = 30;
            int count = _upgradeCount[slotIndex];
            // 每次升级价格增加50%
            return Mathf.RoundToInt(basePrice * Mathf.Pow(1.5f, count));
        }

        /// <summary>获取充能升级价格（更贵）</summary>
        private int GetChargePriceForSlot(int slotIndex)
        {
            return GetUpgradePrice(slotIndex) * 2; // 充能升级是普通升级的2倍价格
        }

        private void OnUpgradeDamage(int slotIndex)
        {
            int price = GetUpgradePrice(slotIndex);
            if (!TrySpend(price)) return;

            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) return;
            var skill = combat.GetSkillInSlot(slotIndex);
            if (skill == null) return;

            // 伤害+15%
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

            // CD-10%（最低1秒）
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

            // 检查充能上限
            int currentMax = combat.GetMaxCharges(slotIndex);
            if (currentMax >= 3)
            {
                Debug.Log("<color=yellow>充能已达上限（3层）！</color>");
                return;
            }

            int price = GetChargePriceForSlot(slotIndex);
            if (!TrySpend(price)) return;

            // 充能+1层
            skill.maxCharges = Mathf.Min(3, skill.maxCharges + 1);
            // 重新初始化充能
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
                    card.dmgBtn.SetEnabled(skill != null && canAfford);
                    if (card.dmgPrice != null) card.dmgPrice.text = $"✦ {price}";
                }
                if (card.cdBtn != null)
                {
                    card.cdBtn.SetEnabled(skill != null && canAfford);
                    if (card.cdPrice != null) card.cdPrice.text = $"✦ {price}";
                }
                if (card.chargeBtn != null)
                {
                    int currentMax = combat != null ? combat.GetMaxCharges(card.slotIndex) : 1;
                    bool atMax = currentMax >= 3;
                    card.chargeBtn.SetEnabled(skill != null && canAffordCharge && !atMax);
                    if (card.chargePrice != null) card.chargePrice.text = atMax ? "已满" : $"✦ {chargePrice}";
                    if (card.chargeLabel != null) card.chargeLabel.text = atMax ? "⚡ 充能已满" : "⚡ 充能 +1层";
                }
            }
        }

        // ==================== 开关面板 ====================

        public void OpenPanel()
        {
            _panelOpen = true;
            if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
            RefreshShardsDisplay();
            RefreshAllCards();
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void ClosePanel()
        {
            _panelOpen = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            // 同步交互提示：被路由器选中时才显示「按 F」提示
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

        // 兜底：玩家被 TeleportPlayer 出生在房间中心，NPC 在 (0,1,2) r=3 内，
        // OnTriggerEnter 不会触发；用 OnTriggerStay 保证 F 交互能注册。
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
