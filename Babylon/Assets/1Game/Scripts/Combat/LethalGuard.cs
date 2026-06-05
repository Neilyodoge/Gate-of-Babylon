using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 金蝉脱壳 · 受致命伤拦截。主动释放武装一段时间；武装期内若受到致命伤害，
    /// 由 <see cref="PlayerController.OnDamage"/> 调 <see cref="TryConsume"/>：免死回血 + 原地爆炸替身 + 向后瞬移。
    /// 挂在玩家 GameObject 上（与 PlayerController 同物体）。
    /// </summary>
    public class LethalGuard : MonoBehaviour
    {
        private float _timer;

        public bool Armed => _timer > 0f;

        public void Arm(float duration)
        {
            _timer = Mathf.Max(_timer, Mathf.Max(0.5f, duration));
        }

        private void Update()
        {
            if (_timer > 0f) _timer -= Time.deltaTime;
        }

        /// <summary>受致命伤时调用。武装中则触发并返回 true（已免死）。</summary>
        public bool TryConsume()
        {
            if (_timer <= 0f) return false;
            _timer = 0f;

            var pc = GetComponent<PlayerController>();
            if (pc == null) return false;

            // 免死：恢复到 15% 血量
            pc.Stats.currentHp = pc.Stats.maxHp * 0.15f;

            // 原地爆炸替身：对周围敌人造成伤害 + 击退
            Vector3 origin = transform.position;
            float dmg = pc.Stats.attackDamage * 2f;
            var hits = Physics.OverlapSphere(origin, 4f);
            foreach (var h in hits)
            {
                if (h == null || h.gameObject == gameObject) continue;
                var d = h.GetComponent<IDamageable>();
                if (d != null) d.OnDamage(dmg, h.transform.position, gameObject);
                var cc = h.GetComponent<CharacterController>();
                if (cc != null) cc.Move((h.transform.position - origin).normalized * 3f);
            }
            SkillModifierApplier.SpawnCubeVfx(origin + Vector3.up * 0.5f, new Color(0.3f, 0.6f, 1f, 0.7f), 2f, 0.5f);

            // 向后瞬移脱身
            Vector3 dir = -pc.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            Vector3 target = origin + dir.normalized * 6f;
            target.y = origin.y;
            var pcc = pc.GetComponent<CharacterController>();
            if (pcc != null) { pcc.enabled = false; pc.transform.position = target; pcc.enabled = true; }
            else pc.transform.position = target;

            pc.SetInvincible(1f);

            GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = pc.Stats.currentHp, MaxHp = pc.Stats.maxHp });
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = origin + Vector3.up * 2f,
                Damage = 0,
                SpecialTag = "金蝉脱壳"
            });
            Debug.Log("<color=cyan>🦋 金蝉脱壳！免死 + 替身爆炸 + 瞬移脱身</color>");
            return true;
        }
    }
}
