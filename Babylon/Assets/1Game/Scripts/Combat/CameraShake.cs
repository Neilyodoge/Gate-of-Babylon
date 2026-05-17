using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 轻量级镜头摇晃 —— 不依赖 Cinemachine，仅对 Camera.main 施加本地偏移。
    ///
    /// 调用方式：
    ///   CameraShake.Trigger(0.25f, 0.4f);            // 时长 0.25s，强度 0.4 单位
    ///   CameraShake.TriggerBig();                    // 心魔劫开战 / 雷柱命中
    ///
    /// 实现：
    /// - 自动在 Camera.main 上挂一个 driver；切场景后会自动重新挂上
    /// - 偏移在 LateUpdate 阶段叠加到 cameraTransform.localPosition，
    ///   以"添加 / 还原"的方式工作，不破坏 follow 控制器的 lerp 逻辑
    /// </summary>
    public static class CameraShake
    {
        public static void Trigger(float duration, float intensity, float frequency = 28f)
        {
            EnsureDriver()?.Begin(duration, intensity, frequency);
        }

        /// <summary>大事件（渡劫 / 心魔劫 / 击杀 Boss）</summary>
        public static void TriggerBig() => Trigger(0.5f, 0.45f, 30f);

        /// <summary>中等事件（暴击命中 / 重击）</summary>
        public static void TriggerMedium() => Trigger(0.18f, 0.18f, 32f);

        /// <summary>小事件（普通命中）</summary>
        public static void TriggerLight() => Trigger(0.08f, 0.06f, 40f);

        private static CameraShakeDriver _driver;
        private static CameraShakeDriver EnsureDriver()
        {
            if (_driver != null) return _driver;
            var cam = Camera.main;
            if (cam == null) return null;
            _driver = cam.GetComponent<CameraShakeDriver>();
            if (_driver == null) _driver = cam.gameObject.AddComponent<CameraShakeDriver>();
            return _driver;
        }
    }

    internal class CameraShakeDriver : MonoBehaviour
    {
        private float _duration;
        private float _t;
        private float _intensity;
        private float _frequency;
        private Vector3 _offset;
        private bool _active;

        public void Begin(float duration, float intensity, float frequency)
        {
            // 多次触发取最大值（避免被弱触发覆盖）
            if (_active)
            {
                _duration = Mathf.Max(_duration, duration);
                _intensity = Mathf.Max(_intensity, intensity);
                _frequency = Mathf.Max(_frequency, frequency);
                return;
            }
            _duration = duration;
            _t = 0f;
            _intensity = intensity;
            _frequency = frequency;
            _active = true;
        }

        private void LateUpdate()
        {
            // 先把上一帧 offset 还原
            if (_offset.sqrMagnitude > 0f)
            {
                transform.localPosition -= _offset;
                _offset = Vector3.zero;
            }
            if (!_active) return;

            _t += Time.unscaledDeltaTime;
            if (_t >= _duration)
            {
                _active = false;
                return;
            }
            float k = 1f - (_t / _duration);   // 衰减曲线
            float amp = _intensity * k * k;
            float phase = Time.unscaledTime * _frequency;

            _offset = new Vector3(
                Mathf.Sin(phase * 1.7f) * amp,
                Mathf.Sin(phase * 2.3f + 1.2f) * amp * 0.6f,
                0f);
            transform.localPosition += _offset;
        }

        private void OnDisable()
        {
            if (_offset.sqrMagnitude > 0f)
            {
                transform.localPosition -= _offset;
                _offset = Vector3.zero;
            }
            _active = false;
        }
    }
}
