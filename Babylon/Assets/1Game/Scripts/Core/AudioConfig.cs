using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 音效配置 —— ScriptableObject
    /// 集中管理所有音效资源引用，后续只需在 Inspector 中拖入音频文件即可
    /// 菜单：Assets → Create → 仙途梦境 → 音效配置
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "仙途梦境/音效配置")]
    public class AudioConfig : ScriptableObject
    {
        // ========== 单例访问 ==========
        private static AudioConfig _instance;
        public static AudioConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<AudioConfig>("AudioConfig");
                return _instance;
            }
        }

        // ==================== 玩家音效 ====================
        [Header("═══ 玩家 · 攻击 ═══")]
        [Tooltip("近战三段连招音效（按段数索引：0=第一段，1=第二段，2=第三段）")]
        public AudioClip[] meleeAttacks = new AudioClip[3];

        [Tooltip("近战命中音效（随机播放一个）")]
        public AudioClip[] meleeHits;

        [Tooltip("暴击命中音效")]
        public AudioClip critHit;

        [Tooltip("击杀音效")]
        public AudioClip killConfirm;

        [Header("═══ 玩家 · 动作 ═══")]
        [Tooltip("闪避音效")]
        public AudioClip dash;

        [Tooltip("玩家受伤音效（随机播放一个）")]
        public AudioClip[] playerHurt;

        [Tooltip("玩家死亡音效")]
        public AudioClip playerDeath;

        [Tooltip("脚步声（循环播放）")]
        public AudioClip footstep;

        // ==================== 技能音效 ====================
        [Header("═══ 技能 ═══")]
        [Tooltip("技能释放通用音效（当技能自身没有配置音效时使用）")]
        public AudioClip skillCastDefault;

        [Tooltip("技能冷却完成提示音")]
        public AudioClip skillReady;

        [Tooltip("投射物发射音效")]
        public AudioClip projectileFire;

        [Tooltip("投射物命中音效")]
        public AudioClip projectileHit;

        [Tooltip("范围技能爆炸音效")]
        public AudioClip aoeExplosion;

        [Tooltip("增益Buff施加音效")]
        public AudioClip buffApply;

        // ==================== 敌人音效 ====================
        [Header("═══ 敌人 ═══")]
        [Tooltip("敌人受击音效（随机播放一个）")]
        public AudioClip[] enemyHurt;

        [Tooltip("敌人死亡音效（随机播放一个）")]
        public AudioClip[] enemyDeath;

        [Tooltip("敌人攻击音效")]
        public AudioClip enemyAttack;

        [Tooltip("敌人冲锋蓄力音效")]
        public AudioClip enemyCharge;

        [Tooltip("敌人投射物发射音效")]
        public AudioClip enemyProjectile;

        [Tooltip("Boss 出场音效")]
        public AudioClip bossAppear;

        [Tooltip("Boss 特殊攻击音效")]
        public AudioClip bossSpecialAttack;

        // ==================== 灵物 & 拾取 ====================
        [Header("═══ 灵物 & 拾取 ═══")]
        [Tooltip("灵物拾取音效（按品阶索引：0=凡品，1=灵品，2=玄品，3=地品，4=天品）")]
        public AudioClip[] itemPickup = new AudioClip[5];

        [Tooltip("功法拾取音效")]
        public AudioClip skillPickup;

        [Tooltip("灵物分解音效")]
        public AudioClip itemDecompose;

        [Tooltip("质变触发音效")]
        public AudioClip qualitativeTransmute;

        [Tooltip("Synergy 组合激活音效")]
        public AudioClip synergyActivate;

        [Tooltip("灵物掉落在地上的音效")]
        public AudioClip itemDrop;

        // ==================== UI 音效 ====================
        [Header("═══ UI ═══")]
        [Tooltip("按钮点击音效")]
        public AudioClip uiClick;

        [Tooltip("面板打开音效")]
        public AudioClip uiOpen;

        [Tooltip("面板关闭音效")]
        public AudioClip uiClose;

        [Tooltip("商店购买成功音效")]
        public AudioClip shopBuy;

        [Tooltip("商店购买失败音效（灵力碎片不足）")]
        public AudioClip shopFail;

        [Tooltip("境界突破音效")]
        public AudioClip realmBreakthrough;

        [Tooltip("游戏通关（飞升成仙）音效")]
        public AudioClip gameWin;

        [Tooltip("游戏失败音效")]
        public AudioClip gameLose;

        // ==================== 环境 & 房间 ====================
        [Header("═══ 环境 & 房间 ═══")]
        [Tooltip("传送门激活音效")]
        public AudioClip portalActivate;

        [Tooltip("传送门传送音效")]
        public AudioClip portalTeleport;

        [Tooltip("宝箱打开音效")]
        public AudioClip chestOpen;

        [Tooltip("灵泉恢复音效")]
        public AudioClip springHeal;

        [Tooltip("陷阱触发音效（地刺）")]
        public AudioClip trapSpike;

        [Tooltip("陷阱触发音效（火焰）")]
        public AudioClip trapFire;

        // ==================== BGM ====================
        [Header("═══ 背景音乐 ═══")]
        [Tooltip("主菜单 BGM")]
        public AudioClip bgmMenu;

        [Tooltip("战斗 BGM（按层数索引，不够则循环最后一首）")]
        public AudioClip[] bgmBattle;

        [Tooltip("商店/休息房间 BGM")]
        public AudioClip bgmShop;

        [Tooltip("Boss 战 BGM")]
        public AudioClip bgmBoss;

        [Tooltip("通关 BGM")]
        public AudioClip bgmVictory;

        // ==================== 全局音量 ====================
        [Header("═══ 音量设置 ═══")]
        [Tooltip("主音量（影响所有音效和音乐）")]
        [Range(0f, 1f)]
        public float masterVolume = 1f;

        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float sfxVolume = 0.8f;

        [Tooltip("背景音乐音量")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.5f;

        [Tooltip("UI 音效音量")]
        [Range(0f, 1f)]
        public float uiVolume = 0.7f;

        // ==================== 工具方法 ====================

        /// <summary>
        /// 根据品阶获取灵物拾取音效
        /// </summary>
        public AudioClip GetItemPickupClip(ItemRarity rarity)
        {
            int index = (int)rarity;
            if (itemPickup != null && index >= 0 && index < itemPickup.Length)
                return itemPickup[index];
            // 没有对应品阶音效时，返回凡品音效作为兜底
            return itemPickup != null && itemPickup.Length > 0 ? itemPickup[0] : null;
        }

        /// <summary>
        /// 根据连招段数获取近战攻击音效
        /// </summary>
        public AudioClip GetMeleeAttackClip(int comboStep)
        {
            if (meleeAttacks != null && comboStep >= 0 && comboStep < meleeAttacks.Length)
                return meleeAttacks[comboStep];
            return null;
        }

        /// <summary>
        /// 根据层数获取战斗 BGM
        /// </summary>
        public AudioClip GetBattleBGM(int level)
        {
            if (bgmBattle == null || bgmBattle.Length == 0) return null;
            int index = Mathf.Min(level, bgmBattle.Length - 1);
            return bgmBattle[index];
        }

        /// <summary>
        /// 从数组中随机选取一个音效
        /// </summary>
        public static AudioClip GetRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
