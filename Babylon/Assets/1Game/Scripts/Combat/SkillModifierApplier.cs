using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// GDD 6.5 技能修饰应用器（v0.3 MVP）。
    ///
    /// 输入：技能 + 该技能槽位下方的灵物列表 + 命中目标。
    /// 输出：根据匹配的 modifierDef 触发"落地区域 / 命中附加灼烧 / 冻结 / 雷击"。
    ///
    /// 临时特效：cube + 颜色（GDD 6.5"先用 cube 大致表现"）。
    /// </summary>
    public static class SkillModifierApplier
    {
        /// <summary>
        /// 在 AOE 技能命中目标后调用，处理命中附加 / 落点区域。
        /// </summary>
        public static void ApplyAreaSkill(
            SkillData skill,
            int skillSlotIndex,
            Vector3 targetPos,
            float radius,
            List<Collider> hitTargets,
            PlayerController player,
            LayerMask enemyLayer)
        {
            if (skill == null || skill.modifierDefs == null || skill.modifierDefs.Length == 0) return;
            if (player == null || player.SpiritSlots == null) return;

            var slotItems = player.SpiritSlots.GetItemsInSkillSlot(skillSlotIndex);
            if (slotItems.Count == 0) return;

            // 统计槽内每个 modTag 的件数
            var tagCount = new Dictionary<ElementTag, int>();
            foreach (var it in slotItems)
            {
                if (it.modTag == ElementTag.None) continue;
                tagCount.TryGetValue(it.modTag, out int c);
                tagCount[it.modTag] = c + 1;
            }
            if (tagCount.Count == 0) return;

            foreach (var mod in skill.modifierDefs)
            {
                if (mod == null) continue;
                if (!tagCount.TryGetValue(mod.requiredTag, out int count) || count < mod.requiredCount) continue;

                ApplySingleModifier(mod, targetPos, radius, hitTargets, player, enemyLayer);

                GameEvents.Publish(new GameEvents.SkillModifierActivated
                {
                    SlotIndex = skillSlotIndex,
                    ModifiedSkillName = string.IsNullOrEmpty(mod.modifiedName) ? skill.skillName : mod.modifiedName,
                    PrimaryTag = mod.requiredTag
                });

                if (!string.IsNullOrEmpty(mod.modifiedName))
                    Debug.Log($"<color=#FF9933>修饰激活：{skill.skillName} → {mod.modifiedName} ({mod.requiredTag})</color>");
            }
        }

        private static void ApplySingleModifier(
            SkillModifierDef mod,
            Vector3 targetPos,
            float radius,
            List<Collider> hitTargets,
            PlayerController player,
            LayerMask enemyLayer)
        {
            // 命中附加（针对当前命中的所有目标）
            if (hitTargets != null)
            {
                foreach (var col in hitTargets)
                {
                    if (col == null) continue;

                    if (mod.addBurn && mod.burnDPS > 0f)
                    {
                        ApplyBurn(col.gameObject, mod.burnDPS, mod.burnDuration);
                    }
                    if (mod.addFreeze && Random.value < mod.freezeChance)
                    {
                        ApplyFreeze(col.gameObject, mod.freezeDuration);
                    }
                    if (mod.addThunderStrike)
                    {
                        var dmgable = col.GetComponent<IDamageable>();
                        if (dmgable != null)
                        {
                            float bonus = player.Stats.attackDamage * mod.thunderMul;
                            dmgable.OnDamage(bonus, col.transform.position, player.gameObject);
                            GameEvents.Publish(new GameEvents.DamageNumberRequested
                            {
                                WorldPosition = col.transform.position + Vector3.up * 1.5f,
                                Damage = bonus,
                                SpecialTag = "雷击"
                            });
                        }
                        SpawnCubeVfx(col.transform.position + Vector3.up * 0.5f, ColorOf(ElementTag.Thunder), 0.4f, 0.4f);
                    }
                }
            }

            // 落点持续地带
            if (mod.leaveZone)
            {
                var go = new GameObject($"ModifierZone_{mod.requiredTag}");
                go.transform.position = targetPos;
                var zone = go.AddComponent<SkillZoneEffect>();
                zone.Init(player, mod, radius, enemyLayer);
                SpawnCubeVfx(targetPos + Vector3.up * 0.1f, ColorOf(mod.requiredTag), Mathf.Max(1f, mod.zoneRadius * 0.6f), mod.zoneDuration);
            }
            else
            {
                // 没有持续地带，但仍弹一个一闪即逝的 cube 提示
                SpawnCubeVfx(targetPos + Vector3.up * 0.1f, ColorOf(mod.requiredTag), Mathf.Max(0.6f, radius * 0.4f), 0.6f);
            }
        }

        /// <summary>给目标加（或刷新）灼烧。封装 BurnEffect 的获取/添加逻辑。</summary>
        public static void ApplyBurn(GameObject target, float dps, float duration)
        {
            if (target == null) return;
            var burn = target.GetComponent<BurnEffect>();
            if (burn == null) burn = target.AddComponent<BurnEffect>();
            burn.Apply(dps, duration);
        }

        /// <summary>给目标施加冻结状态。优先调 EnemyBase 的 ApplyFreeze，否则降级为减速 BUFF。</summary>
        public static void ApplyFreeze(GameObject target, float duration)
        {
            if (target == null || duration <= 0f) return;
            var enemy = target.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                // 反射调用 ApplyFreeze（如果未来 EnemyBase 加了该方法）。当前没有，则走 StatusEffect 降级
                var m = enemy.GetType().GetMethod("ApplyFreeze", new[] { typeof(float) });
                if (m != null)
                {
                    m.Invoke(enemy, new object[] { duration });
                    return;
                }
            }

            // 降级方案：在目标身上挂一个减速 StatusEffect（短时间）
            var status = target.GetComponent<StatusEffectController>();
            if (status == null) status = target.AddComponent<StatusEffectController>();
            status.Apply(new StatusEffect
            {
                id = "Freeze",
                isBuff = false,
                elementTag = ElementTag.Ice,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = duration,
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MoveSpeed, -0.6f)
                },
                displayName = "冻结",
                description = "移动速度大幅降低",
                uiColor = ColorOf(ElementTag.Ice)
            });
        }

        /// <summary>
        /// 技能自身 elementTag 命中表现。与"槽位灵物 modifier"无关——
        /// 只要 SkillData.elementTag != None，命中所有目标都会产生对应元素效果（灼烧/冻结/雷击/颜色 cube）。
        /// </summary>
        public static void ApplyElementImpact(
            ElementTag tag,
            Vector3 impactPos,
            List<Collider> hitTargets,
            PlayerController player)
        {
            if (tag == ElementTag.None) return;

            // 落点弹一个颜色 cube 作为元素提示
            SpawnCubeVfx(impactPos + Vector3.up * 0.1f, ColorOf(tag), 0.8f, 0.5f);

            if (hitTargets == null || player == null) return;

            foreach (var col in hitTargets)
            {
                if (col == null) continue;
                Vector3 pos = col.transform.position + Vector3.up * 0.5f;

                switch (tag)
                {
                    case ElementTag.Fire:
                        ApplyBurn(col.gameObject, 3f, 2f);
                        SpawnCubeVfx(pos, ColorOf(tag), 0.4f, 0.3f);
                        break;
                    case ElementTag.Ice:
                        if (Random.value < 0.35f) ApplyFreeze(col.gameObject, 0.6f);
                        SpawnCubeVfx(pos, ColorOf(tag), 0.4f, 0.3f);
                        break;
                    case ElementTag.Thunder:
                        var dmgable = col.GetComponent<IDamageable>();
                        if (dmgable != null)
                        {
                            float bonus = player.Stats.attackDamage * 0.4f;
                            dmgable.OnDamage(bonus, pos, player.gameObject);
                            GameEvents.Publish(new GameEvents.DamageNumberRequested
                            {
                                WorldPosition = pos + Vector3.up * 1f,
                                Damage = bonus,
                                SpecialTag = "雷击"
                            });
                        }
                        SpawnCubeVfx(pos, ColorOf(tag), 0.5f, 0.3f);
                        break;
                    case ElementTag.Wind:
                        // 风：轻微击退（这里用 cube 提示，击退由 IDamageable 内部处理过了）
                        SpawnCubeVfx(pos, ColorOf(tag), 0.5f, 0.25f);
                        break;
                    case ElementTag.Pierce:
                        // 穿透：白色 cube 标记一下
                        SpawnCubeVfx(pos, ColorOf(tag), 0.35f, 0.25f);
                        break;
                    default:
                        SpawnCubeVfx(pos, ColorOf(tag), 0.4f, 0.3f);
                        break;
                }
            }
        }

        public static Color ColorOf(ElementTag tag) => tag switch
        {
            ElementTag.Fire => new Color(1f, 0.4f, 0.1f, 0.65f),
            ElementTag.Ice => new Color(0.3f, 0.8f, 1f, 0.65f),
            ElementTag.Thunder => new Color(1f, 0.95f, 0.3f, 0.75f),
            ElementTag.Wind => new Color(0.6f, 1f, 0.7f, 0.6f),
            ElementTag.Water => new Color(0.3f, 0.5f, 1f, 0.6f),
            ElementTag.Wood => new Color(0.4f, 0.95f, 0.4f, 0.6f),
            ElementTag.Earth => new Color(0.85f, 0.7f, 0.4f, 0.7f),
            ElementTag.Pierce => new Color(0.85f, 0.85f, 0.85f, 0.6f),
            _ => new Color(1f, 1f, 1f, 0.6f)
        };

        /// <summary>
        /// 临时 cube 特效：用 GameObject.CreatePrimitive(Cube) + 自发光颜色 + 自动销毁。
        /// 等正式 VFX 资源接入后用 prefab 替换。
        /// riseSpeed > 0 时，cube 会以指定速度向上飘（适合火苗/烟雾等环境特效）。
        /// </summary>
        public static GameObject SpawnCubeVfx(Vector3 pos, Color color, float size, float lifetime, float riseSpeed = 0f)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ModifierCubeVfx";
            cube.transform.position = pos;
            cube.transform.localScale = Vector3.one * size;
            var col = cube.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = cube.GetComponent<Renderer>();
            if (rend != null)
            {
                // 走 URP/Unlit 一份不依赖光照的简化材质
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                rend.material = mat;
            }

            cube.AddComponent<CubeVfxAutoDestroy>().Init(lifetime, riseSpeed);
            return cube;
        }
    }

    /// <summary>cube 特效自销毁 + 一些动效（旋转 + 缩放褪色 + 可选上飘）。</summary>
    internal class CubeVfxAutoDestroy : MonoBehaviour
    {
        private float _lifetime;
        private float _t;
        private float _riseSpeed;
        private Renderer _renderer;
        private Vector3 _baseScale;

        public void Init(float lifetime, float riseSpeed = 0f)
        {
            _lifetime = Mathf.Max(0.1f, lifetime);
            _riseSpeed = riseSpeed;
            _renderer = GetComponent<Renderer>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float p = _t / _lifetime;
            if (p >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.Rotate(0, 90f * Time.deltaTime, 0, Space.Self);
            // 火苗/烟雾上升时同步缩小，避免后期变成大方块
            float scaleP = _riseSpeed > 0f ? Mathf.Lerp(1f, 0.4f, p) : Mathf.Lerp(1f, 1.4f, p);
            transform.localScale = _baseScale * scaleP;

            if (_riseSpeed > 0f)
                transform.position += Vector3.up * _riseSpeed * Time.deltaTime;

            if (_renderer != null && _renderer.material != null)
            {
                var c = _renderer.material.color;
                c.a = Mathf.Lerp(c.a, 0f, p);
                _renderer.material.color = c;
            }
        }
    }
}
