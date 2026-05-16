using UnityEngine;
using System.Collections;

namespace XianTu
{
    /// <summary>
    /// 顿帧系统（HitStop）
    /// 攻击命中瞬间短暂降低 Time.timeScale，增强打击感
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Header("顿帧参数")]
        [Tooltip("普通攻击顿帧持续时间（真实时间）")]
        [SerializeField] private float normalHitDuration = 0.05f;
        [Tooltip("普通攻击顿帧时的 TimeScale")]
        [SerializeField] private float normalHitTimeScale = 0.05f;

        [Tooltip("重击顿帧持续时间")]
        [SerializeField] private float heavyHitDuration = 0.1f;
        [Tooltip("重击顿帧时的 TimeScale")]
        [SerializeField] private float heavyHitTimeScale = 0.02f;

        [Tooltip("击杀顿帧持续时间")]
        [SerializeField] private float killHitDuration = 0.12f;
        [Tooltip("击杀顿帧时的 TimeScale")]
        [SerializeField] private float killHitTimeScale = 0.01f;

        private Coroutine _currentHitStop;
        private float _originalFixedDeltaTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        /// <summary>触发普通攻击顿帧</summary>
        public void TriggerNormal()
        {
            Trigger(normalHitDuration, normalHitTimeScale);
        }

        /// <summary>触发重击顿帧</summary>
        public void TriggerHeavy()
        {
            Trigger(heavyHitDuration, heavyHitTimeScale);
        }

        /// <summary>触发击杀顿帧</summary>
        public void TriggerKill()
        {
            Trigger(killHitDuration, killHitTimeScale);
        }

        /// <summary>触发自定义顿帧</summary>
        public void Trigger(float duration, float timeScale)
        {
            if (_currentHitStop != null)
                StopCoroutine(_currentHitStop);
            _currentHitStop = StartCoroutine(HitStopCoroutine(duration, timeScale));
        }

        private IEnumerator HitStopCoroutine(float duration, float timeScale)
        {
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime * timeScale;

            // 使用真实时间等待
            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _currentHitStop = null;
        }

        /// <summary>
        /// 强制中止当前顿帧并恢复时间。
        /// 用于打开暂停式 UI（化身选择等）前调用，避免协程半路把
        /// timeScale 留在 0.05/0.02/0.01 被 UI 捕获，导致关闭后卡在慢动作。
        /// </summary>
        public void ForceClear()
        {
            if (_currentHitStop != null)
            {
                StopCoroutine(_currentHitStop);
                _currentHitStop = null;
            }
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }

        private void OnDestroy()
        {
            // 确保销毁时恢复 TimeScale
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
        }
    }
}
