using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 化身选择面板（v0.3 MVP 临时 IMGUI 实现）
    /// 进入新一局时由 GameManager 调用 <see cref="Show"/>，玩家从 5 个基础化身中选一个。
    /// 后续可替换为基于 UGUI 的卡牌选择面板（参考 Risk of Rain / 杀戮尖塔）。
    /// </summary>
    public class SpiritRootSelectUI : MonoBehaviour
    {
        private static SpiritRootSelectUI _instance;

        private bool _visible;
        private float _previousTimeScale = 1f;

        /// <summary>外部（NPC / Portal 等）查询面板是否处于打开态，用以禁用其他交互输入</summary>
        public static bool IsVisible => _instance != null && _instance._visible;

        public static void Show()
        {
            EnsureInstance();
            if (_instance._visible) return; // 已经在显示，不重复打开
            _instance._visible = true;

            // 先把可能在跑的顿帧（HitStop）强制清掉，否则会捕获到 0.05/0.02 之类的瞬时
            // 慢动作值，关闭面板后被恢复，导致玩家卡在慢动作里。
            if (HitStop.Instance != null)
                HitStop.Instance.ForceClear();

            _instance._previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            // 防御：只接受合理的恢复值（>= 0.1f），否则强制回 1f。
            // 防止任何残留的顿帧 / 异常状态把游戏卡在极慢速度。
            float prev = _instance._previousTimeScale;
            Time.timeScale = prev >= 0.1f ? prev : 1f;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("SpiritRootSelectUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpiritRootSelectUI>();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // 居中弹窗
            float panelW = 920f;
            float panelH = 460f;
            float x = (Screen.width - panelW) * 0.5f;
            float y = (Screen.height - panelH) * 0.5f;

            // 背景遮罩
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Box(new Rect(x, y, panelW, panelH), "");

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x, y + 18, panelW, 36), "选择化身", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(x, y + 54, panelW, 24),
                "化身决定本局的「被动规则」（Hades 风格的角色差异化）。同一件灵物在不同化身下表现不同。", subStyle);

            // 5 张卡片
            var defs = SpiritRootRegistry.All;
            float cardW = (panelW - 60f) / defs.Count;
            float cardH = 320f;
            float cardY = y + 90f;

            for (int i = 0; i < defs.Count; i++)
            {
                float cx = x + 30f + i * cardW;
                // 第一张是默认推荐卡，画的时候打个高亮边框
                bool isDefault = i == 0;
                DrawCard(new Rect(cx, cardY, cardW - 8f, cardH), defs[i], isDefault);
            }

            // 底部提示
            var bottomStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(x, y + panelH - 30f, panelW, 20),
                "[Esc] 默认选「金化身」 · 选完可在村庄内回到司命使重新选择", bottomStyle);

            // ESC 关闭（默认选第一个）
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Pick(defs[0]);
            }
        }

        private void DrawCard(Rect rect, SpiritRootDef def, bool isDefault)
        {
            // 卡片背景
            var bg = GUI.color;
            GUI.color = new Color(def.displayColor.r * 0.3f, def.displayColor.g * 0.3f, def.displayColor.b * 0.3f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = bg;

            // 默认卡的高亮外框（在背景之上、内容之下，金黄色细边）
            if (isDefault)
            {
                var oc = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                float t = 2f;
                GUI.DrawTexture(new Rect(rect.x - t, rect.y - t, rect.width + 2 * t, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x - t, rect.y + rect.height, rect.width + 2 * t, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x - t, rect.y - t, t, rect.height + 2 * t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width, rect.y - t, t, rect.height + 2 * t), Texture2D.whiteTexture);
                GUI.color = oc;
            }

            // 上边框色块
            var topRect = new Rect(rect.x, rect.y, rect.width, 6f);
            var oldColor = GUI.color;
            GUI.color = def.displayColor;
            GUI.DrawTexture(topRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            // 默认徽章（右上角小标签）
            if (isDefault)
            {
                var badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.85f, 0.2f) }
                };
                GUI.Label(new Rect(rect.x + rect.width - 60f, rect.y + 8f, 56f, 18f), "★ 默认", badgeStyle);
            }

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = def.displayColor }
            };
            GUI.Label(new Rect(rect.x, rect.y + 18, rect.width, 30), def.name, nameStyle);

            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y + 56, rect.width - 24, 110), def.passive, descStyle);

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.6f) }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y + 170, rect.width - 24, 30), def.starterItemHint, hintStyle);

            var tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y + 200, rect.width - 24, 70), def.tooltip, tooltipStyle);

            // 选择按钮
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            if (GUI.Button(new Rect(rect.x + 16, rect.y + rect.height - 44, rect.width - 32, 32), "选择", btnStyle))
            {
                Pick(def);
            }
        }

        private void Pick(SpiritRootDef def)
        {
            var player = PlayerController.Instance;
            if (player != null)
            {
                var ctrl = player.GetComponent<SpiritRootController>();
                if (ctrl != null) ctrl.Select(def.type, player.Stats);
            }
            Hide();
        }
    }
}
