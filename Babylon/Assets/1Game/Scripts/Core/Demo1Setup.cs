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
        [SerializeField] private SkillData testSkill;

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
        }

        private void CreateObjectPool()
        {
            var poolGo = new GameObject("ObjectPool");
            poolGo.AddComponent<ObjectPool>();
        }

        private void CreateGround()
        {
            // 主地面
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5, 1, 5);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.15f, 0.18f, 0.22f);
                renderer.material = mat;
            }

            // 边界墙壁
            CreateWall(new Vector3(0, 1, 25), new Vector3(50, 2, 1));
            CreateWall(new Vector3(0, 1, -25), new Vector3(50, 2, 1));
            CreateWall(new Vector3(25, 1, 0), new Vector3(1, 2, 50));
            CreateWall(new Vector3(-25, 1, 0), new Vector3(1, 2, 50));
        }

        private void CreateWall(Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.25f, 0.35f);
                renderer.material = mat;
            }
        }

        private void CreatePlayer()
        {
            // 创建玩家根 GameObject
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 0, 0);

            // CharacterController
            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.7f;
            cc.center = new Vector3(0, 0.85f, 0);

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

            // ========== 攻击原点 & 刀光生成点 ==========
            var attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(playerGo.transform);
            attackOrigin.transform.localPosition = new Vector3(0, 1f, 0.8f);

            var slashSpawnPoint = new GameObject("SlashVFXPoint");
            slashSpawnPoint.transform.SetParent(playerGo.transform);
            slashSpawnPoint.transform.localPosition = new Vector3(0, 1.2f, 1f);

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

            // 设置测试技能
            if (testSkill != null)
            {
                combat.EquipSkillQ(testSkill);
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
            // 创建 Canvas
            var canvasGo = new GameObject("GameCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var hud = canvasGo.AddComponent<GameHUD>();

            // === 血条 ===
            var hpBarBg = CreateUIImage(canvasGo.transform, "HpBarBg",
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(320, -50),
                new Color(0.2f, 0.2f, 0.2f, 0.8f));

            var hpSliderGo = new GameObject("HpSlider");
            hpSliderGo.transform.SetParent(hpBarBg.transform, false);
            var rt = hpSliderGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2, 2);
            rt.offsetMax = new Vector2(-2, -2);

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

            var fill = CreateUIImage(fillArea.transform, "Fill",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.8f, 0.3f));
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.value = 1f;

            var hpText = CreateUIText(hpBarBg.transform, "HpText", "100 / 100", 14,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            SetPrivateField(hud, "hpSlider", slider);
            SetPrivateField(hud, "hpText", hpText.GetComponent<Text>());

            // === 境界信息 ===
            var realmText = CreateUIText(canvasGo.transform, "RealmText", "练气期", 24,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-60, -20), new Vector2(60, -60));
            realmText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            realmText.GetComponent<Text>().color = new Color(1f, 0.85f, 0.3f);
            SetPrivateField(hud, "realmText", realmText.GetComponent<Text>());

            // === 消息提示 ===
            var msgText = CreateUIText(canvasGo.transform, "MessageText", "", 18,
                new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f),
                new Vector2(-200, -15), new Vector2(200, 15));
            msgText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            msgText.GetComponent<Text>().color = Color.white;
            SetPrivateField(hud, "messageText", msgText.GetComponent<Text>());

            // === 灵物计数 ===
            var itemText = CreateUIText(canvasGo.transform, "ItemCountText", "灵物：0 种", 14,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-150, -20), new Vector2(-10, -50));
            itemText.GetComponent<Text>().alignment = TextAnchor.MiddleRight;
            SetPrivateField(hud, "itemCountText", itemText.GetComponent<Text>());

            // === 操作提示 ===
            CreateUIText(canvasGo.transform, "ControlsHint",
                "WASD 移动 | 鼠标瞄准 | 左键挥刀 | Q 技能 | Space 闪避", 12,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-250, 10), new Vector2(250, 30));
            var hint = canvasGo.transform.Find("ControlsHint");
            if (hint != null)
            {
                var t = hint.GetComponent<Text>();
                t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(1, 1, 1, 0.5f);
            }
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
