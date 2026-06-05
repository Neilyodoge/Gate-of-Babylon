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

        public void Activate(float duration)
        {
            _timer = Mathf.Max(_timer, Mathf.Max(1f, duration));
            IsActive = true;
        }

        private void Update()
        {
            if (_timer > 0f)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) IsActive = false;
            }
        }

        private void OnDisable()
        {
            IsActive = false;
        }
    }
}
