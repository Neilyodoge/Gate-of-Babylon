using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 天劫渡劫（v0.5 修仙独有战斗机制 #3）。
    ///
    /// 触发：境界 Boss 死后，渡劫台出生，玩家按 V 主动渡劫（不渡劫也行）。
    /// 流程：5 道雷劫连续降下 → 每道 1.5s telegraph → 半径 4m AOE 雷电伤害（35% MaxHp）
    /// 期间禁用普通闪避（修仙渡劫只能靠走位躲，不能"翻滚混过去"）。
    ///
    /// 成功（中 ≤ 1 次）→ 永久"破劫者" buff，整局 +20% 攻击 / +10% 减伤
    /// 失败（中 2~3 次）→ 强制半残撤离（HP 减半，下一场风险加倍）
    /// 重大失败（中 ≥ 4 次）→ 玩家死亡分支
    /// </summary>
    public class HeavenTribulation : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 15;
        public bool IsInteractionAvailable => _playerInRange && !_inProgress && !_completed;
        public bool IsRoutedActive { get; set; }

        private const int BoltCount = 5;
        private const float TelegraphDuration = 1.5f;
        private const float BoltRadius = 4f;
        private const float BoltGap = 0.5f;
        private const float DamagePercent = 0.35f;

        private bool _playerInRange;
        private bool _inProgress;
        private bool _completed;
        private int _hitCount;
        private NpcHeadCard _headCard;
        private System.Action _onComplete;

        public void Build(System.Action onComplete)
        {
            _onComplete = onComplete;

            // 视觉：黑色雷劫法坛
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "TribulationAltar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = new Vector3(0, 0.5f, 0);
            pillar.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            var pcol = pillar.GetComponent<Collider>();
            if (pcol != null) Destroy(pcol);
            var prend = pillar.GetComponent<Renderer>();
            if (prend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.1f, 0.1f, 0.18f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.6f, 1f) * 1.4f);
                prend.material = mat;
            }

            // 触发器
            var trig = new GameObject("TribulationTrigger");
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

            // 头顶卡片
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "渡劫台",
                icon = "⚡",
                roleSub = "5 道雷劫 · 走位躲避",
                hintText = "按 [V] 渡劫",
                themeColor = new Color(0.6f, 0.7f, 1f),
                yOffset = 3f,
                showLongRangeMarker = true
            });
        }

        private void Update()
        {
            if (_completed) return;
            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && _playerInRange && !_inProgress;
                _headCard.SetHintVisible(wantHint);
            }

            if (!IsRoutedActive || _inProgress) return;
            if (!_playerInRange) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame)
            {
                StartCoroutine(RunTribulation());
            }
        }

        private IEnumerator RunTribulation()
        {
            _inProgress = true;
            _hitCount = 0;
            if (_headCard != null) _headCard.SetHintVisible(false);

            // 全屏遮罩 / 提示由 RunHUD 监听事件显示
            GameEvents.Publish(new GameEvents.TribulationStarted { BoltCount = BoltCount });

            // 禁用闪避
            PlayerController.Instance?.SetDashEnabled(false);

            for (int i = 0; i < BoltCount; i++)
            {
                yield return StartCoroutine(SpawnBolt(i + 1));
                yield return new WaitForSeconds(BoltGap);
            }

            // 恢复闪避
            PlayerController.Instance?.SetDashEnabled(true);

            // 评估结果
            TribulationOutcome outcome;
            if (_hitCount <= 1)
            {
                outcome = TribulationOutcome.Success;
                ApplyBreakthroughBuff();
            }
            else if (_hitCount <= 3)
            {
                outcome = TribulationOutcome.PartialFail;
                // 半残撤离：HP 减半
                if (PlayerController.Instance != null)
                {
                    var stats = PlayerController.Instance.Stats;
                    stats.currentHp = Mathf.Max(1f, stats.currentHp * 0.5f);
                    GameEvents.Publish(new GameEvents.HealthChanged
                    {
                        CurrentHp = stats.currentHp, MaxHp = stats.maxHp
                    });
                }
            }
            else
            {
                outcome = TribulationOutcome.Catastrophic;
                // 直接杀死玩家
                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.OnDamage(99999f, transform.position, gameObject);
                }
            }

            GameEvents.Publish(new GameEvents.TribulationFinished
            {
                Outcome = outcome,
                HitCount = _hitCount
            });

            _completed = true;
            _inProgress = false;
            _onComplete?.Invoke();
        }

        private IEnumerator SpawnBolt(int boltIndex)
        {
            if (PlayerController.Instance == null) yield break;
            Vector3 boltPos = PlayerController.Instance.transform.position;

            // Telegraph：地面圆圈
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = $"BoltTelegraph_{boltIndex}";
            indicator.transform.position = boltPos + Vector3.up * 0.05f;
            indicator.transform.localScale = new Vector3(BoltRadius * 2f, 0.05f, BoltRadius * 2f);
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = indicator.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.6f, 0.65f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.6f, 0.65f, 1f) * 1.2f);
                rend.material = mat;
            }

            GameEvents.Publish(new GameEvents.TribulationBoltTelegraph { BoltIndex = boltIndex });

            // 1.5s telegraph，期间颜色变深变红
            float t = 0f;
            while (t < TelegraphDuration)
            {
                t += Time.deltaTime;
                if (rend != null)
                {
                    float k = t / TelegraphDuration;
                    rend.material.color = new Color(0.6f + k * 0.4f, 0.65f - k * 0.55f, 1f - k * 0.5f, 0.3f + k * 0.4f);
                }
                yield return null;
            }

            // 雷击！
            FxFactory.SpawnElementBurst(boltPos, ElementTag.Lightning, BoltRadius * 0.7f);

            // 判定：玩家是否在范围内
            float dist = Vector3.Distance(PlayerController.Instance.transform.position, boltPos);
            if (dist <= BoltRadius)
            {
                _hitCount++;
                float damage = PlayerController.Instance.Stats.maxHp * DamagePercent;
                PlayerController.Instance.OnDamage(damage, boltPos, gameObject);
                Debug.Log($"<color=#ff6666>[渡劫] 第 {boltIndex} 道雷劫命中！（中弹 {_hitCount}/{BoltCount}）</color>");
            }
            else
            {
                Debug.Log($"<color=#88ccff>[渡劫] 第 {boltIndex} 道雷劫躲过</color>");
            }

            // 闪烁后销毁 indicator
            if (indicator != null)
            {
                indicator.transform.localScale *= 1.2f;
                Destroy(indicator, 0.3f);
            }
        }

        private void ApplyBreakthroughBuff()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var status = p.GetComponent<StatusEffectController>();
            if (status == null) return;

            status.Apply(new StatusEffect
            {
                id = "Tribulation_Breakthrough",
                isBuff = true,
                elementTag = ElementTag.Lightning,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = new System.Collections.Generic.List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, 0.20f),
                    StatModifier.Flat(StatType.DamageReduction, 0.10f)
                },
                displayName = "破劫者",
                description = "渡劫成功 · 攻击力 +20% · 减伤 +10%（本局永久）",
                uiColor = new Color(0.6f, 0.7f, 1f)
            });
            Debug.Log("<color=#88ffff>[渡劫] 破劫者 · 整局攻击 +20% 减伤 +10%</color>");
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
        }
    }

    public enum TribulationOutcome
    {
        Success,        // 中 ≤ 1
        PartialFail,    // 中 2~3 → 半残撤离
        Catastrophic    // 中 ≥ 4 → 死亡
    }
}
