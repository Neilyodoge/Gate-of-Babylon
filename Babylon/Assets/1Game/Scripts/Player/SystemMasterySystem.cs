using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 系精通运行逻辑（阶段C）：加点校验/分配（消耗精通点）+ 入秘境时应用当前化身已点节点。
    /// 数据存 <see cref="SaveDataV1"/>（masteryPoints / masteryNodeIds）；精通点由境界突破发放。
    /// </summary>
    public static class SystemMasterySystem
    {
        private static SaveDataV1 Data => SaveSystem.Instance.Data;

        public static bool IsAllocated(string nodeId) => Data.masteryNodeIds.Contains(nodeId);

        /// <summary>能否点亮该节点（校验：未点 / 灵力足 / 前置已点 / 亲和★足够）。</summary>
        public static bool CanAllocate(MasteryNode node, out string reason)
        {
            reason = null;
            if (node == null) { reason = "无效节点"; return false; }
            if (IsAllocated(node.id)) { reason = "已点亮"; return false; }
            if (InsightSystem.Instance.PermanentInsight < node.cost) { reason = "灵力不足"; return false; }
            if (!string.IsNullOrEmpty(node.prereqId) && !IsAllocated(node.prereqId)) { reason = "需先点前置"; return false; }
            if (SystemMasteryRegistry.AffinityStars(node.avatar, node.system) < node.affinityStarReq) { reason = "亲和不足"; return false; }
            return true;
        }

        /// <summary>点亮节点（扣灵力 + 持久化）。返回是否成功。</summary>
        public static bool Allocate(string nodeId)
        {
            var node = SystemMasteryRegistry.Get(nodeId);
            if (!CanAllocate(node, out _)) return false;
            if (!InsightSystem.Instance.SpendPermanentInsight(node.cost)) return false;
            Data.masteryNodeIds.Add(node.id);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#9cc0ff>[化身成长] 点亮 {node.displayName}（耗 {node.cost} 灵力，余 {InsightSystem.Instance.PermanentInsight}）</color>");
            return true;
        }

        /// <summary>入秘境：把当前化身已点的系精通节点作为常驻 buff 应用到玩家。</summary>
        public static void Apply(PlayerController player)
        {
            if (player == null) return;
            var rootCtrl = player.GetComponent<SpiritRootController>();
            if (rootCtrl == null) return;
            var avatar = rootCtrl.CurrentRoot;

            int applied = 0;
            foreach (var node in SystemMasteryRegistry.NodesFor(avatar))
            {
                if (!IsAllocated(node.id)) continue;
                try { node.apply?.Invoke(player); applied++; }
                catch (System.Exception e) { Debug.LogError($"[系精通] apply 失败 {node.id}: {e.Message}"); }
            }
            if (applied > 0)
                Debug.Log($"<color=#9cc0ff>[系精通] 应用 {applied} 个已点节点（{SystemMasteryRegistry.SystemName(SystemMasteryRegistry.BodySystem(avatar))}系）</color>");
        }
    }
}
