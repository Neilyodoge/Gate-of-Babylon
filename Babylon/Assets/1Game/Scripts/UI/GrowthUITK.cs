using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 御灵·成长页（v0.6 阶段C · UI Toolkit）：一棵"化身成长树"——系精通节点 + 化身天赋，统一花「灵力」。
    /// 头部显示 本体境界 / 灵力。结构 Resources/UI/GrowthUITK.uxml。
    /// 由洞府"悟道蒲团"交互打开（入口后续可迁到选化身页）。Show/Hide/IsVisible。
    /// </summary>
    public class GrowthUITK : MonoBehaviour
    {
        private static GrowthUITK _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _header;
        private ScrollView _content;

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._visible = true;
            _instance.Rebuild();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("GrowthUITK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GrowthUITK>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/GrowthUITK");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 12f;

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _header = root.Q<Label>("header");
            _content = root.Q<ScrollView>("content");
            if (_content != null)
            {
                _content.mode = ScrollViewMode.Vertical;
                _content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            var close = root.Q<Button>("close");
            if (close != null) close.clicked += Hide;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        private static SpiritRootType CurrentAvatar()
        {
            var p = PlayerController.Instance;
            var ctrl = p != null ? p.GetComponent<SpiritRootController>() : null;
            if (ctrl != null && ctrl.CurrentRoot != SpiritRootType.None) return ctrl.CurrentRoot;
            return SpiritRootType.Metal;
        }

        private void Rebuild()
        {
            if (_content == null) return;
            _content.Clear();

            var avatar = CurrentAvatar();
            var cult = CultivationSystem.Instance;
            var insight = InsightSystem.Instance;
            var def = SpiritRootRegistry.Get(avatar);
            string avatarName = def != null ? def.name : avatar.ToString();

            if (_header != null)
                _header.text = $"化身：{avatarName}　·　本体境界：{cult.CurrentRealmName}　·　灵力：{insight.PermanentInsight}";

            // ── 系精通（本命系 · 根基分出两条分支，渐进解锁）──
            string sysName = SystemMasteryRegistry.SystemName(SystemMasteryRegistry.BodySystem(avatar));
            _content.Add(Section($"系精通 · {sysName}（花灵力 · 根基分两路）"));

            EnsureFirstBranchUnlocked(avatar);
            var branchSet = new System.Collections.Generic.HashSet<string>(SaveSystem.Instance.Data.unlockedGrowthBranches);
            string lastBranch = null;
            foreach (var node in SystemMasteryRegistry.NodesFor(avatar))
            {
                if (node.tier > 0 && node.branchLabel != lastBranch)
                {
                    lastBranch = node.branchLabel;
                    string branchKey = BranchKey(avatar, node.branchLabel);
                    if (!branchSet.Contains(branchKey))
                    {
                        _content.Add(LockedBranchHint(node.branchLabel));
                        continue;
                    }
                    _content.Add(BranchHeader("◈ " + node.branchLabel));
                }
                if (node.tier > 0)
                {
                    string branchKey = BranchKey(avatar, node.branchLabel);
                    if (!branchSet.Contains(branchKey)) continue;
                }
                _content.Add(MakeMasteryRow(node));
            }

            // ── 化身天赋（花灵力）──
            _content.Add(Section("化身天赋（花灵力）"));
            var unlocked = new System.Collections.Generic.HashSet<string>(SaveSystem.Instance.Data.unlockedTalentIds);
            int talentCount = 0;
            foreach (var entry in PermanentTalentRegistry.AllTalents)
            {
                if (entry.reward.applicableRoot != avatar) continue;
                _content.Add(MakeTalentRow(entry, unlocked.Contains(entry.reward.id)));
                talentCount++;
            }
            if (talentCount == 0)
                _content.Add(Hint("（该化身暂无可解锁天赋）"));
        }

        private VisualElement MakeMasteryRow(MasteryNode node)
        {
            bool allocated = SystemMasterySystem.IsAllocated(node.id);
            string prefix = node.tier switch { 0 => "● ", 1 => "├ ", _ => "└ " };
            var row = MakeRowBase(prefix + node.displayName, node.description, allocated);
            if (node.tier > 0) row.style.marginLeft = node.tier * 16;

            if (allocated)
            {
                var done = new Label("✓ 已点亮");
                done.AddToClassList("gr-done");
                row.Add(done);
            }
            else
            {
                bool can = SystemMasterySystem.CanAllocate(node, out string reason);
                var btn = new Button(() => { if (SystemMasterySystem.Allocate(node.id)) Rebuild(); })
                { text = can ? $"点亮（{node.cost} 灵力）" : reason };
                btn.AddToClassList("gr-btn");
                btn.SetEnabled(can);
                row.Add(btn);
            }
            return row;
        }

        private VisualElement MakeTalentRow(PermanentTalentRegistry.TalentEntry entry, bool unlocked)
        {
            var r = entry.reward;
            var row = MakeRowBase(r.displayName, r.description, unlocked);
            row.Q<Label>(className: "gr-row__name").style.color = r.displayColor;

            if (unlocked)
            {
                var done = new Label("✓ 已悟");
                done.AddToClassList("gr-done");
                row.Add(done);
            }
            else
            {
                int cost = entry.insightCost;
                bool can = InsightSystem.Instance.PermanentInsight >= cost;
                var btn = new Button(() => { if (TryUnlockTalent(entry)) Rebuild(); })
                { text = $"参悟（{cost} 灵力）" };
                btn.AddToClassList("gr-btn");
                btn.SetEnabled(can);
                row.Add(btn);
            }
            return row;
        }

        private static bool TryUnlockTalent(PermanentTalentRegistry.TalentEntry entry)
        {
            if (!InsightSystem.Instance.SpendPermanentInsight(entry.insightCost)) return false;
            SaveSystem.Instance.Data.unlockedTalentIds.Add(entry.reward.id);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[成长] 参悟天赋：{entry.reward.displayName}（耗 {entry.insightCost} 灵力）</color>");
            return true;
        }

        private static VisualElement MakeRowBase(string name, string desc, bool isOn)
        {
            var row = new VisualElement();
            row.AddToClassList("gr-row");
            if (!isOn) row.AddToClassList("gr-row--locked");
            var n = new Label(name);
            n.AddToClassList("gr-row__name");
            row.Add(n);
            var d = new Label(desc);
            d.AddToClassList("gr-row__desc");
            row.Add(d);
            return row;
        }

        private static Label Section(string text)
        {
            var l = new Label(text);
            l.AddToClassList("gr-section");
            return l;
        }

        private static Label BranchHeader(string text)
        {
            var l = new Label(text);
            l.AddToClassList("gr-branch");
            return l;
        }

        private static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("gr-row__desc");
            return l;
        }

        private static string BranchKey(SpiritRootType avatar, string branchLabel)
            => $"{avatar}_{branchLabel}";

        /// <summary>首次打开时自动解锁每化身的第一条分支。</summary>
        private static void EnsureFirstBranchUnlocked(SpiritRootType avatar)
        {
            var save = SaveSystem.Instance.Data;
            string firstBranch = null;
            foreach (var node in SystemMasteryRegistry.NodesFor(avatar))
            {
                if (node.tier == 1 && !string.IsNullOrEmpty(node.branchLabel))
                {
                    firstBranch = node.branchLabel;
                    break;
                }
            }
            if (firstBranch == null) return;
            string key = BranchKey(avatar, firstBranch);
            if (!save.unlockedGrowthBranches.Contains(key))
            {
                save.unlockedGrowthBranches.Add(key);
                SaveSystem.Instance.Save();
            }
        }

        private static VisualElement LockedBranchHint(string branchLabel)
        {
            var row = new VisualElement();
            row.AddToClassList("gr-row");
            row.AddToClassList("gr-row--locked");
            row.style.marginLeft = 16;
            var icon = new Label("🔒");
            icon.style.fontSize = 16;
            icon.style.marginRight = 6;
            row.Add(icon);
            var label = new Label($"{branchLabel}（需机缘/成就解锁）");
            label.AddToClassList("gr-row__desc");
            label.style.color = new Color(0.5f, 0.5f, 0.55f);
            row.Add(label);
            return row;
        }

        /// <summary>
        /// 外部调用：解锁某化身的某分支（用于机缘事件 / 成就回调）。
        /// 示例：GrowthUITK.UnlockBranch(SpiritRootType.Metal, "御金·铁壁");
        /// </summary>
        public static void UnlockBranch(SpiritRootType avatar, string branchLabel)
        {
            var save = SaveSystem.Instance.Data;
            string key = BranchKey(avatar, branchLabel);
            if (save.unlockedGrowthBranches.Contains(key)) return;
            save.unlockedGrowthBranches.Add(key);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[成长] 分支解锁：{avatar} · {branchLabel}</color>");
        }
    }
}
