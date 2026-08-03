using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 村庄 Hub —— 冒险者基地（参考 Hades 2 的 Mourning Fields）。
    /// 不属于 6 层境界的任何一层，玩家在此进行：
    ///   1. 走配置使按 F → 打开模块装配界面配置初始 Build（GDD V.07）
    ///   2. 走山门 → 调 GameManager.StartNewRun() 进入第一关
    ///
    /// 视觉上比常规战斗房间更大、更暖、装饰性更强；不会清残留拾取物（Hub 本身没有掉落）。
    /// </summary>
    public class VillageHub : MonoBehaviour
    {
        public float RoomWidth => 32f;
        public float RoomDepth => 32f;

        private GameObject _roomVisuals;
        private VillagePortal _portal;

        /// <summary>玩家出生位置（房间中心）</summary>
        public Vector3 PlayerSpawnPos => transform.position;

        public void Initialize(Action onPortalEntered)
        {
            BuildRoom();
            BuildNpcAndPortal(onPortalEntered);
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }

        private void BuildRoom()
        {
            // 村庄独立调色板：暖棕 + 烛火橙，明显区别于"外面的关卡"
            _roomVisuals = RoomBuilder.Build(transform, RoomWidth, RoomDepth, 0, RoomBuilder.VillagePalette);
            _roomVisuals.name = "VillageHubVisuals";

            // 中央广场：一块温暖色调的圆形地砖 + 自发光石碑，提示这是出生点
            BuildCentralPlaza();
        }

        private void BuildCentralPlaza()
        {
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "VillagePlaza";
            plaza.transform.SetParent(transform, false);
            plaza.transform.localPosition = new Vector3(0, 0.05f, 0);
            plaza.transform.localScale = new Vector3(6f, 0.1f, 6f);
            var col = plaza.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = plaza.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.35f, 0.28f, 0.20f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.25f, 0.18f, 0.10f) * 0.6f);
                rend.material = mat;
            }

            // 中央石碑（Title）
            var tablet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tablet.name = "VillageTablet";
            tablet.transform.SetParent(transform, false);
            tablet.transform.localPosition = new Vector3(0, 1f, -3.5f);
            tablet.transform.localScale = new Vector3(0.4f, 2f, 1.6f);
            var tabletCol = tablet.GetComponent<Collider>();
            if (tabletCol != null) Destroy(tabletCol);

            var tabletRend = tablet.GetComponent<Renderer>();
            if (tabletRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.18f, 0.15f, 0.12f);
                tabletRend.material = mat;
            }

            // 石碑文字（朝向玩家出生方向，即 -Z）
            CreateWorldText(tablet.transform, "TabletText", "秘  境  之  门",
                new Vector3(0, 0.4f, 0.85f), Quaternion.Euler(0, 180f, 0),
                new Vector2(2.4f, 0.6f), 36, new Color(1f, 0.8f, 0.4f));

            // 副标题
            CreateWorldText(tablet.transform, "TabletSub", "—— 探索秘境 · 由此出发 ——",
                new Vector3(0, -0.2f, 0.85f), Quaternion.Euler(0, 180f, 0),
                new Vector2(3f, 0.4f), 18, new Color(0.85f, 0.7f, 0.5f));
        }

        private void BuildNpcAndPortal(Action onPortalEntered)
        {
            // ===== 山门：通向第一关（前方）=====
            var portalGo = new GameObject("VillagePortal");
            portalGo.transform.SetParent(transform, false);
            portalGo.transform.localPosition = new Vector3(0f, 0f, RoomDepth / 2f - 4f);
            _portal = portalGo.AddComponent<VillagePortal>();
            _portal.Build(onPortalEntered);
        }

        // ==================== 工具：世界空间贴文字 ====================
        private static void CreateWorldText(Transform parent, string name, string content,
            Vector3 localPos, Quaternion localRot,
            Vector2 size, int fontSize, Color color)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = localPos;
            canvasGo.transform.localRotation = localRot;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            canvasGo.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            if (UGuiKit.CjkFont != null) text.font = UGuiKit.CjkFont;
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.outlineColor = new Color(0, 0, 0, 0.8f);
            text.outlineWidth = 0.2f;
        }
    }

    // ============================================================
    //              NPC：配置使（模块 Build 配置）
    // ============================================================

    /// <summary>
    /// 配置使 NPC（GDD V.07 §4.1）：玩家走近按 F → 打开 ModuleAssemblyUI 配置初始模块 Build。
    /// </summary>
    public class ModuleConfigNPC : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 30;
        public bool IsInteractionAvailable => _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private NpcHeadCard _headCard;

        public void Build()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "NPC_Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            var bodyRend = body.GetComponent<Renderer>();
            if (bodyRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.3f, 0.7f, 0.9f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.75f) * 0.5f);
                bodyRend.material = mat;
            }

            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "配置使",
                icon = "⚙",
                roleSub = "模块配置",
                hintText = "按 [F] 配置 Build",
                themeColor = new Color(0.3f, 0.7f, 0.95f),
                yOffset = 2.6f,
                showLongRangeMarker = true
            });

            var trig = new GameObject("InteractTrigger");
            trig.transform.SetParent(transform, false);
            var sc = trig.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var bridge = trig.AddComponent<TriggerBridge>();
            bridge.OnEnter = OnPlayerEnter;
            bridge.OnExit = OnPlayerExit;
        }

        private void Update()
        {
            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && !ModuleAssemblyUI.IsVisible;
                _headCard.SetHintVisible(wantHint);
            }

            if (!IsRoutedActive) return;
            if (ModuleAssemblyUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
                ModuleAssemblyUI.Instance?.Toggle();
        }

        private void OnPlayerEnter()
        {
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        private void OnPlayerExit()
        {
            _playerInRange = false;
            InteractionRouter.Unregister(this);
            if (_headCard != null) _headCard.SetHintVisible(false);
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }

    // ============================================================
    //                  山门：通向第一关的传送门
    // ============================================================

    /// <summary>
    /// 山门 Portal：玩家走进 → 短延迟 → 按 F 进入第一关。
    /// 优先级 5（与房间出口一致），保证若旁边有 NPC 时 NPC 优先。
    /// </summary>
    public class VillagePortal : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 5;
        public bool IsInteractionAvailable =>
            !_triggered && _playerInRange && _enterTimer >= ENTER_DELAY;
        public bool IsRoutedActive { get; set; }

        private const float ENTER_DELAY = 0.25f;
        private bool _playerInRange;
        private bool _triggered;
        private float _enterTimer;
        private Action _onEnter;
        private NpcHeadCard _headCard;

        public void Build(Action onEnter)
        {
            _onEnter = onEnter;

            // 大门左立柱
            BuildPillar(new Vector3(-2f, 1.5f, 0));
            // 大门右立柱
            BuildPillar(new Vector3(2f, 1.5f, 0));
            // 横梁
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "PortalBeam";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0, 3.2f, 0);
            beam.transform.localScale = new Vector3(5f, 0.4f, 0.6f);
            var bcol = beam.GetComponent<Collider>();
            if (bcol != null) Destroy(bcol);
            var brend = beam.GetComponent<Renderer>();
            if (brend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.5f, 0.15f, 0.12f);
                brend.material = mat;
            }

            // 中央门帘（半透明发光）
            var curtain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curtain.name = "PortalCurtain";
            curtain.transform.SetParent(transform, false);
            curtain.transform.localPosition = new Vector3(0, 1.5f, 0);
            curtain.transform.localScale = new Vector3(3.6f, 3f, 0.1f);
            var ccol = curtain.GetComponent<Collider>();
            if (ccol != null) Destroy(ccol);
            var crend = curtain.GetComponent<Renderer>();
            if (crend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.6f, 0.3f, 0.9f, 0.35f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.6f, 0.3f, 0.9f) * 1.8f);
                crend.material = mat;
            }

            // 触发器
            var trig = new GameObject("PortalTrigger");
            trig.transform.SetParent(transform, false);
            trig.transform.localPosition = Vector3.zero;
            var sc = trig.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var bridge = trig.AddComponent<TriggerBridge>();
            bridge.OnEnter = OnPlayerEnter;
            bridge.OnExit = OnPlayerExit;

            // 统一卡片 UI（紫色 · 入秘境）—— 让山门远距离就能看到，玩家明白这里是出口
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "秘境之门",
                icon = "✦",
                roleSub = "入秘境 · 进入第一关",
                hintText = "按 [F] 入秘境",
                themeColor = new Color(0.7f, 0.4f, 1f),
                yOffset = 4.5f,
                showLongRangeMarker = true
            });
        }

        private void BuildPillar(Vector3 localPos)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "PortalPillar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = localPos;
            pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            var col = pillar.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = pillar.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.45f, 0.13f, 0.10f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.15f, 0.12f) * 0.4f);
                rend.material = mat;
            }
        }

        private void Update()
        {
            if (_triggered || !_playerInRange) return;

            _enterTimer += Time.deltaTime;
            if (_enterTimer < ENTER_DELAY) return;

            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && !ModuleAssemblyUI.IsVisible;
                _headCard.SetHintVisible(wantHint);
            }

            if (!IsRoutedActive) return;
            if (ModuleAssemblyUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                if (_headCard != null) _headCard.SetHintVisible(false);
                _triggered = true;
                // V0.4：删除职业/模板选择，直接进入秘境
                _onEnter?.Invoke();
            }
        }

        private void OnPlayerEnter()
        {
            _playerInRange = true;
            _enterTimer = 0f;
            InteractionRouter.Register(this);
        }

        private void OnPlayerExit()
        {
            _playerInRange = false;
            _enterTimer = 0f;
            InteractionRouter.Unregister(this);
            if (_headCard != null) _headCard.SetHintVisible(false);
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }

    // ============================================================
    //          通用：把 Trigger 回调桥接到 lambda
    // ============================================================

    /// <summary>
    /// 通用 Trigger 桥接器：外部用 lambda 直接订阅 Enter/Exit 事件，
    /// 不需要再专门为每个交互体写一个 SubTrigger 类。
    /// 兜底 OnTriggerStay：当玩家通过 TeleportPlayer 直接出现在触发器内部时
    /// Unity 不会触发 OnTriggerEnter，必须靠 Stay 兜底（参考 RoomExitTrigger / ChestTrigger 同款 pattern）。
    /// </summary>
    public class TriggerBridge : MonoBehaviour
    {
        public Action OnEnter;
        public Action OnExit;
        private bool _inside;

        private void OnTriggerEnter(Collider other) => TryEnter(other);
        private void OnTriggerStay(Collider other) => TryEnter(other);

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!_inside) return;
            _inside = false;
            OnExit?.Invoke();
        }

        private void TryEnter(Collider other)
        {
            if (_inside) return;
            if (!other.CompareTag("Player")) return;
            _inside = true;
            OnEnter?.Invoke();
        }
    }
}
