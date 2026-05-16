using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 程序化 VFX 工厂 —— 不依赖外部 Prefab，仅用 Primitive + LineRenderer + 内置 Shader。
    ///
    /// 目的：在 v0.3.3 阶段为「化身机制 / 技能元素 / 普攻↔技能融合」提供
    /// 一组「一眼能区分」的占位视觉，便于演示项目方向。
    ///
    /// 后续正式美术接入时，把对应 API 替换为 Prefab 实例化即可，不影响调用方。
    /// </summary>
    public static class FxFactory
    {
        // ========== 元素配色（统一调色板，与 SkillModifierApplier.ColorOf 对齐） ==========

        public static Color ElementColor(ElementTag tag) => tag switch
        {
            ElementTag.Fire => new Color(1.0f, 0.42f, 0.10f, 1f),
            ElementTag.Ice => new Color(0.35f, 0.80f, 1.0f, 1f),
            ElementTag.Thunder => new Color(1.0f, 0.95f, 0.30f, 1f),
            ElementTag.Wind => new Color(0.55f, 1.0f, 0.65f, 1f),
            ElementTag.Water => new Color(0.30f, 0.55f, 1.0f, 1f),
            ElementTag.Wood => new Color(0.40f, 0.95f, 0.40f, 1f),
            ElementTag.Earth => new Color(0.85f, 0.70f, 0.40f, 1f),
            ElementTag.Pierce => new Color(0.92f, 0.92f, 0.95f, 1f),
            _ => new Color(1f, 1f, 1f, 1f)
        };

        // 部分元素使用专属几何体以增加辨识度
        public static PrimitiveType ElementShape(ElementTag tag) => tag switch
        {
            ElementTag.Fire => PrimitiveType.Sphere,
            ElementTag.Ice => PrimitiveType.Cube,           // 立方体（晶体感）
            ElementTag.Thunder => PrimitiveType.Cube,       // 锯齿状用 Cube 凑（旋转 + 拉长）
            ElementTag.Wind => PrimitiveType.Capsule,       // 拉长胶囊（气流感）
            ElementTag.Water => PrimitiveType.Sphere,
            ElementTag.Wood => PrimitiveType.Sphere,
            ElementTag.Earth => PrimitiveType.Cube,
            ElementTag.Pierce => PrimitiveType.Capsule,     // 细长（穿刺感）
            _ => PrimitiveType.Sphere
        };

        // ========== 元素爆发：用一颗"主球" + 多个绕飞小球 + AOE 圆环 ==========

        /// <summary>元素爆发特效（落点 1 个主形状 + 4 个绕飞子球 + AOE 圆环）</summary>
        public static void SpawnElementBurst(Vector3 worldPos, ElementTag tag, float radius, float lifetime = 0.6f)
        {
            Color color = ElementColor(tag);

            // 主球
            var main = SpawnPrimitive(worldPos + Vector3.up * 0.4f, ElementShape(tag), radius * 0.45f, color, lifetime, true);
            if (main != null)
            {
                if (tag == ElementTag.Thunder)
                    main.transform.localScale = new Vector3(radius * 0.15f, radius * 0.9f, radius * 0.15f);
                else if (tag == ElementTag.Pierce || tag == ElementTag.Wind)
                    main.transform.localScale = new Vector3(radius * 0.18f, radius * 0.18f, radius * 0.9f);
            }

            // 绕飞 4 子球
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 off = new Vector3(Mathf.Cos(angle), 0.3f, Mathf.Sin(angle)) * radius * 0.6f;
                var sub = SpawnPrimitive(worldPos + off, PrimitiveType.Sphere, radius * 0.22f, color, lifetime * 0.7f, true);
                if (sub != null && tag == ElementTag.Thunder)
                {
                    sub.transform.localScale = new Vector3(radius * 0.08f, radius * 0.4f, radius * 0.08f);
                }
            }

            // 地面圆环
            SpawnAOERing(worldPos + Vector3.up * 0.05f, radius, color, lifetime);
        }

        // ========== 平面圆环（AOE 范围提示） ==========

        public static void SpawnAOERing(Vector3 center, float radius, Color color, float lifetime = 0.6f, int segments = 32)
        {
            var go = new GameObject("AOERing");
            go.transform.position = center;
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = 0.1f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;

            for (int i = 0; i < segments; i++)
            {
                float ang = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius);
            }

            var fade = go.AddComponent<RingFadeAndDestroy>();
            fade.Init(lifetime, color, radius);
        }

        // ========== 剑气直线（金化身流星断 / 普通剑气溅射） ==========

        public static void SpawnSliceLine(Vector3 start, Vector3 direction, float length, Color color, float lifetime = 0.35f)
        {
            var go = new GameObject("SliceLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.18f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.1f);
            lr.SetPosition(0, start + Vector3.up * 0.6f);
            lr.SetPosition(1, start + Vector3.up * 0.6f + direction.normalized * length);

            var fade = go.AddComponent<LineFadeAndDestroy>();
            fade.Init(lifetime, color);
        }

        // ========== 头顶飘字图标（完美收刀 / 剑心通明） ==========

        /// <summary>玩家头顶冒一个金色三角形/小图，向上飘 + 渐隐</summary>
        public static GameObject SpawnHeadHint(Vector3 worldPos, Color color, float size, float lifetime, PrimitiveType shape = PrimitiveType.Sphere)
        {
            var go = SpawnPrimitive(worldPos, shape, size, color, lifetime, true);
            if (go != null)
            {
                var rise = go.AddComponent<RiseAndFade>();
                rise.Init(lifetime, color, 1.5f);
            }
            return go;
        }

        // ========== 敌人头顶种子图标（木化身） ==========

        /// <summary>敌人头顶按 seedCount 在 0.3 半径圆周上摆 N 个绿色小球（跟随 host transform）</summary>
        public static void RefreshHeadSeedIcons(Transform host, int seedCount, Color color, float yOffset = 1.9f)
        {
            if (host == null) return;
            var holder = host.Find("__SeedIcons");
            if (holder == null)
            {
                var go = new GameObject("__SeedIcons");
                go.transform.SetParent(host, false);
                go.transform.localPosition = new Vector3(0f, yOffset, 0f);
                holder = go.transform;
            }

            int existing = holder.childCount;
            // 需要补齐
            while (existing < seedCount)
            {
                var icon = SpawnPrimitive(holder.position, PrimitiveType.Sphere, 0.16f, color, -1f, false);
                if (icon != null)
                {
                    icon.transform.SetParent(holder, false);
                    icon.transform.localPosition = Vector3.zero;
                }
                existing++;
            }
            // 多了就销毁
            while (existing > seedCount)
            {
                var ch = holder.GetChild(existing - 1);
                Object.Destroy(ch.gameObject);
                existing--;
            }
            // 重新排布
            for (int i = 0; i < seedCount; i++)
            {
                float ang = i / (float)Mathf.Max(1, seedCount) * Mathf.PI * 2f;
                var ch = holder.GetChild(i);
                ch.localPosition = new Vector3(Mathf.Cos(ang) * 0.35f, 0f, Mathf.Sin(ang) * 0.35f);
            }
            // 让 holder 在敌人死亡时自动跟随被销毁（child of host，host 销毁时自动 GC）
        }

        public static void ClearHeadSeedIcons(Transform host)
        {
            if (host == null) return;
            var holder = host.Find("__SeedIcons");
            if (holder != null) Object.Destroy(holder.gameObject);
        }

        // ========== 私有 helper ==========

        public static GameObject SpawnPrimitive(Vector3 pos, PrimitiveType type, float size, Color color, float lifetime, bool autoFade)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                rend.material = mat;
            }

            if (autoFade && lifetime > 0f)
            {
                var fade = go.AddComponent<PrimitiveFadeAndDestroy>();
                fade.Init(lifetime, color);
            }
            return go;
        }
    }

    // ========== 内部辅助组件 ==========

    internal class PrimitiveFadeAndDestroy : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private Color _color;
        private Renderer _renderer;
        private Vector3 _baseScale;
        public void Init(float lifetime, Color color)
        {
            _lifetime = Mathf.Max(0.05f, lifetime);
            _color = color;
            _renderer = GetComponent<Renderer>();
            _baseScale = transform.localScale;
        }
        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.localScale = _baseScale * Mathf.Lerp(1f, 1.6f, p);
            transform.Rotate(120f * Time.deltaTime, 60f * Time.deltaTime, 0f, Space.Self);
            if (_renderer != null && _renderer.material != null)
            {
                var c = _color;
                c.a = Mathf.Lerp(_color.a, 0f, p);
                _renderer.material.color = c;
            }
        }
    }

    internal class RingFadeAndDestroy : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private Color _color;
        private LineRenderer _lr;
        private float _baseRadius;
        public void Init(float lifetime, Color color, float baseRadius)
        {
            _lifetime = Mathf.Max(0.05f, lifetime);
            _color = color;
            _baseRadius = baseRadius;
            _lr = GetComponent<LineRenderer>();
        }
        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            // 扩散动画
            float r = Mathf.Lerp(_baseRadius * 0.6f, _baseRadius * 1.15f, p);
            int segs = _lr.positionCount;
            Vector3 c = transform.position;
            for (int i = 0; i < segs; i++)
            {
                float ang = i / (float)segs * Mathf.PI * 2f;
                _lr.SetPosition(i, c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r);
            }
            float a = Mathf.Lerp(_color.a, 0f, p);
            _lr.startColor = new Color(_color.r, _color.g, _color.b, a);
            _lr.endColor = new Color(_color.r, _color.g, _color.b, a);
            _lr.widthMultiplier = Mathf.Lerp(0.12f, 0.02f, p);
        }
    }

    internal class LineFadeAndDestroy : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private Color _color;
        private LineRenderer _lr;
        public void Init(float lifetime, Color color) { _lifetime = Mathf.Max(0.05f, lifetime); _color = color; _lr = GetComponent<LineRenderer>(); }
        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            float a = Mathf.Lerp(_color.a, 0f, p);
            _lr.startColor = new Color(_color.r, _color.g, _color.b, a);
            _lr.endColor = new Color(_color.r, _color.g, _color.b, a * 0.2f);
        }
    }

    internal class RiseAndFade : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private Color _color;
        private Renderer _renderer;
        private float _riseSpeed;
        public void Init(float lifetime, Color color, float riseSpeed)
        {
            _lifetime = Mathf.Max(0.05f, lifetime);
            _color = color;
            _riseSpeed = riseSpeed;
            _renderer = GetComponent<Renderer>();
        }
        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.position += Vector3.up * _riseSpeed * Time.deltaTime;
            transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
            if (_renderer != null && _renderer.material != null)
            {
                var c = _color;
                c.a = Mathf.Lerp(_color.a, 0f, p);
                _renderer.material.color = c;
            }
        }
    }
}
