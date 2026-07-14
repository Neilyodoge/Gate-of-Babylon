using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.3.0 信息面板 —— 展示玩家基础属性、成长属性、当前模板信息。
    /// Tab 键切换显示/隐藏（局内外均可），主菜单暂停菜单可按钮调起。
    /// UITK 程序化构建，无需额外 uxml/uss 资产。
    /// </summary>
    public class PlayerInfoPanel : MonoBehaviour
    {
        private static PlayerInfoPanel _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private UIDocument _doc;
        private VisualElement _overlay;

        private Label _templateName;
        private Label _templateDesc;
        private VisualElement _statsContainer;
        private VisualElement _growthContainer;
        private VisualElement _chainContainer;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PlayerInfoPanel");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerInfoPanel>();
        }

        public static void Show()
        {
            Ensure();
            if (_instance._visible) return;
            _instance._visible = true;
            _instance.Refresh();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        public static void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        private void Awake()
        {
            _instance = this;
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 11f;

            var root = _doc.rootVisualElement;
            Build(root);
            ChineseFontHelper.Apply(root);
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Build(VisualElement root)
        {
            _overlay = new VisualElement { name = "info-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.92f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            root.Add(_overlay);

            var panel = new VisualElement();
            panel.style.width = 680;
            panel.style.maxHeight = Length.Percent(85f);
            panel.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 0.98f);
            SetBorder(panel, 2, new Color(0.4f, 0.55f, 0.8f, 0.7f), 12);
            panel.style.paddingTop = 20; panel.style.paddingBottom = 20;
            panel.style.paddingLeft = 28; panel.style.paddingRight = 28;
            _overlay.Add(panel);

            // Title
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 16;
            panel.Add(titleRow);

            var title = new Label("角色信息");
            title.style.fontSize = 28;
            title.style.color = new Color(0.92f, 0.9f, 0.82f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(title);

            var closeBtn = new Button(Hide) { text = "✕" };
            closeBtn.style.fontSize = 20;
            closeBtn.style.width = 36; closeBtn.style.height = 36;
            closeBtn.style.borderTopLeftRadius = 18; closeBtn.style.borderTopRightRadius = 18;
            closeBtn.style.borderBottomLeftRadius = 18; closeBtn.style.borderBottomRightRadius = 18;
            titleRow.Add(closeBtn);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            panel.Add(scroll);

            // Template section
            _templateName = new Label();
            _templateName.style.fontSize = 20;
            _templateName.style.color = new Color(0.7f, 0.85f, 1f);
            _templateName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _templateName.style.marginBottom = 4;
            scroll.Add(_templateName);

            _templateDesc = new Label();
            _templateDesc.style.fontSize = 13;
            _templateDesc.style.color = new Color(0.65f, 0.68f, 0.75f);
            _templateDesc.style.whiteSpace = WhiteSpace.Normal;
            _templateDesc.style.marginBottom = 14;
            scroll.Add(_templateDesc);

            // Stats section
            scroll.Add(SectionTitle("基础属性"));
            _statsContainer = new VisualElement();
            _statsContainer.style.marginBottom = 16;
            scroll.Add(_statsContainer);

            // Growth section
            scroll.Add(SectionTitle("成长信息"));
            _growthContainer = new VisualElement();
            _growthContainer.style.marginBottom = 16;
            scroll.Add(_growthContainer);

            // Chain section
            scroll.Add(SectionTitle("当前增强链"));
            _chainContainer = new VisualElement();
            scroll.Add(_chainContainer);

            // Hotkey hint
            var hint = new Label("按 Tab 或点击 ✕ 关闭");
            hint.style.fontSize = 11;
            hint.style.color = new Color(0.5f, 0.52f, 0.58f);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 12;
            panel.Add(hint);
        }

        private void Refresh()
        {
            RefreshTemplate();
            RefreshStats();
            RefreshGrowth();
            RefreshChains();
        }

        private void RefreshTemplate()
        {
            var tpl = StartTemplateRegistry.Selected;
            if (tpl != null)
            {
                _templateName.text = $"起始模板：{tpl.displayName}";
                _templateDesc.text = tpl.description ?? "";
                _templateName.style.color = new StyleColor(tpl.themeColor);
            }
            else
            {
                _templateName.text = "起始模板：未选择";
                _templateDesc.text = "";
            }
        }

        private void RefreshStats()
        {
            _statsContainer.Clear();
            var player = PlayerController.Instance;
            if (player == null)
            {
                _statsContainer.Add(StatLabel("（未进入关卡，属性数据暂不可用）", new Color(0.6f, 0.6f, 0.65f)));
                return;
            }

            var s = player.Stats;
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            _statsContainer.Add(grid);

            grid.Add(StatCard("生命值", $"{Mathf.CeilToInt(s.currentHp)} / {Mathf.CeilToInt(s.maxHp)}", HPColor(s)));
            grid.Add(StatCard("攻击力", $"{s.attackDamage:F1}", new Color(1f, 0.7f, 0.4f)));
            grid.Add(StatCard("攻击速度", $"{s.attackSpeed * 100f:F0}%", new Color(0.9f, 0.85f, 0.5f)));
            grid.Add(StatCard("暴击率", $"{s.critRate * 100f:F1}%", new Color(1f, 0.5f, 0.5f)));
            grid.Add(StatCard("暴击伤害", $"{s.critDamage * 100f:F0}%", new Color(1f, 0.6f, 0.6f)));
            grid.Add(StatCard("移动速度", $"{s.moveSpeed:F1}", new Color(0.5f, 0.85f, 1f)));
            grid.Add(StatCard("防御力", $"{s.defense:F1}", new Color(0.6f, 0.8f, 0.6f)));
            grid.Add(StatCard("减伤比例", $"{s.damageReduction * 100f:F0}%", new Color(0.65f, 0.75f, 0.55f)));
        }

        private void RefreshGrowth()
        {
            _growthContainer.Clear();
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            _growthContainer.Add(grid);

            var insight = InsightSystem.Instance;
            if (insight != null)
            {
                grid.Add(StatCard("本局经验", $"{insight.RunInsight}", new Color(0.78f, 0.68f, 1f)));
                grid.Add(StatCard("永久经验", $"{insight.PermanentInsight}", new Color(0.65f, 0.55f, 0.95f)));
            }

            if (FeatureFlags.EnableCaveMeta)
            {
                var cult = CultivationSystem.Instance;
                if (cult != null)
                {
                    grid.Add(StatCard("等级", cult.CurrentRealmName, new Color(0.7f, 0.85f, 1f)));
                    grid.Add(StatCard("历练", $"{cult.RunTempering}", new Color(0.6f, 0.8f, 0.9f)));
                }
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                grid.Add(StatCard("当前层", gm.CurrentRealmName, new Color(0.8f, 0.9f, 0.7f)));
                float elapsed = gm.RunElapsedSeconds;
                if (elapsed > 0f)
                {
                    int m = (int)(elapsed / 60f);
                    int sec = (int)(elapsed % 60f);
                    grid.Add(StatCard("探索时长", $"{m:D2}:{sec:D2}", new Color(0.7f, 0.8f, 0.7f)));
                }
            }

            var hooks = LevelDesign.PlayerStateHooks.Instance;
            if (hooks != null)
            {
                grid.Add(StatCard("击杀数", $"{hooks.KillCount}", new Color(0.95f, 0.55f, 0.45f)));
                grid.Add(StatCard("道心", $"{hooks.Daoxin} ({hooks.DaoxinState})", DaoxinColor(hooks.Daoxin)));
            }
        }

        private void RefreshChains()
        {
            _chainContainer.Clear();

            var player = PlayerController.Instance;
            if (player == null)
            {
                _chainContainer.Add(StatLabel("（未进入关卡）", new Color(0.6f, 0.6f, 0.65f)));
                return;
            }

            var mgr = player.GetComponent<ModuleSlotManager>();
            if (mgr == null)
            {
                _chainContainer.Add(StatLabel("（模块系统未初始化）", new Color(0.6f, 0.6f, 0.65f)));
                return;
            }

            string[] slotKeys = { "Q", "E", "R" };
            var combat = player.GetComponent<PlayerCombat>();
            for (int i = 0; i < 3; i++)
            {
                var chain = mgr.GetChain(i);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;
                row.style.paddingTop = 6; row.style.paddingBottom = 6;
                row.style.paddingLeft = 10; row.style.paddingRight = 10;
                row.style.backgroundColor = new Color(0.1f, 0.11f, 0.15f, 0.9f);
                SetBorder(row, 1, new Color(0.3f, 0.35f, 0.45f, 0.6f), 6);

                var keyLabel = new Label(slotKeys[i]);
                keyLabel.style.fontSize = 18;
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                keyLabel.style.color = new Color(0.9f, 0.85f, 0.5f);
                keyLabel.style.width = 30;
                row.Add(keyLabel);

                var skill = combat != null ? combat.GetSkillInSlot(i) : null;
                var skillLabel = new Label(skill != null ? skill.skillName : "（空）");
                skillLabel.style.fontSize = 14;
                skillLabel.style.color = skill != null ? new StyleColor(new Color(0.85f, 0.88f, 0.95f)) : new StyleColor(new Color(0.5f, 0.5f, 0.55f));
                skillLabel.style.width = 120;
                row.Add(skillLabel);

                if (chain != null && (chain.trigger != null || chain.effect != null))
                {
                    var chainStr = BuildChainStr(chain);
                    var chainLabel = new Label(chainStr);
                    chainLabel.style.fontSize = 12;
                    chainLabel.style.color = new Color(0.7f, 0.75f, 0.85f);
                    chainLabel.style.whiteSpace = WhiteSpace.Normal;
                    chainLabel.style.flexGrow = 1;
                    row.Add(chainLabel);
                }
                else
                {
                    var empty = new Label("未装配增强链");
                    empty.style.fontSize = 12;
                    empty.style.color = new Color(0.45f, 0.47f, 0.52f);
                    empty.style.flexGrow = 1;
                    row.Add(empty);
                }

                _chainContainer.Add(row);
            }
        }

        private static string BuildChainStr(ModuleChain chain)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (chain.trigger != null) parts.Add($"[触] {chain.trigger.displayName}");
            if (chain.effect != null) parts.Add($"[效] {chain.effect.displayName}");
            if (chain.modifier0 != null) parts.Add($"[改] {chain.modifier0.displayName}");
            if (chain.modifier1 != null) parts.Add($"[改] {chain.modifier1.displayName}");
            return string.Join(" → ", parts);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                if (MainMenu.IsVisible || PauseMenu.IsVisible || ModuleAssemblyUI.IsVisible) return;
                Toggle();
                return;
            }

            if (_visible && kb.escapeKey.wasPressedThisFrame)
                Hide();
        }

        // ==================== Helpers ====================

        private static VisualElement SectionTitle(string text)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 8;
            container.style.marginBottom = 8;

            var line1 = new VisualElement();
            line1.style.height = 1;
            line1.style.flexGrow = 1;
            line1.style.backgroundColor = new Color(0.35f, 0.4f, 0.5f, 0.5f);
            container.Add(line1);

            var label = new Label($"  {text}  ");
            label.style.fontSize = 15;
            label.style.color = new Color(0.75f, 0.8f, 0.9f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(label);

            var line2 = new VisualElement();
            line2.style.height = 1;
            line2.style.flexGrow = 1;
            line2.style.backgroundColor = new Color(0.35f, 0.4f, 0.5f, 0.5f);
            container.Add(line2);

            return container;
        }

        private static VisualElement StatCard(string label, string value, Color color)
        {
            var card = new VisualElement();
            card.style.width = 140;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.backgroundColor = new Color(0.1f, 0.11f, 0.15f, 0.85f);
            SetBorder(card, 1, new Color(color.r, color.g, color.b, 0.35f), 6);

            var labelEl = new Label(label);
            labelEl.style.fontSize = 11;
            labelEl.style.color = new Color(0.6f, 0.62f, 0.68f);
            labelEl.style.marginBottom = 3;
            card.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.style.fontSize = 17;
            valueEl.style.color = new StyleColor(color);
            valueEl.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(valueEl);

            return card;
        }

        private static Label StatLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 13;
            l.style.color = new StyleColor(color);
            l.style.marginBottom = 6;
            return l;
        }

        private static Color HPColor(CombatStats s)
        {
            float ratio = s.maxHp > 0 ? s.currentHp / s.maxHp : 0;
            if (ratio > 0.6f) return new Color(0.3f, 0.9f, 0.4f);
            if (ratio > 0.3f) return new Color(1f, 0.8f, 0.2f);
            return new Color(0.95f, 0.3f, 0.3f);
        }

        private static Color DaoxinColor(int dx)
        {
            if (dx >= 80) return new Color(0.42f, 0.75f, 1f);
            if (dx >= 50) return new Color(0.85f, 0.88f, 0.9f);
            if (dx >= 20) return new Color(1f, 0.69f, 0.38f);
            return new Color(1f, 0.33f, 0.38f);
        }

        private static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopColor = color; e.style.borderBottomColor = color;
            e.style.borderLeftColor = color; e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }
    }
}
