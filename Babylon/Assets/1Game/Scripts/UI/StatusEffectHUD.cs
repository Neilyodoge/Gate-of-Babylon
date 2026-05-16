using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 玩家身上当前所有 StatusEffect / 化身的可视化（顶部 IMGUI 小条）。
    /// 包括：当前选中化身的标签 + 所有激活中的 BUFF 列表（带层数 / 倒计时）。
    /// </summary>
    public class StatusEffectHUD : MonoBehaviour
    {
        private static StatusEffectHUD _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("StatusEffectHUD");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StatusEffectHUD>();
        }

        private void OnGUI()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            var status = player.GetComponent<StatusEffectController>();
            var rootCtrl = player.GetComponent<SpiritRootController>();
            if (status == null) return;

            // 顶部居中条
            float barW = 480f;
            float x = (Screen.width - barW) * 0.5f;
            float y = 8f;

            // 化身标签
            if (rootCtrl != null && rootCtrl.CurrentDef != null)
            {
                var def = rootCtrl.CurrentDef;
                var rect = new Rect(x, y, barW, 22f);
                var bgColor = GUI.color;
                GUI.color = new Color(def.displayColor.r * 0.3f, def.displayColor.g * 0.3f, def.displayColor.b * 0.3f, 0.85f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = bgColor;

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = def.displayColor }
                };
                GUI.Label(rect, $"化身·{def.name}　·　{def.passive}", style);
                y += 26f;
            }

            // BUFF 图标条
            float iconW = 86f;
            float iconH = 30f;
            float spacing = 4f;
            int idx = 0;
            foreach (var kv in status.Effects)
            {
                var eff = kv.Value;
                if (string.IsNullOrEmpty(eff.displayName)) continue;

                float ix = x + idx * (iconW + spacing);
                if (ix + iconW > x + barW)
                {
                    idx = 0;
                    y += iconH + 4f;
                    ix = x;
                }

                var iconRect = new Rect(ix, y, iconW, iconH);
                var bg = GUI.color;
                GUI.color = new Color(eff.uiColor.r * 0.3f, eff.uiColor.g * 0.3f, eff.uiColor.b * 0.3f, 0.85f);
                GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
                GUI.color = bg;

                // 顶部色条
                var stripe = GUI.color;
                GUI.color = eff.uiColor;
                GUI.DrawTexture(new Rect(iconRect.x, iconRect.y, iconRect.width, 3f), Texture2D.whiteTexture);
                GUI.color = stripe;

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                string label = eff.displayName;
                if (eff.maxStacks > 1) label += $" ×{eff.stacks}";
                if (!eff.IsPermanent) label += $"\n{eff.duration:F1}s";
                GUI.Label(iconRect, label, style);

                idx++;
            }
        }
    }
}
