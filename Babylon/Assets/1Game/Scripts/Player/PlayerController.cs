using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 玩家控制器 —— Top-down 3D ARPG
    /// WASD 移动，鼠标瞄准方向，Space 闪避
    /// 适配 Frank_Katana 角色模型 + 动画系统
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ItemInventory))]
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(SpiritSlotSystem))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new();

        [Header("闪避")]
        [SerializeField] private float dashDistance = 5f;
        [SerializeField] private float dashDuration = 0.2f;

        [Header("引用")]
        [SerializeField] private Transform modelTransform;

        // 组件缓存
        private CharacterController _cc;
        private ItemInventory _inventory;
        private PlayerCombat _combat;
        private PlayerAnimator _playerAnim;
        private SpiritSlotSystem _spiritSlots;

        // 移动状态
        private Vector3 _moveInput;
        private Vector3 _aimDirection = Vector3.forward;
        private bool _isDashing;
        private float _dashTimer;
        private Vector3 _dashDirection;

        // 闪避充能系统（2层充能）
        private int _dashCharges = 2;
        private int _dashMaxCharges = 2;
        private float _dashRechargeTimer;
        private float _dashRechargeDuration = 1.5f; // 每层充能恢复时间

        // 无敌帧
        private bool _invincible;
        private float _invincibleTimer;
        private const float DASH_INVINCIBLE_TIME = 0.3f; // 闪避无敌帧时长（哈迪斯风格，更宽容）

        // 垂直速度（重力）
        private float _verticalVelocity;

        // 攻击前冲（哈迪斯/梦之行风格：每段攻击有微小的前冲位移）
        private Vector3 _attackLungeVelocity;
        private float _attackLungeTimer;

        // GDD 7.1：每段近战攻击触发后短暂停顿玩家位移，停顿过后允许在攻击动画中继续行走
        // 停顿不会打断 _moveInput 的采集，所以玩家"持续按 WASD"的操作不会被吃掉
        private float _attackMoveLockTimer;
        private const float ATTACK_MOVE_LOCK_TIME = 0.1f;

        // 攻击朝向锁定（攻击瞬间锁定朝向，攻击过程中不跟随鼠标）
        private bool _aimLocked;

        // 本帧是否有闪避请求（用于阻止同帧攻击输入）
        private bool _dashRequestedThisFrame;

        // 属性接口
        public CombatStats Stats => stats;
        /// <summary>本帧是否请求了闪避（供 PlayerCombat 检查，避免同帧攻击抢占闪避）</summary>
        public bool DashRequestedThisFrame => _dashRequestedThisFrame;
        public ItemInventory Inventory => _inventory;
        public SpiritSlotSystem SpiritSlots => _spiritSlots;
        public Vector3 AimDirection => _aimDirection;
        public bool IsDashing => _isDashing;
        public int DashCharges => _dashCharges;
        public int DashMaxCharges => _dashMaxCharges;
        public int MaxDashCharges => _dashMaxCharges;

        /// <summary>调整闪避充能上限（顿悟时刻 / RealmReward 用）</summary>
        public void SetMaxDashCharges(int newMax)
        {
            _dashMaxCharges = Mathf.Max(1, newMax);
            if (_dashCharges > _dashMaxCharges) _dashCharges = _dashMaxCharges;
        }

        /// <summary>把闪避充能补满（顿悟时刻 / 渡劫后用）</summary>
        public void RestoreDashCharge()
        {
            _dashCharges = _dashMaxCharges;
        }

        private bool _dashDisabled;
        /// <summary>开关闪避能力（渡劫期间禁用闪避用）</summary>
        public void SetDashEnabled(bool enabled)
        {
            _dashDisabled = !enabled;
        }

        // 单例（Demo1 简化用）
        public static PlayerController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _inventory = GetComponent<ItemInventory>();
            _combat = GetComponent<PlayerCombat>();
            _playerAnim = GetComponent<PlayerAnimator>();
            _spiritSlots = GetComponent<SpiritSlotSystem>();

            // 从 GameConfig 读取属性
            var config = GameConfig.Instance;
            if (config != null)
            {
                config.ApplyToPlayerStats(stats);
                dashDistance = config.闪避距离;
                dashDuration = config.闪避持续时间;
                _dashMaxCharges = config.闪避充能层数;
                _dashCharges = _dashMaxCharges;
                _dashRechargeDuration = config.闪避冷却时间;
            }

            // StatusEffect 框架（玩家身上）—— 必须在 Inventory.Initialize 之前确保组件存在
            // 因为 RecalculateStats 会从 StatusEffectController 读取 modifiers
            if (GetComponent<StatusEffectController>() == null)
                gameObject.AddComponent<StatusEffectController>();

            // 化身控制器（自动挂载，未选择化身时无副作用）
            if (GetComponent<SpiritRootController>() == null)
                gameObject.AddComponent<SpiritRootController>();

            // 初始化背包系统
            _inventory.Initialize(stats, stats);

            // 初始化灵物槽位系统
            _spiritSlots.Initialize(stats.Clone(), stats);
        }

        private void OnEnable()
        {
            // 监听输入缓冲事件（闪避缓冲从动画系统回调）
            GameEvents.Subscribe<GameEvents.BufferedEvadeRequested>(OnBufferedEvade);
            // 监听攻击前冲事件
            GameEvents.Subscribe<GameEvents.AttackLungeRequested>(OnAttackLunge);
            // 监听连招窗口打开事件（短暂解锁朝向）
            GameEvents.Subscribe<GameEvents.ComboWindowOpened>(OnComboWindowOpened);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.BufferedEvadeRequested>(OnBufferedEvade);
            GameEvents.Unsubscribe<GameEvents.AttackLungeRequested>(OnAttackLunge);
            GameEvents.Unsubscribe<GameEvents.ComboWindowOpened>(OnComboWindowOpened);
        }

        /// <summary>处理缓冲的闪避请求</summary>
        private void OnBufferedEvade(GameEvents.BufferedEvadeRequested evt)
        {
            if (!stats.IsAlive || _dashCharges <= 0) return;
            ExecuteDash();
        }

        /// <summary>
        /// 攻击前冲（哈迪斯/梦之行风格）
        /// 每段攻击触发时给一个短暂的前冲位移脉冲，增加打击感
        /// 同时锁定朝向，攻击过程中不跟随鼠标
        /// </summary>
        private void OnAttackLunge(GameEvents.AttackLungeRequested evt)
        {
            _attackLungeVelocity = _aimDirection * evt.LungeSpeed;
            _attackLungeTimer = evt.LungeDuration;
            _aimLocked = true; // 攻击瞬间锁定朝向

            // GDD 7.1：每段攻击都会出现 0.1s 移动停顿（每段独立刷新）
            _attackMoveLockTimer = ATTACK_MOVE_LOCK_TIME;
        }

        /// <summary>
        /// 连招窗口打开时短暂解锁朝向
        /// 哈迪斯风格：连招段间可以调整攻击方向
        /// </summary>
        private void OnComboWindowOpened(GameEvents.ComboWindowOpened evt)
        {
            _aimLocked = false;
        }

        private void Update()
        {
            if (!stats.IsAlive) return;

            // 每帧开始重置闪避请求标记
            _dashRequestedThisFrame = false;

            // 当攻击/技能结束后自动解锁朝向
            if (_aimLocked && _playerAnim.CurrentPriority < AnimationPriority.Attack)
                _aimLocked = false;

            // 闪避输入最先检测（最高优先级，确保不被攻击输入抢占）
            HandleDash();
            HandleMovementInput();
            HandleAiming();
            UpdateTimers();
            ApplyMovement();
            UpdateAnimation();
        }

        /// <summary>WASD 移动输入</summary>
        private void HandleMovementInput()
        {
            if (_isDashing) return;

            var kb = Keyboard.current;
            if (kb == null) { _moveInput = Vector3.zero; return; }

            float h = 0f, v = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;

            _moveInput = new Vector3(h, 0, v).normalized;
        }

        /// <summary>
        /// 鼠标瞄准（射线投射到地面平面）
        /// 哈迪斯/梦之行风格：攻击时锁定朝向，攻击过程中不跟随鼠标
        /// </summary>
        private void HandleAiming()
        {
            // 攻击/技能中锁定朝向，不跟随鼠标
            if (_aimLocked) return;

            var cam = Camera.main;
            if (cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3 dir = hitPoint - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    _aimDirection = dir.normalized;

                    // 角色朝向瞄准方向
                    if (modelTransform != null)
                        modelTransform.rotation = Quaternion.LookRotation(_aimDirection);
                    else
                        transform.rotation = Quaternion.LookRotation(_aimDirection);
                }
            }
        }

        /// <summary>闪避（Space）—— 哈迪斯风格：最高优先级，可打断一切动作，支持多层充能</summary>
        private void HandleDash()
        {
            if (_isDashing) return;
            if (_dashDisabled) return;  // v0.5 渡劫期间禁用

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                // 标记本帧有闪避输入，阻止同帧的攻击输入（闪避优先级最高）
                _dashRequestedThisFrame = true;

                if (_dashCharges <= 0)
                {
                    // 没有充能，缓冲闪避输入
                    _playerAnim.BufferEvade();
                    return;
                }

                ExecuteDash();
            }
        }

        /// <summary>执行闪避（可由输入或缓冲触发）</summary>
        private void ExecuteDash()
        {
            // 尝试播放闪避动画（会自动打断当前动作）
            if (!_playerAnim.PlayEvade()) return;

            Vector3 dashStartPos = transform.position; // 记录起点（火墙用）

            _isDashing = true;
            _dashTimer = dashDuration;
            _dashDirection = _moveInput.sqrMagnitude > 0.01f ? _moveInput : _aimDirection;

            // 消耗一层充能
            _dashCharges--;
            // 如果充能未满，开始计时恢复
            if (_dashCharges < _dashMaxCharges && _dashRechargeTimer <= 0)
                _dashRechargeTimer = _dashRechargeDuration;

            // 发布闪避充能更新事件
            GameEvents.Publish(new GameEvents.DashChargeUpdate
            {
                CurrentCharges = _dashCharges,
                MaxCharges = _dashMaxCharges,
                RechargeProgress = _dashRechargeTimer > 0 ? 1f - (_dashRechargeTimer / _dashRechargeDuration) : 1f
            });

            // 开启无敌帧（哈迪斯风格：闪避全程无敌）
            _invincible = true;
            _invincibleTimer = DASH_INVINCIBLE_TIME;

            // 质变效果：御风（闪避后留下残影）
            var runner = QualitativeEffectRunner.Instance;
            if (runner != null)
            {
                runner.OnPlayerDash();
                // 协同效果：风火轮（冲刺留火墙）
                Vector3 dashEndPos = dashStartPos + _dashDirection * dashDistance;
                runner.OnPlayerDashForFireTrail(dashStartPos, dashEndPos);
            }
        }

        /// <summary>更新各种计时器</summary>
        private void UpdateTimers()
        {
            float dt = Time.deltaTime;

            // 闪避充能恢复
            if (_dashCharges < _dashMaxCharges)
            {
                _dashRechargeTimer -= dt;
                if (_dashRechargeTimer <= 0)
                {
                    _dashCharges++;
                    // 如果还没满，继续充能下一层
                    if (_dashCharges < _dashMaxCharges)
                        _dashRechargeTimer = _dashRechargeDuration;
                    else
                        _dashRechargeTimer = 0;
                }

                // 发布闪避充能更新
                GameEvents.Publish(new GameEvents.DashChargeUpdate
                {
                    CurrentCharges = _dashCharges,
                    MaxCharges = _dashMaxCharges,
                    RechargeProgress = _dashRechargeTimer > 0 ? 1f - (_dashRechargeTimer / _dashRechargeDuration) : 1f
                });
            }

            if (_invincibleTimer > 0)
            {
                _invincibleTimer -= dt;
                if (_invincibleTimer <= 0)
                    _invincible = false;
            }
        }

        /// <summary>
        /// 应用移动
        /// 哈迪斯/梦之行风格：攻击时完全停止移动，但有前冲位移脉冲
        /// </summary>
        private void ApplyMovement()
        {
            Vector3 velocity;

            if (_isDashing)
            {
                velocity = _dashDirection * (dashDistance / dashDuration);
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0)
                {
                    _isDashing = false;
                    // v0.4 融合层：闪避结束事件（水化身影息斩 / 金化身灵压窗口 30% 概率出现）
                    GameEvents.Publish(new GameEvents.DodgeFinished
                    {
                        EndPosition = transform.position,
                        EndDirection = _dashDirection
                    });
                }
            }
            else
            {
                // GDD 7.1：
                // - 近战攻击：移动不打断攻击，每段攻击后玩家停顿 0.1s，之后可正常移动
                // - 技能 / 受击：仍保持完全停止移动
                float speedMul = 1f;
                var priority = _playerAnim.CurrentPriority;
                if (priority == AnimationPriority.Attack)
                {
                    speedMul = _attackMoveLockTimer > 0 ? 0f : 1f;
                }
                else if (priority > AnimationPriority.Attack)
                {
                    speedMul = 0f;
                }

                if (_attackMoveLockTimer > 0)
                    _attackMoveLockTimer -= Time.deltaTime;

                velocity = _moveInput * stats.moveSpeed * speedMul;

                // 叠加攻击前冲位移（代码驱动的位移脉冲，替代 Root Motion）
                if (_attackLungeTimer > 0)
                {
                    velocity += _attackLungeVelocity;
                    _attackLungeTimer -= Time.deltaTime;
                    // 前冲速度随时间衰减，让停止更自然
                    _attackLungeVelocity *= 0.85f;
                }
            }

            // 应用重力
            if (!_cc.isGrounded)
                _verticalVelocity -= 9.8f * Time.deltaTime;
            else
                _verticalVelocity = -0.5f; // 保持贴地

            velocity.y = _verticalVelocity;

            _cc.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// 更新动画参数
        /// 哈迪斯/梦之行风格：攻击中不播移动动画，但始终传递真实 Speed 值
        /// 这样攻击/闪避/受击结束时，Animator 能根据 Speed 直接切到 Run 而不经过 Idle
        /// </summary>
        private void UpdateAnimation()
        {
            // 始终传递真实的移动速度，让 Animator 在动作结束时能正确判断下一个状态
            float speed = _isDashing ? 0 : _moveInput.magnitude;

            // 计算本地空间的移动方向（用于 BlendTree）
            float moveX = 0, moveZ = 0;
            if (modelTransform != null && _moveInput.sqrMagnitude > 0.01f && speed > 0.01f)
            {
                Vector3 localMove = modelTransform.InverseTransformDirection(_moveInput);
                moveX = localMove.x;
                moveZ = localMove.z;
            }

            _playerAnim.SetMovement(speed, moveX, moveZ);
        }

        // ========== IDamageable 实现 ==========

        /// <summary>
        /// 受到伤害（IDamageable 接口实现）
        /// </summary>
        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            OnDamage(damage, hitPoint, attacker, false);
        }

        /// <summary>
        /// 受到伤害（支持重击参数）
        /// 哈迪斯风格：攻击中有霸体，普通受击不会打断动作（但仍然扣血）
        /// isHeavyHit: 是否为重击（Boss攻击等），可以打断霸体
        /// </summary>
        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker, bool isHeavyHit)
        {
            if (_invincible || !stats.IsAlive) return;

            // 土化身：地脉护盾优先抵挡（仅消耗一次伤害，无视伤害大小）
            var rootCtrl = GetComponent<SpiritRootController>();
            if (rootCtrl != null && rootCtrl.TryConsumeEarthShield())
            {
                Debug.Log("<color=#D4B582>🪨 地脉护盾抵挡了一次伤害</color>");
                return;
            }

            // v0.5 Week 7 · 土化身扎根：在扎根状态下输入伤害额外 ×0.7
            var earthCtrl = GetComponent<SpiritRootEarthController>();
            if (earthCtrl != null && earthCtrl.IsRooted)
            {
                damage = earthCtrl.ScaleIncomingDamage(damage);
            }

            // 金刚不坏协同：30%概率完全格挡并反弹伤害
            if (SynergySystem.IsVajraActive && Random.value < 0.3f)
            {
                Debug.Log("<color=yellow>🛡️ 金刚不坏格挡！反弹伤害！</color>");

                // 飘字：格挡
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 2f,
                    Damage = 0,
                    SpecialTag = "格挡"
                });

                // 反弹50%伤害给攻击者
                if (attacker != null)
                {
                    var attackerDamageable = attacker.GetComponent<IDamageable>();
                    if (attackerDamageable != null)
                    {
                        float reflectDmg = damage * 0.5f;
                        attackerDamageable.OnDamage(reflectDmg, attacker.transform.position, gameObject);

                        // 飘字：反弹伤害
                        GameEvents.Publish(new GameEvents.DamageNumberRequested
                        {
                            WorldPosition = attacker.transform.position + Vector3.up * 1.5f,
                            Damage = reflectDmg,
                            SpecialTag = "反弹"
                        });
                    }
                }
                return; // 完全格挡，不受伤
            }

            // 无论是否被打断，都要扣血（哈迪斯：霸体不等于无敌）
            float actual = stats.TakeDamage(damage);

            // 发布伤害飘字事件
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = false,
                IsPlayerDamage = true
            });

            GameEvents.Publish(new GameEvents.PlayerDamaged
            {
                Damage = actual,
                CurrentHp = stats.currentHp,
                MaxHp = stats.maxHp,
                RawDamage = damage,
                Attacker = attacker
            });

            // v0.5：受到伤害时中断蓄力中的撤离
            ExtractPoint.NotifyPlayerDamaged();

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = stats.currentHp,
                MaxHp = stats.maxHp
            });

            // 尝试播放受击动画（如果有霸体且不是重击，动画不会播放但伤害已扣）
            bool wasInterrupted = _playerAnim.PlayHit(isHeavyHit);

            // 受伤闪烁效果（无论是否被打断都闪烁，给玩家视觉反馈）
            StartCoroutine(DamageFlash());

            // 受击后处理脉冲（屏幕边缘变红）
            if (PostProcessSetup.Instance != null)
                PostProcessSetup.Instance.PulseVignette();

            if (!stats.IsAlive)
            {
                // 尝试玉碎免死
                var runner = QualitativeEffectRunner.Instance;
                if (runner != null && runner.TryJadeShield())
                {
                    // 玉碎触发，免疫致命伤害
                    return;
                }

                OnDeath();
            }
        }

        public void OnDeath()
        {
            // 尝试涅槃复活
            var runner = QualitativeEffectRunner.Instance;
            if (runner != null && runner.TryNirvana())
            {
                Debug.Log("<color=yellow>🔥 涅槃复活！</color>");
                return; // 复活成功，不触发死亡
            }

            Debug.Log("<color=red>玩家死亡！梦境破碎...</color>");
            _playerAnim.PlayDie();
            GameEvents.Publish(new GameEvents.PlayerDied());
        }

        private System.Collections.IEnumerator DamageFlash()
        {
            _invincible = true;
            var renderers = GetComponentsInChildren<Renderer>();

            for (int i = 0; i < 3; i++)
            {
                foreach (var r in renderers)
                    r.enabled = false;
                yield return new WaitForSeconds(0.1f);
                foreach (var r in renderers)
                    r.enabled = true;
                yield return new WaitForSeconds(0.1f);
            }

            _invincible = false;
        }

        /// <summary>设置模型 Transform（运行时创建时使用）</summary>
        public void SetModelTransform(Transform model)
        {
            modelTransform = model;
        }
    }
}
