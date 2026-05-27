using System;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 村庄 Hub —— 玩家入梦后的"现实"出生点（参考 Hades 2 的 Mourning Fields）。
    /// 不属于 6 层境界的任何一层，玩家在此进行：
    ///   1. 通过 NPC 选择 / 重选化身（默认金化身，玩家可不交互直接出发）
    ///   2. 走山门 → 调 GameManager.StartNewRun() 进入第一关
    ///
    /// 视觉上比常规战斗房间更大、更暖、装饰性更强；不会清残留拾取物（Hub 本身没有掉落）。
    /// </summary>
    public class VillageHub : MonoBehaviour
    {
        public float RoomWidth => 32f;
        public float RoomDepth => 32f;

        private GameObject _roomVisuals;
        private SpiritRootNPC _npc;
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
            CreateWorldText(tablet.transform, "TabletText", "梦  境  之  门",
                new Vector3(0, 0.4f, 0.85f), Quaternion.Euler(0, 180f, 0),
                new Vector2(2.4f, 0.6f), 36, new Color(1f, 0.8f, 0.4f));

            // 副标题
            CreateWorldText(tablet.transform, "TabletSub", "—— 入梦修仙 · 由此出发 ——",
                new Vector3(0, -0.2f, 0.85f), Quaternion.Euler(0, 180f, 0),
                new Vector2(3f, 0.4f), 18, new Color(0.85f, 0.7f, 0.5f));
        }

        private void BuildNpcAndPortal(Action onPortalEntered)
        {
            // ===== NPC：司命使（左侧）=====
            var npcGo = new GameObject("SpiritRootNPC");
            npcGo.transform.SetParent(transform, false);
            npcGo.transform.localPosition = new Vector3(-7f, 0f, 2f);
            _npc = npcGo.AddComponent<SpiritRootNPC>();
            _npc.Build();

            // ===== 山门：通向第一关（前方）=====
            var portalGo = new GameObject("VillagePortal");
            portalGo.transform.SetParent(transform, false);
            portalGo.transform.localPosition = new Vector3(0f, 0f, RoomDepth / 2f - 4f);
            _portal = portalGo.AddComponent<VillagePortal>();
            _portal.Build(onPortalEntered);

            // ===== 灵田模块（v0.5 第一个洞府模块，右侧近）=====
            var lingTianGo = new GameObject("LingTian");
            lingTianGo.transform.SetParent(transform, false);
            lingTianGo.transform.localPosition = new Vector3(7f, 0f, 2f);
            lingTianGo.AddComponent<LingTian>();

            // ===== 炼丹房模块（v0.5 第二个洞府模块，右侧远）=====
            var alchemyGo = new GameObject("AlchemyRoom");
            alchemyGo.transform.SetParent(transform, false);
            alchemyGo.transform.localPosition = new Vector3(10f, 0f, -3f);
            alchemyGo.AddComponent<AlchemyRoom>();

            // ===== 悟道蒲团模块（v0.5 第三个洞府模块，左侧近）=====
            var wuDaoGo = new GameObject("WuDaoCushion");
            wuDaoGo.transform.SetParent(transform, false);
            wuDaoGo.transform.localPosition = new Vector3(-7f, 0f, -3f);
            wuDaoGo.AddComponent<WuDaoCushion>();

            // ===== 炼器房模块（v0.5 Week 4 第四个洞府模块，右后）=====
            var forgeGo = new GameObject("ForgeRoom");
            forgeGo.transform.SetParent(transform, false);
            forgeGo.transform.localPosition = new Vector3(10f, 0f, 4f);
            forgeGo.AddComponent<ForgeRoom>();

            // ===== 藏经阁模块（v0.5 Week 4 第五个洞府模块，左后）=====
            var scriptureGo = new GameObject("ScripturePavilion");
            scriptureGo.transform.SetParent(transform, false);
            scriptureGo.transform.localPosition = new Vector3(-10f, 0f, 4f);
            scriptureGo.AddComponent<ScripturePavilion>();

            // ===== 灵兽园模块（v0.5 Week 4 第六个洞府模块，正北偏左）=====
            var beastGo = new GameObject("SpiritBeastGarden");
            beastGo.transform.SetParent(transform, false);
            beastGo.transform.localPosition = new Vector3(-3f, 0f, 8f);
            beastGo.AddComponent<SpiritBeastGarden>();

            // ===== 阵法台模块（v0.5 Week 4 第七个洞府模块，正北偏右）=====
            var formationGo = new GameObject("FormationPlatform");
            formationGo.transform.SetParent(transform, false);
            formationGo.transform.localPosition = new Vector3(3f, 0f, 8f);
            formationGo.AddComponent<FormationPlatform>();
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
            var text = textGo.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }
    }

    // ============================================================
    //                    NPC：司命使（化身选择）
    // ============================================================

    /// <summary>
    /// 司命使 NPC：玩家走近按 F → 弹 SpiritRootSelectUI。
    /// 优先级 30，介于商店 (40) / 升级台 (35) 与拾取物 (20/25) 之间，
    /// 防止玩家站在 NPC 身边时被路边的小灵物抢交互焦点。
    /// </summary>
    public class SpiritRootNPC : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 30;
        public bool IsInteractionAvailable => _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private NpcHeadCard _headCard;
        private GameObject _bodyGo;

        public void Build()
        {
            // NPC 身体（圆柱）
            _bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _bodyGo.name = "NPC_Body";
            _bodyGo.transform.SetParent(transform, false);
            _bodyGo.transform.localPosition = new Vector3(0, 1f, 0);
            var bodyCol = _bodyGo.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            var bodyRend = _bodyGo.GetComponent<Renderer>();
            if (bodyRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.6f, 0.5f, 0.85f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.4f, 0.3f, 0.7f) * 0.4f);
                bodyRend.material = mat;
            }

            // 统一 NPC 头顶卡片（紫色主题 · 化身选择）
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "司命使",
                icon = "✦",
                roleSub = "化身选择",
                hintText = "按 [F] 选择化身",
                themeColor = new Color(0.78f, 0.55f, 1f),
                yOffset = 2.6f,
                showLongRangeMarker = true
            });

            // 触发器（更大一点，方便玩家走过去）
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
                bool wantHint = IsRoutedActive && !SpiritRootSelectUI.IsVisible;
                _headCard.SetHintVisible(wantHint);
            }

            if (!IsRoutedActive) return;
            if (SpiritRootSelectUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
                SpiritRootSelectUI.Show();
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

            // 统一卡片 UI（紫色 · 入梦）—— 让山门远距离就能看到，玩家明白这里是出口
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "梦境之门",
                icon = "✦",
                roleSub = "入梦 · 进入第一关",
                hintText = "按 [F] 入梦",
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
                bool wantHint = IsRoutedActive && !SpiritRootSelectUI.IsVisible;
                _headCard.SetHintVisible(wantHint);
            }

            if (!IsRoutedActive) return;
            if (SpiritRootSelectUI.IsVisible) return;

            // v0.5：魂伤未消退时拒绝入梦
            float soulHurt = SaveSystem.Instance.Data.soulHurtRemainingSec;
            if (soulHurt > 0f)
            {
                if (_headCard != null) _headCard.SetHintVisible(false);
                var kb0 = UnityEngine.InputSystem.Keyboard.current;
                if (kb0 != null && kb0.fKey.wasPressedThisFrame)
                {
                    Debug.Log($"<color=#ff8866>[VillagePortal] 魂伤未愈，无法入梦（剩 {GameTime.FormatDuration(soulHurt)}）</color>");
                }
                return;
            }

            // 携丹面板打开时屏蔽其他输入
            if (PillCarryUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                // v0.5：先弹携丹面板，确认后再入梦
                if (_headCard != null) _headCard.SetHintVisible(false);
                PillCarryUI.Show(
                    onConfirm: () =>
                    {
                        PendingPillCarry.Commit();
                        _triggered = true;
                        _onEnter?.Invoke();
                    },
                    onCancel: () =>
                    {
                        PendingPillCarry.ClearPending();
                    });
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
