using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府模块与心魔劫的共享视觉工厂 —— 用 LineRenderer + 简单 Primitive 拼出
    /// "符箓 / 光柱 / 轨道粒子 / 地面符印"等氛围元素，不依赖任何 prefab，
    /// 仅靠 procedural geometry + 自发光，保持与 RoomBuilder / FxFactory 一致的"程序化美术"路线。
    ///
    /// 调用方负责把返回的 GameObject 挂到合适的父节点，并控制生命周期；
    /// 大部分 helper 会附挂一个轻量 Behaviour 自动驱动（旋转 / 浮动 / 脉动）。
    /// </summary>
    public static class CaveVfx
    {
        // ============== 地面符印（N 边形 LineRenderer）==============

        /// <summary>
        /// 在地面拉一个 N 边形发光符印（持久存在 · 不自动销毁），常用于法坛 / 阵法台 / 心魔台脚下。
        /// </summary>
        public static GameObject SpawnGroundRune(Transform parent, Vector3 localPos, float radius,
            Color color, int sides = 6, float lineWidth = 0.06f, float yLift = 0.02f)
        {
            var go = new GameObject($"GroundRune_{sides}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + Vector3.up * yLift;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = sides;
            lr.widthMultiplier = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Color bright = new Color(color.r, color.g, color.b, 1f);
            lr.startColor = bright;
            lr.endColor = bright;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            for (int i = 0; i < sides; i++)
            {
                float ang = (i / (float)sides) * Mathf.PI * 2f + Mathf.PI / sides;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, 0, Mathf.Sin(ang) * radius));
            }

            // 缓慢旋转 + 呼吸闪烁
            var spin = go.AddComponent<RuneSpinPulse>();
            spin.Init(color);
            return go;
        }

        /// <summary>
        /// 五角星符印（双层环：外圈圆 + 内部五角星），用于心魔劫这种"邪术"氛围。
        /// </summary>
        public static GameObject SpawnPentagramRune(Transform parent, Vector3 localPos, float radius,
            Color color, float lineWidth = 0.06f)
        {
            var holder = new GameObject("PentagramRune");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos + Vector3.up * 0.02f;

            // 外圆（32 段）
            var ring = new GameObject("OuterRing");
            ring.transform.SetParent(holder.transform, false);
            var lr1 = ring.AddComponent<LineRenderer>();
            lr1.useWorldSpace = false;
            lr1.loop = true;
            lr1.positionCount = 32;
            lr1.widthMultiplier = lineWidth * 0.7f;
            lr1.material = new Material(Shader.Find("Sprites/Default"));
            lr1.startColor = color; lr1.endColor = color;
            lr1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                lr1.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }

            // 内部五角星（按 i + i*2 跳点）
            var star = new GameObject("StarLines");
            star.transform.SetParent(holder.transform, false);
            var lr2 = star.AddComponent<LineRenderer>();
            lr2.useWorldSpace = false;
            lr2.loop = true;
            lr2.positionCount = 5;
            lr2.widthMultiplier = lineWidth;
            lr2.material = new Material(Shader.Find("Sprites/Default"));
            lr2.startColor = color; lr2.endColor = color;
            lr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float r = radius * 0.86f;
            // 五角星：把 5 个顶点按 i*2 mod 5 顺序连
            Vector3[] pts = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float a = (i / 5f) * Mathf.PI * 2f + Mathf.PI / 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < 5; i++) lr2.SetPosition(i, pts[(i * 2) % 5]);

            var spin = holder.AddComponent<RuneSpinPulse>();
            spin.Init(color);
            spin.spinSpeed = -20f;  // 五角星反向缓慢旋转
            return holder;
        }

        // ============== 垂直光柱 ==============

        /// <summary>
        /// 竖直发光光柱（Cylinder 拉长 + 透明叠加），用于法坛中心 / 藏经阁经卷下方。
        /// </summary>
        public static GameObject SpawnLightBeam(Transform parent, Vector3 localPos,
            float height, float baseRadius, Color color, float topThinnessRatio = 0.5f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "LightBeam";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(baseRadius * 2f, height * 0.5f, baseRadius * 2f);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(new Color(color.r, color.g, color.b, 0.18f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 2.6f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var anim = go.AddComponent<BeamPulse>();
            anim.Init(baseRadius, topThinnessRatio);
            return go;
        }

        // ============== 轨道粒子（orbiting） ==============

        /// <summary>
        /// 围绕中心点做水平 / 螺旋轨道运动的发光小颗粒，常用作"灵气围绕"氛围。
        /// </summary>
        public static GameObject SpawnOrbitingParticles(Transform parent, Vector3 localCenter,
            int count, float orbitRadius, float orbitHeight, float particleSize, Color color,
            float orbitSpeed = 40f, float verticalBob = 0.15f)
        {
            var holder = new GameObject("OrbitParticles");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localCenter;

            for (int i = 0; i < count; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = $"OrbitP_{i}";
                p.transform.SetParent(holder.transform, false);
                p.transform.localScale = Vector3.one * particleSize;
                var col = p.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                var rend = p.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = MaterialHelper.CreateLit(color);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", color * 2.4f);
                    }
                    rend.material = mat;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                var orb = p.AddComponent<OrbitalParticle>();
                orb.Init(orbitRadius, orbitHeight, orbitSpeed,
                    (i / (float)count) * Mathf.PI * 2f, verticalBob);
            }
            return holder;
        }

        // ============== 上升烟雾粒子（向上飘 + 渐隐 · 持续生成） ==============

        /// <summary>
        /// 一个持续吐烟的 emitter，每隔 spawnInterval 在底部生成一个小球向上飘升 + 渐隐，
        /// 适合心魔台 / 法坛 / 炉子等"散发气息"的氛围物件。
        /// </summary>
        public static GameObject SpawnSmokeEmitter(Transform parent, Vector3 localPos,
            Color color, float particleSize = 0.18f, float spawnInterval = 0.25f,
            float riseSpeed = 0.5f, float lifetime = 1.4f, float jitterRadius = 0.35f)
        {
            var go = new GameObject("SmokeEmitter");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var emitter = go.AddComponent<SmokeEmitter>();
            emitter.Init(color, particleSize, spawnInterval, riseSpeed, lifetime, jitterRadius);
            return go;
        }

        // ============== 漂浮符牌 / 法宝（带自转） ==============

        /// <summary>
        /// 简单浮悬物件 —— 自动获得 SimpleHover（上下飘 + Y 轴自转），调用方只需指定 primitive 与缩放。
        /// </summary>
        public static GameObject SpawnHoveringObject(Transform parent, Vector3 localPos,
            PrimitiveType type, Vector3 scale, Color color, Color emission,
            float hoverAmp = 0.12f, float hoverFreq = 1.5f, float spinSpeed = 60f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(color, emission);
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var hov = go.AddComponent<RichHover>();
            hov.Init(hoverAmp, hoverFreq, spinSpeed);
            return go;
        }

        // ============== 八卦阵 / 大地烙印（土化身专用 v0.5 Week 7）==============

        /// <summary>
        /// 八卦阵 —— 双层符印（外八角 + 内圆），多用于"扎根 / 镇山"。
        /// 调用方控制其 GameObject 的开关，常驻不销毁；颜色淡入淡出由 BaguaRunePulse 自驱。
        /// </summary>
        public static GameObject SpawnBaguaRune(Transform parent, Vector3 localPos, float radius,
            Color color, float lineWidth = 0.07f)
        {
            var holder = new GameObject("BaguaRune");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos + Vector3.up * 0.02f;

            // 外八角（连续 8 边形）
            var outerGo = new GameObject("Octagon");
            outerGo.transform.SetParent(holder.transform, false);
            var outer = outerGo.AddComponent<LineRenderer>();
            outer.useWorldSpace = false;
            outer.loop = true;
            outer.positionCount = 8;
            outer.widthMultiplier = lineWidth;
            outer.material = new Material(Shader.Find("Sprites/Default"));
            outer.startColor = color;
            outer.endColor = color;
            outer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2f + Mathf.PI / 8f;
                outer.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            // 内圆（32 段）
            var innerGo = new GameObject("InnerCircle");
            innerGo.transform.SetParent(holder.transform, false);
            var inner = innerGo.AddComponent<LineRenderer>();
            inner.useWorldSpace = false;
            inner.loop = true;
            inner.positionCount = 32;
            inner.widthMultiplier = lineWidth * 0.7f;
            inner.material = new Material(Shader.Find("Sprites/Default"));
            inner.startColor = color;
            inner.endColor = color;
            inner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float innerR = radius * 0.55f;
            for (int i = 0; i < 32; i++)
            {
                float a = (i / 32f) * Mathf.PI * 2f;
                inner.SetPosition(i, new Vector3(Mathf.Cos(a) * innerR, 0f, Mathf.Sin(a) * innerR));
            }

            // 中心阴阳鱼简化 —— 一条 S 曲线（用一个细 LineRenderer 画 12 段半圆 + 半圆）
            var taijiGo = new GameObject("Taiji");
            taijiGo.transform.SetParent(holder.transform, false);
            var taiji = taijiGo.AddComponent<LineRenderer>();
            taiji.useWorldSpace = false;
            taiji.positionCount = 25;
            taiji.widthMultiplier = lineWidth * 0.6f;
            taiji.material = new Material(Shader.Find("Sprites/Default"));
            taiji.startColor = color;
            taiji.endColor = color;
            taiji.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float tR = innerR;
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.Lerp(0f, Mathf.PI, i / 12f);
                float x = -tR * 0.5f + Mathf.Cos(a) * tR * 0.5f;
                float z = Mathf.Sin(a) * tR * 0.5f;
                taiji.SetPosition(i, new Vector3(x, 0f, z));
            }
            for (int i = 13; i < 25; i++)
            {
                float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 2f, (i - 12) / 12f);
                float x = tR * 0.5f + Mathf.Cos(a) * tR * 0.5f;
                float z = Mathf.Sin(a) * tR * 0.5f;
                taiji.SetPosition(i, new Vector3(x, 0f, z));
            }

            // 整体缓慢自旋 + 呼吸
            var spin = holder.AddComponent<RuneSpinPulse>();
            spin.spinSpeed = 18f;
            spin.Init(color);
            return holder;
        }

        /// <summary>
        /// 大地烙印 —— 敌人脚下 1 层"土印圆盘"（带向中心收缩的内圈），按层数刷新视觉。
        /// 调用方需自行销毁返回 GameObject（通常挂在敌人 transform 下）。
        /// </summary>
        public static GameObject SpawnEarthSigil(Transform parent, Vector3 localPos, float radius,
            Color color, int stacks)
        {
            var go = new GameObject($"EarthSigil_x{stacks}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + Vector3.up * 0.05f;

            // 外圈
            var outer = go.AddComponent<LineRenderer>();
            outer.useWorldSpace = false;
            outer.loop = true;
            outer.positionCount = 24;
            outer.widthMultiplier = 0.07f;
            outer.material = new Material(Shader.Find("Sprites/Default"));
            outer.startColor = color;
            outer.endColor = color;
            outer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 24; i++)
            {
                float a = (i / 24f) * Mathf.PI * 2f;
                outer.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            // 内圈：每层叠 1 个由外向内的小圈
            for (int s = 0; s < Mathf.Min(stacks, 5); s++)
            {
                var ringGo = new GameObject($"InnerRing_{s}");
                ringGo.transform.SetParent(go.transform, false);
                var lr = ringGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = 18;
                lr.widthMultiplier = 0.035f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                Color rc = new Color(color.r, color.g * 0.85f, color.b * 0.5f, 1f);
                lr.startColor = rc;
                lr.endColor = rc;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float r = radius * Mathf.Lerp(0.9f, 0.25f, s / 4f);
                for (int i = 0; i < 18; i++)
                {
                    float a = (i / 18f) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }

            // 自旋 + 脉动
            var pulse = go.AddComponent<RuneSpinPulse>();
            pulse.spinSpeed = -42f;
            pulse.Init(color);
            return go;
        }

        // ============== 残影 / 飘墨刻（一次性，瞬时生成短寿对象） ==============

        /// <summary>
        /// 在 worldPos 生成一个朝某方向的胶囊残影（带轻微缩放），自动 fade 后销毁。
        /// 用于心魔镜像移动 / 玩家闪避后的"留影"。
        /// </summary>
        public static GameObject SpawnAfterimage(Vector3 worldPos, Quaternion rotation, Vector3 scale,
            Color color, float lifetime = 0.45f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Afterimage";
            go.transform.position = worldPos;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(new Color(color.r, color.g, color.b, 0.45f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 1.4f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var fade = go.AddComponent<AfterimageFade>();
            fade.Init(lifetime);
            return go;
        }
    }

    // ====================================================================
    //                          内部驱动组件
    // ====================================================================

    /// <summary>地面符印自转 + 自发光颜色脉动</summary>
    internal class RuneSpinPulse : MonoBehaviour
    {
        public float spinSpeed = 25f;
        private LineRenderer[] _lrs;
        private Color _baseColor;

        public void Init(Color c)
        {
            _baseColor = c;
            _lrs = GetComponentsInChildren<LineRenderer>();
        }
        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            float k = 0.6f + Mathf.Sin(Time.time * 2.2f) * 0.4f;  // 0.2 ~ 1.0
            Color glow = new Color(_baseColor.r, _baseColor.g, _baseColor.b, k);
            if (_lrs != null)
            {
                foreach (var lr in _lrs)
                {
                    if (lr == null) continue;
                    lr.startColor = glow;
                    lr.endColor = glow;
                }
            }
        }
    }

    /// <summary>光柱脉动 —— Y 轴拉伸 + Alpha 周期变化</summary>
    internal class BeamPulse : MonoBehaviour
    {
        private float _baseRadius;
        private float _topThinness;
        private Renderer _rend;
        private float _baseScaleY;
        private Color _baseColor;
        private float _baseAlpha;

        public void Init(float baseRadius, float topThinness)
        {
            _baseRadius = baseRadius;
            _topThinness = topThinness;
            _rend = GetComponent<Renderer>();
            _baseScaleY = transform.localScale.y;
            if (_rend != null && _rend.material != null)
            {
                _baseColor = _rend.material.color;
                _baseAlpha = _baseColor.a;
            }
        }
        private void Update()
        {
            float k = 0.85f + Mathf.Sin(Time.time * 2.1f) * 0.15f;
            var s = transform.localScale;
            s.y = _baseScaleY * k;
            transform.localScale = s;
            if (_rend != null && _rend.material != null)
            {
                Color c = _baseColor;
                c.a = _baseAlpha * (0.7f + Mathf.Sin(Time.time * 1.7f) * 0.3f);
                _rend.material.color = c;
            }
            transform.Rotate(0f, 18f * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>轨道粒子 —— 围绕父节点做圆周运动 + Y 轴正弦上下漂</summary>
    internal class OrbitalParticle : MonoBehaviour
    {
        private float _radius;
        private float _height;
        private float _speed;
        private float _phase;
        private float _bob;

        public void Init(float radius, float height, float speed, float startPhase, float bob)
        {
            _radius = radius;
            _height = height;
            _speed = speed;
            _phase = startPhase;
            _bob = bob;
        }
        private void Update()
        {
            _phase += _speed * Mathf.Deg2Rad * Time.deltaTime;
            float y = _height + Mathf.Sin(Time.time * 2.4f + _phase) * _bob;
            transform.localPosition = new Vector3(Mathf.Cos(_phase) * _radius, y, Mathf.Sin(_phase) * _radius);
        }
    }

    /// <summary>烟雾发射器 —— 按 interval 生成向上飘升 + 渐隐的小球</summary>
    internal class SmokeEmitter : MonoBehaviour
    {
        private Color _color;
        private float _size;
        private float _interval;
        private float _riseSpeed;
        private float _lifetime;
        private float _jitter;
        private float _timer;

        public void Init(Color color, float size, float interval, float riseSpeed, float lifetime, float jitter)
        {
            _color = color; _size = size; _interval = interval;
            _riseSpeed = riseSpeed; _lifetime = lifetime; _jitter = jitter;
            _timer = 0f;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _interval;
            SpawnOne();
        }

        private void SpawnOne()
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = "Smoke";
            var col = p.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            p.transform.SetParent(transform, false);
            Vector2 j = Random.insideUnitCircle * _jitter;
            p.transform.localPosition = new Vector3(j.x, 0f, j.y);
            p.transform.localScale = Vector3.one * _size;

            var rend = p.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(new Color(_color.r, _color.g, _color.b, 0.5f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", _color * 1.6f);
                }
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var rise = p.AddComponent<SmokeParticle>();
            rise.Init(_lifetime, _riseSpeed, _color, _size);
        }
    }

    /// <summary>烟雾单粒子 —— 向上飘升 + 缓慢放大 + Alpha 渐隐</summary>
    internal class SmokeParticle : MonoBehaviour
    {
        private float _lifetime;
        private float _riseSpeed;
        private Color _color;
        private float _baseSize;
        private float _t;
        private Renderer _rend;

        public void Init(float lifetime, float riseSpeed, Color color, float baseSize)
        {
            _lifetime = Mathf.Max(0.1f, lifetime);
            _riseSpeed = riseSpeed;
            _color = color;
            _baseSize = baseSize;
            _rend = GetComponent<Renderer>();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.localPosition += Vector3.up * _riseSpeed * Time.deltaTime;
            transform.localScale = Vector3.one * _baseSize * Mathf.Lerp(1f, 1.6f, p);
            if (_rend != null && _rend.material != null)
            {
                Color c = _color;
                c.a = Mathf.Lerp(0.55f, 0f, p);
                _rend.material.color = c;
            }
        }
    }

    /// <summary>带自转 + Y 浮动 + 颜色脉动的浮悬物件</summary>
    internal class RichHover : MonoBehaviour
    {
        private float _amp, _freq, _spin;
        private Vector3 _basePos;
        private float _phase;
        private Renderer _rend;
        private Color _baseEmission;
        public void Init(float amp, float freq, float spin)
        {
            _amp = amp; _freq = freq; _spin = spin;
            _basePos = transform.localPosition;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _rend = GetComponent<Renderer>();
            if (_rend != null && _rend.material != null && _rend.material.HasProperty("_EmissionColor"))
                _baseEmission = _rend.material.GetColor("_EmissionColor");
        }
        private void Update()
        {
            transform.Rotate(0f, _spin * Time.deltaTime, 0f, Space.Self);
            transform.localPosition = _basePos + Vector3.up * Mathf.Sin(Time.time * _freq + _phase) * _amp;
            if (_rend != null && _rend.material != null)
            {
                float k = 0.75f + Mathf.Sin(Time.time * 2.3f + _phase) * 0.25f;
                _rend.material.SetColor("_EmissionColor", _baseEmission * k);
            }
        }
    }

    /// <summary>残影渐隐 —— 透明度从 0.55 → 0 + Y 微下沉</summary>
    internal class AfterimageFade : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private Renderer _rend;
        private Color _baseColor;

        public void Init(float lifetime)
        {
            _lifetime = Mathf.Max(0.05f, lifetime);
            _rend = GetComponent<Renderer>();
            if (_rend != null && _rend.material != null)
                _baseColor = _rend.material.color;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.position += Vector3.down * 0.05f * Time.deltaTime;
            if (_rend != null && _rend.material != null)
            {
                Color c = _baseColor;
                c.a = Mathf.Lerp(_baseColor.a, 0f, p);
                _rend.material.color = c;
            }
        }
    }
}
