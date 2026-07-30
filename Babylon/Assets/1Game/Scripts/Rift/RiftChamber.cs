using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境计时挑战间（Phase3）。
    ///
    /// 玩法（GDD Q-008，参考暗黑 3 大秘境）：
    ///   1. 计时开始，持续刷怪；
    ///   2. 玩家累计击杀达到目标数量后，刷新出 Boss；
    ///   3. 击杀 Boss → 挑战成功，回调用时。
    ///
    /// 难度随大秘境层数（tier）缩放：目标击杀数、敌人血量/伤害倍率随 tier 提升。
    /// 数值先用常量框架，后续可接入配表（GDD Q-009 奖励/数值书面讨论）。
    /// </summary>
    public class RiftChamber : MonoBehaviour
    {
        private const float ArenaSize = 40f;
        private const int BaseTargetKills = 20;    // 第 1 层需击杀数
        private const int KillsPerTier = 5;        // 每层递增
        private const int MaxConcurrent = 8;       // 场上同时存活上限

        private int _tier;
        private Action<float> _onSuccess;

        private GameObject _roomVisuals;
        private float _startTime;
        private int _killCount;
        private int _targetKills;
        private float _hpMul;
        private float _dmgMul;

        private bool _bossPhase;
        private bool _bossSpawned;
        private bool _complete;
        private float _spawnTimer;
        private readonly List<GameObject> _alive = new();

        // 计时 HUD（uGUI+TMP）
        private GameObject _hud;
        private TextMeshProUGUI _hudTitle;
        private TextMeshProUGUI _hudProgress;

        public void Initialize(int tier, Action<float> onSuccess)
        {
            _tier = Mathf.Max(1, tier);
            _onSuccess = onSuccess;

            _targetKills = BaseTargetKills + (_tier - 1) * KillsPerTier;
            _hpMul = 1f + (_tier - 1) * 0.5f;
            _dmgMul = 1f + (_tier - 1) * 0.3f;

            BuildArena();
            BuildHud();
            _startTime = Time.time;
            _spawnTimer = 0f;

            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            // 首波立即刷怪
            SpawnWave();
            Debug.Log($"<color=#ff66cc>[RiftChamber] 第 {_tier} 层：目标击杀 {_targetKills}，HP×{_hpMul:F1} DMG×{_dmgMul:F1}</color>");
        }

        private void BuildArena()
        {
            _roomVisuals = RoomBuilder.Build(transform, ArenaSize, ArenaSize, 2);
            _roomVisuals.name = "RiftChamberVisuals";
        }

        private void Update()
        {
            if (_complete) return;

            UpdateHud();

            // 清理已销毁引用
            _alive.RemoveAll(e => e == null);

            if (!_bossPhase)
            {
                // 清怪阶段：持续补充刷怪
                _spawnTimer -= Time.deltaTime;
                if (_spawnTimer <= 0f && _alive.Count < MaxConcurrent && _killCount < _targetKills)
                {
                    SpawnWave();
                    _spawnTimer = 2.5f;
                }

                // 达到目标击杀 → 进入 Boss 阶段
                if (_killCount >= _targetKills && !_bossSpawned)
                {
                    EnterBossPhase();
                }
            }
        }

        private void SpawnWave()
        {
            int remaining = _targetKills - _killCount;
            int room = MaxConcurrent - _alive.Count;
            int count = Mathf.Clamp(Mathf.Min(room, remaining), 0, 4);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = RandomSpawnPos();
                GameObject enemy = SpawnByType(i, pos);
                if (enemy != null) _alive.Add(enemy);
            }
        }

        // 敌人各类互不继承（都是独立 MonoBehaviour），统一返回 GameObject 跟踪存活。
        private GameObject SpawnByType(int seed, Vector3 pos)
        {
            int roll = (seed + _killCount) % 4;
            if (_tier >= 2 && roll == 0)
                return EnemyRanged.Spawn(pos, _hpMul, _dmgMul)?.gameObject;
            if (_tier >= 2 && roll == 1)
                return EnemyCharger.Spawn(pos, _hpMul, _dmgMul)?.gameObject;
            if (_tier >= 3 && roll == 2)
                return EnemyMage.Spawn(pos, _hpMul, _dmgMul)?.gameObject;
            return EnemyBase.Spawn(pos, _hpMul, _dmgMul)?.gameObject;
        }

        private void EnterBossPhase()
        {
            _bossPhase = true;
            _bossSpawned = true;

            // 清理残余小怪，聚焦 Boss
            foreach (var go in _alive)
                if (go != null) Destroy(go);
            _alive.Clear();

            Vector3 pos = transform.position + new Vector3(0, 0, 8f);
            var boss = EnemyBoss.Spawn(pos, _hpMul * 1.5f, _dmgMul * 1.2f, bossID: 1);
            if (boss != null) _alive.Add(boss.gameObject);

            Debug.Log($"<color=#ff3366>[RiftChamber] 目标达成！Boss 降临！</color>");
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (_complete) return;

            if (_bossPhase)
            {
                // Boss 阶段：Boss 死亡即通关
                bool bossName = evt.Enemy != null && evt.Enemy.name.Contains("Boss");
                if (bossName || _alive.TrueForAll(e => e == null || e == evt.Enemy))
                {
                    CompleteSuccess();
                }
                return;
            }

            _killCount++;
        }

        private void CompleteSuccess()
        {
            if (_complete) return;
            _complete = true;
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            float elapsed = Time.time - _startTime;
            _onSuccess?.Invoke(elapsed);
        }

        private Vector3 RandomSpawnPos()
        {
            float half = ArenaSize / 2f - 4f;
            Vector3 pos;
            int attempts = 0;
            do
            {
                pos = transform.position + new Vector3(
                    UnityEngine.Random.Range(-half, half), 0,
                    UnityEngine.Random.Range(-half, half));
                attempts++;
            } while (Vector3.Distance(pos, transform.position) < 6f && attempts < 20);
            pos.y = 0;
            return pos;
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            if (_roomVisuals != null) Destroy(_roomVisuals);
            if (_hud != null) Destroy(_hud);
        }

        // ==================== 计时 HUD（uGUI+TMP） ====================

        private void BuildHud()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("RiftChamberHUD", 46);
            _hud = canvas.gameObject;
            var ray = _hud.GetComponent<GraphicRaycaster>();
            if (ray != null) Destroy(ray);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var brt = (RectTransform)box.transform;
            brt.SetParent(_hud.transform, false);
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -12f); brt.sizeDelta = new Vector2(360f, 70f);
            box.color = new Color(0f, 0f, 0f, 0.55f);
            box.raycastTarget = false;
            var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(6, 6, 6, 6); v.spacing = 2f;
            v.childControlWidth = true; v.childForceExpandWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.MiddleCenter;

            _hudTitle = UGuiKit.CreateText(brt, "", 22, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(_hudTitle, 30f);
            _hudProgress = UGuiKit.CreateText(brt, "", 22, new Color(0.7f, 0.9f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(_hudProgress, 28f);
        }

        private void UpdateHud()
        {
            if (_hud == null) return;
            float elapsed = Time.time - _startTime;
            string timeStr = $"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}";
            _hudTitle.text = $"大秘境 · 第 {_tier} 层    {timeStr}";
            _hudProgress.color = _bossPhase ? new Color(1f, 0.4f, 0.4f) : new Color(0.7f, 0.9f, 1f);
            _hudProgress.text = _bossPhase ? "★ 击杀 Boss ★" : $"进度 {_killCount} / {_targetKills}";
        }
    }
}
