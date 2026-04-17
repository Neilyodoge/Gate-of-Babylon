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

        /// <summary>敌人数量变化（用于UI显示）</summary>
        public struct EnemyCountChanged
        {
            public int RemainingCount;
            public int TotalCount;
        }

        /// <summary>伤害数字显示请求</summary>
        public struct DamageNumberRequested
        {
            public UnityEngine.Vector3 WorldPosition;
            public float Damage;
            public bool IsCrit;
            public bool IsPlayerDamage; // true=玩家受伤（红色），false=敌人受伤（白/黄色）
            public bool IsBurn; // true=灼烧持续伤害（橙色+🔥）
            /// <summary>特殊伤害类型标签（焚天/剑阵/御风/火墙/元素爆发/格挡/嗜血等）</summary>
            public string SpecialTag;
        }

        /// <summary>游戏通关</summary>
        public struct GameWon { }

        /// <summary>闪避冷却更新（已废弃，保留兼容）</summary>
        public struct DashCooldownUpdate
        {
            public float RemainingTime;
            public float TotalCooldown;
        }

        /// <summary>闪避充能更新（新系统：多层充能）</summary>
        public struct DashChargeUpdate
        {
            public int CurrentCharges;
            public int MaxCharges;
            public float RechargeProgress; // 当前充能层的恢复进度 0~1
        }

        /// <summary>连招段数变化</summary>
        public struct ComboStepChanged
        {
            public int ComboStep;    // 当前段数 0/1/2
            public bool IsAttacking; // 是否在攻击中
        }

        /// <summary>Synergy 组合激活</summary>
        public struct SynergyActivated
        {
            public string SynergyName;
            public string Description;
        }

        /// <summary>灵物质变触发</summary>
        public struct QualitativeTriggered
        {
            public ItemData Item;
            public int Count;
            public string EffectDescription;
        }

        /// <summary>灵物收集进度变化（用于UI显示质变进度条）</summary>
        public struct ItemProgressChanged
        {
            public ItemData Item;
            public int CurrentCount;
            public int NextThreshold; // 下一个质变阈值（0=已全部触发）
        }

        /// <summary>功法装备到技能槽位</summary>
        public struct SkillEquipped
        {
            public SkillData Skill;
            public int SlotIndex;
        }

        /// <summary>功法被分解</summary>
        public struct SkillDecomposed
        {
            public SkillData Skill;
        }

        /// <summary>灵物槽位变化</summary>
        public struct SpiritSlotChanged
        {
            public int SlotIndex;
            public ItemData NewItem;
            public ItemData OldItem;
        }

        /// <summary>资源变化（灵力碎片等）</summary>
        public struct ResourceChanged
        {
            public int SpiritShards;
            public int Delta; // 变化量（正=获得，负=消耗）
        }

        /// <summary>技能蓄力进度更新（用于UI显示蓄力进度条）</summary>
        public struct SkillChargeProgress
        {
            public int SlotIndex;       // 技能槽位 0=Q, 1=E, 2=R
            public float ChargeTime;    // 当前蓄力时间
            public int ChargeLevel;     // 当前蓄力等级 1/2/3
            public bool IsCharging;     // 是否正在蓄力
        }

        /// <summary>技能蓄力释放（用于音效/特效反馈）</summary>
        public struct SkillChargeReleased
        {
            public int SlotIndex;
            public int ChargeLevel;     // 释放时的蓄力等级
            public SkillData Skill;
        }
    }
}
