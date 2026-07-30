using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4 准备房间 —— 局外→准备房间→技能选择→局内。
    /// 玩家从村庄进入后先到此房间，完成技能选择后通过出口进入第一个战斗关卡。
    /// </summary>
    public class PrepRoom : MonoBehaviour
    {
        private const float RoomWidth = 20f;
        private const float RoomDepth = 20f;

        private Action _onReady;
        private SkillData[] _skillPool;
        private bool _skillSelected;
        private PrepRoomExit _exit;
        private GameObject _roomVisuals;

        public void Initialize(SkillData[] skillPool, Action onReady)
        {
            _skillPool = skillPool;
            _onReady = onReady;
            BuildRoom();
            BuildExit();

            SkillSelectUI.Show(_skillPool, OnSkillPicked);
        }

        private void OnSkillPicked(SkillData skill)
        {
            _skillSelected = true;

            if (skill != null)
            {
                var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    combat.EquipSkillQ(skill);
                    Debug.Log($"<color=#66d9ff>[PrepRoom] 已装备初始技能：{skill.skillName} → Q 槽位</color>");
                }
            }

            if (_exit != null)
                _exit.Unlock();
        }

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, RoomWidth, RoomDepth, 0, PrepPalette);
            _roomVisuals.name = "PrepRoomVisuals";

            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "PrepPlaza";
            plaza.transform.SetParent(transform, false);
            plaza.transform.localPosition = new Vector3(0, 0.03f, 0);
            plaza.transform.localScale = new Vector3(5f, 0.08f, 5f);
            var col = plaza.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = plaza.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.22f, 0.25f, 0.32f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.18f, 0.25f) * 0.5f);
                rend.material = mat;
            }

            BuildSignboard();
        }

        private void BuildSignboard()
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "PrepSign";
            board.transform.SetParent(transform, false);
            board.transform.localPosition = new Vector3(0, 1f, -4f);
            board.transform.localScale = new Vector3(0.3f, 1.8f, 1.2f);
            var col = board.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = board.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.15f, 0.17f, 0.22f);
                rend.material = mat;
            }

            CreateWorldText(board.transform, "SignText", "准  备  区",
                new Vector3(0, 0.3f, 0.65f), Quaternion.Euler(0, 180f, 0),
                new Vector2(2f, 0.5f), 32, new Color(0.6f, 0.8f, 1f));

            CreateWorldText(board.transform, "SignSub", "—— 选择技能后前往出口 ——",
                new Vector3(0, -0.2f, 0.65f), Quaternion.Euler(0, 180f, 0),
                new Vector2(3f, 0.35f), 16, new Color(0.5f, 0.6f, 0.75f));
        }

        private void BuildExit()
        {
            var exitGo = new GameObject("PrepRoomExit");
            exitGo.transform.SetParent(transform, false);
            exitGo.transform.localPosition = new Vector3(0, 0, RoomDepth / 2f - 3f);
            _exit = exitGo.AddComponent<PrepRoomExit>();
            _exit.Build(() =>
            {
                _onReady?.Invoke();
            });
        }

        private void OnDestroy()
        {
            SkillSelectUI.Hide();
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }

        private static readonly RoomPalette PrepPalette = new()
        {
            ground = new Color(0.14f, 0.16f, 0.20f),
            groundLine = new Color(0.20f, 0.24f, 0.30f, 0.4f),
            border = new Color(0.28f, 0.32f, 0.42f, 0.6f),
            wall = new Color(0.18f, 0.20f, 0.26f),
            wallTop = new Color(0.30f, 0.35f, 0.48f),
            pillar = new Color(0.22f, 0.24f, 0.32f),
            pillarTop = new Color(0.38f, 0.42f, 0.58f),
            cornerGlow = new Color(0.4f, 0.6f, 1f),
            obstacleA = new Color(0.16f, 0.18f, 0.24f),
            obstacleB = new Color(0.22f, 0.25f, 0.32f)
        };

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

    /// <summary>
    /// 准备房间出口 —— 技能选择完成后解锁，玩家按 F 进入第一关。
    /// </summary>
    public class PrepRoomExit : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 5;
        public bool IsInteractionAvailable => _unlocked && _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private bool _unlocked;
        private bool _triggered;
        private Action _onEnter;
        private NpcHeadCard _headCard;

        public void Build(Action onEnter)
        {
            _onEnter = onEnter;

            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "ExitGate";
            gate.transform.SetParent(transform, false);
            gate.transform.localPosition = new Vector3(0, 1.5f, 0);
            gate.transform.localScale = new Vector3(3f, 3f, 0.15f);
            var col = gate.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = gate.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.5f, 0.9f, 0.30f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 0.9f) * 1.5f);
                rend.material = mat;
            }

            var trig = new GameObject("ExitTrigger");
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

            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "秘境入口",
                icon = "▶",
                roleSub = "选择技能后可通过",
                hintText = "先选择一个技能",
                themeColor = new Color(0.4f, 0.6f, 1f),
                yOffset = 4f,
                showLongRangeMarker = true
            });
        }

        public void Unlock()
        {
            _unlocked = true;
            if (_headCard != null)
            {
                _headCard.UpdateName("秘境入口 · 就绪");
                _headCard.UpdateHintText("按 [F] 进入秘境");
            }
        }

        private void Update()
        {
            if (_triggered || !_unlocked || !_playerInRange) return;

            if (_headCard != null)
                _headCard.SetHintVisible(IsRoutedActive);

            if (!IsRoutedActive) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                _triggered = true;
                if (_headCard != null) _headCard.SetHintVisible(false);
                _onEnter?.Invoke();
            }
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
}
