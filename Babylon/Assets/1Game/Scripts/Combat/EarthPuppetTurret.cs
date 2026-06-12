using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 兵阵合一 · 土傀儡炮台。原地不动，周期性锁定范围内最近敌人并对其落点造成 AOE 伤害。
    /// 由 <see cref="SpiritRootEarthController.TogglePuppetArrayMode"/> 成阵召唤。
    /// </summary>
    public class EarthPuppetTurret : MonoBehaviour
    {
        private PlayerController _player;
        private LayerMask _mask;
        private float _life;
        private float _range = 10f;
        private float _aoeRadius = 3f;
        private float _fireInterval = 1f;
        private float _dmgRatio = 0.6f;
        private float _timer;

        /// <summary>御物"坐镇聚灵"时由 SpiritRootEarthController 抬高（如 ×1.5），平时为 1。</summary>
        public static float GlobalDamageMul = 1f;

        private static readonly Collider[] _buf = new Collider[32];

        public void Init(PlayerController player, LayerMask mask, float life)
        {
            _player = player;
            _mask = mask;
            _life = life;

            // 视觉：土黄方碑
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "TurretVisual";
            vis.transform.SetParent(transform, false);
            vis.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
            vis.transform.localPosition = Vector3.up * 0.6f;
            var col = vis.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = vis.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.85f, 0.7f, 0.4f, 0.9f);
                rend.material = mat;
            }
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }
            if (_player == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _fireInterval;

            // 锁定最近敌人
            int n = Physics.OverlapSphereNonAlloc(transform.position, _range, _buf, _mask);
            Transform nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i];
                if (c == null || c.CompareTag("Player")) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = c.transform; }
            }
            if (nearest == null) return;

            // 对落点 AOE 炮击
            Vector3 impact = nearest.position;
            float dmg = _player.Stats.attackDamage * _dmgRatio * Mathf.Max(0.1f, GlobalDamageMul);
            var aoe = Physics.OverlapSphere(impact, _aoeRadius, _mask);
            foreach (var c in aoe)
            {
                if (c == null || c.CompareTag("Player")) continue;
                var dmgable = c.GetComponent<IDamageable>();
                if (dmgable != null) dmgable.OnDamage(dmg, c.transform.position, _player.gameObject);
            }

            // 视觉：炮口闪光 + 一道土黄冲击束（傀儡 → 敌人）+ 命中爆，让"攻击过程"可见
            Color earth = FxFactory.ElementColor(ElementTag.Earth);
            Vector3 muzzle = transform.position + Vector3.up * 1.1f;
            Vector3 aim = impact + Vector3.up * 0.6f;
            Vector3 dir = aim - muzzle;
            float dist = dir.magnitude;
            if (dist > 0.01f)
                FxFactory.SpawnSliceLine(muzzle, dir.normalized, dist, earth, 0.16f);
            FxFactory.SpawnElementBurst(muzzle, ElementTag.Earth, 0.5f, 0.18f);
            FxFactory.SpawnElementBurst(impact + Vector3.up * 0.3f, ElementTag.Earth, _aoeRadius, 0.35f);
        }

        /// <summary>
        /// GDD §4.3.5 / §6.7.4：玩家释放技能时，傀儡立即跟随开火一次（同步伤害由调用方传入）。
        /// </summary>
        public void SyncFire(float damage)
        {
            if (_player == null) return;

            int n = Physics.OverlapSphereNonAlloc(transform.position, _range, _buf, _mask);
            Transform nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i];
                if (c == null || c.CompareTag("Player")) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = c.transform; }
            }
            if (nearest == null) return;

            Vector3 impact = nearest.position;
            var aoe = Physics.OverlapSphere(impact, _aoeRadius, _mask);
            foreach (var c in aoe)
            {
                if (c == null || c.CompareTag("Player")) continue;
                var dmgable = c.GetComponent<IDamageable>();
                if (dmgable != null) dmgable.OnDamage(damage, c.transform.position, _player.gameObject);
            }

            Color earth = FxFactory.ElementColor(ElementTag.Earth);
            Vector3 muzzle = transform.position + Vector3.up * 1.1f;
            Vector3 aim = impact + Vector3.up * 0.6f;
            Vector3 dir = aim - muzzle;
            float dist = dir.magnitude;
            if (dist > 0.01f)
                FxFactory.SpawnSliceLine(muzzle, dir.normalized, dist, new Color(earth.r, earth.g, earth.b, 0.5f), 0.12f);
            FxFactory.SpawnElementBurst(impact + Vector3.up * 0.3f, ElementTag.Earth, _aoeRadius * 0.6f, 0.2f);
        }
    }
}
