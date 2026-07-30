using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 伤害飘字管理器 —— 在世界坐标位置显示伤害数字
    /// 挂载在 Canvas 上，监听事件自动生成飘字
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;

        private const int MAX_POPUPS = 20;
        private readonly PopupData[] _popups = new PopupData[MAX_POPUPS];
        private int _nextIndex;

        private class PopupData
        {
            public GameObject Go;
            public TextMeshProUGUI Text;
            public RectTransform Rt;
            public Vector3 WorldPos;
            public float Timer;
            public float Duration;
            public Vector3 Velocity;
        }

        private void Start()
        {
            GameEvents.Subscribe<GameEvents.DamageNumberRequested>(OnDamageNumber);

            // 预创建飘字对象池
            for (int i = 0; i < MAX_POPUPS; i++)
            {
                var go = new GameObject($"DmgPopup_{i}");
                go.transform.SetParent(transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(120, 40);
                var text = go.AddComponent<TextMeshProUGUI>();
                if (UGuiKit.CjkFont != null) text.font = UGuiKit.CjkFont;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                text.fontStyle = FontStyles.Bold;

                // 描边效果（TMP 内建描边，避免 UnityEngine.UI.Outline 不支持 TMP）
                text.outlineColor = new Color(0, 0, 0, 0.9f);
                text.outlineWidth = 0.2f;

                go.SetActive(false);

                _popups[i] = new PopupData
                {
                    Go = go,
                    Text = text,
                    Rt = rt,
                    Timer = 0,
                    Duration = 0
                };
            }
        }

        private void OnDamageNumber(GameEvents.DamageNumberRequested evt)
        {
            var popup = _popups[_nextIndex];
            _nextIndex = (_nextIndex + 1) % MAX_POPUPS;

            // 配置飘字
            popup.WorldPos = evt.WorldPosition + Vector3.up * 1.5f;
            popup.Duration = evt.IsCrit ? 1.2f : 0.8f;
            popup.Timer = popup.Duration;

            // 随机偏移，避免重叠
            float randX = Random.Range(-30f, 30f);
            popup.Velocity = new Vector3(randX, 80f, 0);

            // 文字内容和样式
            int dmgInt = Mathf.CeilToInt(evt.Damage);
            string tag = evt.SpecialTag;

            if (!string.IsNullOrEmpty(tag))
            {
                // 特殊伤害类型飘字
                switch (tag)
                {
                    case "焚天":
                        popup.Text.text = $"🔥焚天 {dmgInt}";
                        popup.Text.fontSize = 24;
                        popup.Text.color = new Color(1f, 0.35f, 0f); // 深橙色
                        popup.Duration = 1.0f;
                        popup.Timer = 1.0f;
                        break;
                    case "剑阵":
                        popup.Text.text = $"⚔️{dmgInt}";
                        popup.Text.fontSize = 18;
                        popup.Text.color = new Color(0.5f, 0.7f, 1f); // 淡蓝色
                        break;
                    case "御风":
                        popup.Text.text = $"🌀{dmgInt}";
                        popup.Text.fontSize = 18;
                        popup.Text.color = new Color(0.3f, 0.9f, 0.7f); // 青绿色
                        break;
                    case "火墙":
                        popup.Text.text = $"🔥{dmgInt}";
                        popup.Text.fontSize = 18;
                        popup.Text.color = new Color(1f, 0.4f, 0.1f); // 火红色
                        break;
                    case "元素爆发":
                        popup.Text.text = $"⚡{dmgInt}";
                        popup.Text.fontSize = 26;
                        popup.Text.color = new Color(0.9f, 0.8f, 1f); // 淡紫色
                        popup.Duration = 1.0f;
                        popup.Timer = 1.0f;
                        break;
                    case "格挡":
                        popup.Text.text = "🛡️格挡!";
                        popup.Text.fontSize = 24;
                        popup.Text.color = new Color(1f, 0.85f, 0.2f); // 金色
                        popup.Duration = 1.0f;
                        popup.Timer = 1.0f;
                        break;
                    case "反弹":
                        popup.Text.text = $"↩️反弹 {dmgInt}";
                        popup.Text.fontSize = 22;
                        popup.Text.color = new Color(1f, 0.9f, 0.3f); // 金黄色
                        popup.Duration = 1.0f;
                        popup.Timer = 1.0f;
                        break;
                    case "玉碎":
                        popup.Text.text = "💎玉碎免疫!";
                        popup.Text.fontSize = 28;
                        popup.Text.color = new Color(0.6f, 1f, 0.85f); // 翡翠色
                        popup.Duration = 1.5f;
                        popup.Timer = 1.5f;
                        break;
                    case "涅槃":
                        popup.Text.text = "🔥涅槃复活!";
                        popup.Text.fontSize = 30;
                        popup.Text.color = new Color(1f, 0.85f, 0.2f); // 金色
                        popup.Duration = 2.0f;
                        popup.Timer = 2.0f;
                        break;
                    case "嗜血":
                        popup.Text.text = $"🩸{dmgInt}";
                        popup.Text.fontSize = 18;
                        popup.Text.color = new Color(0.8f, 0.1f, 0.2f); // 暗红色
                        break;
                    case "治疗":
                        popup.Text.text = $"💚+{dmgInt}";
                        popup.Text.fontSize = 22;
                        popup.Text.color = new Color(0.2f, 1f, 0.4f); // 翠绿色
                        popup.Velocity = new Vector3(0, 100f, 0); // 治疗飘字向上飘更快
                        popup.Duration = 1.0f;
                        popup.Timer = 1.0f;
                        break;
                    default:
                        popup.Text.text = $"✦{dmgInt}";
                        popup.Text.fontSize = 20;
                        popup.Text.color = new Color(0.8f, 0.8f, 1f);
                        break;
                }
            }
            else if (evt.IsCrit)
            {
                popup.Text.text = $"暴击 {dmgInt}!";
                popup.Text.fontSize = 28;
                popup.Text.color = new Color(1f, 0.85f, 0f); // 金色暴击
            }
            else if (evt.IsBurn)
            {
                popup.Text.text = $"🔥{dmgInt}";
                popup.Text.fontSize = 18;
                popup.Text.color = new Color(1f, 0.55f, 0.1f); // 橙色灼烧
            }
            else if (evt.IsPlayerDamage)
            {
                popup.Text.text = $"-{dmgInt}";
                popup.Text.fontSize = 22;
                popup.Text.color = new Color(1f, 0.3f, 0.3f); // 红色（玩家受伤）
            }
            else
            {
                popup.Text.text = $"{dmgInt}";
                popup.Text.fontSize = 20;
                popup.Text.color = Color.white; // 白色（敌人受伤）
            }

            popup.Go.SetActive(true);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            for (int i = 0; i < MAX_POPUPS; i++)
            {
                var popup = _popups[i];
                if (!popup.Go.activeSelf) continue;

                popup.Timer -= Time.deltaTime;
                if (popup.Timer <= 0)
                {
                    popup.Go.SetActive(false);
                    continue;
                }

                // 上浮
                popup.WorldPos += Vector3.up * 1.5f * Time.deltaTime;

                // 世界坐标转屏幕坐标
                Vector3 screenPos = cam.WorldToScreenPoint(popup.WorldPos);

                // 在相机后面则隐藏
                if (screenPos.z < 0)
                {
                    popup.Go.SetActive(false);
                    continue;
                }

                // 转换为 Canvas 坐标
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)canvas.transform, screenPos, null, out Vector2 localPos);
                popup.Rt.anchoredPosition = localPos + (Vector2)popup.Velocity * (1f - popup.Timer / popup.Duration) * 0.3f;

                // 淡出
                float alpha = Mathf.Clamp01(popup.Timer / (popup.Duration * 0.4f));
                var color = popup.Text.color;
                color.a = alpha;
                popup.Text.color = color;

                // 暴击缩放动画
                float t = 1f - popup.Timer / popup.Duration;
                float scale = t < 0.1f ? Mathf.Lerp(1.5f, 1f, t / 0.1f) : 1f;
                popup.Rt.localScale = Vector3.one * scale;
            }
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.DamageNumberRequested>(OnDamageNumber);
        }
    }
}
