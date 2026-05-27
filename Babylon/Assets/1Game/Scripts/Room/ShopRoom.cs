using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 商店房间 —— 完整的商店UI系统
    /// 进入房间后自动弹出商店面板，展示3个随机商品
    /// 用灵力碎片购买灵物/功法
    /// </summary>
    public class ShopRoom : MonoBehaviour, IInteractable
    {
        private ItemData[] _shopItems;
        private SkillData[] _shopSkills;
        private int _roomIndex;
        private GameObject _roomVisuals;
        private Transform _shopkeeperTransform;

        // ===== IInteractable：参与统一 F 交互路由 =====
        // 商店优先级最高 —— 玩家若同时在商店和拾取物范围内，先满足商店交互
        public Vector3 InteractionWorldPos =>
            _shopkeeperTransform != null ? _shopkeeperTransform.position : transform.position;
        public int InteractionPriority => 40;
        public bool IsInteractionAvailable => _playerInRange && !_shopOpen;
        public bool IsRoutedActive { get; set; }

        // 商店UI
        private GameObject _shopCanvas;
        private GameObject _shopPanel;
        private Text _shardsText;
        private List<ShopSlot> _shopSlots = new();
        private GameObject _tooltipPanel;
        private Text _tooltipTitle;
        private Text _tooltipDesc;
        private Text _tooltipEffect;
        private Text _tooltipPrice;
        private RectTransform _tooltipRT;
        private bool _shopOpen;

        // 商品数据
        private struct ShopSlot
        {
            public ItemData item;
            public SkillData skill; // 功法商品
            public int price;
            public bool sold;
            public GameObject cardGo;
            public Image cardBg;
            public Text nameText;
            public Text priceText;
            public Button buyButton;
            public Text buttonText;
            public Image rarityBar;
        }

        public float RoomWidth => 20f;
        public float RoomDepth => 20f;

        // 交互状态
        private bool _playerInRange;
        private NpcHeadCard _headCard; // 统一头顶 UI（v0.3.3）

        public void Initialize(int roomIndex, ItemData[] itemPool, SkillData[] skillPool = null)
        {
            _roomIndex = roomIndex;
            _shopItems = itemPool;
            _shopSkills = skillPool;
            BuildRoom();
            CreateShopUI();
            // 商店初始隐藏，靠近商人按F才打开
            if (_shopCanvas != null) _shopCanvas.SetActive(false);

            // 监听资源变化刷新余额显示
            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            InteractionRouter.Unregister(this);
            if (_roomVisuals != null) Destroy(_roomVisuals);
            if (_shopCanvas != null) Destroy(_shopCanvas);
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

            // 商店装饰：中央柜台
            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "ShopCounter";
            counter.transform.SetParent(transform);
            counter.transform.localPosition = new Vector3(0, 0.5f, 2f);
            counter.transform.localScale = new Vector3(6f, 1f, 1.5f);
            var rend = counter.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.35f, 0.25f, 0.15f);
                rend.material = mat;
            }

            // 商人NPC
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Shopkeeper";
            npc.transform.SetParent(transform);
            npc.transform.localPosition = new Vector3(0, 1f, 3.5f);
            _shopkeeperTransform = npc.transform; // 给 InteractionRouter 用作距离锚点
            var npcCol = npc.GetComponent<Collider>();
            if (npcCol != null) Destroy(npcCol);
            var npcRend = npc.GetComponent<Renderer>();
            if (npcRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.9f, 0.8f, 0.5f);
                npcRend.material = mat;
            }

            // 统一 NPC 头顶卡片（金色主题 · 商人）
            _headCard = NpcHeadCard.Attach(npc.transform, new NpcHeadCard.Config
            {
                displayName = "散修商人",
                icon = "✦",
                roleSub = "灵物交易",
                hintText = "按 [F] 交易",
                themeColor = new Color(1f, 0.82f, 0.35f),
                yOffset = 2.0f,
                showLongRangeMarker = true
            });

            // 商人交互触发器（靠近按F打开商店）
            var shopTriggerGo = new GameObject("ShopInteractTrigger");
            shopTriggerGo.transform.SetParent(npc.transform);
            shopTriggerGo.transform.localPosition = Vector3.zero;
            var shopSc = shopTriggerGo.AddComponent<SphereCollider>();
            shopSc.isTrigger = true;
            shopSc.radius = 3f;
            var shopRb = shopTriggerGo.AddComponent<Rigidbody>();
            shopRb.isKinematic = true;
            shopRb.useGravity = false;

            var interactTrigger = shopTriggerGo.AddComponent<ShopInteractTrigger>();
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
                mat.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.5f);
                pillarRend.material = mat;
            }
        }

        // ==================== 商店UI ====================

        private void CreateShopUI()
        {
            // 屏幕空间Canvas
            _shopCanvas = new GameObject("ShopCanvas");
            var canvas = _shopCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _shopCanvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _shopCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            _shopCanvas.AddComponent<GraphicRaycaster>();

            // 半透明遮罩
            var maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(_shopCanvas.transform, false);
            var maskRT = maskGo.AddComponent<RectTransform>();
            maskRT.anchorMin = Vector2.zero;
            maskRT.anchorMax = Vector2.one;
            maskRT.offsetMin = Vector2.zero;
            maskRT.offsetMax = Vector2.zero;
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0.5f);
            maskImg.raycastTarget = true;

            // 主面板
            _shopPanel = new GameObject("ShopPanel");
            _shopPanel.transform.SetParent(_shopCanvas.transform, false);
            var panelRT = _shopPanel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(820, 480);
            var panelImg = _shopPanel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.06f, 0.12f, 0.95f);

            // 面板边框
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_shopPanel.transform, false);
            var borderRT = borderGo.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.6f, 0.5f, 0.3f, 0.6f);
            borderImg.raycastTarget = false;
            borderGo.transform.SetAsFirstSibling();

            // 标题
            CreateText(_shopPanel.transform, "Title", "✦ 散修商人 ✦",
                new Vector2(0, 0.85f), new Vector2(1, 1),
                24, new Color(1f, 0.9f, 0.5f), FontStyle.Bold);

            // 灵力碎片余额
            int shards = PlayerResources.Instance != null ? PlayerResources.Instance.SpiritShards : 0;
            _shardsText = CreateText(_shopPanel.transform, "Shards", $"✦ 灵力碎片：{shards}",
                new Vector2(0, 0.78f), new Vector2(1, 0.85f),
                16, new Color(0.5f, 0.8f, 1f), FontStyle.Normal);

            // 分隔线
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(_shopPanel.transform, false);
            var lineRT = lineGo.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(0.05f, 0.77f);
            lineRT.anchorMax = new Vector2(0.95f, 0.775f);
            lineRT.offsetMin = Vector2.zero;
            lineRT.offsetMax = Vector2.zero;
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.color = new Color(0.5f, 0.4f, 0.3f, 0.4f);
            lineImg.raycastTarget = false;

            // 生成3个商品卡片
            GenerateShopItems();

            // 关闭按钮
            CreateCloseButton();

            // 提示文字
            CreateText(_shopPanel.transform, "Hint", "点击「购买」消耗灵力碎片 | 按 Esc 关闭商店",
                new Vector2(0, 0), new Vector2(1, 0.06f),
                12, new Color(0.5f, 0.5f, 0.5f, 0.7f), FontStyle.Normal);

            // 创建悬停提示面板
            CreateTooltip();
        }

        private void GenerateShopItems()
        {
            // 商品总数：5个（灵物 + 功法混合）
            int totalSlots = 5;
            float cardWidth = 130f;
            float spacing = 12f;
            float totalWidth = totalSlots * cardWidth + (totalSlots - 1) * spacing;
            float startX = -totalWidth / 2f + cardWidth / 2f;

            int slotIdx = 0;

            // 前3个槽位：灵物
            int itemSlots = Mathf.Min(3, totalSlots);
            if (_shopItems != null && _shopItems.Length > 0)
            {
                for (int i = 0; i < itemSlots && slotIdx < totalSlots; i++)
                {
                    ItemData item;
                    var config = GameConfig.Instance;
                    if (config != null)
                    {
                        ItemRarity rarity = config.RollRarity();
                        var candidates = new System.Collections.Generic.List<ItemData>();
                        foreach (var d in _shopItems)
                            if (d != null && d.rarity == rarity) candidates.Add(d);
                        item = candidates.Count > 0
                            ? candidates[Random.Range(0, candidates.Count)]
                            : _shopItems[Random.Range(0, _shopItems.Length)];
                    }
                    else
                    {
                        item = _shopItems[Random.Range(0, _shopItems.Length)];
                    }

                    if (item == null) continue;
                    int price = CalculatePrice(item);
                    float xPos = startX + slotIdx * (cardWidth + spacing);
                    var slot = CreateItemCard(item, price, xPos, slotIdx);
                    _shopSlots.Add(slot);
                    slotIdx++;
                }
            }

            // 后2个槽位：功法
            if (_shopSkills != null && _shopSkills.Length > 0)
            {
                int skillSlots = totalSlots - slotIdx;
                for (int i = 0; i < skillSlots && slotIdx < totalSlots; i++)
                {
                    var skill = _shopSkills[Random.Range(0, _shopSkills.Length)];
                    if (skill == null) continue;

                    int price = CalculateSkillPrice(skill);
                    float xPos = startX + slotIdx * (cardWidth + spacing);
                    var slot = CreateSkillCard(skill, price, xPos, slotIdx);
                    _shopSlots.Add(slot);
                    slotIdx++;
                }
            }

            // 如果功法池为空，剩余槽位用灵物填充
            if (_shopItems != null && _shopItems.Length > 0)
            {
                while (slotIdx < totalSlots)
                {
                    var item = _shopItems[Random.Range(0, _shopItems.Length)];
                    if (item == null) continue;
                    int price = CalculatePrice(item);
                    float xPos = startX + slotIdx * (cardWidth + spacing);
                    var slot = CreateItemCard(item, price, xPos, slotIdx);
                    _shopSlots.Add(slot);
                    slotIdx++;
                }
            }
        }

        private int CalculatePrice(ItemData item)
        {
            if (item == null) return 0;
            // 价格 = 分解价值 × 3.5（买比卖贵）
            // 2026-04 调整：原 2.5x。配合杀敌碎片产出减半，把单价拉高让商品更稀缺
            // 凡 18 / 灵 53 / 玄 140 / 地 350 / 天 875
            int basePrice = PlayerResources.GetDecomposeShards(item.rarity);
            return Mathf.RoundToInt(basePrice * 3.5f);
        }

        private int CalculateSkillPrice(SkillData skill)
        {
            // 功法价格更贵 4.5x（原 3.5x）：凡 23 / 灵 68 / 玄 180 / 地 450 / 天 1125
            int basePrice = PlayerResources.GetDecomposeShards(skill.rarity);
            return Mathf.RoundToInt(basePrice * 4.5f);
        }

        private ShopSlot CreateItemCard(ItemData item, int price, float xPos, int index)
        {
            var slot = new ShopSlot { item = item, price = price, sold = false };

            // 卡片容器
            var cardGo = new GameObject($"Card_{index}");
            cardGo.transform.SetParent(_shopPanel.transform, false);
            var cardRT = cardGo.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(xPos, -15f);
            cardRT.sizeDelta = new Vector2(190, 280);
            slot.cardGo = cardGo;

            // 卡片背景
            slot.cardBg = cardGo.AddComponent<Image>();
            slot.cardBg.color = new Color(0.12f, 0.1f, 0.18f, 0.9f);

            // 品阶色条（顶部）
            var barGo = new GameObject("RarityBar");
            barGo.transform.SetParent(cardGo.transform, false);
            var barRT = barGo.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0.95f);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.offsetMin = new Vector2(2, 0);
            barRT.offsetMax = new Vector2(-2, -2);
            slot.rarityBar = barGo.AddComponent<Image>();
            slot.rarityBar.color = item.GetRarityColor();
            slot.rarityBar.raycastTarget = false;

            // 灵物图标区域（用品阶颜色的方块代替）
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.15f, 0.6f);
            iconRT.anchorMax = new Vector2(0.85f, 0.9f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = item.GetRarityColor() * 0.6f;
            iconImg.raycastTarget = false;

            // 图标中心文字（品阶）
            var iconLabel = CreateText(iconGo.transform, "Label", GetRaritySymbol(item.rarity),
                Vector2.zero, Vector2.one, 28, item.GetRarityColor(), FontStyle.Bold);
            iconLabel.raycastTarget = false;

            // 名称
            slot.nameText = CreateText(cardGo.transform, "Name", item.itemName,
                new Vector2(0, 0.45f), new Vector2(1, 0.6f),
                16, item.GetRarityColor(), FontStyle.Bold);
            slot.nameText.raycastTarget = false;

            // 品阶标签
            string rarityName = item.rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };
            var rarityText = CreateText(cardGo.transform, "Rarity", rarityName,
                new Vector2(0, 0.38f), new Vector2(1, 0.45f),
                12, new Color(0.7f, 0.7f, 0.7f, 0.8f), FontStyle.Normal);
            rarityText.raycastTarget = false;

            // 简要效果
            string effectBrief = GetBriefEffect(item);
            var effectText = CreateText(cardGo.transform, "Effect", effectBrief,
                new Vector2(0, 0.22f), new Vector2(1, 0.38f),
                11, new Color(0.6f, 0.9f, 0.6f, 0.8f), FontStyle.Normal);
            effectText.raycastTarget = false;

            // 价格
            slot.priceText = CreateText(cardGo.transform, "Price", $"✦ {price}",
                new Vector2(0, 0.12f), new Vector2(1, 0.22f),
                15, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
            slot.priceText.raycastTarget = false;

            // 购买按钮
            var btnGo = new GameObject("BuyBtn");
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.1f, 0.02f);
            btnRT.anchorMax = new Vector2(0.9f, 0.12f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(price);
            btnImg.color = canAfford ? new Color(0.2f, 0.5f, 0.3f, 0.9f) : new Color(0.3f, 0.2f, 0.2f, 0.6f);
            slot.buyButton = btnGo.AddComponent<Button>();
            slot.buyButton.targetGraphic = btnImg;

            slot.buttonText = CreateText(btnGo.transform, "BtnText", canAfford ? "购 买" : "碎片不足",
                Vector2.zero, Vector2.one, 14, Color.white, FontStyle.Bold);
            slot.buttonText.raycastTarget = false;

            // 购买事件
            int slotIndex = index;
            slot.buyButton.onClick.AddListener(() => OnBuyClicked(slotIndex));

            // 悬停事件（通过EventTrigger）
            var trigger = cardGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            int idx = index;
            enterEntry.callback.AddListener((_) => ShowItemTooltip(idx));
            trigger.triggers.Add(enterEntry);

            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener((_) => HideItemTooltip());
            trigger.triggers.Add(exitEntry);

            return slot;
        }

        /// <summary>创建功法商品卡片</summary>
        private ShopSlot CreateSkillCard(SkillData skill, int price, float xPos, int index)
        {
            var slot = new ShopSlot { skill = skill, price = price, sold = false };

            // 卡片容器
            var cardGo = new GameObject($"SkillCard_{index}");
            cardGo.transform.SetParent(_shopPanel.transform, false);
            var cardRT = cardGo.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = new Vector2(xPos, -15f);
            cardRT.sizeDelta = new Vector2(130, 280);
            slot.cardGo = cardGo;

            // 卡片背景（功法用深蓝色调）
            slot.cardBg = cardGo.AddComponent<Image>();
            slot.cardBg.color = new Color(0.08f, 0.1f, 0.2f, 0.9f);

            // 品阶色条（顶部）
            Color rarityColor = skill.rarity switch
            {
                ItemRarity.Fan => Color.white,
                ItemRarity.Ling => Color.green,
                ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                _ => Color.white
            };
            var barGo = new GameObject("RarityBar");
            barGo.transform.SetParent(cardGo.transform, false);
            var barRT = barGo.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0.95f);
            barRT.anchorMax = new Vector2(1, 1);
            barRT.offsetMin = new Vector2(2, 0);
            barRT.offsetMax = new Vector2(-2, -2);
            slot.rarityBar = barGo.AddComponent<Image>();
            slot.rarityBar.color = rarityColor;
            slot.rarityBar.raycastTarget = false;

            // 功法图标区域（用📜书卷图标）
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.15f, 0.6f);
            iconRT.anchorMax = new Vector2(0.85f, 0.9f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.15f, 0.2f, 0.35f, 0.8f);
            iconImg.raycastTarget = false;

            // 图标中心文字（功法类型符号）
            string typeSymbol = skill.skillType switch
            {
                SkillType.AreaDamage => "💥",
                SkillType.Projectile => "⚔",
                SkillType.Dash => "🌀",
                SkillType.Buff => "🛡",
                SkillType.Heal => "💚",
                SkillType.Summon => "👻",
                _ => "📜"
            };
            var iconLabel = CreateText(iconGo.transform, "Label", typeSymbol,
                Vector2.zero, Vector2.one, 24, rarityColor, FontStyle.Bold);
            iconLabel.raycastTarget = false;

            // 名称
            slot.nameText = CreateText(cardGo.transform, "Name", skill.skillName,
                new Vector2(0, 0.45f), new Vector2(1, 0.6f),
                14, rarityColor, FontStyle.Bold);
            slot.nameText.raycastTarget = false;

            // 类型标签
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
            var typeText = CreateText(cardGo.transform, "Type", $"[功法·{typeStr}]",
                new Vector2(0, 0.38f), new Vector2(1, 0.45f),
                10, new Color(0.5f, 0.7f, 1f, 0.8f), FontStyle.Normal);
            typeText.raycastTarget = false;

            // 简要效果
            string effectBrief = $"伤害 {skill.baseDamage}\nCD {skill.cooldown}s";
            if (skill.skillType == SkillType.Heal)
                effectBrief = $"治疗 {skill.healAmount}\nCD {skill.cooldown}s";
            var effectText = CreateText(cardGo.transform, "Effect", effectBrief,
                new Vector2(0, 0.22f), new Vector2(1, 0.38f),
                10, new Color(0.6f, 0.9f, 0.6f, 0.8f), FontStyle.Normal);
            effectText.raycastTarget = false;

            // 价格
            slot.priceText = CreateText(cardGo.transform, "Price", $"✦ {price}",
                new Vector2(0, 0.12f), new Vector2(1, 0.22f),
                13, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
            slot.priceText.raycastTarget = false;

            // 购买按钮
            var btnGo = new GameObject("BuyBtn");
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.1f, 0.02f);
            btnRT.anchorMax = new Vector2(0.9f, 0.12f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(price);
            btnImg.color = canAfford ? new Color(0.2f, 0.3f, 0.6f, 0.9f) : new Color(0.2f, 0.2f, 0.3f, 0.6f);
            slot.buyButton = btnGo.AddComponent<Button>();
            slot.buyButton.targetGraphic = btnImg;

            slot.buttonText = CreateText(btnGo.transform, "BtnText", canAfford ? "购 买" : "碎片不足",
                Vector2.zero, Vector2.one, 12, Color.white, FontStyle.Bold);
            slot.buttonText.raycastTarget = false;

            // 购买事件
            int slotIndex = index;
            slot.buyButton.onClick.AddListener(() => OnBuyClicked(slotIndex));

            // 悬停事件
            var trigger = cardGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            int idx = index;
            enterEntry.callback.AddListener((_) => ShowItemTooltip(idx));
            trigger.triggers.Add(enterEntry);

            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener((_) => HideItemTooltip());
            trigger.triggers.Add(exitEntry);

            return slot;
        }

        private void OnBuyClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _shopSlots.Count) return;
            var slot = _shopSlots[slotIndex];
            if (slot.sold) return;

            if (PlayerResources.Instance == null || !PlayerResources.Instance.SpendShards(slot.price))
            {
                Debug.Log("<color=red>灵力碎片不足！</color>");
                return;
            }

            // 功法商品购买
            if (slot.skill != null)
            {
                if (PlayerController.Instance != null)
                {
                    var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                    if (combat != null)
                    {
                        int emptySlot = combat.FindEmptySlot();
                        if (emptySlot >= 0)
                        {
                            combat.EquipSkillToSlot(slot.skill, emptySlot);
                            GameEvents.Publish(new GameEvents.SkillEquipped
                            {
                                Skill = slot.skill,
                                SlotIndex = emptySlot
                            });
                        }
                        else
                        {
                            // 槽位满了 → 生成SkillPickup让玩家通过选择面板替换
                            Vector3 dropPos = PlayerController.Instance.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                            SkillPickup.Spawn(slot.skill, dropPos);
                        }
                    }
                }

                slot.sold = true;
                _shopSlots[slotIndex] = slot;
                if (slot.cardBg != null) slot.cardBg.color = new Color(0.08f, 0.08f, 0.08f, 0.5f);
                if (slot.buttonText != null) slot.buttonText.text = "已售出";
                if (slot.buyButton != null) slot.buyButton.interactable = false;
                var skillBtnImg = slot.buyButton?.GetComponent<Image>();
                if (skillBtnImg != null) skillBtnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);

                Debug.Log($"<color=green>购买功法成功：{slot.skill.skillName}（花费 {slot.price} 灵力碎片）</color>");
                RefreshShardsDisplay();
                RefreshAllCards();
                return;
            }

            // 灵物商品购买
            if (slot.item != null && PlayerController.Instance != null)
            {
                PlayerController.Instance.Inventory.AddItem(slot.item);

                // 如果是功法类灵物，自动装备
                if (slot.item.linkedSkill != null)
                {
                    var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
                    if (combat != null)
                    {
                        int emptySlot = combat.FindEmptySlot();
                        if (emptySlot >= 0)
                        {
                            combat.EquipSkillToSlot(slot.item.linkedSkill, emptySlot);
                            GameEvents.Publish(new GameEvents.SkillEquipped
                            {
                                Skill = slot.item.linkedSkill,
                                SlotIndex = emptySlot
                            });
                        }
                    }
                }

                // 放入灵物槽位
                var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null)
                {
                    // 检查是否已有相同灵物
                    bool hasSame = false;
                    for (int i = 0; i < spiritSlots.Slots.Count; i++)
                    {
                        if (spiritSlots.Slots[i].item == slot.item)
                        {
                            hasSame = true;
                            break;
                        }
                    }
                    if (!hasSame)
                    {
                        int empty = spiritSlots.FindEmptySlot();
                        if (empty >= 0)
                            spiritSlots.SetSlot(empty, slot.item);
                    }
                }
            }

            // 标记已售出
            slot.sold = true;
            _shopSlots[slotIndex] = slot;

            // 更新卡片显示
            if (slot.cardBg != null) slot.cardBg.color = new Color(0.08f, 0.08f, 0.08f, 0.5f);
            if (slot.buttonText != null) slot.buttonText.text = "已售出";
            if (slot.buyButton != null) slot.buyButton.interactable = false;
            var btnImg = slot.buyButton?.GetComponent<Image>();
            if (btnImg != null) btnImg.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);

            Debug.Log($"<color=green>购买成功：{slot.item.itemName}（花费 {slot.price} 灵力碎片）</color>");

            RefreshShardsDisplay();
            RefreshAllCards();
        }

        private void RefreshShardsDisplay()
        {
            if (_shardsText != null && PlayerResources.Instance != null)
                _shardsText.text = $"✦ 灵力碎片：{PlayerResources.Instance.SpiritShards}";
        }

        private void RefreshAllCards()
        {
            for (int i = 0; i < _shopSlots.Count; i++)
            {
                var slot = _shopSlots[i];
                if (slot.sold) continue;

                bool canAfford = PlayerResources.Instance != null &&
                    PlayerResources.Instance.HasShards(slot.price);
                var btnImg = slot.buyButton?.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = canAfford
                        ? new Color(0.2f, 0.5f, 0.3f, 0.9f)
                        : new Color(0.3f, 0.2f, 0.2f, 0.6f);
                if (slot.buttonText != null)
                    slot.buttonText.text = canAfford ? "购 买" : "碎片不足";
            }
        }

        // ==================== 悬停提示 ====================

        private void CreateTooltip()
        {
            _tooltipPanel = new GameObject("ShopTooltip");
            _tooltipPanel.transform.SetParent(_shopCanvas.transform, false);
            _tooltipRT = _tooltipPanel.AddComponent<RectTransform>();
            _tooltipRT.sizeDelta = new Vector2(260, 160);
            _tooltipRT.pivot = new Vector2(0, 0.5f);

            var bg = _tooltipPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
            bg.raycastTarget = false;

            // 边框
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_tooltipPanel.transform, false);
            var borderRT = borderGo.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-1, -1);
            borderRT.offsetMax = new Vector2(1, 1);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.5f, 0.4f, 0.3f, 0.6f);
            borderImg.raycastTarget = false;
            borderGo.transform.SetAsFirstSibling();

            _tooltipTitle = CreateText(_tooltipPanel.transform, "TTitle", "",
                new Vector2(0, 0.75f), new Vector2(1, 1),
                16, Color.white, FontStyle.Bold);
            _tooltipTitle.raycastTarget = false;

            _tooltipDesc = CreateText(_tooltipPanel.transform, "TDesc", "",
                new Vector2(0, 0.4f), new Vector2(1, 0.75f),
                12, new Color(0.8f, 0.8f, 0.8f, 0.9f), FontStyle.Normal);
            _tooltipDesc.raycastTarget = false;

            _tooltipEffect = CreateText(_tooltipPanel.transform, "TEffect", "",
                new Vector2(0, 0.15f), new Vector2(1, 0.4f),
                11, new Color(0.6f, 0.9f, 0.6f, 0.9f), FontStyle.Normal);
            _tooltipEffect.raycastTarget = false;

            _tooltipPrice = CreateText(_tooltipPanel.transform, "TPrice", "",
                new Vector2(0, 0), new Vector2(1, 0.15f),
                13, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
            _tooltipPrice.raycastTarget = false;

            _tooltipPanel.SetActive(false);
        }

        private void ShowItemTooltip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _shopSlots.Count) return;
            var slot = _shopSlots[slotIndex];

            // 功法商品提示
            if (slot.skill != null)
            {
                string rarityName = slot.skill.rarity switch
                {
                    ItemRarity.Fan => "凡品",
                    ItemRarity.Ling => "灵品",
                    ItemRarity.Xuan => "玄品",
                    ItemRarity.Di => "地品",
                    ItemRarity.Tian => "天品",
                    _ => "凡品"
                };
                string typeStr = slot.skill.skillType switch
                {
                    SkillType.AreaDamage => "范围伤害",
                    SkillType.Projectile => "投射物",
                    SkillType.Dash => "位移",
                    SkillType.Buff => "增益",
                    SkillType.Heal => "治疗",
                    SkillType.Summon => "召唤",
                    _ => "未知"
                };
                Color rarityColor = slot.skill.rarity switch
                {
                    ItemRarity.Fan => Color.white,
                    ItemRarity.Ling => Color.green,
                    ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                    ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                    ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                    _ => Color.white
                };

                _tooltipTitle.text = $"📜 {slot.skill.skillName}（{rarityName}）";
                _tooltipTitle.color = rarityColor;
                _tooltipDesc.text = slot.skill.description;
                string effectStr = $"类型：{typeStr}\n伤害：{slot.skill.baseDamage} (+{slot.skill.damageScaling * 100:0}%攻)\nCD：{slot.skill.cooldown}s";
                if (slot.skill.skillType == SkillType.Heal)
                    effectStr = $"类型：{typeStr}\n治疗：{slot.skill.healAmount} (+{slot.skill.healScaling * 100:0}%攻)\nCD：{slot.skill.cooldown}s";
                _tooltipEffect.text = effectStr;
                _tooltipPrice.text = slot.sold ? "已售出" : $"价格：✦ {slot.price} 灵力碎片";

                if (slot.cardGo != null)
                {
                    var cardRT = slot.cardGo.GetComponent<RectTransform>();
                    Vector3 cardWorldPos = cardRT.position;
                    Vector2 screenPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_shopCanvas.transform,
                        RectTransformUtility.WorldToScreenPoint(null, cardWorldPos),
                        null, out screenPos);
                    _tooltipRT.anchoredPosition = screenPos + new Vector2(80, 0);
                }

                _tooltipPanel.SetActive(true);
                return;
            }

            // 灵物商品提示
            if (slot.item == null) return;

            string itemRarityName = slot.item.rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };

            _tooltipTitle.text = $"{slot.item.itemName}（{itemRarityName}）";
            _tooltipTitle.color = slot.item.GetRarityColor();
            _tooltipDesc.text = slot.item.description;
            _tooltipEffect.text = GetDetailedEffect(slot.item);
            _tooltipPrice.text = slot.sold ? "已售出" : $"价格：✦ {slot.price} 灵力碎片";

            // 定位到卡片右侧
            if (slot.cardGo != null)
            {
                var cardRT = slot.cardGo.GetComponent<RectTransform>();
                Vector3 cardWorldPos = cardRT.position;
                Vector2 screenPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_shopCanvas.transform, 
                    RectTransformUtility.WorldToScreenPoint(null, cardWorldPos),
                    null, out screenPos);
                _tooltipRT.anchoredPosition = screenPos + new Vector2(110, 0);
            }

            _tooltipPanel.SetActive(true);
        }

        private void HideItemTooltip()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }

        // ==================== 关闭按钮 ====================

        private void CreateCloseButton()
        {
            var btnGo = new GameObject("CloseBtn");
            btnGo.transform.SetParent(_shopPanel.transform, false);
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0);
            btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot = new Vector2(0.5f, 0);
            btnRT.anchoredPosition = new Vector2(0, 10);
            btnRT.sizeDelta = new Vector2(140, 36);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.4f, 0.25f, 0.15f, 0.9f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(CloseShop);

            CreateText(btnGo.transform, "Text", "关闭商店",
                Vector2.zero, Vector2.one, 16, new Color(1f, 0.9f, 0.7f), FontStyle.Bold);
        }

        // ==================== 开关商店 ====================

        public void OpenShop()
        {
            _shopOpen = true;
            if (_shopCanvas != null) _shopCanvas.SetActive(true);
            RefreshShardsDisplay();
            RefreshAllCards();

            // 解锁鼠标
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseShop()
        {
            _shopOpen = false;
            if (_shopCanvas != null) _shopCanvas.SetActive(false);
            HideItemTooltip();
        }

        private void Update()
        {
            // 同步交互提示：被路由器选中时才显示「按 F 交易」提示
            if (_headCard != null)
            {
                bool wantHint = _playerInRange && !_shopOpen && IsRoutedActive;
                _headCard.SetHintVisible(wantHint);
            }

            // 仅在被路由器选中时响应 F（避免与拾取物等其他交互体重叠时同时触发）
            if (_playerInRange && !_shopOpen && IsRoutedActive)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.fKey.wasPressedThisFrame)
                    OpenShop();
            }

            // Esc 关闭商店
            if (_shopOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    CloseShop();
            }
        }

        /// <summary>玩家进入商人范围</summary>
        public void OnPlayerEnterRange()
        {
            _playerInRange = true;
            InteractionRouter.Register(this);
            // 实际是否显示提示由 Update 中 IsRoutedActive 决定
        }

        /// <summary>玩家离开商人范围</summary>
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

        private string GetBriefEffect(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"攻+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻+{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"命+{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"速+{item.moveSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤+{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"暴击+{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"击杀回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            if (item.linkedSkill != null) parts.Add($"功法：{item.linkedSkill.skillName}");
            return parts.Count > 0 ? string.Join("\n", parts) : "基础灵物";
        }

        private string GetDetailedEffect(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"⚔ 攻击力 +{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"⚔ 攻击力 +{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"♥ 生命上限 +{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"♥ 生命上限 +{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"👟 移速 +{item.moveSpeedBonusPercent * 100:0}%");
            if (item.attackSpeedBonusPercent > 0) parts.Add($"⚡ 攻速 +{item.attackSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"🛡 减伤 +{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"✧ 暴击率 +{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"♥ 击杀回复 {item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"🔥 灼烧 {item.burnDamagePerSecond}/秒");
            if (item.pierceBonus > 0) parts.Add($"↣ 穿透 +{item.pierceBonus}");
            if (item.linkedSkill != null)
            {
                var sk = item.linkedSkill;
                string typeStr = sk.skillType switch
                {
                SkillType.AreaDamage => "范围伤害",
                SkillType.Projectile => "投射物",
                SkillType.Dash => "位移",
                SkillType.Buff => "增益",
                SkillType.Heal => "治疗",
                SkillType.Summon => "召唤",
                    _ => "未知"
                };
                parts.Add($"📜 功法：{sk.skillName}（{typeStr}）");
                parts.Add($"   伤害 {sk.baseDamage} | CD {sk.cooldown}s");
            }
            if (item.stackable && item.qualitativeThresholds != null && item.qualitativeThresholds.Length > 0)
            {
                string thresholds = string.Join("/", item.qualitativeThresholds);
                parts.Add($"<color=#FFD700>✨ {thresholds}个触发质变</color>");
            }
            return parts.Count > 0 ? string.Join("\n", parts) : "基础灵物，无特殊效果";
        }

        private string GetRaritySymbol(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => "凡",
                ItemRarity.Ling => "灵",
                ItemRarity.Xuan => "玄",
                ItemRarity.Di => "地",
                ItemRarity.Tian => "天",
                _ => "?"
            };
        }
    }

    /// <summary>商人交互触发器 —— 靠近商人时通知ShopRoom</summary>
    public class ShopInteractTrigger : MonoBehaviour
    {
        private ShopRoom _shop;
        private bool _inside;

        public void Initialize(ShopRoom shop)
        {
            _shop = shop;
        }

        private void OnTriggerEnter(Collider other) => TryEnter(other);

        // 兜底：商人 NPC 在 (0,1,3.5) r=3，与玩家出生位 (0,0.1,0) 距离 ≈3.5m 处于临界，
        // 若未来 NPC 位置调整或 r 变大，会出现 TeleportPlayer 出生即在 trigger 内的死局；
        // 跟其他 TriggerBridge / ChestTrigger 保持一致的 Stay 兜底，结构性安全。
        private void OnTriggerStay(Collider other) => TryEnter(other);

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!_inside) return;
            _inside = false;
            _shop?.OnPlayerExitRange();
        }

        private void TryEnter(Collider other)
        {
            if (_inside) return;
            if (!other.CompareTag("Player")) return;
            _inside = true;
            _shop?.OnPlayerEnterRange();
        }
    }
}
