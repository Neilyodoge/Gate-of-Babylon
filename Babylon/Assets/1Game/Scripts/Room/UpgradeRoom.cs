using UnityEngine;
using UnityEngine.UI;
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

        // 升级UI
        private GameObject _upgradeCanvas;
        private GameObject _upgradePanel;
        private Text _shardsText;
        private Text _titleText;
        private bool _panelOpen;

        // 交互
        private bool _playerInRange;
        private NpcHeadCard _headCard; // 统一头顶 UI（v0.3.3）

        // 技能升级追踪（运行时，每个槽位的升级次数）
        private int[] _upgradeCount = new int[3]; // Q=0, E=1, R=2

        // 升级卡片
        private List<UpgradeCard> _cards = new();

        private struct UpgradeCard
        {
            public int slotIndex;
            public GameObject cardGo;
            public Text skillNameText;
            public Text skillInfoText;
            public Button dmgUpBtn;
            public Text dmgUpText;
            public Text dmgPriceText;
            public Button cdUpBtn;
            public Text cdUpText;
            public Text cdPriceText;
            public Button chargeUpBtn;
            public Text chargeUpText;
            public Text chargePriceText;
        }

        public float RoomWidth => 20f;
        public float RoomDepth => 20f;

        public void Initialize(int roomIndex)
        {
            _roomIndex = roomIndex;
            BuildRoom();
            CreateUpgradeUI();
            if (_upgradeCanvas != null) _upgradeCanvas.SetActive(false);

            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            InteractionRouter.Unregister(this);
            if (_roomVisuals != null) Destroy(_roomVisuals);
            if (_upgradeCanvas != null) Destroy(_upgradeCanvas);
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
            _upgradeCanvas = new GameObject("UpgradeCanvas");
            var canvas = _upgradeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _upgradeCanvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _upgradeCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            _upgradeCanvas.AddComponent<GraphicRaycaster>();

            // 半透明遮罩
            var maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(_upgradeCanvas.transform, false);
            var maskRT = maskGo.AddComponent<RectTransform>();
            maskRT.anchorMin = Vector2.zero;
            maskRT.anchorMax = Vector2.one;
            maskRT.offsetMin = Vector2.zero;
            maskRT.offsetMax = Vector2.zero;
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0.5f);
            maskImg.raycastTarget = true;

            // 主面板
            _upgradePanel = new GameObject("UpgradePanel");
            _upgradePanel.transform.SetParent(_upgradeCanvas.transform, false);
            var panelRT = _upgradePanel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(780, 420);
            var panelImg = _upgradePanel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.1f, 0.08f, 0.95f);

            // 边框
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_upgradePanel.transform, false);
            var borderRT = borderGo.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.6f, 0.4f, 0.6f);
            borderImg.raycastTarget = false;
            borderGo.transform.SetAsFirstSibling();

            // 标题
            _titleText = CreateText(_upgradePanel.transform, "Title", "✦ 功法宗师 · 修炼升级 ✦",
                new Vector2(0, 0.88f), new Vector2(1, 1),
                22, new Color(0.5f, 1f, 0.6f), FontStyle.Bold);

            // 灵力碎片余额
            int shards = PlayerResources.Instance != null ? PlayerResources.Instance.SpiritShards : 0;
            _shardsText = CreateText(_upgradePanel.transform, "Shards", $"✦ 灵力碎片：{shards}",
                new Vector2(0, 0.80f), new Vector2(1, 0.88f),
                15, new Color(0.5f, 0.8f, 1f), FontStyle.Normal);

            // 分隔线
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(_upgradePanel.transform, false);
            var lineRT = lineGo.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0.05f, 0.79f);
            lineRT.anchorMax = new Vector2(0.95f, 0.795f);
            lineRT.offsetMin = Vector2.zero;
            lineRT.offsetMax = Vector2.zero;
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.color = new Color(0.3f, 0.5f, 0.4f, 0.4f);
            lineImg.raycastTarget = false;

            // 生成3个技能升级卡片（Q/E/R）
            GenerateSkillCards();

            // 关闭按钮
            var closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(_upgradePanel.transform, false);
            var closeBtnRT = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0.5f, 0);
            closeBtnRT.anchorMax = new Vector2(0.5f, 0);
            closeBtnRT.pivot = new Vector2(0.5f, 0);
            closeBtnRT.anchoredPosition = new Vector2(0, 8);
            closeBtnRT.sizeDelta = new Vector2(140, 34);
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.3f, 0.2f, 0.15f, 0.9f);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(ClosePanel);
            CreateText(closeBtnGo.transform, "Text", "离开修炼",
                Vector2.zero, Vector2.one, 15, new Color(1f, 0.9f, 0.7f), FontStyle.Bold);

            // 提示
            CreateText(_upgradePanel.transform, "Hint", "选择升级方向 · 每次升级费用递增 | 按 Esc 关闭",
                new Vector2(0, 0), new Vector2(1, 0.05f),
                11, new Color(0.5f, 0.5f, 0.5f, 0.7f), FontStyle.Normal);
        }

        private void GenerateSkillCards()
        {
            string[] slotNames = { "Q", "E", "R" };
            float cardWidth = 230f;
            float spacing = 15f;
            float totalWidth = 3 * cardWidth + 2 * spacing;
            float startX = -totalWidth / 2f + cardWidth / 2f;

            var combat = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<PlayerCombat>()
                : null;

            for (int i = 0; i < 3; i++)
            {
                var skill = combat != null ? combat.GetSkillInSlot(i) : null;
                float xPos = startX + i * (cardWidth + spacing);
                var card = CreateSkillUpgradeCard(i, slotNames[i], skill, xPos);
                _cards.Add(card);
            }
        }

        private UpgradeCard CreateSkillUpgradeCard(int slotIndex, string slotName, SkillData skill, float xPos)
        {
            var card = new UpgradeCard { slotIndex = slotIndex };

            // 卡片容器
            var cardGo = new GameObject($"UpgradeCard_{slotName}");
            cardGo.transform.SetParent(_upgradePanel.transform, false);
            var cardRT = cardGo.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(xPos, -15f);
            cardRT.sizeDelta = new Vector2(cardRT.sizeDelta.x, 260);
            cardRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 230);
            card.cardGo = cardGo;

            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = skill != null
                ? new Color(0.1f, 0.15f, 0.12f, 0.9f)
                : new Color(0.1f, 0.1f, 0.1f, 0.5f);

            // 槽位标签
            Color slotColor = slotIndex switch
            {
                0 => new Color(0.3f, 0.7f, 1f),   // Q 蓝
                1 => new Color(1f, 0.7f, 0.3f),    // E 橙
                2 => new Color(0.8f, 0.3f, 0.8f),  // R 紫
                _ => Color.white
            };
            CreateText(cardGo.transform, "SlotLabel", $"【{slotName}】",
                new Vector2(0, 0.88f), new Vector2(1, 1),
                18, slotColor, FontStyle.Bold);

            // 技能名称
            string skillName = skill != null ? skill.skillName : "— 未装备 —";
            card.skillNameText = CreateText(cardGo.transform, "SkillName", skillName,
                new Vector2(0, 0.76f), new Vector2(1, 0.88f),
                15, skill != null ? new Color(0.9f, 0.95f, 0.9f) : new Color(0.4f, 0.4f, 0.4f),
                FontStyle.Bold);

            // 技能信息
            string info = "无功法";
            if (skill != null)
            {
                info = $"伤害：{skill.baseDamage:F0}  CD：{skill.cooldown:F1}s\n充能：{skill.maxCharges}层  已升级：{_upgradeCount[slotIndex]}次";
            }
            card.skillInfoText = CreateText(cardGo.transform, "SkillInfo", info,
                new Vector2(0, 0.62f), new Vector2(1, 0.76f),
                12, new Color(0.7f, 0.85f, 0.7f, 0.9f), FontStyle.Normal);

            if (skill == null)
            {
                // 无技能时显示提示
                CreateText(cardGo.transform, "Empty", "装备功法后\n可在此升级",
                    new Vector2(0, 0.2f), new Vector2(1, 0.6f),
                    13, new Color(0.4f, 0.4f, 0.4f, 0.6f), FontStyle.Normal);
                return card;
            }

            // 升级选项1：伤害+15%
            int dmgPrice = GetUpgradePrice(slotIndex);
            var dmgBtnGo = CreateUpgradeButton(cardGo.transform, "DmgUp",
                $"⚔ 伤害 +15%", $"✦ {dmgPrice}",
                new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.58f),
                new Color(0.5f, 0.25f, 0.2f, 0.9f));
            card.dmgUpBtn = dmgBtnGo.GetComponent<Button>();
            card.dmgUpText = dmgBtnGo.transform.Find("Label")?.GetComponent<Text>();
            card.dmgPriceText = dmgBtnGo.transform.Find("Price")?.GetComponent<Text>();
            int dmgSlot = slotIndex;
            card.dmgUpBtn.onClick.AddListener(() => OnUpgradeDamage(dmgSlot));

            // 升级选项2：CD-10%
            int cdPrice = GetUpgradePrice(slotIndex);
            var cdBtnGo = CreateUpgradeButton(cardGo.transform, "CdUp",
                $"⏱ CD -10%", $"✦ {cdPrice}",
                new Vector2(0.05f, 0.23f), new Vector2(0.95f, 0.39f),
                new Color(0.2f, 0.35f, 0.5f, 0.9f));
            card.cdUpBtn = cdBtnGo.GetComponent<Button>();
            card.cdUpText = cdBtnGo.transform.Find("Label")?.GetComponent<Text>();
            card.cdPriceText = cdBtnGo.transform.Find("Price")?.GetComponent<Text>();
            int cdSlot = slotIndex;
            card.cdUpBtn.onClick.AddListener(() => OnUpgradeCooldown(cdSlot));

            // 升级选项3：充能+1层
            int chargePrice = GetChargePriceForSlot(slotIndex);
            var chargeBtnGo = CreateUpgradeButton(cardGo.transform, "ChargeUp",
                $"⚡ 充能 +1层", $"✦ {chargePrice}",
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.20f),
                new Color(0.2f, 0.45f, 0.3f, 0.9f));
            card.chargeUpBtn = chargeBtnGo.GetComponent<Button>();
            card.chargeUpText = chargeBtnGo.transform.Find("Label")?.GetComponent<Text>();
            card.chargePriceText = chargeBtnGo.transform.Find("Price")?.GetComponent<Text>();
            int chargeSlot = slotIndex;
            card.chargeUpBtn.onClick.AddListener(() => OnUpgradeCharge(chargeSlot));

            return card;
        }

        private GameObject CreateUpgradeButton(Transform parent, string name, string label, string price,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = anchorMin;
            btnRT.anchorMax = anchorMax;
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = bgColor;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.7f;
            colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            btn.colors = colors;

            // 标签（左侧）
            var labelText = CreateText(btnGo.transform, "Label", label,
                new Vector2(0, 0), new Vector2(0.6f, 1),
                13, Color.white, FontStyle.Bold);
            labelText.alignment = TextAnchor.MiddleLeft;

            // 价格（右侧）
            var priceText = CreateText(btnGo.transform, "Price", price,
                new Vector2(0.6f, 0), new Vector2(1, 1),
                13, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
            priceText.alignment = TextAnchor.MiddleRight;

            return btnGo;
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
            if (_shardsText != null && PlayerResources.Instance != null)
                _shardsText.text = $"✦ 灵力碎片：{PlayerResources.Instance.SpiritShards}";
        }

        private void RefreshAllCards()
        {
            var combat = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<PlayerCombat>()
                : null;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var skill = combat != null ? combat.GetSkillInSlot(card.slotIndex) : null;

                // 更新技能信息
                if (card.skillInfoText != null && skill != null)
                {
                    card.skillInfoText.text = $"伤害：{skill.baseDamage:F0}  CD：{skill.cooldown:F1}s\n充能：{skill.maxCharges}层  已升级：{_upgradeCount[card.slotIndex]}次";
                }

                // 更新按钮状态
                int price = GetUpgradePrice(card.slotIndex);
                int chargePrice = GetChargePriceForSlot(card.slotIndex);
                bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(price);
                bool canAffordCharge = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(chargePrice);

                // 伤害按钮
                if (card.dmgUpBtn != null)
                {
                    card.dmgUpBtn.interactable = skill != null && canAfford;
                    if (card.dmgPriceText != null)
                        card.dmgPriceText.text = $"✦ {price}";
                }

                // CD按钮
                if (card.cdUpBtn != null)
                {
                    card.cdUpBtn.interactable = skill != null && canAfford;
                    if (card.cdPriceText != null)
                        card.cdPriceText.text = $"✦ {price}";
                }

                // 充能按钮
                if (card.chargeUpBtn != null)
                {
                    int currentMax = combat != null ? combat.GetMaxCharges(card.slotIndex) : 1;
                    bool atMax = currentMax >= 3;
                    card.chargeUpBtn.interactable = skill != null && canAffordCharge && !atMax;
                    if (card.chargePriceText != null)
                        card.chargePriceText.text = atMax ? "已满" : $"✦ {chargePrice}";
                    if (card.chargeUpText != null)
                        card.chargeUpText.text = atMax ? "⚡ 充能已满" : "⚡ 充能 +1层";
                }
            }
        }

        // ==================== 开关面板 ====================

        public void OpenPanel()
        {
            _panelOpen = true;
            if (_upgradeCanvas != null) _upgradeCanvas.SetActive(true);
            RefreshShardsDisplay();
            RefreshAllCards();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ClosePanel()
        {
            _panelOpen = false;
            if (_upgradeCanvas != null) _upgradeCanvas.SetActive(false);
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

        // ==================== 工具方法 ====================

        private Text CreateText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(6, 0);
            rt.offsetMax = new Vector2(-6, 0);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.7f);
            outline.effectDistance = new Vector2(1, -1);

            return text;
        }
    }

    /// <summary>升级房间交互触发器</summary>
    public class UpgradeInteractTrigger : MonoBehaviour
    {
        private UpgradeRoom _room;

        public void Initialize(UpgradeRoom room)
        {
            _room = room;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _room?.OnPlayerEnterRange();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                _room?.OnPlayerExitRange();
        }
    }
}
