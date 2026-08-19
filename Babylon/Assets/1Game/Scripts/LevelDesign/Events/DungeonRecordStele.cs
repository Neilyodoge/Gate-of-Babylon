using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu.LevelDesign
{
    /// <summary>巡礼封藏室的叙事记录碑，只提供世界观信息，不发放道具或强度奖励。</summary>
    public sealed class DungeonRecordStele : MonoBehaviour, IInteractable
    {
        private bool _playerInRange;
        private NpcHeadCard _headCard;

        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 20;
        public bool IsInteractionAvailable => _playerInRange;
        public bool IsRoutedActive { get; set; }

        private void Awake()
        {
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "巡礼记录碑",
                icon = "◇",
                roleSub = "无暮王城旧档",
                hintText = "按 [F] 阅读残存记录",
                themeColor = new Color(0.88f, 0.62f, 0.28f),
                yOffset = 2.5f,
                showLongRangeMarker = false,
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
            if (!IsRoutedActive
                || Keyboard.current == null
                || !Keyboard.current.fKey.wasPressedThisFrame)
                return;

            BossDialogueUI.Show(
                "巡礼记录碑",
                LevelAPhaseRuntime.IsNightMapActive
                    ? new[]
                    {
                        "碑文在永夜中显出被刮去的末行。",
                        "所谓巡礼，是将受冠者送入封藏室，等待下一次永不结束的加冕。",
                    }
                    : new[]
                    {
                        "名录只记录进入王城者，从未记载任何离城之人。",
                        "封藏室的配额每日增加，王城却始终宣称加冕尚未开始。",
                    },
                3.2f);
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
