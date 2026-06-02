using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 心魔值（v0.5.4 · GDD 6.8.4 ②）—— 乱入心魔的累积条。
    ///
    /// 【v0.5.4 修正】心魔值不是"战斗表现/残血惩罚"，而是**抉择之债**：
    ///   · 作恶 / 滥杀 / 选邪道 → 因果债增加（KarmaDebt↑）→ 心魔值涨
    ///   · 心魔诱惑 / 背信 / 功利抉择 → 道心下降（Daoxin↓）→ 心魔值涨
    ///   · 坚守道心 / 向善（道心回升）→ 心魔值略降（修心抑魔）
    ///   数据来源：奇遇/机缘事件选项（<see cref="StoryEventService"/> → <see cref="PlayerStateHooks"/>），
    ///   或后续"选择对立派系装备/功法"等直接调用 <see cref="AddInnerDemon"/>。
    ///
    /// 满 100 且【正在打境界 Boss】（<see cref="EnemyBoss.AliveCount"/> &gt; 0）→ 心魔分身乱入，双线作战。
    ///   · 斩杀 → 心魔值清零；被反杀 → 走火入魔（死亡分支 = 身死道消）；新一局重置。
    /// </summary>
    public class InnerDemonMeter : MonoBehaviour
    {
        private static InnerDemonMeter _instance;
        public static InnerDemonMeter Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("InnerDemonMeter");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<InnerDemonMeter>();
                }
                return _instance;
            }
        }
        public static bool HasInstance => _instance != null;

        public const float Max = 100f;
        public float Meter { get; private set; }
        public bool IntrusionActive { get; private set; }

        // 抉择 → 心魔值 的换算系数（可调）
        private const float KarmaToDemon = 6f;   // 因果债 +1 → 心魔值 +6
        private const float DaoxinToDemon = 1.2f; // 道心 -1 → 心魔值 +1.2；道心 +1 → 心魔值 -0.6

        private PlayerStateHooks _hooks;
        private bool _subscribed;
        private int _lastKarma;
        private int _lastDaoxin = 60;

        private void OnEnable()
        {
            // 订阅因果 / 道心变化（抉择驱动）
            _hooks = PlayerStateHooks.Instance;
            if (_hooks != null && !_subscribed)
            {
                _hooks.OnKarmaChanged += OnKarmaChanged;
                _hooks.OnDaoxinChanged += OnDaoxinChanged;
                _lastKarma = _hooks.KarmaDebt;
                _lastDaoxin = _hooks.Daoxin;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_hooks != null && _subscribed)
            {
                _hooks.OnKarmaChanged -= OnKarmaChanged;
                _hooks.OnDaoxinChanged -= OnDaoxinChanged;
                _subscribed = false;
            }
        }

        public void ResetMeter()
        {
            Meter = 0f;
            IntrusionActive = false;
            if (_hooks != null)
            {
                _lastKarma = _hooks.KarmaDebt;
                _lastDaoxin = _hooks.Daoxin;
            }
        }

        // ========== 抉择驱动累积 ==========

        /// <summary>因果债变化：作恶（债增加）→ 心魔值涨。</summary>
        private void OnKarmaChanged(int newKarma)
        {
            int delta = newKarma - _lastKarma;
            _lastKarma = newKarma;
            if (delta > 0) AddInnerDemon(delta * KarmaToDemon, "因果业债");
        }

        /// <summary>道心变化：道心下降 → 心魔值涨；道心回升 → 略降（修心抑魔）。</summary>
        private void OnDaoxinChanged(int newDaoxin)
        {
            int delta = newDaoxin - _lastDaoxin;
            _lastDaoxin = newDaoxin;
            if (delta < 0) AddInnerDemon(-delta * DaoxinToDemon, "道心动摇");
            else if (delta > 0) AddInnerDemon(-delta * DaoxinToDemon * 0.5f, "修心抑魔");
        }

        /// <summary>直接增减心魔值（抉择 / 邪道装备功法等通用入口；负数为压制）。</summary>
        public void AddInnerDemon(float amount, string reason)
        {
            if (IntrusionActive) return;
            // v0.5.5：心魔滋生异象 → 正向积累加速（压制 / 负向不放大）
            if (amount > 0f && RealmAnomalySystem.HasInstance)
                amount *= RealmAnomalySystem.Instance.InnerDemonRateMul;
            Meter = Mathf.Clamp(Meter + amount, 0f, Max);
            if (!Mathf.Approximately(amount, 0f))
                Debug.Log($"<color=#ff8899>[心魔] {(amount >= 0 ? "+" : "")}{amount:F0}（{reason}）→ {Mathf.RoundToInt(Meter)}/100</color>");
        }

        /// <summary>调试用：直接加心魔值。</summary>
        public void DebugAddMeter(float amount) => AddInnerDemon(amount, "调试");

        // ========== 乱入触发（满值 + 正在打 Boss）==========

        private void Update()
        {
            if (IntrusionActive) return;
            if (Meter >= Max && EnemyBoss.AliveCount > 0 && PlayerController.Instance != null)
                TriggerIntrusion();
        }

        private void TriggerIntrusion()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            IntrusionActive = true;

            // 心魔从玩家背后浮现（打你个措手不及）
            Vector3 pos = p.transform.position - p.transform.forward * 5f;
            var mirror = InnerDemonMirror.Spawn(pos, (InnerDemonCatalyst)null);
            if (mirror != null) mirror.SetDefeatCallback(OnIntrusionDefeated);
            // 乱入是秘境战斗，保留掉落（不调用 SetSuppressRewards）

            GameEvents.Publish(new GameEvents.InnerDemonStarted
            {
                RealmLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0
            });
            CameraShake.TriggerBig();
            Debug.Log("<color=#ff4455>★★ 心魔乱入！业债深重，心魔于 Boss 战中现身 ★★</color>");
        }

        private void OnIntrusionDefeated()
        {
            Meter = 0f;
            IntrusionActive = false;
            GameEvents.Publish(new GameEvents.InnerDemonFinished { Defeated = true });
            Debug.Log("<color=#ff8866>[心魔] 乱入心魔被斩，心魔值清零</color>");
        }
    }
}
