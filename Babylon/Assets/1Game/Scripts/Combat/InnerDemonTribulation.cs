using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 心魔劫（v0.5 修仙独有战斗机制 #4，Week 4 收官）。
    ///
    /// 触发：化神期 / 大乘期 Boss 被击杀后，房间内同时出现"心魔台"（除渡劫台/出梦点外的第四个选择）。
    /// 玩家按 B 主动渡心魔劫 —— 召唤【镜像玩家】（克隆当前 stats、化身色，强度 0.8x），
    /// 击败它给整局"破障者" buff（+15% 攻击 +15% 减伤）；被它击败按常规死亡分支处理。
    ///
    /// 与天劫渡劫的差异：天劫是"走位躲雷"，心魔劫是"和自己的镜像对打"，
    /// 不禁用闪避，但镜像跟玩家共用近似 AI 模式（追踪 + 蓄力冲斩 + 镜像技能影像），形成对照式战斗体验。
    /// </summary>
    public class InnerDemonCatalyst : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 14;
        public bool IsInteractionAvailable => _playerInRange && !_inProgress && !_completed;
        public bool IsRoutedActive { get; set; }

        private bool _playerInRange;
        private bool _inProgress;
        private bool _completed;
        private NpcHeadCard _headCard;
        private GameObject _altarBody;
        private InnerDemonMirror _mirror;

        public void Build()
        {
            Color bloodRed = new Color(1f, 0.15f, 0.2f);
            Color deepCrim = new Color(0.55f, 0.05f, 0.12f);

            // —— 地面五角星血咒（持久 · 缓慢反转 · alpha 脉动）——
            CaveVfx.SpawnPentagramRune(transform, Vector3.zero, 2.2f, bloodRed, 0.08f);

            // —— 主体：黑紫色六角法坛（替代原 cylinder）——
            _altarBody = BuildAltarHex(deepCrim);

            // —— 顶部漂浮血球（自转 + 上下浮动 + emission 呼吸）——
            CaveVfx.SpawnHoveringObject(transform, new Vector3(0, 1.6f, 0),
                PrimitiveType.Sphere, Vector3.one * 0.5f,
                new Color(0.4f, 0.05f, 0.1f), bloodRed * 2.2f,
                hoverAmp: 0.18f, hoverFreq: 1.4f, spinSpeed: 45f);

            // —— 4 颗围绕法坛的血滴绕行（orbiting particles）——
            CaveVfx.SpawnOrbitingParticles(transform, new Vector3(0, 1.2f, 0),
                count: 5, orbitRadius: 1.3f, orbitHeight: 0f,
                particleSize: 0.16f, color: bloodRed,
                orbitSpeed: -55f, verticalBob: 0.2f);

            // —— 持续上升黑红色烟气（emitter）——
            CaveVfx.SpawnSmokeEmitter(transform, new Vector3(0, 0.5f, 0),
                color: new Color(0.6f, 0.1f, 0.15f),
                particleSize: 0.22f, spawnInterval: 0.35f,
                riseSpeed: 0.55f, lifetime: 1.6f, jitterRadius: 0.6f);

            // —— 触发器 ——
            var trig = new GameObject("InnerDemonTrigger");
            trig.transform.SetParent(transform, false);
            var sc = trig.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var bridge = trig.AddComponent<TriggerBridge>();
            bridge.OnEnter = OnPlayerEnter;
            bridge.OnExit = OnPlayerExit;

            // —— 头顶卡片 ——
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "心魔台",
                icon = "🩸",
                roleSub = "镜像自己 · 0.8x 强度",
                hintText = "按 [B] 渡心魔劫",
                themeColor = new Color(1f, 0.25f, 0.3f),
                yOffset = 3f,
                showLongRangeMarker = true
            });
        }

        private GameObject BuildAltarHex(Color emissionTint)
        {
            // 六角法坛 = 一个矮 Cylinder（侧面像六棱台）+ 顶面浮雕
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "InnerDemonAltar";
            altar.transform.SetParent(transform, false);
            altar.transform.localPosition = new Vector3(0, 0.4f, 0);
            altar.transform.localScale = new Vector3(1.5f, 0.4f, 1.5f);
            var col = altar.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = altar.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(new Color(0.06f, 0.04f, 0.10f),
                    emissionTint * 1.4f);
                rend.material = mat;
            }

            // 顶面浮雕（六角形）
            CaveVfx.SpawnGroundRune(transform, new Vector3(0, 0.81f, 0), 0.85f,
                new Color(1f, 0.4f, 0.5f), sides: 6, lineWidth: 0.05f, yLift: 0f);
            return altar;
        }

        private void Update()
        {
            if (_completed) return;
            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && _playerInRange && !_inProgress;
                _headCard.SetHintVisible(wantHint);
            }
            if (!IsRoutedActive || _inProgress || !_playerInRange) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) StartFight();
        }

        private void StartFight()
        {
            if (PlayerController.Instance == null) return;
            _inProgress = true;
            if (_headCard != null) _headCard.SetHintVisible(false);

            // 镜像 spawn 在玩家面前 5m
            var p = PlayerController.Instance.transform;
            Vector3 spawnPos = p.position + p.forward * 5f;
            _mirror = InnerDemonMirror.Spawn(spawnPos, this);

            // 法坛"激活"视觉 —— 增强发光 + 玩家与镜像之间一道血色剑气连线 + 心魔台脚下爆环
            if (_altarBody != null)
            {
                var rend = _altarBody.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    rend.material.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.15f) * 3.2f);
                }
            }
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, 2.8f,
                new Color(1f, 0.18f, 0.25f, 1f), lifetime: 1.0f);
            FxFactory.SpawnAOERing(spawnPos + Vector3.up * 0.05f, 2.4f,
                new Color(1f, 0.2f, 0.3f, 1f), lifetime: 1.0f);
            Vector3 from = transform.position + Vector3.up * 1.6f;
            FxFactory.SpawnSliceLine(from, (spawnPos - from), Vector3.Distance(from, spawnPos),
                new Color(1f, 0.25f, 0.3f, 1f), lifetime: 0.6f);

            CameraShake.TriggerBig();

            GameEvents.Publish(new GameEvents.InnerDemonStarted
            {
                RealmLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0
            });

            // 订阅玩家死亡：万一被镜像打死，要立即结束 inProgress 让 catalyst 不再阻塞
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDiedDuringFight);

            Debug.Log("<color=#ff5566>★★ 心魔劫降临！镜像自己已现身 ★★</color>");
        }

        public void OnMirrorDefeated()
        {
            if (_completed) return;
            _completed = true;
            _inProgress = false;
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDiedDuringFight);
            ApplyBreakthroughBuff();
            GameEvents.Publish(new GameEvents.InnerDemonFinished { Defeated = true });
            Debug.Log("<color=#ff5566>[心魔劫] 镜像被斩，破障者 buff 已激活</color>");
        }

        private void OnPlayerDiedDuringFight(GameEvents.PlayerDied _)
        {
            _completed = true;
            _inProgress = false;
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDiedDuringFight);
            GameEvents.Publish(new GameEvents.InnerDemonFinished { Defeated = false });
            Debug.Log("<color=#ff5566>[心魔劫] 被镜像反杀（按 GameManager 死亡分支处理）</color>");
        }

        private void ApplyBreakthroughBuff()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var status = p.GetComponent<StatusEffectController>();
            if (status == null) return;

            status.Apply(new StatusEffect
            {
                id = "InnerDemon_Triumphant",
                isBuff = true,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = new System.Collections.Generic.List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, 0.15f),
                    StatModifier.Flat(StatType.DamageReduction, 0.15f)
                },
                displayName = "破障者",
                description = "斩杀心魔后顿悟 —— 攻击力 +15% · 减伤 +15%（本局永久）",
                uiColor = new Color(1f, 0.4f, 0.5f)
            });
        }

        private void OnPlayerEnter()
        {
            _playerInRange = true;
            InteractionRouter.Register(this);
        }

        private void OnPlayerExit()
        {
            _playerInRange = false;
            InteractionRouter.Unregister(this);
            if (_headCard != null) _headCard.SetHintVisible(false);
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDiedDuringFight);
        }
    }

    // ===================================================================
    //                       镜像玩家 · 敌人实体
    // ===================================================================

    /// <summary>
    /// 镜像玩家（心魔劫的对手）—— 克隆当前玩家的 maxHp / attackDamage / moveSpeed × 0.8，
    /// 用类似 Boss / Charger 的复合 AI：追踪 + 蓄力冲斩 + 周期范围斩击。
    ///
    /// 视觉：玩家化身的颜色 / 红黑剪影 / 头顶飘"心魔"标签。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class InnerDemonMirror : MonoBehaviour, IDamageable
    {
        private CombatStats stats;
        public CombatStats Stats => stats;

        private CharacterController _cc;
        private Transform _target;
        private InnerDemonCatalyst _catalyst;
        /// <summary>可选击败回调（渡劫战 TribulationTrial 复用镜像时设置；与 _catalyst 并行，二者皆可空）。</summary>
        private System.Action _onDefeated;
        public void SetDefeatCallback(System.Action cb) => _onDefeated = cb;

        /// <summary>抑制击杀奖励（渡劫战镜像不掉落 / 不给灵力碎片·悟性·历练值；它只是突破试炼）。</summary>
        private bool _suppressRewards;
        public void SetSuppressRewards(bool v) => _suppressRewards = v;
        private EnemyHealthBar _healthBar;
        private GameObject _nameTag;

        // AI 计时
        private float _chargeCooldown;
        private float _swipeCooldown;
        private MirrorState _state = MirrorState.Tracking;
        private float _stateTimer;
        private Vector3 _chargeDir;
        private GameObject _warning;

        private const float DetectRange = 25f;
        private const float MeleeRange = 2.2f;
        private const float SwipeRadius = 3.2f;
        private const float ChargePrepTime = 0.7f;
        private const float ChargeSpeed = 16f;
        private const float ChargeDuration = 0.55f;
        private const float ChargeIntervalBase = 4.5f;
        private const float SwipeIntervalBase = 2.4f;

        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _hitFlashTimer;

        // 残影 trail
        private float _afterimageTimer;
        internal Color _afterimageColor = new Color(1f, 0.3f, 0.4f, 0.5f);

        private enum MirrorState { Tracking, ChargePrep, Charging, Stunned, SwipeWindup, SwipeStrike }

        public static InnerDemonMirror Spawn(Vector3 position, InnerDemonCatalyst catalyst)
        {
            if (PlayerController.Instance == null) return null;

            var go = new GameObject("InnerDemonMirror");
            go.transform.position = position;
            go.tag = "Enemy";  // 让灵兽伙伴 / 通用 Damage 系统能识别
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex >= 0) go.layer = enemyLayerIndex;

            Color rootColor = GetCurrentRootColor();
            Color shroud = Color.Lerp(rootColor, new Color(0.04f, 0.02f, 0.06f), 0.6f);
            Color emission = rootColor * 0.55f + new Color(0.5f, 0.05f, 0.12f);

            // —— 身体（胶囊）——
            BuildMirrorPart(go.transform, "MirrorBody", PrimitiveType.Capsule,
                new Vector3(0, 1f, 0), new Vector3(1.05f, 1.05f, 1.05f),
                shroud, emission);

            // —— 头部（球体，比身体稍小，浮在顶部）——
            BuildMirrorPart(go.transform, "MirrorHead", PrimitiveType.Sphere,
                new Vector3(0, 1.95f, 0), Vector3.one * 0.55f,
                Color.Lerp(shroud, Color.black, 0.3f), emission * 0.7f);

            // —— 肩 / 手部小球（左右各一）——
            BuildMirrorPart(go.transform, "ShoulderL", PrimitiveType.Sphere,
                new Vector3(-0.42f, 1.55f, 0), Vector3.one * 0.32f, shroud, emission * 0.5f);
            BuildMirrorPart(go.transform, "ShoulderR", PrimitiveType.Sphere,
                new Vector3(0.42f, 1.55f, 0), Vector3.one * 0.32f, shroud, emission * 0.5f);

            // —— 右手剑（拉长的发光 Cube）——
            var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sword.name = "MirrorSword";
            sword.transform.SetParent(go.transform, false);
            sword.transform.localPosition = new Vector3(0.62f, 1.15f, 0.5f);
            sword.transform.localRotation = Quaternion.Euler(70f, 0, 15f);
            sword.transform.localScale = new Vector3(0.08f, 1.4f, 0.04f);
            var swCol = sword.GetComponent<Collider>();
            if (swCol != null) Destroy(swCol);
            var swRend = sword.GetComponent<Renderer>();
            if (swRend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    Color.Lerp(rootColor, Color.white, 0.5f),
                    new Color(1f, 0.4f, 0.45f) * 1.8f);
                swRend.material = mat;
                swRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // —— 4 颗围绕镜像旋转的"心魔气"小球（暗紫红轨道粒子）——
            CaveVfx.SpawnOrbitingParticles(go.transform, new Vector3(0, 1.1f, 0),
                count: 4, orbitRadius: 0.95f, orbitHeight: 0f,
                particleSize: 0.16f, color: new Color(1f, 0.2f, 0.25f),
                orbitSpeed: 120f, verticalBob: 0.18f);

            // —— 持续上升黑红烟（脚下散发）——
            CaveVfx.SpawnSmokeEmitter(go.transform, new Vector3(0, 0.05f, 0),
                color: new Color(0.4f, 0.05f, 0.08f),
                particleSize: 0.2f, spawnInterval: 0.18f,
                riseSpeed: 0.45f, lifetime: 1.2f, jitterRadius: 0.45f);

            // —— CharacterController ——
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.45f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            var mirror = go.AddComponent<InnerDemonMirror>();
            mirror.InitFromPlayer(PlayerController.Instance, catalyst);
            mirror._afterimageColor = new Color(rootColor.r, rootColor.g, rootColor.b, 0.55f);
            return mirror;
        }

        private static void BuildMirrorPart(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Color baseColor, Color emission)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(baseColor, emission);
                rend.material = mat;
            }
        }

        private static Color GetCurrentRootColor()
        {
            var p = PlayerController.Instance;
            if (p == null) return new Color(1f, 0.85f, 0.2f);
            var rc = p.GetComponent<SpiritRootController>();
            if (rc == null) return new Color(1f, 0.85f, 0.2f);
            return rc.CurrentRoot switch
            {
                SpiritRootType.Metal => new Color(1f, 0.85f, 0.2f),
                SpiritRootType.Wood  => new Color(0.4f, 0.95f, 0.4f),
                SpiritRootType.Water => new Color(0.3f, 0.7f, 1f),
                SpiritRootType.Fire  => new Color(1f, 0.4f, 0.1f),
                SpiritRootType.Earth => new Color(0.85f, 0.7f, 0.4f),
                _ => new Color(1f, 0.85f, 0.2f)
            };
        }

        private void InitFromPlayer(PlayerController player, InnerDemonCatalyst catalyst)
        {
            _catalyst = catalyst;
            var pStats = player.Stats;
            stats = new CombatStats
            {
                maxHp = pStats.maxHp * 1.2f,
                currentHp = pStats.maxHp * 1.2f,
                attackDamage = pStats.attackDamage * 0.8f,
                moveSpeed = pStats.moveSpeed * 0.95f,
                attackSpeed = Mathf.Max(0.5f, pStats.attackSpeed * 0.8f),
                damageReduction = 0.10f,
                critRate = pStats.critRate * 0.6f,
                critDamage = pStats.critDamage
            };
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].material != null ? _renderers[i].material.color : Color.white;

            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            _healthBar = EnemyHealthBar.Create(gameObject);
            _chargeCooldown = ChargeIntervalBase * 0.4f;
            _swipeCooldown = SwipeIntervalBase * 0.5f;

            BuildNameTag();
        }

        private void BuildNameTag()
        {
            var canvas = new GameObject("MirrorNameCanvas");
            canvas.transform.SetParent(transform, false);
            canvas.transform.localPosition = new Vector3(0, 2.4f, 0);
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(3.5f, 0.45f);
            canvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "✦ 心魔 · 镜像自己 ✦";
            text.fontSize = 22;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(1f, 0.45f, 0.55f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameTag = canvas;
        }

        private void Update()
        {
            if (!stats.IsAlive || _target == null) return;

            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0) RestoreColors();
            }

            // 名签朝相机
            if (_nameTag != null && Camera.main != null)
                _nameTag.transform.rotation = Quaternion.LookRotation(
                    _nameTag.transform.position - Camera.main.transform.position);

            float dist = Vector3.Distance(transform.position, _target.position);

            switch (_state)
            {
                case MirrorState.Tracking: UpdateTracking(dist); break;
                case MirrorState.ChargePrep: UpdateChargePrep(); break;
                case MirrorState.Charging: UpdateCharging(dist); break;
                case MirrorState.Stunned:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0) _state = MirrorState.Tracking;
                    break;
                case MirrorState.SwipeWindup: UpdateSwipeWindup(); break;
                case MirrorState.SwipeStrike: UpdateSwipeStrike(); break;
            }

            // 朝向
            if (_state != MirrorState.Charging)
            {
                Vector3 lookDir = _target.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        private void UpdateTracking(float dist)
        {
            _chargeCooldown -= Time.deltaTime;
            _swipeCooldown -= Time.deltaTime;

            // 近 → 斩击；远 → 冲锋；中距离 → 追
            if (dist <= MeleeRange + 0.6f && _swipeCooldown <= 0f)
            {
                _state = MirrorState.SwipeWindup;
                _stateTimer = 0.5f;
                CreateSwipeWarning();
                SetAllRenderersColor(new Color(1f, 0.45f, 0.45f));
                return;
            }
            if (dist <= DetectRange && _chargeCooldown <= 0f)
            {
                _state = MirrorState.ChargePrep;
                _stateTimer = ChargePrepTime;
                _chargeDir = (_target.position - transform.position).normalized;
                _chargeDir.y = 0;
                CreateChargeWarning();
                SetAllRenderersColor(new Color(1f, 0.4f, 0.3f));
                return;
            }
            // 追
            Vector3 dir = (_target.position - transform.position).normalized;
            dir.y = 0;
            Vector3 vel = dir * stats.moveSpeed;
            vel.y = -9.8f;
            _cc.Move(vel * Time.deltaTime);
        }

        private void UpdateChargePrep()
        {
            _stateTimer -= Time.deltaTime;
            _chargeDir = (_target.position - transform.position).normalized;
            _chargeDir.y = 0;
            UpdateChargeWarning();
            // 抖动
            transform.position += new Vector3(Mathf.Sin(Time.time * 30f) * 0.04f, 0,
                                              Mathf.Cos(Time.time * 30f) * 0.04f);
            if (_stateTimer <= 0f)
            {
                _state = MirrorState.Charging;
                _stateTimer = ChargeDuration;
                DestroyWarning();
            }
        }

        private void UpdateCharging(float dist)
        {
            _stateTimer -= Time.deltaTime;
            Vector3 vel = _chargeDir * ChargeSpeed;
            vel.y = -9.8f;
            _cc.Move(vel * Time.deltaTime);

            // 冲锋期间 每 0.07s 留一道残影
            _afterimageTimer -= Time.deltaTime;
            if (_afterimageTimer <= 0f)
            {
                _afterimageTimer = 0.07f;
                CaveVfx.SpawnAfterimage(transform.position, transform.rotation,
                    new Vector3(0.95f, 1.05f, 0.95f), _afterimageColor, lifetime: 0.35f);
            }

            if (dist < MeleeRange)
            {
                CameraShake.TriggerMedium();
                _target.GetComponent<IDamageable>()?.OnDamage(stats.attackDamage * 1.5f, transform.position, gameObject);
                _state = MirrorState.Stunned;
                _stateTimer = 0.6f;
                _chargeCooldown = ChargeIntervalBase;
                RestoreColors();
                return;
            }
            if (_stateTimer <= 0f)
            {
                _state = MirrorState.Stunned;
                _stateTimer = 0.6f;
                _chargeCooldown = ChargeIntervalBase;
                RestoreColors();
            }
        }

        private void UpdateSwipeWindup()
        {
            _stateTimer -= Time.deltaTime;
            UpdateSwipeWarning();
            if (_stateTimer <= 0f)
            {
                _state = MirrorState.SwipeStrike;
                _stateTimer = 0.18f;
                DestroyWarning();

                // 一道扇形剑气视觉（朝向 forward）+ 收尾爆环 + 镜头中震
                CameraShake.TriggerMedium();
                Vector3 swipeOrigin = transform.position + Vector3.up * 0.6f;
                FxFactory.SpawnSliceLine(swipeOrigin, transform.forward + transform.right * 0.4f,
                    SwipeRadius, new Color(1f, 0.4f, 0.5f, 1f), lifetime: 0.3f);
                FxFactory.SpawnSliceLine(swipeOrigin, transform.forward - transform.right * 0.4f,
                    SwipeRadius, new Color(1f, 0.4f, 0.5f, 1f), lifetime: 0.3f);
                FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, SwipeRadius,
                    new Color(1f, 0.3f, 0.4f, 1f), lifetime: 0.35f);

                // 命中判定（一次性）
                var p = _target;
                if (p != null)
                {
                    float d = Vector3.Distance(transform.position, p.position);
                    if (d <= SwipeRadius)
                    {
                        p.GetComponent<IDamageable>()?.OnDamage(stats.attackDamage, transform.position, gameObject);
                    }
                }
                _swipeCooldown = SwipeIntervalBase;
            }
        }

        private void UpdateSwipeStrike()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
            {
                _state = MirrorState.Tracking;
                RestoreColors();
            }
        }

        // ============================== 警告条 ==============================

        private void CreateChargeWarning()
        {
            DestroyWarning();
            _warning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _warning.name = "MirrorChargeWarning";
            var col = _warning.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = _warning.GetComponent<Renderer>();
            if (rend != null)
            {
                Color warnColor = new Color(1f, 0.18f, 0.22f, 0.42f);
                var mat = MaterialHelper.CreateLitTransparent(warnColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.25f) * 1.8f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void UpdateChargeWarning()
        {
            if (_warning == null) return;
            float length = ChargeSpeed * ChargeDuration;
            Vector3 center = transform.position + _chargeDir * (length / 2f) + Vector3.up * 0.1f;
            _warning.transform.position = center;
            _warning.transform.localScale = new Vector3(1.2f, 0.1f, length);
            _warning.transform.rotation = Quaternion.LookRotation(_chargeDir);
        }

        private void CreateSwipeWarning()
        {
            DestroyWarning();
            _warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _warning.name = "MirrorSwipeWarning";
            var col = _warning.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = _warning.GetComponent<Renderer>();
            if (rend != null)
            {
                Color warnColor = new Color(1f, 0.35f, 0.4f, 0.4f);
                var mat = MaterialHelper.CreateLitTransparent(warnColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.35f) * 1.6f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 同时附一道发光圆环作为外圈高亮（瞬时但持续 0.5s 与 windup 同步）
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, SwipeRadius,
                new Color(1f, 0.3f, 0.35f, 1f), lifetime: 0.5f);
        }

        private void UpdateSwipeWarning()
        {
            if (_warning == null) return;
            _warning.transform.position = transform.position + Vector3.up * 0.05f;
            _warning.transform.localScale = new Vector3(SwipeRadius * 2f, 0.05f, SwipeRadius * 2f);
        }

        private void DestroyWarning()
        {
            if (_warning != null) Destroy(_warning);
            _warning = null;
        }

        // ============================== IDamageable ==============================

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;
            float actual = stats.TakeDamage(damage);
            _hitFlashTimer = 0.1f;

            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = false,
                IsPlayerDamage = false,
                SpecialTag = "心魔"
            });
            if (_healthBar != null)
                _healthBar.UpdateHealth(stats.currentHp, stats.maxHp);

            // 受击闪烁改为暗红 — 强化"心魔"主题
            SetAllRenderersColor(new Color(1f, 0.55f, 0.6f));

            // 命中点冒一小簇血色 burst
            Vector3 burstPos = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1f;
            FxFactory.SpawnElementBurst(burstPos, ElementTag.Fire, 0.55f, 0.35f);

            if (!stats.IsAlive) OnDeath();
        }

        public void OnDeath()
        {
            gameObject.tag = "Untagged";
            DestroyWarning();
            if (_catalyst != null) _catalyst.OnMirrorDefeated();
            _onDefeated?.Invoke();

            // 渡劫战镜像（_suppressRewards）只是突破试炼 → 不发击杀奖励、不掉落
            if (!_suppressRewards)
            {
                GameEvents.Publish(new GameEvents.EnemyKilled
                {
                    Enemy = gameObject, Position = transform.position
                });

                // v0.5 Week 6：击败心魔必定掉一颗"道韵碎片"（藏经阁拼合上古秘籍专用素材）
                var sliver = Resources.Load<ItemData>("CaveMaterials/道韵碎片");
                if (sliver != null)
                {
                    ItemPickup.Spawn(sliver, transform.position + new Vector3(0.6f, 0f, 0f));
                }
            }

            if (HitStop.Instance != null) HitStop.Instance.TriggerKill();

            CameraShake.TriggerBig();

            // —— 死亡视觉：一道破障爆环 + 多颗朝外飞溅的"心魔碎片"——
            Vector3 origin = transform.position + Vector3.up * 1f;
            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.05f, 3.5f,
                new Color(1f, 0.25f, 0.3f, 1f), lifetime: 0.9f);
            FxFactory.SpawnElementBurst(origin, ElementTag.Fire, 1.6f, 0.7f);
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.5f, Mathf.Sin(a));
                FxFactory.SpawnSliceLine(origin, dir, 2.4f, new Color(1f, 0.3f, 0.35f, 1f), 0.5f);
            }

            StartCoroutine(DeathFade());
        }

        private IEnumerator DeathFade()
        {
            enabled = false;
            _cc.enabled = false;
            float t = 0.6f;
            Vector3 startScale = transform.localScale;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                float k = t / 0.6f;
                transform.localScale = startScale * k;
                yield return null;
            }
            Destroy(gameObject);
        }

        // ============================== 视觉辅助 ==============================

        private void SetAllRenderersColor(Color color)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null && r.material != null) r.material.color = color;
        }

        private void RestoreColors()
        {
            if (_renderers == null || _originalColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].material != null)
                    _renderers[i].material.color = _originalColors[i];
        }

        private void OnDestroy()
        {
            DestroyWarning();
        }
    }
}
