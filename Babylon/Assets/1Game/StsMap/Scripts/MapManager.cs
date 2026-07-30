using UnityEngine;

namespace Map
{
    public class MapManager : MonoBehaviour
    {
        public MapConfig config;
        public MapView view;

        public Map CurrentMap { get; private set; }

        private void Start()
        {
            // 本项目按「每局重生成、不持久化」处理（去掉了 silverua 原生的 Newtonsoft/PlayerPrefs 存档）。
            // 由外部（SilveruaMapProvider / 游戏流程）在需要时调用 GenerateNewMap()，此处仅作缺省首生成。
            if (CurrentMap == null)
                GenerateNewMap();
        }

        public void GenerateNewMap()
        {
            Map map = MapGenerator.GetMap(config);
            CurrentMap = map;
            view.ShowMap(map);
        }

        // 保留空实现：MapPlayerTracker 会调用它。本项目不持久化地图。
        public void SaveMap() { }
    }
}
