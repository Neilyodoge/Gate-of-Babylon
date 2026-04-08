using System;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 轻量级全局事件系统，用于模块间解耦通信
    /// </summary>
    public static class GameEvents
    {
        private static readonly Dictionary<Type, Delegate> _events = new();

        /// <summary>订阅事件</summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
                _events[type] = Delegate.Combine(existing, handler);
            else
                _events[type] = handler;
        }

        /// <summary>取消订阅</summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _events.Remove(type);
                else
                    _events[type] = result;
            }
        }

        /// <summary>触发事件</summary>
        public static void Publish<T>(T evt) where T : struct
        {
            if (_events.TryGetValue(typeof(T), out var handler))
                (handler as Action<T>)?.Invoke(evt);
        }

        /// <summary>清空所有事件（场景切换时调用）</summary>
        public static void Clear()
        {
            _events.Clear();
        }

        // ========== 事件定义 ==========

        /// <summary>敌人被击杀</summary>
        public struct EnemyKilled
        {
            public UnityEngine.GameObject Enemy;
            public UnityEngine.Vector3 Position;
        }

        /// <summary>玩家受伤</summary>
        public struct PlayerDamaged
        {
            public float Damage;
            public float CurrentHp;
            public float MaxHp;
        }

        /// <summary>玩家死亡</summary>
        public struct PlayerDied { }

        /// <summary>灵物被拾取</summary>
        public struct ItemPickedUp
        {
            public ItemData Item;
            public int CurrentCount; // 该灵物当前持有数量
        }

        /// <summary>境界突破</summary>
        public struct RealmBreakthrough
        {
            public int NewRealmLevel;
            public string RealmName;
        }

        /// <summary>房间清理完成</summary>
        public struct RoomCleared
        {
            public int RoomIndex;
        }

        /// <summary>技能冷却更新</summary>
        public struct SkillCooldownUpdate
        {
            public int SlotIndex;
            public float RemainingTime;
            public float TotalCooldown;
        }

        /// <summary>生命值变化</summary>
        public struct HealthChanged
        {
            public float CurrentHp;
            public float MaxHp;
        }

        /// <summary>请求播放刀光特效（由动画事件触发）</summary>
        public struct SlashVFXRequested
        {
            public int ComboStep;
        }

        /// <summary>请求播放打击特效</summary>
        public struct HitVFXRequested
        {
            public UnityEngine.Vector3 Position;
            public UnityEngine.Vector3 Normal;
        }

        /// <summary>缓冲的闪避请求（由动画系统在状态结束时触发）</summary>
        public struct BufferedEvadeRequested { }

        /// <summary>攻击前冲请求（哈迪斯/梦之行风格：每段攻击的微小前冲位移）</summary>
        public struct AttackLungeRequested
        {
            public float LungeSpeed;    // 前冲速度
            public float LungeDuration; // 前冲持续时间
        }

        /// <summary>连招窗口打开（用于短暂解锁朝向调整）</summary>
        public struct ComboWindowOpened { }
    }
}
