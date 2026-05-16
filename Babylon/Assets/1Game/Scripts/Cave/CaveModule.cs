using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府模块抽象基类（v0.5 搜打撤核心）。
    ///
    /// 7 个洞府模块（灵田 / 炼丹房 / 炼器房 / 灵兽园 / 藏经阁 / 阵法台 / 悟道蒲团）
    /// 共用同一个 IInteractable + NpcHeadCard + TriggerBridge 范式：
    ///   - 玩家走近 → 进入交互范围
    ///   - 按 F → 打开模块面板（子类实现）
    /// </summary>
    public abstract class CaveModule : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public virtual int InteractionPriority => 25;
        public bool IsInteractionAvailable => _playerInRange && !IsPanelOpen;
        public bool IsRoutedActive { get; set; }

        protected bool _playerInRange;
        protected NpcHeadCard _headCard;

        /// <summary>模块名（显示在 NpcHeadCard 上）</summary>
        public abstract string ModuleName { get; }
        /// <summary>模块图标（显示在 NpcHeadCard 上）</summary>
        public virtual string ModuleIcon => "✦";
        /// <summary>模块角色描述（显示在 NpcHeadCard 副标题）</summary>
        public abstract string ModuleRole { get; }
        /// <summary>主题色</summary>
        public abstract Color ModuleColor { get; }

        /// <summary>面板是否打开</summary>
        public abstract bool IsPanelOpen { get; }

        /// <summary>玩家按 F → 打开面板（子类实现）</summary>
        protected abstract void OpenPanel();

        /// <summary>玩家 ESC 或关闭按钮 → 关闭面板</summary>
        public abstract void ClosePanel();

        protected virtual void Awake()
        {
            BuildBody();
            BuildTrigger();
            BuildHeadCard();
        }

        /// <summary>构建身体视觉（子类可重写）</summary>
        protected virtual void BuildBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"{ModuleName}_Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = ModuleColor * 0.7f;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ModuleColor * 0.4f);
                rend.material = mat;
            }
        }

        private void BuildTrigger()
        {
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

        private void BuildHeadCard()
        {
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = ModuleName,
                icon = ModuleIcon,
                roleSub = ModuleRole,
                hintText = "按 [F] 打开",
                themeColor = ModuleColor,
                yOffset = 2.6f,
                showLongRangeMarker = true
            });
        }

        protected virtual void Update()
        {
            // 提示开关
            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && !IsPanelOpen;
                _headCard.SetHintVisible(wantHint);
            }

            // 按 F 打开面板
            if (!IsRoutedActive || IsPanelOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
            {
                OpenPanel();
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
            if (IsPanelOpen) ClosePanel();
        }

        protected virtual void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }
}
