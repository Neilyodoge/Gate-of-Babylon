using UnityEngine;
using UnityEngine.UIElements;
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

        // 商店UI（UITK）
        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _shardsLabel;
        private VisualElement _cardsRow;
        private readonly List<ShopSlot> _shopSlots = new();
        private VisualElement _tooltipEl;
        private Label _tooltipTitle;
        private Label _tooltipBody;
        private bool _shopOpen;

        // 商品数据
        private class ShopSlot
        {
            public ItemData item;
            public SkillData skill; // 功法商品
            public int price;
            public bool sold;
            public VisualElement cardEl;
            public Label priceLabel;
            public Button buyBtn;
            public Label buyLabel;
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

            // 监听资源变化刷新余额显示
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
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/ShopRoom");

            var go = new GameObject("ShopUITK");
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
            _tooltipEl = root.Q<VisualElement>("tooltip");
            _tooltipTitle = root.Q<Label>("tt-title");
            _tooltipBody = root.Q<Label>("tt-body");
            var close = root.Q<Button>("close");
            if (close != null) close.clicked += CloseShop;

            GenerateShopItems();
            HideItemTooltip();
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void GenerateShopItems()
        {
            if (_cardsRow == null) return;
            _cardsRow.Clear();
            _shopSlots.Clear();

            const int totalSlots = 5;
            int slotIdx = 0;

            // 前3个槽位：灵物（V.03 Q8：灵物屏蔽时不上架，全部让位给功法）
            int itemSlots = FeatureFlags.EnableSpiritItems ? Mathf.Min(3, totalSlots) : 0;
            if (_shopItems != null && _shopItems.Length > 0)
            {
                for (int i = 0; i < itemSlots && slotIdx < totalSlots; i++)
                {
                    ItemData item;
                    var config = GameConfig.Instance;
                    if (config != null)
                    {
                        ItemRarity rarity = config.RollRarity();
                        var candidates = new List<ItemData>();
                        foreach (var d in _shopItems)
                            if (d != null && d.rarity == rarity && AvatarRestriction.IsAllowed(d)) candidates.Add(d);
                        if (candidates.Count == 0)
                            foreach (var d in _shopItems)
                                if (d != null && AvatarRestriction.IsAllowed(d)) candidates.Add(d);
                        item = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)]
                                                    : _shopItems[Random.Range(0, _shopItems.Length)];
                    }
                    else item = _shopItems[Random.Range(0, _shopItems.Length)];

                    if (item == null) continue;
                    _shopSlots.Add(BuildItemCard(item, CalculatePrice(item), slotIdx));
                    slotIdx++;
                }
            }

            // 后续槽位：功法
            if (_shopSkills != null && _shopSkills.Length > 0)
            {
                int skillSlots = totalSlots - slotIdx;
                for (int i = 0; i < skillSlots && slotIdx < totalSlots; i++)
                {
                    var skill = _shopSkills[Random.Range(0, _shopSkills.Length)];
                    if (skill == null) continue;
                    _shopSlots.Add(BuildSkillCard(skill, CalculateSkillPrice(skill), slotIdx));
                    slotIdx++;
                }
            }

            // 功法池空 → 灵物填充剩余（灵物屏蔽时跳过）
            if (FeatureFlags.EnableSpiritItems && _shopItems != null && _shopItems.Length > 0)
            {
                while (slotIdx < totalSlots)
                {
                    var item = _shopItems[Random.Range(0, _shopItems.Length)];
                    if (item == null) continue;
                    _shopSlots.Add(BuildItemCard(item, CalculatePrice(item), slotIdx));
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

        private ShopSlot BuildItemCard(ItemData item, int price, int index)
        {
            var slot = new ShopSlot { item = item, price = price };
            Color rc = item.GetRarityColor();
            var card = NewCard(rc, GetRaritySymbol(item.rarity), item.itemName, RarityName(item.rarity), GetBriefEffect(item), slot, index);
            _cardsRow.Add(card);
            return slot;
        }

        private ShopSlot BuildSkillCard(SkillData skill, int price, int index)
        {
            var slot = new ShopSlot { skill = skill, price = price };
            Color rc = RarityColor(skill.rarity);
            string brief = skill.skillType == SkillType.Heal
                ? $"治疗 {skill.healAmount}\nCD {skill.cooldown}s"
                : $"伤害 {skill.baseDamage}\nCD {skill.cooldown}s";
            var card = NewCard(rc, "法", skill.skillName, $"功法·{SkillTypeName(skill.skillType)}", brief, slot, index);
            _cardsRow.Add(card);
            return slot;
        }

        /// <summary>构建一张商品卡（共用：色条/图标/名称/副标/简效/价格/购买/悬停）。</summary>
        private VisualElement NewCard(Color rarityColor, string iconSymbol, string name, string sub, string brief, ShopSlot slot, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("shop-card");

            var bar = new VisualElement(); bar.AddToClassList("shop-rarity-bar"); bar.style.backgroundColor = rarityColor; card.Add(bar);

            var icon = new VisualElement(); icon.AddToClassList("shop-icon"); icon.style.backgroundColor = rarityColor * 0.5f;
            var iconL = new Label(iconSymbol); iconL.AddToClassList("shop-icon-label"); iconL.style.color = rarityColor; icon.Add(iconL); card.Add(icon);

            var nameL = new Label(name); nameL.AddToClassList("shop-name"); nameL.style.color = rarityColor; card.Add(nameL);
            var subL = new Label(sub); subL.AddToClassList("shop-rarity"); card.Add(subL);
            var effL = new Label(brief); effL.AddToClassList("shop-effect"); card.Add(effL);

            slot.priceLabel = new Label($"✦ {slot.price}"); slot.priceLabel.AddToClassList("shop-price"); card.Add(slot.priceLabel);

            slot.buyBtn = new Button(() => OnBuyClicked(index)) { text = "" };
            slot.buyBtn.AddToClassList("shop-buy");
            slot.buyLabel = new Label("购 买"); slot.buyBtn.Add(slot.buyLabel);
            card.Add(slot.buyBtn);

            int idx = index;
            card.RegisterCallback<PointerEnterEvent>(_ => ShowItemTooltip(idx));
            card.RegisterCallback<PointerLeaveEvent>(_ => HideItemTooltip());

            slot.cardEl = card;
            return card;
        }

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡品", ItemRarity.Ling => "灵品", ItemRarity.Xuan => "玄品",
            ItemRarity.Di => "地品", ItemRarity.Tian => "天品", _ => "凡品"
        };

        private static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Fan => Color.white,
            ItemRarity.Ling => Color.green,
            ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
            ItemRarity.Tian => new Color(1f, 0.85f, 0f),
            _ => Color.white
        };

        private static string SkillTypeName(SkillType t) => t switch
        {
            SkillType.AreaDamage => "范围伤害", SkillType.Projectile => "投射物", SkillType.Dash => "位移",
            SkillType.Buff => "增益", SkillType.Heal => "治疗", SkillType.Summon => "召唤", _ => "未知"
        };

        private void ApplySold(ShopSlot slot)
        {
            slot.sold = true;
            slot.cardEl?.AddToClassList("shop-card--sold");
            if (slot.buyLabel != null) slot.buyLabel.text = "已售出";
            if (slot.buyBtn != null) slot.buyBtn.SetEnabled(false);
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

                ApplySold(slot);
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

            ApplySold(slot);
            Debug.Log($"<color=green>购买成功：{slot.item.itemName}（花费 {slot.price} 灵力碎片）</color>");

            RefreshShardsDisplay();
            RefreshAllCards();
        }

        private void RefreshShardsDisplay()
        {
            if (_shardsLabel != null && PlayerResources.Instance != null)
                _shardsLabel.text = $"✦ 灵力碎片：{PlayerResources.Instance.SpiritShards}";
        }

        private void RefreshAllCards()
        {
            foreach (var slot in _shopSlots)
            {
                if (slot.sold) continue;
                bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(slot.price);
                if (slot.buyBtn != null)
                {
                    slot.buyBtn.SetEnabled(canAfford);
                    slot.buyBtn.EnableInClassList("shop-buy--poor", !canAfford);
                }
                if (slot.buyLabel != null) slot.buyLabel.text = canAfford ? "购 买" : "碎片不足";
            }
        }

        // ==================== 悬停提示 ====================

        private void ShowItemTooltip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _shopSlots.Count) return;
            if (_tooltipEl == null || _tooltipTitle == null || _tooltipBody == null) return;
            var slot = _shopSlots[slotIndex];

            if (slot.skill != null)
            {
                _tooltipTitle.text = $"{slot.skill.skillName}（{RarityName(slot.skill.rarity)} · 功法）";
                _tooltipTitle.style.color = RarityColor(slot.skill.rarity);
                string eff = slot.skill.skillType == SkillType.Heal
                    ? $"类型：{SkillTypeName(slot.skill.skillType)}　治疗：{slot.skill.healAmount} (+{slot.skill.healScaling * 100:0}%攻)　CD：{slot.skill.cooldown}s"
                    : $"类型：{SkillTypeName(slot.skill.skillType)}　伤害：{slot.skill.baseDamage} (+{slot.skill.damageScaling * 100:0}%攻)　CD：{slot.skill.cooldown}s";
                _tooltipBody.text = $"{slot.skill.description}\n{eff}\n{(slot.sold ? "已售出" : $"价格：✦ {slot.price} 灵力碎片")}";
                _tooltipEl.style.visibility = Visibility.Visible;
                return;
            }

            if (slot.item == null) return;
            _tooltipTitle.text = $"{slot.item.itemName}（{RarityName(slot.item.rarity)}）";
            _tooltipTitle.style.color = slot.item.GetRarityColor();
            _tooltipBody.text = $"{slot.item.description}\n{GetDetailedEffect(slot.item)}\n{(slot.sold ? "已售出" : $"价格：✦ {slot.price} 灵力碎片")}";
            _tooltipEl.style.visibility = Visibility.Visible;
        }

        private void HideItemTooltip()
        {
            if (_tooltipEl != null) _tooltipEl.style.visibility = Visibility.Hidden;
        }

        // ==================== 开关商店 ====================

        public void OpenShop()
        {
            _shopOpen = true;
            if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
            RefreshShardsDisplay();
            RefreshAllCards();

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void CloseShop()
        {
            _shopOpen = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
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
