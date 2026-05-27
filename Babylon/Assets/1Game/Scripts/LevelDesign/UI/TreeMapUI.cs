using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.1 树状关卡图 UI（杀戮尖塔式分支选路）。
    /// 简化版：屏幕中央展示节点 + 连线，玩家点击或数字键 1/2/3/4 选择下一节点。
    /// 数据驱动：依赖 TreeMap 数据，独立于业务。
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

        /// <summary>
        /// 显示舆图。
        /// readOnly = false：选择模式（玩家点节点会推进 CurrentNode 并触发 onChosen 回调，进门时使用）。
        /// readOnly = true：查看模式（候选节点不可点击，可按 ESC 关闭，DebugConsole 使用）。
        /// </summary>
        public static void Show(TreeMap map, Action<TreeNode> onChosen, bool readOnly = false)
        {
            if (map == null) return;
            if (_instance == null)
            {
                var go = new GameObject("TreeMapUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TreeMapUI>();
            }
            _instance._map = map;
            _instance._onNodeChosen = onChosen;
            _instance._readOnly = readOnly;
            _instance._visible = true;
            _instance._prevLock = Cursor.lockState;
            _instance._prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void HideImmediate()
        {
            if (_instance != null)
            {
                _instance._visible = false;
                if (!_instance._readOnly)
                {
                    Cursor.lockState = _instance._prevLock;
                    Cursor.visible = _instance._prevVisible;
                }
            }
        }

        private void Update()
        {
            if (!_visible || _map == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // 查看模式：仅 ESC 关闭
            if (_readOnly)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                    HideImmediate();
                return;
            }

            if (_map.CurrentNode == null) return;
            var next = _map.CurrentNode.Next;
            if (next == null || next.Count == 0) return;

            // 数字键快捷选择 1~9
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
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            _map.CurrentNode = node;
            node.Visited = true;
            _onNodeChosen?.Invoke(node);
        }

        private void OnGUI()
        {
            if (!_visible || _map == null) return;

            // 半透明遮罩
            var bg = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = bg;

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.45f, 1f) }
            };
            GUI.Label(new Rect(0, 24f, Screen.width, 36f), $"· 仙山舆图 · 第 {_map.ActID} 境 ·", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.8f, 1f) }
            };
            string subTip = _readOnly
                ? $"已闯：{CountVisited()} / {CountTotal()} 房间        按 ESC 关闭（查看模式）"
                : $"已闯：{CountVisited()} / {CountTotal()} 房间        点击或按数字键选择下一去处";
            GUI.Label(new Rect(0, 60f, Screen.width, 20f), subTip, subStyle);

            // 节点布局：横向铺开（左→右 = 起点→Boss）
            float padLeft = 80f;
            float padTop = 110f;
            float floorGap = Mathf.Max(120f, (Screen.width - padLeft * 2) / Mathf.Max(1, _map.MaxFloor - 1));
            float nodeSize = 56f;

            // 先画连线
            for (int f = 0; f < _map.Floors.Count; f++)
            {
                var layer = _map.Floors[f];
                for (int i = 0; i < layer.Count; i++)
                {
                    var n = layer[i];
                    Vector2 pFrom = NodePos(padLeft, padTop, floorGap, layer.Count, f, i);
                    foreach (var next in n.Next)
                    {
                        var nextLayer = _map.Floors[next.Floor];
                        Vector2 pTo = NodePos(padLeft, padTop, floorGap, nextLayer.Count, next.Floor, next.IndexInFloor);
                        bool active = n == _map.CurrentNode;
                        DrawLine(pFrom, pTo, active ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(0.4f, 0.4f, 0.5f, 0.6f),
                                 active ? 3f : 2f);
                    }
                }
            }

            // 再画节点
            int hotkeyIdx = 0;
            for (int f = 0; f < _map.Floors.Count; f++)
            {
                var layer = _map.Floors[f];
                for (int i = 0; i < layer.Count; i++)
                {
                    var n = layer[i];
                    Vector2 p = NodePos(padLeft, padTop, floorGap, layer.Count, f, i);

                    bool isCurrent = n == _map.CurrentNode;
                    bool isCandidate = _map.CurrentNode != null && _map.CurrentNode.Next.Contains(n);
                    bool isVisited = n.Visited;

                    var rect = new Rect(p.x - nodeSize * 0.5f, p.y - nodeSize * 0.5f, nodeSize, nodeSize);

                    Color fill = n.Color;
                    if (!isCandidate && !isCurrent && !isVisited) fill = new Color(fill.r * 0.4f, fill.g * 0.4f, fill.b * 0.4f, 1f);
                    if (isCurrent) fill = new Color(1f, 0.9f, 0.5f);

                    // 节点圆形底
                    GUI.color = fill;
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    // 边框
                    GUI.color = isCurrent ? Color.white : new Color(0f, 0f, 0f, 0.6f);
                    DrawRectBorder(rect, isCurrent ? 3f : 1.5f);
                    GUI.color = bg;

                    var labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = isCurrent ? Color.black : Color.white }
                    };
                    GUI.Label(rect, n.Icon, labelStyle);

                    // 候选节点 → 数字键提示 + 可点击按钮（查看模式不显示热键不允许点）
                    if (isCandidate && !_readOnly)
                    {
                        hotkeyIdx++;
                        var hotStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 13,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(1f, 0.9f, 0.4f, 1f) }
                        };
                        GUI.Label(new Rect(rect.x, rect.y + rect.height + 4f, rect.width, 18f), $"[{hotkeyIdx}]", hotStyle);

                        if (GUI.Button(rect, "", GUIStyle.none))
                        {
                            PickNode(n);
                            return;
                        }
                    }
                }
            }

            // 图例
            DrawLegend();
        }

        private Vector2 NodePos(float padLeft, float padTop, float floorGap, int layerCount, int floor, int idx)
        {
            float x = padLeft + floor * floorGap;
            float yCenter = padTop + (Screen.height - padTop - 80f) * 0.5f;
            float ySpread = Mathf.Min(Screen.height - padTop - 160f, layerCount * 80f);
            float y = layerCount == 1 ? yCenter : yCenter - ySpread * 0.5f + idx * ySpread / Mathf.Max(1, layerCount - 1);
            return new Vector2(x, y);
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

        // ------------------------------------------------------------
        // 绘制工具
        // ------------------------------------------------------------

        private static Texture2D _lineTex;

        private static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            if (_lineTex == null) _lineTex = Texture2D.whiteTexture;
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(from, to);

            Matrix4x4 savedMat = GUI.matrix;
            Color savedColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), _lineTex);
            GUI.color = savedColor;
            GUI.matrix = savedMat;
        }

        private static void DrawRectBorder(Rect r, float thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
        }

        private void DrawLegend()
        {
            float x = 16f, y = Screen.height - 110f, w = 220f, h = 92f;
            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = bg;

            var s = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            GUI.Label(new Rect(x + 8f, y + 4f, w, 20f), "战 战斗  精 精英  商 商店", s);
            GUI.Label(new Rect(x + 8f, y + 22f, w, 20f), "?  事件  王 Boss", s);
            GUI.Label(new Rect(x + 8f, y + 44f, w, 20f), $"道心：{PlayerStateHooks.Instance.Daoxin} ({PlayerStateHooks.Instance.DaoxinState})", s);
            GUI.Label(new Rect(x + 8f, y + 62f, w, 20f), $"因果：{PlayerStateHooks.Instance.KarmaDebt} / 寿元：{PlayerStateHooks.Instance.Lifespan} 年", s);
        }
    }
}
