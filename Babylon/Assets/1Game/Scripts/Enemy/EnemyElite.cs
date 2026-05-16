using UnityEngine;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 精英怪词缀类型
    /// </summary>
    public enum EliteAffix
    {
        /// <summary>狂暴：攻击速度+50%，移动速度+30%</summary>
        Berserk,
        /// <summary>铁壁：减伤40%，击退抗性</summary>
        Ironwall,
        /// <summary>分裂：死亡时分裂为2个小怪</summary>
        Splitting,
        /// <summary>雷电：攻击附带范围闪电链</summary>
        Lightning,
        /// <summary>吸血：攻击回复自身10%伤害的生命</summary>
        Vampiric,
        /// <summary>冰霜：攻击减速玩家30%持续2秒</summary>
        Frost
    }

    /// <summary>
    /// 精英怪 —— 带词缀的强化敌人
    /// 比普通怪更强，有特殊词缀效果
    /// 头顶有金色"精英"标识和词缀名称
    /// 击杀必定掉落灵物
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyElite : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 90f,
            currentHp = 90f,
            attackDamage = 12f,
            moveSpeed = 3.5f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 15f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackInterval = 1.2f;

        [Header("精英词缀")]
        [SerializeField] private EliteAffix affix1;
        [SerializeField] private EliteAffix affix2;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private SkillData[] possibleSkillDrops;
        [SerializeField] private int _roomLevel;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;
        private float _stunTimer;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;

        // 攻击预警
        private GameObject _attackWarning;
        private float _attackPrepTimer;
        private bool _isPreparing;
        private float _warningDuration = 0.4f;

        // 血条和名牌
        private EnemyHealthBar _healthBar;
        private GameObject _eliteTag;

        // 词缀效果
        private bool _hasIronwall;
        private bool _hasSplitting;
        private bool _hasLightning;
        private bool _hasVampiric;
        private bool _hasFrost;

        // 闪电链CD
        private float _lightningTimer;
        private const float LIGHTNING_INTERVAL = 3f;

        // 减速效果追踪
        private float _slowTimer;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material.color;
        }

        private void Start()
        {
            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);

            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            // 应用词缀效果
            ApplyAffixes();

            // 创建精英标识
            CreateEliteTag();
        }

        private void ApplyAffixes()
        {
            ApplyAffix(affix1);
            ApplyAffix(affix2);
        }

        private void ApplyAffix(EliteAffix affix)
        {
            switch (affix)
            {
                case EliteAffix.Berserk:
                    stats.attackSpeed *= 1.5f;
                    stats.moveSpeed *= 1.3f;
                    attackInterval *= 0.7f;
                    break;
                case EliteAffix.Ironwall:
                    _hasIronwall = true;
                    stats.damageReduction = 0.4f;
                    break;
                case EliteAffix.Splitting:
                    _hasSplitting = true;
                    break;
                case EliteAffix.Lightning:
                    _hasLightning = true;
                    break;
                case EliteAffix.Vampiric:
                    _hasVampiric = true;
                    break;
                case EliteAffix.Frost:
                    _hasFrost = true;
                    break;
            }
        }

        private void CreateEliteTag()
        {
            var canvas = new GameObject("EliteTagCanvas");
            canvas.transform.SetParent(transform);
            canvas.transform.localPosition = new Vector3(0, 3f, 0);
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 0.6f);
            canvas.transform.localScale = Vector3.one * 0.015f;

            var textGo = new GameObject("EliteText");
            textGo.transform.SetParent(canvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = $"⚔ 精英 · {GetAffixName(affix1)} / {GetAffixName(affix2)}";
            text.fontSize = 20;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(1f, 0.85f, 0.2f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            _eliteTag = canvas;
        }

        private static string GetAffixName(EliteAffix affix)
        {
            return affix switch
            {
                EliteAffix.Berserk => "狂暴",
                EliteAffix.Ironwall => "铁壁",
                EliteAffix.Splitting => "分裂",
                EliteAffix.Lightning => "雷电",
                EliteAffix.Vampiric => "吸血",
                EliteAffix.Frost => "冰霜",
                _ => "未知"
            };
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            // 受击闪烁恢复
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                    RestoreColors();
            }

            // 硬直
            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            // 名牌面向相机
            if (_eliteTag != null && Camera.main != null)
                _eliteTag.transform.rotation = Quaternion.LookRotation(
                    _eliteTag.transform.position - Camera.main.transform.position);

            // 闪电链CD
            if (_hasLightning && _lightningTimer > 0)
                _lightningTimer -= Time.deltaTime;

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            // 攻击预警阶段
            if (_isPreparing)
            {
                _attackPrepTimer -= Time.deltaTime;
                UpdateAttackWarning();
                if (_attackPrepTimer <= 0)
                {
                    Attack();
                    _isPreparing = false;
                    DestroyAttackWarning();
                    _attackTimer = attackInterval;
                }
                return;
            }

            if (distToTarget <= attackRange)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                    StartAttackPrep();
            }
            else if (distToTarget <= detectRange)
            {
                MoveTowards(_target.position);
            }

            // 朝向目标
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            Vector3 velocity = dir * stats.moveSpeed;
            velocity.y = -9.8f;
            _cc.Move(velocity * Time.deltaTime);
        }

        private void StartAttackPrep()
        {
            _isPreparing = true;
            _attackPrepTimer = _warningDuration;
            CreateAttackWarning();
            SetAllRenderersColor(new Color(1f, 0.85f, 0.2f)); // 金色蓄力
        }

        private void Attack()
        {
            if (_target == null) return;
            RestoreColors();

            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > attackRange * 1.5f) return;

            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float damage = stats.CalculateDamage();
                damageable.OnDamage(damage, transform.position, gameObject);

                // 吸血词缀：回复伤害的10%
                if (_hasVampiric)
                {
                    float heal = damage * 0.1f;
                    stats.Heal(heal);
                    if (_healthBar != null)
                        _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);
                }

                // 冰霜词缀：减速玩家
                if (_hasFrost && PlayerController.Instance != null)
                {
                    var playerStats = PlayerController.Instance.Stats;
                    if (_slowTimer <= 0)
                    {
                        playerStats.moveSpeed *= 0.7f;
                        _slowTimer = 2f;
                        StartCoroutine(FrostSlowCoroutine(playerStats));
                    }
                }
            }

            // 雷电词缀：攻击时释放闪电链
            if (_hasLightning && _lightningTimer <= 0)
            {
                CastLightningChain();
                _lightningTimer = LIGHTNING_INTERVAL;
            }
        }

        private System.Collections.IEnumerator FrostSlowCoroutine(CombatStats playerStats)
        {
            yield return new WaitForSeconds(2f);
            playerStats.moveSpeed /= 0.7f;
            _slowTimer = 0;
        }

        private void CastLightningChain()
        {
            // 闪电链：对玩家位置周围造成范围伤害
            Vector3 targetPos = _target.position;
            float radius = 2.5f;
            float damage = stats.attackDamage * 0.6f;

            // 视觉效果：闪电球
            var lightning = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightning.name = "[VFX] Lightning";
            lightning.transform.position = targetPos + Vector3.up * 1f;
            lightning.transform.localScale = Vector3.one * radius;
            var col = lightning.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = lightning.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.6f, 1f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 3f);
                rend.material = mat;
            }
            Destroy(lightning, 0.4f);

            // 画闪电线
            Debug.DrawLine(transform.position + Vector3.up, targetPos + Vector3.up,
                new Color(0.3f, 0.6f, 1f), 0.3f);

            // 范围伤害
            var hits = Physics.OverlapSphere(targetPos, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var dmg = hit.GetComponent<IDamageable>();
                    if (dmg != null)
                        dmg.OnDamage(damage, targetPos, gameObject);
                }
            }
        }

        // ========== 预警 ==========

        private void CreateAttackWarning()
        {
            DestroyAttackWarning();
            _attackWarning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _attackWarning.name = "[Warning] EliteZone";
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;
            _attackWarning.transform.localScale = new Vector3(attackRange * 2.5f, 0.05f, attackRange * 2.5f);

            var wCol = _attackWarning.GetComponent<Collider>();
            if (wCol != null) Destroy(wCol);

            var wRend = _attackWarning.GetComponent<Renderer>();
            if (wRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.85f, 0.2f, 0.2f);
                wRend.material = mat;
            }
        }

        private void UpdateAttackWarning()
        {
            if (_attackWarning == null) return;
            _attackWarning.transform.position = transform.position + Vector3.up * 0.05f;
            float progress = 1f - (_attackPrepTimer / _warningDuration);
            var wRend = _attackWarning.GetComponent<Renderer>();
            if (wRend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.45f, progress);
                wRend.material.color = new Color(1f, 0.85f, 0.2f, alpha);
            }
        }

        private void DestroyAttackWarning()
        {
            if (_attackWarning != null)
            {
                Destroy(_attackWarning);
                _attackWarning = null;
            }
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;

            float actual = stats.TakeDamage(damage);

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = false,
                IsPlayerDamage = false
            });

            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            // 铁壁词缀：硬直更短，击退更小
            float stunTime = _hasIronwall ? 0.15f : 0.25f;
            _stunTimer = stunTime;
            _isPreparing = false;
            DestroyAttackWarning();

            if (attacker != null)
            {
                float knockbackForce = _hasIronwall ? 0.15f : 0.4f;
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * knockbackForce;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerHeavy();

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";
            DestroyAttackWarning();

            // 精英怪必定掉落灵物
            TryDropItem(true);
            TryDropSkill();

            // v0.5 搜打撤：精英怪 40% 概率额外掉一件【洞府素材】（独立 roll，不抢灵物槽位）
            // 灵气浓度叠加
            float eliteCaveChance = 0.4f + SpiritDensity.CaveMaterialBonusChance;
            CaveMaterialPool.SpawnRandom(transform.position + new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f)), eliteCaveChance);

            // 分裂词缀：死亡时分裂为2个小怪
            if (_hasSplitting)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
                    var minion = EnemyBase.Spawn(transform.position + offset, 0.4f, 0.4f, _roomLevel, possibleDrops);
                    if (minion != null && possibleSkillDrops != null)
                        minion.SetSkillDrops(possibleSkillDrops);
                }
                Debug.Log("<color=yellow>⚔ 精英怪分裂！</color>");
            }

            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            if (PlayerController.Instance != null)
            {
                float healAmount = PlayerController.Instance.Inventory.GetHealOnKill();
                if (healAmount > 0)
                    PlayerController.Instance.Stats.Heal(healAmount);
            }

            if (HitStop.Instance != null)
                HitStop.Instance.TriggerKill();

            Debug.Log("<color=yellow>⚔ 精英怪被击败！</color>");
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem(bool guaranteed)
        {
            if (possibleDrops == null || possibleDrops.Length == 0) return;

            // 过滤掉 null 元素
            var validDrops = new List<ItemData>();
            foreach (var d in possibleDrops)
                if (d != null) validDrops.Add(d);
            if (validDrops.Count == 0) return;

            if (!guaranteed)
            {
                var config = GameConfig.Instance;
                float chance = config != null ? (config.debugMaxItemDropRate ? 1f : config.敌人掉落概率) : 0.05f;
                if (Random.value > chance) return;
            }

            // 精英怪掉落2个灵物
            for (int i = 0; i < 2; i++)
            {
                var config = GameConfig.Instance;
                ItemData selectedItem;
                if (config != null)
                {
                    ItemRarity targetRarity = config.RollRarity(_roomLevel);
                    var candidates = new List<ItemData>();
                    foreach (var item in validDrops)
                        if (item.rarity == targetRarity)
                            candidates.Add(item);
                    selectedItem = candidates.Count > 0
                        ? candidates[Random.Range(0, candidates.Count)]
                        : validDrops[Random.Range(0, validDrops.Count)];
                }
                else
                {
                    selectedItem = validDrops[Random.Range(0, validDrops.Count)];
                }

                if (selectedItem != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                    ItemPickup.Spawn(selectedItem, transform.position + offset);
                }
            }
        }

        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;
            // 精英怪25%概率掉功法（debug爆率拉满时必掉）
            var skillConfig = GameConfig.Instance;
            bool forceDropSkill = skillConfig != null && skillConfig.debugMaxSkillDropRate;
            if (!forceDropSkill && Random.value > 0.25f) return;
            var skill = possibleSkillDrops[Random.Range(0, possibleSkillDrops.Length)];
            if (skill != null)
                SkillPickup.Spawn(skill, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            enabled = false;
            _cc.enabled = false;
            float timer = 0.5f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.5f;
                transform.localScale = startScale * t;
                transform.Rotate(Vector3.up * 180f * Time.deltaTime);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>工厂方法：生成精英怪</summary>
        public static EnemyElite Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null, SkillData[] skillDrops = null)
        {
            var prefabs = MonsterPrefabs.Instance;
            // 精英怪使用普通小怪Prefab但放大
            var prefab = prefabs != null ? prefabs.普通小怪Prefab : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Elite");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            // 精英怪体型更大
            go.transform.localScale = (prefab != null ? Vector3.one : Vector3.one) * 1.4f;

            // 如果是回退胶囊体，设置金色
            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1f, 0.85f, 0.2f);
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.1f) * 0.5f);
                }
            }

            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.6f;
            cc.height = 2.2f;
            cc.center = new Vector3(0, 1.1f, 0);

            var elite = go.AddComponent<EnemyElite>();

            // 随机选择2个不同的词缀
            var allAffixes = (EliteAffix[])System.Enum.GetValues(typeof(EliteAffix));
            elite.affix1 = allAffixes[Random.Range(0, allAffixes.Length)];
            do
            {
                elite.affix2 = allAffixes[Random.Range(0, allAffixes.Length)];
            } while (elite.affix2 == elite.affix1);

            // 精英怪属性：使用GameConfig的精英倍率
            var config = GameConfig.Instance;
            float eliteHpMul = config != null ? config.精英怪血量倍率 : 3f;
            float eliteDmgMul = config != null ? config.精英怪伤害倍率 : 1.5f;

            if (config != null)
            {
                elite.stats.maxHp = config.敌人基础血量 * eliteHpMul * hpMultiplier;
                elite.stats.attackDamage = config.敌人基础攻击力 * eliteDmgMul * dmgMultiplier;
            }
            else
            {
                elite.stats.maxHp = 90f * hpMultiplier;
                elite.stats.attackDamage = 12f * dmgMultiplier;
            }
            elite.stats.currentHp = elite.stats.maxHp;
            elite._roomLevel = roomLevel;
            if (drops != null) elite.possibleDrops = drops;
            if (skillDrops != null) elite.possibleSkillDrops = skillDrops;

            Debug.Log($"<color=yellow>⚔ 精英怪出现！词缀：{GetAffixName(elite.affix1)} + {GetAffixName(elite.affix2)}</color>");
            return elite;
        }

        private void OnDestroy()
        {
            DestroyAttackWarning();
        }

        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) r.material.color = color;
        }

        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && i < _originalColors.Length)
                    _renderers[i].material.color = _originalColors[i];
        }
    }
}
