using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.3.0 信息面板（V0.4.6 改 uGUI+TMP）—— 展示玩家基础属性、成长信息、当前增强链。
    /// C 键切换显示/隐藏（局内外均可），主菜单/暂停菜单可按钮调起。
    /// </summary>
    public class PlayerInfoPanel : MonoBehaviour
    {
        private static PlayerInfoPanel _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private GameObject _root;

        private TextMeshProUGUI _templateName;
        private RectTransform _content;        // scroll content
        private RectTransform _statsContainer;
        private RectTransform _growthContainer;
        private RectTransform _chainContainer;

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
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;
            if (_instance._root != null) _instance._root.SetActive(false);
        }

        public static void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        private void Awake()
        {
            _instance = this;
            Build();
            if (_root != null) _root.SetActive(false);
        }

        private void Build()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("PlayerInfoCanvas", 120, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.03f, 0.04f, 0.07f, 0.92f));

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(700f, 760f), new Color(0.08f, 0.09f, 0.13f, 0.98f));
            UGuiKit.AddVLayout(panel, 10f, new RectOffset(28, 28, 20, 20), TextAnchor.UpperCenter);

            // 标题行
            var header = UGuiKit.CreateRow(panel, 10f, 40f);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            var title = UGuiKit.CreateText(header, "角色信息", 28, new Color(0.92f, 0.9f, 0.82f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(title, 40f); title.GetComponent<LayoutElement>().preferredWidth = 560f;
            var close = UGuiKit.CreateButton(header, "✕", Hide, UGuiKit.BtnNormal, 20, new Vector2(40f, 40f));
            UGuiKit.SetHeight(close.GetComponent<RectTransform>(), 40f); close.GetComponent<LayoutElement>().preferredWidth = 40f;

            // 内容滚动
            _content = UGuiKit.CreateScroll(panel, "Content", out _, 8f, new RectOffset(6, 6, 6, 6));
            var scrollRoot = (RectTransform)_content.parent;
            var le = UGuiKit.SetHeight(scrollRoot, 620f); le.flexibleHeight = 1f;

            _templateName = UGuiKit.CreateText(_content, "冒险者", 20, new Color(0.9f, 0.85f, 0.6f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(_templateName, 28f);

            UGuiKit.CreateSectionTitle(_content, "基础属性");
            _statsContainer = UGuiKit.CreateGrid(_content, new Vector2(150f, 54f), new Vector2(8f, 8f), 4);

            UGuiKit.CreateSectionTitle(_content, "成长信息");
            _growthContainer = UGuiKit.CreateGrid(_content, new Vector2(150f, 54f), new Vector2(8f, 8f), 4);

            UGuiKit.CreateSectionTitle(_content, "当前增强链");
            _chainContainer = new GameObject("Chains", typeof(RectTransform)).GetComponent<RectTransform>();
            _chainContainer.SetParent(_content, false);
            var cv = _chainContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.spacing = 6f; cv.childControlWidth = true; cv.childForceExpandWidth = true; cv.childControlHeight = true; cv.childForceExpandHeight = false;

            var hint = UGuiKit.CreateText(panel, "按 Tab 或点击 ✕ 关闭", 12, new Color(0.5f, 0.52f, 0.58f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(hint, 20f);
        }

        private void Refresh()
        {
            RefreshStats();
            RefreshGrowth();
            RefreshChains();
        }

        private void ClearGrid(RectTransform grid)
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        }

        private void RefreshStats()
        {
            ClearGrid(_statsContainer);
            var player = PlayerController.Instance;
            if (player == null)
            {
                var l = UGuiKit.CreateText(_statsContainer, "（未进入关卡，属性数据暂不可用）", 13, new Color(0.6f, 0.6f, 0.65f), TextAlignmentOptions.Left);
                return;
            }

            var s = player.Stats;
            UGuiKit.CreateStatCard(_statsContainer, "生命值", $"{Mathf.CeilToInt(s.currentHp)} / {Mathf.CeilToInt(s.maxHp)}", HPColor(s));
            UGuiKit.CreateStatCard(_statsContainer, "攻击力", $"{s.attackDamage:F1}", new Color(1f, 0.7f, 0.4f));
            UGuiKit.CreateStatCard(_statsContainer, "攻击速度", $"{s.attackSpeed * 100f:F0}%", new Color(0.9f, 0.85f, 0.5f));
            UGuiKit.CreateStatCard(_statsContainer, "暴击率", $"{s.critRate * 100f:F1}%", new Color(1f, 0.5f, 0.5f));
            UGuiKit.CreateStatCard(_statsContainer, "暴击伤害", $"{s.critDamage * 100f:F0}%", new Color(1f, 0.6f, 0.6f));
            UGuiKit.CreateStatCard(_statsContainer, "移动速度", $"{s.moveSpeed:F1}", new Color(0.5f, 0.85f, 1f));
            UGuiKit.CreateStatCard(_statsContainer, "防御力", $"{s.defense:F1}", new Color(0.6f, 0.8f, 0.6f));
            UGuiKit.CreateStatCard(_statsContainer, "减伤比例", $"{s.damageReduction * 100f:F0}%", new Color(0.65f, 0.75f, 0.55f));
        }

        private void RefreshGrowth()
        {
            ClearGrid(_growthContainer);

            var insight = InsightSystem.Instance;
            if (insight != null)
            {
                UGuiKit.CreateStatCard(_growthContainer, "本局经验", $"{insight.RunInsight}", new Color(0.78f, 0.68f, 1f));
                UGuiKit.CreateStatCard(_growthContainer, "永久经验", $"{insight.PermanentInsight}", new Color(0.65f, 0.55f, 0.95f));
            }

            if (FeatureFlags.EnableCaveMeta)
            {
                var cult = CultivationSystem.Instance;
                if (cult != null)
                {
                    UGuiKit.CreateStatCard(_growthContainer, "等级", cult.CurrentRealmName, new Color(0.7f, 0.85f, 1f));
                    UGuiKit.CreateStatCard(_growthContainer, "历练", $"{cult.RunTempering}", new Color(0.6f, 0.8f, 0.9f));
                }
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                UGuiKit.CreateStatCard(_growthContainer, "当前层", gm.CurrentRealmName, new Color(0.8f, 0.9f, 0.7f));
                float elapsed = gm.RunElapsedSeconds;
                if (elapsed > 0f)
                {
                    int m = (int)(elapsed / 60f);
                    int sec = (int)(elapsed % 60f);
                    UGuiKit.CreateStatCard(_growthContainer, "探索时长", $"{m:D2}:{sec:D2}", new Color(0.7f, 0.8f, 0.7f));
                }
            }

            var hooks = LevelDesign.PlayerStateHooks.Instance;
            if (hooks != null)
            {
                UGuiKit.CreateStatCard(_growthContainer, "击杀数", $"{hooks.KillCount}", new Color(0.95f, 0.55f, 0.45f));
                UGuiKit.CreateStatCard(_growthContainer, "意志", $"{hooks.Daoxin} ({hooks.DaoxinState})", DaoxinColor(hooks.Daoxin));
            }
        }

        private void RefreshChains()
        {
            for (int i = _chainContainer.childCount - 1; i >= 0; i--) Destroy(_chainContainer.GetChild(i).gameObject);

            var player = PlayerController.Instance;
            if (player == null)
            {
                UGuiKit.CreateText(_chainContainer, "（未进入关卡）", 13, new Color(0.6f, 0.6f, 0.65f), TextAlignmentOptions.Left);
                return;
            }

            var mgr = player.GetComponent<ModuleSlotManager>();
            if (mgr == null)
            {
                UGuiKit.CreateText(_chainContainer, "（模块系统未初始化）", 13, new Color(0.6f, 0.6f, 0.65f), TextAlignmentOptions.Left);
                return;
            }

            string[] slotKeys = { "Q", "E", "R" };
            var combat = player.GetComponent<PlayerCombat>();
            for (int i = 0; i < 3; i++)
            {
                var chain = mgr.GetChain(i);
                var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                var row = (RectTransform)rowGo.transform;
                row.SetParent(_chainContainer, false);
                rowGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.9f);
                var le = rowGo.GetComponent<LayoutElement>(); le.preferredHeight = 40f; le.minHeight = 40f;
                var hl = UGuiKit.AddHLayout(row, 10f, new RectOffset(10, 10, 6, 6), TextAnchor.MiddleLeft);

                var keyLabel = UGuiKit.CreateText(row, slotKeys[i], 18, new Color(0.9f, 0.85f, 0.5f), TextAlignmentOptions.Left, FontStyles.Bold);
                UGuiKit.SetHeight(keyLabel, 28f); keyLabel.GetComponent<LayoutElement>().preferredWidth = 26f;

                var skill = combat != null ? combat.GetSkillInSlot(i) : null;
                var skillLabel = UGuiKit.CreateText(row, skill != null ? skill.skillName : "（空）", 14,
                    skill != null ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Left);
                UGuiKit.SetHeight(skillLabel, 28f); skillLabel.GetComponent<LayoutElement>().preferredWidth = 120f;

                string chainStr = (chain != null && (chain.trigger != null || chain.effect != null)) ? BuildChainStr(chain) : "未装配增强链";
                var chainLabel = UGuiKit.CreateText(row, chainStr, 12,
                    (chain != null && (chain.trigger != null || chain.effect != null)) ? new Color(0.7f, 0.75f, 0.85f) : new Color(0.45f, 0.47f, 0.52f),
                    TextAlignmentOptions.Left);
                UGuiKit.SetHeight(chainLabel, 28f); var cle = chainLabel.GetComponent<LayoutElement>(); cle.flexibleWidth = 1f; cle.preferredWidth = 380f;
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

            if (kb.cKey.wasPressedThisFrame)
            {
                if (MainMenu.IsVisible || PauseMenu.IsVisible || ModuleAssemblyUI.IsVisible) return;
                Toggle();
                return;
            }

            if (_visible && kb.escapeKey.wasPressedThisFrame)
                Hide();
        }

        // ==================== Helpers ====================

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
    }
}
