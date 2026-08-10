using Edgar.Geometry;
using Edgar.GraphBasedGenerator.Grid2D;
using Edgar.GraphBasedGenerator.Grid2D.Exceptions;
using UnityEngine;

namespace Edgar.Unity.Diagnostics
{
    public static class RoomTemplateDiagnosticsGrid3D
    {
        /// <summary>
        /// Tries to compute a room template from a given game object and returns the result.
        /// </summary>
        /// <param name="roomTemplate"></param>
        /// <returns></returns>
        public static ActionResult CheckAll(GameObject roomTemplate)
        {
            RoomTemplateLoaderGrid3D.TryGetRoomTemplate(roomTemplate, null, true, false, out var _, out var result);
            return result;
        }

        /// <summary>
        /// Checks that the room template has all the necessary components.
        /// </summary>
        /// <param name="roomTemplate"></param>
        /// <returns></returns>
        public static ActionResult CheckComponents(GameObject roomTemplate)
        {
            var result = new ActionResult();

            var roomTemplateSettings = roomTemplate.GetComponent<RoomTemplateSettingsGrid3D>();
            if (roomTemplateSettings == null)
            {
                result.AddError($"房间模板根对象缺少 {nameof(RoomTemplateSettingsGrid3D)} 组件。");
            }
            else
            {
                if (roomTemplateSettings.GeneratorSettings == null)
                {
                    result.AddError($"必须指定 {nameof(RoomTemplateSettingsGrid3D.GeneratorSettings)} 字段。");
                }
            }

            return result;
        }

        /// <summary>
        /// Checks the doors of the room template.
        /// </summary>
        /// <param name="outline"></param>
        /// <param name="doorMode"></param>
        /// <returns></returns>
        public static ActionResult CheckDoors(PolygonGrid2D outline, IDoorModeGrid2D doorMode)
        {
            var result = new ActionResult();

            try
            {
                var doors = doorMode.GetDoors(outline);

                if (doors.Count == 0)
                {
                    result.AddError("房间模板没有门。");
                }
            }
            catch (DoorLineOutsideOfOutlineException)
            {
                result.AddError("部分门不在房间模板轮廓上，或门的旋转方向不正确。");
            }
            catch (DuplicateDoorPositionException)
            {
                result.AddError("存在门长和 Socket 相同的重复或重叠门线。");
            }

            return result;
        }

        /// <summary>
        /// Checks the doors of the room template.
        /// </summary>
        /// <param name="roomTemplate"></param>
        /// <returns></returns>
        public static ActionResult CheckDoors(GameObject roomTemplate)
        {
            var roomTemplateSettings = roomTemplate.GetComponent<RoomTemplateSettingsGrid3D>();
            var outline = roomTemplateSettings.ComputeOutline();

            if (!RoomTemplateLoaderGrid3D.TryGetDoors(roomTemplate, outline, out var doorLoadingResult))
            {
                return doorLoadingResult.ActionResult;
            }

            return CheckDoors(outline, doorLoadingResult.DoorMode);
        }
    }
}