using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.1 树状关卡图 UI（V0.4.6 改 uGUI+TMP）。
    /// 节点横向铺开（左起点 → 右 Boss），连线用旋转 Image 线段绘制，点击/数字键选择下一节点。
    /// 对外保持 Show/HideImmediate/IsVisible。
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

        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _sub;
        private RectTransform _mapArea;
        private RectTransform _linesLayer;
        private TextMeshProUGUI _legend3;
        private TextMeshProUGUI _legend4;

        private readonly Dictionary<TreeNode, RectTransform> _nodeEls = new();
        private readonly Dictionary<TreeNode, RectTransform> _hotkeyEls = new();
        private readonly Dictionary<TreeNode, Vector2> _nodePos = new();
        private readonly List<(TreeNode from, TreeNode to, RectTransform seg, Image img)> _segs = new();

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
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._root != null) _instance._root.SetActive(false);
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
            var canvas = XianTu.UGuiKit.CreateOverlayCanvas("TreeMapUI", 120, transform);
            _root = canvas.gameObject;
            XianTu.UGuiKit.CreateScrim(_root.transform, new Color(0.03f, 0.04f, 0.07f, 0.95f));

            var panel = XianTu.UGuiKit.CreateStretch(_root.transform, "Panel");
            panel.offsetMin = new Vector2(40f, 40f); panel.offsetMax = new Vector2(-40f, -40f);
            var pv = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            pv.spacing = 6f; pv.padding = new RectOffset(10, 10, 10, 10);
            pv.childControlWidth = true; pv.childForceExpandWidth = true; pv.childControlHeight = true; pv.childForceExpandHeight = false;
            pv.childAlignment = TextAnchor.UpperCenter;

            _title = XianTu.UGuiKit.CreateText(panel, "", 26, XianTu.UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            XianTu.UGuiKit.SetHeight(_title, 36f);
            _sub = XianTu.UGuiKit.CreateText(panel, "", 14, new Color(0.65f, 0.68f, 0.75f), TextAlignmentOptions.Center);
            XianTu.UGuiKit.SetHeight(_sub, 22f);

            // 地图区（占据剩余空间）
            var mapGo = new GameObject("MapArea", typeof(RectTransform), typeof(LayoutElement));
            _mapArea = (RectTransform)mapGo.transform;
            _mapArea.SetParent(panel, false);
            var mle = mapGo.GetComponent<LayoutElement>(); mle.flexibleHeight = 1f; mle.minHeight = 300f;

            _linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
            _linesLayer.SetParent(_mapArea, false);
            _linesLayer.anchorMin = Vector2.zero; _linesLayer.anchorMax = Vector2.one; _linesLayer.offsetMin = Vector2.zero; _linesLayer.offsetMax = Vector2.zero;

            var legendRow = XianTu.UGuiKit.CreateRow(panel, 30f, 24f);
            legendRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            legendRow.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            _legend3 = XianTu.UGuiKit.CreateText(legendRow, "", 14, new Color(0.7f, 0.78f, 0.9f), TextAlignmentOptions.Center);
            XianTu.UGuiKit.SetHeight(_legend3, 22f); _legend3.GetComponent<LayoutElement>().preferredWidth = 300f;
            _legend4 = XianTu.UGuiKit.CreateText(legendRow, "", 14, new Color(0.7f, 0.78f, 0.9f), TextAlignmentOptions.Center);
            XianTu.UGuiKit.SetHeight(_legend4, 22f); _legend4.GetComponent<LayoutElement>().preferredWidth = 400f;

            _root.SetActive(false);
        }

        private void Rebuild()
        {
            if (_map == null || _mapArea == null) return;

            foreach (var kv in _nodeEls) if (kv.Value != null) Destroy(kv.Value.gameObject);
            foreach (var kv in _hotkeyEls) if (kv.Value != null) Destroy(kv.Value.gameObject);
            foreach (var s in _segs) if (s.seg != null) Destroy(s.seg.gameObject);
            _nodeEls.Clear();
            _hotkeyEls.Clear();
            _nodePos.Clear();
            _segs.Clear();

            if (_title != null) _title.text = $"· 仙山舆图 · 第 {_map.ActID} 境 ·";
            if (_sub != null)
            {
                string tip = _readOnly ? "按 ESC 关闭（查看模式）" : "点击或按数字键选择下一去处";
                _sub.text = $"已闯 {CountVisited()} / {CountTotal()} 房间        {tip}";
            }
            RefreshLegend();

            var candidates = (!_readOnly && _map.CurrentNode != null) ? _map.CurrentNode.Next : null;

            // 线段（放在 lines 层，位于节点之下）
            foreach (var layer in _map.Floors)
                foreach (var n in layer)
                    foreach (var next in n.Next)
                    {
                        bool active = n == _map.CurrentNode;
                        var segGo = new GameObject("Seg", typeof(RectTransform), typeof(Image));
                        var seg = (RectTransform)segGo.transform;
                        seg.SetParent(_linesLayer, false);
                        seg.anchorMin = new Vector2(0f, 1f); seg.anchorMax = new Vector2(0f, 1f); seg.pivot = new Vector2(0f, 0.5f);
                        var img = segGo.GetComponent<Image>();
                        img.raycastTarget = false;
                        img.color = active ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(0.4f, 0.4f, 0.5f, 0.6f);
                        _segs.Add((n, next, seg, img));
                    }

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

                    var nodeGo = new GameObject("Node", typeof(RectTransform), typeof(Image));
                    var node = (RectTransform)nodeGo.transform;
                    node.SetParent(_mapArea, false);
                    node.anchorMin = new Vector2(0f, 1f); node.anchorMax = new Vector2(0f, 1f); node.pivot = new Vector2(0.5f, 0.5f);
                    node.sizeDelta = new Vector2(NodeSize, NodeSize);
                    nodeGo.GetComponent<Image>().color = fill;

                    if (isCurrent || isCandidate)
                    {
                        var ol = nodeGo.AddComponent<Outline>();
                        ol.effectColor = isCurrent ? new Color(1f, 0.95f, 0.6f, 1f) : new Color(0.5f, 0.8f, 1f, 0.9f);
                        ol.effectDistance = new Vector2(2f, 2f);
                    }

                    var icon = XianTu.UGuiKit.CreateText(node, n.Icon, 24, isCurrent ? Color.black : Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    var irt = (RectTransform)icon.transform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;

                    if (isCandidate)
                    {
                        var btn = nodeGo.AddComponent<Button>();
                        btn.targetGraphic = nodeGo.GetComponent<Image>();
                        var captured = n;
                        btn.onClick.AddListener(() => PickNode(captured));
                    }

                    _nodeEls[n] = node;
                }
            }

            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var hotGo = new GameObject("Hot", typeof(RectTransform));
                    var hrt = (RectTransform)hotGo.transform;
                    hrt.SetParent(_mapArea, false);
                    hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(0f, 1f); hrt.pivot = new Vector2(0.5f, 0.5f);
                    hrt.sizeDelta = new Vector2(40f, 20f);
                    var hot = XianTu.UGuiKit.CreateText(hrt, $"[{i + 1}]", 14, XianTu.UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
                    var ort = (RectTransform)hot.transform; ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
                    _hotkeyEls[candidates[i]] = hrt;
                }
            }
        }

        private void LayoutMap()
        {
            if (_map == null || _mapArea == null) return;
            float w = _mapArea.rect.width;
            float h = _mapArea.rect.height;
            if (w <= 1f || h <= 1f) return;

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
                        el.anchoredPosition = new Vector2(p.x, -p.y);
                    if (_hotkeyEls.TryGetValue(n, out var hot))
                        hot.anchoredPosition = new Vector2(p.x, -(p.y + NodeSize * 0.5f + 14f));
                }
            }

            // 更新线段位置/旋转/长度
            foreach (var s in _segs)
            {
                if (s.seg == null) continue;
                if (!_nodePos.TryGetValue(s.from, out var from) || !_nodePos.TryGetValue(s.to, out var to)) { s.seg.gameObject.SetActive(false); continue; }
                s.seg.gameObject.SetActive(true);
                bool active = s.from == _map.CurrentNode;
                Vector2 a = new Vector2(from.x, -from.y);
                Vector2 b = new Vector2(to.x, -to.y);
                Vector2 dir = b - a;
                float dist = dir.magnitude;
                s.seg.anchoredPosition = a;
                s.seg.sizeDelta = new Vector2(dist, active ? 3f : 2f);
                s.seg.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
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

        private void Update()
        {
            if (!_visible || _map == null) return;

            LayoutMap();   // 每帧重排，兼容分辨率变化（节点数少，开销可忽略）

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
            if (_root != null) _root.SetActive(false);
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
