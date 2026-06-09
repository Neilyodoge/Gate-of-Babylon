using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 御金 · 飞剑环绕（v0.6 剑魄→御金 重心）
    /// 围绕玩家旋转的数把金色飞剑；周期性挑选最近的敌人发动一次突刺，造成 攻击×比例 伤害。
    /// 这是"御金/控制金属"在常规战斗中的底子，与剑心通明（爆发）+ 一念刹那（大招）互补。
    /// 由 SpiritRootGoldController 在金化身激活时创建、切换化身/禁用时销毁。
    /// </summary>
    public class FlyingSwordSwarm : MonoBehaviour
    {
        private PlayerController _player;
        private LayerMask _mask;
        private int _count = 3;
        private float _dmgRatio = 0.6f;

        private const float OrbitRadius = 1.8f;
        private const float OrbitSpeed = 140f;     // deg/s
        private const float StrikeInterval = 1.2f;
        private const float StrikeRange = 8f;
        private static readonly Color SwordGold = new Color(1f, 0.85f, 0.2f, 1f);

        private Transform[] _swords;
        private float _angle;
        private float _strikeTimer;

        public void Init(PlayerController player, LayerMask mask, int count, float dmgRatio)
        {
            _player = player;
            _mask = mask;
            _count = Mathf.Max(1, count);
            _dmgRatio = Mathf.Max(0.05f, dmgRatio);

            _swords = new Transform[_count];
            for (int i = 0; i < _count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "FlyingSword";
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(0.12f, 0.12f, 0.75f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = SwordGold;
                _swords[i] = go.transform;
            }
        }

        private void Update()
        {
            if (_player == null) { Destroy(gameObject); return; }

            Vector3 center = _player.transform.position + Vector3.up * 1f;
            _angle += OrbitSpeed * Time.deltaTime;
            for (int i = 0; i < _count; i++)
            {
                if (_swords[i] == null) continue;
                float a = (_angle + i * 360f / _count) * Mathf.Deg2Rad;
                _swords[i].position = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * OrbitRadius;
                _swords[i].rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }

            _strikeTimer -= Time.deltaTime;
            if (_strikeTimer <= 0f)
            {
                _strikeTimer = StrikeInterval;
                TryStrike(center);
            }
        }

        private void TryStrike(Vector3 center)
        {
            var hits = Physics.OverlapSphere(center, StrikeRange, _mask);
            GameObject best = null;
            float bestSqr = float.MaxValue;
            foreach (var col in hits)
            {
                if (col == null) continue;
                if (col.CompareTag("Player")) continue;
                if (col.GetComponent<IDamageable>() == null) continue;
                float sqr = (col.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = col.gameObject; }
            }
            if (best == null) return;

            var dmgable = best.GetComponent<IDamageable>();
            if (dmgable == null || _player == null) return;

            float dmg = _player.Stats.attackDamage * _dmgRatio;
            Vector3 hp = best.transform.position + Vector3.up * 1f;
            dmgable.OnDamage(dmg, hp, _player.gameObject);

            // 视觉：从最近的飞剑突刺到敌人（全程束线）+ 命中火花，让突刺可见
            Vector3 from = center;
            float bestFromSqr = float.MaxValue;
            if (_swords != null)
                foreach (var s in _swords)
                {
                    if (s == null) continue;
                    float sq = (s.position - hp).sqrMagnitude;
                    if (sq < bestFromSqr) { bestFromSqr = sq; from = s.position; }
                }
            Vector3 dir = hp - from;
            float dist = dir.magnitude;
            if (dist > 0.01f)
                FxFactory.SpawnSliceLine(from, dir.normalized, dist, SwordGold, 0.18f);
            FxFactory.SpawnElementBurst(hp, ElementTag.None, 0.6f, 0.18f);
        }
    }
}
