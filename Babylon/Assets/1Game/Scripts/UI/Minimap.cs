using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 小地图 —— 显示当前层的房间布局和玩家位置
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [SerializeField] private RectTransform mapPanel;
        [SerializeField] private Image playerDot;

        private readonly List<(Image icon, int roomIndex, RoomType type)> _roomIcons = new();
        private int _currentRoomIndex;
        private int _totalRooms;

        // V0.4.2：RoomType 已提升为顶层领域枚举 XianTu.RoomType（见 Core/Level/RoomType.cs）。

        /// <summary>初始化小地图布局</summary>
        public void Initialize(List<RoomType> roomLayout)
        {
            _totalRooms = roomLayout.Count;
            _currentRoomIndex = 0;

            // 清空旧图标
            foreach (var (icon, _, _) in _roomIcons)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            _roomIcons.Clear();

            float spacing = 30f;
            float startX = -(_totalRooms - 1) * spacing / 2f;

            for (int i = 0; i < roomLayout.Count; i++)
            {
                var iconGo = new GameObject($"RoomIcon_{i}");
                iconGo.transform.SetParent(mapPanel, false);
                var rt = iconGo.AddComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0);
                rt.sizeDelta = new Vector2(20, 20);

                var img = iconGo.AddComponent<Image>();
                img.color = GetRoomColor(roomLayout[i], false);

                // 房间类型标记文字
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(iconGo.transform, false);
                var labelRt = labelGo.AddComponent<RectTransform>();
                labelRt.anchoredPosition = Vector2.zero;
                labelRt.sizeDelta = new Vector2(20, 20);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = GetRoomSymbol(roomLayout[i]);
                label.fontSize = 10;
                if (UGuiKit.CjkFont != null) label.font = UGuiKit.CjkFont;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;

                _roomIcons.Add((img, i, roomLayout[i]));

                // 连接线
                if (i < roomLayout.Count - 1)
                {
                    var lineGo = new GameObject($"Line_{i}");
                    lineGo.transform.SetParent(mapPanel, false);
                    var lineRt = lineGo.AddComponent<RectTransform>();
                    lineRt.anchoredPosition = new Vector2(startX + i * spacing + spacing / 2f, 0);
                    lineRt.sizeDelta = new Vector2(spacing - 20, 2);
                    var lineImg = lineGo.AddComponent<Image>();
                    lineImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
            }

            UpdateCurrentRoom(0);
        }

        /// <summary>更新当前房间高亮</summary>
        public void UpdateCurrentRoom(int roomIndex)
        {
            _currentRoomIndex = roomIndex;

            for (int i = 0; i < _roomIcons.Count; i++)
            {
                var (icon, idx, type) = _roomIcons[i];
                if (icon == null) continue;

                if (idx < roomIndex)
                    icon.color = GetRoomColor(type, true) * 0.5f; // 已通过的房间变暗
                else if (idx == roomIndex)
                    icon.color = Color.white; // 当前房间高亮
                else
                    icon.color = GetRoomColor(type, false);
            }

            // 更新玩家点位置
            if (playerDot != null && _roomIcons.Count > roomIndex)
            {
                var targetIcon = _roomIcons[roomIndex].icon;
                if (targetIcon != null)
                    playerDot.rectTransform.anchoredPosition =
                        targetIcon.rectTransform.anchoredPosition + new Vector2(0, 15);
            }
        }

        private Color GetRoomColor(RoomType type, bool cleared)
        {
            return type switch
            {
                RoomType.Battle => cleared ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.8f, 0.3f, 0.3f, 0.8f),
                RoomType.Elite => cleared ? new Color(0.5f, 0.3f, 0.15f) : new Color(0.95f, 0.5f, 0.2f, 0.9f),
                RoomType.Event => cleared ? new Color(0.3f, 0.35f, 0.5f) : new Color(0.6f, 0.7f, 1f, 0.8f),
                RoomType.Shop => new Color(0.9f, 0.8f, 0.3f, 0.8f),
                RoomType.Rest => new Color(0.3f, 0.7f, 1f, 0.8f),
                RoomType.Treasure => new Color(1f, 0.6f, 0.1f, 0.8f),
                RoomType.Boss => new Color(0.8f, 0.1f, 0.1f, 0.9f),
                RoomType.Upgrade => new Color(0.4f, 0.8f, 0.4f, 0.8f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }

        private string GetRoomSymbol(RoomType type)
        {
            return type switch
            {
                RoomType.Battle => "⚔",
                RoomType.Elite => "⚡",
                RoomType.Event => "?",
                RoomType.Shop => "$",
                RoomType.Rest => "♥",
                RoomType.Treasure => "★",
                RoomType.Boss => "☠",
                RoomType.Upgrade => "↑",
                _ => "·"
            };
        }
    }
}
