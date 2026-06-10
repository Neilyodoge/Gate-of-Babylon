using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 天地大挪移 · 乾坤倒转状态（默认 10 秒）。
    /// - 敌人对你造成的伤害（含投射物）→ 反弹给来源/最近敌人，你免疫（"敌人投射物变为你的攻击"）。
    /// - 你的普攻不再伤敌，而是按比例治疗自身（"你的攻击变为治疗"）。
    /// 由 <see cref="PlayerController.OnDamage"/> 与 <see cref="PlayerCombat"/> 读取 <see cref="IsActive"/>。
    /// </summary>
    public class HeavenEarthShift : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        private float _timer;
        private GameObject _aura;

        public void Activate(float duration)
        {
            _timer = Mathf.Max(_timer, Mathf.Max(1f, duration));
            IsActive = true;
            EnsureAura();
        }

        private void Update()
        {
            if (_timer > 0f)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    IsActive = false;
                    DestroyAura();
                }
                else if (_aura != null)
                {
                    float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
                    var lr = _aura.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        Color c = new Color(0.2f, 1f, 0.6f, pulse * 0.7f);
                        lr.startColor = c; lr.endColor = c;
                    }
                }
            }
        }

        private void OnDisable()
        {
            IsActive = false;
            DestroyAura();
        }

        private void EnsureAura()
        {
            if (_aura != null) return;
            _aura = new GameObject("HES_Aura");
            _aura.transform.SetParent(transform);
            _aura.transform.localPosition = Vector3.up * 0.05f;
            var lr = _aura.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 24;
            lr.widthMultiplier = 0.12f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Color c = new Color(0.2f, 1f, 0.6f, 0.7f);
            lr.startColor = c; lr.endColor = c;
            for (int i = 0; i < 24; i++)
            {
                float ang = i / 24f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * 1.2f, 0f, Mathf.Sin(ang) * 1.2f));
            }
        }

        private void DestroyAura()
        {
            if (_aura != null) { Destroy(_aura); _aura = null; }
        }
    }
}
