using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace XianTu
{
    /// <summary>
    /// Edgar 地牢完成缩放和旋转后，在整张生成结果上构建运行时 NavMesh。
    /// </summary>
    public sealed class DungeonNavMeshRuntime : MonoBehaviour
    {
        private NavMeshSurface _surface;

        public bool IsBuilt { get; private set; }

        public static DungeonNavMeshRuntime BuildFor(GameObject dungeonRoot)
        {
            return BuildFor(dungeonRoot, null);
        }

        /// <summary>
        /// <paramref name="worldVolume"/> 用于手工搭建的场景：那里的地形和环境碰撞体
        /// 不在玩法根之下，只能按世界空间体积收集。
        /// </summary>
        public static DungeonNavMeshRuntime BuildFor(
            GameObject dungeonRoot,
            Bounds? worldVolume)
        {
            if (dungeonRoot == null)
                return null;

            var runtime = dungeonRoot.GetComponent<DungeonNavMeshRuntime>();
            if (runtime == null)
                runtime = dungeonRoot.AddComponent<DungeonNavMeshRuntime>();
            runtime.Build(worldVolume);
            return runtime;
        }

        public void Build()
        {
            Build(null);
        }

        public void Build(Bounds? worldVolume)
        {
            _surface = GetComponent<NavMeshSurface>();
            if (_surface == null)
                _surface = gameObject.AddComponent<NavMeshSurface>();

            if (worldVolume.HasValue)
            {
                Bounds volume = worldVolume.Value;
                Vector3 scale = transform.lossyScale;
                _surface.collectObjects = CollectObjects.Volume;
                _surface.center =
                    transform.InverseTransformPoint(volume.center);
                _surface.size = new Vector3(
                    volume.size.x / Mathf.Max(0.0001f, scale.x),
                    volume.size.y / Mathf.Max(0.0001f, scale.y),
                    volume.size.z / Mathf.Max(0.0001f, scale.z));
            }
            else
            {
                _surface.collectObjects = CollectObjects.Children;
            }

            _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _surface.layerMask = ~0;
            if (_surface.navMeshData != null)
                _surface.RemoveData();
            _surface.BuildNavMesh();
            IsBuilt = _surface.navMeshData != null;

            if (!IsBuilt)
                Debug.LogError("[NavMesh] Edgar 地牢运行时 NavMesh 构建失败。");
            else
                Debug.Log($"<color=#66ff99>[NavMesh] 地牢导航构建完成：{name}</color>");
        }

        private void OnDestroy()
        {
            if (_surface != null)
                _surface.RemoveData();
            IsBuilt = false;
        }
    }
}
