using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 阵法台 · 第七个洞府模块（v0.5 Week 4 终章）。
    ///
    /// 玩家在出梦前消耗一张【阵法符】+ 灵气，"预置"一种阵法增益到 <see cref="SaveDataV1.pendingFormationBuffId"/>，
    /// 进入下一局梦境时 <see cref="GameManager.StartNewRun"/> 调用 <see cref="FormationBuffApplier.Apply"/>
    /// 一次性挂到玩家身上（贯穿整局 / 死亡或撤离时清空）。
    ///
    /// 与悟道蒲团的差异：悟道蒲团是【永久天赋】，阵法台是【一次性增益】，每局都要消耗符。
    /// </summary>
    public class FormationPlatform : CaveModule
    {
        public override string ModuleName => "阵法台";
        public override string ModuleIcon => "🪶";
        public override string ModuleRole => "阵法符 → 单局开场增益";
        public override Color ModuleColor => new Color(1f, 0.7f, 0.95f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private const string SigilMaterial = "阵法符";
        private const int QiCost = 30;

        protected override void BuildBody()
        {
            Color violet = new Color(0.85f, 0.55f, 1f);

            // —— 五角星紫色阵纹 + 外圈圆 ——
            CaveVfx.SpawnPentagramRune(transform, Vector3.zero, 2.0f, violet, 0.08f);

            // —— 六边形矮台 ——
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "FormationDais";
            dais.transform.SetParent(transform, false);
            dais.transform.localPosition = new Vector3(0, 0.12f, 0);
            dais.transform.localScale = new Vector3(2.0f, 0.25f, 2.0f);
            var col = dais.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = dais.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.28f, 0.16f, 0.36f), ModuleColor * 0.7f);
                rend.material = mat;
            }

            // —— 中央紫色光柱（出梦前阵法激活感）——
            CaveVfx.SpawnLightBeam(transform, new Vector3(0, 0.3f, 0),
                height: 2.6f, baseRadius: 0.28f, color: violet);

            // —— 中央水晶柱（顶端发光宝石）——
            var crystalBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crystalBase.name = "CrystalBase";
            crystalBase.transform.SetParent(transform, false);
            crystalBase.transform.localPosition = new Vector3(0, 0.85f, 0);
            crystalBase.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);
            var cbcol = crystalBase.GetComponent<Collider>();
            if (cbcol != null) Destroy(cbcol);
            var cbrend = crystalBase.GetComponent<Renderer>();
            if (cbrend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.5f, 0.3f, 0.6f), violet * 1.4f);
                cbrend.material = mat;
            }
            CaveVfx.SpawnHoveringObject(transform, new Vector3(0, 1.7f, 0),
                PrimitiveType.Cube, Vector3.one * 0.28f,
                new Color(0.85f, 0.55f, 1f), violet * 2.6f,
                hoverAmp: 0.08f, hoverFreq: 1.3f, spinSpeed: 90f);

            // —— 旋转的三道符箓（位置上移到水晶柱周围）——
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(ang) * 1.3f, 1.1f, Mathf.Sin(ang) * 1.3f);
                var card = CaveVfx.SpawnHoveringObject(transform, pos,
                    PrimitiveType.Cube, new Vector3(0.35f, 0.5f, 0.05f),
                    new Color(1f, 0.85f, 0.6f), ModuleColor * 1.4f,
                    hoverAmp: 0.18f, hoverFreq: 1.4f, spinSpeed: 0f);
                if (card != null)
                    card.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)));
            }

            // —— 6 颗紫色灵气围绕水晶柱旋转 ——
            CaveVfx.SpawnOrbitingParticles(transform, new Vector3(0, 1.7f, 0),
                count: 6, orbitRadius: 0.6f, orbitHeight: 0f,
                particleSize: 0.1f, color: violet,
                orbitSpeed: 120f, verticalBob: 0.2f);
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel() => _panelOpen = false;

        // ============================== UI ==============================

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 660f, H = 460f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("🪶 阵法台 · 出梦前布置房间增益", titleStyle);

            int sigils = SaveSystem.Instance.GetCaveItemCount(SigilMaterial);
            int qi = CaveEconomy.Instance.Qi;
            GUILayout.Label($"持有 阵法符 ×{sigils}  ·  灵气 {qi}  ·  消耗：阵法符 ×1 + 灵气 {QiCost}",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUILayout.Space(6);

            var save = SaveSystem.Instance.Data;
            string current = save.pendingFormationBuffId;
            if (!string.IsNullOrEmpty(current))
            {
                var entry = FormationLibrary.GetById(current);
                string desc = entry != null ? entry.displayName : current;
                GUILayout.Label($"<color=#ffdfee>★ 已布置：{desc}（入秘境时自动激活）</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                GUILayout.Space(4);
                if (GUILayout.Button("撤销布置（不退还符 / 灵气）", GUILayout.Height(24)))
                {
                    save.pendingFormationBuffId = "";
                    SaveSystem.Instance.Save();
                }
                GUILayout.Space(8);
            }

            GUILayout.Label("可选阵法：", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });

            foreach (var entry in FormationLibrary.AllFormations)
            {
                DrawFormationRow(entry, sigils, qi);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                ClosePanel();
        }

        private void DrawFormationRow(FormationEntry entry, int sigils, int qi)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            nameStyle.normal.textColor = entry.displayColor;
            GUILayout.Label(entry.displayName, nameStyle, GUILayout.Width(120));

            GUILayout.Label(entry.description, new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true },
                GUILayout.Width(380));

            bool canAfford = sigils >= 1 && qi >= QiCost;
            bool current = SaveSystem.Instance.Data.pendingFormationBuffId == entry.id;
            GUI.enabled = canAfford && !current;
            string btn = current ? "已布置" : (canAfford ? "布置" : "材料不足");
            if (GUILayout.Button(btn, GUILayout.Width(80), GUILayout.Height(28)))
            {
                TryDeploy(entry);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        // ============================== 逻辑 ==============================

        private void TryDeploy(FormationEntry entry)
        {
            if (SaveSystem.Instance.GetCaveItemCount(SigilMaterial) < 1)
            {
                Debug.Log("<color=red>[阵法台] 阵法符不足</color>");
                return;
            }
            if (CaveEconomy.Instance.Qi < QiCost)
            {
                Debug.Log("<color=red>[阵法台] 灵气不足</color>");
                return;
            }

            SaveSystem.Instance.ConsumeCaveItem(SigilMaterial, 1);
            CaveEconomy.Instance.SpendQi(QiCost);
            SaveSystem.Instance.Data.pendingFormationBuffId = entry.id;
            SaveSystem.Instance.Save();

            GameEvents.Publish(new GameEvents.FormationDeployed { FormationId = entry.id });

            Debug.Log($"<color=#ffdfee>[阵法台] 已布置阵法：{entry.displayName}（下次入梦自动激活）</color>");
        }
    }

    // ===================================================================
    //                          阵法定义
    // ===================================================================

    public class FormationEntry
    {
        public string id;            // 内部 id（写入 SaveData.pendingFormationBuffId）
        public string displayName;
        public string description;
        public Color displayColor;
        public List<StatModifier> modifiers;
    }

    public static class FormationLibrary
    {
        private static List<FormationEntry> _cache;
        public static IReadOnlyList<FormationEntry> AllFormations
        {
            get { if (_cache == null) Build(); return _cache; }
        }

        public static FormationEntry GetById(string id)
        {
            foreach (var e in AllFormations)
                if (e.id == id) return e;
            return null;
        }

        private static void Build()
        {
            _cache = new List<FormationEntry>();

            _cache.Add(new FormationEntry
            {
                id = "Formation_GoldArmy",
                displayName = "金兵阵",
                description = "<i>「兵者，金也。锋芒所指，万军披靡。」</i>\n· 攻击力 +25%（整局）",
                displayColor = new Color(1f, 0.85f, 0.3f),
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, 0.25f)
                }
            });

            _cache.Add(new FormationEntry
            {
                id = "Formation_Earthwall",
                displayName = "土墉阵",
                description = "<i>「以土为墙，雷劫不入。」</i>\n· 减伤 +15% · 最大生命 +20%（整局）",
                displayColor = new Color(0.85f, 0.7f, 0.4f),
                modifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.DamageReduction, 0.15f),
                    StatModifier.Percent(StatType.MaxHp, 0.20f)
                }
            });

            _cache.Add(new FormationEntry
            {
                id = "Formation_Windstep",
                displayName = "风行阵",
                description = "<i>「足下生风，闪转腾挪。」</i>\n· 移速 +20% · 攻速 +15%（整局）",
                displayColor = new Color(0.6f, 0.95f, 0.95f),
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MoveSpeed, 0.20f),
                    StatModifier.Percent(StatType.AttackSpeed, 0.15f)
                }
            });

            _cache.Add(new FormationEntry
            {
                id = "Formation_FateEye",
                displayName = "命眼阵",
                description = "<i>「天眼一开，命脉无形。」</i>\n· 暴击率 +12% · 暴击伤害 +30%（整局）",
                displayColor = new Color(1f, 0.55f, 0.85f),
                modifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.CritRate, 0.12f),
                    StatModifier.Flat(StatType.CritDamage, 0.30f)
                }
            });
        }
    }

    /// <summary>入梦时一次性把 pendingFormationBuffId 挂为常驻 StatusEffect，挂完即清空 pendingFormationBuffId。</summary>
    public static class FormationBuffApplier
    {
        public static void Apply(PlayerController player)
        {
            if (player == null) return;
            var save = SaveSystem.Instance.Data;
            if (string.IsNullOrEmpty(save.pendingFormationBuffId)) return;

            var entry = FormationLibrary.GetById(save.pendingFormationBuffId);
            if (entry == null)
            {
                save.pendingFormationBuffId = "";
                SaveSystem.Instance.Save();
                return;
            }

            var status = player.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.Apply(new StatusEffect
                {
                    id = "Formation_" + entry.id,
                    isBuff = true,
                    elementTag = ElementTag.None,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = -1f,
                    duration = -1f,
                    modifiers = entry.modifiers,
                    displayName = entry.displayName,
                    description = entry.description,
                    uiColor = entry.displayColor
                });
                Debug.Log($"<color=#ffdfee>[FormationBuffApplier] 阵法激活：{entry.displayName}（贯穿整局）</color>");
            }

            // 一次性消耗
            save.pendingFormationBuffId = "";
            SaveSystem.Instance.Save();
        }
    }

    // 视觉小帮手：让符箓上下飘动 + 自转
    internal class SimpleHover : MonoBehaviour
    {
        private float _phase;
        private Vector3 _baseLocalPos;
        private void Awake()
        {
            _baseLocalPos = transform.localPosition;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }
        private void Update()
        {
            transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);
            transform.localPosition = _baseLocalPos + new Vector3(0, Mathf.Sin(Time.time * 2f + _phase) * 0.12f, 0);
        }
    }
}
