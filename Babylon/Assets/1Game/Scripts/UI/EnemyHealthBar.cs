using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 敌人头顶血条 —— 世界空间 Billboard UI
    /// 挂载在敌人身上，自动面向相机
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        private Transform _camera;
        private Canvas _canvas;
        private Image _hpFill;
        private Image _damageFill;
        private CanvasGroup _canvasGroup;

        private float _targetRatio = 1f;
        private float _damageRatio = 1f;
        private float _damageDelay;
        private float _showTimer;
        private bool _initialized;

        private const float BAR_WIDTH = 120f;
        private const float BAR_HEIGHT = 12f;
        private const float OFFSET_Y = 2.2f; // 头顶偏移
        private const float DAMAGE_BAR_DELAY = 0.3f;
        private const float DAMAGE_BAR_SPEED = 3f;
        private const float SHOW_DURATION = 5f; // 受伤后显示时长
        private const float FADE_SPEED = 3f;

        /// <summary>
        /// 在敌人身上创建血条
        /// </summary>
        public static EnemyHealthBar Create(GameObject enemy)
        {
            var barGo = new GameObject("HealthBar");
            barGo.transform.SetParent(enemy.transform, false);
            barGo.transform.localPosition = new Vector3(0, OFFSET_Y, 0);

            var healthBar = barGo.AddComponent<EnemyHealthBar>();
            healthBar.BuildUI();
            return healthBar;
        }

        private void BuildUI()
        {
            // 世界空间 Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 50;

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
            rt.localScale = Vector3.one * 0.008f; // 缩小到世界空间合适大小

            // CanvasGroup 用于淡入淡出
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f; // 初始显示（满血也可见）
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // 背景
            var bg = CreateBarImage("Bg", new Color(0.1f, 0.1f, 0.15f, 0.85f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 受伤延迟条（红色）
            var damageFill = CreateBarImage("DamageFill", new Color(0.85f, 0.15f, 0.15f, 0.8f));
            var dmgRt = damageFill.GetComponent<RectTransform>();
            dmgRt.anchorMin = Vector2.zero;
            dmgRt.anchorMax = Vector2.one;
            dmgRt.offsetMin = new Vector2(1, 1);
            dmgRt.offsetMax = new Vector2(-1, -1);
            _damageFill = damageFill.GetComponent<Image>();
            _damageFill.type = Image.Type.Filled;
            _damageFill.fillMethod = Image.FillMethod.Horizontal;

            // 血条填充（绿色）
            var hpFill = CreateBarImage("HpFill", new Color(0.2f, 0.85f, 0.3f, 1f));
            var hpRt = hpFill.GetComponent<RectTransform>();
            hpRt.anchorMin = Vector2.zero;
            hpRt.anchorMax = Vector2.one;
            hpRt.offsetMin = new Vector2(1, 1);
            hpRt.offsetMax = new Vector2(-1, -1);
            _hpFill = hpFill.GetComponent<Image>();
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;

            // 边框
            var border = CreateBarImage("Border", new Color(0.4f, 0.4f, 0.5f, 0.5f));
            var borderRt = border.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-0.5f, -0.5f);
            borderRt.offsetMax = new Vector2(0.5f, 0.5f);
            border.GetComponent<Image>().raycastTarget = false;

            _initialized = true;
        }

        private GameObject CreateBarImage(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        /// <summary>
        /// 更新血条显示
        /// </summary>
        public void UpdateHealth(float current, float max)
        {
            if (!_initialized) return;

            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0;

            // 如果受伤，触发延迟条
            if (ratio < _targetRatio)
            {
                _damageDelay = DAMAGE_BAR_DELAY;
                _damageRatio = _targetRatio;
                _showTimer = SHOW_DURATION;
            }

            _targetRatio = ratio;

            if (_hpFill != null)
            {
                _hpFill.fillAmount = ratio;

                // 血条颜色
                if (ratio > 0.6f)
                    _hpFill.color = new Color(0.2f, 0.85f, 0.3f);
                else if (ratio > 0.3f)
                    _hpFill.color = new Color(1f, 0.75f, 0.15f);
                else
                    _hpFill.color = new Color(0.9f, 0.2f, 0.2f);
            }
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            // Billboard：始终面向相机
            if (_camera == null)
            {
                var cam = Camera.main;
                if (cam != null) _camera = cam.transform;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.rotation;
            }

            float dt = Time.deltaTime;

            // 受伤延迟条动画
            if (_damageFill != null)
            {
                if (_damageDelay > 0)
                    _damageDelay -= dt;
                else
                    _damageRatio = Mathf.MoveTowards(_damageRatio, _targetRatio, DAMAGE_BAR_SPEED * dt);

                _damageFill.fillAmount = _damageRatio;
            }

            // 淡入淡出
            if (_canvasGroup != null)
            {
                if (_showTimer > 0)
                {
                    _showTimer -= dt;
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, FADE_SPEED * dt);
                }
                else if (_targetRatio < 1f)
                {
                    // 受伤过但计时器结束，缓慢淡出
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0.4f, FADE_SPEED * dt);
                }
                else
                {
                    // 满血时半透明显示
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0.3f, FADE_SPEED * dt);
                }
            }
        }
    }
}
