using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4 技能三选一面板（准备房间用，V0.4.6 改 uGUI+TMP）。
    /// 从技能池中随机抽取 3 个最低品质技能供玩家选择一个装备到 Q 槽位。
    /// </summary>
    public class SkillSelectUI : MonoBehaviour
    {
        private static SkillSelectUI _instance;
        private GameObject _root;
        private RectTransform _cardsRow;
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
            _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance._root.SetActive(false);
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
            var canvas = UGuiKit.CreateOverlayCanvas("SkillSelectCanvas", 130, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.03f, 0.04f, 0.07f, 0.92f));

            var center = UGuiKit.CreatePanel(_root.transform, "Center", new Vector2(1000f, 10f), new Color(0, 0, 0, 0));
            var fit = center.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var v = UGuiKit.AddVLayout(center, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter, false, false);
            v.childControlWidth = false;

            var title = UGuiKit.CreateText(center, "选择初始技能", 36, new Color(0.95f, 0.90f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 48f);
            var subtitle = UGuiKit.CreateText(center, "从下方选择一个技能装备到 Q 槽位，开始你的冒险", 15, new Color(0.65f, 0.68f, 0.75f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(subtitle, 24f);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(center, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 20f;

            _cardsRow = UGuiKit.CreateCardRow(center, 24f);

            _root.SetActive(false);
        }

        private void Populate(List<SkillData> skills)
        {
            for (int i = _cardsRow.childCount - 1; i >= 0; i--) Destroy(_cardsRow.GetChild(i).gameObject);
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                BuildCard(skill);
            }
        }

        private void BuildCard(SkillData skill)
        {
            var tc = SkillTypeColor(skill.skillType);
            var card = UGuiKit.CreateCard(_cardsRow, new Vector2(230f, 320f), tc);

            var name = UGuiKit.CreateText(card, skill.skillName, 22, tc, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(name, 30f);
            var type = UGuiKit.CreateText(card, SkillTypeName(skill.skillType), 12, new Color(0.55f, 0.58f, 0.65f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(type, 20f);

            var desc = UGuiKit.CreateText(card, string.IsNullOrEmpty(skill.description) ? "" : skill.description, 13, new Color(0.72f, 0.74f, 0.80f), TextAlignmentOptions.Top);
            desc.enableWordWrapping = true;
            var dle = desc.gameObject.AddComponent<LayoutElement>(); dle.flexibleHeight = 1f; dle.minHeight = 40f;

            var stat = UGuiKit.CreateText(card, $"伤害 {skill.baseDamage:F0}  |  冷却 {skill.cooldown:F1}s", 12, new Color(0.78f, 0.80f, 0.85f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(stat, 20f);

            var pick = UGuiKit.CreateButton(card, "选择", () => Confirm(skill), new Color(tc.r * 0.4f, tc.g * 0.4f, tc.b * 0.4f, 0.95f), 16, new Vector2(190f, 38f));
            UGuiKit.SetHeight(pick.GetComponent<RectTransform>(), 38f);
        }

        private void Confirm(SkillData skill)
        {
            if (_root != null) _root.SetActive(false);
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
    }
}
