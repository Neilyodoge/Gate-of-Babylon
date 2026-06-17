using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// AOE 法师敌人
    /// 在玩家脚下释放范围攻击，攻击前有红色圆形预警
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyMage : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 25f,
            currentHp = 25f,
            attackDamage = 12f,
            moveSpeed = 2f
        };

        [Header("AI 参数")]
        [SerializeField] private float castRange = 13f;
        [SerializeField] private float preferredRange = 9f;
        [SerializeField] private float attackInterval = 3.5f;
        [SerializeField] private float warningDuration = 1.0f;
        [SerializeField] private float aoeRadius = 2.5f;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private SkillData[] possibleSkillDrops;
        [SerializeField] private int _roomLevel;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;
        private float _stunTimer;

        // 施法状态
        private bool _isCasting;
        private float _castTimer;
        private Vector3 _castTargetPos;
        private GameObject _warningCircle;

        // 受击表现
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;

        // 瞬移闪避
        private float _teleportTimer;
        private const float TELEPORT_COOLDOWN = 5f;

        private EnemyHealthBar _healthBar;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = MaterialHelper.SafeGetColor(_renderers[i].material);
        }

        private void Start()
        {
            stats.ResetHp();
            _healthBar = EnemyHealthBar.Create(gameObject);
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;
            _attackTimer = attackInterval * 0.5f; // 首次攻击快一些
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0)
                    RestoreColors();
            }

            if (_stunTimer > 0)
            {
                _stunTimer -= Time.deltaTime;
                return;
            }

            // 瞬移CD更新
            if (_teleportTimer > 0) _teleportTimer -= Time.deltaTime;

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            // 施法中
            if (_isCasting)
            {
                _castTimer -= Time.deltaTime;
                UpdateWarningCircle();

                if (_castTimer <= 0)
                {
                    ExecuteAOE();
                    _isCasting = false;
                    DestroyWarningCircle();
                    _attackTimer = attackInterval;
                }
                return;
            }

            // 保持距离
            if (distToTarget < preferredRange - 2f)
            {
                Vector3 fleeDir = (transform.position - _target.position).normalized;
                fleeDir.y = 0;
                Vector3 velocity = fleeDir * stats.moveSpeed;
                velocity.y = -9.8f;
                _cc.Move(velocity * Time.deltaTime);
            }
            else if (distToTarget > castRange)
            {
                MoveTowards(_target.position);
            }

            // 攻击
            if (distToTarget <= castRange)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                    StartCasting();
            }

            // 朝向
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

        private void StartCasting()
        {
            _isCasting = true;
            _castTimer = warningDuration;
            _castTargetPos = _target.position; // 锁定目标位置
            CreateWarningCircle();

            SetAllRenderersColor(new Color(0.6f, 0.1f, 0.8f)); // 紫色蓄力
        }

        private void CreateWarningCircle()
        {
            DestroyWarningCircle();
            _warningCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _warningCircle.name = "[Warning] AOEZone";
            _warningCircle.transform.position = _castTargetPos + Vector3.up * 0.05f;
            _warningCircle.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);

            var col = _warningCircle.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = _warningCircle.GetComponent<Renderer>();
            if (rend != null)
            {
                Color warnColor = new Color(0.85f, 0.15f, 0.15f, 0.28f);
                var mat = MaterialHelper.CreateLitTransparent(warnColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.2f) * 1.6f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 同步生成一道紫色 AOE 圆环（外圈高亮，从开始时就能看清"圈在哪"）
            FxFactory.SpawnAOERing(_castTargetPos + Vector3.up * 0.05f, aoeRadius,
                new Color(0.85f, 0.2f, 0.85f, 1f), lifetime: warningDuration);

            // 法师身上向目标点引一条紫色魔气线（视觉上有"导引"感）
            FxFactory.SpawnSliceLine(transform.position + Vector3.up * 1.4f,
                (_castTargetPos - transform.position),
                Vector3.Distance(transform.position, _castTargetPos),
                new Color(0.85f, 0.2f, 0.85f, 1f), lifetime: warningDuration * 0.8f);
        }

        private void UpdateWarningCircle()
        {
            if (_warningCircle == null) return;

            // 逐渐变红变不透明
            float progress = 1f - (_castTimer / warningDuration);
            var rend = _warningCircle.GetComponent<Renderer>();
            if (rend != null)
            {
                float alpha = Mathf.Lerp(0.15f, 0.5f, progress);
                float pulse = Mathf.PingPong(Time.time * 5f, 0.15f);
                rend.material.color = new Color(0.9f, 0.1f, 0.1f, alpha + pulse);
            }

            // 缩放脉冲
            float scale = aoeRadius * 2f * (1f + Mathf.Sin(Time.time * 8f) * 0.03f);
            _warningCircle.transform.localScale = new Vector3(scale, 0.05f, scale);
        }

        private void ExecuteAOE()
        {
            RestoreColors();

            // 升级：用 FxFactory.SpawnElementBurst（紫色作为"魔法"系视觉）+ AOE 爆环 + 镜头中震
            FxFactory.SpawnElementBurst(_castTargetPos + Vector3.up * 0.3f,
                ElementTag.Pierce,  // 用 Pierce 拿到接近紫白的颜色（Pierce: 0.92, 0.92, 0.95）
                aoeRadius * 1.1f, lifetime: 0.55f);
            FxFactory.SpawnAOERing(_castTargetPos + Vector3.up * 0.05f, aoeRadius * 1.05f,
                new Color(0.85f, 0.2f, 0.85f, 1f), lifetime: 0.55f);

            // 紫色爆炸球（保留原 vibe，但用更稳的 CreateLitTransparent + emission）
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "[VFX] AOE_Explosion";
            explosion.transform.position = _castTargetPos + Vector3.up * 0.5f;
            explosion.transform.localScale = Vector3.one * aoeRadius * 2f;
            var expCol = explosion.GetComponent<Collider>();
            if (expCol != null) Destroy(expCol);

            var expRend = explosion.GetComponent<Renderer>();
            if (expRend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(new Color(0.8f, 0.2f, 0.8f, 0.55f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.85f, 0.25f, 0.95f) * 3.2f);
                }
                expRend.material = mat;
                expRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Destroy(explosion, 0.5f);

            CameraShake.TriggerLight();

            // 范围伤害
            var hits = Physics.OverlapSphere(_castTargetPos, aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        float tDef = damageable.Stats != null ? damageable.Stats.defense : 0f;
                        var (mageDmg, _) = stats.CalcSkillDamage(tDef, 1f);
                        damageable.OnDamage(mageDmg, _castTargetPos, gameObject);
                        CameraShake.TriggerMedium();
                    }
                }
            }
        }

        private void DestroyWarningCircle()
        {
            if (_warningCircle != null)
            {
                Destroy(_warningCircle);
                _warningCircle = null;
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
                Damage = actual, IsCrit = false, IsPlayerDamage = false
            });

            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            SetAllRenderersColor(Color.white);
            _hitFlashTimer = 0.1f;

            _stunTimer = 0.35f;
            if (_isCasting)
            {
                _isCasting = false;
                DestroyWarningCircle();
                RestoreColors();
            }

            // 法师受击后35%概率瞬移到随机位置
            if (_teleportTimer <= 0 && Random.value < 0.35f && attacker != null)
            {
                _stunTimer = 0;
                TryTeleport();
            }

            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.4f;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerNormal();
            if (!stats.IsAlive) OnDeath();
        }

        /// <summary>瞬移闪避（法师特有）</summary>
        private void TryTeleport()
        {
            _teleportTimer = TELEPORT_COOLDOWN;

            // 瞬移到远离玩家的随机位置
            Vector3 awayDir = (transform.position - _target.position).normalized;
            float teleportDist = Random.Range(4f, 7f);
            Vector3 targetPos = transform.position + awayDir * teleportDist
                + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

            // 起点特效
            var startVfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            startVfx.name = "[VFX] TeleportStart";
            startVfx.transform.position = transform.position + Vector3.up * 1f;
            startVfx.transform.localScale = Vector3.one * 1.5f;
            var startCol = startVfx.GetComponent<Collider>();
            if (startCol != null) Destroy(startCol);
            var startRend = startVfx.GetComponent<Renderer>();
            if (startRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.6f, 0.2f, 0.8f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.6f, 0.2f, 0.8f) * 2f);
                startRend.material = mat;
            }
            Destroy(startVfx, 0.5f);

            // 瞬移
            _cc.enabled = false;
            transform.position = targetPos;
            _cc.enabled = true;

            Debug.Log("<color=magenta>法师瞬移！</color>");
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";

            DestroyWarningCircle();
            TryDropItem();
            TryDropSkill();
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject, Position = transform.position
            });

            if (PlayerController.Instance != null)
            {
                float healAmount = PlayerController.Instance.Inventory.GetHealOnKill();
                if (healAmount > 0) PlayerController.Instance.Stats.Heal(healAmount);
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerKill();
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem()
        {
            if (possibleDrops == null || possibleDrops.Length == 0) return;
            var config = GameConfig.Instance;
            float chance = 0.08f;
            if (config != null) chance = config.debugMaxItemDropRate ? 1f : config.敌人掉落概率;
            if (Random.value > chance) return;
            // 过滤 null 元素
            var valid = new System.Collections.Generic.List<ItemData>();
            foreach (var d in possibleDrops) if (d != null) valid.Add(d);
            if (valid.Count == 0) return;
            ItemData item = valid[Random.Range(0, valid.Count)];
            ItemPickup.Spawn(item, transform.position);
        }

        private void TryDropSkill()
        {
            if (possibleSkillDrops == null || possibleSkillDrops.Length == 0) return;
            var config = GameConfig.Instance;
            float chance = 0.03f;
            if (config != null)
                chance = config.debugMaxSkillDropRate ? 1f : config.功法掉落概率;
            if (Random.value > chance) return;
            var skill = SkillPickup.PickValid(possibleSkillDrops);
            if (skill != null)
                SkillPickup.Spawn(skill, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
        }

        /// <summary>设置功法掉落池</summary>
        public void SetSkillDrops(SkillData[] skills) => possibleSkillDrops = skills;

        private System.Collections.IEnumerator DeathAnimation()
        {
            enabled = false;
            _cc.enabled = false;
            float timer = 0.3f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                transform.localScale = startScale * (timer / 0.3f);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>工厂方法：生成AOE法师</summary>
        public static EnemyMage Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f,
            int roomLevel = 0, ItemData[] drops = null)
        {
            var prefabs = MonsterPrefabs.Instance;
            var prefab = prefabs != null ? prefabs.法师敌人Prefab : null;
            var go = MonsterPrefabs.InstantiateMonster(prefab, position, "Enemy_Mage");
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            EnemyBase.SetLayerRecursively(go, go.layer);

            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.5f, 0.2f, 0.8f);
            }

            var existingCols = go.GetComponentsInChildren<Collider>();
            foreach (var c in existingCols) Object.Destroy(c);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 2f;
            cc.center = new Vector3(0, 1f, 0);

            var enemy = go.AddComponent<EnemyMage>();
            var config = GameConfig.Instance;
            if (config != null)
            {
                enemy.stats.maxHp = config.敌人基础血量 * 0.8f * hpMultiplier;
                enemy.stats.attackDamage = config.敌人基础攻击力 * 1.5f * dmgMultiplier;
                enemy.stats.defense = config.敌人基础防御力 * 0.6f;
            }
            else
            {
                enemy.stats.maxHp = 25f * hpMultiplier;
                enemy.stats.attackDamage = 12f * dmgMultiplier;
                enemy.stats.defense = 1.8f;
            }
            enemy.stats.currentHp = enemy.stats.maxHp;
            enemy._roomLevel = roomLevel;
            if (drops != null) enemy.possibleDrops = drops;

            return enemy;
        }

        private void OnDestroy()
        {
            DestroyWarningCircle();
        }

        /// <summary>设置所有Renderer的颜色</summary>
        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) MaterialHelper.SafeSetColor(r.material, color);
        }

        /// <summary>恢复所有Renderer的原始颜色</summary>
        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && i < _originalColors.Length)
                    MaterialHelper.SafeSetColor(_renderers[i].material, _originalColors[i]);
        }
    }
}
