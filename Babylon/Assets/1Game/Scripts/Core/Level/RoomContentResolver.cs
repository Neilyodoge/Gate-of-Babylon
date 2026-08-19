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
            int seed,
            District? districtOverride = null,
            IReadOnlyList<string> roomTags = null)
        {
            var role = ToRole(roomType);
            var district = districtOverride ?? ResolveDistrict(roomIndex, roomCount);
            var candidates = new List<RoomContentRow>();
            int bestSpecificity = -1;
            foreach (var pair in ConfigDatabase.Instance.RoomContents)
            {
                var row = pair.Value;
                if (row.RoleEnum == role
                    && row.DistrictEnum == district
                    && roomIndex >= row.MinGraphDepth
                    && roomIndex <= row.MaxGraphDepth
                    && TryMatchTags(row.PrefabTags, roomTags, out int specificity))
                {
                    if (specificity > bestSpecificity)
                    {
                        candidates.Clear();
                        bestSpecificity = specificity;
                    }
                    if (specificity == bestSpecificity)
                        candidates.Add(row);
                }
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException(
                    $"找不到同分区房间内容配置：Room={roomIndex}, Role={role}, " +
                    $"District={district}, Tags=[{string.Join(",", roomTags ?? Array.Empty<string>())}], " +
                    $"Seed={seed}。禁止跨区或忽略标签回退，请补齐对应配置。");

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

        public static bool TryMatchTags(
            IReadOnlyList<string> requiredTags,
            IReadOnlyList<string> roomTags,
            out int specificity)
        {
            specificity = 0;
            if (requiredTags == null || requiredTags.Count == 0)
                return true;

            var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roomTags != null)
            {
                foreach (string tag in roomTags)
                {
                    string canonical = NormalizeTag(tag);
                    if (!string.IsNullOrEmpty(canonical))
                        available.Add(canonical);
                }
            }

            foreach (string rawTag in requiredTags)
            {
                string required = NormalizeTag(rawTag);
                if (string.IsNullOrEmpty(required))
                    continue;
                specificity++;
                if (!available.Contains(required))
                    return false;
            }
            return true;
        }

        public static List<string> MergeNormalizedTags(
            params IReadOnlyList<string>[] sources)
        {
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sources == null)
                return result;
            foreach (IReadOnlyList<string> source in sources)
            {
                if (source == null)
                    continue;
                foreach (string rawTag in source)
                {
                    string tag = NormalizeTag(rawTag);
                    if (!string.IsNullOrEmpty(tag) && unique.Add(tag))
                        result.Add(tag);
                }
            }
            return result;
        }

        public static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;
            return tag.Trim() switch
            {
                "战斗" => "Combat",
                "精英" => "Elite",
                "首领" => "Boss",
                "事件" => "Event",
                "商店" => "Shop",
                "降落" => "Landing",
                // 分区已由 District 强约束；旧表中的这些值不是额外内容条件。
                "外环" => null,
                "连接区" => null,
                "内环" => null,
                // 旧事件表的展示标签，现有房间模板没有对应结构语义。
                "实体交互" => null,
                _ => tag.Trim(),
            };
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
                RoomType.Landing => RoomRole.Landing,
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
