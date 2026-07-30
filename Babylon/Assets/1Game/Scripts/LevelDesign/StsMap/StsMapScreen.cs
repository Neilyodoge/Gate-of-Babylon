using System;
using Map;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// V0.4.1（接入 silverua 杀戮尖塔地图）：运行时地图屏。
    ///
    /// silverua 的 UI 变体预制体（MapObjectsUI Variant）里 <c>MapViewUI</c> 的两个 ScrollRect
    /// 引用原本靠场景装配；本项目不走它的示例场景，改为在此用代码搭一个全屏 Canvas + 两个
    /// ScrollRect，实例化该预制体并回填引用，再负责显隐与「玩家点节点 → 回调房型」。
    ///
    /// 由 <see cref="SilveruaMapProvider"/> 持有，跨场景常驻（DontDestroyOnLoad）。
    /// </summary>
    public class StsMapScreen : MonoBehaviour
    {
        private const string PrefabResource = "StsMapObjectsUI"; // 位于任意 Resources 目录下

        private GameObject _canvasGo;
        private MapManager _mapManager;
        private MapViewUI _view;
        private bool _built;
        private bool _generatedForRealm;

        private Action<NodeType> _onPicked;

        public bool IsShowing => _canvasGo != null && _canvasGo.activeSelf;

        /// <summary>当前已选节点是否还有后继；地图尚未生成/尚未选点时返回 true。</summary>
        public bool CurrentNodeHasNext
        {
            get
            {
                EnsureBuilt();
                var map = _mapManager != null ? _mapManager.CurrentMap : null;
                if (map == null || map.path == null || map.path.Count == 0) return true;
                var node = map.GetNode(map.path[map.path.Count - 1]);
                return node != null && node.outgoing.Count > 0;
            }
        }

        /// <summary>地图层数（= 每境房间数脚手架长度）。首次访问会构建地图屏。</summary>
        public int LayerCount
        {
            get
            {
                EnsureBuilt();
                if (_mapManager != null && _mapManager.config != null && _mapManager.config.layers != null)
                    return _mapManager.config.layers.Count;
                return 12;
            }
        }

        public static StsMapScreen Create()
        {
            var go = new GameObject("StsMapScreen");
            DontDestroyOnLoad(go);
            return go.AddComponent<StsMapScreen>();
        }

        /// <summary>换境/开局：下次 Show 时重生成本境地图（复位分叉图与路径）。</summary>
        public void ResetForRealm()
        {
            _generatedForRealm = false;
        }

        /// <summary>
        /// 按 Act（1-based）选择地图配置：从 <c>MapViewUI.allMapConfigs</c> 取第 (act-1) 个，
        /// 越界则取最后一个。目前列表通常只有一个 DefaultMapConfig（各境共用）；
        /// 后续要按 Act 分化地图，只需把对应 <c>MapConfig</c> 追加进该列表即可，无需改代码。
        /// </summary>
        public void SetActConfig(int act)
        {
            EnsureBuilt();
            if (_mapManager == null || _view == null) return;
            var cfgs = _view.allMapConfigs;
            if (cfgs == null || cfgs.Count == 0) return;
            int idx = Mathf.Clamp(act - 1, 0, cfgs.Count - 1);
            _mapManager.config = cfgs[idx];
        }

        /// <summary>显示地图并等待玩家点选下一节点；点定后隐藏并回调节点类型。</summary>
        public void Show(Action<NodeType> onPicked)
        {
            EnsureBuilt();
            _onPicked = onPicked;

            _canvasGo.SetActive(true);

            if (!_generatedForRealm)
            {
                _mapManager.GenerateNewMap();
                _generatedForRealm = true;
            }

            if (MapPlayerTracker.Instance != null)
                MapPlayerTracker.Instance.Locked = false;
        }

        public void Hide()
        {
            if (_canvasGo != null)
                _canvasGo.SetActive(false);
        }

        private void OnNodeEntered(MapNode node)
        {
            if (node == null) return;
            var cb = _onPicked;
            _onPicked = null;
            Hide();
            cb?.Invoke(node.Node.nodeType);
        }

        private void OnEnable()  { MapPlayerTracker.NodeEntered += OnNodeEntered; }
        private void OnDisable() { MapPlayerTracker.NodeEntered -= OnNodeEntered; }

        // ------------------------------------------------------------
        // 构建
        // ------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (_built) return;

            EnsureEventSystem();

            _canvasGo = new GameObject("StsMapCanvas");
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            // 半透明背景 + 拦截身后点击
            var backdrop = CreateFullRect("Backdrop", _canvasGo.transform);
            var bdImg = backdrop.gameObject.AddComponent<Image>();
            bdImg.color = new Color(0.02f, 0.02f, 0.04f, 0.88f);
            bdImg.raycastTarget = true;

            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(_canvasGo.transform, false);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -30);
            trt.sizeDelta = new Vector2(800, 70);
            var tmp = title.AddComponent<TextMeshProUGUI>();
            tmp.text = "择路 · 点选下一处秘境";
            tmp.font = UGuiKit.CjkFont;
            tmp.fontSize = 40;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.8f);
            tmp.raycastTarget = false;

            var scrollH = CreateScrollRect("ScrollH", true, false);
            var scrollV = CreateScrollRect("ScrollV", false, true);

            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError($"[StsMapScreen] 找不到地图预制体 Resources/{PrefabResource}");
                _built = true;
                return;
            }

            var mapGo = Instantiate(prefab, _canvasGo.transform, false);
            mapGo.name = "MapObjectsUI";
            _mapManager = mapGo.GetComponentInChildren<MapManager>(true);
            _view = mapGo.GetComponentInChildren<MapViewUI>(true);
            if (_view != null)
                _view.SetScrollRects(scrollH, scrollV);

            _built = true;
            _canvasGo.SetActive(false);
        }

        private ScrollRect CreateScrollRect(string name, bool horizontal, bool vertical)
        {
            var rt = CreateFullRect(name, _canvasGo.transform);
            rt.offsetMin = new Vector2(40, 40);
            rt.offsetMax = new Vector2(-40, -110);

            var vpImg = rt.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.001f); // 近乎透明，仅用于接收拖拽
            rt.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(rt, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(1000, 1000);

            var sr = rt.gameObject.AddComponent<ScrollRect>();
            sr.content = crt;
            sr.viewport = rt;
            sr.horizontal = horizontal;
            sr.vertical = vertical;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.1f;
            sr.inertia = true;
            sr.scrollSensitivity = 20f;

            rt.gameObject.SetActive(false);
            return sr;
        }

        private static RectTransform CreateFullRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindObjectOfType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            // 项目使用新输入系统：优先挂 InputSystemUIInputModule
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t);
            else es.AddComponent<StandaloneInputModule>();
        }
    }
}
