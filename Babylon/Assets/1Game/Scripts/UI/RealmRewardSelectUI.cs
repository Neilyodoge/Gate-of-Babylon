using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 境界突破 3 选 1 奖励选择面板（v0.4 最小可用版 / IMGUI 临时实现）。
    ///
    /// 调用：RealmRewardSelectUI.Show(rewards, onSelected);
    ///       面板显示 → 玩家点其中一个 → onSelected 回调被触发 → 面板自动关闭。
    /// 期间游戏继续进行（不主动暂停时间），但鼠标解锁可点击。
    /// </summary>
    public class RealmRewardSelectUI : MonoBehaviour
    {
        private static RealmRewardSelectUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private List<RealmReward> _options;
        private Action<RealmReward> _onSelected;
        private string _realmName;

        public static void Show(string realmName, List<RealmReward> options, Action<RealmReward> onSelected)
        {
            if (_instance == null)
            {
                var go = new GameObject("RealmRewardSelectUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RealmRewardSelectUI>();
            }
            _instance._realmName = realmName;
            _instance._options = options;
            _instance._onSelected = onSelected;
            _instance._visible = options != null && options.Count > 0;

            if (_instance._visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public static void HideImmediate()
        {
            if (_instance != null) _instance._visible = false;
        }

        private void OnGUI()
        {
            if (!_visible || _options == null || _options.Count == 0) return;

            // 半透明遮罩
            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = bg;

            // 标题
            float w = 720f;
            float h = 480f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // 主面板背景
            GUI.color = new Color(0.05f, 0.04f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.5f, 0.4f, 0.7f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
            GUI.color = bg;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.5f, 1f) }
            };
            GUI.Label(new Rect(x, y + 12f, w, 36f), $"★ 境界突破 · {_realmName} ★", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(x, y + 50f, w, 22f), "选择一项奖励（整局生效）", subStyle);

            // 三张卡片
            int count = Mathf.Min(3, _options.Count);
            float cardW = (w - 80f) / count;
            float cardH = h - 130f;
            float startX = x + 20f;
            float cardY = y + 88f;

            for (int i = 0; i < count; i++)
            {
                var reward = _options[i];
                var cardRect = new Rect(startX + i * (cardW + 20f), cardY, cardW, cardH);

                // 卡片背景
                Color cardBg = new Color(0.08f, 0.07f, 0.13f, 0.95f);
                GUI.color = cardBg;
                GUI.DrawTexture(cardRect, Texture2D.whiteTexture);

                // 顶部主题色条
                GUI.color = reward.displayColor;
                GUI.DrawTexture(new Rect(cardRect.x, cardRect.y, cardRect.width, 6f), Texture2D.whiteTexture);
                GUI.color = bg;

                // 类别 tag
                var tagStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = reward.displayColor }
                };
                GUI.Label(new Rect(cardRect.x, cardRect.y + 14f, cardRect.width, 18f), CategoryTag(reward.category), tagStyle);

                // 奖励名
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = reward.displayColor }
                };
                GUI.Label(new Rect(cardRect.x + 10f, cardRect.y + 40f, cardRect.width - 20f, 50f), reward.displayName, nameStyle);

                // 描述
                var descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 0.95f) },
                    wordWrap = true
                };
                GUI.Label(new Rect(cardRect.x + 14f, cardRect.y + 100f, cardRect.width - 28f, cardRect.height - 170f),
                    reward.description, descStyle);

                // 选择按钮
                var btnRect = new Rect(cardRect.x + 18f, cardRect.y + cardRect.height - 56f, cardRect.width - 36f, 40f);
                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                GUI.backgroundColor = reward.displayColor * 0.65f;
                if (GUI.Button(btnRect, "选择此项", btnStyle))
                {
                    SelectReward(reward);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void SelectReward(RealmReward reward)
        {
            _visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _onSelected?.Invoke(reward);
        }

        private static string CategoryTag(RealmRewardCategory cat) => cat switch
        {
            RealmRewardCategory.Numeric => "[ 数值类 ]",
            RealmRewardCategory.Mechanic => "[ 机制类 ]",
            RealmRewardCategory.Structural => "[ 结构类 ]",
            RealmRewardCategory.Risk => "[ 风险类 ]",
            RealmRewardCategory.SpiritTalent => "[ 化身天赋 ]",
            _ => "[ ? ]"
        };
    }
}
