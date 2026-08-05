using System;
using System.Collections.Generic;
using XianTu.LevelDesign;

namespace XianTu
{
    public static class RoomContentResolver
    {
        public static RoomContentRow Resolve(
            RoomType roomType,
            int roomIndex,
            int roomCount,
            int seed)
        {
            var role = ToRole(roomType);
            var district = ResolveDistrict(roomIndex, roomCount);
            var candidates = new List<RoomContentRow>();
            foreach (var pair in ConfigDatabase.Instance.RoomContents)
            {
                var row = pair.Value;
                if (row.RoleEnum == role
                    && row.DistrictEnum == district
                    && roomIndex >= row.MinGraphDepth
                    && roomIndex <= row.MaxGraphDepth)
                    candidates.Add(row);
            }

            if (candidates.Count == 0)
            {
                foreach (var pair in ConfigDatabase.Instance.RoomContents)
                {
                    var row = pair.Value;
                    if (row.RoleEnum == role
                        && roomIndex >= row.MinGraphDepth
                        && roomIndex <= row.MaxGraphDepth)
                        candidates.Add(row);
                }
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException(
                    $"找不到房间内容配置：Room={roomIndex}, Role={role}, District={district}, Seed={seed}。");

            candidates.Sort((a, b) => a.ID.CompareTo(b.ID));
            int totalWeight = 0;
            foreach (var row in candidates)
                totalWeight += Math.Max(0, row.Weight);
            if (totalWeight <= 0)
                throw new InvalidOperationException(
                    $"房间内容候选权重均为 0：Room={roomIndex}, Role={role}, Seed={seed}。");

            var random = new Random(seed);
            int roll = random.Next(totalWeight);
            foreach (var row in candidates)
            {
                roll -= Math.Max(0, row.Weight);
                if (roll < 0) return row;
            }
            return candidates[candidates.Count - 1];
        }

        private static RoomRole ToRole(RoomType roomType)
        {
            return roomType switch
            {
                RoomType.Battle => RoomRole.Battle,
                RoomType.Elite => RoomRole.Elite,
                RoomType.Event => RoomRole.Event,
                RoomType.Shop => RoomRole.Shop,
                RoomType.Rest => RoomRole.Rest,
                RoomType.Boss => RoomRole.Boss,
                RoomType.Treasure => RoomRole.Armory,
                RoomType.Upgrade => RoomRole.Armory,
                _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null)
            };
        }

        private static District ResolveDistrict(int roomIndex, int roomCount)
        {
            int count = Math.Max(1, roomCount);
            if (roomIndex < count / 3) return District.Outer;
            if (roomIndex < count * 2 / 3) return District.Transition;
            return District.Inner;
        }
    }
}
