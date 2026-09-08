using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// UI 类别构建器：GameCanvas + GameHUD（血条 / 区域进度 / 敌人计数 / 技能栏 / 连招 / 碎片 / 消息 / 死亡&通关面板 / 小地图）。
    /// 挂在场景「UI」根节点上，由 <see cref="Demo1Setup"/> 调用；GameCanvas 挂到本节点下。
    /// </summary>
    public class HudBuilder : MonoBehaviour
    {
        public void BuildHud()
        {
            // ========== Canvas ==========
            var canvasGo = new GameObject("GameCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var hud = canvasGo.AddComponent<GameHUD>();
            ProjectRUITheme uiTheme = ProjectRUITheme.Instance;

            // ========== 左上角：血条区域 ==========
            var hpPanel = CreateUIImage(canvasGo.transform, "HpPanel",
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -72), new Vector2(248, -16),
                Color.white);
            Image hpPanelImage = hpPanel.GetComponent<Image>();
            if (uiTheme?.HealthFrame != null)
            {
                hpPanelImage.sprite = uiTheme.HealthFrame;
                hpPanelImage.type = Image.Type.Simple;
            }

            var hpBarBg = CreateUIImage(hpPanel.transform, "HpBarBg",
                Vector2.zero, Vector2.one,
                new Vector2(42, 17), new Vector2(-12, -17),
                new Color(0.20f, 0.16f, 0.11f, 0.88f));

            var hpBarBorder = CreateUIImage(hpPanel.transform, "HpBarBorder",
                Vector2.zero, Vector2.one,
                new Vector2(41, 16), new Vector2(-11, -16),
                new Color(0.88f, 0.74f, 0.48f, 0.72f));
            hpBarBorder.GetComponent<Image>().raycastTarget = false;

            var hpDamageFill = CreateUIImage(hpPanel.transform, "HpDamageFill",
                Vector2.zero, new Vector2(1, 1),
                new Vector2(44, 19), new Vector2(-14, -19),
                new Color(0.85f, 0.15f, 0.15f, 0.8f));
            hpDamageFill.GetComponent<Image>().type = Image.Type.Filled;
            hpDamageFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;

            var hpSliderGo = new GameObject("HpSlider");
            hpSliderGo.transform.SetParent(hpPanel.transform, false);
            var hpSliderRt = hpSliderGo.AddComponent<RectTransform>();
            hpSliderRt.anchorMin = Vector2.zero;
            hpSliderRt.anchorMax = Vector2.one;
            hpSliderRt.offsetMin = new Vector2(44, 19);
            hpSliderRt.offsetMax = new Vector2(-14, -19);

            var slider = hpSliderGo.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(hpSliderGo.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var hpFill = CreateUIImage(fillArea.transform, "Fill",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.78f, 0.25f, 0.16f));
            slider.fillRect = hpFill.GetComponent<RectTransform>();
            slider.value = 1f;

            var hpText = CreateUIText(hpPanel.transform, "HpText", "100 / 100", 13,
                Vector2.zero, Vector2.one, new Vector2(42, 0), new Vector2(-12, 0));
            hpText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var hpTextOutline = hpText.AddComponent<Outline>();
            hpTextOutline.effectColor = new Color(0, 0, 0, 0.8f);
            hpTextOutline.effectDistance = new Vector2(1, -1);

            SetPrivateField(hud, "hpSlider", slider);
            SetPrivateField(hud, "hpFillImage", hpFill.GetComponent<Image>());
            SetPrivateField(hud, "hpDamageFill", hpDamageFill.GetComponent<Image>());
            SetPrivateField(hud, "hpText", hpText.GetComponent<TextMeshProUGUI>());

            var activeSpiritRoot = CreateUIImage(
                canvasGo.transform,
                "ActiveSpiritStatus",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -290f),
                new Vector2(90f, -82f),
                Color.clear);
            var activeSpiritHud =
                activeSpiritRoot.AddComponent<ActiveSpiritStatusHUD>();
            activeSpiritHud.Configure(
                activeSpiritRoot.GetComponent<RectTransform>());
            canvasGo.AddComponent<SpiritTalentChoiceHUD>();

            // ========== 顶部中央：区域进度 ==========
            var realmPanel = CreateUIImage(canvasGo.transform, "RealmPanel",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-100, -55), new Vector2(100, -10),
                new Color(0, 0, 0, 0));

            var realmText = CreateUIText(realmPanel.transform, "RealmText", "探索区域", 26,
                new Vector2(0, 0.5f), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            var realmTxt = realmText.GetComponent<TextMeshProUGUI>();
            realmTxt.alignment = TextAlignmentOptions.Center;
            realmTxt.color = new Color(1f, 0.85f, 0.3f);
            realmTxt.fontStyle = FontStyles.Bold;
            var realmOutline = realmText.AddComponent<Outline>();
            realmOutline.effectColor = new Color(0, 0, 0, 0.6f);
            realmOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var levelText = CreateUIText(realmPanel.transform, "LevelText", "进度 --", 16,
                new Vector2(0, 0), new Vector2(1, 0.5f),
                Vector2.zero, Vector2.zero);
            var levelTxt = levelText.GetComponent<TextMeshProUGUI>();
            levelTxt.alignment = TextAlignmentOptions.Center;
            levelTxt.color = new Color(0.8f, 0.8f, 0.9f, 0.8f);

            SetPrivateField(hud, "realmText", realmTxt);
            SetPrivateField(hud, "levelText", levelTxt);

            // ========== 右上角：敌人计数（小地图下方） ==========
            var enemyPanel = CreateUIImage(canvasGo.transform, "EnemyPanel",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-180, -110), new Vector2(-20, -75),
                new Color(0.15f, 0.1f, 0.1f, 0.7f));

            var enemyIcon = CreateUIText(enemyPanel.transform, "EnemyIcon", "敌", 18,
                new Vector2(0, 0), new Vector2(0.25f, 1),
                Vector2.zero, Vector2.zero);
            enemyIcon.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            enemyIcon.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.4f, 0.4f);

            var enemyCountText = CreateUIText(enemyPanel.transform, "EnemyCountText", "0 / 0", 18,
                new Vector2(0.25f, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            enemyCountText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            enemyCountText.GetComponent<TextMeshProUGUI>().color = Color.white;

            SetPrivateField(hud, "enemyCountText", enemyCountText.GetComponent<TextMeshProUGUI>());

            // ========== 底部中央：技能栏 + 模块链状态 ==========
            var skillBarContainer = CreateUIImage(canvasGo.transform, "SkillBarContainer",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-340, 4), new Vector2(340, 144),
                new Color(0, 0, 0, 0));

            var skillBarUI = skillBarContainer.AddComponent<SkillBarUI>();

            float skillSize = 84f;
            float skillY = 70f;

            Color[] skillColors = {
                new Color(0.12f, 0.18f, 0.17f, 0.86f),
                new Color(0.12f, 0.18f, 0.17f, 0.86f),
                new Color(0.12f, 0.18f, 0.17f, 0.86f),
                new Color(0.24f, 0.36f, 0.28f, 0.86f),
                new Color(0.45f, 0.25f, 0.14f, 0.88f)
            };
            string[] skillLabels = { "Q", "E", "R", "SPACE", "LMB" };
            string[] carrierGlyphs = { "术", "术", "术", "步", "刃" };
            float[] skillXPositions = { -102f, 0f, 102f, 204f, -204f };
            var skillSlotRTs = new RectTransform[5];

            for (int s = 0; s < 5; s++)
            {
                float sx = skillXPositions[s];
                float halfSkill = skillSize / 2f;

                var skillSlot = CreateUIImage(skillBarContainer.transform, $"Skill_{s}",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(sx - halfSkill, skillY - halfSkill),
                    new Vector2(sx + halfSkill, skillY + halfSkill),
                    skillColors[s]);
                Image skillSlotImage = skillSlot.GetComponent<Image>();
                Sprite slotFrame = s == 1 || s == 2
                    ? uiTheme?.SkillFrameLocked
                    : uiTheme?.SkillFrame;
                if (slotFrame != null)
                {
                    skillSlotImage.sprite = slotFrame;
                    skillSlotImage.type = Image.Type.Simple;
                    skillSlotImage.preserveAspect = true;
                    skillSlotImage.color = Color.white;
                }
                skillSlotRTs[s] = skillSlot.GetComponent<RectTransform>();

                var skillBorder = CreateUIImage(skillSlot.transform, $"SkillBorder_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(-3, -3), new Vector2(3, 3),
                    new Color(1f, 1f, 1f, 0f));
                Image skillBorderImage = skillBorder.GetComponent<Image>();
                skillBorderImage.raycastTarget = false;
                if (uiTheme?.SkillFrameActive != null)
                {
                    skillBorderImage.sprite = uiTheme.SkillFrameActive;
                    skillBorderImage.type = Image.Type.Simple;
                    skillBorderImage.preserveAspect = true;
                }

                var skillIcon = CreateUIImage(skillSlot.transform, $"SkillIcon_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(15, 15), new Vector2(-15, -15),
                    new Color(0.92f, 0.82f, 0.60f, 0.16f));
                Image skillIconImage = skillIcon.GetComponent<Image>();
                Sprite carrierIcon = uiTheme?.CarrierIcon(s);
                if (carrierIcon != null)
                {
                    skillIconImage.sprite = carrierIcon;
                    skillIconImage.type = Image.Type.Simple;
                    skillIconImage.preserveAspect = true;
                    skillIconImage.color = Color.white;
                }

                var carrierGlyph = CreateUIText(
                    skillSlot.transform,
                    "CarrierGlyph",
                    carrierGlyphs[s],
                    20,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                var carrierGlyphText =
                    carrierGlyph.GetComponent<TextMeshProUGUI>();
                carrierGlyphText.alignment =
                    TextAlignmentOptions.Center;
                carrierGlyphText.fontStyle = FontStyles.Bold;
                carrierGlyphText.color =
                    new Color(0.94f, 0.84f, 0.65f, 0.9f);
                carrierGlyph.SetActive(carrierIcon == null);

                var cdFill = CreateUIImage(skillSlot.transform, $"SkillCD_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(2, 2), new Vector2(-2, -2),
                    new Color(0, 0, 0, 0.7f));
                var cdFillImg = cdFill.GetComponent<Image>();
                cdFillImg.sprite = uiTheme?.SkillFrame;
                cdFillImg.type = Image.Type.Filled;
                cdFillImg.fillMethod = Image.FillMethod.Radial360;
                cdFillImg.fillOrigin = (int)Image.Origin360.Top;
                cdFillImg.fillClockwise = false;
                cdFillImg.fillAmount = 0;

                var cdText = CreateUIText(skillSlot.transform, $"SkillCDText_{s}",
                    "", 14,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cdTxt = cdText.GetComponent<TextMeshProUGUI>();
                cdTxt.alignment = TextAlignmentOptions.Center;
                cdTxt.fontStyle = FontStyles.Bold;
                var cdOutline = cdText.AddComponent<Outline>();
                cdOutline.effectColor = new Color(0, 0, 0, 0.8f);
                cdOutline.effectDistance = new Vector2(1, -1);

                var keyCap = CreateUIImage(
                    skillBarContainer.transform,
                    $"KeyCap_{s}",
                    new Vector2(0.5f, 0),
                    new Vector2(0.5f, 0),
                    new Vector2(sx - 22f, 7f),
                    new Vector2(sx + 22f, 25f),
                    Color.white);
                Image keyCapImage = keyCap.GetComponent<Image>();
                if (uiTheme?.Keycap != null)
                {
                    keyCapImage.sprite = uiTheme.Keycap;
                    keyCapImage.type = Image.Type.Simple;
                }
                var keyText = CreateUIText(
                    keyCap.transform,
                    "Key",
                    skillLabels[s],
                    s == 3 ? 8 : 9,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                var keyTxt = keyText.GetComponent<TextMeshProUGUI>();
                keyTxt.alignment = TextAlignmentOptions.Center;
                keyTxt.fontStyle = FontStyles.Bold;
                keyTxt.color = new Color(0.24f, 0.18f, 0.12f);

                switch (s)
                {
                    case 0:
                        SetPrivateField(hud, "skillQCooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillQCooldownText", cdTxt);
                        SetPrivateField(hud, "skillQIcon", skillIconImage);
                        break;
                    case 1:
                        SetPrivateField(hud, "skillECooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillECooldownText", cdTxt);
                        SetPrivateField(hud, "skillEIcon", skillIconImage);
                        break;
                    case 2:
                        SetPrivateField(hud, "skillRCooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillRCooldownText", cdTxt);
                        SetPrivateField(hud, "skillRIcon", skillIconImage);
                        break;
                    case 3:
                        SetPrivateField(hud, "dashCooldownFill", cdFillImg);
                        SetPrivateField(hud, "dashCooldownText", cdTxt);
                        break;
                }

                if (s < 3)
                {
                    var chainLabel = CreateUIText(skillBarContainer.transform, $"ChainLabel_{s}",
                        "", 11,
                        new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                        new Vector2(sx - 48, skillY + halfSkill + 2),
                        new Vector2(sx + 48, skillY + halfSkill + 14));
                    var chainTxt = chainLabel.GetComponent<TextMeshProUGUI>();
                    chainTxt.alignment = TextAlignmentOptions.Center;
                    chainTxt.color = new Color(0.4f, 0.9f, 0.8f, 0.7f);
                    chainTxt.fontSize = 9;
                    chainTxt.enableWordWrapping = false;
                    var chainOutline = chainLabel.AddComponent<Outline>();
                    chainOutline.effectColor = new Color(0, 0, 0, 0.8f);
                    chainOutline.effectDistance = new Vector2(1, -1);
                }
            }

            SetPrivateField(skillBarUI, "skillSlotRTs", skillSlotRTs);
            var spiritCircuitHUD =
                skillBarContainer.AddComponent<SpiritCircuitHUD>();
            spiritCircuitHUD.Configure(skillSlotRTs);

            // ========== 连招指示器（技能栏上方） ==========
            var comboPanel = CreateUIImage(canvasGo.transform, "ComboPanel",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-40, 128), new Vector2(40, 140),
                new Color(0, 0, 0, 0));

            var comboIndicators = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 22f;
                var dot = CreateUIImage(comboPanel.transform, $"ComboDot_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x - 4, -4), new Vector2(x + 4, 4),
                    new Color(0.30f, 0.24f, 0.16f, 0.55f));
                comboIndicators[i] = dot.GetComponent<Image>();
            }
            SetPrivateField(hud, "comboIndicators", comboIndicators);

            // ========== ProjectR回路总览 / Legacy模块链回退 ==========
            if (FeatureFlags.EnableCarrierRuntime)
            {
                canvasGo.AddComponent<SpiritCircuitOverviewUI>();
            }
            else
            {
                canvasGo.AddComponent<ModuleAssemblyUI>();
                var procOverlay =
                    canvasGo.AddComponent<ModuleChainProcOverlay>();
                procOverlay.SetSkillSlots(skillSlotRTs);
                canvasGo.AddComponent<ProcBarsHUD>();
            }

            // ========== 左下角：碎片计数 ==========
            var shardPanel = CreateUIImage(canvasGo.transform, "ShardPanel",
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 55), new Vector2(160, 90),
                new Color(0.1f, 0.1f, 0.18f, 0.7f));

            var shardIcon = CreateUIText(shardPanel.transform, "ShardIcon", "碎", 16,
                new Vector2(0, 0), new Vector2(0.25f, 1),
                Vector2.zero, Vector2.zero);
            shardIcon.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            shardIcon.GetComponent<TextMeshProUGUI>().color = new Color(0.5f, 0.7f, 1f);

            var shardCountText = CreateUIText(shardPanel.transform, "ShardCountText", "0", 16,
                new Vector2(0.25f, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            shardCountText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            shardCountText.GetComponent<TextMeshProUGUI>().color = new Color(0.5f, 0.8f, 1f);
            SetPrivateField(hud, "shardCountText", shardCountText.GetComponent<TextMeshProUGUI>());

            // ========== 中央偏下：消息提示 ==========
            var msgText = CreateUIText(canvasGo.transform, "MessageText", "", 20,
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
                new Vector2(-250, -15), new Vector2(250, 15));
            var msgTxt = msgText.GetComponent<TextMeshProUGUI>();
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.color = Color.white;
            msgTxt.richText = true;
            var msgOutline = msgText.AddComponent<Outline>();
            msgOutline.effectColor = new Color(0, 0, 0, 0.7f);
            msgOutline.effectDistance = new Vector2(1, -1);
            SetPrivateField(hud, "messageText", msgTxt);

            // ========== 底部：操作提示 ==========
            var controlsHint = CreateUIText(canvasGo.transform, "ControlsHint",
            "", 10,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-380, 2), new Vector2(380, 14));
            var hintTxt = controlsHint.GetComponent<TextMeshProUGUI>();
            hintTxt.alignment = TextAlignmentOptions.Center;
            hintTxt.color = new Color(1, 1, 1, 0.3f);

            // ========== 死亡面板 ==========
            CreateDeathPanel(canvasGo.transform, hud);

            // ========== 通关面板 ==========
            CreateWinPanel(canvasGo.transform, hud);

            // ========== 伤害飘字 ==========
            var dmgPopup = canvasGo.AddComponent<DamagePopup>();
            SetPrivateField(dmgPopup, "canvas", canvas);

            // ========== 小地图 ==========
            CreateMinimap(canvasGo.transform);
        }

        /// <summary>创建小地图</summary>
        private void CreateMinimap(Transform canvasTransform)
        {
            var mapPanel = CreateUIImage(canvasTransform, "MinimapPanel",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-220, -55), new Vector2(-20, -15),
                new Color(0, 0, 0, 0.5f));

            var minimap = mapPanel.gameObject.AddComponent<Minimap>();
            SetPrivateField(minimap, "mapPanel", mapPanel.GetComponent<RectTransform>());

            var playerDot = CreateUIImage(mapPanel.transform, "PlayerDot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-5, 5), new Vector2(5, 15),
                new Color(0.2f, 1f, 0.4f));
            SetPrivateField(minimap, "playerDot", playerDot.GetComponent<Image>());

            var title = CreateUIText(mapPanel.transform, "MapTitle", "地图", 12,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(2, -18), new Vector2(-2, -2));
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            title.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.7f, 0.5f);

            var legendText = CreateUIText(canvasTransform, "MinimapLegend",
                "战斗  精英  ?事件  $商店  休息  BOSS", 10,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-220, -72), new Vector2(-20, -57));
            legendText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            legendText.GetComponent<TextMeshProUGUI>().color = new Color(0.55f, 0.6f, 0.7f, 0.8f);

            if (GameManager.Instance != null)
                GameManager.Instance.SetMinimap(minimap);
        }

        /// <summary>创建死亡面板</summary>
        private void CreateDeathPanel(Transform parent, GameHUD hud)
        {
            var panel = CreateUIImage(parent, "DeathPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0, 0, 0.75f));

            var title = CreateUIText(panel.transform, "DeathTitle", "探索失败", 48,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(-200, -30), new Vector2(200, 30));
            var titleTxt = title.GetComponent<TextMeshProUGUI>();
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(0.9f, 0.2f, 0.2f);
            titleTxt.fontStyle = FontStyles.Bold;
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(2, -2);

            var sub = CreateUIText(panel.transform, "DeathSubText", "", 20,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<TextMeshProUGUI>();
            subTxt.alignment = TextAlignmentOptions.Center;
            subTxt.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);

            var btnGo = CreateUIImage(panel.transform, "RestartButton",
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
                new Vector2(-80, -22), new Vector2(80, 22),
                new Color(0.8f, 0.25f, 0.25f, 0.9f));
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnGo.GetComponent<Image>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(1f, 0.35f, 0.35f);
            btnColors.pressedColor = new Color(0.6f, 0.15f, 0.15f);
            btn.colors = btnColors;

            var btnText = CreateUIText(btnGo.transform, "BtnText", "重新入秘境", 18,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            btnText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            btnText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            panel.SetActive(false);

            SetPrivateField(hud, "deathPanel", panel);
            SetPrivateField(hud, "deathTitleText", titleTxt);
            SetPrivateField(hud, "deathSubText", subTxt);
            SetPrivateField(hud, "restartButton", btn);
        }

        /// <summary>创建通关面板</summary>
        private void CreateWinPanel(Transform parent, GameHUD hud)
        {
            var panel = CreateUIImage(parent, "WinPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0.03f, 0, 0.75f));

            var title = CreateUIText(panel.transform, "WinTitle", "✨ 通关成功 ✨", 48,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(-250, -30), new Vector2(250, 30));
            var titleTxt = title.GetComponent<TextMeshProUGUI>();
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = new Color(1f, 0.85f, 0.2f);
            titleTxt.fontStyle = FontStyles.Bold;
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(2, -2);

            var sub = CreateUIText(panel.transform, "WinSubText", "秘境征服，冒险圆满", 22,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<TextMeshProUGUI>();
            subTxt.alignment = TextAlignmentOptions.Center;
            subTxt.color = new Color(1f, 0.95f, 0.8f, 0.9f);

            var btnGo = CreateUIImage(panel.transform, "WinRestartButton",
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
                new Vector2(-80, -22), new Vector2(80, 22),
                new Color(0.85f, 0.7f, 0.15f, 0.9f));
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnGo.GetComponent<Image>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(1f, 0.85f, 0.3f);
            btnColors.pressedColor = new Color(0.6f, 0.5f, 0.1f);
            btn.colors = btnColors;

            var btnText = CreateUIText(btnGo.transform, "BtnText", "再入秘境", 18,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            btnText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            btnText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            btnText.GetComponent<TextMeshProUGUI>().color = new Color(0.1f, 0.08f, 0);

            panel.SetActive(false);

            SetPrivateField(hud, "winPanel", panel);
            SetPrivateField(hud, "winTitleText", titleTxt);
            SetPrivateField(hud, "winSubText", subTxt);
            SetPrivateField(hud, "winRestartButton", btn);
        }

        // ========== UI 工具方法 ==========

        private GameObject CreateUIImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private GameObject CreateUIText(Transform parent, string name, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            if (UGuiKit.CjkFont != null) t.font = UGuiKit.CjkFont;
            t.color = Color.white;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return go;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

    }
}
