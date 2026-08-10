using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>把内容标记的平面候选点落到实体房地板，并验证角色胶囊净空。</summary>
    public static class DungeonSpawnSafety
    {
        public static bool TryFindGroundedPoint(
            Transform roomRoot,
            Vector3 planarCandidate,
            float radius,
            float height,
            float groundOffset,
            out Vector3 point)
        {
            point = default;
            if (roomRoot == null)
                return false;

            Vector3 origin = planarCandidate + Vector3.up * 50f;
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                100f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null
                    || !hit.collider.transform.IsChildOf(roomRoot)
                    || Vector3.Dot(hit.normal, Vector3.up) < 0.7f
                    || !IsFloorSurface(roomRoot, hit.collider))
                    continue;

                Vector3 ground = hit.point;
                if (!IsCapsuleClear(roomRoot, ground, radius, height, groundOffset))
                    continue;

                point = ground + Vector3.up * groundOffset;
                return true;
            }

            return false;
        }

        public static bool TryFindRandomGroundedPoint(
            Transform roomRoot,
            System.Random random,
            float radius,
            float height,
            float groundOffset,
            out Vector3 point)
        {
            point = default;
            if (roomRoot == null || random == null)
                return false;

            Bounds bounds = GetRoomBounds(roomRoot);
            float margin = Mathf.Min(
                Mathf.Min(bounds.size.x, bounds.size.z) * 0.15f,
                2f);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                Vector3 candidate = new(
                    Mathf.Lerp(
                        bounds.min.x + margin,
                        bounds.max.x - margin,
                        (float)random.NextDouble()),
                    bounds.max.y,
                    Mathf.Lerp(
                        bounds.min.z + margin,
                        bounds.max.z - margin,
                        (float)random.NextDouble()));
                if (TryFindGroundedPoint(
                        roomRoot,
                        candidate,
                        radius,
                        height,
                        groundOffset,
                        out point))
                    return true;
            }

            return false;
        }

        private static bool IsFloorSurface(Transform roomRoot, Collider collider)
        {
            Transform current = collider.transform;
            while (current != null && current != roomRoot)
            {
                if (current.name.StartsWith("Floor", StringComparison.OrdinalIgnoreCase))
                    return true;
                current = current.parent;
            }

            Bounds bounds = collider.bounds;
            float minHorizontal = Mathf.Min(bounds.size.x, bounds.size.z);
            return minHorizontal > 0.01f && bounds.size.y <= minHorizontal * 0.6f;
        }

        private static bool IsCapsuleClear(
            Transform roomRoot,
            Vector3 ground,
            float radius,
            float height,
            float groundOffset)
        {
            float safeRadius = Mathf.Max(0.1f, radius);
            float safeHeight = Mathf.Max(safeRadius * 2f, height);
            Vector3 bottom = ground + Vector3.up * (safeRadius + groundOffset);
            Vector3 top = ground + Vector3.up * (safeHeight - safeRadius + groundOffset);
            var overlaps = Physics.OverlapCapsule(
                bottom,
                top,
                safeRadius,
                ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (overlap != null && overlap.transform.IsChildOf(roomRoot))
                    return false;
            }

            return true;
        }

        private static Bounds GetRoomBounds(Transform roomRoot)
        {
            var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(roomRoot.position, new Vector3(10f, 3f, 10f));

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
