using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 境界压制（v0.5.4）—— 比较玩家【本体境界】与【当前秘境层环境境界】，挂减益 / 加成。
    ///
    /// 秘境层深度 index（GameManager 的 _currentLevel，0~5）即该层"环境境界"品级：
    ///   第 1 层 = 练气级(0) … 第 6 层 = 渡劫级(5)。
    ///
    /// delta = 本体境界 - 环境境界：
    ///   ≥ +2  高屋建瓴：造成伤害 +5%
    ///   +1/0  契合：     正常
    ///   -1    越级：     造成伤害 -20% · 受到伤害 +25%
    ///   -2    险境：     造成伤害 -40% · 受到伤害 +50%
    ///   ≤ -3  九死一生： 造成伤害 -60% · 受到伤害 +100%
    ///
    /// 监听 <see cref="GameEvents.RealmBreakthrough"/>（进入新秘境层时由 GameManager 发布）刷新。
    /// 减益是 StatusEffect，不是硬锁——低境界仍可强闯深层（贪），但极险。
    /// </summary>
    public class CultivationSuppression : MonoBehaviour
    {
        private const string EffectId = "JingjieSuppression";

        private StatusEffectController _status;

        private void Awake()
        {
            _status = GetComponent<StatusEffectController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnEnterLayer);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnEnterLayer);
        }

        /// <summary>进入新秘境层（GameManager 复用 RealmBreakthrough 事件携带层深度）。</summary>
        private void OnEnterLayer(GameEvents.RealmBreakthrough evt)
        {
            Refresh(evt.NewRealmLevel);
        }

        private void Refresh(int envRealm)
        {
            if (_status == null) _status = GetComponent<StatusEffectController>();
            if (_status == null) return;

            int delta = CultivationSystem.Instance.SuppressionDelta(envRealm);

            // 先移除旧的压制效果，再按当前 delta 重挂
            _status.Remove(EffectId);

            var (dmgDealtMul, dmgTakenFlat, label, color) = Resolve(delta);
            if (Mathf.Approximately(dmgDealtMul, 0f) && Mathf.Approximately(dmgTakenFlat, 0f))
                return; // 契合：无修正，不挂任何效果

            var mods = new List<StatModifier>();
            if (!Mathf.Approximately(dmgDealtMul, 0f))
                mods.Add(StatModifier.Percent(StatType.AttackDamage, dmgDealtMul));
            if (!Mathf.Approximately(dmgTakenFlat, 0f))
                mods.Add(StatModifier.Flat(StatType.DamageReduction, dmgTakenFlat)); // 负值 = 受伤更多

            _status.Apply(new StatusEffect
            {
                id = EffectId,
                isBuff = delta >= 0,
                elementTag = ElementTag.None,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = -1f,
                duration = -1f,
                modifiers = mods,
                displayName = label,
                description = delta >= 0
                    ? "本体境界高于此地，气机通畅"
                    : $"越级 {-delta} 阶 · 受境界压制",
                uiColor = color
            });

            Debug.Log($"<color=#c8b0ff>[境界压制] 环境境界 {envRealm} vs 本体 {CultivationSystem.Instance.CurrentRealm} → delta {delta}（{label}）</color>");
        }

        /// <summary>delta → (造成伤害乘区, 受伤减伤Flat, 标签, 颜色)。</summary>
        private static (float, float, string, Color) Resolve(int delta)
        {
            if (delta >= 2) return (0.05f, 0f, "高屋建瓴", new Color(1f, 0.9f, 0.5f));
            if (delta >= 0) return (0f, 0f, "契合", Color.white);
            if (delta == -1) return (-0.20f, -0.25f, "越级压制", new Color(1f, 0.6f, 0.4f));
            if (delta == -2) return (-0.40f, -0.50f, "险境压制", new Color(1f, 0.4f, 0.3f));
            return (-0.60f, -1.00f, "九死一生", new Color(1f, 0.2f, 0.25f));
        }
    }
}
