using UnityEngine;

namespace XianTu
{
    /// <summary>序章试玩靶；承接真实伤害事件，但不会被摧毁。</summary>
    public sealed class StarterSpiritTrialTarget :
        MonoBehaviour,
        IDamageable
    {
        private readonly CombatStats _stats = new()
        {
            maxHp = 100000f,
            currentHp = 100000f,
            defense = 0f
        };
        private Vector3 _restScale;
        private float _pulse;

        public CombatStats Stats => _stats;

        private void Awake()
        {
            _restScale = transform.localScale;
        }

        public void OnDamage(
            float damage,
            Vector3 hitPoint,
            GameObject attacker)
        {
            float applied = Mathf.Max(1f, damage);
            LegacyCombatResultRecorder.RecordPlayerDamage(
                attacker,
                gameObject,
                damage,
                applied,
                countTowardRunTotal: false);
            _stats.currentHp = _stats.maxHp;
            _pulse = 1f;
        }

        public void OnDeath()
        {
            _stats.currentHp = _stats.maxHp;
        }

        private void Update()
        {
            _pulse = Mathf.MoveTowards(
                _pulse,
                0f,
                Time.deltaTime * 5f);
            transform.localScale =
                _restScale * (1f + _pulse * 0.13f);
        }
    }
}
