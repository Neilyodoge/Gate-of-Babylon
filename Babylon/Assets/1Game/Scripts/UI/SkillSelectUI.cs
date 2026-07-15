using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4 技能三选一面板（准备房间用）。
    /// 从技能池中随机抽取 3 个最低品质技能供玩家选择一个装备到 Q 槽位。
    /// UITK 程序化构建，无需 uxml/uss。
    /// </summary>
    public class SkillSelectUI : MonoBehaviour
    {
        private static SkillSelectUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private Action<SkillData> _onPicked;

        /// <summary>
        /// 从 skillPool 中抽 3 个最低品质技能显示三选一面板。
        /// 选中后回调 onPicked(skill)；池不足 3 个时全部展示。
        /// </summary>
        public static void Show(SkillData[] skillPool, Action<SkillData> onPicked)
        {
            var candidates = FilterLowestRarity(skillPool);
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[SkillSelectUI] 技能池中没有可选技能");
                onPicked?.Invoke(null);
                return;
            }

            if (_instance == null)
            {
                var go = new GameObject("SkillSelectUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SkillSelectUI>();
                _instance.Build();
            }
            _instance._onPicked = onPicked;
            _instance.Populate(PickRandom(candidates, 3));
            _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._overlay != null)
                _instance._overlay.style.display = DisplayStyle.None;
        }

        private static List<SkillData> FilterLowestRarity(SkillData[] pool)
        {
            if (pool == null || pool.Length == 0) return new List<SkillData>();

            var lowestRarity = ItemRarity.Tian;
            foreach (var s in pool)
                if (s != null && s.rarity < lowestRarity)
                    lowestRarity = s.rarity;

            var result = new List<SkillData>();
            foreach (var s in pool)
                if (s != null && s.rarity == lowestRarity)
                    result.Add(s);
            return result;
        }

        private static List<SkillData> PickRandom(List<SkillData> source, int count)
        {
            var shuffled = new List<SkillData>(source);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            if (shuffled.Count > count)
                shuffled.RemoveRange(count, shuffled.Count - count);
            return shuffled;
        }

        private void Build()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 15f;

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "skill-select-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.92f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            root.Add(_overlay);

            var title = new Label("选择初始技能");
            title.style.fontSize = 36;
            title.style.color = new Color(0.95f, 0.90f, 0.75f);
            title.style.marginBottom = 6;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overlay.Add(title);

            var subtitle = new Label("从下方选择一个技能装备到 Q 槽位，开始你的冒险");
            subtitle.style.fontSize = 15;
            subtitle.style.color = new Color(0.65f, 0.68f, 0.75f);
            subtitle.style.marginBottom = 24;
            _overlay.Add(subtitle);

            var row = new VisualElement { name = "cards" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            _overlay.Add(row);

            ChineseFontHelper.Apply(root);
        }

        private void Populate(List<SkillData> skills)
        {
            var row = _overlay.Q<VisualElement>("cards");
            row.Clear();
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                row.Add(BuildCard(skill));
            }
        }

        private VisualElement BuildCard(SkillData skill)
        {
            var card = new VisualElement();
            card.style.width = 220;
            card.style.marginLeft = 12; card.style.marginRight = 12;
            card.style.marginBottom = 12;
            card.style.paddingTop = 16; card.style.paddingBottom = 16;
            card.style.paddingLeft = 18; card.style.paddingRight = 18;
            card.style.backgroundColor = new Color(0.10f, 0.12f, 0.17f, 1f);

            var typeColor = SkillTypeColor(skill.skillType);
            SetBorder(card, 2, new Color(typeColor.r, typeColor.g, typeColor.b, 0.85f), 10);

            var nameLabel = new Label(skill.skillName);
            nameLabel.style.fontSize = 22;
            nameLabel.style.color = typeColor;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginBottom = 6;
            card.Add(nameLabel);

            var typeLabel = new Label(SkillTypeName(skill.skillType));
            typeLabel.style.fontSize = 12;
            typeLabel.style.color = new Color(0.55f, 0.58f, 0.65f);
            typeLabel.style.marginBottom = 8;
            card.Add(typeLabel);

            if (!string.IsNullOrEmpty(skill.description))
            {
                var desc = new Label(skill.description);
                desc.style.fontSize = 13;
                desc.style.color = new Color(0.72f, 0.74f, 0.80f);
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.marginBottom = 10;
                card.Add(desc);
            }

            card.Add(StatLine($"伤害: {skill.baseDamage:F0}"));
            card.Add(StatLine($"冷却: {skill.cooldown:F1}s"));

            var pick = new Button(() => Confirm(skill)) { text = "选择" };
            pick.style.marginTop = 14;
            pick.style.height = 36;
            pick.style.fontSize = 16;
            pick.style.backgroundColor = new Color(typeColor.r * 0.4f, typeColor.g * 0.4f, typeColor.b * 0.4f, 0.9f);
            pick.style.color = Color.white;
            SetBorder(pick, 1, typeColor, 6);
            card.Add(pick);

            return card;
        }

        private static Label StatLine(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 12;
            l.style.color = new Color(0.78f, 0.80f, 0.85f);
            l.style.marginBottom = 2;
            return l;
        }

        private void Confirm(SkillData skill)
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            var cb = _onPicked;
            _onPicked = null;
            cb?.Invoke(skill);
        }

        private static Color SkillTypeColor(SkillType type) => type switch
        {
            SkillType.AreaDamage => new Color(0.95f, 0.55f, 0.35f),
            SkillType.Projectile => new Color(0.45f, 0.75f, 1.0f),
            SkillType.Dash => new Color(0.50f, 0.95f, 0.65f),
            SkillType.Buff => new Color(1.0f, 0.85f, 0.30f),
            SkillType.Heal => new Color(0.45f, 0.95f, 0.45f),
            SkillType.Summon => new Color(0.80f, 0.55f, 0.95f),
            SkillType.Zone => new Color(0.90f, 0.45f, 0.60f),
            _ => new Color(0.75f, 0.75f, 0.80f),
        };

        private static string SkillTypeName(SkillType type) => type switch
        {
            SkillType.AreaDamage => "范围伤害",
            SkillType.Projectile => "投射物",
            SkillType.Dash => "位移",
            SkillType.Buff => "增益",
            SkillType.Heal => "治疗",
            SkillType.Summon => "召唤",
            SkillType.Zone => "持续区域",
            _ => "技能",
        };

        private static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopColor = color; e.style.borderBottomColor = color;
            e.style.borderLeftColor = color; e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }
    }
}
