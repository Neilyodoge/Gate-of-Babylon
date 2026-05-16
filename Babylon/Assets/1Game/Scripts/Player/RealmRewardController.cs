using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 境界突破奖励控制器（v0.4 最小可用版）—— 订阅 RealmBreakthrough 事件 → 3 选 1 → 应用。
    ///
    /// 仅在玩家进入新境界时弹一次面板；选完即把奖励永久应用到玩家身上（走 StatusEffect 框架）。
    /// 已取得的奖励 id 会记录下来，下次同名奖励不会再出现。
    /// </summary>
    public class RealmRewardController : MonoBehaviour
    {
        private readonly HashSet<string> _takenIds = new();

        private PlayerController _player;
        private SpiritRootController _root;
        private int _lastSeenRealmLevel = -1;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _root = GetComponent<SpiritRootController>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.InsightMomentTriggered>(OnInsightMoment);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.InsightMomentTriggered>(OnInsightMoment);
        }

        /// <summary>顿悟时刻 —— 复用 RealmRewardSelectUI 显示轻量 3 选 1 buff。</summary>
        private void OnInsightMoment(GameEvents.InsightMomentTriggered evt)
        {
            // 如果境界突破 UI 正在显示，延后到下一帧再弹（避免 UI 冲突）
            if (RealmRewardSelectUI.IsVisible)
            {
                StartCoroutine(DelayedInsightMoment(evt));
                return;
            }
            ShowInsightMoment(evt);
        }

        private System.Collections.IEnumerator DelayedInsightMoment(GameEvents.InsightMomentTriggered evt)
        {
            while (RealmRewardSelectUI.IsVisible) yield return null;
            yield return new WaitForSeconds(0.4f);
            ShowInsightMoment(evt);
        }

        private void ShowInsightMoment(GameEvents.InsightMomentTriggered evt)
        {
            var options = InsightMomentLibrary.Roll3(_takenIds);
            if (options == null || options.Count == 0) return;
            RealmRewardSelectUI.Show($"顿悟 · 第 {evt.MomentIndex} 次", options, OnRewardSelected);
        }

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            // 仅当真的"晋升"到新境界（level 增加）时弹面板。
            // 第一次进入第 0 层（练气期）不弹。
            if (evt.NewRealmLevel <= _lastSeenRealmLevel)
            {
                _lastSeenRealmLevel = evt.NewRealmLevel;
                return;
            }

            if (_lastSeenRealmLevel < 0)
            {
                // 首次进入境界系统：记录但不弹（玩家本来就要从练气期开始）
                _lastSeenRealmLevel = evt.NewRealmLevel;
                return;
            }

            _lastSeenRealmLevel = evt.NewRealmLevel;

            var currentRoot = _root != null ? _root.CurrentRoot : SpiritRootType.None;
            var options = RealmRewardLibrary.Roll3(currentRoot, _takenIds);
            if (options == null || options.Count == 0) return;

            RealmRewardSelectUI.Show(evt.RealmName, options, OnRewardSelected);
        }

        private void OnRewardSelected(RealmReward reward)
        {
            if (reward == null || _player == null) return;
            _takenIds.Add(reward.id);

            try { reward.apply?.Invoke(_player); }
            catch (System.Exception e) { Debug.LogError($"[RealmReward] apply failed: {reward.id} — {e.Message}"); }

            // 飘字反馈
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = _player.transform.position + Vector3.up * 2.6f,
                Damage = 0,
                SpecialTag = $"获得：{reward.displayName}"
            });

            Debug.Log($"<color=#FFD080>[境界突破] 选定奖励：{reward.displayName} —— {reward.description}</color>");
        }
    }
}
