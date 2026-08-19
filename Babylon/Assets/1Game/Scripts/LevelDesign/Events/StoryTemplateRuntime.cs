using System;

namespace XianTu.LevelDesign
{
    public enum StoryTemplateKind
    {
        Route = 0,
        Facility = 1,
        Hazard = 2,
    }

    public sealed class StoryTemplateDefinition
    {
        public StoryTemplateKind Kind;
        public string ID;
        public string DisplayName;
        public int MandatoryEventID;
        public string TemplateFlag;
    }

    /// <summary>
    /// 关卡 A 的 MVP 故事模板入口。事件按昼夜分配，Boss 结果改由机制 Flag 消费。
    /// </summary>
    public static class StoryTemplateRuntime
    {
        private static readonly StoryTemplateDefinition[] Templates =
        {
            new()
            {
                Kind = StoryTemplateKind.Route,
                ID = "Story_昼夜机关",
                DisplayName = "昼夜机关",
                MandatoryEventID = 1004,
                TemplateFlag = "story_template_day_night_devices",
            },
        };

        public static StoryTemplateDefinition Current { get; private set; }

        public static StoryTemplateDefinition SelectForAct(int actID, int runSeed)
        {
            Current = null;
            if (actID != 1)
                return null;

            Current = Templates[0];
            BossFlagSet.Instance.Set(Current.TemplateFlag, 1);
            return Current;
        }

        public static void Clear() => Current = null;

        public static bool ApplyBossModifier(
            ref float hp,
            ref float attack,
            ref float speed,
            out string resultName)
        {
            resultName = null;
            return false;
        }

        public static StoryTemplateDefinition GetByMandatoryEvent(int eventID)
        {
            return Array.Find(Templates, template => template.MandatoryEventID == eventID);
        }
    }
}
