using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

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

        protected override void OpenPanel()
        {
            _panelOpen = true;
            EnsurePanel();
            RefreshPanel();
            if (_ui != null) _ui.SetActive(true);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public override void ClosePanel()
        {
            _panelOpen = false;
            if (_ui != null) _ui.SetActive(false);
        }

        // ============================== UI（uGUI+TMP） ==============================

        private GameObject _ui;
        private TextMeshProUGUI _infoLabel;
        private GameObject _currentRow;
        private TextMeshProUGUI _currentLabel;
        private RectTransform _listContainer;

        private void EnsurePanel()
        {
            if (_ui != null) return;

            var canvas = UGuiKit.CreateOverlayCanvas("FormationPlatformUI", 118);
            _ui = canvas.gameObject;
            UGuiKit.CreateScrim(_ui.transform, new Color(0.05f, 0.02f, 0.06f, 0.9f));

            var panel = UGuiKit.CreatePanel(_ui.transform, "Panel", new Vector2(700f, 520f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 8f, new RectOffset(24, 24, 18, 18), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(panel, "🪶 阵法台 · 出梦前布置房间增益", 20, ModuleColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 30f);
            _infoLabel = UGuiKit.CreateText(panel, "", 13, new Color(0.75f, 0.78f, 0.85f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_infoLabel, 22f);

            // 已布置行（可撤销）
            _currentRow = UGuiKit.CreateRow(panel, 10f, 30f).gameObject;
            _currentRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _currentRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
            _currentLabel = UGuiKit.CreateText(_currentRow.transform, "", 13, new Color(1f, 0.87f, 0.93f), TextAlignmentOptions.Right);
            UGuiKit.SetHeight(_currentLabel, 26f); _currentLabel.GetComponent<LayoutElement>().preferredWidth = 420f;
            var cancelBtn = UGuiKit.CreateButton(_currentRow.transform, "撤销布置", () =>
            {
                SaveSystem.Instance.Data.pendingFormationBuffId = "";
                SaveSystem.Instance.Save();
                RefreshPanel();
            }, UGuiKit.BtnNormal, 13, new Vector2(140f, 26f));
            UGuiKit.SetHeight(cancelBtn.GetComponent<RectTransform>(), 26f); cancelBtn.GetComponent<LayoutElement>().preferredWidth = 140f;

            var listHeader = UGuiKit.CreateText(panel, "可选阵法：", 13, UGuiKit.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(listHeader, 22f);

            _listContainer = UGuiKit.CreateScroll(panel, "List", out _, 6f, new RectOffset(6, 6, 6, 6));
            var scrollRoot = (RectTransform)_listContainer.parent;
            var le = UGuiKit.SetHeight(scrollRoot, 300f); le.flexibleHeight = 1f;

            var closeBtn = UGuiKit.CreateButton(panel, "关闭 [ESC]", ClosePanel, new Color(0.25f, 0.25f, 0.3f, 0.9f), 15, new Vector2(180f, 34f));
            UGuiKit.SetHeight(closeBtn.GetComponent<RectTransform>(), 34f);

            _ui.SetActive(false);
        }

        private void RefreshPanel()
        {
            if (_ui == null) return;
            int sigils = SaveSystem.Instance.GetCaveItemCount(SigilMaterial);
            int qi = CaveEconomy.Instance.Qi;
            _infoLabel.text = $"持有 阵法符 ×{sigils}  ·  灵气 {qi}  ·  消耗：阵法符 ×1 + 灵气 {QiCost}";

            var save = SaveSystem.Instance.Data;
            string current = save.pendingFormationBuffId;
            bool hasCurrent = !string.IsNullOrEmpty(current);
            _currentRow.SetActive(hasCurrent);
            if (hasCurrent)
            {
                var entry = FormationLibrary.GetById(current);
                _currentLabel.text = $"★ 已布置：{(entry != null ? entry.displayName : current)}（入秘境时自动激活）";
            }

            for (int i = _listContainer.childCount - 1; i >= 0; i--) Destroy(_listContainer.GetChild(i).gameObject);
            foreach (var entry in FormationLibrary.AllFormations)
                BuildFormationRow(entry, sigils, qi);
        }

        private void BuildFormationRow(FormationEntry entry, int sigils, int qi)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_listContainer, false);
            rowGo.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 1f);
            var le = rowGo.GetComponent<LayoutElement>(); le.preferredHeight = 60f; le.minHeight = 60f;
            UGuiKit.AddHLayout(row, 10f, new RectOffset(12, 12, 6, 6), TextAnchor.MiddleLeft, cChildW: true);

            var name = UGuiKit.CreateText(row, entry.displayName, 14, entry.displayColor, TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(name, 48f); name.GetComponent<LayoutElement>().preferredWidth = 120f;

            var desc = UGuiKit.CreateText(row, entry.description, 12, new Color(0.75f, 0.77f, 0.83f), TextAlignmentOptions.Left);
            desc.enableWordWrapping = true;
            UGuiKit.SetHeight(desc, 48f); var dle = desc.GetComponent<LayoutElement>(); dle.flexibleWidth = 1f; dle.preferredWidth = 380f;

            bool canAfford = sigils >= 1 && qi >= QiCost;
            bool current = SaveSystem.Instance.Data.pendingFormationBuffId == entry.id;
            string btnText = current ? "已布置" : (canAfford ? "布置" : "材料不足");
            var captured = entry;
            var btn = UGuiKit.CreateButton(row, btnText, () => { TryDeploy(captured); RefreshPanel(); }, UGuiKit.BtnPrimary, 13, new Vector2(90f, 30f));
            UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 30f); btn.GetComponent<LayoutElement>().preferredWidth = 90f;
            btn.interactable = canAfford && !current;
        }

        protected override void Update()
        {
            base.Update();
            if (!_panelOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ClosePanel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_ui != null) Destroy(_ui);
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
