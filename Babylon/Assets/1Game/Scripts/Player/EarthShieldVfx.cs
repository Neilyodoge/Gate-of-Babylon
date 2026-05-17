using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 土化身 · 地脉护盾视觉（v0.5 Week 7）
    ///
    /// 围绕玩家身体（默认头顶~腰部之间）水平旋转 N 块土黄色岩石板，
    /// 每一块代表一层 EarthShield 库存。
    ///
    /// - <see cref="SetStackCount"/> 重设当前层数，按需补 / 砍。
    /// - <see cref="ShatterOuterMost"/> 让最外一块"炸裂"成 6 颗小石块朝外飞散并消失。
    ///
    /// 配色 / 形态完全 procedural，无 prefab 依赖。
    /// </summary>
    public class EarthShieldVfx : MonoBehaviour
    {
        private const float OrbitRadius = 1.05f;
        private const float OrbitHeight = 0.05f;   // 在 transform.localPosition 基础上的额外高度
        private const float SpinSpeed = 55f;       // 整组板的自转速度（度/秒）

        private Color _color;
        private readonly List<GameObject> _plates = new List<GameObject>();
        private int _targetCount;
        private float _phase;

        public void Init(Color color)
        {
            _color = color;
            _phase = 0f;
        }

        public void SetStackCount(int count)
        {
            count = Mathf.Max(0, count);
            _targetCount = count;

            // 砍掉多余的板（直接销毁，不动画）
            while (_plates.Count > count)
            {
                int idx = _plates.Count - 1;
                var p = _plates[idx];
                _plates.RemoveAt(idx);
                if (p != null) Destroy(p);
            }
            // 补足新板
            while (_plates.Count < count)
            {
                _plates.Add(BuildPlate(_plates.Count));
            }
            // 重新分配角度
            ReassignAngles();
        }

        /// <summary>让"最外一块"（视觉上最显眼的一块）炸裂飞散</summary>
        public void ShatterOuterMost()
        {
            if (_plates.Count == 0) return;
            int idx = _plates.Count - 1;
            var p = _plates[idx];
            _plates.RemoveAt(idx);
            if (p == null) return;

            // 1) 在该位置产生 6 颗小石块朝外飞 + AOE 圆环
            Vector3 worldPos = p.transform.position;
            for (int i = 0; i < 6; i++)
            {
                float ang = (i / 6f) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), Random.Range(0.2f, 0.7f), Mathf.Sin(ang));
                var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frag.name = "ShieldFragment";
                frag.transform.position = worldPos;
                frag.transform.rotation = Random.rotationUniform;
                frag.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
                var col = frag.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = frag.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = MaterialHelper.CreateLitEmissive(_color * 0.6f, _color * 1.2f);
                    rend.material = mat;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var fly = frag.AddComponent<EarthFragmentFly>();
                fly.Init(dir * Random.Range(2.5f, 4.5f), 0.7f);
            }

            FxFactory.SpawnAOERing(transform.position + Vector3.up * 0.1f, OrbitRadius * 1.4f,
                _color, lifetime: 0.45f);

            Destroy(p);
            // 更新角度让剩余的板补位
            ReassignAngles();
        }

        // ============================== 旋转更新 ==============================

        private void Update()
        {
            _phase += SpinSpeed * Mathf.Deg2Rad * Time.deltaTime;
            int n = _plates.Count;
            if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                var p = _plates[i];
                if (p == null) continue;
                float baseAngle = (i / (float)n) * Mathf.PI * 2f;
                float a = baseAngle + _phase;
                Vector3 pos = new Vector3(Mathf.Cos(a) * OrbitRadius, OrbitHeight, Mathf.Sin(a) * OrbitRadius);
                p.transform.localPosition = pos;
                // 板的"朝向"：板面朝外（法线沿 radial 方向）
                p.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
        }

        // ============================== 板的几何 ==============================

        private GameObject BuildPlate(int index)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = $"ShieldPlate_{index}";
            plate.transform.SetParent(transform, false);
            // 长宽厚比例：宽 0.45 / 高 0.55 / 厚 0.08，营造"石板"质感
            plate.transform.localScale = new Vector3(0.45f, 0.55f, 0.08f);
            var col = plate.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = plate.GetComponent<Renderer>();
            if (rend != null)
            {
                // 主色 = 土黄偏深，自发光 = 浅金（呼吸感由 ShaderGraph 没法走，简化为静态）
                Color body = new Color(_color.r * 0.55f, _color.g * 0.50f, _color.b * 0.32f, 1f);
                Color emit = new Color(_color.r, _color.g * 0.9f, _color.b * 0.55f, 1f) * 0.6f;
                var mat = MaterialHelper.CreateLitEmissive(body, emit);
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return plate;
        }

        private void ReassignAngles()
        {
            // 真正位置在 Update 里按 phase 重算，这里只是触发一次立即重排
            _phase = _phase % (Mathf.PI * 2f);
        }
    }

    /// <summary>护盾岩石板碎块 —— 朝指定方向直线飞 + 缩小消失</summary>
    internal class EarthFragmentFly : MonoBehaviour
    {
        private Vector3 _vel;
        private float _life;
        private float _t;
        private Vector3 _spin;

        public void Init(Vector3 vel, float life)
        {
            _vel = vel;
            _life = Mathf.Max(0.1f, life);
            _t = 0f;
            _spin = new Vector3(Random.Range(-360f, 360f),
                                Random.Range(-360f, 360f),
                                Random.Range(-360f, 360f));
        }

        private Vector3 _baseScale;
        private bool _baseScaleCaptured;

        private void Update()
        {
            if (!_baseScaleCaptured)
            {
                _baseScale = transform.localScale;
                _baseScaleCaptured = true;
            }
            _t += Time.deltaTime;
            float p = _t / _life;
            if (p >= 1f) { Destroy(gameObject); return; }
            transform.position += _vel * Time.deltaTime;
            transform.Rotate(_spin * Time.deltaTime, Space.Self);
            // 整段生命期：从 baseScale 缩到 0.05x，匀速渐隐
            transform.localScale = _baseScale * Mathf.Lerp(1f, 0.05f, p);
        }
    }
}
