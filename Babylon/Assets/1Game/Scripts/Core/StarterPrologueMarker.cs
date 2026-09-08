using UnityEngine;

namespace XianTu
{
    public enum StarterPrologueMarkerKind
    {
        PlayerSpawn = 0,
        HomeOutskirts = 1,
        RescueClearing = 2,
        PossessionClearing = 3,
        HomeReturn = 4,
        RescueResolved = 5,
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
