using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>掉落物提示面板的内容数据（标题 / 副行 / 描述 / 操作提示）。</summary>
    public struct PickupPromptData
    {
        public string title;
        public Color titleColor;
        public string subLine;    // 可选：效果 / 类型信息
        public Color subColor;
        public string desc;       // 可选：描述
        public string promptHint; // 底部操作提示
    }

    /// <summary>构建出的提示面板句柄：根物体 + 长按进度条 + 提示文字（供调用方更新/销毁）。</summary>
    public class WorldPromptHandle
    {
        public GameObject root;
        public Image holdFill;
        public Text promptText;
    }

    /// <summary>
    /// 掉落物世界空间提示面板的统一构建器（v0.5.5 重构）——
    /// ItemPickup / SkillPickup 共用，消除两边各自手搓 Canvas 的重复。
    ///
    /// 注意：面板**不挂任何父物体**（根节点世界空间），由调用方每帧同步位置——
    /// 避免继承拾取物的自转 / 缩放（否则朝向乱、被压扁，见 v0.5.5 修复）。
    /// </summary>
    public static class WorldPromptPanel
    {
        public static WorldPromptHandle Build(Vector3 worldPos, in PickupPromptData d)
        {
            var canvasGo = new GameObject("WorldPromptCanvas");
            canvasGo.transform.position = worldPos;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 200);
            rt.localScale = Vector3.one * 0.00875f;

            // 背景
            var bg = AddImage(canvasGo.transform, "Bg", Vector2.zero, Vector2.one);
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

            // 标题（带描边）
            var title = AddText(canvasGo.transform, "Title", new Vector2(0, 0.68f), new Vector2(1, 1f), 28, d.titleColor, true);
            title.text = d.title;
            var ol = title.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.9f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            // 副行（效果 / 类型）—— 可选
            if (!string.IsNullOrEmpty(d.subLine))
            {
                var sub = AddText(canvasGo.transform, "Sub", new Vector2(0, 0.52f), new Vector2(1, 0.68f), 16, d.subColor, false);
                sub.text = d.subLine;
                sub.supportRichText = true;
            }

            // 描述 —— 可选
            if (!string.IsNullOrEmpty(d.desc))
            {
                var desc = AddText(canvasGo.transform, "Desc", new Vector2(0, 0.30f), new Vector2(1, 0.52f), 18, new Color(0.78f, 0.78f, 0.78f, 0.9f), false);
                desc.text = d.desc;
            }

            // 操作提示
            var prompt = AddText(canvasGo.transform, "Prompt", new Vector2(0, 0.12f), new Vector2(1, 0.30f), 18, new Color(0.6f, 0.8f, 1f, 0.95f), false);
            prompt.text = d.promptHint;

            // 长按进度条
            var holdBg = AddImage(canvasGo.transform, "HoldBg", new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.10f));
            holdBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            var holdFill = AddImage(holdBg.transform, "HoldFill", Vector2.zero, Vector2.one);
            holdFill.color = new Color(1f, 0.4f, 0.2f, 0.85f);
            holdFill.type = Image.Type.Filled;
            holdFill.fillMethod = Image.FillMethod.Horizontal;
            holdFill.fillAmount = 0f;

            // 朝向相机（与场上其它世界 UI 一致）
            canvasGo.AddComponent<BillboardUI>().lerpFactor = 0.5f;

            return new WorldPromptHandle { root = canvasGo, holdFill = holdFill, promptText = prompt };
        }

        private static Text AddText(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            int fontSize, Color color, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, 0);
            var t = go.AddComponent<Text>();
            t.font = UIBuiltins.LegacyFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }

        private static Image AddImage(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.AddComponent<Image>();
        }
    }

    /// <summary>
    /// Billboard：让世界空间 UI 朝向相机。lerpFactor 0=不转 / 1=完全朝向相机。
    /// （原定义在 SkillPickup.cs，v0.5.5 重构搬到此处共用。）
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        [Tooltip("朝向相机的插值：0.4≈轻微朝向，0.7≈强烈朝向")]
        public float lerpFactor = 0.5f;

        private Quaternion _initialRotation;
        private bool _initialized;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (!_initialized)
            {
                _initialRotation = transform.rotation;
                _initialized = true;
            }

            Quaternion lookAtCam = Quaternion.LookRotation(transform.position - cam.transform.position);
            transform.rotation = Quaternion.Slerp(_initialRotation, lookAtCam, lerpFactor);
        }
    }
}
