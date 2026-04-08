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

        [Header("刀光特效")]
        [SerializeField] private GameObject slashVFXPrefab;
        [SerializeField] private Transform slashVFXSpawnPoint;

        [Header("打击特效")]
        [SerializeField] private GameObject hitVFXPrefab;

        [Header("技能槽位（Demo1: 仅Q槽位）")]
        [SerializeField] private SkillData skillQ;

        private PlayerController _player;
        private PlayerAnimator _playerAnim;
        private float _skillQCooldown;

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
            if (!_player.Stats.IsAlive || _player.IsDashing) return;

            HandleMeleeAttack();
            HandleSkills();
            UpdateCooldowns();
            CheckMeleeHit();
        }

        // ==================== 近战攻击 ====================

        /// <summary>鼠标左键触发近战连招</summary>
        private void HandleMeleeAttack()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _playerAnim.RequestAttack();
            }
        }

        /// <summary>在攻击判定窗口内检测敌人</summary>
        private void CheckMeleeHit()
        {
            if (!_playerAnim.IsHitWindowOpen) return;

            // 每段攻击只判定一次
            if (_lastHitComboStep == _playerAnim.ComboStep && _hasHitThisSwing) return;

            // 扇形范围检测
            Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 forward = _player.AimDirection;

            var colliders = Physics.OverlapSphere(origin, meleeRange, enemyLayer);
            bool hitAny = false;

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

                        // 播放打击特效
                        SpawnHitVFX(hitPoint);
                        hitAny = true;
                    }
                }
            }

            if (hitAny)
            {
                _hasHitThisSwing = true;
                _lastHitComboStep = _playerAnim.ComboStep;
            }
        }

        /// <summary>连招段数伤害倍率</summary>
        private float GetComboDamageMultiplier(int comboStep)
        {
            switch (comboStep)
            {
                case 0: return 1.0f;  // 第一段：标准伤害
                case 1: return 1.2f;  // 第二段：120%
                case 2: return 1.5f;  // 第三段：150%（终结技）
                default: return 1.0f;
            }
        }

        // ==================== 特效 ====================

        /// <summary>动画事件触发刀光特效</summary>
        private void OnSlashVFXRequested(GameEvents.SlashVFXRequested evt)
        {
            // 重置判定状态（新的一段攻击开始）
            _hasHitThisSwing = false;

            if (slashVFXPrefab == null) return;

            Vector3 spawnPos = slashVFXSpawnPoint != null
                ? slashVFXSpawnPoint.position
                : transform.position + _player.AimDirection * 1f + Vector3.up * 1f;

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

        /// <summary>技能释放</summary>
        private void HandleSkills()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame && skillQ != null && _skillQCooldown <= 0)
            {
                UseSkill(skillQ);
                _skillQCooldown = skillQ.cooldown;
            }
        }

        /// <summary>使用技能</summary>
        private void UseSkill(SkillData skill)
        {
            if (skill == null) return;

            // 尝试播放技能动画（遵循优先级系统）
            if (!_playerAnim.PlaySkill()) return;

            Debug.Log($"<color=cyan>释放功法：{skill.skillName}</color>");

            switch (skill.skillType)
            {
                case SkillType.AreaDamage:
                    CastAreaSkill(skill);
                    break;
                case SkillType.Projectile:
                    CastProjectileSkill(skill);
                    break;
                case SkillType.Dash:
                    break;
                case SkillType.Buff:
                    break;
            }
        }

        /// <summary>范围伤害技能（如落石术）</summary>
        private void CastAreaSkill(SkillData skill)
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

                var hits = Physics.OverlapSphere(targetPos, skill.aoeRadius, enemyLayer);
                foreach (var hit in hits)
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float damage = skill.baseDamage + _player.Stats.attackDamage * skill.damageScaling;
                        damageable.OnDamage(damage, hit.transform.position, gameObject);
                    }
                }
            }
        }

        /// <summary>投射物技能</summary>
        private void CastProjectileSkill(SkillData skill)
        {
            if (skill.projectilePrefab == null) return;

            Vector3 spawnPos = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 dir = _player.AimDirection;

            GameObject proj;
            if (ObjectPool.Instance != null)
                proj = ObjectPool.Instance.Get(skill.projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            else
                proj = Instantiate(skill.projectilePrefab, spawnPos, Quaternion.LookRotation(dir));

            var projectile = proj.GetComponent<Projectile>();
            if (projectile != null)
            {
                float damage = skill.baseDamage + _player.Stats.attackDamage * skill.damageScaling;
                projectile.Initialize(damage, dir, skill.projectileSpeed, 0, 0);
            }
        }

        /// <summary>更新冷却</summary>
        private void UpdateCooldowns()
        {
            if (_skillQCooldown > 0)
            {
                _skillQCooldown -= Time.deltaTime;
                if (skillQ != null)
                {
                    GameEvents.Publish(new GameEvents.SkillCooldownUpdate
                    {
                        SlotIndex = 0,
                        RemainingTime = Mathf.Max(0, _skillQCooldown),
                        TotalCooldown = skillQ.cooldown
                    });
                }
            }
        }

        /// <summary>装备技能到Q槽位</summary>
        public void EquipSkillQ(SkillData skill)
        {
            skillQ = skill;
            _skillQCooldown = 0;
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
    }
}
