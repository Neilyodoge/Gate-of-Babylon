using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 玩家战斗系统 —— 近战挥刀连招 + 功法技能
    /// 鼠标左键：三段连招（S1_Combo01_01 → 02 → 03）
    /// Q：功法技能槽位
    /// </summary>
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("近战攻击")]
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeAngle = 120f;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask enemyLayer;

        /// <summary>暴露给其他战斗组件（如金化身灵压爆发）使用，避免重复配置 LayerMask 引发自伤 bug</summary>
        public LayerMask EnemyLayer => enemyLayer;

        [Header("刀光特效")]
        [SerializeField] private GameObject slashVFXPrefab;
        [SerializeField] private Transform slashVFXSpawnPoint;

        [Header("打击特效")]
        [SerializeField] private GameObject hitVFXPrefab;

        [Header("技能槽位")]
        [SerializeField] private SkillData skillQ;
        [SerializeField] private SkillData skillE;
        [SerializeField] private SkillData skillR;

        [Header("Debug 可视化")]
        [SerializeField] private bool showDebugVisuals = true;

        private PlayerController _player;
        private PlayerAnimator _playerAnim;
        private ModuleSlotManager _moduleSlots;

        // 技能充能系统（每个槽位独立充能）
        private int[] _skillCharges = new int[3];       // 当前充能层数
        private int[] _skillMaxCharges = new int[3];    // 最大充能层数
        private float[] _skillRechargeTimer = new float[3]; // 充能恢复计时器
        private float[] _skillRechargeDuration = new float[3]; // 每层充能恢复时间
        private int[] _chargeBonusFromItems = new int[3]; // 额外充能层数

        // 蓄力系统
        private int _chargingSlot = -1;          // 当前正在蓄力的技能槽位（-1=未蓄力）
        private float _chargeTimer = 0f;          // 蓄力计时器
        private int _currentChargeLevel = 1;      // 当前蓄力等级
        private float _originalMoveSpeed;         // 蓄力前的移速（用于恢复）
        private bool _chargeMoveSpeedApplied;     // 是否已应用蓄力减速

        // V.08 增强注入上下文：BeginEnhancement 在 cast 前设置，cast 中读取，EndEnhancement/Clear 后失效
        private bool _enhActive;                  // 本次 cast 是否注入增强（仅 Enhancement 角色）
        private float _enhDamageMul = 1f;         // 核心技能伤害倍率
        private ElementTag _enhElement = ElementTag.None; // 核心技能元素覆盖（None=不覆盖）
        private float _enhRadiusMult = 1f;        // 核心技能范围倍率（形态改造·扩散）
        private float _enhProjectileMult = 1f;    // 核心技能投射物数量倍率（形态改造·连锁）
        private int _enhExtraProjectiles;         // 核心技能额外投射物数（形态改造·额外飞弹）
        private int _enhChainCount;               // 核心技能投射物链锁弹射次数（形态改造·链锁）
        private bool _enhSurround;                 // 核心投射技 360° 环绕发射（形态改造·环绕）
        private bool _enhSustained;                 // 核心范围技留下持续地带（节奏改造·持续）
        private bool _enhDelayedBlast;              // 核心范围技追加延迟重爆（节奏改造·延迟爆炸）
        private bool _enhTargetFarthest;            // 核心投射技自动锁定范围内最远敌（目标改造·最远）
        private ShapeMode _enhShape = ShapeMode.None; // 核心投射技发射形态（形态改造：Wall/Ring/Zone）
        private bool _chargeEnhPending;           // 蓄力技能：释放时是否消费 Proc
        private readonly System.Collections.Generic.List<GameObject> _enhHitTargets = new(); // 本次核心技能命中的敌人（增强控制/状态用）
        private ChainConfig _enhCfg;              // 本次增强的编译配置（投射物 payload 用）
        private bool _enhWorldDelegated;          // 控制/状态已交由世界对象（投射物命中/区域 tick）施加 → EndEnhancement 不再绕玩家回退

        // 攻击判定：每段攻击只判定一次
        private bool _hasHitThisSwing;
        private int _lastHitComboStep = -1;

        // ==================== 远程普攻（法系主角） ====================
        // 由 PlayerCharacterProfile 配置：勾选后左键不再做近战扇形判定，
        // 而是在挥击动画事件（OnSlashVFX）点向瞄准方向发射一枚投射物。
        private bool _rangedBasic;
        private GameObject _basicProjPrefab;
        private float _basicProjSpeed = 18f;
        private ElementTag _basicElement = ElementTag.None;
        private float _basicDamageMul = 1f;

        /// <summary>当前主角是否为远程普攻（法系）。</summary>
        public bool IsRangedBasic => _rangedBasic;

        /// <summary>由主角档案配置普攻形态（剑客近战 / 法师远程）。</summary>
        public void ConfigureBasicAttack(PlayerCharacterProfile profile)
        {
            if (profile == null)
            {
                _rangedBasic = false;
                return;
            }
            _rangedBasic = profile.rangedBasicAttack;
            _basicProjPrefab = profile.basicProjectilePrefab;
            _basicProjSpeed = profile.basicProjectileSpeed > 0.01f ? profile.basicProjectileSpeed : 18f;
            _basicElement = profile.basicElement;
            _basicDamageMul = profile.basicDamageMultiplier > 0.01f ? profile.basicDamageMultiplier : 1f;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _playerAnim = GetComponent<PlayerAnimator>();
        }

        public ModuleSlotManager ModuleSlots => _moduleSlots;

        private void EnsureModuleSlots()
        {
            if (_moduleSlots == null)
                _moduleSlots = GetComponent<ModuleSlotManager>();
            EnsureAutoSubscription();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.SlashVFXRequested>(OnSlashVFXRequested);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.SlashVFXRequested>(OnSlashVFXRequested);
        }

        /// <summary> EnsureModuleSlots 后订阅 Auto 自动消费事件（只订阅一次）</summary>
        private bool _autoSubscribed;

        private void EnsureAutoSubscription()
        {
            if (_autoSubscribed || _moduleSlots == null) return;
            _moduleSlots.OnAutoConsume += HandleAutoConsume;
            _autoSubscribed = true;
        }

        private void Update()
        {
            if (!_player.Stats.IsAlive || _player.IsDashing)
            {
                if (_chargingSlot >= 0)
                    CancelCharging();
                return;
            }

            EnsureModuleSlots();
            HandleMeleeAttack();
            HandleSkills();
            UpdateCooldowns();
            CheckMeleeHit();
        }

        /// <summary>取消蓄力（不释放技能）</summary>
        private void CancelCharging()
        {
            if (_chargingSlot < 0) return;

            int slot = _chargingSlot;
            RestoreChargeMoveSpeed();
            _chargingSlot = -1;
            _chargeTimer = 0f;
            _currentChargeLevel = 1;

            GameEvents.Publish(new GameEvents.SkillChargeProgress
            {
                SlotIndex = slot,
                ChargeTime = 0f,
                ChargeLevel = 1,
                IsCharging = false
            });

            Debug.Log("<color=gray>蓄力被中断</color>");
        }

        // ==================== 近战攻击 ====================

        /// <summary>鼠标左键触发近战连招</summary>
        private void HandleMeleeAttack()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // 哈迪斯风格：如果同帧按了闪避，闪避优先，跳过攻击输入
                if (_player.DashRequestedThisFrame) return;

                // 鼠标在UI槽位上时不攻击（拖拽或点击槽位）
                if (SkillBarUI.Instance != null && SkillBarUI.Instance.IsMouseOverSlot) return;

                _playerAnim.RequestAttack(_player.Stats.attackSpeed);
            }
        }

        /// <summary>在攻击判定窗口内检测敌人</summary>
        private void CheckMeleeHit()
        {
            // 远程主角：普攻不做近战扇形判定，伤害由 OnSlashVFXRequested 发射的投射物结算
            if (_rangedBasic) return;

            if (!_playerAnim.IsHitWindowOpen)
            {
                // 非攻击判定窗口时也绘制攻击范围（淡色）
                if (showDebugVisuals)
                    DrawAttackRange(new Color(1f, 0.5f, 0.1f, 0.3f));
                return;
            }

            // 攻击判定窗口打开时绘制攻击范围（亮色）
            if (showDebugVisuals)
                DrawAttackRange(new Color(1f, 0.2f, 0.1f, 1f));

            // 每段攻击只判定一次
            if (_lastHitComboStep == _playerAnim.ComboStep && _hasHitThisSwing) return;

            // 扇形范围检测
            // 注意：attackOrigin / slashVFXSpawnPoint 都挂在 playerGo 根节点上（根节点不旋转，只有 modelTransform 跟随瞄准方向）。
            // 所以这里把它们的 localPosition 当成"沿瞄准方向的偏移"（x=侧向, y=高度, z=前向距离），用 AimDirection 旋转后得到世界坐标。
            // 修复前直接用 attackOrigin.position 会导致命中球永远偏在角色北侧，不跟随瞄准。
            Vector3 origin = GetAimRelativeWorldPos(attackOrigin, transform.position + Vector3.up * 0.8f);
            Vector3 forward = _player.AimDirection;

            var colliders = Physics.OverlapSphere(origin, meleeRange, enemyLayer);
            bool hitAny = false;
            GameObject firstHitTarget = null;
            Vector3 firstHitPoint = origin;

            foreach (var col in colliders)
            {
                // 检查是否在扇形角度内
                Vector3 dirToTarget = (col.transform.position - origin).normalized;
                dirToTarget.y = 0;
                float angle = Vector3.Angle(forward, dirToTarget);

                if (angle <= meleeAngle * 0.5f)
                {
                    var damageable = col.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float damageMultiplier = GetComboDamageMultiplier(_playerAnim.ComboStep);
                        float targetDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (damage, _) = _player.Stats.CalcMeleeDamage(targetDef);
                        damage *= damageMultiplier;

                        Vector3 hitPoint = col.ClosestPoint(origin);

                        // 天地大挪移：普攻不伤敌，转化为自身治疗
                        if (HeavenEarthShift.IsActive)
                        {
                            float heal = damage * 0.5f;
                            _player.Stats.currentHp = Mathf.Min(_player.Stats.maxHp, _player.Stats.currentHp + heal);
                            GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = _player.Stats.currentHp, MaxHp = _player.Stats.maxHp });
                            GameEvents.Publish(new GameEvents.DamageNumberRequested { WorldPosition = hitPoint + Vector3.up * 1.5f, Damage = heal, SpecialTag = "挪移·治疗" });
                            SpawnHitVFX(hitPoint);
                            hitAny = true;
                            continue;
                        }

                        damageable.OnDamage(damage, hitPoint, gameObject);

                        // 播放打击特效
                        SpawnHitVFX(hitPoint);
                        hitAny = true;
                        if (firstHitTarget == null)
                        {
                            firstHitTarget = col.gameObject;
                            firstHitPoint = hitPoint;
                        }
                    }
                }
            }

            if (hitAny)
            {
                _hasHitThisSwing = true;
                _lastHitComboStep = _playerAnim.ComboStep;

                // v0.3.3 融合层：发布命中事件（木化身种种子、金化身触发完美窗口等订阅）
                int comboStep = _playerAnim.ComboStep;
                GameEvents.Publish(new GameEvents.MeleeHitConnected
                {
                    ComboStep = comboStep,
                    HitPoint = firstHitPoint,
                    Target = firstHitTarget
                });

                // v0.3.3 融合层维度一·A：Attack3 命中 → 随机减 1 个 CD 中技能 10%
                if (comboStep == 2)
                    ReduceRandomSkillCooldown(0.10f);
            }
        }

        /// <summary>立即把全部 3 个技能槽 CD 清零（顿悟时刻 / 灵机一动 buff 用）。</summary>
        public void ResetAllCooldowns()
        {
            SkillData[] skills = { skillQ, skillE, skillR };
            for (int i = 0; i < 3; i++)
            {
                _skillRechargeTimer[i] = 0f;
                PublishSkillChargeUpdate(i, skills[i]);
            }
        }

        /// <summary>
        /// 融合层 · 减少一个正在 CD 中的随机技能的 cooldown（按当前 timer 的百分比）。
        /// </summary>
        private void ReduceRandomSkillCooldown(float percent)
        {
            // 收集所有处于充能中的技能槽位
            System.Collections.Generic.List<int> activeSlots = null;
            for (int i = 0; i < 3; i++)
            {
                if (_skillRechargeTimer[i] > 0.01f)
                {
                    activeSlots ??= new System.Collections.Generic.List<int>();
                    activeSlots.Add(i);
                }
            }
            if (activeSlots == null || activeSlots.Count == 0) return;

            int pickSlot = activeSlots[Random.Range(0, activeSlots.Count)];
            float before = _skillRechargeTimer[pickSlot];
            _skillRechargeTimer[pickSlot] = Mathf.Max(0f, _skillRechargeTimer[pickSlot] - before * percent);

            // 同步刷新 HUD
            SkillData[] skills = { skillQ, skillE, skillR };
            PublishSkillChargeUpdate(pickSlot, skills[pickSlot]);

            // 飘字反馈
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.4f,
                Damage = 0,
                SpecialTag = $"-{(before * percent):F1}s CD"
            });
        }

        /// <summary>连招段数伤害倍率</summary>
        private float GetComboDamageMultiplier(int comboStep)
        {
            var config = GameConfig.Instance;
            if (config != null)
            {
                switch (comboStep)
                {
                    case 0: return config.第一段伤害倍率;
                    case 1: return config.第二段伤害倍率;
                    case 2: return config.第三段伤害倍率;
                    default: return 1.0f;
                }
            }

            switch (comboStep)
            {
                case 0: return 1.0f;
                case 1: return 1.2f;
                case 2: return 1.5f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// 把 attackOrigin / slashVFXSpawnPoint 的 localPosition 转换成跟随瞄准方向的世界坐标。
        /// 因为 PlayerController 只让 modelTransform 旋转，根节点不转，
        /// 直接读 pivot.position 会得到一个与 AimDirection 无关的固定偏移点。
        /// </summary>
        private Vector3 GetAimRelativeWorldPos(Transform pivot, Vector3 fallback)
        {
            if (pivot == null) return fallback;
            Vector3 localOffset = pivot.localPosition;
            Vector3 aim = _player != null ? _player.AimDirection : transform.forward;
            if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;
            Quaternion aimRot = Quaternion.LookRotation(aim);
            return transform.position + aimRot * localOffset;
        }

        // ==================== 特效 ====================

        /// <summary>动画事件触发刀光特效</summary>
        private void OnSlashVFXRequested(GameEvents.SlashVFXRequested evt)
        {
            // 重置判定状态（新的一段攻击开始）
            _hasHitThisSwing = false;

            // 远程主角（法系）：挥击动画到点 → 发射一枚法术投射物
            if (_rangedBasic)
                FireBasicProjectile();

            if (slashVFXPrefab == null) return;

            // 与命中判定同源：把 spawnPoint.localPosition 当成沿瞄准方向的偏移
            // 否则刀光视觉会出现在角色固定的"北侧"，与玩家朝向不一致，造成视觉与命中错位
            Vector3 spawnPos = GetAimRelativeWorldPos(
                slashVFXSpawnPoint,
                transform.position + _player.AimDirection * 1f + Vector3.up * 1f);

            Quaternion rot = Quaternion.LookRotation(_player.AimDirection);

            GameObject vfx;
            if (ObjectPool.Instance != null)
            {
                vfx = ObjectPool.Instance.Get(slashVFXPrefab, spawnPos, rot);
                ObjectPool.Instance.Return(vfx, 1.5f);
            }
            else
            {
                vfx = Instantiate(slashVFXPrefab, spawnPos, rot);
                Destroy(vfx, 1.5f);
            }
        }

        /// <summary>
        /// 远程主角普攻：向瞄准方向发射一枚投射物。
        /// 伤害走近战公式（含连招段倍率），命中时由 Projectile 结算目标防御/穿甲。
        /// </summary>
        private void FireBasicProjectile()
        {
            Vector3 spawnPos = GetAimRelativeWorldPos(
                attackOrigin, transform.position + _player.AimDirection * 0.6f + Vector3.up * 0.9f);
            Vector3 dir = _player.AimDirection;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            Quaternion rot = Quaternion.LookRotation(dir);

            float comboMul = GetComboDamageMultiplier(_playerAnim.ComboStep);
            var (damage, _) = _player.Stats.CalcMeleeDamage(0f);
            damage *= comboMul * _basicDamageMul;

            if (_basicProjPrefab != null)
            {
                GameObject proj;
                if (ObjectPool.Instance != null)
                    proj = ObjectPool.Instance.Get(_basicProjPrefab, spawnPos, rot);
                else
                    proj = Instantiate(_basicProjPrefab, spawnPos, rot);

                var projectile = proj.GetComponent<Projectile>();
                if (projectile != null)
                    projectile.Initialize(damage, dir, _basicProjSpeed, 0, 0, _basicElement, _player, _player.Stats.armorPenPercent);
            }
            else if (showDebugVisuals)
            {
                CreateDebugProjectile(spawnPos, dir, _basicProjSpeed, damage, 1f, _basicElement);
            }

            // 命中事件（融合层：木化身种子、金化身窗口等订阅）—— 远程普攻也算一次"出手"
            int comboStep = _playerAnim.ComboStep;
            if (comboStep == 2)
                ReduceRandomSkillCooldown(0.10f);
        }

        /// <summary>生成打击特效</summary>
        private void SpawnHitVFX(Vector3 hitPoint)
        {
            if (hitVFXPrefab == null) return;

            GameObject vfx;
            if (ObjectPool.Instance != null)
            {
                vfx = ObjectPool.Instance.Get(hitVFXPrefab, hitPoint, Quaternion.identity);
                ObjectPool.Instance.Return(vfx, 1f);
            }
            else
            {
                vfx = Instantiate(hitVFXPrefab, hitPoint, Quaternion.identity);
                Destroy(vfx, 1f);
            }
        }

        // ==================== 技能 ====================

        /// <summary>
        /// 技能释放——GDD V.07 模块链驱动：
        /// · 被动模式：条件满足后自动释放
        /// · 主动模式：条件满足后图标亮起，按 Q/E/R 手动释放
        /// · 若槽位无模块链，回退到旧 SkillData（过渡兼容）
        /// </summary>
        private void HandleSkills()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            SkillData[] skills = { skillQ, skillE, skillR };
            var keys = new[] { kb.qKey, kb.eKey, kb.rKey };

            // 蓄力中（旧 SkillData 兼容）
            if (_chargingSlot >= 0)
            {
                var skill = skills[_chargingSlot];
                var key = keys[_chargingSlot];

                if (skill == null || !key.isPressed)
                {
                    ReleaseChargedSkill();
                    return;
                }

                _chargeTimer += Time.deltaTime;
                int newLevel = skill.GetChargeLevel(_chargeTimer);
                if (newLevel != _currentChargeLevel)
                {
                    _currentChargeLevel = newLevel;
                    Debug.Log($"<color=yellow>蓄力等级提升 → Lv{_currentChargeLevel}！</color>");
                }

                GameEvents.Publish(new GameEvents.SkillChargeProgress
                {
                    SlotIndex = _chargingSlot,
                    ChargeTime = _chargeTimer,
                    ChargeLevel = _currentChargeLevel,
                    IsCharging = true
                });
                return;
            }

            // ===== V.08 统一：按键 → 释放核心技能 → 若链 Proc 则注入增强 + 消费 =====
            for (int i = 0; i < 3; i++)
            {
                if (skills[i] == null || _skillCharges[i] <= 0) continue;
                if (!keys[i].wasPressedThisFrame) continue;

                // 蓄力技能：进入蓄力状态，增强在 ReleaseChargedSkill 时注入
                if (skills[i].canCharge)
                {
                    _chargeEnhPending = _moduleSlots != null && _moduleSlots.HasChain(i) && _moduleSlots.IsProc(i);
                    StartCharging(i, skills[i]);
                    return;
                }

                // 链 Proc → cast 前准备增强上下文
                bool willEnhance = _moduleSlots != null && _moduleSlots.HasChain(i) && _moduleSlots.IsProc(i);
                if (willEnhance) BeginEnhancement(i);

                bool cast = UseSkill(skills[i], i, 1);
                if (cast)
                {
                    ConsumeSkillCharge(i, skills[i]);

                    if (willEnhance)
                    {
                        EndEnhancement(i, skills[i]);
                        _moduleSlots.ConsumeProc(i);
                    }
                }
                else if (willEnhance)
                {
                    ClearEnhancement();
                }
                return; // 每帧只释放一个技能
            }
        }

        /// <summary>Auto 模式 Proc 时由 ModuleSlotManager 回调：自动释放绑定核心技能 + 注入增强。</summary>
        private void HandleAutoConsume(int slot)
        {
            if (slot < 0 || slot >= 3) return;
            SkillData[] skills = { skillQ, skillE, skillR };
            var skill = skills[slot];
            if (skill == null || _skillCharges[slot] <= 0)
            {
                Debug.Log($"<color=yellow>[Auto] 槽 {slot} 无可用核心技能或充能，跳过自动释放</color>");
                return;
            }

            // 不能在死亡/闪避中自动释放
            var priority = _playerAnim.CurrentPriority;
            if (priority == AnimationPriority.Die || priority == AnimationPriority.Evade) return;

            BeginEnhancement(slot);
            bool cast = UseSkill(skill, slot, 1);
            if (cast)
            {
                ConsumeSkillCharge(slot, skill);
                EndEnhancement(slot, skill);
                // Auto 模式 tracker 已自动消费，无需 ConsumeProc
                Debug.Log($"<color=#44ff88>[Auto] 自动释放 {skill.skillName} + 增强注入</color>");
            }
            else
            {
                ClearEnhancement();
            }
        }

        /// <summary>
        /// V0.1.13 消费爆发层：Proc 被消费瞬间的表现反馈——角色处爆闪光 + 元素色特效环 + 镜头震屏。
        /// 强度随 consumeKind 分级（Single/Window 强，Stacks/Auto 弱），程序化零美术资源。
        /// </summary>
        private void PlayConsumeBurst(ChainConfig cfg)
        {
            if (_player == null) return;
            Vector3 p = _player.transform.position + Vector3.up * 1f;
            ElementTag e = cfg.elementTag != ElementTag.None ? cfg.elementTag : ElementFromStatus(cfg);
            Color col = SkillModifierApplier.ColorOf(e);

            // 角色特效：元素色爆闪环（快速放大消失）
            SkillModifierApplier.SpawnCubeVfx(p, col, 2.4f, 0.22f);

            // 爆闪：瞬时明亮点光源
            var lightGo = new GameObject("ConsumeBurstFlash");
            lightGo.transform.position = p;
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = col;
            lt.range = 8f;
            lt.intensity = 6f;
            Destroy(lightGo, 0.14f);

            // 震屏：按 consumeKind 分级
            switch (cfg.consumeKind)
            {
                case ConsumeKind.Auto:
                case ConsumeKind.Stacks:
                    CameraShake.TriggerLight();
                    break;
                default:
                    CameraShake.TriggerMedium();
                    break;
            }
        }

        /// <summary>
        /// V.08 增强注入（cast 前）：仅 Enhancement 角色设置核心技能的伤害倍率 + 元素覆盖。
        /// Addon 角色不改核心技能（在 EndEnhancement spawn 独立效果）。
        /// </summary>
        private void BeginEnhancement(int slot)
        {
            if (_moduleSlots == null) return;
            var cfg = _moduleSlots.GetConfig(slot);

            // V0.1.13 消费爆发层：任意 Proc 被消费瞬间的爆闪 + 角色特效 + 震屏（Enhancement/Addon 皆触发）
            PlayConsumeBurst(cfg);

            if (cfg.effectRole != EffectRole.Enhancement) return;

            _enhActive = true;
            _enhDamageMul = cfg.enhanceDamageMult > 0.01f ? cfg.enhanceDamageMult : 1f;
            _enhElement = cfg.elementTag != ElementTag.None ? cfg.elementTag : ElementFromStatus(cfg);
            _enhRadiusMult = cfg.enhanceRadiusMult > 0.01f ? cfg.enhanceRadiusMult : 1f;
            _enhProjectileMult = cfg.enhanceProjectileMult > 0.01f ? cfg.enhanceProjectileMult : 1f;
            _enhExtraProjectiles = Mathf.Max(0, cfg.enhanceExtraProjectiles);
            _enhChainCount = Mathf.Max(0, cfg.enhanceChainCount);
            _enhSurround = cfg.enhanceSurround;
            _enhSustained = cfg.enhanceSustained;
            _enhDelayedBlast = cfg.enhanceDelayedBlast;
            _enhTargetFarthest = cfg.enhanceTargetFarthest;
            _enhShape = cfg.enhanceShape;
            _enhHitTargets.Clear();
            _enhCfg = cfg;
            _enhWorldDelegated = false;
        }

        /// <summary>
        /// V.08 增强注入（cast 后）：
        /// · Enhancement → 即时自益（heal/shield/invincible）或对范围敌人施加控制/状态；伤害倍率/元素已在 cast 中生效。
        /// · Addon → spawn 独立世界效果（ExecuteChainEffect）。
        /// </summary>
        private void EndEnhancement(int slot, SkillData coreSkill)
        {
            if (_moduleSlots == null) { ClearEnhancement(); return; }
            var cfg = _moduleSlots.GetConfig(slot);
            var chain = _moduleSlots.GetChain(slot);

            // 种子引爆（状态型触发器）：消费时在每颗世界种子位置触发接入效果器的伤害/元素/状态。
            if (cfg.triggerType == TriggerType.SeedDetonate)
            {
                int detonated = SeedSystem.HasInstance ? SeedSystem.Instance.DetonateAll(cfg, _player, enemyLayer) : 0;
                Debug.Log($"<color=#66ff66>[种子引爆] 引爆 {detonated} 颗种子</color>");
                ShowChainProcNotification(chain.DisplayName, cfg.elementTag);
                ClearEnhancement();
                return;
            }

            string role = cfg.effectRole == EffectRole.Enhancement ? "增强" : "附加";
            Debug.Log($"<color=#44ff88>[增强] 槽 {slot} · {coreSkill.skillName} · {role} · {cfg.consumeKind} · ×{cfg.enhanceDamageMult:F2}</color>");

            if (cfg.effectRole == EffectRole.Enhancement)
                ApplyEnhancementSelf(cfg, chain);
            else
                ExecuteChainEffect(slot);

            ClearEnhancement();
        }

        /// <summary>Enhancement 角色：即时自益 / 范围控制 / 附加状态。伤害倍率与元素覆盖已在核心技能 cast 中生效。</summary>
        private void ApplyEnhancementSelf(ChainConfig cfg, ModuleChain chain)
        {
            bool handledTargets = false;
            switch (cfg.effectType)
            {
                case EffectType.Heal:
                    ExecuteChainHeal(cfg);
                    break;
                case EffectType.Shield:
                    ExecuteChainShield(cfg, chain);
                    break;
                case EffectType.Invincible:
                    ExecuteChainInvincible(cfg, chain);
                    break;
                case EffectType.Cleanse:
                    var status = _player.GetComponent<StatusEffectController>();
                    if (status != null) status.ClearDebuffs();
                    break;
                case EffectType.Slow:
                case EffectType.Stun:
                case EffectType.Knockback:
                case EffectType.MarkVulnerable:
                    if (_enhWorldDelegated)
                    {
                        // 控制随世界对象（投射物命中/区域 tick）施加，无需此处处理
                    }
                    else if (_enhHitTargets.Count > 0)
                    {
                        // 作用到核心技能实际命中的敌人（含 modifier 状态）
                        foreach (var t in _enhHitTargets)
                            ApplyControlToEnemy(cfg, t, transform.position);
                    }
                    else
                    {
                        ExecuteChainControl(cfg); // 核心技能未命中（或非范围）→ 回退绕玩家
                    }
                    handledTargets = true;
                    break;
            }

            // 纯属性增强（无控制/自益效果）但带 modifier 状态 → 优先附加到命中敌人，否则绕玩家
            if (!handledTargets && !_enhWorldDelegated && (cfg.addBurn || cfg.addFreeze || cfg.addPoison))
            {
                if (_enhHitTargets.Count > 0)
                {
                    foreach (var t in _enhHitTargets)
                        ApplyChainStatusEffects(cfg, t);
                }
                else
                {
                    ApplyEnhStatusAroundPlayer(cfg);
                }
            }
        }

        /// <summary>对玩家周围敌人施加 modifier 附加状态（灼烧/冰冻/毒），用于纯属性增强链。</summary>
        private void ApplyEnhStatusAroundPlayer(ChainConfig cfg)
        {
            float radius = cfg.radius > 0f ? cfg.radius : 5f;
            var hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);
            foreach (var hit in hits)
                ApplyChainStatusEffects(cfg, hit.gameObject);
        }

        private void ClearEnhancement()
        {
            _enhActive = false;
            _enhDamageMul = 1f;
            _enhElement = ElementTag.None;
            _enhRadiusMult = 1f;
            _enhProjectileMult = 1f;
            _enhExtraProjectiles = 0;
            _enhChainCount = 0;
            _enhSurround = false;
            _enhSustained = false;
            _enhDelayedBlast = false;
            _enhTargetFarthest = false;
            _enhShape = ShapeMode.None;
        }

        /// <summary>从 modifier 附加状态推断元素（用于元素覆盖）。</summary>
        private static ElementTag ElementFromStatus(ChainConfig cfg)
        {
            if (cfg.addBurn) return ElementTag.Fire;
            if (cfg.addFreeze) return ElementTag.Ice;
            if (cfg.addLightning) return ElementTag.Thunder;
            if (cfg.addPoison) return ElementTag.Earth;
            return ElementTag.None;
        }

        /// <summary>核心技能元素：增强激活且有覆盖时用覆盖元素，否则用技能本身元素。</summary>
        private ElementTag EnhElem(SkillData skill)
            => _enhActive && _enhElement != ElementTag.None ? _enhElement : skill.elementTag;

        /// <summary>开始蓄力</summary>
        private void StartCharging(int slotIndex, SkillData skill)
        {
            _chargingSlot = slotIndex;
            _chargeTimer = 0f;
            _currentChargeLevel = 1;
            _chargeMoveSpeedApplied = false;

            // 应用蓄力减速
            if (skill.chargeMoveSpeedMultiplier < 1f && _player != null)
            {
                _originalMoveSpeed = _player.Stats.moveSpeed;
                _player.Stats.moveSpeed *= skill.chargeMoveSpeedMultiplier;
                _chargeMoveSpeedApplied = true;
            }

            Debug.Log($"<color=yellow>开始蓄力：{skill.skillName}</color>");

            GameEvents.Publish(new GameEvents.SkillChargeProgress
            {
                SlotIndex = slotIndex,
                ChargeTime = 0f,
                ChargeLevel = 1,
                IsCharging = true
            });
        }

        /// <summary>释放蓄力技能</summary>
        private void ReleaseChargedSkill()
        {
            if (_chargingSlot < 0) return;

            int slot = _chargingSlot;
            int chargeLevel = _currentChargeLevel;
            SkillData[] skills = { skillQ, skillE, skillR };
            var skill = skills[slot];

            // 恢复移速
            RestoreChargeMoveSpeed();

            // 重置蓄力状态
            _chargingSlot = -1;
            _chargeTimer = 0f;
            _currentChargeLevel = 1;

            // 发布蓄力结束事件
            GameEvents.Publish(new GameEvents.SkillChargeProgress
            {
                SlotIndex = slot,
                ChargeTime = 0f,
                ChargeLevel = 1,
                IsCharging = false
            });

            if (skill == null) { _chargeEnhPending = false; ClearEnhancement(); return; }

            // 释放技能（带蓄力等级）+ V.08 增强注入
            if (_chargeEnhPending) BeginEnhancement(slot);
            if (UseSkill(skill, slot, chargeLevel))
            {
                ConsumeSkillCharge(slot, skill);

                if (_chargeEnhPending && _moduleSlots != null)
                {
                    EndEnhancement(slot, skill);
                    _moduleSlots.ConsumeProc(slot);
                }

                GameEvents.Publish(new GameEvents.SkillChargeReleased
                {
                    SlotIndex = slot,
                    ChargeLevel = chargeLevel,
                    Skill = skill
                });

                if (chargeLevel > 1)
                    Debug.Log($"<color=cyan>蓄力释放 Lv{chargeLevel}：{skill.skillName}（伤害×{skill.GetChargeDamageMultiplier(chargeLevel):F1}）</color>");
            }
            else if (_chargeEnhPending)
            {
                ClearEnhancement();
            }
            _chargeEnhPending = false;
        }

        /// <summary>恢复蓄力减速</summary>
        private void RestoreChargeMoveSpeed()
        {
            if (_chargeMoveSpeedApplied && _player != null)
            {
                _player.Stats.moveSpeed = _originalMoveSpeed;
                _chargeMoveSpeedApplied = false;
            }
        }

        /// <summary>消耗一层技能充能</summary>
        private void ConsumeSkillCharge(int slotIndex, SkillData skill)
        {
            _skillCharges[slotIndex]--;
            // 如果充能未满且没在恢复中，开始恢复
            if (_skillCharges[slotIndex] < _skillMaxCharges[slotIndex] && _skillRechargeTimer[slotIndex] <= 0)
            {
                float rechargeTime = skill.chargeTime > 0 ? skill.chargeTime : SkillTuning.EffectiveCooldown(skill);

                _skillRechargeTimer[slotIndex] = rechargeTime;
                _skillRechargeDuration[slotIndex] = rechargeTime;
            }

            // 发布充能更新事件
            PublishSkillChargeUpdate(slotIndex, skill);
        }

        /// <summary>发布技能充能更新事件</summary>
        private void PublishSkillChargeUpdate(int slotIndex, SkillData skill)
        {
            float rechargeProgress = _skillRechargeTimer[slotIndex] > 0 && _skillRechargeDuration[slotIndex] > 0
                ? 1f - (_skillRechargeTimer[slotIndex] / _skillRechargeDuration[slotIndex])
                : 1f;

            GameEvents.Publish(new GameEvents.SkillCooldownUpdate
            {
                SlotIndex = slotIndex,
                RemainingTime = _skillRechargeTimer[slotIndex],
                TotalCooldown = _skillRechargeDuration[slotIndex] > 0 ? _skillRechargeDuration[slotIndex] : (skill != null ? SkillTuning.EffectiveCooldown(skill) : 1f)
            });
        }

        /// <summary>初始化技能槽位的充能数据</summary>
        private void InitSkillCharges(int slotIndex, SkillData skill)
        {
            if (skill != null)
            {
                int baseCharges = Mathf.Max(1, skill.maxCharges);
                _skillMaxCharges[slotIndex] = Mathf.Clamp(baseCharges + _chargeBonusFromItems[slotIndex], 1, 3);
                _skillCharges[slotIndex] = _skillMaxCharges[slotIndex];
            }
            else
            {
                _skillMaxCharges[slotIndex] = 1;
                _skillCharges[slotIndex] = 1;
            }
            _skillRechargeTimer[slotIndex] = 0;
            _skillRechargeDuration[slotIndex] = 0;
        }

        // ==================== 充能加成 ====================

        /// <summary>增加技能槽位的充能上限</summary>
        public void AddChargeBonus(int skillSlotIndex, int bonus)
        {
            if (skillSlotIndex < 0 || skillSlotIndex >= 3) return;
            _chargeBonusFromItems[skillSlotIndex] += bonus;
            // 重新初始化充能
            SkillData[] skills = { skillQ, skillE, skillR };
            if (skills[skillSlotIndex] != null)
            {
                int oldCharges = _skillCharges[skillSlotIndex];
                InitSkillCharges(skillSlotIndex, skills[skillSlotIndex]);
                // 保持当前充能不减少
                _skillCharges[skillSlotIndex] = Mathf.Max(oldCharges, _skillCharges[skillSlotIndex]);
                PublishSkillChargeUpdate(skillSlotIndex, skills[skillSlotIndex]);
                Debug.Log($"<color=green>技能{skillSlotIndex}充能上限+{bonus} → {_skillMaxCharges[skillSlotIndex]}层</color>");
            }
        }

        /// <summary>移除技能槽位的充能上限加成</summary>
        public void RemoveChargeBonus(int skillSlotIndex, int bonus)
        {
            if (skillSlotIndex < 0 || skillSlotIndex >= 3) return;
            _chargeBonusFromItems[skillSlotIndex] = Mathf.Max(0, _chargeBonusFromItems[skillSlotIndex] - bonus);
            // 重新初始化充能
            SkillData[] skills = { skillQ, skillE, skillR };
            if (skills[skillSlotIndex] != null)
            {
                InitSkillCharges(skillSlotIndex, skills[skillSlotIndex]);
                PublishSkillChargeUpdate(skillSlotIndex, skills[skillSlotIndex]);
                Debug.Log($"<color=gray>技能{skillSlotIndex}充能上限-{bonus} → {_skillMaxCharges[skillSlotIndex]}层</color>");
            }
        }

        /// <summary>获取技能槽位的实际充能上限</summary>
        public int GetMaxCharges(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 3) return 1;
            return _skillMaxCharges[slotIndex];
        }

        /// <summary>获取技能槽位的当前充能层数</summary>
        public int GetCurrentCharges(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 3) return 0;
            return _skillCharges[slotIndex];
        }

        /// <summary>是否正在蓄力</summary>
        public bool IsCharging => _chargingSlot >= 0;

        /// <summary>当前蓄力的技能槽位</summary>
        public int ChargingSlot => _chargingSlot;

        /// <summary>当前蓄力等级</summary>
        public int CurrentChargeLevel => _currentChargeLevel;

        /// <summary>使用技能（返回是否成功释放）</summary>
        private bool UseSkill(SkillData skill, int slotIndex, int chargeLevel = 1)
        {
            if (skill == null) return false;

            // v0.3.3 融合层：发布技能开始事件（金化身灵压窗口订阅）
            GameEvents.Publish(new GameEvents.SkillCastStarted
            {
                SlotIndex = slotIndex,
                Skill = skill
            });

            // Buff类技能立即生效，不需要播放技能动画
            if (skill.skillType == SkillType.Buff)
            {
                Debug.Log($"<color=cyan>释放功法：{skill.skillName}</color>");
                CastBuffSkill(skill, slotIndex);
                return true;
            }

            // Heal类技能也立即生效
            if (skill.skillType == SkillType.Heal)
            {
                Debug.Log($"<color=cyan>释放功法：{skill.skillName}</color>");
                CastHealSkill(skill);
                return true;
            }

            // 计算技能释放速度：优先使用技能自身配置，否则使用全局配置
            float castSpeed = skill.castSpeed > 0.01f ? skill.castSpeed : 1f;
            var config = GameConfig.Instance;
            if (config != null && Mathf.Approximately(castSpeed, 1f))
                castSpeed = config.技能释放速度;

            // 根据配置决定是否播放技能动画
            if (skill.playAnimation)
            {
                // 尝试播放技能动画（遵循优先级系统）
                if (!_playerAnim.PlaySkill(castSpeed)) return false;
            }
            else
            {
                // 不播放动画：仅检查当前状态是否允许释放（不能在死亡/闪避中释放）
                var priority = _playerAnim.CurrentPriority;
                if (priority == AnimationPriority.Die || priority == AnimationPriority.Evade)
                    return false;
            }

            // 计算蓄力加成
            float chargeDmgMul = skill.GetChargeDamageMultiplier(chargeLevel);
            float chargeRadiusMul = skill.GetChargeRadiusMultiplier(chargeLevel);

            // V.08：Enhancement 增强注入伤害倍率
            if (_enhActive) chargeDmgMul *= _enhDamageMul;

            string chargeSuffix = chargeLevel > 1 ? $" [蓄力Lv{chargeLevel}]" : "";
            Debug.Log($"<color=cyan>释放功法：{skill.skillName}{chargeSuffix}</color>");

            switch (skill.skillType)
            {
                case SkillType.AreaDamage:
                    CastAreaSkill(skill, chargeDmgMul, chargeRadiusMul, slotIndex);
                    break;
                case SkillType.Projectile:
                    CastProjectileSkill(skill, chargeDmgMul);
                    break;
                case SkillType.Dash:
                    CastDashSkill(skill);
                    break;
                case SkillType.Buff:
                    CastBuffSkill(skill, slotIndex);
                    break;
                case SkillType.Zone:
                    CastZoneSkill(skill, chargeDmgMul);
                    break;
                case SkillType.Heal:
                    CastHealSkill(skill);
                    return true; // Heal不需要动画
                case SkillType.Summon:
                    CastSummonSkill(skill);
                    break;
            }

            return true;
        }

        // ==================== 模块链效果执行 ====================

        /// <summary>执行模块链效果——根据 ChainConfig.effectType 分发到对应效果实现。</summary>
        private void ExecuteChainEffect(int slot)
        {
            var cfg = _moduleSlots.GetConfig(slot);
            var chain = _moduleSlots.GetChain(slot);

            var priority = _playerAnim.CurrentPriority;
            if (priority == AnimationPriority.Die || priority == AnimationPriority.Evade)
                return;

            // 代价改造：消耗生命
            if (cfg.costHPPercent > 0f)
            {
                float cost = _player.Stats.maxHp * cfg.costHPPercent;
                _player.Stats.currentHp = Mathf.Max(1f, _player.Stats.currentHp - cost);
                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = _player.Stats.currentHp, MaxHp = _player.Stats.maxHp
                });
            }

            string modeTag = cfg.executionMode == ExecutionMode.Passive ? "被动" : "主动";
            Debug.Log($"<color=#00ffcc>[{modeTag}] 模块链触发 [{SlotLabel(slot)}]：{chain.DisplayName}</color>");
            ShowChainProcNotification(chain.DisplayName, cfg.elementTag);

            switch (cfg.effectType)
            {
                // 伤害输出
                case EffectType.AreaDamage:
                    ExecuteChainArea(cfg);
                    break;
                case EffectType.Projectile:
                case EffectType.SwordWave:
                    ExecuteChainProjectile(cfg);
                    break;
                case EffectType.DoT:
                    ExecuteChainDoT(cfg);
                    break;

                // 控制/状态
                case EffectType.Slow:
                case EffectType.Stun:
                case EffectType.Knockback:
                case EffectType.MarkVulnerable:
                    ExecuteChainControl(cfg);
                    break;

                // 防御/回复
                case EffectType.Heal:
                    ExecuteChainHeal(cfg);
                    break;
                case EffectType.Shield:
                    ExecuteChainShield(cfg, chain);
                    break;
                case EffectType.Invincible:
                    ExecuteChainInvincible(cfg, chain);
                    break;

                // 位移
                case EffectType.Dash:
                    ExecuteChainDash(cfg);
                    break;
                case EffectType.Pull:
                    ExecuteChainPull(cfg);
                    break;
            }
        }

        private void ShowChainProcNotification(string chainName, ElementTag element)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("ChainProc");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, 120);
            rt.sizeDelta = new Vector2(500, 60);

            var text = go.AddComponent<UnityEngine.UI.Text>();
            text.text = $"★ {chainName} ★";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            Color c = element switch
            {
                ElementTag.Fire => new Color(1f, 0.4f, 0.1f),
                ElementTag.Water => new Color(0.2f, 0.6f, 1f),
                ElementTag.Earth => new Color(0.9f, 0.8f, 0.3f),
                ElementTag.Wind => new Color(0.3f, 1f, 0.6f),
                _ => new Color(0f, 1f, 0.8f)
            };
            text.color = c;

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            Destroy(go, 1.5f);
        }

        private void ExecuteChainArea(ChainConfig cfg)
        {
            var cam = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam == null || mouse == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, transform.position);
            if (!groundPlane.Raycast(ray, out float dist)) return;

            Vector3 targetPos = ray.GetPoint(dist);
            float radius = cfg.radius;

            if (cfg.vfxPrefab != null)
            {
                GameObject vfx = ObjectPool.Instance != null
                    ? ObjectPool.Instance.Get(cfg.vfxPrefab, targetPos, Quaternion.identity)
                    : Instantiate(cfg.vfxPrefab, targetPos, Quaternion.identity);
                float dur = 1.5f;
                if (ObjectPool.Instance != null) ObjectPool.Instance.Return(vfx, dur);
                else Destroy(vfx, dur);
            }
            else if (showDebugVisuals)
            {
                FxFactory.SpawnElementBurst(targetPos + Vector3.up * 0.05f, cfg.elementTag, radius, 0.8f);
            }

            var hits = Physics.OverlapSphere(targetPos, radius, enemyLayer);
            foreach (var hit in hits)
            {
                var dmgable = hit.GetComponent<IDamageable>();
                if (dmgable == null) continue;

                float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                float skillBase = cfg.damage + _player.Stats.attackDamage * cfg.damageScaling;
                float sMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage);
                var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, sMul);
                dmgable.OnDamage(dmg, hit.transform.position, gameObject);

                ApplyChainStatusEffects(cfg, hit.gameObject);
            }
        }

        private void ExecuteChainProjectile(ChainConfig cfg)
        {
            Vector3 spawnPos = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 dir = _player.AimDirection;

            float skillBase = cfg.damage + _player.Stats.attackDamage * cfg.damageScaling;
            float sMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage);
            var (damage, _) = _player.Stats.CalcSkillDamage(0f, sMul);

            int count = Mathf.Max(1, cfg.projectileCount);
            float halfSpread = cfg.spreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector3 projDir = dir;
                if (count > 1)
                {
                    float angle = Mathf.Lerp(-halfSpread, halfSpread, (float)i / (count - 1));
                    projDir = Quaternion.Euler(0, angle, 0) * dir;
                }

                if (showDebugVisuals)
                    CreateDebugProjectile(spawnPos, projDir, cfg.projectileSpeed, damage, 1.5f, cfg.elementTag);
            }
        }

        private void ExecuteChainHeal(ChainConfig cfg)
        {
            float heal = cfg.healAmount + _player.Stats.attackDamage * cfg.healScaling;
            float oldHp = _player.Stats.currentHp;
            _player.Stats.currentHp = Mathf.Min(_player.Stats.currentHp + heal, _player.Stats.maxHp);
            float actual = _player.Stats.currentHp - oldHp;

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _player.Stats.currentHp,
                MaxHp = _player.Stats.maxHp
            });
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2f,
                Damage = actual,
                SpecialTag = "治疗"
            });

            if (showDebugVisuals)
                CreateDebugHealIndicator(1.5f);
        }

        private void ExecuteChainShield(ChainConfig cfg, ModuleChain chain)
        {
            float dur = cfg.buffDuration > 0f ? cfg.buffDuration : 5f;

            var mods = new System.Collections.Generic.List<StatModifier>();
            if (cfg.buffDamageReduction != 0f)
                mods.Add(StatModifier.Flat(StatType.DamageReduction, cfg.buffDamageReduction));
            if (mods.Count == 0)
                mods.Add(StatModifier.Flat(StatType.DamageReduction, 0.3f));

            var status = _player.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.Apply(new StatusEffect
                {
                    id = $"module_buff_{chain.DisplayName}",
                    isBuff = true,
                    elementTag = cfg.elementTag,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = dur,
                    duration = dur,
                    modifiers = mods,
                    displayName = chain.DisplayName,
                    description = "模块链增益",
                    uiColor = SkillModifierApplier.ColorOf(cfg.elementTag)
                });
            }

            if (showDebugVisuals)
            {
                Color c = cfg.elementTag != ElementTag.None
                    ? SkillModifierApplier.ColorOf(cfg.elementTag)
                    : new Color(0.3f, 0.8f, 1f, 0.3f);
                c.a = 0.35f;
                CreateDebugShieldIndicator(dur, c);
            }
        }

        private void ExecuteChainDoT(ChainConfig cfg)
        {
            Vector3 targetPos = GetMouseWorldPos();
            float radius = cfg.radius > 0f ? cfg.radius : 3f;

            if (showDebugVisuals)
                FxFactory.SpawnElementBurst(targetPos + Vector3.up * 0.05f, cfg.elementTag, radius, cfg.dotDuration);

            var hits = Physics.OverlapSphere(targetPos, radius, enemyLayer);
            foreach (var hit in hits)
            {
                if (cfg.dotDPS > 0f)
                    SkillModifierApplier.ApplyBurn(hit.gameObject, cfg.dotDPS, cfg.dotDuration);
                ApplyChainStatusEffects(cfg, hit.gameObject);
            }
        }

        private void ExecuteChainControl(ChainConfig cfg)
        {
            Vector3 center = transform.position;
            float radius = cfg.radius > 0f ? cfg.radius : 5f;
            var hits = Physics.OverlapSphere(center, radius, enemyLayer);

            if (showDebugVisuals)
                FxFactory.SpawnElementBurst(center + Vector3.up * 0.05f, cfg.elementTag, radius, 0.6f);

            foreach (var hit in hits)
                ApplyControlToEnemy(cfg, hit.gameObject, center);
        }

        /// <summary>对单个敌人施加控制（减速/眩晕/击退/易伤）+ modifier 附加状态。center 用于计算击退方向。</summary>
        private void ApplyControlToEnemy(ChainConfig cfg, GameObject target, Vector3 center)
            => SkillModifierApplier.ApplyEnhancementToEnemy(cfg, target, center, _player);

        private void ExecuteChainInvincible(ChainConfig cfg, ModuleChain chain)
        {
            float dur = cfg.invincibleDuration > 0f ? cfg.invincibleDuration : 1f;
            var status = _player.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.Apply(new StatusEffect
                {
                    id = $"module_invincible_{chain.DisplayName}",
                    isBuff = true,
                    elementTag = cfg.elementTag,
                    stacks = 1, maxStacks = 1,
                    defaultDuration = dur, duration = dur,
                    modifiers = new() { StatModifier.Flat(StatType.DamageReduction, 1f) },
                    displayName = "无敌",
                    description = "短暂无敌",
                    uiColor = Color.yellow
                });
            }
            if (showDebugVisuals)
                CreateDebugShieldIndicator(dur, new Color(1f, 1f, 0f, 0.4f));
        }

        private void ExecuteChainDash(ChainConfig cfg)
        {
            Vector3 dir = _player.AimDirection;
            float dist = cfg.dashDistance > 0f ? cfg.dashDistance : 5f;
            _player.transform.position += dir * dist;

            float radius = 2f;
            var hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);
            if (cfg.damage > 0f)
            {
                foreach (var hit in hits)
                {
                    var dmgable = hit.GetComponent<IDamageable>();
                    if (dmgable == null) continue;
                    float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                    float skillBase = cfg.damage + _player.Stats.attackDamage * cfg.damageScaling;
                    float sMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage);
                    var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, sMul);
                    dmgable.OnDamage(dmg, hit.transform.position, gameObject);
                    ApplyChainStatusEffects(cfg, hit.gameObject);
                }
            }
        }

        private void ExecuteChainPull(ChainConfig cfg)
        {
            float radius = cfg.pullRadius > 0f ? cfg.pullRadius : 6f;
            var hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);

            if (showDebugVisuals)
                FxFactory.SpawnElementBurst(transform.position + Vector3.up * 0.05f, cfg.elementTag, radius, 0.5f);

            foreach (var hit in hits)
            {
                var dir = (transform.position - hit.transform.position).normalized;
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(dir * cfg.knockbackForce, ForceMode.Impulse);
                ApplyChainStatusEffects(cfg, hit.gameObject);
            }
        }

        /// <summary>应用改造件附加的状态效果</summary>
        private static void ApplyChainStatusEffects(ChainConfig cfg, GameObject target)
            => SkillModifierApplier.ApplyEnhancementStatus(cfg, target);

        private Vector3 GetMouseWorldPos()
        {
            var cam = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam == null || mouse == null) return transform.position;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, transform.position);
            return groundPlane.Raycast(ray, out float dist)
                ? ray.GetPoint(dist)
                : transform.position;
        }

        private static string SlotLabel(int slot) => slot switch { 0 => "Q", 1 => "E", 2 => "R", _ => "?" };

        /// <summary>范围伤害技能（如落石术），支持蓄力倍率</summary>
        private void CastAreaSkill(SkillData skill, float damageMul = 1f, float radiusMul = 1f, int slotIndex = -1)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, transform.position);

                if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPos = ray.GetPoint(distance);
                float actualRadius = skill.aoeRadius * radiusMul;
                if (_enhActive) actualRadius *= _enhRadiusMult; // 形态改造·扩散
                System.Collections.Generic.List<Collider> hitList = null;

                if (skill.vfxPrefab != null)
                {
                    GameObject vfx;
                    if (ObjectPool.Instance != null)
                    {
                        vfx = ObjectPool.Instance.Get(skill.vfxPrefab, targetPos, Quaternion.identity);
                        ObjectPool.Instance.Return(vfx, skill.vfxDuration);
                    }
                    else
                    {
                        vfx = Instantiate(skill.vfxPrefab, targetPos, Quaternion.identity);
                        Destroy(vfx, skill.vfxDuration);
                    }
                    // 蓄力时放大VFX
                    if (radiusMul > 1f)
                        vfx.transform.localScale *= radiusMul;
                }
                else if (showDebugVisuals)
                {
                    // v0.3.3 视觉差异化：按 ElementTag 出不同颜色 / 形状的元素爆发，而不是统一红 cube
                    FxFactory.SpawnElementBurst(targetPos + Vector3.up * 0.05f, EnhElem(skill), actualRadius, Mathf.Max(0.4f, skill.vfxDuration * 0.8f));
                }

                var hits = Physics.OverlapSphere(targetPos, actualRadius, enemyLayer);
                hitList = new System.Collections.Generic.List<Collider>(hits);
                GameObject firstSkillHit = null;
                foreach (var hit in hits)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float targetDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        float damage;
                        if (skill.damageFromRunTotal)
                            damage = RunCombatStats.TotalPlayerDamage * skill.runTotalDamageRatio * damageMul;
                        else
                        {
                            float skillBase = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
                            var (d, _) = _player.Stats.CalcSkillDamage(targetDef, skillBase / Mathf.Max(1f, _player.Stats.attackDamage));
                            damage = d * damageMul;
                        }
                        damageable.OnDamage(damage, hit.transform.position, gameObject);
                        if (firstSkillHit == null) firstSkillHit = hit.gameObject;
                        if (_enhActive) _enhHitTargets.Add(hit.gameObject);

                        if (skill.freezeOnHitChance > 0f && Random.value < skill.freezeOnHitChance)
                            SkillModifierApplier.ApplyFreeze(hit.gameObject, skill.freezeOnHitDuration);
                    }
                }

                // v0.3.3 融合层：技能命中事件（木化身种子引爆 / 水化身水痕收割 等）
                if (firstSkillHit != null)
                {
                    GameEvents.Publish(new GameEvents.SkillHitConnected
                    {
                        SlotIndex = slotIndex,
                        Skill = skill,
                        HitPoint = firstSkillHit.transform.position,
                        Target = firstSkillHit
                    });
                }

                // 技能自身元素命中表现（cube 颜色提示 + 灼烧 / 冻结 / 雷击）
                var castElem = EnhElem(skill);
                if (castElem != ElementTag.None)
                {
                    SkillModifierApplier.ApplyElementImpact(castElem, targetPos, hitList, _player);
                }

                // GDD 6.5：槽位修饰落地触发（灼烧 / 冻结 / 雷击 / 持续地带）
                if (slotIndex >= 0)
                {
                    SkillModifierApplier.ApplyAreaSkill(
                        skill,
                        slotIndex,
                        targetPos,
                        actualRadius,
                        hitList,
                        _player,
                        enemyLayer);
                }

                // V.08 节奏改造·持续：增强让瞬发范围技在落点留下持续地带（DoT + 每 tick 附加状态）
                if (_enhActive && _enhSustained)
                {
                    float skillBase = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
                    float perTickMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage) * 0.35f * damageMul;
                    var zone = ActiveSkillZone.SpawnCustom(targetPos, _player, enemyLayer,
                        actualRadius, 4f, 0.5f, perTickMul, castElem);
                    if (zone != null)
                    {
                        zone.SetEnhancement(_enhCfg, castElem, 1f);
                        if (_enhCfg.addBurn || _enhCfg.addFreeze || _enhCfg.addPoison)
                            _enhWorldDelegated = true; // 状态随地带 tick 施加
                    }
                }

                // V.08 节奏改造·延迟爆炸：增强让范围技在落点追加一次带预警的延迟重爆
                if (_enhActive && _enhDelayedBlast)
                {
                    float skillBase = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
                    float blastMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage) * 1.5f * damageMul; // 延迟换爆发
                    bool hasStatus = _enhCfg.addBurn || _enhCfg.addFreeze || _enhCfg.addPoison;
                    DelayedAreaBlast.Spawn(targetPos, 0.8f, actualRadius * 1.15f, _player, enemyLayer, blastMul, castElem, _enhCfg, hasStatus);
                    if (hasStatus) _enhWorldDelegated = true; // 状态随延迟爆炸施加
                }
            }
        }

        /// <summary>区域技能（混沌吞噬/天罡北斗阵/九天玄火阵/冥河召唤）：召唤一个持续作用区域。</summary>
        private void CastZoneSkill(SkillData skill, float damageMul = 1f)
        {
            Vector3 spawnPos = transform.position;
            if (!skill.zoneFollowPlayer)
            {
                var cam = Camera.main;
                var mouse = Mouse.current;
                if (cam != null && mouse != null)
                {
                    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                    var groundPlane = new Plane(Vector3.up, transform.position);
                    if (groundPlane.Raycast(ray, out float d))
                        spawnPos = ray.GetPoint(d);
                }
            }

            var zone = ActiveSkillZone.Spawn(skill, spawnPos, _player, enemyLayer, damageMul);
            if (_enhActive && zone != null)
            {
                // V.08：增强注入区域——元素覆盖 + 范围倍率 + 每 tick 附加状态
                zone.SetEnhancement(_enhCfg, _enhElement, _enhRadiusMult);
                if (_enhCfg.addBurn || _enhCfg.addFreeze || _enhCfg.addPoison)
                    _enhWorldDelegated = true; // 状态随区域 tick 施加，EndEnhancement 不再绕玩家回退
            }
            Debug.Log($"<color=cyan>{skill.skillName} 召唤持续区域（{(skill.zoneFollowPlayer ? "随身" : "落点")}）</color>");
        }

        /// <summary>查找范围内最远的存活敌人方向（目标改造·最远用）。无敌人返回 false。</summary>
        private bool TryFindFarthestEnemyDir(Vector3 origin, float maxRange, out Vector3 dir)
        {
            dir = Vector3.zero;
            var hits = Physics.OverlapSphere(origin, maxRange, enemyLayer);
            float best = -1f;
            Vector3 bestPos = Vector3.zero;
            foreach (var h in hits)
            {
                var dmg = h.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.Stats != null && !dmg.Stats.IsAlive) continue;
                Vector3 p = h.transform.position;
                float d = (p - origin).sqrMagnitude;
                if (d > best) { best = d; bestPos = p; }
            }
            if (best < 0f) return false;
            Vector3 flat = bestPos - origin; flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return false;
            dir = flat.normalized;
            return true;
        }

        /// <summary>投射物技能（支持多发散射），支持蓄力倍率</summary>
        private void CastProjectileSkill(SkillData skill, float damageMul = 1f)
        {
            Vector3 spawnPos = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 dir = _player.AimDirection;
            // 目标改造·最远：增强让核心投射技自动改朝范围内最远敌（覆盖鼠标瞄准；环绕时不适用）
            if (_enhActive && _enhTargetFarthest && !_enhSurround
                && TryFindFarthestEnemyDir(spawnPos, 22f, out Vector3 farDir))
                dir = farDir;
            float skillBase = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
            float skillMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage);
            var (damage, _) = _player.Stats.CalcSkillDamage(0f, skillMul);
            damage *= damageMul;

            int count = Mathf.Max(1, skill.projectileCount);
            float spreadAngle = skill.spreadAngle;
            // 形态改造：环绕 / 火环（Ring）均为 360° 均分；火墙（Wall）平行排列；火域（Zone）飞弹落点附带持续区域
            bool ring = _enhActive && _enhShape == ShapeMode.Ring;
            bool wall = _enhActive && _enhShape == ShapeMode.Wall;
            bool zone = _enhActive && _enhShape == ShapeMode.Zone;
            bool surround = (_enhActive && _enhSurround) || ring;
            if (_enhActive)
            {
                // 形态改造·连锁（数量倍率）+ 额外飞弹（加数）
                count = Mathf.Max(1, Mathf.RoundToInt(count * _enhProjectileMult) + _enhExtraProjectiles);
                if (surround) count = Mathf.Max(count, 8); // 环绕/火环至少 8 发才成环
                if (wall) count = Mathf.Max(count, 5);     // 火墙至少 5 发才成墙
                if (count > 1 && spreadAngle < 1f && !wall) spreadAngle = 20f; // 火墙走平行不散射
            }
            float halfSpread = spreadAngle * 0.5f;
            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized; // 火墙横向偏移基向量
            const float WallSpacing = 1.1f;

            for (int i = 0; i < count; i++)
            {
                // 计算每发投射物的方向与起点
                Vector3 projDir = dir;
                Vector3 pos = spawnPos;
                if (surround)
                {
                    // 形态改造·环绕/火环：360° 均分
                    projDir = Quaternion.Euler(0, i * (360f / count), 0) * dir;
                }
                else if (wall)
                {
                    // 形态改造·火墙：平行同向，起点横向铺开成墙
                    float off = (i - (count - 1) * 0.5f) * WallSpacing;
                    pos = spawnPos + perp * off;
                }
                else if (count > 1)
                {
                    float angle = Mathf.Lerp(-halfSpread, halfSpread, (float)i / (count - 1));
                    projDir = Quaternion.Euler(0, angle, 0) * dir;
                }

                if (skill.projectilePrefab != null)
                {
                    GameObject proj;
                    if (ObjectPool.Instance != null)
                        proj = ObjectPool.Instance.Get(skill.projectilePrefab, pos, Quaternion.LookRotation(projDir));
                    else
                        proj = Instantiate(skill.projectilePrefab, pos, Quaternion.LookRotation(projDir));

                    var projectile = proj.GetComponent<Projectile>();
                    if (projectile != null)
                    {
                        projectile.Initialize(damage, projDir, skill.projectileSpeed, 0, 0, EnhElem(skill), _player, _player.Stats.armorPenPercent);
                        if (_enhActive)
                        {
                            projectile.SetEnhancement(_enhCfg);
                            if (_enhChainCount > 0) projectile.SetChain(_enhChainCount, enemyLayer);
                            if (zone) ApplyImpactZone(projectile, damage, EnhElem(skill));
                            _enhWorldDelegated = true; // 控制/状态随投射物命中施加，EndEnhancement 不再绕玩家回退
                        }
                    }
                }
                else if (showDebugVisuals)
                {
                    // 没有Prefab时创建Debug投射物（带元素颜色提示）
                    var dbgProj = CreateDebugProjectile(pos, projDir, skill.projectileSpeed, damage, skill.vfxDuration, EnhElem(skill));
                    if (_enhActive && dbgProj != null)
                    {
                        dbgProj.SetEnhancement(_enhCfg);
                        if (_enhChainCount > 0) dbgProj.SetChain(_enhChainCount, enemyLayer);
                        if (zone) ApplyImpactZone(dbgProj, damage, EnhElem(skill));
                        _enhWorldDelegated = true;
                    }
                }
            }
        }

        /// <summary>形态改造·火域：为投射物挂上命中落点小型持续区域（程序化，复用 ActiveSkillZone）。</summary>
        private void ApplyImpactZone(Projectile projectile, float damage, ElementTag element)
        {
            if (projectile == null) return;
            float dps = Mathf.Max(1f, damage * 0.3f); // 每 tick 约本发飞弹 30% 伤害
            projectile.SetImpactZone(2.5f, 3f, 0.5f, dps, element, enemyLayer);
        }

        /// <summary>增益技能（如金钟罩）</summary>
        private void CastBuffSkill(SkillData skill, int slotIndex = -1)
        {
            // 金蝉脱壳：武装"受致命伤拦截"，不走常规属性增益
            if (skill.armLethalGuard)
            {
                var guard = _player.GetComponent<LethalGuard>();
                if (guard == null) guard = _player.gameObject.AddComponent<LethalGuard>();
                guard.Arm(skill.lethalGuardDuration > 0f ? skill.lethalGuardDuration : skill.cooldown);
                Debug.Log($"<color=cyan>{skill.skillName} 武装！受致命伤将自动脱身</color>");
                return;
            }

            // 天地大挪移：进入乾坤倒转（受伤反弹+免疫、普攻转治疗）
            if (skill.heavenEarthShift)
            {
                var hes = _player.GetComponent<HeavenEarthShift>();
                if (hes == null) hes = _player.gameObject.AddComponent<HeavenEarthShift>();
                hes.Activate(skill.buffDuration > 0f ? skill.buffDuration : 10f);
                Debug.Log($"<color=cyan>{skill.skillName}！乾坤倒转：伤害反弹，攻击转治疗</color>");
                return;
            }

            float dur = skill.buffDuration > 0f ? skill.buffDuration
                       : (skill.vfxDuration > 0f ? skill.vfxDuration : 5f);

            // 由 SkillData 增益字段组装 StatusEffect（攻速/移速/攻击/减伤）；都未填则兜底减伤 +50%（金钟罩式）
            var mods = new System.Collections.Generic.List<StatModifier>();
            if (skill.buffAttackSpeedPct != 0f) mods.Add(StatModifier.Percent(StatType.AttackSpeed, skill.buffAttackSpeedPct));
            if (skill.buffMoveSpeedPct != 0f) mods.Add(StatModifier.Percent(StatType.MoveSpeed, skill.buffMoveSpeedPct));
            if (skill.buffAttackPct != 0f) mods.Add(StatModifier.Percent(StatType.AttackDamage, skill.buffAttackPct));
            if (skill.buffDamageReduction != 0f) mods.Add(StatModifier.Flat(StatType.DamageReduction, skill.buffDamageReduction));
            if (mods.Count == 0) mods.Add(StatModifier.Flat(StatType.DamageReduction, 0.5f));

            var status = _player.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.Apply(new StatusEffect
                {
                    id = $"skill_buff_{skill.configId}_{skill.skillName}",
                    isBuff = true,
                    elementTag = skill.elementTag,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = dur,
                    duration = dur,
                    modifiers = mods,
                    displayName = skill.skillName,
                    description = skill.description,
                    uiColor = SkillModifierApplier.ColorOf(skill.elementTag)
                });
            }

            // GDD 6.5：buff 类技能也支持槽位修饰
            if (slotIndex >= 0 && skill.modifierDefs != null && skill.modifierDefs.Length > 0)
            {
                SkillModifierApplier.ApplyAreaSkill(
                    skill, slotIndex,
                    transform.position,
                    Mathf.Max(2.5f, skill.aoeRadius),
                    null,    // buff 技能没有命中目标列表，仅触发 zone
                    _player,
                    enemyLayer);
            }

            // 特效
            if (skill.vfxPrefab != null)
            {
                GameObject vfx;
                if (ObjectPool.Instance != null)
                {
                    vfx = ObjectPool.Instance.Get(skill.vfxPrefab, transform.position, Quaternion.identity);
                    ObjectPool.Instance.Return(vfx, skill.vfxDuration);
                }
                else
                {
                    vfx = Instantiate(skill.vfxPrefab, transform.position, Quaternion.identity);
                    Destroy(vfx, skill.vfxDuration);
                }
            }
            else if (showDebugVisuals)
            {
                // 没有VFX时创建Debug可视化：用半透明球体表示护盾，按 elementTag 上色
                Color shieldColor = skill.elementTag != ElementTag.None
                    ? SkillModifierApplier.ColorOf(skill.elementTag)
                    : new Color(1f, 0.85f, 0.1f, 0.3f);
                shieldColor.a = 0.35f;
                CreateDebugShieldIndicator(skill.vfxDuration, shieldColor);
            }

            Debug.Log($"<color=cyan>{skill.skillName} 增益生效（由 StatusEffect 计时），持续 {dur}秒</color>");
        }

        /// <summary>更新技能充能恢复</summary>
        private void UpdateCooldowns()
        {
            SkillData[] skills = { skillQ, skillE, skillR };
            for (int i = 0; i < 3; i++)
            {
                if (skills[i] == null) continue;
                if (_skillCharges[i] >= _skillMaxCharges[i]) continue;

                _skillRechargeTimer[i] -= Time.deltaTime;
                if (_skillRechargeTimer[i] <= 0)
                {
                    // 恢复一层充能
                    _skillCharges[i]++;
                    // 如果还没满，继续充能下一层
                    if (_skillCharges[i] < _skillMaxCharges[i])
                    {
                        float rechargeTime = skills[i].chargeTime > 0 ? skills[i].chargeTime : SkillTuning.EffectiveCooldown(skills[i]);

                        _skillRechargeTimer[i] = rechargeTime;
                        _skillRechargeDuration[i] = rechargeTime;
                    }
                    else
                    {
                        _skillRechargeTimer[i] = 0;
                    }
                }

                PublishSkillChargeUpdate(i, skills[i]);
            }
        }

        /// <summary>装备技能到Q槽位</summary>
        public void EquipSkillQ(SkillData skill)
        {
            skillQ = skill;
            InitSkillCharges(0, skill);
            PublishSkillChargeUpdate(0, skill);
        }

        /// <summary>装备技能到E槽位</summary>
        public void EquipSkillE(SkillData skill)
        {
            skillE = skill;
            InitSkillCharges(1, skill);
            PublishSkillChargeUpdate(1, skill);
        }

        // ==================== 公开设置方法 ====================

        /// <summary>设置刀光特效Prefab</summary>
        public void SetSlashVFX(GameObject prefab, Transform spawnPoint)
        {
            slashVFXPrefab = prefab;
            slashVFXSpawnPoint = spawnPoint;
        }

        /// <summary>设置打击特效Prefab</summary>
        public void SetHitVFX(GameObject prefab)
        {
            hitVFXPrefab = prefab;
        }

        /// <summary>设置攻击原点</summary>
        public void SetAttackOrigin(Transform origin)
        {
            attackOrigin = origin;
        }

        /// <summary>设置敌人层级</summary>
        public void SetEnemyLayer(LayerMask layer)
        {
            enemyLayer = layer;
        }

        /// <summary>装备技能到R槽位</summary>
        public void EquipSkillR(SkillData skill)
        {
            skillR = skill;
            InitSkillCharges(2, skill);
            PublishSkillChargeUpdate(2, skill);
        }

        // ==================== 技能槽位管理 ====================

        /// <summary>获取指定槽位的技能</summary>
        public SkillData GetSkillInSlot(int slotIndex)
        {
            return slotIndex switch
            {
                0 => skillQ,
                1 => skillE,
                2 => skillR,
                _ => null
            };
        }

        /// <summary>装备技能到指定槽位（返回被替换的旧技能，可能为null）</summary>
        public SkillData EquipSkillToSlot(SkillData skill, int slotIndex)
        {
            SkillData old = GetSkillInSlot(slotIndex);
            switch (slotIndex)
            {
                case 0: EquipSkillQ(skill); break;
                case 1: EquipSkillE(skill); break;
                case 2: EquipSkillR(skill); break;
            }
            return old;
        }

        /// <summary>交换两个槽位的技能</summary>
        public void SwapSkills(int slotA, int slotB)
        {
            if (slotA == slotB) return;
            SkillData a = GetSkillInSlot(slotA);
            SkillData b = GetSkillInSlot(slotB);
            EquipSkillToSlot(b, slotA);
            EquipSkillToSlot(a, slotB);
            Debug.Log($"<color=cyan>技能交换：槽位{slotA} ↔ 槽位{slotB}</color>");
        }

        /// <summary>卸下指定槽位的技能（返回被卸下的技能）</summary>
        public SkillData UnequipSkill(int slotIndex)
        {
            return EquipSkillToSlot(null, slotIndex);
        }

        /// <summary>找到第一个空闲槽位（-1表示没有空位）</summary>
        public int FindEmptySlot()
        {
            if (skillQ == null) return 0;
            if (skillE == null) return 1;
            if (skillR == null) return 2;
            return -1;
        }

        // ==================== Debug 可视化 ====================

        /// <summary>创建范围技能的Debug指示器（落石等）</summary>
        private void CreateDebugAreaIndicator(Vector3 position, float radius, float duration, Color color)
        {
            // 创建一个下落的Cube表示落石
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "[Debug] 落石";
            rock.transform.position = position + Vector3.up * 8f;
            rock.transform.localScale = new Vector3(radius * 0.8f, radius * 0.8f, radius * 0.8f);
            rock.transform.rotation = Quaternion.Euler(45, 45, 0);

            // 移除碰撞体
            var col = rock.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 设置半透明材质
            var rend = rock.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = color;
                rend.material = mat;
            }

            // 创建地面范围指示圈（扁平圆柱体）
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = "[Debug] 落石范围";
            circle.transform.position = position + Vector3.up * 0.05f;
            circle.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

            var circleCol = circle.GetComponent<Collider>();
            if (circleCol != null) Destroy(circleCol);

            var circleRend = circle.GetComponent<Renderer>();
            if (circleRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.2f, 0.1f, 0.35f);
                circleRend.material = mat;
            }

            // 落石下落动画
            StartCoroutine(FallingRockAnimation(rock, circle, position, duration));
        }

        /// <summary>落石下落动画协程</summary>
        private System.Collections.IEnumerator FallingRockAnimation(GameObject rock, GameObject circle, Vector3 targetPos, float duration)
        {
            float fallDuration = 0.4f;
            float startY = targetPos.y + 8f;
            float endY = targetPos.y + 0.5f;
            float timer = 0f;

            // 下落阶段
            while (timer < fallDuration && rock != null)
            {
                timer += Time.deltaTime;
                float t = timer / fallDuration;
                float y = Mathf.Lerp(startY, endY, t * t); // 加速下落
                rock.transform.position = new Vector3(targetPos.x, y, targetPos.z);
                rock.transform.Rotate(Vector3.one * 360f * Time.deltaTime, Space.Self);
                yield return null;
            }

            // 落地后闪烁并消失
            if (rock != null)
            {
                rock.transform.position = new Vector3(targetPos.x, endY, targetPos.z);
                // 放大一下表示冲击
                rock.transform.localScale *= 1.3f;
            }

            yield return new WaitForSeconds(0.3f);

            // 淡出
            float fadeTime = 0.5f;
            float fadeTimer = 0f;
            while (fadeTimer < fadeTime)
            {
                fadeTimer += Time.deltaTime;
                float alpha = 1f - (fadeTimer / fadeTime);
                if (rock != null)
                {
                    var rend = rock.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var c = rend.material.color;
                        c.a = alpha * 0.6f;
                        rend.material.color = c;
                    }
                }
                if (circle != null)
                {
                    var rend = circle.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var c = rend.material.color;
                        c.a = alpha * 0.35f;
                        rend.material.color = c;
                    }
                }
                yield return null;
            }

            if (rock != null) Destroy(rock);
            if (circle != null) Destroy(circle);
        }

        /// <summary>创建Buff技能的Debug护盾指示器</summary>
        private void CreateDebugShieldIndicator(float duration, Color color)
        {
            var shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shield.name = "[Debug] 护盾";
            shield.transform.SetParent(transform);
            shield.transform.localPosition = new Vector3(0, 1f, 0);
            shield.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);

            var col = shield.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = shield.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = color;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f, 0.2f));
                rend.material = mat;
            }

            StartCoroutine(ShieldAnimation(shield, duration));
        }

        /// <summary>护盾动画协程（旋转 + 淡出）</summary>
        private System.Collections.IEnumerator ShieldAnimation(GameObject shield, float duration)
        {
            float timer = 0f;
            while (timer < duration && shield != null)
            {
                timer += Time.deltaTime;
                // 缓慢旋转
                shield.transform.Rotate(Vector3.up * 30f * Time.deltaTime, Space.World);

                // 最后1秒淡出
                float remaining = duration - timer;
                if (remaining < 1f)
                {
                    var rend = shield.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var c = rend.material.color;
                        c.a = remaining * 0.3f;
                        rend.material.color = c;
                    }
                }
                yield return null;
            }

            if (shield != null) Destroy(shield);
        }

        /// <summary>位移技能（如土遁术、缩地成寸）</summary>
        private void CastDashSkill(SkillData skill)
        {
            Vector3 dir = _player.AimDirection;
            float distance = skill.dashDistance > 0 ? skill.dashDistance : 8f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + dir * distance;

            // 射线检测避免穿墙
            if (Physics.Raycast(startPos + Vector3.up * 0.5f, dir, out RaycastHit wallHit, distance))
            {
                targetPos = wallHit.point - dir * 0.5f;
            }

            // 起点特效（按 elementTag 上色，土黄/风青/水蓝/默认褐）
            if (showDebugVisuals)
            {
                Color trailColor = skill.elementTag != ElementTag.None
                    ? SkillModifierApplier.ColorOf(skill.elementTag)
                    : new Color(0.6f, 0.4f, 0.2f, 0.5f);
                trailColor.a = 0.5f;
                CreateDebugDashTrail(startPos, targetPos, trailColor);
            }

            // 瞬移
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = targetPos;
                cc.enabled = true;
            }
            else
            {
                transform.position = targetPos;
            }

            // 土遁术：钻地无敌
            if (skill.dashInvulnerable && skill.dashInvulnDuration > 0f && PlayerController.Instance != null)
                PlayerController.Instance.SetInvincible(skill.dashInvulnDuration);

            // 如果留下伤害区域，对路径上的敌人造成伤害
            if (skill.leaveTrail)
            {
                float skillBase = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
                float sMul = skillBase / Mathf.Max(1f, _player.Stats.attackDamage);
                var hits = Physics.OverlapCapsule(startPos + Vector3.up * 0.5f,
                    targetPos + Vector3.up * 0.5f, 1.5f, enemyLayer);
                foreach (var hit in hits)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, sMul);
                        damageable.OnDamage(dmg, hit.transform.position, gameObject);
                    }
                }
            }

            // VFX
            if (skill.vfxPrefab != null)
            {
                GameObject vfx;
                if (ObjectPool.Instance != null)
                {
                    vfx = ObjectPool.Instance.Get(skill.vfxPrefab, targetPos, Quaternion.identity);
                    ObjectPool.Instance.Return(vfx, skill.vfxDuration);
                }
                else
                {
                    vfx = Instantiate(skill.vfxPrefab, targetPos, Quaternion.identity);
                    Destroy(vfx, skill.vfxDuration);
                }
            }

            Debug.Log($"<color=cyan>土遁！位移 {Vector3.Distance(startPos, targetPos):F1} 米</color>");
        }

        /// <summary>治疗技能（如回春术）</summary>
        private void CastHealSkill(SkillData skill)
        {
            float healAmount = skill.healAmount + _player.Stats.attackDamage * skill.healScaling;
            float oldHp = _player.Stats.currentHp;
            _player.Stats.currentHp = Mathf.Min(_player.Stats.currentHp + healAmount, _player.Stats.maxHp);
            float actualHeal = _player.Stats.currentHp - oldHp;

            // 发布血量变化事件
            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _player.Stats.currentHp,
                MaxHp = _player.Stats.maxHp
            });

            // 治疗飘字（绿色）
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2f,
                Damage = actualHeal,
                SpecialTag = "治疗"
            });

            // VFX
            if (skill.vfxPrefab != null)
            {
                GameObject vfx;
                if (ObjectPool.Instance != null)
                {
                    vfx = ObjectPool.Instance.Get(skill.vfxPrefab, transform.position, Quaternion.identity);
                    ObjectPool.Instance.Return(vfx, skill.vfxDuration);
                }
                else
                {
                    vfx = Instantiate(skill.vfxPrefab, transform.position, Quaternion.identity);
                    Destroy(vfx, skill.vfxDuration);
                }
            }
            else if (showDebugVisuals)
            {
                CreateDebugHealIndicator(skill.vfxDuration);
            }

            Debug.Log($"<color=green>回春术！恢复 {actualHeal:F0} 生命值</color>");
        }

        /// <summary>召唤技能（如傀儡术）</summary>
        private void CastSummonSkill(SkillData skill)
        {
            // 水镜术：嘲讽分身（吸引敌人，不参与战斗）
            if (skill.summonIsDecoy)
            {
                float decoyLife = skill.summonDuration > 0f ? skill.summonDuration : 3f;
                WaterMirrorDecoy.Spawn(transform.position, decoyLife);
                Debug.Log($"<color=cyan>{skill.skillName}！水镜分身吸引敌人 {decoyLife:F0} 秒</color>");
                return;
            }

            Vector3 spawnPos = transform.position + _player.AimDirection * 2f;
            float baseRatio = skill.damageScaling;
            var (damage, _) = _player.Stats.BuildSummonDamage(baseRatio, skill.summonDamage);
            float duration = skill.summonDuration;

            // 创建召唤物
            if (skill.vfxPrefab != null)
            {
                var summon = Instantiate(skill.vfxPrefab, spawnPos, Quaternion.identity);
                Destroy(summon, duration);
            }
            else if (showDebugVisuals)
            {
                // Debug召唤物：一个旋转的球体，会自动攻击附近敌人
                StartCoroutine(DebugSummonCoroutine(spawnPos, damage, duration));
            }

            Debug.Log($"<color=cyan>召唤！持续 {duration:F0} 秒，每次攻击 {damage:F0} 伤害</color>");
        }

        /// <summary>创建Debug投射物（无Prefab时的可视化），按 elementTag 上色 + 选用差异化形状（v0.3.3）</summary>
        private Projectile CreateDebugProjectile(Vector3 spawnPos, Vector3 direction, float speed, float damage, float lifetime, ElementTag elementTag = ElementTag.None)
        {
            // v0.3.3：根据 elementTag 选择形状（火球 / 冰晶 / 雷柱 / 风刺 / 木球 / 水球 / 土块 / 穿透标枪）
            PrimitiveType shape = FxFactory.ElementShape(elementTag);
            var proj = GameObject.CreatePrimitive(shape);
            proj.name = $"[Debug] 投射物·{elementTag}";
            proj.transform.position = spawnPos;

            // 元素特色缩放
            Vector3 baseScale = new Vector3(0.4f, 0.4f, 0.4f);
            switch (elementTag)
            {
                case ElementTag.Thunder: baseScale = new Vector3(0.18f, 0.7f, 0.18f); break;
                case ElementTag.Pierce:
                case ElementTag.Wind:
                    baseScale = new Vector3(0.2f, 0.2f, 0.8f);
                    proj.transform.rotation = Quaternion.LookRotation(direction);
                    break;
            }
            proj.transform.localScale = baseScale;
            proj.layer = 0; // Default层，避免自伤

            // 元素颜色（None 走默认蓝白）
            Color body = elementTag == ElementTag.None
                ? new Color(0.3f, 0.7f, 1f, 0.95f)
                : FxFactory.ElementColor(elementTag);
            body.a = 0.95f;

            // 设置材质
            var rend = proj.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = body;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(body.r, body.g, body.b) * 2.5f);
                rend.material = mat;
            }

            // 添加Projectile组件
            var projComp = proj.AddComponent<Projectile>();
            projComp.Initialize(damage, direction, speed, 0, 0, elementTag, _player, _player.Stats.armorPenPercent);

            // 确保有触发器碰撞体
            var col = proj.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // 添加Rigidbody（Projectile需要触发OnTriggerEnter）
            var rb = proj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            return projComp;
        }

        /// <summary>创建Debug位移轨迹</summary>
        private void CreateDebugDashTrail(Vector3 start, Vector3 end, Color color)
        {
            // 起点烟雾
            var startSmoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            startSmoke.name = "[Debug] 土遁起点";
            startSmoke.transform.position = start + Vector3.up * 0.5f;
            startSmoke.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var startCol = startSmoke.GetComponent<Collider>();
            if (startCol != null) Destroy(startCol);
            var startRend = startSmoke.GetComponent<Renderer>();
            if (startRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = color;
                startRend.material = mat;
            }
            Destroy(startSmoke, 1f);

            // 终点闪光
            var endFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            endFlash.name = "[Debug] 土遁终点";
            endFlash.transform.position = end + Vector3.up * 0.5f;
            endFlash.transform.localScale = new Vector3(2f, 2f, 2f);
            var endCol = endFlash.GetComponent<Collider>();
            if (endCol != null) Destroy(endCol);
            var endRend = endFlash.GetComponent<Renderer>();
            if (endRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.8f, 0.6f, 0.2f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.2f) * 1.5f);
                endRend.material = mat;
            }
            Destroy(endFlash, 0.8f);

            // 轨迹线（用Debug.DrawLine持续绘制）
            StartCoroutine(DrawTrailCoroutine(start, end, 1f));
        }

        private System.Collections.IEnumerator DrawTrailCoroutine(Vector3 start, Vector3 end, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float alpha = 1f - timer / duration;
                Debug.DrawLine(start + Vector3.up * 0.5f, end + Vector3.up * 0.5f,
                    new Color(0.8f, 0.6f, 0.2f, alpha));
                yield return null;
            }
        }

        /// <summary>创建Debug治疗指示器</summary>
        private void CreateDebugHealIndicator(float duration)
        {
            // 绿色上升光柱
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "[Debug] 治疗";
            pillar.transform.SetParent(transform);
            pillar.transform.localPosition = new Vector3(0, 1.5f, 0);
            pillar.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

            var col = pillar.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = pillar.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.2f, 1f, 0.3f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 0.2f) * 1.5f);
                rend.material = mat;
            }

            StartCoroutine(HealAnimation(pillar, duration > 0 ? duration : 1.5f));
        }

        private System.Collections.IEnumerator HealAnimation(GameObject pillar, float duration)
        {
            float timer = 0f;
            while (timer < duration && pillar != null)
            {
                timer += Time.deltaTime;
                // 上升 + 淡出
                pillar.transform.localPosition += Vector3.up * 0.5f * Time.deltaTime;
                var rend = pillar.GetComponent<Renderer>();
                if (rend != null)
                {
                    var c = rend.material.color;
                    c.a = (1f - timer / duration) * 0.3f;
                    rend.material.color = c;
                }
                yield return null;
            }
            if (pillar != null) Destroy(pillar);
        }

        /// <summary>Debug召唤物协程（自动攻击附近敌人）</summary>
        private System.Collections.IEnumerator DebugSummonCoroutine(Vector3 pos, float damage, float duration)
        {
            var summon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            summon.name = "[Debug] 召唤物";
            summon.transform.position = pos + Vector3.up * 1f;
            summon.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var col = summon.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = summon.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.6f, 0.3f, 0.9f, 0.8f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 0.8f) * 2f);
                rend.material = mat;
            }

            float timer = 0f;
            float attackInterval = 1.5f;
            float attackTimer = 0f;

            while (timer < duration && summon != null)
            {
                timer += Time.deltaTime;
                attackTimer += Time.deltaTime;

                // 悬浮旋转
                summon.transform.Rotate(Vector3.up * 120f * Time.deltaTime);
                summon.transform.position = pos + Vector3.up * (1f + Mathf.Sin(timer * 2f) * 0.3f);

                // 定时攻击附近敌人
                if (attackTimer >= attackInterval)
                {
                    attackTimer = 0f;
                    var hits = Physics.OverlapSphere(summon.transform.position, 5f, enemyLayer);
                    if (hits.Length > 0)
                    {
                        // 攻击最近的敌人
                        var nearest = hits[0];
                        float minDist = float.MaxValue;
                        foreach (var hit in hits)
                        {
                            float dist = Vector3.Distance(summon.transform.position, hit.transform.position);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                nearest = hit;
                            }
                        }

                        var damageable = nearest.GetComponent<IDamageable>();
                        if (damageable != null)
                        {
                            // 走 BuildSummonDamage 继承玩家暴击 + 当前 attackDamage
                            var (dmg, isCrit) = _player.Stats.BuildSummonDamage(0.3f, damage * 0.5f);
                            damageable.OnDamage(dmg, nearest.transform.position, gameObject);
                            GameEvents.Publish(new GameEvents.DamageNumberRequested
                            {
                                WorldPosition = nearest.transform.position + Vector3.up * 1.5f,
                                Damage = dmg,
                                IsCrit = isCrit,
                                SpecialTag = "傀儡"
                            });
                            // 攻击特效线
                            Debug.DrawLine(summon.transform.position, nearest.transform.position,
                                new Color(0.6f, 0.3f, 0.9f), 0.3f);
                        }
                    }
                }

                // 最后1秒淡出
                float remaining = duration - timer;
                if (remaining < 1f && rend != null)
                {
                    var c = rend.material.color;
                    c.a = remaining * 0.8f;
                    rend.material.color = c;
                }

                yield return null;
            }

            if (summon != null) Destroy(summon);
        }

        /// <summary>运行时绘制攻击扇形范围（Debug.DrawLine，Game视图可见）</summary>
        private void DrawAttackRange(Color color)
        {
            Vector3 origin = GetAimRelativeWorldPos(attackOrigin, transform.position + Vector3.up * 0.8f);
            Vector3 forward = _player.AimDirection;
            float halfAngle = meleeAngle * 0.5f;

            Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;

            // 绘制扇形边线
            Debug.DrawLine(origin, origin + leftDir * meleeRange, color);
            Debug.DrawLine(origin, origin + rightDir * meleeRange, color);
            Debug.DrawLine(origin, origin + forward * meleeRange, color);

            // 绘制扇形弧线
            int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = -halfAngle + (meleeAngle / segments) * i;
                float angle2 = -halfAngle + (meleeAngle / segments) * (i + 1);
                Vector3 p1 = origin + Quaternion.Euler(0, angle1, 0) * forward * meleeRange;
                Vector3 p2 = origin + Quaternion.Euler(0, angle2, 0) * forward * meleeRange;
                Debug.DrawLine(p1, p2, color);
            }
        }

        // ==================== Debug Gizmos ====================

#if UNITY_EDITOR
        /// <summary>在Scene视图中绘制近战攻击范围</summary>
        private void OnDrawGizmosSelected()
        {
            if (_player == null) return;

            Vector3 forward = Application.isPlaying ? _player.AimDirection : transform.forward;
            Vector3 origin;
            if (attackOrigin != null)
            {
                Vector3 localOffset = attackOrigin.localPosition;
                Quaternion aimRot = Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward : transform.forward);
                origin = transform.position + aimRot * localOffset;
            }
            else
            {
                origin = transform.position + Vector3.up * 0.8f;
            }

            // 绘制攻击范围球
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
            Gizmos.DrawWireSphere(origin, meleeRange);

            // 绘制扇形范围
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.4f);
            float halfAngle = meleeAngle * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * forward;
            Gizmos.DrawLine(origin, origin + leftDir * meleeRange);
            Gizmos.DrawLine(origin, origin + rightDir * meleeRange);
            Gizmos.DrawLine(origin, origin + forward * meleeRange);

            // 绘制扇形弧线
            int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = -halfAngle + (meleeAngle / segments) * i;
                float angle2 = -halfAngle + (meleeAngle / segments) * (i + 1);
                Vector3 p1 = origin + Quaternion.Euler(0, angle1, 0) * forward * meleeRange;
                Vector3 p2 = origin + Quaternion.Euler(0, angle2, 0) * forward * meleeRange;
                Gizmos.DrawLine(p1, p2);
            }
        }
#endif
    }
}
