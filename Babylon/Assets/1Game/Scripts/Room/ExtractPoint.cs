using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 出梦点 / 撤离点（v0.5 搜打撤核心）。
    ///
    /// 每境界结束（每层 Boss 死后）由 GameManager 在战斗房中央 spawn 一个。
    /// 玩家走近 → 按 F → 5s 蓄力 → 撤离成功 → 回 VillageHub + 提交洞府素材。
    ///
    /// 蓄力期间被敌人攻击会中断（魂魄被打散，需重新按 F）。
    /// 玩家可以选择不撤离 → 走到旁边的下一境界传送门继续闯（更危险，更多奖励）。
    /// </summary>
    public class ExtractPoint : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 10;
        public bool IsInteractionAvailable => _playerInRange && !_extracting && !_completed;
        public bool IsRoutedActive { get; set; }

        private const float ExtractDuration = 5f;
        private const float TriggerRadius = 2.5f;

        private bool _playerInRange;
        private bool _extracting;
        private bool _completed;
        private float _extractTimer;
        private Action _onExtractSuccess;
        private NpcHeadCard _headCard;

        // 视觉：脚下进度条
        private GameObject _progressBarGo;
        private LineRenderer _progressLR;

        public void Build(Action onExtractSuccess)
        {
            _onExtractSuccess = onExtractSuccess;

            // 视觉：古镜 / 道门（用悬浮发光的环形 + 中央光柱表达）
            BuildVisuals();

            // 触发器
            var trig = new GameObject("ExtractTrigger");
            trig.transform.SetParent(transform, false);
            trig.transform.localPosition = Vector3.zero;
            var sc = trig.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = TriggerRadius;
            var rb = trig.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var bridge = trig.AddComponent<TriggerBridge>();
            bridge.OnEnter = OnPlayerEnter;
            bridge.OnExit = OnPlayerExit;

            // 头顶卡片
            _headCard = NpcHeadCard.Attach(transform, new NpcHeadCard.Config
            {
                displayName = "出梦点",
                icon = "✦",
                roleSub = "撤离回洞府",
                hintText = "按 [F] 蓄力 5s · 撤离",
                themeColor = new Color(0.8f, 0.85f, 1f),
                yOffset = 3f,
                showLongRangeMarker = true
            });
        }

        private void BuildVisuals()
        {
            // 主柱体（光柱）
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "ExtractPillar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = new Vector3(0, 1.2f, 0);
            pillar.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            var pcol = pillar.GetComponent<Collider>();
            if (pcol != null) Destroy(pcol);
            var prend = pillar.GetComponent<Renderer>();
            if (prend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.65f, 0.78f, 1f, 0.55f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.65f, 0.78f, 1f) * 2.2f);
                prend.material = mat;
            }

            // 进度条用 LineRenderer 在地面画一个圆环
            _progressBarGo = new GameObject("ExtractProgress");
            _progressBarGo.transform.SetParent(transform, false);
            _progressBarGo.transform.localPosition = new Vector3(0, 0.06f, 0);
            _progressLR = _progressBarGo.AddComponent<LineRenderer>();
            _progressLR.useWorldSpace = false;
            _progressLR.loop = false;
            _progressLR.positionCount = 0;
            _progressLR.widthMultiplier = 0.18f;
            _progressLR.material = new Material(Shader.Find("Sprites/Default"));
            _progressLR.startColor = new Color(0.65f, 0.78f, 1f, 0.95f);
            _progressLR.endColor = new Color(0.95f, 0.85f, 0.4f, 0.95f);
            _progressBarGo.SetActive(false);
        }

        private void Update()
        {
            if (_completed) return;

            // 提示开关
            if (_headCard != null)
            {
                bool wantHint = IsRoutedActive && _playerInRange && !_extracting;
                _headCard.SetHintVisible(wantHint);
            }

            // 蓄力中
            if (_extracting)
            {
                _extractTimer += Time.deltaTime;
                UpdateProgressVisual(_extractTimer / ExtractDuration);

                // 玩家走出范围 → 中断
                if (!_playerInRange)
                {
                    InterruptExtract("Moved");
                    return;
                }

                if (_extractTimer >= ExtractDuration)
                {
                    CompleteExtract();
                }
                return;
            }

            // 待机：检测玩家按 F 触发撤离
            if (_playerInRange && IsRoutedActive)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.fKey.wasPressedThisFrame)
                {
                    StartExtract();
                }
            }
        }

        private void StartExtract()
        {
            _extracting = true;
            _extractTimer = 0f;
            _progressBarGo?.SetActive(true);
            UpdateProgressVisual(0f);
            if (_headCard != null) _headCard.SetHintVisible(false);

            GameEvents.Publish(new GameEvents.ExtractRequested { ExtractPoint = gameObject });
            Debug.Log("<color=#a8c8ff>[ExtractPoint] 撤离蓄力开始，5s 后回洞府</color>");
        }

        public void InterruptExtract(string reason)
        {
            if (!_extracting || _completed) return;
            _extracting = false;
            _extractTimer = 0f;
            _progressBarGo?.SetActive(false);

            GameEvents.Publish(new GameEvents.ExtractInterrupted { Reason = reason });
            Debug.Log($"<color=#ffa080>[ExtractPoint] 撤离被中断：{reason}</color>");
        }

        private void CompleteExtract()
        {
            if (_completed) return;
            _completed = true;
            _extracting = false;
            _progressBarGo?.SetActive(false);

            int committedCount = CaveInventory.Instance.TotalPendingCount;
            // 实际提交在 GameManager / 回村流程中调，避免重复触发
            GameEvents.Publish(new GameEvents.ExtractSuccess
            {
                CaveMaterialsCommitted = committedCount,
                RealmReachedIndex = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0
            });

            Debug.Log($"<color=#88ff88>[ExtractPoint] 撤离成功！本局 {committedCount} 件洞府素材准备带回</color>");
            _onExtractSuccess?.Invoke();
        }

        private void UpdateProgressVisual(float progress)
        {
            if (_progressLR == null) return;
            progress = Mathf.Clamp01(progress);
            int segments = 48;
            float endAng = progress * Mathf.PI * 2f;
            int posCount = Mathf.Max(2, Mathf.CeilToInt(segments * progress));
            _progressLR.positionCount = posCount;
            for (int i = 0; i < posCount; i++)
            {
                float t = i / (float)(posCount - 1);
                float ang = t * endAng - Mathf.PI / 2f;
                _progressLR.SetPosition(i, new Vector3(Mathf.Cos(ang) * 1.4f, 0f, Mathf.Sin(ang) * 1.4f));
            }
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
            if (_extracting) InterruptExtract("Moved");
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }

        /// <summary>由其他系统调用：玩家在蓄力期间被攻击 → 中断撤离</summary>
        public static void NotifyPlayerDamaged()
        {
            var allPoints = FindObjectsOfType<ExtractPoint>();
            foreach (var p in allPoints)
            {
                if (p._extracting) p.InterruptExtract("Damaged");
            }
        }
    }
}
