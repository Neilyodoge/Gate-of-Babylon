using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 监听 GameEvents 评估单条模块链的触发条件。每个 TriggerTracker 对应一条链的触发器。
    ///
    /// V.08 Proc → Consume 模型：
    /// - Single：Proc 条件满足 → IsProc=true，等待玩家按键消费
    /// - Window：Proc 条件满足 → 进入 windowSeconds 就绪窗口，窗口内 IsProc=true
    /// - Stacks：每次 Proc 累层（上限 maxStacks），IsProc = (stacks > 0)，每按一次消费 1 层
    /// - Auto：Proc 条件满足 → 立即触发 OnAutoTriggered 回调（由 ModuleSlotManager 自动消费），不保持 IsProc
    ///
    /// 触发阈值计数（_thresholdCount）与 Stacks 消费层数（_stacks）是两个独立计数器。
    /// </summary>
    public class TriggerTracker
    {
        public ConsumeKind Kind => _cfg.consumeKind;
        public int MaxStacks => _cfg.maxStacks;
        public float WindowTotal => _cfg.windowSeconds;
        public float WindowRemaining => _windowTimer;
        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownTotal => _cfg.triggerCooldown;
        public int Threshold => _cfg.triggerThreshold;

        /// <summary>触发阈值当前进度（如近战命中 2/3），仅 triggerThreshold &gt; 1 时有意义。</summary>
        public int ThresholdProgress => _thresholdCount;

        /// <summary>当前可消费的增强是否就绪（Single proc 未消费 / Window 在窗口内 / Stacks 层数 &gt; 0）。Auto 永远 false。</summary>
        public bool IsProc
        {
            get
            {
                switch (_cfg.consumeKind)
                {
                    case ConsumeKind.Single: return _procReady;
                    case ConsumeKind.Window: return _windowTimer > 0f && !_windowExpired;
                    case ConsumeKind.Stacks: return _stacks > 0;
                    case ConsumeKind.Auto: return false;
                    default: return false;
                }
            }
        }

        /// <summary>Stacks 模式当前层数；其它模式返回 0/1 仅供 UI 使用。</summary>
        public int CurrentStacks => _cfg.consumeKind == ConsumeKind.Stacks ? _stacks : (_procReady || _windowTimer > 0f ? 1 : 0);

        /// <summary>Auto 模式 Proc 时触发，参数是 slot 索引（由 ModuleSlotManager 注入）。</summary>
        public event Action<int> OnAutoTriggered;

        private readonly ChainConfig _cfg;
        private readonly int _slot;
        private float _cooldownRemaining;
        private float _intervalTimer;
        private float _moveAccum;
        private int _thresholdCount;     // 触发阈值计数（如命中 N 次）
        private bool _subscribed;

        // V.08 消费模型状态
        private bool _procReady;          // Single
        private float _windowTimer;       // Window
        private bool _windowExpired;      // Window 已过期但本次未消费
        private int _stacks;              // Stacks

        public TriggerTracker(ChainConfig cfg, int slot)
        {
            _cfg = cfg;
            _slot = slot;
        }

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            switch (_cfg.triggerType)
            {
                case TriggerType.MeleeHitCount:
                case TriggerType.CriticalHit:
                    GameEvents.Subscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
                    break;
                case TriggerType.DodgeFinish:
                    GameEvents.Subscribe<GameEvents.DodgeFinished>(OnDodge);
                    break;
                case TriggerType.OnDamaged:
                case TriggerType.ShieldBreak:
                case TriggerType.LowHealth:
                    GameEvents.Subscribe<GameEvents.PlayerDamaged>(OnDamaged);
                    break;
                case TriggerType.EnemyKill:
                case TriggerType.EliteKill:
                    GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKill);
                    break;
                case TriggerType.MoveDistance:
                    _moveAccum = 0f;
                    break;
                case TriggerType.TimeInterval:
                    _intervalTimer = _cfg.triggerInterval;
                    break;
                case TriggerType.RoomEnter:
                    GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomEvent);
                    break;
            }
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            switch (_cfg.triggerType)
            {
                case TriggerType.MeleeHitCount:
                case TriggerType.CriticalHit:
                    GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(OnMeleeHit);
                    break;
                case TriggerType.DodgeFinish:
                    GameEvents.Unsubscribe<GameEvents.DodgeFinished>(OnDodge);
                    break;
                case TriggerType.OnDamaged:
                case TriggerType.ShieldBreak:
                case TriggerType.LowHealth:
                    GameEvents.Unsubscribe<GameEvents.PlayerDamaged>(OnDamaged);
                    break;
                case TriggerType.EnemyKill:
                case TriggerType.EliteKill:
                    GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKill);
                    break;
                case TriggerType.RoomEnter:
                    GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomEvent);
                    break;
            }
        }

        /// <summary>每帧由 ModuleSlotManager 调用</summary>
        public void Tick(float dt, Vector3 playerPos, ref Vector3 lastPos)
        {
            // 冷却期：不累计阈值、不进窗口、不叠层
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= dt;
                if (_cooldownRemaining < 0f) _cooldownRemaining = 0f;
            }

            // Window 倒计时（独立于冷却，窗口结束即过期）
            if (_cfg.consumeKind == ConsumeKind.Window && _windowTimer > 0f)
            {
                _windowTimer -= dt;
                if (_windowTimer <= 0f)
                {
                    _windowTimer = 0f;
                    _windowExpired = true;
                    // 窗口结束 → 重新进入冷却
                    StartCooldown();
                }
            }

            // TimeInterval / MoveDistance / LowHealth 在 Tick 内评估
            if (_cooldownRemaining > 0f) return;

            switch (_cfg.triggerType)
            {
                case TriggerType.TimeInterval:
                    _intervalTimer -= dt;
                    if (_intervalTimer <= 0f)
                    {
                        TriggerConditionMet();
                        _intervalTimer = _cfg.triggerInterval;
                    }
                    break;

                case TriggerType.MoveDistance:
                    float moved = Vector3.Distance(playerPos, lastPos);
                    if (moved > 0.01f)
                    {
                        _moveAccum += moved;
                        lastPos = playerPos;
                        if (_moveAccum >= _cfg.moveDistanceThreshold)
                        {
                            TriggerConditionMet();
                            _moveAccum = 0f;
                        }
                    }
                    break;

                case TriggerType.LowHealth:
                    var player = PlayerController.Instance;
                    if (player != null)
                    {
                        float ratio = player.Stats.currentHp / Mathf.Max(1f, player.Stats.maxHp);
                        if (ratio <= _cfg.healthThreshold)
                            TriggerConditionMet();
                    }
                    break;
            }
        }

        /// <summary>玩家按键消费增强。返回是否成功消费。</summary>
        public bool Consume()
        {
            switch (_cfg.consumeKind)
            {
                case ConsumeKind.Single:
                    if (!_procReady) return false;
                    _procReady = false;
                    StartCooldown();
                    return true;

                case ConsumeKind.Window:
                    if (_windowTimer <= 0f || _windowExpired) return false;
                    _windowTimer = 0f;
                    _windowExpired = false;
                    StartCooldown();
                    return true;

                case ConsumeKind.Stacks:
                    if (_stacks <= 0) return false;
                    _stacks--;
                    // Stacks 模式：层数归零时进入冷却；仍有层数时不进冷却，允许连续消费
                    if (_stacks <= 0) StartCooldown();
                    return true;

                case ConsumeKind.Auto:
                    // Auto 不由玩家消费
                    return false;

                default:
                    return false;
            }
        }

        public void Reset()
        {
            _thresholdCount = 0;
            _cooldownRemaining = 0f;
            _intervalTimer = _cfg.triggerInterval;
            _moveAccum = 0f;
            _procReady = false;
            _windowTimer = 0f;
            _windowExpired = false;
            _stacks = 0;
        }

        // ==================== 内部 ====================

        private void StartCooldown()
        {
            _cooldownRemaining = _cfg.triggerCooldown;
            _thresholdCount = 0;
        }

        /// <summary>Proc 条件达成（阈值满足）→ 按 consumeKind 分派</summary>
        private void TriggerConditionMet()
        {
            if (_cooldownRemaining > 0f) return;

            // 阈值计数（triggerThreshold > 1 时累加；=1 直接成立）
            if (_cfg.triggerThreshold > 1)
            {
                _thresholdCount++;
                if (_thresholdCount < _cfg.triggerThreshold) return;
            }

            switch (_cfg.consumeKind)
            {
                case ConsumeKind.Single:
                    if (_procReady) return;   // 已就绪不重复触发
                    _procReady = true;
                    break;

                case ConsumeKind.Window:
                    if (_windowTimer > 0f && !_windowExpired) return;
                    _windowTimer = _cfg.windowSeconds;
                    _windowExpired = false;
                    break;

                case ConsumeKind.Stacks:
                    if (_stacks >= _cfg.maxStacks) return;   // 已满不重复叠
                    _stacks++;
                    break;

                case ConsumeKind.Auto:
                    // 立即触发自动消费
                    OnAutoTriggered?.Invoke(_slot);
                    StartCooldown();
                    break;
            }
        }

        private void OnMeleeHit(GameEvents.MeleeHitConnected evt) => TriggerConditionMet();
        private void OnDodge(GameEvents.DodgeFinished evt) => TriggerConditionMet();
        private void OnDamaged(GameEvents.PlayerDamaged evt) => TriggerConditionMet();
        private void OnEnemyKill(GameEvents.EnemyKilled evt) => TriggerConditionMet();
        private void OnRoomEvent(GameEvents.RoomCleared evt) => TriggerConditionMet();
    }
}
