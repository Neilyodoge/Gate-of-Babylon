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
            if (dungeonRoot == null)
                return null;

            var runtime = dungeonRoot.GetComponent<DungeonNavMeshRuntime>();
            if (runtime == null)
                runtime = dungeonRoot.AddComponent<DungeonNavMeshRuntime>();
            runtime.Build();
            return runtime;
        }

        public void Build()
        {
            _surface = GetComponent<NavMeshSurface>();
            if (_surface == null)
                _surface = gameObject.AddComponent<NavMeshSurface>();

            _surface.collectObjects = CollectObjects.Children;
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
