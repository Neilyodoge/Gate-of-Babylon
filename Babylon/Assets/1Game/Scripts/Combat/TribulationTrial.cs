using System.Collections;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 渡劫战（v0.5.4）—— 本体境界突破的专属 encounter（GDD 6.8.3）。
    ///
    /// 由闭关石室「冲击境界」触发。同场两件事：
    ///   ① 心魔（主对手）：复用 <see cref="InnerDemonMirror"/> 镜像 —— 击败它 = 渡劫成功
    ///   ② 天劫（环境威胁）：周期性落雷 AOE，逼玩家边打边走位
    ///
    /// 成色：按全程受创比例判定（无伤=完美 / 轻伤=上品 / 中伤=凡品 / 重伤=瑕品），
    /// 成色越高 → 该阶本体境界增益越强（见 9.1.8）。
    ///
    /// 结果：
    ///   - 斩杀心魔 → <see cref="CultivationSystem.Breakthrough"/>(quality)，晋升本体境界
    ///   - 渡劫战中陨落 → 走 GameManager 死亡分支（身死道消·转世，见 8.3.4），突破不发生
    ///   - 主动放弃（未实现 UI，预留）→ 突破中止，修为留存
    ///
    /// 难度随目标境界递增：前期（筑基/金丹）落雷少而弱，后期（化神/渡劫）多而猛。
    /// </summary>
    public class TribulationTrial : MonoBehaviour
    {
        private System.Action<bool, int> _onComplete;
        private int _targetRealm;          // 冲击进入的境界阶（1~5）
        private InnerDemonMirror _mirror;
        private bool _finished;

        private float _lastHp;
        private float _damageTaken;
        private float _maxHpAtStart = 1f;

        private const float BoltRadius = 3.5f;
        private const float BoltTelegraph = 1.2f;

        /// <summary>开始渡劫战。onComplete(success, quality 0~3)。</summary>
        public static TribulationTrial Begin(int targetRealm, System.Action<bool, int> onComplete)
        {
            var go = new GameObject("TribulationTrial");
            var t = go.AddComponent<TribulationTrial>();
            t._targetRealm = Mathf.Clamp(targetRealm, 1, CultivationSystem.MaxRealm);
            t._onComplete = onComplete;
            t.StartTrial();
            return t;
        }

        private void StartTrial()
        {
            var p = PlayerController.Instance;
            if (p == null) { Finish(false, 0); return; }

            _maxHpAtStart = Mathf.Max(1f, p.Stats.maxHp);
            _lastHp = p.Stats.currentHp;

            // ① 心魔镜像（复用心魔劫的镜像，传 null catalyst + 回调）
            Vector3 spawnPos = p.transform.position + p.transform.forward * 5f;
            _mirror = InnerDemonMirror.Spawn(spawnPos, (InnerDemonCatalyst)null);
            if (_mirror != null)
            {
                _mirror.SetDefeatCallback(OnMirrorDefeated);
                _mirror.SetSuppressRewards(true);   // 渡劫战镜像不掉落 / 不给击杀奖励
            }

            // ② 天劫环境
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Publish(new GameEvents.TribulationStarted { BoltCount = BoltsPerWave() });
            StartCoroutine(HeavenlyBolts());

            string realmName = CultivationSystem.RealmNames[Mathf.Clamp(_targetRealm, 0, CultivationSystem.MaxRealm)];
            Debug.Log($"<color=#c8b0ff>★★ 渡劫战 · 冲击【{realmName}】—— 天劫 + 心魔同场 ★★</color>");
        }

        private void Update()
        {
            if (_finished) return;
            // 累计受创（仅统计掉血，回血不抵消）
            var p = PlayerController.Instance;
            if (p != null)
            {
                float hp = p.Stats.currentHp;
                if (hp < _lastHp) _damageTaken += (_lastHp - hp);
                _lastHp = hp;
            }
        }

        // ========== 天劫环境（周期落雷）==========

        /// <summary>每波落雷数：目标境界越高越多。</summary>
        private int BoltsPerWave() => Mathf.Clamp(_targetRealm, 1, 5);

        /// <summary>落雷间隔：目标境界越高越短（越密）。</summary>
        private float WaveInterval() => Mathf.Lerp(3.2f, 1.4f, (_targetRealm - 1) / 4f);

        /// <summary>单道雷伤害（占最大生命比例）：前期低、后期高。</summary>
        private float BoltDamagePercent() => Mathf.Lerp(0.10f, 0.22f, (_targetRealm - 1) / 4f);

        private IEnumerator HeavenlyBolts()
        {
            // 炼气→筑基（targetRealm==1）：教学级，几乎不落雷
            if (_targetRealm <= 1)
            {
                while (!_finished) yield return null;
                yield break;
            }

            while (!_finished)
            {
                yield return new WaitForSeconds(WaveInterval());
                if (_finished) yield break;

                int n = BoltsPerWave();
                for (int i = 0; i < n; i++)
                {
                    var p = PlayerController.Instance;
                    if (p == null) yield break;
                    // 在玩家当前位置附近随机落点（逼走位）
                    Vector3 jitter = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                    StartCoroutine(SpawnBolt(p.transform.position + jitter));
                    yield return new WaitForSeconds(0.25f);
                }
            }
        }

        private IEnumerator SpawnBolt(Vector3 pos)
        {
            // 预警圈
            FxFactory.SpawnAOERing(pos + Vector3.up * 0.05f, BoltRadius,
                new Color(0.7f, 0.8f, 1f, 1f), lifetime: BoltTelegraph);

            yield return new WaitForSeconds(BoltTelegraph);
            if (_finished) yield break;

            // 落雷视觉 + 伤害判定
            FxFactory.SpawnElementBurst(pos, ElementTag.Thunder, BoltRadius * 0.7f);
            FxFactory.SpawnAOERing(pos + Vector3.up * 0.05f, BoltRadius * 1.1f,
                new Color(1f, 1f, 0.5f, 1f), lifetime: 0.4f);

            var p = PlayerController.Instance;
            if (p == null) yield break;
            float dist = Vector3.Distance(p.transform.position, pos);
            if (dist <= BoltRadius)
            {
                float dmg = p.Stats.maxHp * BoltDamagePercent();
                p.OnDamage(dmg, pos, gameObject);
                CameraShake.TriggerMedium();
            }
        }

        // ========== 结算 ==========

        private void OnMirrorDefeated()
        {
            if (_finished) return;
            int quality = ResolveQuality();
            Finish(true, quality);
        }

        private void OnPlayerDied(GameEvents.PlayerDied _)
        {
            if (_finished) return;
            // 渡劫战中陨落 → GameManager 死亡分支已处理身死道消·转世，这里只清理
            Finish(false, 0);
        }

        /// <summary>
        /// 成色 = 受创比例基础档 + 道心修正档（道心稳→渡劫稳）。
        /// 受创：0 无伤=完美 / &lt;0.3 上品 / &lt;0.7 凡品 / 否则 瑕品。
        /// 道心：入定 +1 / 清明 0 / 心摇 -1 / 入魔 -2，最终钳制 0~3。
        /// </summary>
        private int ResolveQuality()
        {
            float ratio = _damageTaken / _maxHpAtStart;
            int baseQ;
            if (ratio <= 0.001f) baseQ = 3;       // 完美
            else if (ratio < 0.30f) baseQ = 2;    // 上品
            else if (ratio < 0.70f) baseQ = 1;    // 凡品
            else baseQ = 0;                       // 瑕品

            int shift = DaoHeartQualityShift();
            int q = Mathf.Clamp(baseQ + shift, 0, 3);
            if (shift != 0)
                Debug.Log($"<color=#b0c8ff>[渡劫] 道心修正成色：基础 {baseQ} {(shift > 0 ? "+" : "")}{shift} → {q}（{CultivationSystem.QualityNames[q]}）</color>");
            return q;
        }

        /// <summary>道心 → 渡劫成色档位修正：入定 +1 / 清明 0 / 心摇 -1 / 入魔 -2。</summary>
        public static int DaoHeartQualityShift()
        {
            int dx = PlayerStateHooks.Instance.Daoxin;
            if (dx >= 80) return +1;   // 入定 · 道心通明，渡劫如行坦途
            if (dx >= 50) return 0;    // 清明
            if (dx >= 20) return -1;   // 心摇 · 杂念趁虚
            return -2;                 // 入魔 · 心魔趁势，险象环生
        }

        private void Finish(bool success, int quality)
        {
            if (_finished) return;
            _finished = true;
            StopAllCoroutines();
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);

            // 失败时清掉残留镜像（成功时镜像已自毁）
            if (!success && _mirror != null) Destroy(_mirror.gameObject);

            GameEvents.Publish(new GameEvents.TribulationFinished
            {
                Outcome = success ? TribulationOutcome.Success : TribulationOutcome.Catastrophic,
                HitCount = 0
            });

            _onComplete?.Invoke(success, quality);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
        }
    }
}
