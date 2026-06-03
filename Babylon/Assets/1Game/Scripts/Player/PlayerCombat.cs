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

        // 技能充能系统（每个槽位独立充能）
        private int[] _skillCharges = new int[3];       // 当前充能层数
        private int[] _skillMaxCharges = new int[3];    // 最大充能层数
        private float[] _skillRechargeTimer = new float[3]; // 充能恢复计时器
        private float[] _skillRechargeDuration = new float[3]; // 每层充能恢复时间
        private int[] _chargeBonusFromItems = new int[3]; // 灵物提供的额外充能层数

        // 蓄力系统
        private int _chargingSlot = -1;          // 当前正在蓄力的技能槽位（-1=未蓄力）
        private float _chargeTimer = 0f;          // 蓄力计时器
        private int _currentChargeLevel = 1;      // 当前蓄力等级
        private float _originalMoveSpeed;         // 蓄力前的移速（用于恢复）
        private bool _chargeMoveSpeedApplied;     // 是否已应用蓄力减速

        // 攻击判定：每段攻击只判定一次
        private bool _hasHitThisSwing;
        private int _lastHitComboStep = -1;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _playerAnim = GetComponent<PlayerAnimator>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.SlashVFXRequested>(OnSlashVFXRequested);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.SlashVFXRequested>(OnSlashVFXRequested);
        }

        private void Update()
        {
            if (!_player.Stats.IsAlive || _player.IsDashing)
            {
                // 死亡或闪避时取消蓄力
                if (_chargingSlot >= 0)
                    CancelCharging();
                return;
            }

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
                        // 根据连招段数计算伤害倍率
                        float damageMultiplier = GetComboDamageMultiplier(_playerAnim.ComboStep);
                        float damage = _player.Stats.CalculateDamage() * damageMultiplier;

                        Vector3 hitPoint = col.ClosestPoint(origin);
                        damageable.OnDamage(damage, hitPoint, gameObject);

                        // 近战攻击也触发灼烧效果（火灵珠等灵物）
                        float burnDPS = _player.Inventory.GetTotalBurnDPS();
                        if (burnDPS > 0)
                        {
                            var burn = col.GetComponent<BurnEffect>();
                            if (burn == null)
                                burn = col.gameObject.AddComponent<BurnEffect>();
                            burn.Apply(burnDPS, 3f); // 灼烧3秒
                        }

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

            // 通知质变效果运行器（焚天：每N次攻击释放火焰冲击波，不需要命中）
            var runner = QualitativeEffectRunner.Instance;
            if (runner != null)
                runner.OnPlayerAttackHit();

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

        /// <summary>技能释放（支持充能系统 + 蓄力系统）</summary>
        private void HandleSkills()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            SkillData[] skills = { skillQ, skillE, skillR };
            var keys = new[] { kb.qKey, kb.eKey, kb.rKey };

            // 如果正在蓄力中
            if (_chargingSlot >= 0)
            {
                var skill = skills[_chargingSlot];
                var key = keys[_chargingSlot];

                if (skill == null || !key.isPressed)
                {
                    // 松开按键 → 释放蓄力技能
                    ReleaseChargedSkill();
                    return;
                }

                // 继续蓄力
                float chargeSpeedBonus = 0f;
                var spiritSlots = GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null)
                    chargeSpeedBonus = spiritSlots.GetSkillChargeSpeedBonus(_chargingSlot);

                _chargeTimer += Time.deltaTime * (1f + chargeSpeedBonus);
                int newLevel = skill.GetChargeLevel(_chargeTimer);

                if (newLevel != _currentChargeLevel)
                {
                    _currentChargeLevel = newLevel;
                    Debug.Log($"<color=yellow>蓄力等级提升 → Lv{_currentChargeLevel}！</color>");
                }

                // 发布蓄力进度事件
                GameEvents.Publish(new GameEvents.SkillChargeProgress
                {
                    SlotIndex = _chargingSlot,
                    ChargeTime = _chargeTimer,
                    ChargeLevel = _currentChargeLevel,
                    IsCharging = true
                });

                return; // 蓄力中不处理其他技能
            }

            // 非蓄力状态：检测按键
            for (int i = 0; i < 3; i++)
            {
                if (skills[i] == null || _skillCharges[i] <= 0) continue;

                if (keys[i].wasPressedThisFrame)
                {
                    // 支持蓄力的技能 → 开始蓄力
                    if (skills[i].canCharge)
                    {
                        StartCharging(i, skills[i]);
                    }
                    else
                    {
                        // 不支持蓄力 → 直接释放
                        if (UseSkill(skills[i], i, 1))
                            ConsumeSkillCharge(i, skills[i]);
                    }
                    return; // 每帧只处理一个技能
                }
            }
        }

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

            if (skill == null) return;

            // 释放技能（带蓄力等级）
            if (UseSkill(skill, slot, chargeLevel))
            {
                ConsumeSkillCharge(slot, skill);

                GameEvents.Publish(new GameEvents.SkillChargeReleased
                {
                    SlotIndex = slot,
                    ChargeLevel = chargeLevel,
                    Skill = skill
                });

                if (chargeLevel > 1)
                    Debug.Log($"<color=cyan>蓄力释放 Lv{chargeLevel}：{skill.skillName}（伤害×{skill.GetChargeDamageMultiplier(chargeLevel):F1}）</color>");
            }
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

                // 应用灵物CD缩减
                var spiritSlots = GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null)
                {
                    float cdReduction = spiritSlots.GetSkillCooldownReduction(slotIndex);
                    rechargeTime *= (1f - cdReduction);
                }

                // v0.4 融合层：火化身狂火期间技能 CD ×0.7
                var fire = GetComponent<SpiritRootFireController>();
                if (fire != null) rechargeTime *= fire.SkillCdMultiplier;

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

        // ==================== 充能加成（灵物系统调用） ====================

        /// <summary>增加技能槽位的充能上限（由灵物系统调用）</summary>
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

        /// <summary>移除技能槽位的充能上限加成（由灵物系统调用）</summary>
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

        /// <summary>获取技能槽位的实际充能上限（含灵物加成）</summary>
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

            // 灵物蓄力伤害加成
            if (chargeLevel > 1)
            {
                var spiritSlots = GetComponent<SpiritSlotSystem>();
                if (spiritSlots != null)
                {
                    float bonusDmg = spiritSlots.GetSkillChargeDamageBonus(slotIndex);
                    chargeDmgMul *= (1f + bonusDmg);
                }
            }

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
                case SkillType.Heal:
                    CastHealSkill(skill);
                    return true; // Heal不需要动画
                case SkillType.Summon:
                    CastSummonSkill(skill);
                    break;
            }

            return true;
        }

        /// <summary>范围伤害技能（如落石术），支持蓄力倍率 + GDD 6.5 灵物修饰</summary>
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
                    FxFactory.SpawnElementBurst(targetPos + Vector3.up * 0.05f, skill.elementTag, actualRadius, Mathf.Max(0.4f, skill.vfxDuration * 0.8f));
                }

                var hits = Physics.OverlapSphere(targetPos, actualRadius, enemyLayer);
                hitList = new System.Collections.Generic.List<Collider>(hits);
                GameObject firstSkillHit = null;
                foreach (var hit in hits)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float damage = (SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling) * damageMul;
                        damageable.OnDamage(damage, hit.transform.position, gameObject);
                        if (firstSkillHit == null) firstSkillHit = hit.gameObject;
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
                if (skill.elementTag != ElementTag.None)
                {
                    SkillModifierApplier.ApplyElementImpact(skill.elementTag, targetPos, hitList, _player);
                }

                // GDD 6.5：槽位灵物修饰落地触发（cube 临时特效 / 灼烧 / 冻结 / 雷击 / 持续地带）
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
            }
        }

        /// <summary>投射物技能（支持多发散射），支持蓄力倍率</summary>
        private void CastProjectileSkill(SkillData skill, float damageMul = 1f)
        {
            Vector3 spawnPos = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 dir = _player.AimDirection;
            float damage = (SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling) * damageMul;

            int count = Mathf.Max(1, skill.projectileCount);
            float halfSpread = skill.spreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                // 计算每发投射物的方向
                Vector3 projDir = dir;
                if (count > 1)
                {
                    float angle = Mathf.Lerp(-halfSpread, halfSpread, (float)i / (count - 1));
                    projDir = Quaternion.Euler(0, angle, 0) * dir;
                }

                if (skill.projectilePrefab != null)
                {
                    GameObject proj;
                    if (ObjectPool.Instance != null)
                        proj = ObjectPool.Instance.Get(skill.projectilePrefab, spawnPos, Quaternion.LookRotation(projDir));
                    else
                        proj = Instantiate(skill.projectilePrefab, spawnPos, Quaternion.LookRotation(projDir));

                    var projectile = proj.GetComponent<Projectile>();
                    if (projectile != null)
                        projectile.Initialize(damage, projDir, skill.projectileSpeed, 0, 0, skill.elementTag, _player);
                }
                else if (showDebugVisuals)
                {
                    // 没有Prefab时创建Debug投射物（带元素颜色提示）
                    CreateDebugProjectile(spawnPos, projDir, skill.projectileSpeed, damage, skill.vfxDuration, skill.elementTag);
                }
            }
        }

        /// <summary>增益技能（如金钟罩）</summary>
        private void CastBuffSkill(SkillData skill, int slotIndex = -1)
        {
            // 简单实现：临时增加减伤
            var stats = _player.Stats;
            float originalReduction = stats.damageReduction;
            stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.5f);

            // GDD 6.5：buff 类技能也支持槽位修饰。例如金钟罩 + 火灵珠 → 护火金钟（玩家周围 zone 烧敌人）
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

            // 延迟恢复
            StartCoroutine(BuffDurationCoroutine(stats, originalReduction, skill.vfxDuration));

            Debug.Log($"<color=cyan>{skill.skillName} 启动！减伤 +50%，持续 {skill.vfxDuration}秒</color>");
        }

        private System.Collections.IEnumerator BuffDurationCoroutine(CombatStats stats, float originalReduction, float duration)
        {
            yield return new WaitForSeconds(duration);
            stats.damageReduction = originalReduction;
            Debug.Log("<color=cyan>金钟罩结束</color>");
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

                        // 应用灵物CD缩减
                        var spiritSlots = GetComponent<SpiritSlotSystem>();
                        if (spiritSlots != null)
                        {
                            float cdReduction = spiritSlots.GetSkillCooldownReduction(i);
                            rechargeTime *= (1f - cdReduction);
                        }

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

            // 如果留下伤害区域，对路径上的敌人造成伤害
            if (skill.leaveTrail)
            {
                float damage = SkillTuning.EffectiveBaseDamage(skill) + _player.Stats.attackDamage * skill.damageScaling;
                var hits = Physics.OverlapCapsule(startPos + Vector3.up * 0.5f,
                    targetPos + Vector3.up * 0.5f, 1.5f, enemyLayer);
                foreach (var hit in hits)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                        damageable.OnDamage(damage, hit.transform.position, gameObject);
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
            Vector3 spawnPos = transform.position + _player.AimDirection * 2f;
            float damage = skill.summonDamage + _player.Stats.attackDamage * skill.damageScaling;
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
        private void CreateDebugProjectile(Vector3 spawnPos, Vector3 direction, float speed, float damage, float lifetime, ElementTag elementTag = ElementTag.None)
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
            projComp.Initialize(damage, direction, speed, 0, 0, elementTag, _player);

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
