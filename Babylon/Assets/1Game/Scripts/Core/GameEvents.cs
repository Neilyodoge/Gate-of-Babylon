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

        /// <summary>化身选择完成（开局或调试切换时）</summary>
        public struct SpiritRootSelected
        {
            public SpiritRootType Root;
            public string DisplayName;
            public string Description;
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

        /// <summary>金化身 · 灵压窗口打开（VFX / HUD 用）</summary>
        public struct PerfectStrikeWindowOpened
        {
            public float WindowDuration;  // 窗口持续时长（秒）
            public UnityEngine.Vector3 PlayerHeadPos;  // 玩家头顶世界坐标（VFX 用）
            public string SourceTag;  // "Melee" / "Skill" / "Dodge" / "Sword Heart"
        }

        /// <summary>金化身 · 灵压爆发触发（完美收刀成功）</summary>
        public struct PerfectStrikeTriggered
        {
            public UnityEngine.Vector3 HitPoint;
            public int ConsecutiveCount;  // 连续完美次数
            public bool EnteredSwordHeart;  // 是否进入剑心通明
        }

        /// <summary>木化身 · 寄生种子引爆（VFX 用）</summary>
        public struct ParasiteSeedDetonated
        {
            public UnityEngine.Vector3 Position;
            public int SeedCount;     // 引爆的种子数量
            public float ExplosionRadius;
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
            public int RealmReachedIndex;       // 撤离时所在境界 0~5
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

        /// <summary>灵气浓度变化（HUD 弹房间属性提示用）</summary>
        public struct SpiritDensityChanged
        {
            public SpiritDensityLevel NewLevel;
            public string DisplayName;
            public UnityEngine.Color Tint;
        }

        /// <summary>悟性变化（HUD 监听显示）</summary>
        public struct InsightChanged
        {
            public int NewRunInsight;
            public int Delta;
            public string Reason;
        }

        // ========== 本体境界（v0.5.4）==========

        /// <summary>历练值变化（本局累积，撤离转永久修为）</summary>
        public struct TemperingChanged
        {
            public int NewRunTempering;
            public int Delta;
            public string Reason;
        }

        /// <summary>本体境界突破（渡劫战成功后）</summary>
        public struct CultivationBreakthrough
        {
            public int NewRealm;
            public string RealmName;
            public int Quality;   // 0=瑕品 1=凡品 2=上品 3=完美
        }

        // ========== 天劫渡劫（修仙独有战斗机制 #3）==========

        public struct TribulationStarted { public int BoltCount; }
        public struct TribulationBoltTelegraph { public int BoltIndex; }
        public struct TribulationFinished
        {
            public TribulationOutcome Outcome;
            public int HitCount;
        }

        // ========== 心魔劫（修仙独有战斗机制 #4，Week 4）==========

        /// <summary>心魔劫开战 —— RunHUD 用于显示横幅 / 全屏遮罩</summary>
        public struct InnerDemonStarted { public int RealmLevel; }
        /// <summary>心魔劫结束 —— 镜像被斩 or 玩家被反杀</summary>
        public struct InnerDemonFinished { public bool Defeated; }

        // ========== 洞府模块（Week 4）==========

        /// <summary>炼器房成功炼制一件灵物 —— RunHUD 弹"新解锁"提示</summary>
        public struct ForgeItemUnlocked
        {
            public string ItemName;
            public int TotalUnlocked;
        }

        /// <summary>藏经阁成功拼合一卷功法</summary>
        public struct ScriptureSkillUnlocked
        {
            public string SkillName;
            public int TotalUnlocked;
        }

        /// <summary>灵兽园成功孕育一只灵兽</summary>
        public struct SpiritBeastHatched { public string BeastName; }

        /// <summary>阵法台预置一种阵法（等待下次入梦时激活）</summary>
        public struct FormationDeployed { public string FormationId; }

        // ========== v0.4 融合层事件 ==========

        /// <summary>闪避动作结束（水化身影息斩 / 金化身灵压窗口 30% 概率出现 等订阅）</summary>
        public struct DodgeFinished
        {
            public UnityEngine.Vector3 EndPosition;
            public UnityEngine.Vector3 EndDirection;
        }

        /// <summary>水化身 · 影息斩触发（命中目标后留下水痕印 + 视觉反馈）</summary>
        public struct ShadowStrikeTriggered
        {
            public UnityEngine.Vector3 HitPoint;
            public UnityEngine.GameObject Target;
            public float DamageDealt;
        }

        /// <summary>火化身 · 怒气变化（HUD 用，与 ResourceChanged 区分以避免混淆灵力碎片）</summary>
        public struct RageChanged
        {
            public int CurrentRage;
            public int MaxRage;
        }

        /// <summary>火化身 · 狂火开始 / 结束（视觉用）</summary>
        public struct FireFrenzyState
        {
            public bool IsActive;        // true=进入狂火，false=狂火结束
            public float Duration;       // 持续时长（IsActive=false 时为 0）
            public bool IsForced;        // 是否强制爆发（怒气满 100 闲置 5s 触发）
        }

        // ==================== v0.5 Week 6：业焰印 / 火灵根重设计 ====================

        /// <summary>火灵根 · 业焰印层数变化（用于 HUD 提示"当前接触的敌人有几层"等）</summary>
        public struct FireBrandStackChanged
        {
            public GameObject Enemy;
            public int NewStacks;
            public int MaxStacks;
        }

        /// <summary>火灵根 · 业焰印满 5 层引爆 ——  HUD 弹"引爆！" + 视觉钩子</summary>
        public struct FireBrandExploded
        {
            public Vector3 EnemyPos;
            public int StacksConsumed;
            public float Radius;
        }

        // ==================== v0.5 Week 7：土化身 · 山岳承负 ====================

        /// <summary>土化身 · 扎根状态切换（进入 = true / 退出 = false）—— HUD + 视觉钩子</summary>
        public struct EarthRootedStateChanged
        {
            public bool IsRooted;
            public float AttackBonus;       // 仅 IsRooted=true 时有意义
            public float DamageReduction;
        }

        /// <summary>土化身 · 地脉烙印被技能引爆（HUD 飘字 + 视觉）</summary>
        public struct EarthSigilDetonated
        {
            public Vector3 Position;
            public int StacksConsumed;
            public int EnemiesAffected;
        }

        /// <summary>土化身 · 地脉护盾"某层被破"（视觉：让对应那块岩石板碎裂）</summary>
        public struct EarthShieldStackConsumed
        {
            public int StacksRemaining;
        }
    }

    /// <summary>渡劫结果（原定义于已删除的 HeavenTribulation.cs，v0.5.4 迁移至此）。</summary>
    public enum TribulationOutcome
    {
        Success,        // 渡劫成功
        PartialFail,    // 渡劫失利（半残，仅余撤离）
        Catastrophic    // 渡劫中止 / 陨落
    }
}
