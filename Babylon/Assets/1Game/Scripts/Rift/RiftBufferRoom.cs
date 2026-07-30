using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境缓冲区（Phase3）。
    /// 玩家在此装备局内带出的 Build，然后走「挑战之门」开始计时挑战。
    /// 缓冲区同时提供返回基地出口；正式挑战间不提供返回入口。
    /// </summary>
    public class RiftBufferRoom : MonoBehaviour
    {
        public float RoomWidth => 28f;
        public float RoomDepth => 28f;

        private GameObject _roomVisuals;
        private Action _onStartChallenge;

        public void Initialize(int tier, Action onStartChallenge)
        {
            _onStartChallenge = onStartChallenge;
            BuildRoom(tier);
            BuildStationAndGate();
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }

        private void BuildRoom(int tier)
        {
            // 大秘境缓冲区调色板：冷紫 + 幽蓝，区别于村庄暖色
            _roomVisuals = RoomBuilder.Build(transform, RoomWidth, RoomDepth, 0, RoomBuilder.VillagePalette);
            _roomVisuals.name = "RiftBufferVisuals";

            // 中央传送阵
            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "RiftPlaza";
            plaza.transform.SetParent(transform, false);
            plaza.transform.localPosition = new Vector3(0, 0.05f, 0);
            plaza.transform.localScale = new Vector3(5f, 0.1f, 5f);
            var col = plaza.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = plaza.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.15f, 0.10f, 0.25f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.35f, 0.15f, 0.55f) * 0.8f);
                rend.material = mat;
            }
        }

        private void BuildStationAndGate()
        {
            // ===== 装备台 NPC（左侧）—— 打开 RiftEquipUI 选择/装备 Build =====
            var stationGo = new GameObject("RiftEquipStation");
            stationGo.transform.SetParent(transform, false);
            stationGo.transform.localPosition = new Vector3(-5f, 0f, 2f);
            stationGo.AddComponent<RiftEquipStation>().Build();

            // ===== 挑战之门（前方）—— 开始计时挑战 =====
            var gateGo = new GameObject("RiftChallengeGate");
            gateGo.transform.SetParent(transform, false);
            gateGo.transform.localPosition = new Vector3(0f, 0f, RoomDepth / 2f - 4f);
            gateGo.AddComponent<RiftChallengeGate>().Build(_onStartChallenge);

            // ===== 返回基地（右侧）—— 仅缓冲区可用 =====
            var returnGo = new GameObject("RiftReturnToHubExit");
            returnGo.transform.SetParent(transform, false);
            returnGo.transform.localPosition = new Vector3(RoomWidth / 2f - 4f, 0f, 0f);
            returnGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            returnGo.AddComponent<PreparationGate>().Build(
                () => RiftManager.Instance.ExitBufferToVillage(),
                "返回基地", "离开大秘境准备区", "按 [F] 返回基地",
                new Color(0.35f, 0.85f, 0.65f));
        }
    }

    // ============================================================
    //            装备台 NPC：打开 Build 装备 UI
    // ============================================================

    /// <summary>大秘境装备台：玩家走近按 F → 打开 RiftEquipUI 选择装备 Build。</summary>
    public class RiftEquipStation : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 30;
        public bool IsInteractionAvailable => _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private NpcHeadCard _headCard;

        public void Build()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Station_Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);
            var bodyRend = body.GetComponent<Renderer>();
            if (bodyRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.4f, 0.3f, 0.7f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.3f, 0.85f) * 0.6f);
                bodyRend.material = mat;
            }

            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "装备台",
                icon = "⚔",
                roleSub = "装备 Build",
                hintText = "按 [F] 装备 Build",
                themeColor = new Color(0.55f, 0.4f, 0.9f),
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
                _headCard.SetHintVisible(IsRoutedActive && !RiftEquipUI.IsVisible);

            if (!IsRoutedActive || RiftEquipUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
                RiftEquipUI.Show(null);
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

        private void OnDestroy() => InteractionRouter.Unregister(this);
    }

    // ============================================================
    //            挑战之门：开始计时挑战
    // ============================================================

    /// <summary>大秘境挑战门：玩家走近按 F → 开始计时挑战（需已装备 Build）。</summary>
    public class RiftChallengeGate : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 5;
        public bool IsInteractionAvailable => !_triggered && _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private bool _triggered;
        private Action _onEnter;
        private NpcHeadCard _headCard;

        public void Build(Action onEnter)
        {
            _onEnter = onEnter;

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "GateBeam";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0, 3.2f, 0);
            beam.transform.localScale = new Vector3(5f, 0.4f, 0.6f);
            var bcol = beam.GetComponent<Collider>();
            if (bcol != null) Destroy(bcol);
            var brend = beam.GetComponent<Renderer>();
            if (brend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.35f, 0.1f, 0.4f);
                brend.material = mat;
            }

            var curtain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curtain.name = "GateCurtain";
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
                mat.color = new Color(0.9f, 0.2f, 0.4f, 0.35f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.9f, 0.2f, 0.5f) * 1.8f);
                crend.material = mat;
            }

            var trig = new GameObject("GateTrigger");
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
                displayName = "挑战之门",
                icon = "✦",
                roleSub = "开始计时挑战",
                hintText = "按 [F] 开始挑战",
                themeColor = new Color(0.9f, 0.3f, 0.5f),
                yOffset = 4.5f,
                showLongRangeMarker = true
            });
        }

        private void Update()
        {
            if (_triggered || !_playerInRange) return;

            if (_headCard != null)
                _headCard.SetHintVisible(IsRoutedActive && !RiftEquipUI.IsVisible);

            if (!IsRoutedActive || RiftEquipUI.IsVisible) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                if (_headCard != null) _headCard.SetHintVisible(false);
                _triggered = true;
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

        private void OnDestroy() => InteractionRouter.Unregister(this);
    }
}
