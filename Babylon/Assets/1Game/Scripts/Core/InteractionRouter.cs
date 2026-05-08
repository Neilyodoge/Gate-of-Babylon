using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 玩家可交互对象的统一接口。所有按 F 触发的对象（拾取、商店、铸炼台、出口…）都应实现此接口
    /// 并在「玩家进入范围」时调用 <see cref="InteractionRouter.Register"/>，离开时 Unregister。
    /// 路由器每帧按 (优先级 desc, 距离 asc) 选出唯一 active，避免多个交互体同时响应 F。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>用于「最近距离」比较的世界坐标</summary>
        Vector3 InteractionWorldPos { get; }

        /// <summary>
        /// 优先级，高的永远胜过低的；同级用距离决胜。
        /// 建议梯度：商店 40 / 升级台 35 / 功法拾取 25 / 灵物拾取 20 / 房间出口 5。
        /// </summary>
        int InteractionPriority { get; }

        /// <summary>当前是否仍可被选中（玩家在范围内 && 未被消耗）</summary>
        bool IsInteractionAvailable { get; }

        /// <summary>由路由器写入。值为 true 时该对象应处理 F 输入并显示交互提示，false 时应静默</summary>
        bool IsRoutedActive { get; set; }
    }

    /// <summary>
    /// F 键交互路由器：解决多个 IInteractable 重叠时 F 同时触发多个对象的问题。
    ///
    /// 用法：
    ///   - 交互体在 OnTriggerEnter(Player) 时 InteractionRouter.Register(this)
    ///   - 在 OnTriggerExit(Player) 或 OnDestroy 时 InteractionRouter.Unregister(this)
    ///   - 处理 F 输入前判断 InteractionRouter.IsActive(this)
    ///   - 显示/隐藏提示 UI 时根据 IsRoutedActive 切换
    /// 解析逻辑由 <see cref="InteractionRouterDriver"/> 在 LateUpdate 调用。
    /// </summary>
    public static class InteractionRouter
    {
        private static readonly List<IInteractable> _candidates = new();
        private static IInteractable _active;

        /// <summary>当前被选中的交互对象（可能为 null）</summary>
        public static IInteractable Active => _active;

        public static bool IsActive(IInteractable i) => _active != null && ReferenceEquals(_active, i);

        public static void Register(IInteractable i)
        {
            if (i == null) return;
            if (!_candidates.Contains(i))
                _candidates.Add(i);
        }

        public static void Unregister(IInteractable i)
        {
            if (i == null) return;
            _candidates.Remove(i);
            if (ReferenceEquals(_active, i))
            {
                i.IsRoutedActive = false;
                _active = null;
            }
        }

        /// <summary>每帧由 driver 调用，根据玩家位置选出唯一 active</summary>
        public static void Resolve(Vector3 playerPos)
        {
            IInteractable best = null;
            int bestPri = int.MinValue;
            float bestDistSqr = float.MaxValue;

            // 反向遍历以便顺手剔除被销毁的元素
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                var c = _candidates[i];
                // Unity 的 MonoBehaviour 被 Destroy 后 == null 仍为 true（伪空），用 ReferenceEquals 配合 Unity 重载
                if (c == null || (c is Object uo && uo == null))
                {
                    _candidates.RemoveAt(i);
                    continue;
                }
                if (!c.IsInteractionAvailable) continue;

                Vector3 p = c.InteractionWorldPos;
                float distSqr = (p - playerPos).sqrMagnitude;
                int pri = c.InteractionPriority;

                bool better = pri > bestPri || (pri == bestPri && distSqr < bestDistSqr);
                if (better)
                {
                    best = c;
                    bestPri = pri;
                    bestDistSqr = distSqr;
                }
            }

            if (!ReferenceEquals(_active, best))
            {
                if (_active != null) _active.IsRoutedActive = false;
                _active = best;
                if (_active != null) _active.IsRoutedActive = true;
            }
            else if (best != null)
            {
                // 同一个 active 维持状态（某些对象可能被外部改写过）
                best.IsRoutedActive = true;
            }
        }

        /// <summary>场景切换时清理（防止跨场景的旧引用残留）</summary>
        public static void Clear()
        {
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i] != null) _candidates[i].IsRoutedActive = false;
            _candidates.Clear();
            _active = null;
        }
    }

    /// <summary>
    /// 驱动器：每帧 Update 早期解析一次路由结果。
    /// 执行顺序设为 -10000，确保在所有交互体的 Update 之前运行 ——
    /// 这样物理回调（OnTriggerEnter）注册后，本帧 Update 里 IsRoutedActive 就已是正确值，
    /// 玩家走进范围当帧就能按 F。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class InteractionRouterDriver : MonoBehaviour
    {
        private static InteractionRouterDriver _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("[InteractionRouterDriver]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InteractionRouterDriver>();
        }

        private void Update()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            InteractionRouter.Resolve(player.transform.position);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
        }
    }
}
