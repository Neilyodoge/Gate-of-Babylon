using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵根控制器 —— 监听玩家事件，把灵根的"被动规则"翻译为：
    ///   1. 起手时改 _baseStats（永久基础修正）
    ///   2. 运行期产出 StatusEffect（连杀 BUFF / 地脉护盾 / 反伤标记…）
    ///
    /// 所有"持续生效"的部分都走 StatusEffectController，
    /// 这样灵根的 modifiers 与协同 / 质变 / 灵物 走同一个属性聚合管线，避免冲突。
    /// </summary>
    public class SpiritRootController : MonoBehaviour
    {
        public SpiritRootType CurrentRoot { get; private set; } = SpiritRootType.None;
        public SpiritRootDef CurrentDef => SpiritRootRegistry.Get(CurrentRoot);

        // 火灵根：连杀
        private const string FireKillStreakId = "Root_FireKillStreak";
        private const float FireStreakDuration = 4f;
        private const float FireStreakAtkPerStack = 0.12f;
        private const int FireStreakMaxStacks = 3;

        // 水灵根：反伤标记（无 modifier，仅作为"开关"标识，逻辑在 OnPlayerDamaged 钩子）
        private const string WaterRetaliateId = "Root_WaterRetaliate";

        // 土灵根：地脉护盾
        private const string EarthShieldId = "Root_EarthShield";
        private const int EarthShieldEvery = 5;

        // 木灵根：清房回血在房间清掉时直接回血，不挂 status

        private PlayerController _player;
        private StatusEffectController _status;
        private ItemInventory _inventory;
        private CombatStats _baseStats;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _status = GetComponent<StatusEffectController>();
            _inventory = GetComponent<ItemInventory>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.PlayerDamaged>(OnPlayerDamaged);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
        }

        /// <summary>
        /// 选择灵根（开局或调试用）。会清掉之前的灵根状态，重新应用 baseModifiers。
        /// </summary>
        public void Select(SpiritRootType type, CombatStats baseStats)
        {
            ResetRoot(baseStats);

            CurrentRoot = type;
            _baseStats = baseStats;

            var def = CurrentDef;
            if (def == null) return;

            // 把 baseModifiers 直接合并到 baseStats（这是"永久"修正，不进 StatusEffect）
            ApplyBaseModifiersToBase(def, baseStats, +1);

            // 水灵根：挂一个常驻"反伤标记"以便 HUD 显示
            if (type == SpiritRootType.Water)
            {
                _status?.Apply(new StatusEffect
                {
                    id = WaterRetaliateId,
                    isBuff = true,
                    elementTag = ElementTag.Water,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = -1f,
                    duration = -1f,
                    displayName = "上善若水",
                    description = "受击时反弹 25% 伤害",
                    uiColor = def.displayColor
                });
            }

            // 起手灵物（自动加入背包）
            if (!string.IsNullOrEmpty(def.starterItemName) && _inventory != null && GameManager.Instance != null)
            {
                var starter = GameManager.Instance.FindItemByName(def.starterItemName);
                if (starter != null)
                {
                    _inventory.AddItem(starter);
                    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(def.displayColor)}>起手灵物：{starter.itemName}</color>");
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>灵根起手灵物未找到：{def.starterItemName}（请把它加入 GameManager.itemPool）</color>");
                }
            }

            // 立刻刷新土灵根护盾层数
            UpdateEarthShield();

            if (_inventory != null) _inventory.RecalculatePlayerStats();

            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(def.displayColor)}>选定灵根：{def.name} —— {def.passive}</color>");

            GameEvents.Publish(new GameEvents.SpiritRootSelected
            {
                Root = type,
                DisplayName = def.name,
                Description = def.passive
            });
        }

        public void ResetRoot(CombatStats baseStats)
        {
            if (CurrentRoot == SpiritRootType.None) return;

            var def = CurrentDef;
            if (def != null && baseStats != null)
                ApplyBaseModifiersToBase(def, baseStats, -1);

            _status?.Remove(FireKillStreakId);
            _status?.Remove(WaterRetaliateId);
            _status?.Remove(EarthShieldId);

            CurrentRoot = SpiritRootType.None;
        }

        private static void ApplyBaseModifiersToBase(SpiritRootDef def, CombatStats baseStats, int sign)
        {
            if (def.baseModifiers == null) return;
            foreach (var m in def.baseModifiers)
            {
                float v = m.value * sign;
                switch (m.type)
                {
                    case StatType.AttackDamage:
                        if (m.isPercent) baseStats.attackDamage *= 1f + v;
                        else baseStats.attackDamage += v;
                        break;
                    case StatType.AttackSpeed:
                        baseStats.attackSpeed *= 1f + v;
                        break;
                    case StatType.MaxHp:
                        if (m.isPercent) baseStats.maxHp *= 1f + v;
                        else baseStats.maxHp += v;
                        break;
                    case StatType.MoveSpeed:
                        baseStats.moveSpeed *= 1f + v;
                        break;
                    case StatType.DamageReduction:
                        baseStats.damageReduction = Mathf.Clamp01(baseStats.damageReduction + v);
                        break;
                    case StatType.CritRate:
                        baseStats.critRate = Mathf.Clamp01(baseStats.critRate + v);
                        break;
                    case StatType.CritDamage:
                        baseStats.critDamage += v;
                        break;
                    case StatType.PierceCount:
                        baseStats.pierceCount += Mathf.RoundToInt(v);
                        break;
                    case StatType.ProjectileSpeed:
                        baseStats.projectileSpeed *= 1f + v;
                        break;
                }
            }
        }

        // ==================== 事件钩子 ====================

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (CurrentRoot == SpiritRootType.Fire && _status != null)
            {
                var def = CurrentDef;
                _status.Apply(new StatusEffect
                {
                    id = FireKillStreakId,
                    isBuff = true,
                    elementTag = ElementTag.Fire,
                    stacks = 1,
                    maxStacks = FireStreakMaxStacks,
                    defaultDuration = FireStreakDuration,
                    modifiers = new System.Collections.Generic.List<StatModifier>
                    {
                        StatModifier.Percent(StatType.AttackDamage, FireStreakAtkPerStack)
                    },
                    displayName = "燎原之火",
                    description = $"连杀加成（每层 +{FireStreakAtkPerStack * 100:F0}% 攻击）",
                    uiColor = def != null ? def.displayColor : new Color(1f, 0.4f, 0.1f)
                });
            }
        }

        private void OnPlayerDamaged(GameEvents.PlayerDamaged evt)
        {
            // 水灵根反伤
            if (CurrentRoot != SpiritRootType.Water) return;
            if (evt.Attacker == null) return;
            var dmgable = evt.Attacker.GetComponent<IDamageable>();
            if (dmgable == null) return;

            float retaliate = evt.RawDamage * 0.25f;
            if (retaliate <= 0f) return;

            dmgable.OnDamage(retaliate, evt.Attacker.transform.position, gameObject);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = evt.Attacker.transform.position + Vector3.up * 1.5f,
                Damage = retaliate,
                SpecialTag = "反伤"
            });
        }

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            // 木灵根：清房回血 8% 当前最大生命
            if (CurrentRoot == SpiritRootType.Wood && _player != null)
            {
                float heal = _player.Stats.maxHp * 0.08f;
                _player.Stats.Heal(heal);
                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = _player.Stats.currentHp,
                    MaxHp = _player.Stats.maxHp
                });
                GameEvents.Publish(new GameEvents.DamageNumberRequested
                {
                    WorldPosition = transform.position + Vector3.up * 2f,
                    Damage = heal,
                    SpecialTag = "生生不息"
                });
            }
        }

        private void OnItemPickedUp(GameEvents.ItemPickedUp evt)
        {
            if (CurrentRoot == SpiritRootType.Earth) UpdateEarthShield();
        }

        private void UpdateEarthShield()
        {
            if (CurrentRoot != SpiritRootType.Earth || _status == null || _inventory == null) return;

            int total = 0;
            foreach (var kv in _inventory.Items) total += kv.Value;
            int desired = total / EarthShieldEvery;
            if (desired <= 0)
            {
                _status.Remove(EarthShieldId);
                return;
            }

            var def = CurrentDef;
            var existing = _status.Get(EarthShieldId);
            if (existing == null)
            {
                _status.Apply(new StatusEffect
                {
                    id = EarthShieldId,
                    isBuff = true,
                    elementTag = ElementTag.Earth,
                    stacks = desired,
                    maxStacks = 99,
                    defaultDuration = -1f,
                    duration = -1f,
                    displayName = "地脉护盾",
                    description = "每 5 件灵物 +1 层，吸收一次伤害",
                    uiColor = def != null ? def.displayColor : new Color(0.85f, 0.7f, 0.4f)
                });
            }
            else if (existing.stacks != desired)
            {
                existing.stacks = desired;
            }
        }

        /// <summary>
        /// 由 PlayerController.OnDamage 调用：尝试用一层地脉护盾抵消伤害。
        /// 返回 true 表示已抵挡（外部不应再扣血）。
        /// </summary>
        public bool TryConsumeEarthShield()
        {
            if (CurrentRoot != SpiritRootType.Earth || _status == null) return false;
            var eff = _status.Get(EarthShieldId);
            if (eff == null || eff.stacks <= 0) return false;
            _status.Consume(EarthShieldId, 1);
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 2f,
                Damage = 0,
                SpecialTag = "地脉护盾"
            });
            return true;
        }
    }
}
