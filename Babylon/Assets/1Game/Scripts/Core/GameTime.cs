using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 游戏内时间单例（v0.5 洞府种田核心）。
    ///
    /// 跟 <see cref="Time.time"/> 的区别：
    /// - Unity Time.time：基于真实时间，玩家不在线时不流逝（场景重启会重置）
    /// - GameTime.Time：游戏内"修仙历"时间，跨场景持久化，支持加速 / 暂停
    ///
    /// 洞府模块（灵田生长、炼丹、炼器）用 GameTime 而非 Time.time。
    /// 战斗系统继续用 Time.time（避免大规模替换）。
    ///
    /// 加速 / 暂停：
    /// - <see cref="TimeScale"/>：默认 1.0；玩家点"加速"消耗灵气把它调到 4.0~10.0
    /// - <see cref="IsPaused"/>：进入战斗 / 弹出 UI 时暂停洞府时间
    /// </summary>
    public class GameTime : MonoBehaviour
    {
        private static GameTime _instance;
        public static GameTime Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameTime");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GameTime>();
                }
                return _instance;
            }
        }

        /// <summary>游戏内累积时间（秒）。可跨场景累积。</summary>
        public float Time { get; private set; }

        /// <summary>每帧实际累积量（=Time.deltaTime * TimeScale * (IsPaused ? 0 : 1)）</summary>
        public float DeltaTime { get; private set; }

        /// <summary>时间倍速（默认 1.0，加速时调高）</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>是否暂停（进入战斗时设为 true，避免洞府生长时段被战斗时间影响）</summary>
        public bool IsPaused { get; set; } = false;

        private void Update()
        {
            if (IsPaused)
            {
                DeltaTime = 0f;
                return;
            }
            DeltaTime = UnityEngine.Time.deltaTime * TimeScale;
            Time += DeltaTime;
        }

        // ========== 便捷接口 ==========

        /// <summary>设置加速倍率（消耗灵气加速洞府生长的逻辑封装）</summary>
        public bool TrySetSpeed(float speed, int qiCostPerSecond = 1)
        {
            if (speed <= 0f) return false;
            TimeScale = speed;
            return true;
        }

        /// <summary>暂停 / 恢复（进入战斗 / 退出战斗时调用）</summary>
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        /// <summary>把秒数格式化为"X 分 Y 秒"显示用</summary>
        public static string FormatDuration(float seconds)
        {
            if (seconds < 60f) return $"{seconds:F0}s";
            if (seconds < 3600f) return $"{seconds / 60f:F0}m {seconds % 60f:F0}s";
            return $"{seconds / 3600f:F0}h {(seconds % 3600f) / 60f:F0}m";
        }
    }
}
