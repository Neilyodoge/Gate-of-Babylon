using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 出梦前的"携丹"过桥系统（v0.5 洞府 ↔ 梦境联通点）。
    ///
    /// 流程：
    /// 1. 玩家走近山门按 F → <see cref="VillagePortal"/> 先弹出 <see cref="PillCarryUI"/>
    /// 2. 玩家从洞府丹药库存中选 0~MaxCarry 颗 → 点确认
    /// 3. <see cref="PendingPillCarry.Commit"/> 扣洞府库存 + 把丹药放入 <see cref="ActiveCarry"/>
    /// 4. 入梦后玩家可按【G】键消耗一颗（即时回血 / 攻击 buff，依丹药名而定）
    ///
    /// MVP 实现：所有丹药都按"回血 40% MaxHp"处理，后续根据丹药名差异化。
    /// </summary>
    public static class PendingPillCarry
    {
        public const int MaxCarry = 3;

        /// <summary>本次入梦计划携带的丹药（pillName → count）</summary>
        private static readonly Dictionary<string, int> _pending = new();

        /// <summary>当前正在使用的携丹库存（入梦后被消耗）</summary>
        public static readonly Dictionary<string, int> ActiveCarry = new();

        public static IReadOnlyDictionary<string, int> Pending => _pending;

        public static int TotalPending
        {
            get
            {
                int t = 0; foreach (var kv in _pending) t += kv.Value; return t;
            }
        }

        public static int TotalActive
        {
            get { int t = 0; foreach (var kv in ActiveCarry) t += kv.Value; return t; }
        }

        public static void AddPending(string pillName)
        {
            if (TotalPending >= MaxCarry) return;
            if (_pending.ContainsKey(pillName)) _pending[pillName]++;
            else _pending[pillName] = 1;
        }

        public static void RemovePending(string pillName)
        {
            if (!_pending.ContainsKey(pillName)) return;
            _pending[pillName]--;
            if (_pending[pillName] <= 0) _pending.Remove(pillName);
        }

        public static void ClearPending() => _pending.Clear();

        /// <summary>从 _pending 把丹药扣洞府库存，搬到 ActiveCarry（入梦前一刻调用）</summary>
        public static void Commit()
        {
            ActiveCarry.Clear();
            foreach (var kv in _pending)
            {
                // 扣洞府库存
                int can = SaveSystem.Instance.GetCaveItemCount(kv.Key);
                int n = Mathf.Min(can, kv.Value);
                if (n <= 0) continue;
                SaveSystem.Instance.ConsumeCaveItem(kv.Key, n);
                ActiveCarry[kv.Key] = n;
            }
            SaveSystem.Instance.Save();
            _pending.Clear();
        }

        /// <summary>消耗一颗 ActiveCarry 中的丹药（返回是否成功）</summary>
        public static bool ConsumeOne(out string consumedPillName)
        {
            consumedPillName = null;
            foreach (var kv in ActiveCarry)
            {
                consumedPillName = kv.Key;
                break;
            }
            if (consumedPillName == null) return false;
            ActiveCarry[consumedPillName]--;
            if (ActiveCarry[consumedPillName] <= 0) ActiveCarry.Remove(consumedPillName);
            return true;
        }

        /// <summary>梦醒（撤离成功 / 死亡）时清空 ActiveCarry 防止穿越</summary>
        public static void ClearActive() => ActiveCarry.Clear();

        /// <summary>列出洞府库存中所有可携带的丹药 itemName（优先按 SO category=Pill 判断，无 SO 时按 itemName 兜底）</summary>
        public static List<string> ListAvailablePills()
        {
            var list = new List<string>();
            foreach (var e in SaveSystem.Instance.Data.caveInventory)
            {
                if (e.count <= 0) continue;
                var so = CaveMaterialPool.GetByName(e.itemName);
                if (so != null)
                {
                    if (so.category == ItemCategory.Pill) list.Add(e.itemName);
                }
                else if (e.itemName.Contains("丹") && !e.itemName.Contains("灵药") && !e.itemName.Contains("种子"))
                {
                    list.Add(e.itemName);  // 旧字符串兜底
                }
            }
            return list;
        }
    }

    /// <summary>
    /// 携丹 IMGUI 选择面板。玩家在山门按 F 后由 VillagePortal 调出。
    /// </summary>
    public class PillCarryUI : MonoBehaviour
    {
        private static PillCarryUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private System.Action _onConfirm;
        private System.Action _onCancel;

        public static void Show(System.Action onConfirm, System.Action onCancel)
        {
            if (_instance == null)
            {
                var go = new GameObject("PillCarryUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PillCarryUI>();
            }
            _instance._visible = true;
            _instance._onConfirm = onConfirm;
            _instance._onCancel = onCancel;
            PendingPillCarry.ClearPending();
        }

        public static void Hide()
        {
            if (_instance != null) _instance._visible = false;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            const float W = 480f, H = 360f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.65f);
            GUILayout.Label("⚱ 携丹入梦", titleStyle);
            GUILayout.Space(4);

            GUILayout.Label($"已选 {PendingPillCarry.TotalPending} / {PendingPillCarry.MaxCarry} 颗");
            GUILayout.Space(6);

            var pills = PendingPillCarry.ListAvailablePills();
            if (pills.Count == 0)
            {
                GUILayout.Label("洞府无丹药 · 先在炼丹房烧炼");
            }
            else
            {
                foreach (var p in pills)
                {
                    int caveCount = SaveSystem.Instance.GetCaveItemCount(p);
                    PendingPillCarry.Pending.TryGetValue(p, out int picked);

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"{p}（库存 {caveCount}）", GUILayout.Width(220));
                    GUILayout.Label($"已选 {picked}", GUILayout.Width(60));
                    GUI.enabled = picked > 0;
                    if (GUILayout.Button("-", GUILayout.Width(30))) PendingPillCarry.RemovePending(p);
                    GUI.enabled = picked < caveCount && PendingPillCarry.TotalPending < PendingPillCarry.MaxCarry;
                    if (GUILayout.Button("+", GUILayout.Width(30))) PendingPillCarry.AddPending(p);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("梦中按【G】消耗一颗 · 即时回 40% 最大生命", new GUIStyle(GUI.skin.label) { fontSize = 11 });

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消 [ESC]", GUILayout.Height(34)))
            {
                _visible = false;
                _onCancel?.Invoke();
            }
            if (GUILayout.Button("入梦 [Enter]", GUILayout.Height(34)))
            {
                _visible = false;
                _onConfirm?.Invoke();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape) { _visible = false; _onCancel?.Invoke(); }
                else if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                { _visible = false; _onConfirm?.Invoke(); }
            }
        }
    }
}
