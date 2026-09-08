using UnityEngine;

namespace XianTu
{
    public enum StarterPrologueEnemyRole
    {
        Melee = 0,
        Ranged = 1,
    }

    /// <summary>新手验证战的稳定敌人出生位。</summary>
    public sealed class StarterPrologueEnemySpawnMarker : MonoBehaviour
    {
        [SerializeField] private StarterPrologueEnemyRole role;
        public StarterPrologueEnemyRole Role => role;

        public void Configure(StarterPrologueEnemyRole value)
        {
            role = value;
        }
    }
}
