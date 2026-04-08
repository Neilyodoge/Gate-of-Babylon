using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 游戏管理器 —— 控制整局游戏流程
    /// Demo1: 线性推进 5 层（练气→筑基→金丹→元婴→化神），每层一个战斗房间
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("境界配置")]
        private readonly string[] _realmNames = { "练气期", "筑基期", "金丹期", "元婴期", "化神期", "渡劫期" };

        [Header("难度曲线")]
        [SerializeField] private int baseEnemyCount = 3;
        [SerializeField] private int enemyCountPerLevel = 2;
        [SerializeField] private float hpScalePerLevel = 0.3f;
        [SerializeField] private float dmgScalePerLevel = 0.2f;

        [Header("灵物池（在 Inspector 中配置）")]
        [SerializeField] private ItemData[] itemPool;

        [Header("敌人受击特效")]
        [SerializeField] private GameObject enemyHitVFXPrefab;

        [Header("引用")]
        [SerializeField] private Transform roomSpawnPoint;

        // 游戏状态
        private int _currentLevel; // 当前层数（0-5）
        private BattleRoom _currentRoom;
        private bool _gameOver;

        public int CurrentLevel => _currentLevel;
        public string CurrentRealmName => _currentLevel < _realmNames.Length ? _realmNames[_currentLevel] : "飞升";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);

            StartNewRun();
        }

        /// <summary>开始新的一局</summary>
        public void StartNewRun()
        {
            _currentLevel = 0;
            _gameOver = false;

            Debug.Log("<color=magenta>═══════════════════════════</color>");
            Debug.Log("<color=magenta>  入梦... 仙途梦境开始</color>");
            Debug.Log("<color=magenta>═══════════════════════════</color>");

            SpawnRoom();
        }

        /// <summary>生成当前层的战斗房间</summary>
        private void SpawnRoom()
        {
            if (_currentRoom != null)
                Destroy(_currentRoom.gameObject);

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            var roomGo = new GameObject($"BattleRoom_Lv{_currentLevel}_{CurrentRealmName}");
            roomGo.transform.position = spawnPos;
            _currentRoom = roomGo.AddComponent<BattleRoom>();

            int enemyCount = baseEnemyCount + _currentLevel * enemyCountPerLevel;
            float hpMul = 1f + _currentLevel * hpScalePerLevel;
            float dmgMul = 1f + _currentLevel * dmgScalePerLevel;

            _currentRoom.Initialize(_currentLevel, enemyCount, hpMul, dmgMul, itemPool);

            // 传递打击特效给战斗房间
            if (enemyHitVFXPrefab != null)
                _currentRoom.SetEnemyHitVFX(enemyHitVFXPrefab);

            // 发布境界信息
            GameEvents.Publish(new GameEvents.RealmBreakthrough
            {
                NewRealmLevel = _currentLevel,
                RealmName = CurrentRealmName
            });

            Debug.Log($"<color=yellow>【{CurrentRealmName}】第 {_currentLevel + 1} 层 | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1}</color>");

            _currentRoom.StartBattle();
        }

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            if (_gameOver) return;

            _currentLevel++;

            if (_currentLevel >= _realmNames.Length)
            {
                // 通关！
                Debug.Log("<color=yellow>✨✨✨ 渡劫成功！飞升成仙！✨✨✨</color>");
                _gameOver = true;
                // TODO: 通关界面、感悟结算
                return;
            }

            // 短暂延迟后进入下一层
            Invoke(nameof(SpawnRoom), 2f);
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            _gameOver = true;
            Debug.Log("<color=red>梦境破碎... 惊醒回到现实</color>");
            // TODO: 死亡结算界面、感悟保留
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
        }

        /// <summary>
        /// 重新开始（UI 按钮调用）
        /// </summary>
        public void Restart()
        {
            // 清理
            if (_currentRoom != null)
                Destroy(_currentRoom.gameObject);

            // 清空灵物
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.Inventory.Clear();
                PlayerController.Instance.Stats.ResetHp();
            }

            GameEvents.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
