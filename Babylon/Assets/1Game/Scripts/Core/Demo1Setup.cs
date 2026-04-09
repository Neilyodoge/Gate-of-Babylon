using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// Demo1 场景快速搭建器
    /// 挂载到空 GameObject 上，运行时自动创建所有必要对象
    /// 支持使用 Frank_Katana 真实模型 + 刀光/打击特效
    /// </summary>
    public class Demo1Setup : MonoBehaviour
    {
        [Header("灵物池（可选，不配置则使用内置测试数据）")]
        [SerializeField] private ItemData[] itemPool;

        [Header("技能（可选）")]
        [SerializeField] private SkillData testSkillQ;
        [SerializeField] private SkillData testSkillE;

        [Header("角色模型 Prefab（可选，不配置则自动创建胶囊体）")]
        [SerializeField] private GameObject playerModelPrefab;

        [Header("Animator Controller（可选）")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        [Header("刀光特效 Prefab（可选）")]
        [SerializeField] private GameObject slashVFXPrefab;

        [Header("打击特效 Prefab（可选）")]
        [SerializeField] private GameObject hitVFXPrefab;

        [Header("投射物 Prefab（可选，不配置则自动创建）")]
        [SerializeField] private GameObject projectilePrefab;

        private void Awake()
        {
            // 1. 创建对象池
            CreateObjectPool();

            // 2. 创建地面
            CreateGround();

            // 3. 创建玩家
            CreatePlayer();

            // 4. 创建相机
            SetupCamera();

            // 5. 创建游戏管理器
            CreateGameManager();

            // 6. 创建 HUD
            CreateHUD();

            // 7. 设置光照
            SetupLighting();

            // 8. 创建顿帧系统
            CreateHitStop();

            // 9. 创建层间过渡
            CreateLevelTransition();

            // 10. 创建后处理效果
            CreatePostProcess();
        }

        private void CreateObjectPool()
        {
            var poolGo = new GameObject("ObjectPool");
            poolGo.AddComponent<ObjectPool>();
        }

        private void CreateGround()
        {
            // 地面和墙壁现在由 BattleRoom + RoomBuilder 动态生成
            // 这里只创建一个临时的小地面，防止玩家在房间生成前掉落
            var tempGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tempGround.name = "TempGround";
            tempGround.transform.position = Vector3.zero;
            tempGround.transform.localScale = new Vector3(1, 1, 1);
            var renderer = tempGround.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.12f, 0.14f, 0.18f);
                renderer.material = mat;
            }
            // 房间生成后会覆盖这个临时地面
            Destroy(tempGround, 1f);
        }

        private void CreatePlayer()
        {
            // 创建玩家根 GameObject
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 0, 0);

            // CharacterController（Frank_Katana 尺寸）
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            // ========== 角色模型 ==========
            Transform modelTransform;
            Animator modelAnimator = null;

            if (playerModelPrefab != null)
            {
                // 使用真实模型
                var model = Instantiate(playerModelPrefab, playerGo.transform);
                model.name = "PlayerModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                modelTransform = model.transform;
                modelAnimator = model.GetComponentInChildren<Animator>();

                // 如果有 Animator Controller，设置它
                if (animatorController != null && modelAnimator != null)
                {
                    modelAnimator.runtimeAnimatorController = animatorController;
                }

                // 关闭 Root Motion，移动完全由 CharacterController 控制
                if (modelAnimator != null)
                {
                    modelAnimator.applyRootMotion = false;
                }
            }
            else
            {
                // 回退：使用胶囊体
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.name = "PlayerModel";
                model.transform.SetParent(playerGo.transform);
                model.transform.localPosition = new Vector3(0, 1f, 0);
                model.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

                var modelCol = model.GetComponent<Collider>();
                if (modelCol != null) Destroy(modelCol);

                var rend = model.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.3f, 0.6f, 1f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.1f, 0.2f, 0.4f));
                    rend.material = mat;
                }

                // 朝向指示器
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                indicator.name = "DirectionIndicator";
                indicator.transform.SetParent(model.transform);
                indicator.transform.localPosition = new Vector3(0, 0, 0.8f);
                indicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);
                var indCol = indicator.GetComponent<Collider>();
                if (indCol != null) Destroy(indCol);
                var indRenderer = indicator.GetComponent<Renderer>();
                if (indRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(1f, 0.8f, 0.2f);
                    indRenderer.material = mat;
                }

                modelTransform = model.transform;
            }

            // ========== 攻击原点 & 刀光生成点（适配 Frank_Katana 模型） ==========
            var attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(playerGo.transform);
            attackOrigin.transform.localPosition = new Vector3(0, 0.9f, 0.6f);

            var slashSpawnPoint = new GameObject("SlashVFXPoint");
            slashSpawnPoint.transform.SetParent(playerGo.transform);
            slashSpawnPoint.transform.localPosition = new Vector3(0, 1.0f, 0.8f);

            // ========== 添加组件 ==========
            var playerCtrl = playerGo.AddComponent<PlayerController>();
            playerCtrl.SetModelTransform(modelTransform);

            var playerAnim = playerGo.AddComponent<PlayerAnimator>();
            if (modelAnimator != null)
            {
                playerAnim.SetAnimator(modelAnimator);

                // 在 Animator 所在的 GameObject 上添加动画事件转发器
                // 这样动画剪辑中的 AnimationEvent 才能被正确接收并转发给 PlayerAnimator
                var animatorGo = modelAnimator.gameObject;
                if (animatorGo.GetComponent<AnimationEventRelay>() == null)
                {
                    animatorGo.AddComponent<AnimationEventRelay>();
                }
            }

            var combat = playerGo.AddComponent<PlayerCombat>();

            // 设置近战攻击参数
            combat.SetAttackOrigin(attackOrigin.transform);
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex >= 0)
                combat.SetEnemyLayer(1 << enemyLayerIndex);
            else
                combat.SetEnemyLayer(LayerMask.GetMask("Default"));

            // 设置刀光特效
            if (slashVFXPrefab != null)
            {
                combat.SetSlashVFX(slashVFXPrefab, slashSpawnPoint.transform);
            }

            // 设置打击特效
            if (hitVFXPrefab != null)
            {
                combat.SetHitVFX(hitVFXPrefab);
            }

            // 设置测试技能（如果 Inspector 中没有配置，运行时创建默认技能）
            if (testSkillQ != null)
            {
                combat.EquipSkillQ(testSkillQ);
            }
            else
            {
                // 兜底：运行时创建默认落石术
                var fallbackQ = ScriptableObject.CreateInstance<SkillData>();
                fallbackQ.skillName = "落石术";
                fallbackQ.description = "召唤巨石砸落指定位置";
                fallbackQ.skillType = SkillType.AreaDamage;
                fallbackQ.baseDamage = 30f;
                fallbackQ.damageScaling = 0.5f;
                fallbackQ.cooldown = 8f;
                fallbackQ.aoeRadius = 3f;
                fallbackQ.vfxDuration = 1.5f;
                combat.EquipSkillQ(fallbackQ);
                Debug.Log("<color=yellow>[Demo1Setup] Q技能未配置，已使用内置落石术</color>");
            }

            if (testSkillE != null)
            {
                combat.EquipSkillE(testSkillE);
            }
            else
            {
                // 兜底：运行时创建默认金钟罩
                var fallbackE = ScriptableObject.CreateInstance<SkillData>();
                fallbackE.skillName = "金钟罩";
                fallbackE.description = "凝聚灵力化为护罩，大幅提升减伤";
                fallbackE.skillType = SkillType.Buff;
                fallbackE.baseDamage = 0f;
                fallbackE.cooldown = 12f;
                fallbackE.vfxDuration = 5f;
                combat.EquipSkillE(fallbackE);
                Debug.Log("<color=yellow>[Demo1Setup] E技能未配置，已使用内置金钟罩</color>");
            }

            playerGo.AddComponent<ItemInventory>();
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            if (cam.GetComponent<TopDownCamera>() == null)
                cam.gameObject.AddComponent<TopDownCamera>();
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
        }

        private void CreateGameManager()
        {
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();

            // 设置灵物池
            if (itemPool != null && itemPool.Length > 0)
            {
                var poolField = typeof(GameManager).GetField("itemPool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                poolField?.SetValue(gm, itemPool);
            }

            // 设置打击特效给 GameManager，让它传递给生成的敌人
            if (hitVFXPrefab != null)
            {
                var hitField = typeof(GameManager).GetField("enemyHitVFXPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                hitField?.SetValue(gm, hitVFXPrefab);
            }
        }

        private void CreateHUD()
        {
            // ========== Canvas ==========
            var canvasGo = new GameObject("GameCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var hud = canvasGo.AddComponent<GameHUD>();

            // ========== 左上角：血条区域 ==========
            var hpPanel = CreateUIImage(canvasGo.transform, "HpPanel",
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -65), new Vector2(340, -15),
                new Color(0, 0, 0, 0)); // 透明容器

            // 血条背景（深色圆角感）
            var hpBarBg = CreateUIImage(hpPanel.transform, "HpBarBg",
                Vector2.zero, Vector2.one,
                new Vector2(0, 0), new Vector2(0, 0),
                new Color(0.1f, 0.1f, 0.15f, 0.9f));

            // 血条边框
            var hpBarBorder = CreateUIImage(hpPanel.transform, "HpBarBorder",
                Vector2.zero, Vector2.one,
                new Vector2(-1, -1), new Vector2(1, 1),
                new Color(0.4f, 0.4f, 0.5f, 0.6f));
            hpBarBorder.GetComponent<Image>().raycastTarget = false;

            // 受伤延迟条（红色，在绿色血条下面）
            var hpDamageFill = CreateUIImage(hpPanel.transform, "HpDamageFill",
                Vector2.zero, new Vector2(1, 1),
                new Vector2(3, 3), new Vector2(-3, -3),
                new Color(0.85f, 0.15f, 0.15f, 0.8f));
            hpDamageFill.GetComponent<Image>().type = Image.Type.Filled;
            hpDamageFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;

            // 血条 Slider
            var hpSliderGo = new GameObject("HpSlider");
            hpSliderGo.transform.SetParent(hpPanel.transform, false);
            var hpSliderRt = hpSliderGo.AddComponent<RectTransform>();
            hpSliderRt.anchorMin = Vector2.zero;
            hpSliderRt.anchorMax = Vector2.one;
            hpSliderRt.offsetMin = new Vector2(3, 3);
            hpSliderRt.offsetMax = new Vector2(-3, -3);

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
                new Color(0.2f, 0.85f, 0.35f));
            slider.fillRect = hpFill.GetComponent<RectTransform>();
            slider.value = 1f;

            // 血条文字
            var hpText = CreateUIText(hpPanel.transform, "HpText", "100 / 100", 16,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hpText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            // 血条文字描边
            var hpTextOutline = hpText.AddComponent<Outline>();
            hpTextOutline.effectColor = new Color(0, 0, 0, 0.8f);
            hpTextOutline.effectDistance = new Vector2(1, -1);

            SetPrivateField(hud, "hpSlider", slider);
            SetPrivateField(hud, "hpFillImage", hpFill.GetComponent<Image>());
            SetPrivateField(hud, "hpDamageFill", hpDamageFill.GetComponent<Image>());
            SetPrivateField(hud, "hpText", hpText.GetComponent<Text>());

            // ========== 顶部中央：境界信息 ==========
            var realmPanel = CreateUIImage(canvasGo.transform, "RealmPanel",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-100, -55), new Vector2(100, -10),
                new Color(0, 0, 0, 0)); // 透明容器

            var realmText = CreateUIText(realmPanel.transform, "RealmText", "练气期", 26,
                new Vector2(0, 0.5f), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            var realmTxt = realmText.GetComponent<Text>();
            realmTxt.alignment = TextAnchor.MiddleCenter;
            realmTxt.color = new Color(1f, 0.85f, 0.3f);
            realmTxt.fontStyle = FontStyle.Bold;
            var realmOutline = realmText.AddComponent<Outline>();
            realmOutline.effectColor = new Color(0, 0, 0, 0.6f);
            realmOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var levelText = CreateUIText(realmPanel.transform, "LevelText", "第 1 层", 16,
                new Vector2(0, 0), new Vector2(1, 0.5f),
                Vector2.zero, Vector2.zero);
            var levelTxt = levelText.GetComponent<Text>();
            levelTxt.alignment = TextAnchor.MiddleCenter;
            levelTxt.color = new Color(0.8f, 0.8f, 0.9f, 0.8f);

            SetPrivateField(hud, "realmText", realmTxt);
            SetPrivateField(hud, "levelText", levelTxt);

            // ========== 右上角：敌人计数 ==========
            var enemyPanel = CreateUIImage(canvasGo.transform, "EnemyPanel",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-180, -50), new Vector2(-20, -15),
                new Color(0.15f, 0.1f, 0.1f, 0.7f));

            // 骷髅图标（用文字代替）
            var enemyIcon = CreateUIText(enemyPanel.transform, "EnemyIcon", "☠", 22,
                new Vector2(0, 0), new Vector2(0.25f, 1),
                Vector2.zero, Vector2.zero);
            enemyIcon.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            enemyIcon.GetComponent<Text>().color = new Color(1f, 0.4f, 0.4f);

            var enemyCountText = CreateUIText(enemyPanel.transform, "EnemyCountText", "0 / 0", 18,
                new Vector2(0.25f, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            enemyCountText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            enemyCountText.GetComponent<Text>().color = Color.white;

            SetPrivateField(hud, "enemyCountText", enemyCountText.GetComponent<Text>());

            // ========== 底部中央：技能栏 ==========
            var skillBar = CreateUIImage(canvasGo.transform, "SkillBar",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-220, 15), new Vector2(220, 95),
                new Color(0, 0, 0, 0)); // 透明容器

            // --- Q 技能槽 ---
            var skillQSlot = CreateSkillSlot(skillBar.transform, "SkillQ", new Vector2(-110, 0), "Q",
                new Color(0.3f, 0.5f, 1f, 0.8f));
            SetPrivateField(hud, "skillQCooldownFill", skillQSlot.cdFill);
            SetPrivateField(hud, "skillQCooldownText", skillQSlot.cdText);
            SetPrivateField(hud, "skillQIcon", skillQSlot.iconImage);

            // --- E 技能槽 ---
            var skillESlot = CreateSkillSlot(skillBar.transform, "SkillE", new Vector2(-40, 0), "E",
                new Color(0.8f, 0.4f, 0.2f, 0.8f));
            SetPrivateField(hud, "skillECooldownFill", skillESlot.cdFill);
            SetPrivateField(hud, "skillECooldownText", skillESlot.cdText);
            SetPrivateField(hud, "skillEIcon", skillESlot.iconImage);

            // --- W(R键) 技能槽 ---
            var skillWSlot = CreateSkillSlot(skillBar.transform, "SkillW", new Vector2(40, 0), "R",
                new Color(0.6f, 0.3f, 0.8f, 0.8f));
            SetPrivateField(hud, "skillWCooldownFill", skillWSlot.cdFill);
            SetPrivateField(hud, "skillWCooldownText", skillWSlot.cdText);
            SetPrivateField(hud, "skillWIcon", skillWSlot.iconImage);

            // --- 闪避槽 ---
            var dashSlot = CreateSkillSlot(skillBar.transform, "Dash", new Vector2(120, 0), "闪避",
                new Color(0.2f, 0.8f, 0.6f, 0.8f));
            SetPrivateField(hud, "dashCooldownFill", dashSlot.cdFill);
            SetPrivateField(hud, "dashCooldownText", dashSlot.cdText);

            // ========== 底部中央偏上：连招指示器 ==========
            var comboPanel = CreateUIImage(canvasGo.transform, "ComboPanel",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-40, 100), new Vector2(40, 120),
                new Color(0, 0, 0, 0)); // 透明容器

            var comboIndicators = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 22f; // -22, 0, 22
                var dot = CreateUIImage(comboPanel.transform, $"ComboDot_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x - 7, -7), new Vector2(x + 7, 7),
                    new Color(0.3f, 0.3f, 0.3f, 0.5f));
                comboIndicators[i] = dot.GetComponent<Image>();
            }
            SetPrivateField(hud, "comboIndicators", comboIndicators);

            // ========== 左下角：灵物计数 ==========
            var itemPanel = CreateUIImage(canvasGo.transform, "ItemPanel",
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 15), new Vector2(160, 50),
                new Color(0.1f, 0.12f, 0.18f, 0.7f));

            var itemIcon = CreateUIText(itemPanel.transform, "ItemIcon", "🔮", 18,
                new Vector2(0, 0), new Vector2(0.25f, 1),
                Vector2.zero, Vector2.zero);
            itemIcon.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            var itemCountText = CreateUIText(itemPanel.transform, "ItemCountText", "0", 16,
                new Vector2(0.25f, 0), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            itemCountText.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            itemCountText.GetComponent<Text>().color = new Color(0.7f, 0.9f, 1f);
            SetPrivateField(hud, "itemCountText", itemCountText.GetComponent<Text>());

            // ========== 中央偏下：消息提示 ==========
            var msgText = CreateUIText(canvasGo.transform, "MessageText", "", 20,
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
                new Vector2(-250, -15), new Vector2(250, 15));
            var msgTxt = msgText.GetComponent<Text>();
            msgTxt.alignment = TextAnchor.MiddleCenter;
            msgTxt.color = Color.white;
            msgTxt.supportRichText = true;
            var msgOutline = msgText.AddComponent<Outline>();
            msgOutline.effectColor = new Color(0, 0, 0, 0.7f);
            msgOutline.effectDistance = new Vector2(1, -1);
            SetPrivateField(hud, "messageText", msgTxt);

            // ========== 底部：操作提示 ==========
            var controlsHint = CreateUIText(canvasGo.transform, "ControlsHint",
                "WASD 移动  |  鼠标瞄准  |  左键挥刀  |  Q/E/R 技能  |  Space 闪避", 13,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-300, 2), new Vector2(300, 18));
            var hintTxt = controlsHint.GetComponent<Text>();
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.color = new Color(1, 1, 1, 0.35f);

            // ========== 死亡面板 ==========
            CreateDeathPanel(canvasGo.transform, hud);

            // ========== 通关面板 ==========
            CreateWinPanel(canvasGo.transform, hud);

            // ========== 伤害飘字 ==========
            var dmgPopup = canvasGo.AddComponent<DamagePopup>();
            SetPrivateField(dmgPopup, "canvas", canvas);

            // ========== 小地图 ==========
            CreateMinimap(canvasGo.transform);

            // ========== 背包UI ==========
            CreateInventoryUI(canvasGo.transform);

            // ========== 操作提示更新 ==========
            hintTxt.text = "WASD 移动 | 左键挥刀 | Q/E/R 技能 | Space 闪避 | Tab 背包 | F 交互";
        }

        /// <summary>创建小地图</summary>
        private void CreateMinimap(Transform canvasTransform)
        {
            // 小地图面板（右上角）
            var mapPanel = CreateUIImage(canvasTransform, "MinimapPanel",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-280, -50), new Vector2(-10, -10),
                new Color(0, 0, 0, 0.5f));

            var minimap = mapPanel.gameObject.AddComponent<Minimap>();
            SetPrivateField(minimap, "mapPanel", mapPanel.GetComponent<RectTransform>());

            // 玩家点
            var playerDot = CreateUIImage(mapPanel.transform, "PlayerDot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-5, 5), new Vector2(5, 15),
                new Color(0.2f, 1f, 0.4f));
            SetPrivateField(minimap, "playerDot", playerDot);

            // 标题
            var title = CreateUIText(mapPanel.transform, "MapTitle", "仙途", 12,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(2, -18), new Vector2(-2, -2));
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            title.GetComponent<Text>().color = new Color(0.8f, 0.7f, 0.5f);

            // 注册到GameManager
            if (GameManager.Instance != null)
                GameManager.Instance.SetMinimap(minimap);
        }

        /// <summary>创建背包UI</summary>
        private void CreateInventoryUI(Transform canvasTransform)
        {
            var invGo = new GameObject("InventoryUI");
            invGo.transform.SetParent(canvasTransform, false);
            var invRT = invGo.AddComponent<RectTransform>();
            invRT.anchorMin = Vector2.zero;
            invRT.anchorMax = Vector2.one;
            invRT.offsetMin = Vector2.zero;
            invRT.offsetMax = Vector2.zero;

            var invUI = invGo.AddComponent<InventoryUI>();

            // ===== 背包面板（全屏半透明遮罩） =====
            var panel = CreateUIImage(invGo.transform, "InvPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.03f, 0.08f, 0.85f));
            panel.SetActive(false); // 默认隐藏

            // ===== 标题 =====
            var title = CreateUIText(panel.transform, "InvTitle", "灵物背包 (0)", 28,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-200, -55), new Vector2(200, -15));
            var titleTxt = title.GetComponent<Text>();
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1f, 0.9f, 0.5f);
            titleTxt.fontStyle = FontStyle.Bold;
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // ===== 关闭提示 =====
            var closeHint = CreateUIText(panel.transform, "CloseHint", "按 Tab 关闭", 14,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-60, -75), new Vector2(60, -58));
            closeHint.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            closeHint.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f, 0.6f);

            // ===== 左侧：灵物列表区域 =====
            var itemSection = CreateUIImage(panel.transform, "ItemSection",
                new Vector2(0.05f, 0.05f), new Vector2(0.55f, 0.88f),
                Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.08f, 0.12f, 0.6f));

            var itemSectionTitle = CreateUIText(itemSection.transform, "ItemSectionTitle", "持有灵物", 18,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10, -30), new Vector2(-10, -5));
            itemSectionTitle.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            itemSectionTitle.GetComponent<Text>().color = new Color(0.7f, 0.9f, 1f);

            // 灵物列表内容区（带 VerticalLayoutGroup）
            var itemListGo = new GameObject("ItemListContent");
            itemListGo.transform.SetParent(itemSection.transform, false);
            var itemListRT = itemListGo.AddComponent<RectTransform>();
            itemListRT.anchorMin = new Vector2(0, 0);
            itemListRT.anchorMax = new Vector2(1, 1);
            itemListRT.offsetMin = new Vector2(5, 5);
            itemListRT.offsetMax = new Vector2(-5, -35);
            var itemListLayout = itemListGo.AddComponent<VerticalLayoutGroup>();
            itemListLayout.spacing = 4;
            itemListLayout.childAlignment = TextAnchor.UpperLeft;
            itemListLayout.childForceExpandWidth = true;
            itemListLayout.childForceExpandHeight = false;
            itemListLayout.padding = new RectOffset(4, 4, 4, 4);
            var itemListCSF = itemListGo.AddComponent<ContentSizeFitter>();
            itemListCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ===== 右侧：Synergy 组合区域 =====
            var synergySection = CreateUIImage(panel.transform, "SynergySection",
                new Vector2(0.58f, 0.05f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.08f, 0.12f, 0.6f));

            var synergySectionTitle = CreateUIText(synergySection.transform, "SynergySectionTitle", "灵力组合", 18,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10, -30), new Vector2(-10, -5));
            synergySectionTitle.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            synergySectionTitle.GetComponent<Text>().color = new Color(1f, 0.85f, 0.5f);

            // Synergy 列表内容区
            var synergyListGo = new GameObject("SynergyListContent");
            synergyListGo.transform.SetParent(synergySection.transform, false);
            var synergyListRT = synergyListGo.AddComponent<RectTransform>();
            synergyListRT.anchorMin = new Vector2(0, 0);
            synergyListRT.anchorMax = new Vector2(1, 1);
            synergyListRT.offsetMin = new Vector2(5, 5);
            synergyListRT.offsetMax = new Vector2(-5, -35);
            var synergyListLayout = synergyListGo.AddComponent<VerticalLayoutGroup>();
            synergyListLayout.spacing = 4;
            synergyListLayout.childAlignment = TextAnchor.UpperLeft;
            synergyListLayout.childForceExpandWidth = true;
            synergyListLayout.childForceExpandHeight = false;
            synergyListLayout.padding = new RectOffset(4, 4, 4, 4);
            var synergyListCSF = synergyListGo.AddComponent<ContentSizeFitter>();
            synergyListCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ===== 底部：属性总览 =====
            var statsBar = CreateUIImage(panel.transform, "StatsBar",
                new Vector2(0.05f, 0), new Vector2(0.95f, 0.04f),
                new Vector2(0, 5), new Vector2(0, 5),
                new Color(0, 0, 0, 0));
            var statsText = CreateUIText(statsBar.transform, "StatsText",
                "攻击力 | 生命 | 移速 | 暴击率 | 减伤", 12,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            statsText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            statsText.GetComponent<Text>().color = new Color(0.5f, 0.5f, 0.6f, 0.7f);

            // ===== 绑定字段到 InventoryUI =====
            SetPrivateField(invUI, "panel", panel);
            SetPrivateField(invUI, "itemListContent", itemListGo.transform);
            SetPrivateField(invUI, "synergyListContent", synergyListGo.transform);
            SetPrivateField(invUI, "titleText", titleTxt);
        }

        /// <summary>创建技能槽位</summary>
        private (Image cdFill, Text cdText, Image iconImage) CreateSkillSlot(
            Transform parent, string name, Vector2 offset, string label, Color bgColor)
        {
            float size = 60f;
            float halfSize = size / 2f;

            // 槽位背景
            var slot = CreateUIImage(parent, $"{name}Slot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(offset.x - halfSize, offset.y - halfSize),
                new Vector2(offset.x + halfSize, offset.y + halfSize),
                bgColor);

            // 图标区域（用颜色块代替图标）
            var icon = CreateUIImage(slot.transform, $"{name}Icon",
                Vector2.zero, Vector2.one,
                new Vector2(4, 4), new Vector2(-4, -4),
                new Color(1, 1, 1, 0.15f));

            // CD 遮罩（Filled Image）
            var cdFill = CreateUIImage(slot.transform, $"{name}CDFill",
                Vector2.zero, Vector2.one,
                new Vector2(2, 2), new Vector2(-2, -2),
                new Color(0, 0, 0, 0.7f));
            var cdFillImg = cdFill.GetComponent<Image>();
            cdFillImg.type = Image.Type.Filled;
            cdFillImg.fillMethod = Image.FillMethod.Radial360;
            cdFillImg.fillOrigin = (int)Image.Origin360.Top;
            cdFillImg.fillClockwise = false;
            cdFillImg.fillAmount = 0;

            // CD 文字
            var cdText = CreateUIText(slot.transform, $"{name}CDText", label, 16,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cdTxt = cdText.GetComponent<Text>();
            cdTxt.alignment = TextAnchor.MiddleCenter;
            cdTxt.fontStyle = FontStyle.Bold;
            var cdOutline = cdText.AddComponent<Outline>();
            cdOutline.effectColor = new Color(0, 0, 0, 0.8f);
            cdOutline.effectDistance = new Vector2(1, -1);

            // 快捷键标签
            var keyLabel = CreateUIText(slot.transform, $"{name}Key", label.Length <= 1 ? label : "",
                11, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(2, -14), new Vector2(18, -2));
            keyLabel.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            keyLabel.GetComponent<Text>().color = new Color(1, 1, 1, 0.6f);

            // 边框
            var border = CreateUIImage(slot.transform, $"{name}Border",
                Vector2.zero, Vector2.one,
                new Vector2(-1, -1), new Vector2(1, 1),
                new Color(0.6f, 0.6f, 0.7f, 0.4f));
            border.GetComponent<Image>().raycastTarget = false;

            return (cdFillImg, cdTxt, icon.GetComponent<Image>());
        }

        /// <summary>创建死亡面板</summary>
        private void CreateDeathPanel(Transform parent, GameHUD hud)
        {
            // 全屏半透明遮罩
            var panel = CreateUIImage(parent, "DeathPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0, 0, 0.75f));

            // 标题
            var title = CreateUIText(panel.transform, "DeathTitle", "梦境破碎", 48,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(-200, -30), new Vector2(200, 30));
            var titleTxt = title.GetComponent<Text>();
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.9f, 0.2f, 0.2f);
            titleTxt.fontStyle = FontStyle.Bold;
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(2, -2);

            // 副标题
            var sub = CreateUIText(panel.transform, "DeathSubText", "", 20,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<Text>();
            subTxt.alignment = TextAnchor.MiddleCenter;
            subTxt.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);

            // 重新开始按钮
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

            var btnText = CreateUIText(btnGo.transform, "BtnText", "重新入梦", 18,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            btnText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            btnText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            panel.SetActive(false);

            SetPrivateField(hud, "deathPanel", panel);
            SetPrivateField(hud, "deathTitleText", titleTxt);
            SetPrivateField(hud, "deathSubText", subTxt);
            SetPrivateField(hud, "restartButton", btn);
        }

        /// <summary>创建通关面板</summary>
        private void CreateWinPanel(Transform parent, GameHUD hud)
        {
            // 全屏半透明遮罩
            var panel = CreateUIImage(parent, "WinPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0.03f, 0, 0.75f));

            // 标题
            var title = CreateUIText(panel.transform, "WinTitle", "✨ 渡劫成功 ✨", 48,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(-250, -30), new Vector2(250, 30));
            var titleTxt = title.GetComponent<Text>();
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1f, 0.85f, 0.2f);
            titleTxt.fontStyle = FontStyle.Bold;
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(2, -2);

            // 副标题
            var sub = CreateUIText(panel.transform, "WinSubText", "飞升成仙，梦境圆满", 22,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<Text>();
            subTxt.alignment = TextAnchor.MiddleCenter;
            subTxt.color = new Color(1f, 0.95f, 0.8f, 0.9f);

            // 重新开始按钮
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

            var btnText = CreateUIText(btnGo.transform, "BtnText", "再入梦境", 18,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            btnText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            btnText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            btnText.GetComponent<Text>().color = new Color(0.1f, 0.08f, 0);

            panel.SetActive(false);

            SetPrivateField(hud, "winPanel", panel);
            SetPrivateField(hud, "winTitleText", titleTxt);
            SetPrivateField(hud, "winSubText", subTxt);
            SetPrivateField(hud, "winRestartButton", btn);
        }

        private void SetupLighting()
        {
            var existingLight = FindFirstObjectByType<Light>();
            if (existingLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        private void CreateHitStop()
        {
            var go = new GameObject("HitStop");
            go.AddComponent<HitStop>();
        }

        private void CreateLevelTransition()
        {
            var go = new GameObject("LevelTransition");
            go.AddComponent<LevelTransition>();
        }

        private void CreatePostProcess()
        {
            var go = new GameObject("PostProcess");
            go.AddComponent<PostProcessSetup>();
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
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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
