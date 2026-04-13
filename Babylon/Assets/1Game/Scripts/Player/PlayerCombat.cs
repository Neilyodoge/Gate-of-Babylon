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

        [Header("技能槽位")]
        [SerializeField] private SkillData skillQ;
        [SerializeField] private SkillData skillE;
        [SerializeField] private SkillData skillR;

        [Header("Debug 可视化")]
        [SerializeField] private bool showDebugVisuals = true;

        private PlayerController _player;
        private PlayerAnimator _playerAnim;
        private float _skillQCooldown;
        private float _skillECooldown;
        private float _skillRCooldown;

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
            if (kb == null) return;

            // Q 技能
            if (kb.qKey.wasPressedThisFrame && skillQ != null && _skillQCooldown <= 0)
            {
                if (UseSkill(skillQ, 0))
                    _skillQCooldown = skillQ.cooldown;
            }

            // E 技能
            if (kb.eKey.wasPressedThisFrame && skillE != null && _skillECooldown <= 0)
            {
                if (UseSkill(skillE, 1))
                    _skillECooldown = skillE.cooldown;
            }

            // R 技能
            if (kb.rKey.wasPressedThisFrame && skillR != null && _skillRCooldown <= 0)
            {
                if (UseSkill(skillR, 2))
                    _skillRCooldown = skillR.cooldown;
            }
        }

        /// <summary>使用技能（返回是否成功释放）</summary>
        private bool UseSkill(SkillData skill, int slotIndex)
        {
            if (skill == null) return false;

            // Buff类技能立即生效，不需要播放技能动画
            if (skill.skillType == SkillType.Buff)
            {
                Debug.Log($"<color=cyan>释放功法：{skill.skillName}</color>");
                CastBuffSkill(skill);
                return true;
            }

            // 计算技能释放速度：优先使用技能自身配置，否则使用全局配置
            float castSpeed = skill.castSpeed > 0.01f ? skill.castSpeed : 1f;
            var config = GameConfig.Instance;
            if (config != null && Mathf.Approximately(castSpeed, 1f))
                castSpeed = config.技能释放速度;

            // 尝试播放技能动画（遵循优先级系统）
            if (!_playerAnim.PlaySkill(castSpeed)) return false;

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
            }

            return true;
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
                else if (showDebugVisuals)
                {
                    // 没有VFX时创建Debug可视化：用半透明Cube表示落石
                    CreateDebugAreaIndicator(targetPos, skill.aoeRadius, skill.vfxDuration,
                        new Color(0.8f, 0.3f, 0.1f, 0.6f));
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

        /// <summary>增益技能（如金钟罩）</summary>
        private void CastBuffSkill(SkillData skill)
        {
            // 简单实现：临时增加减伤
            var stats = _player.Stats;
            float originalReduction = stats.damageReduction;
            stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.5f);

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
                // 没有VFX时创建Debug可视化：用半透明球体表示护盾
                CreateDebugShieldIndicator(skill.vfxDuration, new Color(1f, 0.85f, 0.1f, 0.3f));
            }

            // 延迟恢复
            StartCoroutine(BuffDurationCoroutine(stats, originalReduction, skill.vfxDuration));

            Debug.Log($"<color=cyan>金钟罩启动！减伤 +50%，持续 {skill.vfxDuration}秒</color>");
        }

        private System.Collections.IEnumerator BuffDurationCoroutine(CombatStats stats, float originalReduction, float duration)
        {
            yield return new WaitForSeconds(duration);
            stats.damageReduction = originalReduction;
            Debug.Log("<color=cyan>金钟罩结束</color>");
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

            if (_skillECooldown > 0)
            {
                _skillECooldown -= Time.deltaTime;
                if (skillE != null)
                {
                    GameEvents.Publish(new GameEvents.SkillCooldownUpdate
                    {
                        SlotIndex = 1,
                        RemainingTime = Mathf.Max(0, _skillECooldown),
                        TotalCooldown = skillE.cooldown
                    });
                }
            }

            if (_skillRCooldown > 0)
            {
                _skillRCooldown -= Time.deltaTime;
                if (skillR != null)
                {
                    GameEvents.Publish(new GameEvents.SkillCooldownUpdate
                    {
                        SlotIndex = 2,
                        RemainingTime = Mathf.Max(0, _skillRCooldown),
                        TotalCooldown = skillR.cooldown
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

        /// <summary>装备技能到E槽位</summary>
        public void EquipSkillE(SkillData skill)
        {
            skillE = skill;
            _skillECooldown = 0;
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
            _skillRCooldown = 0;
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

        /// <summary>运行时绘制攻击扇形范围（Debug.DrawLine，Game视图可见）</summary>
        private void DrawAttackRange(Color color)
        {
            Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
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

            Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.8f;
            Vector3 forward = Application.isPlaying ? _player.AimDirection : transform.forward;

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
