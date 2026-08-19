using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 围攻协调器：限制近战敌人同时出手数量。
    /// 以目标为分组键，因此玩家与水镜分身可分别协调。
    /// </summary>
    public static class EnemyCombatCoordinator
    {
        private sealed class TargetGroup
        {
            public Transform Target;
            public readonly HashSet<int> Attackers = new();
        }

        private static readonly Dictionary<int, TargetGroup> Groups = new();

        public static bool TryAcquireAttackToken(
            GameObject owner,
            Transform target,
            int maxConcurrentAttackers)
        {
            if (owner == null || target == null)
                return false;

            TargetGroup group = GetGroup(target);
            int ownerID = owner.GetInstanceID();
            if (group.Attackers.Contains(ownerID))
                return true;
            if (group.Attackers.Count >= Mathf.Max(1, maxConcurrentAttackers))
                return false;

            group.Attackers.Add(ownerID);
            return true;
        }

        public static void ReleaseAttackToken(GameObject owner, Transform target)
        {
            if (owner == null || target == null)
                return;
            if (Groups.TryGetValue(target.GetInstanceID(), out TargetGroup group))
                group.Attackers.Remove(owner.GetInstanceID());
        }

        public static void Unregister(GameObject owner)
        {
            if (owner == null)
                return;

            int ownerID = owner.GetInstanceID();
            foreach (TargetGroup group in Groups.Values)
                group.Attackers.Remove(ownerID);
        }

        public static void Clear()
        {
            Groups.Clear();
        }

        private static TargetGroup GetGroup(Transform target)
        {
            PruneDestroyedTargets();
            int targetID = target.GetInstanceID();
            if (!Groups.TryGetValue(targetID, out TargetGroup group))
            {
                group = new TargetGroup { Target = target };
                Groups.Add(targetID, group);
            }
            return group;
        }

        private static void PruneDestroyedTargets()
        {
            List<int> stale = null;
            foreach (KeyValuePair<int, TargetGroup> pair in Groups)
            {
                if (pair.Value.Target != null)
                    continue;
                stale ??= new List<int>();
                stale.Add(pair.Key);
            }

            if (stale == null)
                return;
            foreach (int targetID in stale)
                Groups.Remove(targetID);
        }
    }
}
