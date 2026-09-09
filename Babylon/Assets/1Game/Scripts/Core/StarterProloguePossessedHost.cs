using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章失控附身者。稳定值归零只解除人宠连接，不进入普通敌人死亡与掉落流程。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class StarterProloguePossessedHost :
        MonoBehaviour,
        IDamageable,
        ICombatImpulseReceiver
    {
        private const float PreferredRange = 7f;
        private const float MoveSpeed = 2.6f;
        private const float WaveWarningSeconds = 0.7f;
        private const float DashWarningSeconds = 0.7f;
        private const float DashDuration = 0.28f;
        private const float PulseWarningSeconds = 0.8f;

        [SerializeField] private CombatStats stability = new()
        {
            maxHp = 90f,
            currentHp = 90f,
            attackDamage = 6f,
            moveSpeed = MoveSpeed
        };

        private CharacterController _controller;
        private EnemyNavMotor _navMotor;
        private EnemyHealthBar _stabilityBar;
        private Transform _target;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Material _energyMaterial;
        private TextMeshPro _stabilityLabel;
        private Action<GameObject, Vector3> _resolved;
        private bool _isActing;
        private bool _overflowUsed;
        private bool _isResolved;
        private float _dashCooldown;
        private float _hitFlash;
        private int _actionVersion;
        private GameObject _activeWarning;

        public CombatStats Stats => stability;
        public bool IsResolved => _isResolved;
        public int EnergyWaveCount { get; private set; }
        public int DashSlashCount { get; private set; }
        public int OverflowPulseCount { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = 1.9f;
            _controller.radius = 0.42f;
            _controller.center = Vector3.up * 0.95f;
            _navMotor = gameObject.AddComponent<EnemyNavMotor>();
        }

        private void Start()
        {
            stability.ResetHp();
            _stabilityBar = EnemyHealthBar.Create(gameObject);
            _stabilityBar.UpdateHealth(
                stability.currentHp,
                stability.maxHp);
            CreateStabilityLabel();
            _target = PlayerController.Instance != null
                ? PlayerController.Instance.transform
                : null;
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalColors[i] =
                    MaterialHelper.SafeGetColor(
                        _renderers[i].material);
            }
            StartCoroutine(AttackLoop());
        }

        private void LateUpdate()
        {
            if (_stabilityLabel == null || Camera.main == null)
                return;
            _stabilityLabel.transform.rotation =
                Camera.main.transform.rotation;
        }

        private void Update()
        {
            if (_isResolved)
                return;
            if (_target == null && PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;

            _dashCooldown -= Time.deltaTime;
            UpdateHitFlash();
            UpdateEnergyFlow();
            if (_target == null || _isActing)
                return;

            Vector3 offset = _target.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > PreferredRange + 1.5f)
            {
                _navMotor.MoveTo(
                    _target.position,
                    MoveSpeed,
                    PreferredRange);
            }
            else if (distance < PreferredRange - 2f)
            {
                Vector3 retreat = offset.sqrMagnitude > 0.01f
                    ? -offset.normalized
                    : -transform.forward;
                _navMotor.MoveTo(
                    transform.position + retreat * 3f,
                    MoveSpeed,
                    0.1f);
            }
            else
            {
                _navMotor.Stop();
            }

            if (offset.sqrMagnitude > 0.01f)
                transform.rotation =
                    Quaternion.LookRotation(offset.normalized);
        }

        public void Configure(
            float hpMultiplier,
            float damageMultiplier,
            Action<GameObject, Vector3> resolved)
        {
            stability.maxHp =
                90f * Mathf.Max(0.1f, hpMultiplier);
            stability.currentHp = stability.maxHp;
            stability.attackDamage =
                8f * Mathf.Max(0f, damageMultiplier);
            stability.moveSpeed = MoveSpeed;
            _resolved = resolved;
        }

        public void OnDamage(
            float damage,
            Vector3 hitPoint,
            GameObject attacker)
        {
            if (_isResolved || !stability.IsAlive)
                return;

            float actual = stability.TakeDamage(damage);
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
            _stabilityBar?.UpdateHealth(
                stability.currentHp,
                stability.maxHp);
            _hitFlash = 0.12f;

            if (!stability.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            if (_isResolved)
                return;
            _isResolved = true;
            StopAllCoroutines();
            _navMotor.Stop();
            gameObject.tag = "Untagged";
            _controller.enabled = false;
            _resolved?.Invoke(gameObject, transform.position);
            Destroy(gameObject);
        }

        public bool TryApplyCombatImpulse(
            Vector3 origin,
            float distance,
            bool interrupt)
        {
            if (_isResolved ||
                !stability.IsAlive ||
                distance <= 0f ||
                _controller == null ||
                !_controller.enabled)
            {
                return false;
            }

            Vector3 direction = transform.position - origin;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f
                ? direction.normalized
                : -transform.forward;
            _controller.Move(direction * distance);
            _navMotor?.ResyncAfterForcedMove();
            if (interrupt)
            {
                _actionVersion++;
                _isActing = false;
                if (_activeWarning != null)
                {
                    Destroy(_activeWarning);
                    _activeWarning = null;
                }
            }
            return true;
        }

        private IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(0.8f);
            while (!_isResolved)
            {
                if (_target == null)
                {
                    yield return null;
                    continue;
                }

                float ratio = stability.maxHp > 0f
                    ? stability.currentHp / stability.maxHp
                    : 0f;
                if (!_overflowUsed && ratio <= 0.5f)
                {
                    _overflowUsed = true;
                    yield return OverflowPulse();
                }
                else if (_dashCooldown <= 0f)
                {
                    _dashCooldown = 7f;
                    yield return DashSlash();
                }
                else
                {
                    yield return EnergyWave();
                }
                yield return new WaitForSeconds(1.3f);
            }
        }

        private IEnumerator EnergyWave()
        {
            _isActing = true;
            int actionVersion = _actionVersion;
            Vector3 direction = DirectionToTarget();
            LineRenderer warning = CreateWarningLine(
                direction,
                13f);
            yield return new WaitForSeconds(
                WaveWarningSeconds);
            if (!_isResolved && actionVersion == _actionVersion)
            {
                EnergyWaveCount++;
                SpawnProjectile(direction);
            }
            if (warning != null)
                Destroy(warning.gameObject);
            _activeWarning = null;
            if (actionVersion == _actionVersion)
                _isActing = false;
        }

        private IEnumerator DashSlash()
        {
            _isActing = true;
            int actionVersion = _actionVersion;
            Vector3 direction = DirectionToTarget();
            LineRenderer warning = CreateWarningLine(
                direction,
                5f);
            yield return new WaitForSeconds(
                DashWarningSeconds);
            if (warning != null)
                Destroy(warning.gameObject);
            _activeWarning = null;
            if (_isResolved || actionVersion != _actionVersion)
                yield break;

            float elapsed = 0f;
            while (!_isResolved &&
                   actionVersion == _actionVersion &&
                   elapsed < DashDuration)
            {
                elapsed += Time.deltaTime;
                _controller.Move(
                    direction * (9f * Time.deltaTime));
                yield return null;
            }
            _navMotor.ResyncAfterForcedMove();
            if (_isResolved || actionVersion != _actionVersion)
                yield break;
            DashSlashCount++;
            DamagePlayerInRadius(1.7f, 1.15f);
            _isActing = false;
        }

        private IEnumerator OverflowPulse()
        {
            _isActing = true;
            int actionVersion = _actionVersion;
            GameObject warning = CreatePulseWarning();
            yield return new WaitForSeconds(
                PulseWarningSeconds);
            if (!_isResolved && actionVersion == _actionVersion)
            {
                OverflowPulseCount++;
                DamagePlayerInRadius(3.25f, 0.85f);
            }
            if (warning != null)
                Destroy(warning);
            _activeWarning = null;
            if (actionVersion == _actionVersion)
                _isActing = false;
        }

        private void SpawnProjectile(Vector3 direction)
        {
            GameObject projectile = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            projectile.name = "PossessedEnergyWave";
            projectile.transform.position =
                transform.position +
                Vector3.up * 0.85f +
                direction * 0.65f;
            projectile.transform.localScale =
                Vector3.one * 0.36f;
            projectile.layer = gameObject.layer;
            Renderer renderer =
                projectile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateEnergyMaterial();
            Collider collider =
                projectile.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            SphereCollider trigger =
                projectile.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            projectile.AddComponent<EnemyProjectile>().Initialize(
                stability.attackDamage,
                direction,
                11f);
        }

        private void DamagePlayerInRadius(
            float radius,
            float multiplier)
        {
            if (PlayerController.Instance == null)
                return;
            Vector3 delta =
                PlayerController.Instance.transform.position -
                transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > radius * radius)
                return;
            PlayerController.Instance.OnDamage(
                stability.attackDamage * multiplier,
                PlayerController.Instance.transform.position,
                gameObject);
        }

        private Vector3 DirectionToTarget()
        {
            Vector3 direction = _target != null
                ? _target.position - transform.position
                : transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f
                ? direction.normalized
                : transform.forward;
        }

        private LineRenderer CreateWarningLine(
            Vector3 direction,
            float length)
        {
            GameObject go = new("PossessedAttackWarning");
            _activeWarning = go;
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.material =
                new Material(Shader.Find("Sprites/Default"));
            line.startWidth = 0.12f;
            line.endWidth = 0.04f;
            line.startColor =
                new Color(0.2f, 0.65f, 1f, 0.9f);
            line.endColor =
                new Color(0.2f, 0.65f, 1f, 0.15f);
            line.positionCount = 2;
            Vector3 start =
                transform.position + Vector3.up * 0.1f;
            line.SetPosition(0, start);
            line.SetPosition(1, start + direction * length);
            return line;
        }

        private GameObject CreatePulseWarning()
        {
            GameObject pulse = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            pulse.name = "PossessedOverflowWarning";
            _activeWarning = pulse;
            pulse.transform.position =
                transform.position + Vector3.up * 0.03f;
            pulse.transform.localScale =
                new Vector3(3.25f, 0.025f, 3.25f);
            Collider collider = pulse.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            Renderer renderer = pulse.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateEnergyMaterial(
                    new Color(0.2f, 0.72f, 1f));
            return pulse;
        }

        private Material CreateEnergyMaterial()
        {
            return CreateEnergyMaterial(
                new Color(0.18f, 0.58f, 1f));
        }

        private Material CreateEnergyMaterial(Color color)
        {
            Material material =
                new(MaterialHelper.GetLitShader());
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
            return material;
        }

        private void UpdateEnergyFlow()
        {
            if (_energyMaterial == null)
                return;
            float pulse =
                1.5f + Mathf.PingPong(Time.time * 2.5f, 1.2f);
            _energyMaterial.SetColor(
                "_EmissionColor",
                new Color(0.18f, 0.58f, 1f) * pulse);
        }

        private void CreateStabilityLabel()
        {
            GameObject label = new("StabilityLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition =
                new Vector3(0f, 2.62f, 0f);
            _stabilityLabel = label.AddComponent<TextMeshPro>();
            if (UGuiKit.CjkFont != null)
                _stabilityLabel.font = UGuiKit.CjkFont;
            _stabilityLabel.text = "稳定值";
            _stabilityLabel.fontSize = 2.4f;
            _stabilityLabel.alignment =
                TextAlignmentOptions.Center;
            _stabilityLabel.color =
                new Color(0.55f, 0.85f, 1f, 0.95f);
            _stabilityLabel.transform.localScale =
                Vector3.one * 0.14f;
        }

        private void UpdateHitFlash()
        {
            if (_hitFlash <= 0f)
                return;
            _hitFlash -= Time.deltaTime;
            if (_hitFlash > 0f)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                        MaterialHelper.SafeSetColor(
                            _renderers[i].material,
                            Color.white);
                }
                return;
            }
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    MaterialHelper.SafeSetColor(
                        _renderers[i].material,
                        _originalColors[i]);
            }
        }

        public static StarterProloguePossessedHost Spawn(
            Vector3 position,
            float hpMultiplier,
            float damageMultiplier,
            Action<GameObject, Vector3> resolved)
        {
            GameObject root =
                new("TutorialPossessedHost");
            root.transform.position = position;
            root.tag = "Enemy";
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                root.layer = enemyLayer;
            root.AddComponent<CharacterController>();

            GameObject body = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            body.name = "HostBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.95f;
            body.transform.localScale =
                new Vector3(0.72f, 0.95f, 0.72f);
            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
                Destroy(bodyCollider);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material =
                    new Material(MaterialHelper.GetLitShader());
                bodyRenderer.material.color =
                    new Color(0.25f, 0.3f, 0.42f);
            }

            GameObject weapon = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            weapon.name = "FlowingEnergyWeapon";
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition =
                new Vector3(0.62f, 1f, 0.05f);
            weapon.transform.localRotation =
                Quaternion.Euler(0f, 0f, -18f);
            weapon.transform.localScale =
                new Vector3(0.12f, 1.15f, 0.12f);
            Collider weaponCollider =
                weapon.GetComponent<Collider>();
            if (weaponCollider != null)
                Destroy(weaponCollider);

            StarterProloguePossessedHost host =
                root.AddComponent<StarterProloguePossessedHost>();
            Renderer weaponRenderer =
                weapon.GetComponent<Renderer>();
            if (weaponRenderer != null)
            {
                host._energyMaterial =
                    host.CreateEnergyMaterial();
                weaponRenderer.material = host._energyMaterial;
            }
            host.Configure(
                hpMultiplier,
                damageMultiplier,
                resolved);
            return host;
        }
    }
}
