using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 商店房间 —— 完整的商店UI系统（V0.4.6 UI 改 uGUI+TMP）。
    /// 进入房间后靠近商人按 F 弹出商店面板，展示随机商品（技能 + 模块），用碎片购买。
    /// </summary>
    public class ShopRoom : MonoBehaviour, IInteractable
    {
        private SkillData[] _shopSkills;
        private ModuleDef[] _shopModules;
        private int _roomIndex;
        private GameObject _roomVisuals;
        private Transform _shopkeeperTransform;

        // ===== IInteractable：参与统一 F 交互路由 =====
        public Vector3 InteractionWorldPos =>
            _shopkeeperTransform != null ? _shopkeeperTransform.position : transform.position;
        public int InteractionPriority => 40;
        public bool IsInteractionAvailable => _playerInRange && !_shopOpen;
        public bool IsRoutedActive { get; set; }

        // 商店UI（uGUI+TMP）
        private GameObject _shopUI;          // Canvas 根
        private TextMeshProUGUI _shardsLabel;
        private RectTransform _cardsGrid;
        private readonly List<ShopSlot> _shopSlots = new();
        private GameObject _tooltip;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;
        private bool _shopOpen;
        private Button _refreshBtn;
        private TextMeshProUGUI _refreshLabel;
        private int _refreshCount;
        private const int RefreshBaseCost = 20;

        private class ShopSlot
        {
            public SkillData skill;
            public ModuleDef module;
            public int price;
            public bool sold;
            public GameObject cardEl;
            public TextMeshProUGUI priceLabel;
            public Button buyBtn;
            public TextMeshProUGUI buyLabel;
        }

        public float RoomWidth => 20f;
        public float RoomDepth => 20f;

        private bool _playerInRange;
        private NpcHeadCard _headCard;

        public void Initialize(int roomIndex, SkillData[] skillPool = null, ModuleDef[] modulePool = null)
        {
            _roomIndex = roomIndex;
            _shopSkills = skillPool;
            _shopModules = modulePool;
            BuildRoom();
            CreateShopUI();

            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            InteractionRouter.Unregister(this);
            if (_roomVisuals != null) Destroy(_roomVisuals);
            if (_shopUI != null) Destroy(_shopUI);
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

            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Shopkeeper";
            npc.transform.SetParent(transform);
            npc.transform.localPosition = new Vector3(0, 1f, 3.5f);
            _shopkeeperTransform = npc.transform;
            var npcCol = npc.GetComponent<Collider>();
            if (npcCol != null) Destroy(npcCol);
            var npcRend = npc.GetComponent<Renderer>();
            if (npcRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.9f, 0.8f, 0.5f);
                npcRend.material = mat;
            }

            _headCard = NpcHeadCard.Attach(npc.transform, new NpcHeadCard.Config
            {
                displayName = "散修商人",
                icon = "✦",
                roleSub = "道具交易",
                hintText = "按 [F] 交易",
                themeColor = new Color(1f, 0.82f, 0.35f),
                yOffset = 2.0f,
                showLongRangeMarker = true
            });

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
                mat.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.5f);
                pillarRend.material = mat;
            }
        }

        // ==================== 商店UI（uGUI+TMP） ====================

        private void CreateShopUI()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("ShopUI", 118);
            _shopUI = canvas.gameObject;
            UGuiKit.CreateScrim(_shopUI.transform, new Color(0.02f, 0.03f, 0.06f, 0.9f));

            var panel = UGuiKit.CreatePanel(_shopUI.transform, "Panel", new Vector2(920f, 680f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 10f, new RectOffset(24, 24, 18, 18), TextAnchor.UpperCenter);

            // 标题行
            var header = UGuiKit.CreateRow(panel, 12f, 44f);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            var title = UGuiKit.CreateText(header, "散修商店", 28, new Color(1f, 0.82f, 0.35f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(title, 40f); title.GetComponent<LayoutElement>().preferredWidth = 300f;
            _shardsLabel = UGuiKit.CreateText(header, "✦ 碎片：0", 20, new Color(0.95f, 0.85f, 0.4f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_shardsLabel, 40f); _shardsLabel.GetComponent<LayoutElement>().preferredWidth = 240f;
            _refreshBtn = UGuiKit.CreateButton(header, "🔄 刷新", OnRefreshClicked, out _refreshLabel, UGuiKit.BtnNormal, 16, new Vector2(200f, 40f));
            UGuiKit.SetHeight(_refreshBtn.GetComponent<RectTransform>(), 40f); _refreshBtn.GetComponent<LayoutElement>().preferredWidth = 200f;
            var close = UGuiKit.CreateButton(header, "✕", CloseShop, UGuiKit.BtnNormal, 20, new Vector2(44f, 40f));
            UGuiKit.SetHeight(close.GetComponent<RectTransform>(), 40f); close.GetComponent<LayoutElement>().preferredWidth = 44f;

            // 商品网格（7 个：4 列）
            _cardsGrid = UGuiKit.CreateGrid(panel, new Vector2(200f, 300f), new Vector2(12f, 12f), 4);
            UGuiKit.SetHeight(_cardsGrid, 620f);

            // 底部悬停提示
            _tooltip = new GameObject("Tooltip", typeof(RectTransform), typeof(Image)).GetComponent<Image>().gameObject;
            var trt = (RectTransform)_tooltip.transform;
            trt.SetParent(panel, false);
            _tooltip.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            var tle = _tooltip.AddComponent<LayoutElement>(); tle.preferredHeight = 84f; tle.minHeight = 84f;
            var tv = _tooltip.AddComponent<VerticalLayoutGroup>();
            tv.padding = new RectOffset(14, 14, 8, 8); tv.spacing = 4f;
            tv.childControlWidth = true; tv.childForceExpandWidth = true; tv.childControlHeight = true; tv.childForceExpandHeight = false;
            _tooltipTitle = UGuiKit.CreateText(trt, "", 16, UGuiKit.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(_tooltipTitle, 22f);
            _tooltipBody = UGuiKit.CreateText(trt, "", 13, new Color(0.75f, 0.78f, 0.85f), TextAlignmentOptions.TopLeft);
            _tooltipBody.enableWordWrapping = true;
            var ble = _tooltipBody.gameObject.AddComponent<LayoutElement>(); ble.flexibleHeight = 1f; ble.minHeight = 40f;

            GenerateShopItems();
            HideItemTooltip();
            UpdateRefreshButton();
            _shopUI.SetActive(false);
        }

        private void GenerateShopItems()
        {
            if (_cardsGrid == null) return;
            for (int i = _cardsGrid.childCount - 1; i >= 0; i--) Destroy(_cardsGrid.GetChild(i).gameObject);
            _shopSlots.Clear();

            int slotIdx = 0;
            int skillSlots = 2;
            int moduleSlots = 5;

            if (_shopSkills != null && _shopSkills.Length > 0)
            {
                for (int i = 0; i < skillSlots; i++)
                {
                    var skill = _shopSkills[Random.Range(0, _shopSkills.Length)];
                    if (skill == null) continue;
                    _shopSlots.Add(BuildSkillCard(skill, CalculateSkillPrice(skill), slotIdx));
                    slotIdx++;
                }
            }

            if (_shopModules != null && _shopModules.Length > 0)
            {
                for (int i = 0; i < moduleSlots; i++)
                {
                    var mod = ModuleDropWeighting.PickWeighted(_shopModules, GetFloorRarityBias());
                    if (mod == null) continue;
                    _shopSlots.Add(BuildModuleCard(mod, CalculateModulePrice(mod), slotIdx));
                    slotIdx++;
                }
            }
        }

        private static int GetFloorRarityBias()
        {
            int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0;
            return MapProviders.Current.GetRarityBias(currentLevel);
        }

        private int CalculateSkillPrice(SkillData skill)
        {
            int basePrice = PlayerResources.GetDecomposeShards(skill.rarity);
            return Mathf.RoundToInt(basePrice * 4.5f);
        }

        private int CalculateModulePrice(ModuleDef mod)
        {
            int basePrice = PlayerResources.GetDecomposeShards(mod.rarity);
            return Mathf.RoundToInt(basePrice * 3f);
        }

        private static string ModuleCategoryName(ModuleCategory c) => c switch
        {
            ModuleCategory.Trigger => "触发器",
            ModuleCategory.Effect => "效果器",
            ModuleCategory.Modifier => "改造件",
            ModuleCategory.Universal => "通用",
            _ => "模块"
        };

        private ShopSlot BuildModuleCard(ModuleDef mod, int price, int index)
        {
            var slot = new ShopSlot { module = mod, price = price };
            Color rc = RarityColor(mod.rarity);
            string brief = $"{ModuleCategoryName(mod.category)}\n{mod.description}";
            if (brief.Length > 40) brief = brief.Substring(0, 40) + "…";
            NewCard(rc, "▣", mod.displayName, $"模块·{ModuleCategoryName(mod.category)}", brief, slot, index);
            return slot;
        }

        private ShopSlot BuildSkillCard(SkillData skill, int price, int index)
        {
            var slot = new ShopSlot { skill = skill, price = price };
            Color rc = RarityColor(skill.rarity);
            string brief = skill.skillType == SkillType.Heal
                ? $"治疗 {skill.healAmount}\nCD {skill.cooldown}s"
                : $"伤害 {skill.baseDamage}\nCD {skill.cooldown}s";
            NewCard(rc, "技", skill.skillName, $"技能·{SkillTypeName(skill.skillType)}", brief, slot, index);
            return slot;
        }

        /// <summary>构建一张商品卡（色框/图标/名称/副标/简效/价格/购买/悬停）。</summary>
        private void NewCard(Color rarityColor, string iconSymbol, string name, string sub, string brief, ShopSlot slot, int index)
        {
            var card = UGuiKit.CreateCard(_cardsGrid, new Vector2(200f, 300f), rarityColor);

            var iconL = UGuiKit.CreateText(card, iconSymbol, 30, rarityColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(iconL, 40f);
            var nameL = UGuiKit.CreateText(card, name, 17, rarityColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(nameL, 24f);
            var subL = UGuiKit.CreateText(card, sub, 12, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(subL, 18f);
            var effL = UGuiKit.CreateText(card, brief, 12, new Color(0.72f, 0.74f, 0.8f), TextAlignmentOptions.Top);
            effL.enableWordWrapping = true;
            var ele = effL.gameObject.AddComponent<LayoutElement>(); ele.flexibleHeight = 1f; ele.minHeight = 40f;

            slot.priceLabel = UGuiKit.CreateText(card, $"✦ {slot.price}", 16, new Color(0.95f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(slot.priceLabel, 22f);

            slot.buyBtn = UGuiKit.CreateButton(card, "购 买", () => OnBuyClicked(index), out slot.buyLabel, new Color(rarityColor.r * 0.35f, rarityColor.g * 0.35f, rarityColor.b * 0.35f, 0.95f), 15, new Vector2(160f, 36f));
            UGuiKit.SetHeight(slot.buyBtn.GetComponent<RectTransform>(), 36f);

            slot.cardEl = card.parent.gameObject; // Card 外框

            int idx = index;
            var trig = slot.cardEl.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowItemTooltip(idx));
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideItemTooltip());
            trig.triggers.Add(exit);
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
            if (slot.buyLabel != null) slot.buyLabel.text = "已售出";
            if (slot.buyBtn != null) UGuiKit.SetButtonEnabled(slot.buyBtn, false);
            var img = slot.cardEl != null ? slot.cardEl.GetComponent<Image>() : null;
            if (img != null) img.color = new Color(0.3f, 0.3f, 0.32f, 0.6f);
        }

        private void OnBuyClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _shopSlots.Count) return;
            var slot = _shopSlots[slotIndex];
            if (slot.sold) return;

            if (PlayerResources.Instance == null || !PlayerResources.Instance.SpendShards(slot.price))
            {
                Debug.Log("<color=red>碎片不足！</color>");
                return;
            }

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
                            GameEvents.Publish(new GameEvents.SkillEquipped { Skill = slot.skill, SlotIndex = emptySlot });
                        }
                        else
                        {
                            Vector3 dropPos = PlayerController.Instance.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                            SkillPickup.Spawn(slot.skill, dropPos);
                        }
                    }
                }
                ApplySold(slot);
                Debug.Log($"<color=green>购买技能成功：{slot.skill.skillName}（花费 {slot.price} 碎片）</color>");
            }
            else if (slot.module != null)
            {
                if (PlayerController.Instance != null)
                {
                    var mgr = PlayerController.Instance.GetComponent<ModuleSlotManager>();
                    if (mgr != null)
                    {
                        bool ok = RewardPickUI.TryAutoEquipModule(mgr, slot.module);
                        if (!ok)
                            Debug.Log("<color=#ffcc33>[Shop] 模块槽位已满，请打开装配界面 [M] 手动调整</color>");
                    }
                    GameEvents.Publish(new GameEvents.ModulePickedUp { Module = slot.module });
                }
                ApplySold(slot);
                Debug.Log($"<color=green>购买模块成功：{slot.module.displayName}（花费 {slot.price} 碎片）</color>");
            }

            RefreshShardsDisplay();
            RefreshAllCards();
        }

        private void RefreshShardsDisplay()
        {
            if (_shardsLabel != null && PlayerResources.Instance != null)
                _shardsLabel.text = $"✦ 碎片：{PlayerResources.Instance.SpiritShards}";
        }

        private void RefreshAllCards()
        {
            foreach (var slot in _shopSlots)
            {
                if (slot.sold) continue;
                bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(slot.price);
                if (slot.buyBtn != null) slot.buyBtn.interactable = canAfford;
                if (slot.buyLabel != null) slot.buyLabel.text = canAfford ? "购 买" : "碎片不足";
            }
        }

        // ==================== 悬停提示 ====================

        private void ShowItemTooltip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _shopSlots.Count) return;
            if (_tooltip == null || _tooltipTitle == null || _tooltipBody == null) return;
            var slot = _shopSlots[slotIndex];

            if (slot.skill != null)
            {
                _tooltipTitle.text = $"{slot.skill.skillName}（{RarityName(slot.skill.rarity)} · 技能）";
                _tooltipTitle.color = RarityColor(slot.skill.rarity);
                string eff = slot.skill.skillType == SkillType.Heal
                    ? $"类型：{SkillTypeName(slot.skill.skillType)}　治疗：{slot.skill.healAmount} (+{slot.skill.healScaling * 100:0}%攻)　CD：{slot.skill.cooldown}s"
                    : $"类型：{SkillTypeName(slot.skill.skillType)}　伤害：{slot.skill.baseDamage} (+{slot.skill.damageScaling * 100:0}%攻)　CD：{slot.skill.cooldown}s";
                _tooltipBody.text = $"{slot.skill.description}\n{eff}\n{(slot.sold ? "已售出" : $"价格：✦ {slot.price} 碎片")}";
            }
            else if (slot.module != null)
            {
                _tooltipTitle.text = $"{slot.module.displayName}（{RarityName(slot.module.rarity)} · {ModuleCategoryName(slot.module.category)}）";
                _tooltipTitle.color = RarityColor(slot.module.rarity);
                _tooltipBody.text = $"{slot.module.description}\n{(slot.sold ? "已售出" : $"价格：✦ {slot.price} 碎片")}";
            }
            else return;
            _tooltip.SetActive(true);
        }

        private void HideItemTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        // ==================== 开关商店 ====================

        public void OpenShop()
        {
            _shopOpen = true;
            if (_shopUI != null) _shopUI.SetActive(true);
            RefreshShardsDisplay();
            RefreshAllCards();
            UpdateRefreshButton();

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void CloseShop()
        {
            _shopOpen = false;
            if (_shopUI != null) _shopUI.SetActive(false);
            HideItemTooltip();
        }

        /// <summary>V0.4.1：刷新商品（消耗基础货币，每次刷新费用递增）</summary>
        private void OnRefreshClicked()
        {
            int cost = GetRefreshCost();
            if (PlayerResources.Instance == null || !PlayerResources.Instance.SpendShards(cost))
            {
                Debug.Log("<color=red>碎片不足，无法刷新！</color>");
                return;
            }

            _refreshCount++;
            GenerateShopItems();
            RefreshShardsDisplay();
            RefreshAllCards();
            UpdateRefreshButton();
            Debug.Log($"<color=cyan>[Shop] 商品已刷新（花费 {cost}，第 {_refreshCount} 次）</color>");
        }

        private int GetRefreshCost() => RefreshBaseCost * (_refreshCount + 1);

        private void UpdateRefreshButton()
        {
            if (_refreshBtn == null) return;
            int cost = GetRefreshCost();
            if (_refreshLabel != null) _refreshLabel.text = $"🔄 刷新（✦{cost}）";
            bool canAfford = PlayerResources.Instance != null && PlayerResources.Instance.HasShards(cost);
            _refreshBtn.interactable = canAfford;
        }

        private void Update()
        {
            if (_headCard != null)
            {
                bool wantHint = _playerInRange && !_shopOpen && IsRoutedActive;
                _headCard.SetHintVisible(wantHint);
            }

            if (_playerInRange && !_shopOpen && IsRoutedActive)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.fKey.wasPressedThisFrame)
                    OpenShop();
            }

            if (_shopOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    CloseShop();
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
