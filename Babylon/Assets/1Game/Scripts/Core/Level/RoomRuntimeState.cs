using System;

namespace XianTu
{
    public enum RoomRuntimePhase
    {
        Dormant = 0,
        Armed = 1,
        Active = 2,
        Completed = 3
    }

    [Serializable]
    public sealed class RoomRuntimeState
    {
        public int RoomIndex;
        public int ContentID;
        public int EncounterSeed;
        public int SelectedBossID;
        public RoomRuntimePhase Phase;
        public bool DoorsLocked;
        public bool Cleared;
        public bool MaterialsSpawned;
    }
}
