using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace XianTu.LevelDesign
{
    /// <summary>同一事件房 Prefab 内按 EventID 启用对应的白膜内容。</summary>
    [AddComponentMenu("仙途秘境/关卡/事件内容变体根")]
    public sealed class DungeonEventVariantRoot : MonoBehaviour
    {
        [SerializeField, InspectorName("事件编号")]
        private int eventID;

        public int EventID => eventID;

        public void Configure(int id)
        {
            eventID = id;
        }

        public static void ActivateOnly(Transform contentRoot, int activeEventID)
        {
            SetActiveEvents(contentRoot, new[] { activeEventID });
        }

        public static void SetActiveEvents(
            Transform contentRoot,
            IReadOnlyCollection<int> activeEventIDs)
        {
            if (contentRoot == null)
                return;
            foreach (var variant in contentRoot.GetComponentsInChildren<DungeonEventVariantRoot>(true))
                variant.gameObject.SetActive(
                    activeEventIDs != null && activeEventIDs.Contains(variant.eventID));
        }
    }
}
