using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 管理 3 条模块链槽位（对应 Q/E/R），驱动 TriggerTracker。
    /// V.08：链是核心技能的增强器。Proc 由 TriggerTracker 判定，Consume 由 PlayerCombat 按键调用；
    /// Auto 模式 Proc 时通过 OnAutoConsume 事件自动触发 PlayerCombat 释放绑定核心技能。
    /// 挂在 PlayerController 同一 GameObject 上。
    /// </summary>
    public class ModuleSlotManager : MonoBehaviour
    {
        private readonly ModuleChain[] _chains = new ModuleChain[3];
        private readonly ChainConfig[] _configs = new ChainConfig[3];
        private readonly TriggerTracker[] _trackers = new TriggerTracker[3];
        private readonly bool[] _hasChain = new bool[3];

        private Vector3 _lastPlayerPos;

        /// <summary>是否处于战斗中（战斗中禁止更换链）</summary>
        public bool InCombat { get; set; }

        /// <summary>Auto 模式 Proc 时触发，参数是 slot 索引。PlayerCombat 订阅以自动释放绑定核心技能。</summary>
        public event System.Action<int> OnAutoConsume;

        private void Start()
        {
            _lastPlayerPos = transform.position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            for (int i = 0; i < 3; i++)
            {
                if (_trackers[i] != null)
                    _trackers[i].Tick(dt, pos, ref _lastPlayerPos);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 3; i++)
                UnsubscribeSlot(i);
        }

        // ==================== Query API ====================

        public bool HasChain(int slot) => slot >= 0 && slot < 3 && _hasChain[slot];
        public bool IsProc(int slot) => HasChain(slot) && _trackers[slot] != null && _trackers[slot].IsProc;
        public ChainConfig GetConfig(int slot) => _configs[slot];
        public ModuleChain GetChain(int slot) => slot >= 0 && slot < 3 ? _chains[slot] : null;
        public TriggerTracker GetTracker(int slot) => slot >= 0 && slot < 3 ? _trackers[slot] : null;

        /// <summary>该槽位的消费模型</summary>
        public ConsumeKind GetConsumeKind(int slot)
        {
            if (!HasChain(slot)) return ConsumeKind.Single;
            return _configs[slot].consumeKind;
        }

        /// <summary>Stacks 模式当前层数（UI 用）</summary>
        public int GetStacks(int slot) => HasChain(slot) && _trackers[slot] != null ? _trackers[slot].CurrentStacks : 0;

        /// <summary>Window 模式剩余秒数（UI 用）</summary>
        public float GetWindowRemaining(int slot) => HasChain(slot) && _trackers[slot] != null ? _trackers[slot].WindowRemaining : 0f;

        /// <summary>Window 模式总秒数（UI 用）</summary>
        public float GetWindowTotal(int slot) => HasChain(slot) ? _configs[slot].windowSeconds : 0f;

        // ==================== Equip / Unequip ====================

        public bool EquipChain(int slot, ModuleChain chain)
        {
            if (slot < 0 || slot >= 3) return false;
            if (InCombat)
            {
                Debug.Log("<color=red>战斗中无法更换模块链！</color>");
                return false;
            }

            UnsubscribeSlot(slot);

            bool hasAny = chain != null &&
                (chain.trigger != null || chain.effect != null || chain.modifier0 != null || chain.modifier1 != null);

            // 完全为空 → 清空槽位
            if (!hasAny)
            {
                _chains[slot] = null;
                _hasChain[slot] = false;
                _trackers[slot] = null;
                PublishUpdate(slot);
                return true;
            }

            // 保留链对象（即使尚未成链），以便局内逐件装配持久化 + UI 显示"待补"状态，避免模块丢失
            _chains[slot] = chain;

            // 未成链（缺触发器或效果器）→ 存而不激活，不建 tracker
            if (!chain.IsValid)
            {
                _hasChain[slot] = false;
                _trackers[slot] = null;
                PublishUpdate(slot);
                return true;
            }

            _chains[slot] = chain;
            _configs[slot] = chain.Compile();
            _hasChain[slot] = true;

            var tracker = new TriggerTracker(_configs[slot], slot);
            _trackers[slot] = tracker;
            tracker.OnAutoTriggered += HandleAutoTriggered;
            tracker.Subscribe();

            string kindLabel = KindLabel(_configs[slot].consumeKind);
            Debug.Log($"<color=cyan>增强链装备到 {SlotName(slot)}（{kindLabel}）：{chain.DisplayName}</color>");
            PublishUpdate(slot);
            return true;
        }

        public ModuleChain UnequipChain(int slot)
        {
            if (slot < 0 || slot >= 3) return null;
            if (InCombat)
            {
                Debug.Log("<color=red>战斗中无法更换模块链！</color>");
                return null;
            }

            var old = _chains[slot];
            EquipChain(slot, null);
            return old;
        }

        /// <summary>玩家按键消费增强。返回是否成功消费（PlayerCombat 调用）。</summary>
        public bool ConsumeProc(int slot)
        {
            if (slot < 0 || slot >= 3 || _trackers[slot] == null) return false;
            bool ok = _trackers[slot].Consume();
            if (ok) PublishUpdate(slot);
            return ok;
        }

        public void ClearAll()
        {
            for (int i = 0; i < 3; i++)
            {
                UnsubscribeSlot(i);
                _chains[i] = null;
                _hasChain[i] = false;
                _trackers[i] = null;
            }
        }

        // ==================== 内部 ====================

        private void HandleAutoTriggered(int slot)
        {
            // Auto 模式 Proc → 通知 PlayerCombat 自动释放绑定核心技能 + 注入增强
            OnAutoConsume?.Invoke(slot);
            PublishUpdate(slot);
        }

        private void UnsubscribeSlot(int slot)
        {
            if (_trackers[slot] != null)
            {
                _trackers[slot].OnAutoTriggered -= HandleAutoTriggered;
                _trackers[slot].Unsubscribe();
                _trackers[slot] = null;
            }
        }

        private void PublishUpdate(int slot)
        {
            GameEvents.Publish(new GameEvents.ModuleChainChanged
            {
                SlotIndex = slot,
                HasChain = _hasChain[slot],
                IsProc = IsProc(slot),
                DisplayName = _hasChain[slot] ? _chains[slot].DisplayName : ""
            });
        }

        private static string SlotName(int slot) => slot switch
        {
            0 => "Q",
            1 => "E",
            2 => "R",
            _ => "?"
        };

        private static string KindLabel(ConsumeKind k) => k switch
        {
            ConsumeKind.Single => "单发",
            ConsumeKind.Window => "窗口",
            ConsumeKind.Stacks => "叠层",
            ConsumeKind.Auto => "自动",
            _ => "?"
        };
    }
}
