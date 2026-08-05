using System;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    public readonly struct RoomContentContext
    {
        public readonly RoomType RoomType;
        public readonly RoomContentRow Content;
        public readonly RoomSpawnContext Spawn;
        public readonly RoomRuntimeState State;
        public readonly int EncounterSeed;
        public readonly Action OnCompleted;

        public RoomContentContext(
            RoomType roomType,
            RoomContentRow content,
            in RoomSpawnContext spawn,
            RoomRuntimeState state,
            int encounterSeed,
            Action onCompleted)
        {
            RoomType = roomType;
            Content = content;
            Spawn = spawn;
            State = state;
            EncounterSeed = encounterSeed;
            OnCompleted = onCompleted;
        }

        public Transform ContentRoot => Spawn.contentRoot;
    }

    public interface IRoomContentHandler
    {
        void Initialize(in RoomContentContext context);
        void Arm();
        void Activate();
        void RestoreCompleted();
    }
}
