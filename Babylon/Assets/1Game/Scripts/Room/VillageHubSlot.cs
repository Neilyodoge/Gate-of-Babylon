using UnityEngine;

namespace XianTu
{
    public enum VillageHubSlotType
    {
        PlayerSpawn = 0,
        RealmPortal = 1,
        PreparationStation = 2,
        CaveStation = 3,
        MapTablet = 4,
    }

    /// <summary>基地白盒与正式美术 Prefab 共用的功能接线点。</summary>
    public sealed class VillageHubSlot : MonoBehaviour
    {
        [SerializeField, InspectorName("基地功能点")]
        private VillageHubSlotType slotType;

        public VillageHubSlotType SlotType => slotType;

        public void Configure(VillageHubSlotType type)
        {
            slotType = type;
            name = $"Slot_{type}";
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = slotType switch
            {
                VillageHubSlotType.PlayerSpawn => Color.cyan,
                VillageHubSlotType.RealmPortal => new Color(0.55f, 0.3f, 1f),
                VillageHubSlotType.PreparationStation => Color.yellow,
                VillageHubSlotType.CaveStation => Color.magenta,
                VillageHubSlotType.MapTablet => Color.green,
                _ => Color.white,
            };
            Gizmos.DrawWireSphere(transform.position, 0.45f);
        }
    }
}
