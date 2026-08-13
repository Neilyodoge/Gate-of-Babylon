using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    public enum EventSceneObjectAction
    {
        Enable = 0,
        Disable = 1,
    }

    /// <summary>正式房间可放置此标记，让事件选择直接启用或移除对应场景对象。</summary>
    [AddComponentMenu("仙途秘境/关卡/事件场景对象")]
    public sealed class DungeonEventSceneObject : MonoBehaviour
    {
        [SerializeField, InspectorName("响应的场景结果")]
        private EventSceneResult result;
        [SerializeField, InspectorName("触发后的动作")]
        private EventSceneObjectAction action;
        [SerializeField, InspectorName("响应的昼夜阶段")]
        private LevelPhaseMask allowedPhases = LevelPhaseMask.Both;

        public void Configure(EventSceneResult targetResult, EventSceneObjectAction targetAction)
        {
            Configure(targetResult, targetAction, LevelPhaseMask.Both);
        }

        public void Configure(
            EventSceneResult targetResult,
            EventSceneObjectAction targetAction,
            LevelPhaseMask phases)
        {
            result = targetResult;
            action = targetAction;
            allowedPhases = phases;
        }

        public bool Apply(EventSceneResult selected)
        {
            if (selected == EventSceneResult.None || result != selected)
                return false;
            LevelPhaseMask currentPhase = LevelAPhaseRuntime.IsNightMapActive
                ? LevelPhaseMask.Night
                : LevelPhaseMask.Day;
            if ((allowedPhases & currentPhase) == 0)
                return false;

            gameObject.SetActive(action == EventSceneObjectAction.Enable);
            return true;
        }
    }
}
