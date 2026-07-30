using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// Demo1 场景快速搭建器
    /// 挂载到空 GameObject 上，运行时自动创建所有必要对象
    /// 支持使用 Frank_Katana 真实模型 + 刀光/打击特效
    /// </summary>
    public class Demo1Setup : MonoBehaviour
    {
        [Header("技能池（可选，自动配置会填充）")]
        [SerializeField] private SkillData[] skillPool;

        [Header("技能（可选）")]
        [SerializeField] private SkillData testSkillQ;
        [SerializeField] private SkillData testSkillE;
        [SerializeField] private SkillData testSkillR;

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

            // 11. 创建 EventSystem（UI交互必需）
            CreateEventSystem();

            // 12. 创建音效管理器
            CreateAudioManager();
        }

        private void CreateEventSystem()
        {
            // 检查场景中是否已有 EventSystem
            if (UnityEngine.EventSystems.EventSystem.current != null) return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // 使用 InputSystem 的 UI 输入模块
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private void CreateAudioManager()
        {
            // 检查场景中是否已有 AudioManager
            if (AudioManager.Instance != null) return;

            var audioGo = new GameObject("AudioManager");
            audioGo.AddComponent<AudioManager>();
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
                var mat = new Material(MaterialHelper.GetLitShader());
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
            // 优先走主角档案系统（PlayerCharacterProfile）：若已选档案且带模型，
            // 则跳过这里的序列化模型构建，改由 PlayerController.ApplyCharacterProfile 在组件就绪后热构建。
            var selectedProfile = PlayerCharacterRegistry.Selected;
            bool useProfile = selectedProfile != null && selectedProfile.modelPrefab != null;

            Transform modelTransform = null;
            Animator modelAnimator = null;

            if (useProfile)
            {
                // 模型延迟到组件挂载后由 ApplyCharacterProfile 构建
            }
            else if (playerModelPrefab != null)
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
                    var mat = new Material(MaterialHelper.GetLitShader());
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
                    var mat = new Material(MaterialHelper.GetLitShader());
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

            // V0.4：玩家初始无技能，Q/E/R 全部留空。
            // 仅保留 Inspector 手动配置的测试技能（开发调试用）。
            if (testSkillQ != null) combat.EquipSkillQ(testSkillQ);
            if (testSkillE != null) combat.EquipSkillE(testSkillE);
            if (testSkillR != null) combat.EquipSkillR(testSkillR);
            playerGo.AddComponent<PlayerResources>();

            // 主角档案系统：组件就绪后热构建所选主角模型（剑客/法师）。
            // 默认档案（sortOrder 最小）一般为剑客，与旧的 Frank_Katana 一致。
            if (useProfile)
                playerCtrl.ApplyCharacterProfile(selectedProfile);
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

            // 如果 skillPool 为空或全部为null，尝试自动加载
            bool skillPoolEmpty = skillPool == null || skillPool.Length == 0;
            if (!skillPoolEmpty)
            {
                bool allNull = true;
                foreach (var sk in skillPool)
                    if (sk != null) { allNull = false; break; }
                if (allNull)
                {
                    Debug.LogWarning($"[Demo1Setup] skillPool 有 {skillPool.Length} 个槽位但全部为 null，重新自动加载...");
                    skillPoolEmpty = true;
                }
            }
            if (skillPoolEmpty)
            {
#if UNITY_EDITOR
                var skillGuids = UnityEditor.AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/1Game/Data/Skills" });
                if (skillGuids.Length > 0)
                {
                    var skills = new System.Collections.Generic.List<SkillData>();
                    foreach (var guid in skillGuids)
                    {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(path);
                        if (skill != null) skills.Add(skill);
                    }
                    skillPool = skills.ToArray();
                    Debug.Log($"<color=green>[Demo1Setup] 自动加载了 {skillPool.Length} 个技能数据</color>");
                }
#endif
            }

            // 设置技能池
            {
                var skillList = new System.Collections.Generic.List<SkillData>();

                // 优先使用 Inspector 配置的 skillPool
                if (skillPool != null && skillPool.Length > 0)
                {
                    foreach (var sk in skillPool)
                    {
                        if (sk != null && !skillList.Contains(sk))
                            skillList.Add(sk);
                    }
                }

                // 补充从 testSkill 字段收集
                if (testSkillQ != null && !skillList.Contains(testSkillQ)) skillList.Add(testSkillQ);
                if (testSkillE != null && !skillList.Contains(testSkillE)) skillList.Add(testSkillE);
                if (testSkillR != null && !skillList.Contains(testSkillR)) skillList.Add(testSkillR);

                var skillPoolField = typeof(GameManager).GetField("skillPool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (skillPoolField != null && skillList.Count > 0)
                {
                    skillPoolField.SetValue(gm, skillList.ToArray());
                    Debug.Log($"<color=cyan>[Demo1Setup] 技能池：{skillList.Count} 个技能</color>");
                }
                else if (skillList.Count == 0)
                {
                    Debug.Log("<color=yellow>[Demo1Setup] 未找到技能数据，技能池为空</color>");
                }
            }

            // 设置打击特效给 GameManager，让它传递给生成的敌人
            if (hitVFXPrefab != null)
            {
                var hitField = typeof(GameManager).GetField("enemyHitVFXPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                hitField?.SetValue(gm, hitVFXPrefab);
            }

            // 模块池注入（GDD V.07 模块化技能）
            {
                ModuleDef[] mods = null;
#if UNITY_EDITOR
                var modGuids = UnityEditor.AssetDatabase.FindAssets("t:ModuleDef", new[] { "Assets/1Game/Data/Modules" });
                if (modGuids.Length > 0)
                {
                    var list = new System.Collections.Generic.List<ModuleDef>();
                    foreach (var guid in modGuids)
                    {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleDef>(path);
                        if (m != null) list.Add(m);
                    }
                    mods = list.ToArray();
                }
#endif
                if (mods == null || mods.Length == 0)
                    mods = Resources.LoadAll<ModuleDef>("Modules");

                if (mods != null && mods.Length > 0)
                {
                    var modField = typeof(GameManager).GetField("modulePool",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    modField?.SetValue(gm, mods);
                    Debug.Log($"<color=#00ffcc>[Demo1Setup] 模块池：{mods.Length} 个模块定义</color>");
                }
            }

            // 创建 Debug 控制台（F1 打开）
            gmGo.AddComponent<DebugConsole>();
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
            hpText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            // 血条文字描边
            var hpTextOutline = hpText.AddComponent<Outline>();
            hpTextOutline.effectColor = new Color(0, 0, 0, 0.8f);
            hpTextOutline.effectDistance = new Vector2(1, -1);

            SetPrivateField(hud, "hpSlider", slider);
            SetPrivateField(hud, "hpFillImage", hpFill.GetComponent<Image>());
            SetPrivateField(hud, "hpDamageFill", hpDamageFill.GetComponent<Image>());
            SetPrivateField(hud, "hpText", hpText.GetComponent<TextMeshProUGUI>());

            // ========== 顶部中央：境界信息 ==========
            var realmPanel = CreateUIImage(canvasGo.transform, "RealmPanel",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-100, -55), new Vector2(100, -10),
                new Color(0, 0, 0, 0)); // 透明容器

            var realmText = CreateUIText(realmPanel.transform, "RealmText", "练气期", 26,
                new Vector2(0, 0.5f), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            var realmTxt = realmText.GetComponent<TextMeshProUGUI>();
            realmTxt.alignment = TextAlignmentOptions.Center;
            realmTxt.color = new Color(1f, 0.85f, 0.3f);
            realmTxt.fontStyle = FontStyles.Bold;
            var realmOutline = realmText.AddComponent<Outline>();
            realmOutline.effectColor = new Color(0, 0, 0, 0.6f);
            realmOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var levelText = CreateUIText(realmPanel.transform, "LevelText", "第 1 层", 16,
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

            // 骷髅图标（用文字代替）
            var enemyIcon = CreateUIText(enemyPanel.transform, "EnemyIcon", "☠", 22,
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
                new Vector2(-480, 5), new Vector2(480, 230),
                new Color(0, 0, 0, 0)); // 透明容器

            // SkillBarUI 组件
            var skillBarUI = skillBarContainer.AddComponent<SkillBarUI>();

            float skillSize = 68f;
            float skillY = 110f;

            // 技能槽位颜色（Q/E/R初始暗色，由SkillBarUI.RefreshSkillSlots根据实际状态设置）
            Color[] skillColors = {
                new Color(0.08f, 0.08f, 0.12f, 0.35f),    // Q - 初始暗色（有技能后由RefreshSkillSlots设置品阶色）
                new Color(0.08f, 0.08f, 0.12f, 0.35f),    // E - 初始暗色
                new Color(0.08f, 0.08f, 0.12f, 0.35f),    // R - 初始暗色
                new Color(0.2f, 0.8f, 0.6f, 0.85f),   // 闪避 - 青
                new Color(0.7f, 0.7f, 0.7f, 0.7f)     // 普攻 - 灰
            };
            string[] skillLabels = { "Q", "E", "R", "闪避", "攻击" };
            float[] skillXPositions = { -290f, -145f, 0f, 145f, 290f };

            var skillSlotRTs = new RectTransform[5];

            for (int s = 0; s < 5; s++)
            {
                float sx = skillXPositions[s];
                float halfSkill = skillSize / 2f;

                // --- 技能圆形图标 ---
                var skillSlot = CreateUIImage(skillBarContainer.transform, $"Skill_{s}",
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(sx - halfSkill, skillY - halfSkill),
                    new Vector2(sx + halfSkill, skillY + halfSkill),
                    skillColors[s]);
                skillSlotRTs[s] = skillSlot.GetComponent<RectTransform>();

                // 圆形边框（银色）
                var skillBorder = CreateUIImage(skillSlot.transform, $"SkillBorder_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(-2, -2), new Vector2(2, 2),
                    new Color(0.6f, 0.65f, 0.7f, 0.5f));
                skillBorder.GetComponent<Image>().raycastTarget = false;

                // 图标区域
                var skillIcon = CreateUIImage(skillSlot.transform, $"SkillIcon_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(4, 4), new Vector2(-4, -4),
                    new Color(1, 1, 1, 0.15f));

                // CD遮罩
                var cdFill = CreateUIImage(skillSlot.transform, $"SkillCD_{s}",
                    Vector2.zero, Vector2.one,
                    new Vector2(2, 2), new Vector2(-2, -2),
                    new Color(0, 0, 0, 0.7f));
                var cdFillImg = cdFill.GetComponent<Image>();
                cdFillImg.type = Image.Type.Filled;
                cdFillImg.fillMethod = Image.FillMethod.Radial360;
                cdFillImg.fillOrigin = (int)Image.Origin360.Top;
                cdFillImg.fillClockwise = false;
                cdFillImg.fillAmount = 0;

                // CD文字 / 快捷键标签
                var cdText = CreateUIText(skillSlot.transform, $"SkillCDText_{s}",
                    skillLabels[s], 18,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cdTxt = cdText.GetComponent<TextMeshProUGUI>();
                cdTxt.alignment = TextAlignmentOptions.Center;
                cdTxt.fontStyle = FontStyles.Bold;
                var cdOutline = cdText.AddComponent<Outline>();
                cdOutline.effectColor = new Color(0, 0, 0, 0.8f);
                cdOutline.effectDistance = new Vector2(1, -1);

                // 绑定到HUD
                switch (s)
                {
                    case 0: // Q
                        SetPrivateField(hud, "skillQCooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillQCooldownText", cdTxt);
                        SetPrivateField(hud, "skillQIcon", skillIcon.GetComponent<Image>());
                        break;
                    case 1: // E
                        SetPrivateField(hud, "skillECooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillECooldownText", cdTxt);
                        SetPrivateField(hud, "skillEIcon", skillIcon.GetComponent<Image>());
                        break;
                    case 2: // R
                        SetPrivateField(hud, "skillRCooldownFill", cdFillImg);
                        SetPrivateField(hud, "skillRCooldownText", cdTxt);
                        SetPrivateField(hud, "skillRIcon", skillIcon.GetComponent<Image>());
                        break;
                    case 3: // 闪避
                        SetPrivateField(hud, "dashCooldownFill", cdFillImg);
                        SetPrivateField(hud, "dashCooldownText", cdTxt);
                        break;
                }

                // 模块链状态标签（Q/E/R 下方，显示当前链名）
                if (s < 3)
                {
                    var chainLabel = CreateUIText(skillBarContainer.transform, $"ChainLabel_{s}",
                        "", 11,
                        new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                        new Vector2(sx - 60, skillY - halfSkill - 18),
                        new Vector2(sx + 60, skillY - halfSkill - 2));
                    var chainTxt = chainLabel.GetComponent<TextMeshProUGUI>();
                    chainTxt.alignment = TextAlignmentOptions.Center;
                    chainTxt.color = new Color(0.4f, 0.9f, 0.8f, 0.7f);
                    chainTxt.fontSize = 11;
                    chainTxt.enableWordWrapping = false;
                    var chainOutline = chainLabel.AddComponent<Outline>();
                    chainOutline.effectColor = new Color(0, 0, 0, 0.8f);
                    chainOutline.effectDistance = new Vector2(1, -1);
                }
            }

            SetPrivateField(skillBarUI, "skillSlotRTs", skillSlotRTs);

            // ========== 连招指示器（技能栏上方） ==========
            var comboPanel = CreateUIImage(canvasGo.transform, "ComboPanel",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-40, 178), new Vector2(40, 198),
                new Color(0, 0, 0, 0));

            var comboIndicators = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 22f;
                var dot = CreateUIImage(comboPanel.transform, $"ComboDot_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x - 7, -7), new Vector2(x + 7, 7),
                    new Color(0.3f, 0.3f, 0.3f, 0.5f));
                comboIndicators[i] = dot.GetComponent<Image>();
            }
            SetPrivateField(hud, "comboIndicators", comboIndicators);

            // ========== 模块链装配 UI + Proc 指示 ==========
            canvasGo.AddComponent<ModuleAssemblyUI>();
            var procOverlay = canvasGo.AddComponent<ModuleChainProcOverlay>();
            procOverlay.SetSkillSlots(skillSlotRTs);
            // GDD 5.13：角色旁三竖条 Proc 指示器
            canvasGo.AddComponent<ProcBarsHUD>();

            // ========== 左下角：碎片计数 ==========
            var shardPanel = CreateUIImage(canvasGo.transform, "ShardPanel",
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 55), new Vector2(160, 90),
                new Color(0.1f, 0.1f, 0.18f, 0.7f));

            var shardIcon = CreateUIText(shardPanel.transform, "ShardIcon", "✦", 18,
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
            "WASD 移动  |  左键挥刀  |  Q/E/R 技能  |  Space 闪避  |  F 拾取  |  M 模块装配  |  C 角色信息  |  ESC 暂停", 12,
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

            // 操作提示已在上方设置，不再重复
        }

        /// <summary>创建小地图</summary>
        private void CreateMinimap(Transform canvasTransform)
        {
            // 小地图面板（右上角，与敌人计数对齐）
            var mapPanel = CreateUIImage(canvasTransform, "MinimapPanel",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-220, -55), new Vector2(-20, -15),
                new Color(0, 0, 0, 0.5f));

            var minimap = mapPanel.gameObject.AddComponent<Minimap>();
            SetPrivateField(minimap, "mapPanel", mapPanel.GetComponent<RectTransform>());

            // 玩家点
            var playerDot = CreateUIImage(mapPanel.transform, "PlayerDot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-5, 5), new Vector2(5, 15),
                new Color(0.2f, 1f, 0.4f));
            SetPrivateField(minimap, "playerDot", playerDot.GetComponent<Image>());

            // 标题
            var title = CreateUIText(mapPanel.transform, "MapTitle", "地图", 12,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(2, -18), new Vector2(-2, -2));
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            title.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.7f, 0.5f);

            // 图例（小地图下方）
            var legendText = CreateUIText(canvasTransform, "MinimapLegend",
                "⚔战斗  ⚡精英  ?事件  $商店  ♥休息  ☠Boss", 10,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-220, -72), new Vector2(-20, -57));
            legendText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            legendText.GetComponent<TextMeshProUGUI>().color = new Color(0.55f, 0.6f, 0.7f, 0.8f);

            // 注册到GameManager
            if (GameManager.Instance != null)
                GameManager.Instance.SetMinimap(minimap);
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

            // 副标题
            var sub = CreateUIText(panel.transform, "DeathSubText", "", 20,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<TextMeshProUGUI>();
            subTxt.alignment = TextAlignmentOptions.Center;
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
            // 全屏半透明遮罩
            var panel = CreateUIImage(parent, "WinPanel",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.05f, 0.03f, 0, 0.75f));

            // 标题
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

            // 副标题
            var sub = CreateUIText(panel.transform, "WinSubText", "秘境征服，冒险圆满", 22,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200, -15), new Vector2(200, 15));
            var subTxt = sub.GetComponent<TextMeshProUGUI>();
            subTxt.alignment = TextAlignmentOptions.Center;
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
