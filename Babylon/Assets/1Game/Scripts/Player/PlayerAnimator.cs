using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 动画优先级（数值越大优先级越高）
    /// 参考哈迪斯：闪避 > 死亡 > 重击受击 > 技能 > 攻击 > 轻击受击 > 移动/待机
    /// </summary>
    public enum AnimationPriority
    {
        None = 0,       // 无状态（Idle/Run）
        LightHit = 10,  // 轻击受击（不会打断攻击）
        Attack = 20,    // 普通攻击连招
        Skill = 30,     // 技能释放
        HeavyHit = 40,  // 重击受击（可以打断攻击，Boss技能等）
        Evade = 50,     // 闪避（可以打断除死亡外的一切）
        Die = 100,      // 死亡（最高优先级，不可打断）
    }

    /// <summary>
    /// 玩家动画控制器 —— 参考哈迪斯（Hades）的动画优先级系统
    /// 
    /// 核心设计：
    /// 1. 闪避可以打断一切动作（攻击、技能、受击），给玩家最大操控感
    /// 2. 攻击中拥有"霸体"，普通受击不会打断连招
    /// 3. 重击/Boss攻击可以打断霸体
    /// 4. 死亡打断一切
    /// 5. 宽容的输入缓冲系统
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Animator animator;

        // Animator 参数名（Hash 缓存）
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int AttackIndex = Animator.StringToHash("AttackIndex");
        private static readonly int EvadeTrigger = Animator.StringToHash("Evade");
        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DieTrigger = Animator.StringToHash("Die");
        private static readonly int SkillTrigger = Animator.StringToHash("Skill");
        private static readonly int IsAttacking = Animator.StringToHash("IsAttacking");

        // ==================== 状态管理 ====================
        private AnimationPriority _currentPriority = AnimationPriority.None;
        private float _stateTimer;  // 当前状态的安全退出计时器（防止状态卡死）

        // 连招状态
        private int _comboStep;           // 当前连招段数 0/1/2
        private float _comboResetTimer;   // 连招重置计时器
        private bool _canCombo;           // 是否可以接连招（动画事件设置）
        private bool _comboQueued;        // 是否有排队的连招输入

        // 攻击判定窗口
        private bool _attackHitWindowOpen;

        // 输入缓冲
        private float _attackBufferTimer;  // 攻击输入缓冲计时器
        private float _evadeBufferTimer;   // 闪避输入缓冲计时器
        private const float INPUT_BUFFER_TIME = 0.25f; // 输入缓冲窗口（250ms，更宽容）

        // 霸体（超级护甲）
        private bool _superArmor;  // 攻击/技能中是否拥有霸体

        // 攻击速度（影响攻击动画播放速度）
        private float _attackSpeed = 1f;

        // 安全超时（防止动画事件丢失导致状态卡死）
        private const float STATE_TIMEOUT = 3.0f;        private const float SKILL_TIMEOUT = 2.0f;  // 技能动作超时（兜底保护）

        // ==================== 公开属性 ====================

        /// <summary>当前是否在攻击中</summary>
        public bool IsInAttack => _currentPriority == AnimationPriority.Attack;

        /// <summary>攻击判定窗口是否打开</summary>
        public bool IsHitWindowOpen => _attackHitWindowOpen;

        /// <summary>当前连招段数</summary>
        public int ComboStep => _comboStep;

        /// <summary>当前动画优先级</summary>
        public AnimationPriority CurrentPriority => _currentPriority;

        /// <summary>是否拥有霸体（攻击/技能中不会被轻击打断）</summary>
        public bool HasSuperArmor => _superArmor;

        /// <summary>是否处于可被打断的空闲状态</summary>
        public bool IsIdle => _currentPriority == AnimationPriority.None;

        /// <summary>是否正在闪避</summary>
        public bool IsEvading => _currentPriority == AnimationPriority.Evade;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 连招超时重置
            if (_comboResetTimer > 0)
            {
                _comboResetTimer -= dt;
                if (_comboResetTimer <= 0)
                    ResetCombo();
            }

            // 状态安全超时（防止动画事件丢失导致卡死）
            if (_currentPriority != AnimationPriority.None && _currentPriority != AnimationPriority.Die)
            {
                _stateTimer -= dt;
                if (_stateTimer <= 0)
                {
                    Debug.LogWarning($"[PlayerAnimator] 状态超时强制重置！优先级={_currentPriority}");
                    ForceReturnToIdle();
                }

                // 额外安全检测：如果 Animator 已经不在对应状态了（动画播完自动过渡走了），
                // 但代码层优先级还没重置，说明动画事件丢失了，立即修复
                if (animator != null && _currentPriority >= AnimationPriority.Attack)
                {
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    bool inExpectedState = false;

                    switch (_currentPriority)
                    {
                        case AnimationPriority.Attack:
                            inExpectedState = stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2") || stateInfo.IsName("Attack3");
                            break;
                        case AnimationPriority.Skill:
                            inExpectedState = stateInfo.IsName("Skill");
                            break;
                        case AnimationPriority.Evade:
                            inExpectedState = stateInfo.IsName("Evade");
                            break;
                        case AnimationPriority.LightHit:
                        case AnimationPriority.HeavyHit:
                            inExpectedState = stateInfo.IsName("Hit");
                            break;
                    }

                    // 如果 Animator 已经不在预期状态（说明动画事件丢失了），强制重置
                    if (!inExpectedState && !animator.IsInTransition(0))
                    {
                        Debug.LogWarning($"[PlayerAnimator] 检测到动画事件丢失！Animator已离开{_currentPriority}状态，强制重置");
                        var lostPriority = _currentPriority;
                        _currentPriority = AnimationPriority.None;
                        _superArmor = false;
                        _attackHitWindowOpen = false;

                        // 恢复动画播放速度（技能释放时可能修改过）
                        animator.speed = 1f;

                        if (lostPriority == AnimationPriority.Attack)
                            ResetComboInternal();
                        TryConsumeInputBuffer();
                    }
                }
            }

            // 更新输入缓冲计时器
            if (_attackBufferTimer > 0)
            {
                _attackBufferTimer -= dt;
            }
            if (_evadeBufferTimer > 0)
            {
                _evadeBufferTimer -= dt;
            }
        }

        /// <summary>设置 Animator 引用（运行时创建时使用）</summary>
        public void SetAnimator(Animator anim)
        {
            animator = anim;
        }

        // ==================== 优先级判断 ====================

        /// <summary>
        /// 判断新动作是否可以打断当前动作
        /// 核心规则（参考哈迪斯）：
        /// - 闪避可以打断除死亡外的一切
        /// - 死亡可以打断一切
        /// - 攻击中拥有霸体，轻击受击无法打断
        /// - 重击受击可以打断攻击霸体
        /// - 同优先级的攻击可以互相过渡（连招）
        /// </summary>
        private bool CanInterrupt(AnimationPriority newPriority)
        {
            // 死亡状态不可被任何东西打断
            if (_currentPriority == AnimationPriority.Die)
                return false;

            // 死亡可以打断一切
            if (newPriority == AnimationPriority.Die)
                return true;

            // 闪避可以打断除死亡外的一切（哈迪斯核心：给玩家最大操控感）
            if (newPriority == AnimationPriority.Evade)
                return true;

            // 空闲状态可以被任何动作打断
            if (_currentPriority == AnimationPriority.None)
                return true;

            // 闪避中不能被攻击/技能/受击打断（闪避必须播完）
            if (_currentPriority == AnimationPriority.Evade)
                return false;

            // 霸体判断：攻击/技能中，轻击受击无法打断
            if (_superArmor && newPriority == AnimationPriority.LightHit)
                return false;

            // 重击受击可以打断攻击和技能
            if (newPriority == AnimationPriority.HeavyHit)
                return true;

            // 攻击中可以接连招（同优先级特殊处理，由连招系统控制）
            if (_currentPriority == AnimationPriority.Attack && newPriority == AnimationPriority.Attack)
                return true; // 连招逻辑在 RequestAttack 中处理

            // 技能中不能被普通攻击打断
            if (_currentPriority == AnimationPriority.Skill && newPriority == AnimationPriority.Attack)
                return false;

            // 受击中可以被攻击打断（让玩家可以反击）
            if (_currentPriority == AnimationPriority.LightHit && newPriority >= AnimationPriority.Attack)
                return true;

            // 默认：高优先级可以打断低优先级
            return (int)newPriority > (int)_currentPriority;
        }

        // ==================== 移动动画 ====================

        /// <summary>
        /// 更新移动动画参数
        /// 哈迪斯风格：极快的响应速度，几乎没有阻尼
        /// </summary>
        public void SetMovement(float speed, float moveX = 0, float moveZ = 0)
        {
            if (animator == null) return;
            // 哈迪斯风格：移动响应极快，直接设置无阻尼
            // 确保攻击结束瞬间 Speed 值已经是最新的，CrossFade 判断不会出错
            animator.SetFloat(Speed, speed);
            animator.SetFloat(MoveX, moveX);
            animator.SetFloat(MoveZ, moveZ);
        }

        // ==================== 攻击连招 ====================

        /// <summary>
        /// 请求攻击（鼠标左键）
        /// 返回 true 表示成功触发攻击或缓冲了输入
        /// </summary>
        public bool RequestAttack(float attackSpeed = 1f)
        {
            if (animator == null) return false;

            // 缓存攻击速度，用于 StartAttack 中设置动画播放速度
            _attackSpeed = Mathf.Clamp(attackSpeed, 0.5f, 3f);

            // 如果当前不能被攻击打断，缓冲输入
            if (!CanInterrupt(AnimationPriority.Attack))
            {
                // 在闪避/受击中缓冲攻击输入，结束后自动执行
                _attackBufferTimer = INPUT_BUFFER_TIME;
                return true;
            }

            // 如果不在攻击中，直接开始第一段
            if (_currentPriority != AnimationPriority.Attack)
            {
                StartAttack(0);
                return true;
            }

            // 如果在攻击中，缓存输入（无论是否已开启连招窗口）
            if (_comboStep < 2)
            {
                _comboQueued = true;

                // 如果连招窗口已经打开，立即执行下一段
                if (_canCombo)
                {
                    _canCombo = false;
                    _comboQueued = false;
                    StartAttack(_comboStep + 1);
                }
                return true;
            }

            // 第三段攻击中，仍然缓冲输入（攻击结束后自动开始新一轮连招）
            _attackBufferTimer = INPUT_BUFFER_TIME;
            return true;
        }

        /// <summary>开始指定段数的攻击</summary>
        private void StartAttack(int step)
        {
            _comboStep = step;
            _currentPriority = AnimationPriority.Attack;
            _superArmor = true;  // 攻击中开启霸体
            _canCombo = false;
            _comboQueued = false;
            _attackHitWindowOpen = false;
            _stateTimer = STATE_TIMEOUT;

            animator.SetInteger(AttackIndex, step);
            animator.ResetTrigger(AttackTrigger);
            animator.SetTrigger(AttackTrigger);
            animator.SetBool(IsAttacking, true);

            // 设置攻击动画播放速度（受玩家攻击速度属性影响）
            animator.speed = _attackSpeed;

            // 设置连招超时（如果玩家不继续攻击，超时后重置）
            _comboResetTimer = 1.5f;

            // 发布连招段数变化事件
            GameEvents.Publish(new GameEvents.ComboStepChanged
            {
                ComboStep = step,
                IsAttacking = true
            });

            // 哈迪斯/梦之行风格：每段攻击触发前冲位移脉冲
            float lungeSpeed = GetLungeSpeed(step);
            float lungeDuration = 0.12f; // 前冲持续 120ms
            GameEvents.Publish(new GameEvents.AttackLungeRequested
            {
                LungeSpeed = lungeSpeed,
                LungeDuration = lungeDuration
            });
        }

        /// <summary>
        /// 根据连招段数获取前冲速度
        /// 哈迪斯风格：第三段（终结技）前冲更大
        /// </summary>
        private float GetLungeSpeed(int step)
        {
            switch (step)
            {
                case 0: return 4f;  // 第一段：微小前冲
                case 1: return 5f;  // 第二段：稍大前冲
                case 2: return 7f;  // 第三段：明显前冲（终结技）
                default: return 4f;
            }
        }

        // ==================== 闪避 ====================

        /// <summary>
        /// 播放闪避动画
        /// 哈迪斯核心：闪避优先级最高（除死亡外），可以打断一切动作
        /// 使用 CrossFade 强制切换，不依赖 Trigger（避免 Animator 过渡中吞掉 Trigger）
        /// </summary>
        public bool PlayEvade()
        {
            if (animator == null) return false;

            if (!CanInterrupt(AnimationPriority.Evade))
                return false;

            // 打断当前状态
            if (_currentPriority == AnimationPriority.Attack)
            {
                ResetComboInternal();
                animator.speed = 1f; // 恢复攻击时修改的动画速度
            }

            _currentPriority = AnimationPriority.Evade;
            _superArmor = false;  // 闪避中不需要霸体（有无敌帧）
            _stateTimer = STATE_TIMEOUT;

            // 清除所有待处理的 Trigger，防止闪避结束后误触发
            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(HitTrigger);
            animator.ResetTrigger(SkillTrigger);
            animator.ResetTrigger(EvadeTrigger);

            // 使用 CrossFade 强制切换到闪避动画
            // 不依赖 Trigger 机制，因为 Animator 在过渡中可能吞掉 Trigger
            animator.CrossFade("Evade", 0.05f, 0);

            return true;
        }

        // ==================== 受击 / 死亡 ====================

        /// <summary>
        /// 播放受击动画
        /// isHeavyHit: 是否为重击（Boss攻击等），重击可以打断霸体
        /// </summary>
        public bool PlayHit(bool isHeavyHit = false)
        {
            if (animator == null) return false;

            AnimationPriority hitPriority = isHeavyHit ? AnimationPriority.HeavyHit : AnimationPriority.LightHit;

            if (!CanInterrupt(hitPriority))
            {
                // 霸体生效，不播放受击动画，但仍然可以扣血（由外部处理）
                return false;
            }

            // 打断当前状态
            if (_currentPriority == AnimationPriority.Attack)
            {
                ResetComboInternal();
                animator.speed = 1f; // 恢复攻击时修改的动画速度
            }

            _currentPriority = hitPriority;
            _superArmor = false;
            _stateTimer = STATE_TIMEOUT;

            animator.ResetTrigger(AttackTrigger);
            animator.SetTrigger(HitTrigger);

            return true;
        }

        /// <summary>播放死亡动画（最高优先级，打断一切）</summary>
        public void PlayDie()
        {
            if (animator == null) return;

            ResetComboInternal();
            _currentPriority = AnimationPriority.Die;
            _superArmor = false;
            animator.speed = 1f; // 恢复动画播放速度
            _stateTimer = float.MaxValue; // 死亡不超时

            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(HitTrigger);
            animator.ResetTrigger(EvadeTrigger);
            animator.ResetTrigger(SkillTrigger);
            animator.SetTrigger(DieTrigger);
        }

        // ==================== 技能 ====================

        /// <summary>播放技能动画</summary>
        /// <param name="castSpeed">技能动画播放速度倍率（1.0 = 默认速度）</param>
        public bool PlaySkill(float castSpeed = 1f)
        {
            if (animator == null) return false;

            if (!CanInterrupt(AnimationPriority.Skill))
            {
                // 技能中再按技能，不缓冲（避免连续释放）
                return false;
            }

            // 打断当前状态
            if (_currentPriority == AnimationPriority.Attack)
            {
                ResetComboInternal();
            }

            _currentPriority = AnimationPriority.Skill;
            _superArmor = true;  // 技能中也有霸体
            _stateTimer = SKILL_TIMEOUT;  // 技能用更长的超时

            // 清除所有待处理的 Trigger，防止技能结束后误触发
            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(SkillTrigger);
            animator.ResetTrigger(HitTrigger);
            animator.SetBool(IsAttacking, false);

            // 设置技能动画播放速度
            animator.speed = Mathf.Clamp(castSpeed, 0.5f, 3f);

            // 使用 CrossFade 强制切换到技能动画（和闪避一样，不依赖 Trigger）
            // Trigger 在 Animator 过渡中可能被吞掉，CrossFade 更可靠
            animator.CrossFade("Skill", 0.05f, 0);

            return true;
        }

        // ==================== 动画事件回调 ====================

        /// <summary>动画事件：开启连招窗口（可以接下一段攻击）</summary>
        public void OnComboWindowOpen()
        {
            _canCombo = true;

            // 哈迪斯风格：连招窗口打开时短暂解锁朝向，让玩家可以调整下一段攻击的方向
            GameEvents.Publish(new GameEvents.ComboWindowOpened());

            // 如果有排队的输入，立即执行下一段
            if (_comboQueued && _comboStep < 2)
            {
                StartAttack(_comboStep + 1);
            }
        }

        /// <summary>动画事件：关闭连招窗口</summary>
        public void OnComboWindowClose()
        {
            _canCombo = false;
        }

        /// <summary>动画事件：开启攻击判定窗口</summary>
        public void OnHitWindowOpen()
        {
            _attackHitWindowOpen = true;
        }

        /// <summary>动画事件：关闭攻击判定窗口</summary>
        public void OnHitWindowClose()
        {
            _attackHitWindowOpen = false;
        }

        /// <summary>动画事件：攻击动画结束</summary>
        public void OnAttackEnd()
        {
            _attackHitWindowOpen = false;
            _canCombo = false;
            _superArmor = false;
            animator.SetBool(IsAttacking, false);

            // 恢复动画播放速度
            animator.speed = 1f;

            // 给一小段时间让玩家可以继续连招
            _comboResetTimer = 0.4f;

            // 回到空闲状态
            _currentPriority = AnimationPriority.None;

            // 发布连招结束事件
            GameEvents.Publish(new GameEvents.ComboStepChanged
            {
                ComboStep = 0,
                IsAttacking = false
            });

            // 直接通过代码切换动画状态，不依赖 Animator 的 exitTime 过渡
            // 这样攻击结束后立即响应移动，不会卡在攻击动画尾帧
            if (animator != null)
            {
                animator.ResetTrigger(AttackTrigger);
                // 直接读取当前输入的移动状态来决定切换目标
                // 注意：这里不能用 GetFloat(Speed) 因为攻击中 Speed 可能还没更新到最新值
                // 通过 PlayerController 的 _moveInput 来判断更准确
                float currentSpeed = animator.GetFloat(Speed);
                if (currentSpeed > 0.05f)
                    animator.CrossFade("Run", 0.05f, 0);  // 极短过渡，几乎瞬切
                else
                    animator.CrossFade("Idle", 0.08f, 0);
            }

            // 检查输入缓冲：如果闪避/攻击在缓冲中，立即执行
            TryConsumeInputBuffer();
        }

        /// <summary>动画事件：闪避动画结束</summary>
        public void OnEvadeEnd()
        {
            _currentPriority = AnimationPriority.None;

            if (animator != null)
            {
                animator.ResetTrigger(EvadeTrigger);
                float currentSpeed = animator.GetFloat(Speed);
                if (currentSpeed > 0.05f)
                    animator.CrossFade("Run", 0.05f, 0);
                else
                    animator.CrossFade("Idle", 0.08f, 0);
            }

            TryConsumeInputBuffer();
        }

        /// <summary>动画事件：受击动画结束</summary>
        public void OnHitEnd()
        {
            _currentPriority = AnimationPriority.None;

            if (animator != null)
            {
                animator.ResetTrigger(HitTrigger);
                float currentSpeed = animator.GetFloat(Speed);
                if (currentSpeed > 0.05f)
                    animator.CrossFade("Run", 0.05f, 0);
                else
                    animator.CrossFade("Idle", 0.08f, 0);
            }

            TryConsumeInputBuffer();
        }

        /// <summary>动画事件：技能动画结束</summary>
        public void OnSkillEnd()
        {
            _currentPriority = AnimationPriority.None;
            _superArmor = false;

            if (animator != null)
            {
                // 恢复动画播放速度（技能释放时可能修改过）
                animator.speed = 1f;

                // 清除可能残留的 Skill Trigger，防止误触发
                animator.ResetTrigger(SkillTrigger);

                // 技能动画的 clip.length 可能远大于实际动作时长（FBX 中有额外帧）
                // 不能依赖 exitTime 过渡（会在动画末尾才触发），需要直接切换
                // 根据当前是否有移动输入，直接 CrossFade 到 Run 或 Idle
                float currentSpeed = animator.GetFloat(Speed);
                if (currentSpeed > 0.05f)
                    animator.CrossFade("Run", 0.05f, 0);
                else
                    animator.CrossFade("Idle", 0.08f, 0);
            }

            // 检查输入缓冲：如果技能中缓冲了攻击/闪避，立即执行
            TryConsumeInputBuffer();
        }

        /// <summary>动画事件：播放刀光特效</summary>
        public void OnSlashVFX()
        {
            GameEvents.Publish(new GameEvents.SlashVFXRequested
            {
                ComboStep = _comboStep
            });
        }

        // ==================== 输入缓冲系统 ====================

        /// <summary>
        /// 尝试消费输入缓冲
        /// 当一个动作结束时，检查是否有缓冲的输入需要执行
        /// </summary>
        private void TryConsumeInputBuffer()
        {
            // 闪避缓冲优先（哈迪斯：闪避优先级最高）
            if (_evadeBufferTimer > 0)
            {
                _evadeBufferTimer = 0;
                // 通过事件通知 PlayerController 执行闪避
                // （因为闪避涉及位移逻辑，不能只在动画层处理）
                GameEvents.Publish(new GameEvents.BufferedEvadeRequested());
                return;
            }

            // 攻击缓冲（使用已缓存的攻击速度）
            if (_attackBufferTimer > 0)
            {
                _attackBufferTimer = 0;
                RequestAttack(_attackSpeed);
            }
        }

        /// <summary>缓冲闪避输入</summary>
        public void BufferEvade()
        {
            _evadeBufferTimer = INPUT_BUFFER_TIME;
        }

        // ==================== 工具方法 ====================

        /// <summary>内部重置连招状态（不改变优先级）</summary>
        private void ResetComboInternal()
        {
            _comboStep = 0;
            _canCombo = false;
            _comboQueued = false;
            _attackHitWindowOpen = false;
            _comboResetTimer = 0;

            if (animator != null)
                animator.SetBool(IsAttacking, false);
        }

        /// <summary>重置连招状态（公开方法，同时重置优先级）</summary>
        public void ResetCombo()
        {
            ResetComboInternal();
            _currentPriority = AnimationPriority.None;
            _superArmor = false;
        }

        /// <summary>强制回到空闲状态（安全超时用）</summary>
        private void ForceReturnToIdle()
        {
            ResetComboInternal();
            _currentPriority = AnimationPriority.None;
            _superArmor = false;
            _attackBufferTimer = 0;
            _evadeBufferTimer = 0;

            if (animator != null)
            {
                animator.ResetTrigger(AttackTrigger);
                animator.ResetTrigger(HitTrigger);
                animator.ResetTrigger(EvadeTrigger);
                animator.ResetTrigger(SkillTrigger);
                animator.SetBool(IsAttacking, false);
                animator.speed = 1f; // 恢复动画播放速度

                // 强制 Animator 切回 Idle/Run（通过 CrossFade 直接打断当前状态）
                // 这解决了超时重置后 Animator 仍在播放旧动画的问题
                animator.Play("Idle", 0, 0f);
            }
        }
    }
}
