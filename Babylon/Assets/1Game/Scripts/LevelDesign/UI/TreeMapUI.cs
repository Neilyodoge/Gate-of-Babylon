using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.1 树状关卡图 UI（v0.6 改 UI Toolkit）。
    /// 节点横向铺开（左起点 → 右 Boss），连线用 Painter2D 自绘，点击/数字键选择下一节点。
    /// 结构 Resources/UI/TreeMapUI.uxml，样式同名 uss。对外保持 Show/HideImmediate/IsVisible。
    /// readOnly=false：选择模式（推进 CurrentNode + 回调）；readOnly=true：查看模式（仅 ESC 关闭）。
    /// </summary>
    public class TreeMapUI : MonoBehaviour
    {
        private static TreeMapUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private bool _readOnly;
        private TreeMap _map;
        private Action<TreeNode> _onNodeChosen;
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _title;
        private Label _sub;
        private VisualElement _mapArea;
        private VisualElement _lines;
        private Label _legend3;
        private Label _legend4;

        private readonly Dictionary<TreeNode, VisualElement> _nodeEls = new();
        private readonly Dictionary<TreeNode, Label> _hotkeyEls = new();
        private readonly Dictionary<TreeNode, Vector2> _nodePos = new();

        private const float NodeSize = 56f;
        private const float PadLeft = 70f;
        private const float PadTop = 24f;
        private const float PadBottom = 70f;

        public static void Show(TreeMap map, Action<TreeNode> onChosen, bool readOnly = false)
        {
            if (map == null) return;
            EnsureInstance();
            if (_instance == null) return;

            _instance._map = map;
            _instance._onNodeChosen = onChosen;
            _instance._readOnly = readOnly;
            _instance._visible = true;
            _instance._prevLock = UnityEngine.Cursor.lockState;
            _instance._prevVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _instance.Rebuild();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
            if (!_instance._readOnly)
            {
                UnityEngine.Cursor.lockState = _instance._prevLock;
                UnityEngine.Cursor.visible = _instance._prevVisible;
            }
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("TreeMapUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TreeMapUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/TreeMapUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 11f;
            XianTu.ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _title = root.Q<Label>("title");
            _sub = root.Q<Label>("sub");
            _mapArea = root.Q<VisualElement>("mapArea");
            _lines = root.Q<VisualElement>("lines");
            _legend3 = root.Q<Label>("legend3");
            _legend4 = root.Q<Label>("legend4");

            if (_lines != null)
            {
                _lines.pickingMode = PickingMode.Ignore;
                _lines.generateVisualContent += OnDrawLines;
            }
            if (_mapArea != null)
                _mapArea.RegisterCallback<GeometryChangedEvent>(_ => LayoutMap());

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (_map == null || _mapArea == null) return;

            // 清掉旧节点/热键（保留 lines 层）
            foreach (var kv in _nodeEls) kv.Value.RemoveFromHierarchy();
            foreach (var kv in _hotkeyEls) kv.Value.RemoveFromHierarchy();
            _nodeEls.Clear();
            _hotkeyEls.Clear();
            _nodePos.Clear();

            if (_title != null) _title.text = $"· 仙山舆图 · 第 {_map.ActID} 境 ·";
            if (_sub != null)
            {
                string tip = _readOnly ? "按 ESC 关闭（查看模式）" : "点击或按数字键选择下一去处";
                _sub.text = $"已闯 {CountVisited()} / {CountTotal()} 房间        {tip}";
            }
            RefreshLegend();

            // 候选（仅选择模式可点）
            var candidates = (!_readOnly && _map.CurrentNode != null) ? _map.CurrentNode.Next : null;

            foreach (var layer in _map.Floors)
            {
                foreach (var n in layer)
                {
                    bool isCurrent = n == _map.CurrentNode;
                    bool isCandidate = candidates != null && candidates.Contains(n);
                    bool isVisited = n.Visited;

                    Color fill = n.Color;
                    if (!isCandidate && !isCurrent && !isVisited)
                        fill = new Color(fill.r * 0.4f, fill.g * 0.4f, fill.b * 0.4f, 1f);
                    if (isCurrent) fill = new Color(1f, 0.9f, 0.5f);

                    var node = new VisualElement();
                    node.AddToClassList("tm-node");
                    if (isCurrent) node.AddToClassList("tm-node--current");
                    else if (isCandidate) node.AddToClassList("tm-node--candidate");
                    node.style.backgroundColor = fill;

                    var icon = new Label(n.Icon);
                    icon.AddToClassList("tm-node-icon");
                    icon.style.color = isCurrent ? Color.black : Color.white;
                    node.Add(icon);

                    if (isCandidate)
                    {
                        var captured = n;
                        node.RegisterCallback<ClickEvent>(_ => PickNode(captured));
                    }

                    _mapArea.Add(node);
                    _nodeEls[n] = node;
                }
            }

            // 候选热键标签（按 CurrentNode.Next 顺序，与数字键一致）
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var hot = new Label($"[{i + 1}]");
                    hot.AddToClassList("tm-hot");
                    _mapArea.Add(hot);
                    _hotkeyEls[candidates[i]] = hot;
                }
            }

            LayoutMap();
        }

        private void LayoutMap()
        {
            if (_map == null || _mapArea == null) return;
            float w = _mapArea.resolvedStyle.width;
            float h = _mapArea.resolvedStyle.height;
            if (w <= 1f || h <= 1f) return;   // 布局尚未就绪，等 GeometryChangedEvent

            float floorGap = Mathf.Max(120f, (w - PadLeft * 2f) / Mathf.Max(1, _map.MaxFloor - 1));

            for (int f = 0; f < _map.Floors.Count; f++)
            {
                var layer = _map.Floors[f];
                for (int i = 0; i < layer.Count; i++)
                {
                    var n = layer[i];
                    Vector2 p = NodePos(w, h, floorGap, layer.Count, f, i);
                    _nodePos[n] = p;
                    if (_nodeEls.TryGetValue(n, out var el))
                    {
                        el.style.left = p.x - NodeSize * 0.5f;
                        el.style.top = p.y - NodeSize * 0.5f;
                    }
                    if (_hotkeyEls.TryGetValue(n, out var hot))
                    {
                        hot.style.left = p.x - 20f;
                        hot.style.top = p.y + NodeSize * 0.5f + 4f;
                    }
                }
            }

            if (_lines != null) _lines.MarkDirtyRepaint();
        }

        private Vector2 NodePos(float w, float h, float floorGap, int layerCount, int floor, int idx)
        {
            float x = PadLeft + floor * floorGap;
            float yCenter = PadTop + (h - PadTop - PadBottom) * 0.5f;
            float ySpread = Mathf.Min(h - PadTop - PadBottom * 1.5f, layerCount * 84f);
            float y = layerCount == 1
                ? yCenter
                : yCenter - ySpread * 0.5f + idx * ySpread / Mathf.Max(1, layerCount - 1);
            return new Vector2(x, y);
        }

        private void OnDrawLines(MeshGenerationContext ctx)
        {
            if (_map == null || _nodePos.Count == 0) return;
            var p = ctx.painter2D;

            foreach (var layer in _map.Floors)
            {
                foreach (var n in layer)
                {
                    if (!_nodePos.TryGetValue(n, out var from)) continue;
                    bool active = n == _map.CurrentNode;
                    foreach (var next in n.Next)
                    {
                        if (!_nodePos.TryGetValue(next, out var to)) continue;
                        p.strokeColor = active ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(0.4f, 0.4f, 0.5f, 0.6f);
                        p.lineWidth = active ? 3f : 2f;
                        p.BeginPath();
                        p.MoveTo(from);
                        p.LineTo(to);
                        p.Stroke();
                    }
                }
            }
        }

        private void Update()
        {
            if (!_visible || _map == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_readOnly)
            {
                if (kb.escapeKey.wasPressedThisFrame) HideImmediate();
                return;
            }

            if (_map.CurrentNode == null) return;
            var next = _map.CurrentNode.Next;
            if (next == null || next.Count == 0) return;
            int n = Mathf.Min(next.Count, 9);
            for (int i = 0; i < n; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                {
                    PickNode(next[i]);
                    return;
                }
            }
        }

        private void PickNode(TreeNode node)
        {
            _visible = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = _prevLock;
            UnityEngine.Cursor.visible = _prevVisible;
            _map.CurrentNode = node;
            node.Visited = true;
            _onNodeChosen?.Invoke(node);
        }

        private int CountVisited()
        {
            int c = 0;
            foreach (var layer in _map.Floors) foreach (var n in layer) if (n.Visited) c++;
            return c;
        }

        private int CountTotal()
        {
            int c = 0;
            foreach (var layer in _map.Floors) c += layer.Count;
            return c;
        }

        private void RefreshLegend()
        {
            var h = PlayerStateHooks.Instance;
            if (h == null) return;
            if (_legend3 != null) _legend3.text = $"道心：{h.Daoxin} ({h.DaoxinState})";
            if (_legend4 != null) _legend4.text = $"因果：{h.KarmaDebt} / 寿元：{h.Lifespan} 年";
        }
    }
}
