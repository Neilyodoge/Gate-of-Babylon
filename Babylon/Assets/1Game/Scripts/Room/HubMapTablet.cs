using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>基地石碑入口：打开与 Tab 相同的关卡提示面板。</summary>
    public sealed class HubMapTablet : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 20;
        public bool IsInteractionAvailable => _playerInRange;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private NpcHeadCard _headCard;

        private void Awake()
        {
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "秘境情报碑",
                icon = "◇",
                roleSub = "关卡特殊事件与阶段情报",
                hintText = "按 [F] 查看关卡提示",
                themeColor = new Color(0.35f, 0.85f, 0.7f),
                yOffset = 2.4f,
                showLongRangeMarker = true,
            });

            var trigger = new GameObject("InteractTrigger");
            trigger.transform.SetParent(transform, false);
            var collider = trigger.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 2.5f;
            var body = trigger.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var bridge = trigger.AddComponent<TriggerBridge>();
            bridge.OnEnter = HandleEnter;
            bridge.OnExit = HandleExit;
        }

        private void Update()
        {
            _headCard?.SetHintVisible(IsRoutedActive);
            if (!IsRoutedActive)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                DebugConsole.Instance?.OpenLevelGuide();
        }

        private void HandleEnter()
        {
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        private void HandleExit()
        {
            _playerInRange = false;
            InteractionRouter.Unregister(this);
            _headCard?.SetHintVisible(false);
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }
}
