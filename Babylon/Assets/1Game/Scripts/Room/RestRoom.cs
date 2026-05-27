using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 休息房间 —— 恢复生命值
    /// </summary>
    public class RestRoom : MonoBehaviour
    {
        private int _roomIndex;
        private GameObject _roomVisuals;
        private GameObject _hintCanvas;
        private bool _healed;

        public float RoomWidth => 18f;
        public float RoomDepth => 18f;

        public void Initialize(int roomIndex)
        {
            _roomIndex = roomIndex;
            BuildRoom();
        }

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, 18f, 18f, _roomIndex);

            // 中央灵泉（圆柱体 + 发光）
            var spring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spring.name = "SpiritSpring";
            spring.transform.SetParent(transform);
            spring.transform.localPosition = new Vector3(0, 0.15f, 0);
            spring.transform.localScale = new Vector3(3f, 0.15f, 3f);

            var springCol = spring.GetComponent<Collider>();
            if (springCol != null) Destroy(springCol);

            var springRend = spring.GetComponent<Renderer>();
            if (springRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.2f, 0.7f, 1f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 1f) * 2f);
                springRend.material = mat;
            }

            // 触发器
            var triggerGo = new GameObject("SpringTrigger");
            triggerGo.transform.SetParent(spring.transform);
            triggerGo.transform.localPosition = Vector3.zero;
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 8f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<SpringTrigger>();
            trigger.Initialize(this);

            // 提示文字
            _hintCanvas = new GameObject("HintCanvas");
            _hintCanvas.transform.SetParent(spring.transform);
            _hintCanvas.transform.localPosition = new Vector3(0, 3f, 0);
            var c = _hintCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 0.5f);
            _hintCanvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(_hintCanvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "按 [F] 沐浴灵泉";
            text.fontSize = 18;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(0.5f, 0.9f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hintCanvas.AddComponent<BillboardUI>();
            _hintCanvas.SetActive(false);

            // 出口触发器（房间北侧）
            CreateExitTrigger();
        }

        /// <summary>由 SpringTrigger 调用：控制灵泉头顶提示的显隐</summary>
        public void SetHintVisible(bool visible)
        {
            if (_hintCanvas != null) _hintCanvas.SetActive(visible && !_healed);
        }

        /// <summary>在房间北侧创建出口触发器</summary>
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

            // 出口视觉标记（发光柱）
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

        public void HealPlayer()
        {
            if (_healed) return;
            _healed = true;

            // 销毁头顶提示
            if (_hintCanvas != null)
            {
                Destroy(_hintCanvas);
                _hintCanvas = null;
            }

            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                float healAmount = stats.maxHp * 0.5f; // 恢复50%最大生命
                stats.Heal(healAmount);

                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = stats.currentHp,
                    MaxHp = stats.maxHp
                });

                Debug.Log($"<color=cyan>灵泉恢复了 {healAmount:F0} 点生命</color>");
            }
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }
    }

    /// <summary>灵泉触发器 —— 手动按 F 治疗（参与 InteractionRouter 路由）</summary>
    public class SpringTrigger : MonoBehaviour, IInteractable
    {
        private RestRoom _room;
        private bool _used;
        private bool _playerInRange;

        // IInteractable
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 30; // 高于灵物拾取 20
        public bool IsInteractionAvailable => !_used && _playerInRange;
        public bool IsRoutedActive { get; set; }

        public void Initialize(RestRoom room)
        {
            _room = room;
        }

        private void OnTriggerEnter(Collider other) => TryRegister(other);
        private void OnTriggerStay(Collider other) => TryRegister(other); // 兜底（spawn-inside）

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!_playerInRange) return;
            _playerInRange = false;
            InteractionRouter.Unregister(this);
            _room?.SetHintVisible(false);
        }

        private void TryRegister(Collider other)
        {
            if (_used || _playerInRange) return;
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        private void Update()
        {
            if (_used) return;
            _room?.SetHintVisible(IsRoutedActive);
            if (!IsRoutedActive) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.fKey.wasPressedThisFrame)
            {
                _used = true;
                InteractionRouter.Unregister(this);
                _room?.HealPlayer();
            }
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }
}
