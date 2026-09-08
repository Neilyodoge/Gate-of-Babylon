using UnityEngine;

namespace XianTu
{
    public enum StarterPrologueMarkerKind
    {
        PlayerSpawn = 0,
        DailyCourtyard = 1,
        BondingAltar = 2,
        TrainingArena = 3,
        CaveGate = 4,
    }

    /// <summary>白盒Prefab中的稳定流程标记，避免场景逻辑依赖物体名称。</summary>
    public sealed class StarterPrologueMarker : MonoBehaviour
    {
        [SerializeField] private StarterPrologueMarkerKind kind;
        public StarterPrologueMarkerKind Kind => kind;

        public void Configure(StarterPrologueMarkerKind value)
        {
            kind = value;
        }
    }
}
