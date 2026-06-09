using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 化身机制 HUD（v0.3.3 新增）—— 把"机制版化身"的运行时状态可视化：
    ///
    /// - 屏幕中央：完美收刀窗口提示（短闪烁文字 + 渐隐）
    /// - 右上角：当前化身专属状态条
    ///     · 金化身：完美连击计数（×1 / ×2 / ×3 → 剑心通明）
    ///     · 木化身：场上寄生种子总数（统计场内所有敌人身上的种子层数总和）
    ///     · 其他化身：保留为空（v0.4 后续扩展）
    ///
    /// 配合 StatusEffectHUD 一起组成「化身 + StatusEffect」的完整 HUD 体系。
    /// </summary>
    public class SpiritRootMechanicHUD : MonoBehaviour
    {
        private static SpiritRootMechanicHUD _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("SpiritRootMechanicHUD");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpiritRootMechanicHUD>();
        }

        // 完美窗口提示（短暂闪一下"灵压窗口！"）
        private float _windowFlashTimer = 0f;
        private string _windowSource = null;
        private const float WindowFlashDuration = 0.45f;

        // 完美爆发的反馈（连击计数 / 剑心通明）
        private float _perfectFlashTimer = 0f;
        private string _perfectFlashText = null;
        private bool _enteredSwordHeart = false;
        private const float PerfectFlashDuration = 0.8f;

        // 寄生种子计数缓存（每帧从场内敌人聚合）
        private int _totalSeedCount = 0;
        private int _totalWaterMarkCount = 0;
        private float _seedSampleTimer = 0f;
        private const float SeedSampleInterval = 0.2f;

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.PerfectStrikeWindowOpened>(OnWindow);
            GameEvents.Subscribe<GameEvents.PerfectStrikeTriggered>(OnPerfect);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.PerfectStrikeWindowOpened>(OnWindow);
            GameEvents.Unsubscribe<GameEvents.PerfectStrikeTriggered>(OnPerfect);
        }

        private void OnWindow(GameEvents.PerfectStrikeWindowOpened evt)
        {
            _windowFlashTimer = WindowFlashDuration;
            _windowSource = evt.SourceTag;
        }

        private void OnPerfect(GameEvents.PerfectStrikeTriggered evt)
        {
            _perfectFlashTimer = PerfectFlashDuration;
            _enteredSwordHeart = evt.EnteredSwordHeart;
            _perfectFlashText = evt.EnteredSwordHeart
                ? "★剑心通明！★"
                : $"完美 ×{evt.ConsecutiveCount}";
        }

        private void Update()
        {
            if (_windowFlashTimer > 0f) _windowFlashTimer -= Time.deltaTime;
            if (_perfectFlashTimer > 0f) _perfectFlashTimer -= Time.deltaTime;

            // 周期采样：场内寄生种子 / 水痕印总数
            _seedSampleTimer -= Time.deltaTime;
            if (_seedSampleTimer <= 0f)
            {
                _seedSampleTimer = SeedSampleInterval;
                SampleActiveMarks();
            }
        }

        private void SampleActiveMarks()
        {
            _totalSeedCount = 0;
            _totalWaterMarkCount = 0;
            // 简化实现：扫描场内所有 StatusEffectController（每 0.2s 采样一次，性能不敏感）
            var controllers = FindObjectsOfType<StatusEffectController>();
            foreach (var ctrl in controllers)
            {
                var seed = ctrl.Get(SpiritRootWoodController.ParasiteSeedEffectId);
                if (seed != null) _totalSeedCount += seed.stacks;
                var mark = ctrl.Get(SpiritRootWaterController.WaterMarkEffectId);
                if (mark != null) _totalWaterMarkCount += mark.stacks;
            }
        }

        private void OnGUI()
        {
            if (MainMenu.IsVisible) return;   // 主菜单(UITK)时不画游戏内 HUD
            var player = PlayerController.Instance;
            if (player == null) return;
            var root = player.GetComponent<SpiritRootController>();

            DrawWindowFlash();
            DrawPerfectFlash();
            DrawRootPanel(root);
        }

        // ========== 屏幕中央 - 灵压窗口闪烁提示 ==========

        private void DrawWindowFlash()
        {
            if (_windowFlashTimer <= 0f) return;
            float alpha = Mathf.Clamp01(_windowFlashTimer / WindowFlashDuration);

            float w = 280f, h = 50f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.42f, w, h);

            var bg = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.18f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // 边框
            GUI.color = new Color(1f, 0.85f, 0.2f, alpha);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2f, rect.width, 2f), Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.3f, alpha) }
            };
            string label = "灵压窗口！按【左键】触发完美收刀";
            if (!string.IsNullOrEmpty(_windowSource))
                label += $"  · {_windowSource}";
            GUI.Label(rect, label, style);

            GUI.color = bg;
        }

        // ========== 屏幕中央偏下 - 完美爆发反馈飘字 ==========

        private void DrawPerfectFlash()
        {
            if (_perfectFlashTimer <= 0f) return;
            float p = 1f - (_perfectFlashTimer / PerfectFlashDuration);
            float alpha = Mathf.Clamp01(_perfectFlashTimer / PerfectFlashDuration);
            float fontSize = Mathf.Lerp(38f, 60f, p);

            float w = 360f, h = 80f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.55f + p * 30f, w, h);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(fontSize),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _enteredSwordHeart
                    ? new Color(1f, 1f, 0.6f, alpha)
                    : new Color(1f, 0.85f, 0.25f, alpha) }
            };
            GUI.Label(rect, _perfectFlashText ?? "完美！", style);
        }

        // ========== 右上角 - 化身专属面板 ==========

        private void DrawRootPanel(SpiritRootController root)
        {
            if (root == null) return;
            var def = root.CurrentDef;
            if (def == null) return;

            float w = 240f;
            float x = Screen.width - w - 12f;
            float y = 96f;

            // 背景
            var bg = GUI.color;
            GUI.color = new Color(def.displayColor.r * 0.25f, def.displayColor.g * 0.25f, def.displayColor.b * 0.25f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, 100f), Texture2D.whiteTexture);

            // 顶色条
            GUI.color = def.displayColor;
            GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
            GUI.color = bg;

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = def.displayColor }
            };
            GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 20f), $"{def.name} · {def.mechanicTitle ?? "—"}", titleStyle);

            // 副词条 / 机制版状态
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 0.85f) }
            };
            string mechFlag = def.mechanicEnabled ? "[机制已落地]" : "[仅副词条层]";
            GUI.Label(new Rect(x + 10f, y + 26f, w - 20f, 20f), mechFlag, subStyle);

            // 内容随化身变化
            switch (root.CurrentRoot)
            {
                case SpiritRootType.Metal:
                    DrawGoldStatus(root, x, y + 48f, w);
                    break;
                case SpiritRootType.Wood:
                    DrawWoodStatus(x, y + 48f, w);
                    break;
                case SpiritRootType.Water:
                    DrawWaterStatus(root, x, y + 48f, w);
                    break;
                case SpiritRootType.Fire:
                    DrawFireStatus(root, x, y + 48f, w);
                    break;
                default:
                    GUI.Label(new Rect(x + 10f, y + 48f, w - 20f, 40f),
                        "(此化身的机制版尚未实现，仅副词条生效)",
                        subStyle);
                    break;
            }
        }

        private void DrawGoldStatus(SpiritRootController root, float x, float y, float w)
        {
            var gold = root.GetComponent<SpiritRootGoldController>();
            if (gold == null) return;

            // 连击计数
            int cc = gold.ConsecutivePerfects;
            string perfectStr = $"完美连击：{cc}/3";
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = cc > 0 ? new Color(1f, 0.95f, 0.4f, 1f) : new Color(0.85f, 0.85f, 0.85f, 0.75f) }
            };
            GUI.Label(new Rect(x + 10f, y, w - 20f, 18f), perfectStr, style);

            // 连击进度条
            var barRect = new Rect(x + 10f, y + 20f, w - 20f, 8f);
            var bg = GUI.color;
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            float prog = Mathf.Clamp01(cc / 3f);
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * prog, barRect.height), Texture2D.whiteTexture);
            GUI.color = bg;

            // 窗口状态
            if (gold.IsWindowOpen)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.85f);
                GUI.DrawTexture(new Rect(x + 6f, y + 32f, w - 12f, 14f), Texture2D.whiteTexture);
                GUI.color = bg;
                var winStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.black }
                };
                GUI.Label(new Rect(x + 6f, y + 32f, w - 12f, 14f), "■ 灵压窗口开启中 ■", winStyle);
            }
        }

        private void DrawWoodStatus(float x, float y, float w)
        {
            string label = $"场上寄生种子：{_totalSeedCount} 颗";
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = _totalSeedCount > 0 ? new Color(0.5f, 1f, 0.5f, 1f) : new Color(0.85f, 0.85f, 0.85f, 0.7f) }
            };
            GUI.Label(new Rect(x + 10f, y, w - 20f, 18f), label, style);

            var tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.7f, 1f, 0.7f, 0.8f) },
                wordWrap = true
            };
            GUI.Label(new Rect(x + 10f, y + 20f, w - 20f, 32f),
                "普攻种种子 · 技能引爆 ×0.5/颗 AOE",
                tipStyle);
        }

        private void DrawFireStatus(SpiritRootController root, float x, float y, float w)
        {
            var fire = root.GetComponent<SpiritRootFireController>();
            if (fire == null) return;

            // 怒气条
            string label = fire.InFrenzy
                ? $"★ 狂火中：{fire.FrenzyTimer:F1}s 剩余 ★"
                : $"怒气：{fire.CurrentRage}/{fire.MaxRage}";
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = fire.InFrenzy ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = fire.InFrenzy
                    ? new Color(1f, 0.7f, 0.2f, 1f)
                    : new Color(1f, 0.45f, 0.2f, 0.95f) }
            };
            GUI.Label(new Rect(x + 10f, y, w - 20f, 18f), label, style);

            // 怒气进度条
            var barRect = new Rect(x + 10f, y + 20f, w - 20f, 10f);
            var bg = GUI.color;
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            float prog;
            Color barColor;
            if (fire.InFrenzy)
            {
                // 狂火期间显示剩余时长比例（先按 5s 满刻度做粗略可视化）
                prog = Mathf.Clamp01(fire.FrenzyTimer / 6f);
                barColor = new Color(1f, 0.7f, 0.2f, 0.95f);
            }
            else
            {
                prog = (float)fire.CurrentRage / Mathf.Max(1, fire.MaxRage);
                // 满 50 后变金色提示可开狂火
                barColor = fire.CurrentRage >= 50
                    ? new Color(1f, 0.65f, 0.1f, 0.95f)
                    : new Color(0.8f, 0.35f, 0.15f, 0.85f);
            }
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * prog, barRect.height), Texture2D.whiteTexture);
            GUI.color = bg;

            // 提示
            if (!fire.InFrenzy && fire.CurrentRage >= 50)
            {
                var tip = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.85f, 0.3f, 1f) }
                };
                GUI.Label(new Rect(x + 10f, y + 34f, w - 20f, 16f), "▶ 按 [V] 开启狂火！", tip);
            }
            else if (!fire.InFrenzy)
            {
                var tip = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.85f, 0.6f, 0.4f, 0.8f) }
                };
                GUI.Label(new Rect(x + 10f, y + 34f, w - 20f, 16f), $"满 50 即可按 [V] 开狂火（时长=怒气÷20）", tip);
            }
            else
            {
                var tip = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.95f, 0.7f, 1f) }
                };
                GUI.Label(new Rect(x + 10f, y + 34f, w - 20f, 16f), "技能 CD ×0.7 / 攻速 +50% / 移速 +30%", tip);
            }
        }

        private void DrawWaterStatus(SpiritRootController root, float x, float y, float w)
        {
            var water = root.GetComponent<SpiritRootWaterController>();

            // 影息蓄势进度条（闪避后 0.4s 内）
            string label = water != null && water.IsShadowStrikeReady
                ? "▶ 影息蓄势中 · 下一击 ×2"
                : "(闪避后下一击触发影息斩)";
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = water != null && water.IsShadowStrikeReady ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = water != null && water.IsShadowStrikeReady
                    ? new Color(0.5f, 0.95f, 1f, 1f)
                    : new Color(0.85f, 0.85f, 0.85f, 0.7f) }
            };
            GUI.Label(new Rect(x + 10f, y, w - 20f, 18f), label, style);

            // 进度条
            if (water != null && water.IsShadowStrikeReady)
            {
                var barRect = new Rect(x + 10f, y + 20f, w - 20f, 8f);
                var bg = GUI.color;
                GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);

                GUI.color = new Color(0.3f, 0.7f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * water.ShadowWindowProgress, barRect.height), Texture2D.whiteTexture);
                GUI.color = bg;
            }

            // 水痕计数
            string markLabel = $"场上水痕印：{_totalWaterMarkCount} 个";
            var markStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = _totalWaterMarkCount > 0 ? new Color(0.5f, 0.95f, 1f, 1f) : new Color(0.85f, 0.85f, 0.85f, 0.65f) }
            };
            GUI.Label(new Rect(x + 10f, y + 32f, w - 20f, 16f), markLabel, markStyle);
        }
    }
}
