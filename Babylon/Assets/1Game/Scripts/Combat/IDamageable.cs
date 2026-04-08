using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 可受伤害的接口，所有可被攻击的实体实现此接口
    /// </summary>
    public interface IDamageable
    {
        CombatStats Stats { get; }
        void OnDamage(float damage, Vector3 hitPoint, GameObject attacker);
        void OnDeath();
    }
}
