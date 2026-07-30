using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 敌人头顶血条 —— 世界空间 Billboard UI
    /// 样式与左上角玩家血条一致：深色背景 + 边框 + 受伤延迟条 + 渐变填充 + 血量数值
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        private Transform _camera;
        private Canvas _canvas;
        private RectTransform _hpFillRt;   // 用 anchorMax.x 控制宽度
        private Image _hpFillImage;
        private RectTransform _damageFillRt;
        private Image _borderImage;
        private TextMeshProUGUI _hpText;
        private CanvasGroup _canvasGroup;

        private float _currentHp;
        private float _maxHp;
        private float _targetRatio = 1f;
        private float _damageRatio = 1f;
        private float _damageDelay;
        private float _showTimer;
        private bool _initialized;

        // 进度条尺寸
        private const float BAR_WIDTH = 160f;
        private const float BAR_HEIGHT = 16f;
        private const float OFFSET_Y = 2.4f;
        private const float DAMAGE_BAR_DELAY = 0.4f;
        private const float DAMAGE_BAR_SPEED = 2.5f;
        private const float SHOW_DURATION = 5f;
        private const float FADE_SPEED = 3f;
        private const float WORLD_SCALE = 0.007f;

        // 边框发光动画
        private float _borderGlowTimer;
        private bool _borderGlowing;

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
            rt.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT + 14f); // 额外空间给文字
            rt.localScale = Vector3.one * WORLD_SCALE;

            // CanvasGroup 用于淡入淡出
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // 初始隐藏，受伤后显示
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // ===== 进度条容器 =====
            var barContainer = new GameObject("BarContainer");
            barContainer.transform.SetParent(transform, false);
            var containerRt = barContainer.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0, 0);
            containerRt.anchorMax = new Vector2(1, 0);
            containerRt.pivot = new Vector2(0.5f, 0);
            containerRt.offsetMin = new Vector2(0, 0);
            containerRt.offsetMax = new Vector2(0, BAR_HEIGHT);

            // 1. 外边框（亮色描边）
            var border = CreateBarImage(barContainer.transform, "Border",
                new Color(0.45f, 0.45f, 0.55f, 0.7f));
            var borderRt = border.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-1.5f, -1.5f);
            borderRt.offsetMax = new Vector2(1.5f, 1.5f);
            _borderImage = border.GetComponent<Image>();

            // 2. 深色背景
            var bg = CreateBarImage(barContainer.transform, "Bg",
                new Color(0.08f, 0.08f, 0.12f, 0.92f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 3. 受伤延迟条（红色）—— 用 anchorMax.x 控制宽度
            var damageFill = CreateBarImage(barContainer.transform, "DamageFill",
                new Color(0.85f, 0.15f, 0.1f, 0.85f));
            _damageFillRt = damageFill.GetComponent<RectTransform>();
            _damageFillRt.anchorMin = new Vector2(0, 0);
            _damageFillRt.anchorMax = new Vector2(1, 1); // anchorMax.x 会动态调整
            _damageFillRt.offsetMin = new Vector2(2, 2);
            _damageFillRt.offsetMax = new Vector2(0, -2); // x=0 让右边缘精确跟随 anchor

            // 4. 血条填充（绿色）—— 用 anchorMax.x 控制宽度
            var hpFill = CreateBarImage(barContainer.transform, "HpFill",
                new Color(0.2f, 0.85f, 0.35f, 1f));
            _hpFillRt = hpFill.GetComponent<RectTransform>();
            _hpFillRt.anchorMin = new Vector2(0, 0);
            _hpFillRt.anchorMax = new Vector2(1, 1); // anchorMax.x 会动态调整
            _hpFillRt.offsetMin = new Vector2(2, 2);
            _hpFillRt.offsetMax = new Vector2(0, -2); // x=0 让右边缘精确跟随 anchor
            _hpFillImage = hpFill.GetComponent<Image>();

            // 5. 高光条（顶部细线，增加质感）
            var highlight = CreateBarImage(barContainer.transform, "Highlight",
                new Color(1f, 1f, 1f, 0.12f));
            var hlRt = highlight.GetComponent<RectTransform>();
            hlRt.anchorMin = new Vector2(0, 0.65f);
            hlRt.anchorMax = new Vector2(1, 1);
            hlRt.offsetMin = new Vector2(3, 0);
            hlRt.offsetMax = new Vector2(-3, -2);

            // 6. 血量数值文字（进度条上方）
            var textGo = new GameObject("HpText");
            textGo.transform.SetParent(transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0);
            textRt.anchorMax = new Vector2(1, 0);
            textRt.pivot = new Vector2(0.5f, 0);
            textRt.offsetMin = new Vector2(0, BAR_HEIGHT + 1f);
            textRt.offsetMax = new Vector2(0, BAR_HEIGHT + 13f);

            _hpText = textGo.AddComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null) _hpText.font = UGuiKit.CjkFont;
            _hpText.fontSize = 11;
            _hpText.alignment = TextAlignmentOptions.Center;
            _hpText.color = new Color(1f, 1f, 1f, 0.95f);
            _hpText.raycastTarget = false;
            _hpText.enableWordWrapping = false;
            _hpText.overflowMode = TextOverflowModes.Overflow;

            // 文字描边（TMP 内建）
            _hpText.outlineColor = new Color(0, 0, 0, 0.85f);
            _hpText.outlineWidth = 0.15f;

            _initialized = true;
        }

        private GameObject CreateBarImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
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

            _currentHp = current;
            _maxHp = max;
            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0;

            // 如果受伤，触发延迟条和边框发光
            if (ratio < _targetRatio)
            {
                _damageDelay = DAMAGE_BAR_DELAY;
                _damageRatio = _targetRatio;
                _showTimer = SHOW_DURATION;
                _borderGlowing = true;
                _borderGlowTimer = 0.4f;
            }

            _targetRatio = ratio;

            if (_hpFillRt != null)
            {
                // 通过 anchorMax.x 控制填充宽度（0~1）
                var aMax = _hpFillRt.anchorMax;
                aMax.x = ratio;
                _hpFillRt.anchorMax = aMax;

                // 血条颜色渐变（与玩家血条一致）
                if (_hpFillImage != null)
                {
                    if (ratio > 0.6f)
                        _hpFillImage.color = new Color(0.2f, 0.85f, 0.35f); // 绿色
                    else if (ratio > 0.3f)
                        _hpFillImage.color = new Color(1f, 0.75f, 0.15f);   // 黄色
                    else
                        _hpFillImage.color = new Color(0.9f, 0.2f, 0.2f);   // 红色
                }
            }

            // 更新数值文字
            if (_hpText != null)
            {
                _hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
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

            // 受伤延迟条动画（平滑追赶）
            if (_damageFillRt != null)
            {
                if (_damageDelay > 0)
                    _damageDelay -= dt;
                else
                    _damageRatio = Mathf.MoveTowards(_damageRatio, _targetRatio, DAMAGE_BAR_SPEED * dt);

                var aMax = _damageFillRt.anchorMax;
                aMax.x = _damageRatio;
                _damageFillRt.anchorMax = aMax;
            }

            // 边框受击发光效果
            if (_borderImage != null && _borderGlowing)
            {
                _borderGlowTimer -= dt;
                if (_borderGlowTimer > 0)
                {
                    float t = _borderGlowTimer / 0.4f;
                    _borderImage.color = Color.Lerp(
                        new Color(0.45f, 0.45f, 0.55f, 0.7f),
                        new Color(1f, 0.6f, 0.2f, 1f),
                        t);
                }
                else
                {
                    _borderImage.color = new Color(0.45f, 0.45f, 0.55f, 0.7f);
                    _borderGlowing = false;
                }
            }

            // 淡入淡出
            if (_canvasGroup != null)
            {
                if (_targetRatio >= 1f)
                {
                    // 满血时隐藏
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, FADE_SPEED * dt);
                }
                else if (_showTimer > 0)
                {
                    _showTimer -= dt;
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, FADE_SPEED * 2f * dt);
                }
                else
                {
                    // 受伤过但计时器结束，保持半透明
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0.5f, FADE_SPEED * dt);
                }
            }
        }
    }
}
