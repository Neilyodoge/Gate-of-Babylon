using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵兽伙伴 —— 入梦时由 <see cref="SpiritBeastLoader"/> 在玩家身边 spawn，
    /// 跟随玩家移动 + 自动攻击周围敌人。死亡或撤离时一并销毁，不参与 RunOnly 池。
    ///
    /// 设计要点：
    /// - 不实现 IDamageable（避免被敌人误击杀），仅作为光球状视觉跟随物 + 持续伤害源
    /// - 攻击逻辑：扫描半径内最近的"Enemy" tag → 投射光弹（直接判定，无 Projectile 实体）
    /// </summary>
    public class SpiritBeastCompanion : MonoBehaviour
    {
        private SpiritBeastEntry _entry;
        private Transform _player;
        private float _attackCooldown;
        private GameObject _body;

        private const float FollowDistance = 1.8f;     // 玩家身后 / 身侧的悬浮距离
        private const float FollowHeight = 1.6f;       // 浮在玩家头顶高度
        private const float FollowSmooth = 6f;
        private const float ProjectileTravelTime = 0.18f;

        public static SpiritBeastCompanion Spawn(SpiritBeastEntry entry, Transform player)
        {
            if (entry == null || player == null) return null;

            var go = new GameObject($"SpiritBeast_{entry.beastName}");
            go.transform.position = player.position + new Vector3(FollowDistance, FollowHeight, 0);
            var c = go.AddComponent<SpiritBeastCompanion>();
            c._entry = entry;
            c._player = player;
            c.BuildVisual();
            return c;
        }

        private void BuildVisual()
        {
            Color tint = _entry.displayColor;

            // —— 主体形状：根据灵兽名走差异化造型 ——
            // 青鸾 = 鸟形（拉宽 Capsule + 两片翅膀 Cube）
            // 赤虎 = 兽形（拉长 Capsule + 头球 + 两个前爪）
            // 玄龟 = 龟形（扁球 + 顶部龟壳 Cube）
            // 默认 = 普通发光球
            BuildShapeByName(_entry.beastName, tint);

            // —— 围绕主体的灵气粒子（颜色与灵兽匹配）——
            CaveVfx.SpawnOrbitingParticles(transform, Vector3.zero,
                count: 4, orbitRadius: 0.45f, orbitHeight: 0f,
                particleSize: 0.08f, color: tint,
                orbitSpeed: 220f, verticalBob: 0.12f);

            // —— 脚下光晕（发光透明圆盘）——
            var halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            halo.name = "BeastHalo";
            halo.transform.SetParent(transform, false);
            halo.transform.localPosition = new Vector3(0, -0.05f, 0);
            halo.transform.localScale = new Vector3(0.85f, 0.04f, 0.85f);
            var hcol = halo.GetComponent<Collider>();
            if (hcol != null) Destroy(hcol);
            var hrend = halo.GetComponent<Renderer>();
            if (hrend != null)
            {
                var mat = MaterialHelper.CreateLitTransparent(
                    new Color(tint.r, tint.g, tint.b, 0.4f));
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", tint * 1.4f);
                }
                hrend.material = mat;
                hrend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void BuildShapeByName(string name, Color tint)
        {
            if (name == "青鸾")
            {
                // 鸟身（前后拉长的胶囊）
                _body = SpawnPart(PrimitiveType.Capsule, Vector3.zero,
                    new Vector3(0.35f, 0.45f, 0.7f), tint, tint * 1.8f);
                _body.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                // 头部
                SpawnPart(PrimitiveType.Sphere, new Vector3(0, 0, 0.42f),
                    Vector3.one * 0.28f, tint, tint * 1.6f);
                // 翅膀（左右两片）
                var wingL = SpawnPart(PrimitiveType.Cube, new Vector3(-0.35f, 0, 0),
                    new Vector3(0.55f, 0.04f, 0.32f), tint, tint * 1.5f);
                wingL.transform.localRotation = Quaternion.Euler(0, 0, 18f);
                var wingR = SpawnPart(PrimitiveType.Cube, new Vector3(0.35f, 0, 0),
                    new Vector3(0.55f, 0.04f, 0.32f), tint, tint * 1.5f);
                wingR.transform.localRotation = Quaternion.Euler(0, 0, -18f);
                // 尾羽
                SpawnPart(PrimitiveType.Cube, new Vector3(0, 0, -0.5f),
                    new Vector3(0.1f, 0.04f, 0.4f), tint, tint * 1.4f);
            }
            else if (name == "赤虎")
            {
                // 虎身（拉长 Capsule）
                _body = SpawnPart(PrimitiveType.Capsule, Vector3.zero,
                    new Vector3(0.45f, 0.45f, 0.85f), tint, tint * 1.6f);
                _body.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                // 头球
                SpawnPart(PrimitiveType.Sphere, new Vector3(0, 0.05f, 0.55f),
                    Vector3.one * 0.4f, tint, tint * 1.8f);
                // 两只眼睛（小白点）
                SpawnPart(PrimitiveType.Sphere, new Vector3(-0.12f, 0.1f, 0.72f),
                    Vector3.one * 0.08f, Color.white, new Color(1f, 0.9f, 0.7f) * 2f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(0.12f, 0.1f, 0.72f),
                    Vector3.one * 0.08f, Color.white, new Color(1f, 0.9f, 0.7f) * 2f);
                // 四脚（小球）
                SpawnPart(PrimitiveType.Sphere, new Vector3(-0.25f, -0.25f, 0.3f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(0.25f, -0.25f, 0.3f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(-0.25f, -0.25f, -0.3f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(0.25f, -0.25f, -0.3f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                // 尾巴（拉长 Cube）
                var tail = SpawnPart(PrimitiveType.Capsule, new Vector3(0, 0.1f, -0.55f),
                    new Vector3(0.08f, 0.3f, 0.08f), tint, tint * 1.4f);
                tail.transform.localRotation = Quaternion.Euler(40f, 0, 0);
            }
            else if (name == "玄龟")
            {
                // 龟身（扁球）
                _body = SpawnPart(PrimitiveType.Sphere, Vector3.zero,
                    new Vector3(0.7f, 0.32f, 0.85f), tint, tint * 1.4f);
                // 龟壳（顶部稍大六角块）
                var shell = SpawnPart(PrimitiveType.Cylinder, new Vector3(0, 0.18f, 0),
                    new Vector3(0.6f, 0.12f, 0.6f),
                    new Color(tint.r * 0.7f, tint.g * 0.7f, tint.b * 0.7f),
                    tint * 1.6f);
                shell.transform.localScale = new Vector3(0.65f, 0.18f, 0.65f);
                // 头球
                SpawnPart(PrimitiveType.Sphere, new Vector3(0, 0.0f, 0.5f),
                    Vector3.one * 0.32f, tint, tint * 1.6f);
                // 4 只脚
                SpawnPart(PrimitiveType.Sphere, new Vector3(-0.32f, -0.15f, 0.28f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(0.32f, -0.15f, 0.28f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(-0.32f, -0.15f, -0.28f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
                SpawnPart(PrimitiveType.Sphere, new Vector3(0.32f, -0.15f, -0.28f),
                    Vector3.one * 0.18f, tint, tint * 1.4f);
            }
            else
            {
                _body = SpawnPart(PrimitiveType.Sphere, Vector3.zero,
                    Vector3.one * 0.55f, tint, tint * 1.6f);
            }
        }

        private GameObject SpawnPart(PrimitiveType type, Vector3 localPos, Vector3 localScale,
            Color baseColor, Color emission)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(baseColor, emission);
                rend.material = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        private void Update()
        {
            if (_player == null || _entry == null)
            {
                Destroy(gameObject);
                return;
            }

            // 跟随玩家（按玩家朝向放在身后右侧，叠加 sin 漂浮）
            float bob = 0.12f * Mathf.Sin(Time.time * 2.6f);
            Vector3 desiredPos = _player.position
                + _player.right * 0.9f
                + -_player.forward * 0.6f
                + Vector3.up * (FollowHeight + bob);
            transform.position = Vector3.Lerp(transform.position, desiredPos, FollowSmooth * Time.deltaTime);

            // 整体朝向玩家前方（让灵兽"望向"玩家正在面对的方向）
            Vector3 lookDir = _player.forward;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDir), 6f * Time.deltaTime);

            // 攻击 CD
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                if (TryAttack()) _attackCooldown = _entry.attackInterval;
            }
        }

        private bool TryAttack()
        {
            GameObject best = null;
            float bestDist = _entry.scanRadius;
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best == null) return false;

            StartCoroutine(FireProjectileAt(best));
            return true;
        }

        private System.Collections.IEnumerator FireProjectileAt(GameObject target)
        {
            if (target == null) yield break;

            // 发射前先在灵兽位置闪一下（出招前摇视觉）
            FxFactory.SpawnPrimitive(transform.position, PrimitiveType.Sphere,
                0.45f, new Color(_entry.displayColor.r, _entry.displayColor.g, _entry.displayColor.b, 0.7f),
                0.15f, true);

            // 简单光弹：在自身 → 敌人之间生成短暂的发光球 + 拖尾 LineRenderer
            var bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "BeastProjectile";
            bullet.transform.localScale = Vector3.one * 0.22f;
            var bcol = bullet.GetComponent<Collider>();
            if (bcol != null) Destroy(bcol);
            var brend = bullet.GetComponent<Renderer>();
            if (brend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    _entry.displayColor, _entry.displayColor * 2.4f);
                brend.material = mat;
                brend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 拖尾
            var trail = bullet.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = _entry.displayColor;
            trail.endColor = new Color(_entry.displayColor.r, _entry.displayColor.g, _entry.displayColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 start = transform.position;
            float t = 0f;
            while (t < ProjectileTravelTime)
            {
                if (target == null) break;
                t += Time.deltaTime;
                bullet.transform.position = Vector3.Lerp(start, target.transform.position + Vector3.up * 0.6f,
                    Mathf.Clamp01(t / ProjectileTravelTime));
                yield return null;
            }

            // 命中判定 + 命中爆裂
            if (target != null)
            {
                var dmg = target.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    dmg.OnDamage(_entry.attackDamage, target.transform.position, gameObject);
                }
                FxFactory.SpawnAOERing(target.transform.position + Vector3.up * 0.05f, 0.6f,
                    _entry.displayColor, lifetime: 0.3f);
            }
            Destroy(bullet);
        }

        private void OnDestroy()
        {
            // 视觉碎裂闪光（极简：单帧光球，依赖 Particle 太重）
        }
    }

    /// <summary>入梦时根据 <see cref="SaveDataV1.activeSpiritBeastId"/> 调用一次，spawn 出灵兽伙伴。</summary>
    public static class SpiritBeastLoader
    {
        private static SpiritBeastCompanion _current;

        public static void Apply(PlayerController player)
        {
            // 同时只允许一只跟随；切场景前先清掉旧的
            if (_current != null)
            {
                Object.Destroy(_current.gameObject);
                _current = null;
            }
            if (player == null) return;
            var id = SaveSystem.Instance.Data.activeSpiritBeastId;
            if (string.IsNullOrEmpty(id)) return;

            var entry = SpiritBeastLibrary.GetByName(id);
            if (entry == null) return;
            _current = SpiritBeastCompanion.Spawn(entry, player.transform);
            if (_current != null)
                Debug.Log($"<color=#a0d090>[SpiritBeastLoader] 灵兽出征：{entry.beastName}（跟随玩家入梦）</color>");
        }

        public static void Despawn()
        {
            if (_current != null)
            {
                Object.Destroy(_current.gameObject);
                _current = null;
            }
        }
    }
}
