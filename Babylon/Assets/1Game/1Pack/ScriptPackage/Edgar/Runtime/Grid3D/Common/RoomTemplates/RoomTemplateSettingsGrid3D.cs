using System.Collections.Generic;
using Edgar.Geometry;
using Edgar.GraphBasedGenerator.Common;
using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Component that is attached to each room template game objects and contains basic settings.
    /// </summary>
    [AddComponentMenu("仙途秘境/Edgar Grid3D/房间模板设置")]
    public class RoomTemplateSettingsGrid3D : MonoBehaviour
    {
        /// <summary>
        /// Controls how the outline is computed.
        /// </summary>
        /// <seealso cref="RoomTemplateRepeatMode"/>
        [InspectorName("轮廓来源")]
        public RoomTemplateOutlineModeGrid3D OutlineMode = RoomTemplateOutlineModeGrid3D.FromColliders;

        /// <summary>
        /// Whether the room template can be used multiple times in generated levels.
        /// </summary>
        [InspectorName("重复使用模式")]
        public RoomTemplateRepeatMode RepeatMode = RoomTemplateRepeatMode.AllowRepeat;

        /// <summary>
        /// Whether the room template can appear rotates in generated levels.
        /// </summary>
        [InspectorName("允许旋转")]
        public bool AllowRotation = true;

        [InspectorName("生成器设置")]
        public GeneratorSettingsGrid3D GeneratorSettings;

        /// <summary>
        /// Outline of the room template that is used when the computation mode is set to InsideEditor.
        /// </summary>
        [HideInInspector]
        public List<SerializableVector3Int> Outline;

        /// <summary>
        /// Computes the outline of the room template.
        /// </summary>
        /// <returns>Returns null if outline is not valid.</returns>
        public PolygonGrid2D ComputeOutline()
        {
            try
            {
                return RoomTemplateLoaderGrid3D.GetOutline(gameObject);
            }
            catch (InvalidOutlineException)
            {
                return null;
            }
        }

//        public void OnBeforeSerialize()
//        {
//#if UNITY_EDITOR
//            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

//            if (EditorUtility.IsDirty(this) && prefabStage != null)
//            {
//                var root = prefabStage.prefabContentsRoot;
//                var anotherPrefabStage = PrefabStageUtility.GetPrefabStage(prefabStage.prefabContentsRoot);
//                var anotherPrefabStage2 = PrefabStageUtility.GetPrefabStage(gameObject);
//                var isPart = prefabStage.IsPartOfPrefabContents(gameObject);
//                var go = gameObject;
//                var equal = go == root;
//                var partOfPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(this);

//                var outline = GetOutline();
//                var newRandom = Random.Range(0, 1000);
//                Debug.Log($"Cache outline, {go.name}, {EditorUtility.IsDirty(this)}, {random}, {newRandom}, {string.Join(", ", outline.GetPoints())}");
//                random = newRandom;
//            }
            
//            //if (PrefabStageUtility.GetCurrentPrefabStage() == null)
//            //{
//            //    Debug.Log("Cache outline");
//            //    var outline = GetOutline();
//            //    //cachedOutline = outline
//            //    //    .GetPoints()
//            //    //    .Select(x => new SerializableVector3Int(x.X, x.Y, 0))
//            //    //    .ToList();
//            //}
//#endif
//        }
    }
}