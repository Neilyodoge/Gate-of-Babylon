using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>无攻击、无掉落的序章机制展示靶，保留正式伤害与回路归因入口。</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StarterPrologueTrainingDummy :
        MonoBehaviour,
        IDamageable
    {
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 24f,
            currentHp = 24f,
            defense = 0f,
        };
        private bool _dead;

        public CombatStats Stats => stats;

        private void Awake()
        {
            stats.ResetHp();
        }

        public void OnDamage(
            float damage,
            Vector3 hitPoint,
            GameObject attacker)
        {
            if (_dead || !stats.IsAlive)
                return;

            float actual = stats.TakeDamage(damage);
            LegacyCombatResultRecorder.RecordPlayerDamage(
                attacker,
                gameObject,
                damage,
                actual,
                countTowardRunTotal: true);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero
                    ? hitPoint
                    : transform.position + Vector3.up,
                Damage = actual,
                IsPlayerDamage = false
            });

            if (stats.IsAlive)
                return;

            OnDeath();
        }

        public void OnDeath()
        {
            if (_dead)
                return;
            _dead = true;
            gameObject.tag = "Untagged";
            GetComponent<Collider>().enabled = false;
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });
            StartCoroutine(ShrinkAndDestroy());
        }

        private IEnumerator ShrinkAndDestroy()
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            const float duration = 0.24f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(
                    start,
                    Vector3.zero,
                    elapsed / duration);
                yield return null;
            }
            Destroy(gameObject);
        }

        public static StarterPrologueTrainingDummy Spawn(
            Vector3 position,
            int index)
        {
            GameObject go = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            go.name = $"TutorialTarget_{index + 1}";
            go.transform.position = position;
            go.transform.localScale =
                new Vector3(0.82f, 0.82f, 0.82f);
            go.tag = "Enemy";
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                go.layer = enemyLayer;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = index switch
                {
                    0 => new Color(0.92f, 0.62f, 0.30f),
                    1 => new Color(0.66f, 0.78f, 0.55f),
                    _ => new Color(0.38f, 0.75f, 0.72f)
                };
            }
            return go.AddComponent<StarterPrologueTrainingDummy>();
        }
    }
}
