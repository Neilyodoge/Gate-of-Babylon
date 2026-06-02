using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 掉落物拾取基类（v0.5.5 重构）—— 抽出 ItemPickup / SkillPickup 的公共行为：
    ///   · 浮动 + 自转
    ///   · 触发器进出 + InteractionRouter 注册（多个可交互物重叠时只有"路由激活"的响应 F）
    ///   · [F] 轻按 = 主操作（拾取 / 装备）；长按 = 分解
    ///   · 世界提示面板的显示 / 跟随 / 销毁（用 <see cref="WorldPromptPanel"/>）
    ///
    /// 子类实现：视觉、提示内容、主操作、分解、目标获取；可选覆盖：模态保持 / 额外输入（如功法换槽）。
    /// </summary>
    public abstract class PickupBase : MonoBehaviour, IInteractable
    {
        // ===== IInteractable：统一 F 键交互路由 =====
        public Vector3 InteractionWorldPos => transform.position;
        public abstract int InteractionPriority { get; }
        public bool IsInteractionAvailable => !_pickedUp && _playerInRange;
        public bool IsRoutedActive { get; set; }

        // ===== 可调运动参数（子类可覆盖）=====
        protected virtual float BobSpeed => 2f;
        protected virtual float BobHeight => 0.15f;
        protected virtual float RotateSpeed => 90f;
        protected virtual float TriggerRadius => 2.5f;
        protected virtual float HoldToDecompose => 1.5f;

        // ===== 运行时状态 =====
        protected Vector3 _startPos;
        protected bool _pickedUp;
        protected bool _playerInRange;
        protected WorldPromptHandle _prompt;
        protected Keyboard _keyboard;
        private float _holdTimer;

        // ===== 子类钩子 =====
        protected abstract void SetupVisual();
        protected abstract PickupPromptData BuildPromptData();
        protected abstract void OnPrimaryAction();    // F 轻按
        protected abstract void OnDecomposeAction();   // F 长按
        protected abstract bool AcquireTarget(Collider other);  // 进入触发器时获取玩家目标；返回 false 则不响应
        protected abstract bool HasTarget { get; }     // 目标是否有效（避免目标丢失时空引用）
        protected virtual void ReleaseTarget() { }

        /// <summary>模态保持：为 true 时即使路由切走也不隐藏提示 / 不清长按计时（如功法换槽选择中）。</summary>
        protected virtual bool KeepActiveOverride => false;
        /// <summary>额外输入处理（如换槽 Q/E/R/Esc）；返回 true 表示已消费、跳过本帧 F 逻辑。</summary>
        protected virtual bool HandleExtraInput(Keyboard kb) => false;

        protected virtual void Awake()
        {
            _keyboard = Keyboard.current;
        }

        protected virtual void Start()
        {
            _startPos = transform.position;

            var col = GetComponent<SphereCollider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.radius = TriggerRadius;
            }

            SetupVisual();
        }

        protected virtual void Update()
        {
            if (_pickedUp) return;

            // 浮动 + 自转（提示面板不受影响——它是独立根物体，位置每帧手动跟随）
            float newY = _startPos.y + Mathf.Sin(Time.time * BobSpeed) * BobHeight;
            transform.position = new Vector3(_startPos.x, newY, _startPos.z);
            transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime);

            if (_prompt != null && _prompt.root != null)
                _prompt.root.transform.position = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);

            // 提示显隐：只有"路由激活"的可交互物显示提示（重叠时不会一起弹）
            bool showActive = _playerInRange && IsRoutedActive;
            if (showActive)
            {
                if (_prompt == null) ShowPrompt();
            }
            else
            {
                if (!KeepActiveOverride && _prompt != null) HidePrompt();
                if (!KeepActiveOverride) _holdTimer = 0f;
            }

            // 输入：路由激活 或 模态保持 时响应
            if (_playerInRange && HasTarget && (IsRoutedActive || KeepActiveOverride))
            {
                var kb = _keyboard ?? (_keyboard = Keyboard.current);
                if (kb == null) return;

                if (HandleExtraInput(kb)) return;  // 模态（如换槽）已消费输入

                if (kb.fKey.isPressed)
                {
                    _holdTimer += Time.deltaTime;
                    if (_prompt?.holdFill != null) _prompt.holdFill.fillAmount = _holdTimer / HoldToDecompose;
                    if (_holdTimer >= HoldToDecompose)
                    {
                        OnDecomposeAction();
                        return;
                    }
                }

                if (kb.fKey.wasReleasedThisFrame && _holdTimer < HoldToDecompose)
                    OnPrimaryAction();

                if (!kb.fKey.isPressed)
                {
                    _holdTimer = 0f;
                    if (_prompt?.holdFill != null) _prompt.holdFill.fillAmount = 0f;
                }
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;
            if (!AcquireTarget(other)) return;
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            _holdTimer = 0f;
            ReleaseTarget();
            InteractionRouter.Unregister(this);
            HidePrompt();
        }

        protected void ShowPrompt()
        {
            if (_prompt != null) return;
            Vector3 pos = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);
            _prompt = WorldPromptPanel.Build(pos, BuildPromptData());
        }

        protected void HidePrompt()
        {
            if (_prompt != null)
            {
                if (_prompt.root != null) Destroy(_prompt.root);
                _prompt = null;
            }
        }

        protected virtual void OnDestroy()
        {
            InteractionRouter.Unregister(this);
            HidePrompt();
        }
    }
}
