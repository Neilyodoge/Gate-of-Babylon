using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 房间出口触发器 —— 通用组件
    /// 玩家走进触发区域后显示提示，按F触发离开回调
    /// 必须走进Trigger范围才能交互，防止远距离按F跳层
    /// </summary>
    public class RoomExitTrigger : MonoBehaviour, IInteractable
    {
        private System.Action _onExit;
        private bool _playerInRange;
        private bool _triggered;
        private GameObject _promptUI;
        private float _enterDelay = 0.3f; // 进入触发器后短暂延迟才能按F（防止冲进去瞬间触发）
        private float _enterTimer;

        // ===== IInteractable：参与统一 F 交互路由 =====
        // 出口优先级最低 —— 走到出口时如果旁边还有可拾取物，先满足拾取
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 5;
        public bool IsInteractionAvailable =>
            !_triggered && _playerInRange && _enterTimer >= _enterDelay;
        public bool IsRoutedActive { get; set; }

        public void Initialize(System.Action onExitCallback)
        {
            _onExit = onExitCallback;
        }

        private void Update()
        {
            if (_triggered || !_playerInRange) return;

            _enterTimer += Time.deltaTime;
            if (_enterTimer < _enterDelay) return;

            // 同步提示显示状态：被路由器选中时才显示「按 F 继续前进」
            if (_promptUI != null)
            {
                bool wantShown = IsRoutedActive;
                if (_promptUI.activeSelf != wantShown)
                    _promptUI.SetActive(wantShown);
            }

            // 仅当被选中时才响应 F（避免与拾取物等同时触发）
            if (!IsRoutedActive) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                _triggered = true;
                HidePrompt();
                _onExit?.Invoke();
            }
        }

        private void OnTriggerEnter(Collider other) => TryEnter(other);

        private void OnTriggerStay(Collider other) => TryEnter(other);

        private void TryEnter(Collider other)
        {
            if (_triggered || _playerInRange) return;
            if (!other.CompareTag("Player")) return;

            _playerInRange = true;
            _enterTimer = 0f;
            InteractionRouter.Register(this);
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!_playerInRange) return;

            _playerInRange = false;
            _enterTimer = 0f;
            InteractionRouter.Unregister(this);
            HidePrompt();
        }

        private void ShowPrompt()
        {
            if (_promptUI != null) return;

            var canvasGo = new GameObject("ExitPromptCanvas");
            canvasGo.transform.SetParent(transform);
            canvasGo.transform.localPosition = new Vector3(0, 3.5f, 0);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280, 50);
            rt.localScale = Vector3.one * 0.03f;

            // 背景
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.1f, 0.2f, 0.8f);

            // 提示文字
            var textGo = new GameObject("PromptText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "按 [F] 继续前进";
            text.fontSize = 16;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(0.5f, 0.9f, 1f);
            text.alignment = TextAnchor.MiddleCenter;

            _promptUI = canvasGo;

            // 面向相机
            canvasGo.AddComponent<BillboardUI>();
        }

        private void HidePrompt()
        {
            if (_promptUI != null)
            {
                Destroy(_promptUI);
                _promptUI = null;
            }
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
            HidePrompt();
        }
    }
}
