using System;
using System.Collections.Generic;
using UnityEngine;

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
            /// <summary>未经减伤的原始伤害（用于水化身反伤等）</summary>
            public float RawDamage;
            /// <summary>攻击者 GameObject（可空）</summary>
            public UnityEngine.GameObject Attacker;
        }

        /// <summary>玩家死亡</summary>
        public struct PlayerDied { }


        /// <summary>进入新层</summary>
        public struct RealmBreakthrough
        {
            public int NewRealmLevel;
            public string RealmName;
        }

        /// <summary>房间清理完成</summary>
        public struct RoomCleared
        {
            public int RoomIndex;
            /// <summary>是否精英房（影响掉落数量与稀有度）。</summary>
            public bool IsElite;
            /// <summary>是否事件房。</summary>
            public bool IsEvent;
            /// <summary>是否战斗类房间（战斗/精英/事件）。</summary>
            public bool IsCombatRoom;
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

        /// <summary>技能修饰被激活（灵物在槽位中触发功法变体）</summary>
        public struct SkillModifierActivated
        {
            public int SlotIndex;          // 0=Q, 1=E, 2=R
            public string ModifiedSkillName; // 例如"陨石术"
            public ElementTag PrimaryTag;
        }

        // ========== v0.3.3 普攻↔技能融合层事件 ==========

        /// <summary>普攻命中（每段攻击命中至少 1 个敌人时发布一次）</summary>
        public struct MeleeHitConnected
        {
            public int ComboStep;   // 0=一段, 1=二段, 2=三段
            public UnityEngine.Vector3 HitPoint;
            public UnityEngine.GameObject Target;  // 主要命中的目标（通常是离玩家最近的）
        }

        /// <summary>主动技能开始释放（触发灵压窗口等）</summary>
        public struct SkillCastStarted
        {
            public int SlotIndex;
            public SkillData Skill;
        }

        /// <summary>主动技能命中敌人（每次技能命中至少 1 个敌人时发布一次）</summary>
        public struct SkillHitConnected
        {
            public int SlotIndex;
            public SkillData Skill;
            public UnityEngine.Vector3 HitPoint;
            public UnityEngine.GameObject Target;
        }


        // ========== v0.5 搜打撤核心事件 ==========

        /// <summary>洞府灵气资源变化（HUD / UI 监听）</summary>
        public struct CaveQiChanged
        {
            public int NewQi;
            public int Delta;
        }

        /// <summary>玩家请求开始撤离（按 F 走近出梦点时触发）</summary>
        public struct ExtractRequested
        {
            public UnityEngine.GameObject ExtractPoint;
        }

        /// <summary>撤离蓄力成功 —— 玩家完成 5s 蓄力，准备回洞府</summary>
        public struct ExtractSuccess
        {
            public int CaveMaterialsCommitted;  // 上交的洞府素材总件数（用于 UI 显示）
            public int RealmReachedIndex;       // 撤离时所在层 0~5
        }

        /// <summary>撤离被中断（蓄力期间被敌人攻击等）</summary>
        public struct ExtractInterrupted
        {
            public string Reason;  // "Damaged" / "Moved" / "Cancelled"
        }

        /// <summary>洞府素材已拾取（UI 提示用）</summary>
        public struct CaveMaterialPickedUp
        {
            public ItemData Item;
            public int Amount;
            public int CurrentBufferTotal;
        }

        /// <summary>经验变化（HUD 监听显示）</summary>
        public struct InsightChanged
        {
            public int NewRunInsight;
            public int Delta;
            public string Reason;
        }

        // ========== 角色等级 ==========

        /// <summary>历练变化（本局累积，撤离转进阶经验）</summary>
        public struct TemperingChanged
        {
            public int NewRunTempering;
            public int Delta;
            public string Reason;
        }

        /// <summary>角色等级晋升（晋级成功后）</summary>
        public struct CultivationBreakthrough
        {
            public int NewRealm;
            public string RealmName;
            public int Quality;   // 0=粗糙 1=普通 2=精良 3=完美
        }

        // ========== 洞府模块（Week 4）==========


        /// <summary>藏经阁成功拼合一卷功法</summary>
        public struct ScriptureSkillUnlocked
        {
            public string SkillName;
            public int TotalUnlocked;
        }

        /// <summary>阵法台预置一种阵法（等待下次入梦时激活）</summary>
        public struct FormationDeployed { public string FormationId; }

        // ========== v0.4 融合层事件 ==========

        /// <summary>闪避动作结束（水化身影息斩 / 金化身灵压窗口 30% 概率出现 等订阅）</summary>
        public struct DodgeFinished
        {
            public UnityEngine.Vector3 EndPosition;
            public UnityEngine.Vector3 EndDirection;
        }

        // ==================== 模块链系统（GDD V.07）====================

        /// <summary>模块链槽位变化（装备/卸下/Proc 状态切换）</summary>
        public struct ModuleChainChanged
        {
            public int SlotIndex;
            public bool HasChain;
            public bool IsProc;
            public string DisplayName;
        }

        /// <summary>模块被拾取</summary>
        public struct ModulePickedUp
        {
            public ModuleDef Module;
        }
    }
}
