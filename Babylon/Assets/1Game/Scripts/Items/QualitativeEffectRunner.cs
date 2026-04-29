using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 质变效果运行器 —— 管理所有机制性质变效果
    /// 挂载在玩家身上，监听质变事件并运行对应的特殊机制
    /// </summary>
    public class QualitativeEffectRunner : MonoBehaviour
    {
        /// <summary>已激活的质变效果标记</summary>
        private readonly HashSet<string> _activeEffects = new();

        // ========== 玉碎（护体质变）==========
        private bool _jadeShieldReady = true;
        private float _jadeShieldCooldown = 60f;
        private float _jadeShieldTimer;

        // ========== 焚天（攻伐·火质变）==========
        private int _fireHitCounter;
        private const int FIRE_BURST_EVERY = 5; // 每5次攻击触发一次火焰冲击波
        private float _fireBurstRadius = 4f;
        private float _fireBurstDamage = 15f;

        // ========== 御风（身法质变）==========
        private float _phantomDuration = 3f;
        private float _phantomAttackInterval = 0.8f;
        private float _phantomDamageRatio = 0.3f;
        private readonly List<GameObject> _activePhantoms = new();

        // ========== 剑阵（攻伐·剑质变）==========
        private bool _swordOrbitActive;
        private readonly List<GameObject> _orbitSwords = new();
        private float _swordOrbitDamage = 8f;
        private float _swordOrbitRadius = 2.5f;
        private float _swordOrbitSpeed = 180f; // 度/秒
        private float _swordOrbitHitInterval = 1f;
        private float _swordOrbitHitTimer;

        // ========== 涅槃（丹药质变）==========
        private bool _nirvanaReady;
        private float _nirvanaInvincibleDuration = 3f;

        // ========== 嗜血（协同效果）==========
        private bool _bloodlustActive;
        private float _bloodlustDuration = 5f;
        private float _bloodlustTimer;
        private float _bloodlustHpDrain = 2f; // 每秒掉血

        // ========== 火墙（协同效果）==========
        private bool _fireTrailActive;
        private float _fireTrailDamage = 5f;
        private float _fireTrailDuration = 2f;

        // ========== 元素爆发（协同效果）==========
        private bool _elementBurstActive;
        private float _elementBurstInterval = 30f;
        private float _elementBurstTimer;
        private float _elementBurstRadius = 5f;
        private float _elementBurstDamage = 25f;

        private PlayerController _player;
        private CombatStats _stats;

        public static QualitativeEffectRunner Instance { get; private set; }

        /// <summary>玉碎是否就绪（UI显示用）</summary>
        public bool IsJadeShieldReady => _activeEffects.Contains("玉碎") && _jadeShieldReady;
        /// <summary>涅槃是否就绪（UI显示用）</summary>
        public bool IsNirvanaReady => _activeEffects.Contains("涅槃") && _nirvanaReady;
        /// <summary>嗜血状态是否激活</summary>
        public bool IsBloodlustActive => _bloodlustActive;
        /// <summary>获取所有已激活的质变效果名称</summary>
        public IReadOnlyCollection<string> ActiveEffects => _activeEffects;

        private void Awake()
        {
            Instance = this;
            _player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.QualitativeTriggered>(OnQualitativeTriggered);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.QualitativeTriggered>(OnQualitativeTriggered);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
        }

        private void Update()
        {
            if (_player == null || !_player.Stats.IsAlive) return;

            // 玉碎冷却
            if (!_jadeShieldReady && _activeEffects.Contains("玉碎"))
            {
                _jadeShieldTimer -= Time.deltaTime;
                if (_jadeShieldTimer <= 0)
                {
                    _jadeShieldReady = true;
                    Debug.Log("<color=cyan>🛡️ 玉碎效果已恢复！</color>");
                }
            }

            // 剑阵旋转 & 伤害
            if (_swordOrbitActive)
                UpdateSwordOrbit();

            // 嗜血状态
            if (_bloodlustActive)
                UpdateBloodlust();

            // 元素爆发
            if (_elementBurstActive)
                UpdateElementBurst();
        }

        // ==================== 质变效果注册 ====================

        private void OnQualitativeTriggered(GameEvents.QualitativeTriggered evt)
        {
            // 质变效果由 ItemInventory 的 ApplyQualitativeEffect 返回的 effectId 驱动
            // 这里不再处理，改为由 ActivateEffect 直接调用
        }

        /// <summary>
        /// 激活质变效果（由 ItemInventory 调用）
        /// </summary>
        public void ActivateEffect(string effectId)
        {
            if (_activeEffects.Contains(effectId)) return;
            _activeEffects.Add(effectId);

            switch (effectId)
            {
                case "玉碎":
                    _jadeShieldReady = true;
                    Debug.Log("<color=cyan>🛡️ 玉碎激活：受到致命伤害时免疫并击退敌人（CD 60秒）</color>");
                    break;

                case "焚天":
                    _fireHitCounter = 0;
                    Debug.Log("<color=orange>🔥 焚天激活：每5次攻击释放火焰冲击波</color>");
                    break;

                case "御风":
                    Debug.Log("<color=green>🌀 御风激活：闪避后留下风之残影自动攻击</color>");
                    break;

                case "剑阵":
                    ActivateSwordOrbit();
                    Debug.Log("<color=white>⚔️ 剑阵激活：飞剑环绕自动攻击靠近的敌人</color>");
                    break;

                case "涅槃":
                    _nirvanaReady = true;
                    Debug.Log("<color=yellow>💊 涅槃激活：死亡时消耗回灵丹原地复活</color>");
                    break;
            }
        }

        /// <summary>
        /// 停用质变效果（灵物被移除时调用）
        /// </summary>
        public void DeactivateEffect(string effectId)
        {
            if (!_activeEffects.Contains(effectId)) return;
            _activeEffects.Remove(effectId);

            switch (effectId)
            {
                case "玉碎":
                    _jadeShieldReady = false;
                    _jadeShieldTimer = 0f;
                    break;
                case "焚天":
                    _fireHitCounter = 0;
                    break;
                case "御风":
                    ClearPhantoms();
                    break;
                case "剑阵":
                    DeactivateSwordOrbit();
                    break;
                case "涅槃":
                    _nirvanaReady = false;
                    break;
            }

            Debug.Log($"<color=gray>质变机制失效：{effectId}</color>");
        }

        // ==================== 玉碎：致命伤害免疫 ====================

        private void OnPlayerDamaged(GameEvents.PlayerDamaged evt)
        {
            // 玉碎检测：如果玩家血量降到0以下，且玉碎就绪
            if (_activeEffects.Contains("玉碎") && _jadeShieldReady && evt.CurrentHp <= 0)
            {
                TriggerJadeShield();
            }
        }

        /// <summary>
        /// 尝试触发玉碎效果（由 PlayerController.OnDamage 调用）
        /// 返回 true 表示免疫了致命伤害
        /// </summary>
        public bool TryJadeShield()
        {
            if (!_activeEffects.Contains("玉碎") || !_jadeShieldReady) return false;
            TriggerJadeShield();
            return true;
        }

        private void TriggerJadeShield()
        {
            _jadeShieldReady = false;
            _jadeShieldTimer = _jadeShieldCooldown;

            // 恢复到10%血量
            _player.Stats.currentHp = _player.Stats.maxHp * 0.1f;

            // 击退周围敌人
            var hits = Physics.OverlapSphere(transform.position, 5f);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<IDamageable>();
                if (enemy != null && hit.gameObject != gameObject)
                {
                    Vector3 knockDir = (hit.transform.position - transform.position).normalized;
                    var cc = hit.GetComponent<CharacterController>();
                    if (cc != null)
                        cc.Move(knockDir * 3f);
                }
            }

            // 短暂无敌
            StartCoroutine(JadeShieldInvincible());

            // 视觉效果：白色闪光球
            CreateJadeShieldVFX();

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _player.Stats.currentHp,
                MaxHp = _player.Stats.maxHp
            });

            // 飘字：玉碎免疫
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2f,
                Damage = 0,
                SpecialTag = "玉碎"
            });

            Debug.Log("<color=cyan>💎 玉碎触发！免疫致命伤害，击退周围敌人！</color>");
        }

        private IEnumerator JadeShieldInvincible()
        {
            // 通过事件通知短暂无敌（1.5秒）
            // 这里简单用闪烁表示
            var renderers = GetComponentsInChildren<Renderer>();
            float timer = 1.5f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float alpha = Mathf.PingPong(Time.time * 8f, 1f) > 0.5f ? 1f : 0.3f;
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        var c = r.material.color;
                        c.a = alpha;
                        r.material.color = c;
                    }
                }
                yield return null;
            }
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    var c = r.material.color;
                    c.a = 1f;
                    r.material.color = c;
                }
            }
        }

        private void CreateJadeShieldVFX()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "[VFX] 玉碎冲击波";
            sphere.transform.position = transform.position + Vector3.up;

            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.8f, 1f, 0.9f, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 1f, 0.8f));
                rend.material = mat;
            }

            StartCoroutine(ExpandAndFade(sphere, 5f, 0.5f));
        }

        // ==================== 焚天：每N次攻击释放火焰冲击波 ====================

        /// <summary>
        /// 记录一次攻击动作（由 PlayerCombat 在每次攻击时调用，不需要命中）
        /// </summary>
        public void OnPlayerAttackHit()
        {
            if (!_activeEffects.Contains("焚天")) return;

            _fireHitCounter++;
            if (_fireHitCounter >= FIRE_BURST_EVERY)
            {
                _fireHitCounter = 0;
                TriggerFireBurst();
            }
        }

        private void TriggerFireBurst()
        {
            Vector3 center = transform.position;

            // 对周围敌人造成AOE伤害
            var hits = Physics.OverlapSphere(center, _fireBurstRadius);
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && hit.gameObject != gameObject)
                {
                    var (damage, isCrit) = _player.Stats.BuildSummonDamage(0.5f, _fireBurstDamage);
                    damageable.OnDamage(damage, hit.transform.position, gameObject);

                    // 飘字：焚天伤害（继承暴击）
                    GameEvents.Publish(new GameEvents.DamageNumberRequested
                    {
                        WorldPosition = hit.transform.position + Vector3.up * 1.5f,
                        Damage = damage,
                        IsCrit = isCrit,
                        SpecialTag = "焚天"
                    });

                    // 附加灼烧
                    var burn = hit.GetComponent<BurnEffect>();
                    if (burn == null)
                        burn = hit.gameObject.AddComponent<BurnEffect>();
                    burn.Apply(_fireBurstDamage * 0.3f, 4f);
                }
            }

            // 视觉效果：扩散的火焰环
            CreateFireBurstVFX(center);

            Debug.Log("<color=orange>🔥 焚天冲击波！对周围敌人造成AOE灼烧伤害</color>");
        }

        private void CreateFireBurstVFX(Vector3 center)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "[VFX] 焚天冲击波";
            ring.transform.position = center + Vector3.up * 0.1f;
            ring.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);

            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.4f, 0.1f, 0.7f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0f) * 2f);
                rend.material = mat;
            }

            StartCoroutine(ExpandAndFade(ring, _fireBurstRadius * 2f, 0.6f));
        }

        // ==================== 御风：闪避后留下残影 ====================

        /// <summary>
        /// 闪避时触发残影（由 PlayerController 调用）
        /// </summary>
        public void OnPlayerDash()
        {
            if (!_activeEffects.Contains("御风")) return;
            StartCoroutine(SpawnPhantom());
        }

        private IEnumerator SpawnPhantom()
        {
            Vector3 spawnPos = transform.position;

            // 创建残影
            var phantom = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            phantom.name = "[VFX] 风之残影";
            phantom.transform.position = spawnPos;
            phantom.transform.rotation = transform.rotation;
            phantom.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            _activePhantoms.Add(phantom);

            var col = phantom.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = phantom.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.9f, 0.7f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 0.5f));
                rend.material = mat;
            }

            // 残影存在期间自动攻击
            float timer = _phantomDuration;
            float attackTimer = 0f;
            while (timer > 0 && phantom != null)
            {
                timer -= Time.deltaTime;
                attackTimer -= Time.deltaTime;

                // 淡出
                float alpha = Mathf.Clamp01(timer / _phantomDuration) * 0.5f;
                if (rend != null)
                {
                    var c = rend.material.color;
                    c.a = alpha;
                    rend.material.color = c;
                }

                // 自动攻击最近的敌人
                if (attackTimer <= 0)
                {
                    attackTimer = _phantomAttackInterval;
                    PhantomAttack(phantom.transform.position);
                }

                yield return null;
            }

            if (phantom != null) Destroy(phantom);
            _activePhantoms.Remove(phantom);
        }

        private void ClearPhantoms()
        {
            foreach (var phantom in _activePhantoms)
            {
                if (phantom != null) Destroy(phantom);
            }
            _activePhantoms.Clear();
        }

        private void PhantomAttack(Vector3 position)
        {
            float range = 3f;
            var hits = Physics.OverlapSphere(position, range);
            float minDist = float.MaxValue;
            IDamageable closest = null;
            Vector3 closestPos = Vector3.zero;

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && hit.gameObject != gameObject)
                {
                    float dist = Vector3.Distance(position, hit.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = damageable;
                        closestPos = hit.transform.position;
                    }
                }
            }

            if (closest != null)
            {
                var (damage, isCrit) = _player.Stats.BuildSummonDamage(_phantomDamageRatio);
                closest.OnDamage(damage, closestPos, gameObject);

                // 飘字：御风残影伤害（继承暴击）
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = closestPos + Vector3.up * 1.5f,
                    Damage = damage,
                    IsCrit = isCrit,
                    SpecialTag = "御风"
                });
            }
        }

        // ==================== 剑阵：飞剑环绕 ====================

        private void ActivateSwordOrbit()
        {
            if (_swordOrbitActive) return;
            _swordOrbitActive = true;

            // 创建3把环绕飞剑
            for (int i = 0; i < 3; i++)
            {
                var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sword.name = $"[VFX] 飞剑_{i}";
                sword.transform.localScale = new Vector3(0.1f, 0.1f, 0.6f);

                var col = sword.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var swordRend = sword.GetComponent<Renderer>();
                if (swordRend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(0.7f, 0.8f, 1f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 1f) * 1.5f);
                    swordRend.material = mat;
                }

                _orbitSwords.Add(sword);
            }
        }

        private void DeactivateSwordOrbit()
        {
            _swordOrbitActive = false;
            foreach (var sword in _orbitSwords)
            {
                if (sword != null) Destroy(sword);
            }
            _orbitSwords.Clear();
        }

        private void UpdateSwordOrbit()
        {
            if (_orbitSwords.Count == 0) return;

            Vector3 center = transform.position + Vector3.up * 1f;
            float baseAngle = Time.time * _swordOrbitSpeed;

            for (int i = 0; i < _orbitSwords.Count; i++)
            {
                if (_orbitSwords[i] == null) continue;

                float angle = baseAngle + (360f / _orbitSwords.Count) * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * _swordOrbitRadius;

                _orbitSwords[i].transform.position = center + offset;
                _orbitSwords[i].transform.rotation = Quaternion.LookRotation(
                    new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad)));
            }

            // 定时对靠近的敌人造成伤害
            _swordOrbitHitTimer -= Time.deltaTime;
            if (_swordOrbitHitTimer <= 0)
            {
                _swordOrbitHitTimer = _swordOrbitHitInterval;
                SwordOrbitDamage();
            }
        }

        private void SwordOrbitDamage()
        {
            var hits = Physics.OverlapSphere(transform.position, _swordOrbitRadius + 0.5f);
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && hit.gameObject != gameObject)
                {
                    var (damage, isCrit) = _player.Stats.BuildSummonDamage(0.2f, _swordOrbitDamage);
                    damageable.OnDamage(damage, hit.transform.position, gameObject);

                    // 飘字：剑阵伤害（继承暴击）
                    GameEvents.Publish(new GameEvents.DamageNumberRequested
                    {
                        WorldPosition = hit.transform.position + Vector3.up * 1.5f,
                        Damage = damage,
                        IsCrit = isCrit,
                        SpecialTag = "剑阵"
                    });
                }
            }
        }

        // ==================== 涅槃：死亡复活 ====================

        /// <summary>
        /// 尝试触发涅槃复活（由 PlayerController.OnDeath 调用）
        /// 返回 true 表示成功复活
        /// </summary>
        public bool TryNirvana()
        {
            if (!_activeEffects.Contains("涅槃") || !_nirvanaReady) return false;

            _nirvanaReady = false;

            // 消耗所有回灵丹
            var inventory = _player.Inventory;
            var allItems = inventory.GetAllItems();
            foreach (var (item, count) in allItems)
            {
                if (item.category == ItemCategory.Pill)
                {
                    inventory.RemoveItem(item, count);
                    break;
                }
            }

            // 满血复活
            _player.Stats.currentHp = _player.Stats.maxHp;

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _player.Stats.currentHp,
                MaxHp = _player.Stats.maxHp
            });

            // 复活无敌
            StartCoroutine(NirvanaInvincible());

            // 视觉效果
            CreateNirvanaVFX();

            // 飘字：涅槃复活
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2.5f,
                Damage = 0,
                SpecialTag = "涅槃"
            });

            Debug.Log("<color=yellow>🔥 涅槃触发！消耗回灵丹原地复活，3秒无敌！</color>");
            return true;
        }

        private IEnumerator NirvanaInvincible()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            float timer = _nirvanaInvincibleDuration;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float glow = Mathf.PingPong(Time.time * 6f, 1f);
                foreach (var r in renderers)
                {
                    if (r != null && r.material.HasProperty("_EmissionColor"))
                    {
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor",
                            new Color(1f, 0.8f, 0.2f) * glow * 2f);
                    }
                }
                yield return null;
            }

            foreach (var r in renderers)
            {
                if (r != null && r.material.HasProperty("_EmissionColor"))
                    r.material.SetColor("_EmissionColor", Color.black);
            }
        }

        private void CreateNirvanaVFX()
        {
            // 金色光柱
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "[VFX] 涅槃光柱";
            pillar.transform.position = transform.position + Vector3.up * 5f;
            pillar.transform.localScale = new Vector3(1.5f, 5f, 1.5f);

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
                mat.color = new Color(1f, 0.85f, 0.2f, 0.4f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0f) * 3f);
                rend.material = mat;
            }

            StartCoroutine(ExpandAndFade(pillar, 3f, 1.5f));
        }

        // ==================== 协同效果：嗜血状态 ====================

        /// <summary>
        /// 激活嗜血状态（击杀后触发）
        /// </summary>
        public void ActivateBloodlust()
        {
            _bloodlustActive = true;
            _bloodlustTimer = _bloodlustDuration;
            _player.Stats.attackSpeed *= 2f;
            Debug.Log("<color=red>🩸 嗜血状态激活！攻速翻倍，但持续掉血！</color>");
        }

        /// <summary>标记嗜血协同是否已激活（由SynergySystem设置）</summary>
        public bool BloodlustSynergyActive { get; set; }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            // 嗜血协同：击杀后进入嗜血状态
            if (BloodlustSynergyActive && !_bloodlustActive)
            {
                ActivateBloodlust();
            }
        }

        private float _bloodlustDmgPopupTimer;

        private void UpdateBloodlust()
        {
            _bloodlustTimer -= Time.deltaTime;

            // 持续掉血
            float drain = _bloodlustHpDrain * Time.deltaTime;
            _player.Stats.currentHp -= drain;
            _player.Stats.currentHp = Mathf.Max(1f, _player.Stats.currentHp); // 不会因嗜血死亡

            // 嗜血掉血飘字（每1秒显示一次）
            _bloodlustDmgPopupTimer -= Time.deltaTime;
            if (_bloodlustDmgPopupTimer <= 0)
            {
                _bloodlustDmgPopupTimer = 1f;
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 2f + Vector3.right * Random.Range(-0.5f, 0.5f),
                    Damage = _bloodlustHpDrain,
                    IsPlayerDamage = true,
                    SpecialTag = "嗜血"
                });
            }

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _player.Stats.currentHp,
                MaxHp = _player.Stats.maxHp
            });

            if (_bloodlustTimer <= 0)
            {
                _bloodlustActive = false;
                _player.Stats.attackSpeed /= 2f;
                Debug.Log("<color=gray>嗜血状态结束</color>");
            }
        }

        // ==================== 协同效果：冲刺火墙 ====================

        /// <summary>标记火墙协同是否已激活</summary>
        public bool FireTrailSynergyActive { get; set; }

        /// <summary>
        /// 冲刺时留下火墙（由 PlayerController 调用）
        /// </summary>
        public void OnPlayerDashForFireTrail(Vector3 startPos, Vector3 endPos)
        {
            if (!FireTrailSynergyActive) return;
            StartCoroutine(SpawnFireTrail(startPos, endPos));
        }

        private IEnumerator SpawnFireTrail(Vector3 start, Vector3 end)
        {
            Vector3 dir = (end - start);
            float length = dir.magnitude;
            if (length < 0.5f) yield break;

            dir.Normalize();
            int segments = Mathf.CeilToInt(length / 0.8f);

            var trailObjects = new List<GameObject>();

            for (int i = 0; i < segments; i++)
            {
                Vector3 pos = start + dir * (i * 0.8f) + Vector3.up * 0.1f;
                var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "[VFX] 火墙";
                flame.transform.position = pos;
                flame.transform.localScale = new Vector3(0.8f, 1.5f, 0.3f);
                flame.transform.rotation = Quaternion.LookRotation(dir);

                var col = flame.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = flame.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.SetFloat("_Surface", 1);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                    mat.color = new Color(1f, 0.3f, 0f, 0.5f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0f) * 2f);
                    rend.material = mat;
                }

                trailObjects.Add(flame);
            }

            // 火墙持续伤害
            float timer = _fireTrailDuration;
            float dmgTimer = 0f;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                dmgTimer -= Time.deltaTime;

                if (dmgTimer <= 0)
                {
                    dmgTimer = 0.5f;
                    // 对火墙路径上的敌人造成伤害
                    for (int i = 0; i < segments; i++)
                    {
                        Vector3 pos = start + dir * (i * 0.8f);
                        var hits = Physics.OverlapSphere(pos, 1f);
                        foreach (var hit in hits)
                        {
                            var damageable = hit.GetComponent<IDamageable>();
                            if (damageable != null && hit.gameObject != gameObject)
                            {
                                damageable.OnDamage(_fireTrailDamage, hit.transform.position, gameObject);

                                // 飘字：火墙伤害
                                GameEvents.Publish(new GameEvents.DamageNumberRequested
                                {
                                    WorldPosition = hit.transform.position + Vector3.up * 1.5f,
                                    Damage = _fireTrailDamage,
                                    SpecialTag = "火墙"
                                });
                            }
                        }
                    }
                }

                // 淡出
                float alpha = Mathf.Clamp01(timer / _fireTrailDuration) * 0.5f;
                foreach (var obj in trailObjects)
                {
                    if (obj != null)
                    {
                        var r = obj.GetComponent<Renderer>();
                        if (r != null)
                        {
                            var c = r.material.color;
                            c.a = alpha;
                            r.material.color = c;
                        }
                    }
                }

                yield return null;
            }

            foreach (var obj in trailObjects)
            {
                if (obj != null) Destroy(obj);
            }
        }

        // ==================== 协同效果：元素爆发 ====================

        /// <summary>激活元素爆发</summary>
        public void ActivateElementBurst()
        {
            _elementBurstActive = true;
            _elementBurstTimer = _elementBurstInterval;
        }

        /// <summary>停用元素爆发</summary>
        public void DeactivateElementBurst()
        {
            _elementBurstActive = false;
        }

        private void UpdateElementBurst()
        {
            _elementBurstTimer -= Time.deltaTime;
            if (_elementBurstTimer <= 0)
            {
                _elementBurstTimer = _elementBurstInterval;
                TriggerRandomElementBurst();
            }
        }

        private void TriggerRandomElementBurst()
        {
            int element = Random.Range(0, 4);
            string elementName;
            Color elementColor;

            switch (element)
            {
                case 0:
                    elementName = "火";
                    elementColor = new Color(1f, 0.3f, 0f);
                    break;
                case 1:
                    elementName = "冰";
                    elementColor = new Color(0.3f, 0.7f, 1f);
                    break;
                case 2:
                    elementName = "风";
                    elementColor = new Color(0.3f, 1f, 0.5f);
                    break;
                default:
                    elementName = "雷";
                    elementColor = new Color(0.8f, 0.6f, 1f);
                    break;
            }

            // AOE伤害
            var hits = Physics.OverlapSphere(transform.position, _elementBurstRadius);
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && hit.gameObject != gameObject)
                {
                    var (damage, isCrit) = _player.Stats.BuildSummonDamage(0.4f, _elementBurstDamage);
                    damageable.OnDamage(damage, hit.transform.position, gameObject);

                    // 飘字：元素爆发伤害（继承暴击）
                    GameEvents.Publish(new GameEvents.DamageNumberRequested
                    {
                        WorldPosition = hit.transform.position + Vector3.up * 1.5f,
                        Damage = damage,
                        IsCrit = isCrit,
                        SpecialTag = "元素爆发"
                    });

                    // 冰元素附加冻结
                    if (element == 1)
                    {
                        var enemy = hit.GetComponent<EnemyBase>();
                        if (enemy != null)
                            enemy.Stats.moveSpeed *= 0.5f; // 减速50%
                    }
                }
            }

            // 视觉效果
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = $"[VFX] {elementName}元素爆发";
            ring.transform.position = transform.position + Vector3.up * 0.1f;

            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(elementColor.r, elementColor.g, elementColor.b, 0.6f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", elementColor * 2f);
                rend.material = mat;
            }

            StartCoroutine(ExpandAndFade(ring, _elementBurstRadius * 2f, 0.8f));

            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(elementColor)}>⚡ {elementName}元素爆发！</color>");
        }

        // ==================== 通用工具 ====================

        /// <summary>扩散并淡出的通用动画</summary>
        private IEnumerator ExpandAndFade(GameObject obj, float targetSize, float duration)
        {
            float timer = 0f;
            while (timer < duration && obj != null)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                float size = Mathf.Lerp(0.5f, targetSize, t);
                obj.transform.localScale = new Vector3(size, 0.05f, size);

                var rend = obj.GetComponent<Renderer>();
                if (rend != null)
                {
                    var c = rend.material.color;
                    c.a = Mathf.Lerp(0.7f, 0f, t);
                    rend.material.color = c;
                }

                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        /// <summary>清空所有效果（新一局开始时）</summary>
        public void Clear()
        {
            _activeEffects.Clear();
            _jadeShieldReady = true;
            _nirvanaReady = false;
            _fireHitCounter = 0;
            _bloodlustActive = false;
            BloodlustSynergyActive = false;
            FireTrailSynergyActive = false;
            ClearPhantoms();
            DeactivateSwordOrbit();
            DeactivateElementBurst();
        }
    }
}
