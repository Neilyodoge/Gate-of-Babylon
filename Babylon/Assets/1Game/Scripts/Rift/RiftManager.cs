using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境管理器（Phase3）。
    ///
    /// 流程：村庄大秘境入口 → 缓冲区（装备 Build）→ 计时挑战间（清怪→Boss）→ 奖励 → 回村。
    /// 大秘境是「局外」挑战：装备局内带出的 Build 验证数值，独立于常规 3 层肉鸽循环。
    ///
    /// GDD §11.4.1：需有局内 Build 才可进入；缓冲区装备 Build 后开始挑战。
    /// GDD Q-008：计时模式——限时清完指定数量怪物后出现 Boss（参考暗黑 3 大秘境）。
    /// GDD Q-009：奖励先搭框架，实际产出后续书面讨论。
    /// </summary>
    public class RiftManager : MonoBehaviour
    {
        private static RiftManager _instance;
        public static RiftManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RiftManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<RiftManager>();
                }
                return _instance;
            }
        }

        private GameObject _currentRoomGo;
        private BuildSnapshot _equippedBuild;

        /// <summary>当前大秘境层数（每成功一次 +1，用于难度缩放）。</summary>
        public int RiftTier { get; private set; } = 1;

        // ==================== 缓冲区 ====================

        /// <summary>进入大秘境缓冲区：构建房间 + 弹出 Build 装备 UI。</summary>
        public void EnterBuffer()
        {
            DestroyRoom();

            Vector3 spawnPos = Vector3.zero;
            _currentRoomGo = new GameObject("RiftBufferRoom");
            _currentRoomGo.transform.position = spawnPos;
            var buffer = _currentRoomGo.AddComponent<RiftBufferRoom>();
            buffer.Initialize(RiftTier, OnStartChallengeRequested);

            GameManager.Instance.PlacePlayer(spawnPos);
        }

        /// <summary>缓冲区「开始挑战」被触发（Build 已通过 RiftEquipUI 装备）。</summary>
        private void OnStartChallengeRequested()
        {
            if (!RiftEquipUI.HasEquipped)
            {
                Debug.Log("<color=yellow>[Rift] 需先装备一套 Build 才能开始挑战</color>");
                RiftEquipUI.Show(OnBuildEquipped);
                return;
            }
            // #6：正式进入前弹出层数选择（最高 100 层），选定后开始挑战。
            RiftTierSelectUI.Show(RiftTier, tier =>
            {
                RiftTier = Mathf.Clamp(tier, RiftTierSelectUI.MinTier, RiftTierSelectUI.MaxTier);
                StartChallenge();
            });
        }

        /// <summary>外部设置大秘境层数（供层数选择 UI 使用）。</summary>
        public void SetTier(int tier) => RiftTier = Mathf.Clamp(tier, RiftTierSelectUI.MinTier, RiftTierSelectUI.MaxTier);

        private void OnBuildEquipped(BuildSnapshot snap)
        {
            _equippedBuild = snap;
            Debug.Log($"<color=#00ffcc>[Rift] 已装备 Build：{snap?.buildName}</color>");
        }

        // ==================== 挑战间 ====================

        /// <summary>开始计时挑战：构建挑战间，清怪 → Boss。</summary>
        public void StartChallenge()
        {
            DestroyRoom();

            Vector3 spawnPos = Vector3.zero;
            _currentRoomGo = new GameObject("RiftChamber");
            _currentRoomGo.transform.position = spawnPos;
            var chamber = _currentRoomGo.AddComponent<RiftChamber>();
            chamber.Initialize(RiftTier, OnChallengeSuccess);

            GameManager.Instance.PlacePlayer(spawnPos);
            Debug.Log($"<color=#ff66cc>═══ 大秘境挑战开始 · 第 {RiftTier} 层 ═══</color>");
        }

        /// <summary>挑战成功（Boss 被击杀）。</summary>
        private void OnChallengeSuccess(float clearSeconds)
        {
            Debug.Log($"<color=lime>═══ 大秘境通关！用时 {clearSeconds:F1}s ═══</color>");
            RiftRewardUI.Show(RiftTier, clearSeconds, isSuccess: true, onDone: () =>
            {
                RiftTier++;   // 通关后提升层数
                ReturnToVillage();
            });
        }

        /// <summary>挑战失败（玩家死亡）。</summary>
        public void OnPlayerDiedInRift()
        {
            Debug.Log("<color=red>═══ 大秘境挑战失败 ═══</color>");
            RiftRewardUI.Show(RiftTier, 0f, isSuccess: false, onDone: ReturnToVillage);
        }

        // ==================== 返回 ====================

        private void ReturnToVillage()
        {
            DestroyRoom();
            RiftEquipUI.ClearEquipped();
            GameManager.Instance.ExitRiftToVillage();
        }

        private void DestroyRoom()
        {
            if (_currentRoomGo != null)
            {
                Destroy(_currentRoomGo);
                _currentRoomGo = null;
            }
        }
    }
}
