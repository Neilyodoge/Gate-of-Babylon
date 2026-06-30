using UnityEngine;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 音效管理器 —— 全局单例
    /// 负责播放所有音效和背景音乐，自动从 AudioConfig 读取配置
    /// 使用对象池管理 AudioSource，避免频繁创建销毁
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ========== 单例 ==========
        public static AudioManager Instance { get; private set; }

        // ========== 内部组件 ==========
        private AudioSource _bgmSource;         // BGM 专用 AudioSource
        private AudioSource _uiSource;           // UI 音效专用 AudioSource
        private readonly List<AudioSource> _sfxPool = new();  // SFX 对象池
        private int _sfxPoolIndex;

        /// <summary>SFX 对象池大小</summary>
        private const int SFX_POOL_SIZE = 16;

        /// <summary>同一音效的最小播放间隔（防止重叠刺耳）</summary>
        private const float MIN_PLAY_INTERVAL = 0.05f;
        private readonly Dictionary<int, float> _lastPlayTime = new();

        private AudioConfig _config;

        // ========== 生命周期 ==========

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _config = AudioConfig.Instance;

            // 创建 BGM AudioSource
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f; // 2D 音效

            // 创建 UI AudioSource
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

            // 创建 SFX 对象池
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 默认 2D，3D 音效可单独设置
                _sfxPool.Add(source);
            }

            UpdateVolumes();
        }

        private void OnEnable()
        {
            // 订阅游戏事件，自动播放对应音效
            GameEvents.Subscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.SkillEquipped>(OnSkillEquipped);
            GameEvents.Subscribe<GameEvents.SkillDecomposed>(OnSkillDecomposed);
            GameEvents.Subscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Subscribe<GameEvents.DashCooldownUpdate>(OnDashCooldown);
            GameEvents.Subscribe<GameEvents.ComboStepChanged>(OnComboStep);
            GameEvents.Subscribe<GameEvents.DamageNumberRequested>(OnDamageNumber);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.SkillEquipped>(OnSkillEquipped);
            GameEvents.Unsubscribe<GameEvents.SkillDecomposed>(OnSkillDecomposed);
            GameEvents.Unsubscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Unsubscribe<GameEvents.DashCooldownUpdate>(OnDashCooldown);
            GameEvents.Unsubscribe<GameEvents.ComboStepChanged>(OnComboStep);
            GameEvents.Unsubscribe<GameEvents.DamageNumberRequested>(OnDamageNumber);
        }

        // ========== 公共 API ==========

        /// <summary>播放音效（2D，无空间感）</summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || _config == null) return;
            if (!CheckPlayInterval(clip)) return;

            var source = GetAvailableSFXSource();
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = _config.masterVolume * _config.sfxVolume * volumeScale;
            source.pitch = 1f;
            source.Play();
        }

        /// <summary>播放音效（带随机音高变化，增加多样性）</summary>
        public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f, float volumeScale = 1f)
        {
            if (clip == null || _config == null) return;
            if (!CheckPlayInterval(clip)) return;

            var source = GetAvailableSFXSource();
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = _config.masterVolume * _config.sfxVolume * volumeScale;
            source.pitch = Random.Range(minPitch, maxPitch);
            source.Play();
        }

        /// <summary>在指定世界位置播放 3D 音效</summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null || _config == null) return;
            if (!CheckPlayInterval(clip)) return;

            // 使用 Unity 内置的 PlayClipAtPoint（简单但无法控制音量）
            // 改用对象池方式
            var source = GetAvailableSFXSource();
            source.spatialBlend = 0.8f; // 偏 3D
            source.clip = clip;
            source.volume = _config.masterVolume * _config.sfxVolume * volumeScale;
            source.pitch = 1f;
            source.Play();
        }

        /// <summary>从数组中随机播放一个音效</summary>
        public void PlayRandomSFX(AudioClip[] clips, float volumeScale = 1f)
        {
            var clip = AudioConfig.GetRandom(clips);
            PlaySFXRandomPitch(clip, 0.9f, 1.1f, volumeScale);
        }

        /// <summary>播放 UI 音效</summary>
        public void PlayUI(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || _config == null) return;

            _uiSource.clip = clip;
            _uiSource.volume = _config.masterVolume * _config.uiVolume * volumeScale;
            _uiSource.pitch = 1f;
            _uiSource.Play();
        }

        /// <summary>播放背景音乐</summary>
        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (_config == null) return;

            if (clip == null)
            {
                StopBGM(fadeTime);
                return;
            }

            // 如果已经在播放同一首，不重复
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            // TODO: 淡入淡出（后续可用协程实现）
            _bgmSource.clip = clip;
            _bgmSource.volume = _config.masterVolume * _config.bgmVolume;
            _bgmSource.Play();
        }

        /// <summary>停止背景音乐</summary>
        public void StopBGM(float fadeTime = 1f)
        {
            // TODO: 淡出（后续可用协程实现）
            _bgmSource.Stop();
        }

        /// <summary>更新音量设置（修改 AudioConfig 后调用）</summary>
        public void UpdateVolumes()
        {
            if (_config == null) return;
            _bgmSource.volume = _config.masterVolume * _config.bgmVolume;
            _uiSource.volume = _config.masterVolume * _config.uiVolume;
        }

        // ========== 便捷方法（直接从 AudioConfig 读取并播放） ==========

        /// <summary>播放近战攻击音效</summary>
        public void PlayMeleeAttack(int comboStep)
        {
            if (_config == null) return;
            PlaySFXRandomPitch(_config.GetMeleeAttackClip(comboStep), 0.95f, 1.05f);
        }

        /// <summary>播放近战命中音效</summary>
        public void PlayMeleeHit(bool isCrit = false)
        {
            if (_config == null) return;
            if (isCrit && _config.critHit != null)
                PlaySFX(_config.critHit);
            else
                PlayRandomSFX(_config.meleeHits);
        }

        /// <summary>播放闪避音效</summary>
        public void PlayDash()
        {
            if (_config == null) return;
            PlaySFX(_config.dash);
        }

        /// <summary>播放技能释放音效</summary>
        public void PlaySkillCast(AudioClip skillSpecificClip = null)
        {
            if (_config == null) return;
            PlaySFX(skillSpecificClip != null ? skillSpecificClip : _config.skillCastDefault);
        }

        /// <summary>播放灵物拾取音效</summary>
        public void PlayItemPickup(ItemRarity rarity)
        {
            if (_config == null) return;
            PlaySFX(_config.GetItemPickupClip(rarity));
        }

        /// <summary>播放传送门音效</summary>
        public void PlayPortal(bool isTeleport = false)
        {
            if (_config == null) return;
            PlaySFX(isTeleport ? _config.portalTeleport : _config.portalActivate);
        }

        /// <summary>播放战斗 BGM</summary>
        public void PlayBattleBGM(int level)
        {
            if (_config == null) return;
            PlayBGM(_config.GetBattleBGM(level));
        }

        /// <summary>播放 Boss BGM</summary>
        public void PlayBossBGM()
        {
            if (_config == null) return;
            PlayBGM(_config.bgmBoss);
        }

        // ========== 事件回调（自动播放音效） ==========

        private void OnPlayerDamaged(GameEvents.PlayerDamaged evt)
        {
            if (_config == null) return;
            PlayRandomSFX(_config.playerHurt);
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            if (_config == null) return;
            PlaySFX(_config.playerDeath);
            PlayBGM(_config.gameLose);
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (_config == null) return;
            PlayRandomSFX(_config.enemyDeath, 0.8f);
            PlaySFX(_config.killConfirm, 0.6f);
        }

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            if (_config == null) return;
            PlaySFX(_config.realmBreakthrough);
        }

        private void OnSkillEquipped(GameEvents.SkillEquipped evt)
        {
            if (_config == null) return;
            PlaySFX(_config.skillPickup);
        }

        private void OnSkillDecomposed(GameEvents.SkillDecomposed evt)
        {
            if (_config == null) return;
            PlaySFX(_config.itemDecompose);
        }

        private void OnGameWon(GameEvents.GameWon evt)
        {
            if (_config == null) return;
            PlaySFX(_config.gameWin);
            PlayBGM(_config.bgmVictory);
        }

        private void OnDashCooldown(GameEvents.DashCooldownUpdate evt)
        {
            // 闪避时播放音效（CD 刚开始时 = 刚闪避）
            if (_config == null) return;
            if (Mathf.Approximately(evt.RemainingTime, evt.TotalCooldown))
                PlayDash();
        }

        private void OnComboStep(GameEvents.ComboStepChanged evt)
        {
            if (_config == null) return;
            if (evt.IsAttacking)
                PlayMeleeAttack(evt.ComboStep);
        }

        private void OnDamageNumber(GameEvents.DamageNumberRequested evt)
        {
            // 敌人受击音效
            if (_config == null) return;
            if (!evt.IsPlayerDamage)
                PlayRandomSFX(_config.enemyHurt, 0.5f);
        }

        // ========== 内部方法 ==========

        /// <summary>从对象池获取可用的 SFX AudioSource</summary>
        private AudioSource GetAvailableSFXSource()
        {
            // 轮询方式分配
            var source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Count;
            return source;
        }

        /// <summary>检查同一音效的播放间隔，防止重叠</summary>
        private bool CheckPlayInterval(AudioClip clip)
        {
            int id = clip.GetInstanceID();
            float now = Time.unscaledTime;

            if (_lastPlayTime.TryGetValue(id, out float lastTime))
            {
                if (now - lastTime < MIN_PLAY_INTERVAL)
                    return false;
            }

            _lastPlayTime[id] = now;
            return true;
        }
    }
}
