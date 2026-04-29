using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 技能修饰定义（GDD 6.5 方案 A：槽位限定触发）。
    /// 一个 SkillData 可挂多个 modifierDef；当**该技能下方槽位**中的灵物 modTag 与
    /// requiredTag 匹配且数量满足时，该 modifierDef 被激活，<see cref="SkillModifierApplier"/>
    /// 在技能命中后追加变体行为（如落点留地带、命中附加灼烧 / 冻结 / 雷击）。
    /// </summary>
    [System.Serializable]
    public class SkillModifierDef
    {
        [Header("匹配条件")]
        public string modifiedName = "";
        public ElementTag requiredTag = ElementTag.None;
        [Tooltip("槽位中要求的最小数量（默认 1，即槽内只要有 1 件即可）")]
        public int requiredCount = 1;

        [Header("AOE 落地区域")]
        public bool leaveZone = false;
        public float zoneRadius = 2.5f;
        public float zoneDuration = 3f;
        public float zoneTickInterval = 0.5f;
        [Tooltip("每次 tick 的伤害 = 玩家攻击 × zoneDamageMul")]
        public float zoneDamageMul = 0.15f;

        [Header("命中附加：灼烧（Fire）")]
        public bool addBurn = false;
        public float burnDPS = 4f;
        public float burnDuration = 3f;

        [Header("命中附加：冻结（Ice）")]
        public bool addFreeze = false;
        [Range(0f, 1f)] public float freezeChance = 1f;
        public float freezeDuration = 2f;

        [Header("命中附加：雷击（Thunder）")]
        public bool addThunderStrike = false;
        [Tooltip("额外雷击伤害 = 玩家攻击 × thunderMul")]
        public float thunderMul = 0.6f;
    }
}
