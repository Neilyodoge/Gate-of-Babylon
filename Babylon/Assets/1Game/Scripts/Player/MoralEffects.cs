using System.Collections.Generic;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 道心 / 因果效应（v0.5.5 · GDD §12.4）—— 把「抉择」真正变成局内后果。
    ///
    /// 监听 <see cref="PlayerStateHooks"/> 的道心 / 因果债变化，按阈值挂对应 StatusEffect：
    ///
    ///   ◇ 道心（0~100，机缘/剧情抉择改变）：
    ///       入定 ≥80   → 剑意通明：造成伤害 +10%
    ///       清明 50~79 → 无修正
    ///       心摇 20~49 → 心神不宁：受到伤害 +10%
    ///       入魔 <20   → 走火入魔：造成伤害 +25% · 受到伤害 +30%（高伤高险）
    ///
    ///   ◇ 因果债（行恶累积，行善为负，整局重置）：
    ///       善缘 <0    → 善缘庇佑：受到伤害 -5%
    ///       清白 0     → 无
    ///       业障 10~24 → 业障缠身：受到伤害 +10%
    ///       重业 ≥25   → 业火焚身：受到伤害 +20%
    ///
    /// 与心魔系统分工：心魔值由抉择「累积」并触发乱入（<see cref="InnerDemonMeter"/>）；
    /// 本组件负责道心/因果的「持续状态增减益」。两者都源于抉择，互补不重复。
    ///
    /// 属局内战斗效果，常驻挂载（不受洞府 meta 开关影响）。
    /// </summary>
    [RequireComponent(typeof(StatusEffectController))]
    public class MoralEffects : MonoBehaviour
    {
        private const string DaoHeartEffectId = "DaoHeartState";
        private const string KarmaEffectId = "KarmaDebtState";
        private const string LifespanEffectId = "LifespanState";

        private StatusEffectController _status;

        private void Awake()
        {
            _status = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            var hooks = PlayerStateHooks.Instance;
            hooks.OnDaoxinChanged += OnDaoxinChanged;
            hooks.OnKarmaChanged += OnKarmaChanged;
            hooks.OnLifespanChanged += OnLifespanChanged;
            // 初始按当前值刷新一次（新局重置后道心/因果为清明/清白 → 无效果；寿元按当前）
            RefreshDaoHeart(hooks.Daoxin);
            RefreshKarma(hooks.KarmaDebt);
            RefreshLifespan(hooks.Lifespan);
        }

        private void OnDisable()
        {
            var hooks = PlayerStateHooks.Instance;
            hooks.OnDaoxinChanged -= OnDaoxinChanged;
            hooks.OnKarmaChanged -= OnKarmaChanged;
            hooks.OnLifespanChanged -= OnLifespanChanged;
        }

        private void OnDaoxinChanged(int daoxin) => RefreshDaoHeart(daoxin);
        private void OnKarmaChanged(int karma) => RefreshKarma(karma);
        private void OnLifespanChanged(int lifespan) => RefreshLifespan(lifespan);

        // ==================== 道心 ====================

        private void RefreshDaoHeart(int daoxin)
        {
            if (_status == null) _status = GetComponent<StatusEffectController>();
            if (_status == null) return;

            _status.Remove(DaoHeartEffectId);

            var (attackPct, dmgRedFlat, label, desc, color, isBuff) = ResolveDaoHeart(daoxin);
            if (Mathf.Approximately(attackPct, 0f) && Mathf.Approximately(dmgRedFlat, 0f))
                return; // 清明：无修正

            var mods = new List<StatModifier>();
            if (!Mathf.Approximately(attackPct, 0f))
                mods.Add(StatModifier.Percent(StatType.AttackDamage, attackPct));
            if (!Mathf.Approximately(dmgRedFlat, 0f))
                mods.Add(StatModifier.Flat(StatType.DamageReduction, dmgRedFlat));

            _status.Apply(new StatusEffect
            {
                id = DaoHeartEffectId,
                isBuff = isBuff,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = mods,
                displayName = label,
                description = desc,
                uiColor = color
            });

            Debug.Log($"<color=#b0c8ff>[道心] {daoxin}（{label}）→ 攻击 {attackPct:+0%;-0%;0}、受伤减免 {dmgRedFlat:+0%;-0%;0}</color>");
        }

        /// <summary>道心 → (造成伤害乘区, 受伤减伤Flat, 标签, 描述, 颜色, 是否增益)。</summary>
        private static (float, float, string, string, Color, bool) ResolveDaoHeart(int daoxin)
        {
            // 道心试炼异象额外修正
            float trialAtk = 0f, trialDmgRed = 0f;
            if (RealmAnomalySystem.HasInstance)
            {
                trialAtk = RealmAnomalySystem.Instance.DaoTrialAtkBonus;
                trialDmgRed = RealmAnomalySystem.Instance.DaoTrialDmgRedPenalty;
            }

            if (daoxin >= 80) return (0.10f + trialAtk, 0f, "入定 · 剑意通明", "道心入定，气机通畅，攻势更利。", new Color(0.6f, 0.85f, 1f), true);
            if (daoxin >= 50) return (0f, 0f, "清明", "", Color.white, true);
            if (daoxin >= 20) return (0f, -0.10f, "心摇 · 心神不宁", "道心动摇，破绽渐生，受创更重。", new Color(1f, 0.75f, 0.4f), false);
            return (0.25f, -0.30f + trialDmgRed, "入魔 · 走火入魔", "道心崩坏，杀念暴涨——攻势凌厉却失了守御。", new Color(1f, 0.3f, 0.35f), false);
        }

        // ==================== 因果 ====================

        private void RefreshKarma(int karma)
        {
            if (_status == null) _status = GetComponent<StatusEffectController>();
            if (_status == null) return;

            _status.Remove(KarmaEffectId);

            var (dmgRedFlat, label, desc, color, isBuff) = ResolveKarma(karma);
            if (Mathf.Approximately(dmgRedFlat, 0f))
                return; // 清白/轻债：无修正

            _status.Apply(new StatusEffect
            {
                id = KarmaEffectId,
                isBuff = isBuff,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = new List<StatModifier> { StatModifier.Flat(StatType.DamageReduction, dmgRedFlat) },
                displayName = label,
                description = desc,
                uiColor = color
            });

            Debug.Log($"<color=#d8b0a0>[因果] 债 {karma}（{label}）→ 受伤减免 {dmgRedFlat:+0%;-0%;0}</color>");
        }

        /// <summary>因果债 → (受伤减伤Flat, 标签, 描述, 颜色, 是否增益)。</summary>
        private static (float, string, string, Color, bool) ResolveKarma(int karma)
        {
            if (karma < 0) return (0.05f, "善缘庇佑", "广结善缘，冥冥中有福泽庇身。", new Color(0.7f, 0.95f, 0.7f), true);
            if (karma < 10) return (0f, "", "", Color.white, true);
            if (karma < 25) return (-0.10f, "业障缠身", "因果缠身，灾劫渐近，受创更重。", new Color(0.85f, 0.6f, 0.5f), false);
            return (-0.20f, "业火焚身", "业债深重，业火加身，凶险倍增。", new Color(1f, 0.45f, 0.35f), false);
        }

        // ==================== 寿元（低寿元 → 衰朽减益；GDD 12.4.4） ====================

        private void RefreshLifespan(int lifespan)
        {
            if (_status == null) _status = GetComponent<StatusEffectController>();
            if (_status == null) return;

            _status.Remove(LifespanEffectId);

            var (atkPct, movePct, label, desc, color) = ResolveLifespan(lifespan);
            if (Mathf.Approximately(atkPct, 0f) && Mathf.Approximately(movePct, 0f)) return;

            _status.Apply(new StatusEffect
            {
                id = LifespanEffectId,
                isBuff = false,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, atkPct),
                    StatModifier.Percent(StatType.MoveSpeed, movePct),
                },
                displayName = label,
                description = desc,
                uiColor = color
            });

            Debug.Log($"<color=#c0b0a0>[寿元] 剩 {lifespan} 年（{label}）→ 攻击 {atkPct:+0%;-0%;0}、移速 {movePct:+0%;-0%;0}</color>");
        }

        /// <summary>寿元 → (攻击%, 移速%, 标签, 描述, 颜色)。寿元充裕无修正；将尽则衰朽。</summary>
        private static (float, float, string, string, Color) ResolveLifespan(int lifespan)
        {
            if (lifespan < 10) return (-0.25f, -0.20f, "油尽灯枯", "寿元将尽，气血枯竭，举步维艰。", new Color(0.7f, 0.55f, 0.5f));
            if (lifespan < 30) return (-0.10f, -0.10f, "寿元衰朽", "寿元渐薄，精力不济，攻势趋缓。", new Color(0.8f, 0.7f, 0.6f));
            return (0f, 0f, "", "", Color.white);
        }
    }
}
