using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace XianTu.Editor
{
    /// <summary>
    /// Demo1 数据资产批量创建工具
    /// 在 Unity 菜单栏：仙途梦境 → 各项配置操作
    /// </summary>
    public static class Demo1DataCreator
    {
        private const string ITEM_PATH = "Assets/1Game/Data/Items/";
        private const string SKILL_PATH = "Assets/1Game/Data/Skills/";
        private const string CONTROLLER_PATH = "Assets/1Game/Data/";
        private const string SCENE_PATH = "Assets/1Game/Scenes/";

        // Frank_Katana 资源路径
        private const string FRANK_ANIM_PATH = "Assets/1Game/ArtRes/Package/Frank_Katana/Assets/Animations/";
        private const string FRANK_ANIM_FBX_PATH = "Assets/1Game/ArtRes/Package/Frank_Katana/Assets/Animations/FBX/";
        private const string FRANK_MESH_PATH = "Assets/1Game/ArtRes/Package/Frank_Katana/Assets/Meshes/";

        // 怪物资源包路径
        private const string MONSTER_PACK_PATH = "Assets/1Game/ArtRes/Package/Monsters Ultimate Pack 05 Cute Series/";

        // ==================== 菜单项 ====================

        [MenuItem("仙途梦境/① 配置 Tags 和 Layers", false, 1)]
        public static void ConfigureTagsAndLayers()
        {
            AddTag("Enemy");
            AddLayer("Enemy");
            Debug.Log("<color=green>✅ Tags 和 Layers 配置完成！</color>");
        }

        [MenuItem("仙途梦境/② 创建 Demo1 测试数据", false, 2)]
        public static void CreateAllDemo1Data()
        {
            EnsureDirectory(ITEM_PATH);
            EnsureDirectory(SKILL_PATH);

            CreateFireOrb();
            CreateWindOrb();
            CreateJadePendant();
            CreateRustySword();
            CreateHealPill();
            // 灵品灵物
            CreateThunderBead();
            CreateShadowCloak();
            // 玄品灵物
            CreateDragonScale();
            CreatePhoenixFeather();
            // 地品灵物
            CreateVoidStone();
            // 天品灵物
            CreateCelestialOrb();
            // 新增灵物
            CreateIceJade();
            CreateBloodCoral();
            CreateMirrorShard();
            CreateSpiritVine();
            CreateStarDust();
            CreateSoulLantern();
            CreateChaosCore();
            CreateFallingRock();
            CreateGoldenBell();
            CreateThunderStrike();
            CreateSwordProjectile();
            CreateEarthDash();
            CreateFrostBlast();
            CreateHealSpring();
            // 新增功法
            CreatePuppetSummon();
            CreateFlameWave();
            CreateMirrorShield();
            CreateThunderChain();
            CreateShadowStep();
            CreateAudioConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>✅ Demo1 测试数据创建完成！共 18 个灵物 + 12 个功法 + 1 个音效配置</color>");
        }

        [MenuItem("仙途梦境/③ 创建 Animator Controller", false, 3)]
        public static void CreateAnimatorController()
        {
            EnsureDirectory(CONTROLLER_PATH);

            string controllerPath = CONTROLLER_PATH + "PlayerAnimatorController.controller";

            // 如果已存在，先删除
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // 添加参数
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Evade", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Skill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

            // 获取基础层
            var rootStateMachine = controller.layers[0].stateMachine;

            // ========== 加载 Frank_Katana 动画剪辑 ==========
            var idleClip = LoadAnimClip("Frank_RPG_Katana_Stance1_Idle");
            var runClip = LoadAnimClip("Frank_RPG_Katana_Run01");
            var attack1Clip = LoadAnimClip("Frank_RPG_Katana_S1_Attack01");
            var attack2Clip = LoadAnimClip("Frank_RPG_Katana_S1_Attack02");
            var attack3Clip = LoadAnimClip("Frank_RPG_Katana_S1_Attack03");
            var evadeClip = LoadAnimClip("Frank_RPG_Katana_Evade_F");
            var hitClip = LoadAnimClip("Frank_RPG_Katana_Hit01");
            var dieClip = LoadAnimClip("Frank_RPG_Katana_Die01");
            var skillClip = LoadAnimClip("Frank_RPG_Katana_S1_Skill01");

            // ========== 创建状态 ==========

            // Idle
            var idleState = rootStateMachine.AddState("Idle", new Vector3(0, 0, 0));
            if (idleClip != null) idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            // Run
            var runState = rootStateMachine.AddState("Run", new Vector3(0, 120, 0));
            if (runClip != null) runState.motion = runClip;

            // Attack1（加速播放，让攻击更干脆利落）
            var attack1State = rootStateMachine.AddState("Attack1", new Vector3(300, 0, 0));
            if (attack1Clip != null) attack1State.motion = attack1Clip;
            attack1State.speed = 1.3f;

            // Attack2
            var attack2State = rootStateMachine.AddState("Attack2", new Vector3(300, 80, 0));
            if (attack2Clip != null) attack2State.motion = attack2Clip;
            attack2State.speed = 1.3f;

            // Attack3
            var attack3State = rootStateMachine.AddState("Attack3", new Vector3(300, 160, 0));
            if (attack3Clip != null) attack3State.motion = attack3Clip;
            attack3State.speed = 1.3f;

            // Evade
            var evadeState = rootStateMachine.AddState("Evade", new Vector3(-300, 0, 0));
            if (evadeClip != null) evadeState.motion = evadeClip;

            // Hit
            var hitState = rootStateMachine.AddState("Hit", new Vector3(-300, 120, 0));
            if (hitClip != null) hitState.motion = hitClip;

            // Die
            var dieState = rootStateMachine.AddState("Die", new Vector3(-300, 240, 0));
            if (dieClip != null) dieState.motion = dieClip;

            // Skill
            var skillState = rootStateMachine.AddState("Skill", new Vector3(300, 240, 0));
            if (skillClip != null) skillState.motion = skillClip;

            // ========== 创建转换 ==========

            // Idle → Run（Speed > 0.1）
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.15f;

            // Run → Idle（Speed < 0.1）
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.15f;

            // Any → Attack1（Attack trigger + AttackIndex == 0）
            var anyToAttack1 = rootStateMachine.AddAnyStateTransition(attack1State);
            anyToAttack1.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack1.AddCondition(AnimatorConditionMode.Equals, 0, "AttackIndex");
            anyToAttack1.hasExitTime = false;
            anyToAttack1.duration = 0.1f;
            anyToAttack1.canTransitionToSelf = false;

            // Any → Attack2（Attack trigger + AttackIndex == 1）
            var anyToAttack2 = rootStateMachine.AddAnyStateTransition(attack2State);
            anyToAttack2.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack2.AddCondition(AnimatorConditionMode.Equals, 1, "AttackIndex");
            anyToAttack2.hasExitTime = false;
            anyToAttack2.duration = 0.05f;
            anyToAttack2.canTransitionToSelf = false;

            // Any → Attack3（Attack trigger + AttackIndex == 2）
            var anyToAttack3 = rootStateMachine.AddAnyStateTransition(attack3State);
            anyToAttack3.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack3.AddCondition(AnimatorConditionMode.Equals, 2, "AttackIndex");
            anyToAttack3.hasExitTime = false;
            anyToAttack3.duration = 0.05f;
            anyToAttack3.canTransitionToSelf = false;

            // Attack1 → Run（兜底过渡，主要靠代码 CrossFade 驱动）
            var attack1ToRun = attack1State.AddTransition(runState);
            attack1ToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack1ToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            attack1ToRun.hasExitTime = true;
            attack1ToRun.exitTime = 0.95f;
            attack1ToRun.duration = 0.05f;

            // Attack1 → Idle（兜底）
            var attack1ToIdle = attack1State.AddTransition(idleState);
            attack1ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack1ToIdle.hasExitTime = true;
            attack1ToIdle.exitTime = 0.95f;
            attack1ToIdle.duration = 0.05f;

            // Attack2 → Run
            var attack2ToRun = attack2State.AddTransition(runState);
            attack2ToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack2ToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            attack2ToRun.hasExitTime = true;
            attack2ToRun.exitTime = 0.95f;
            attack2ToRun.duration = 0.05f;

            // Attack2 → Idle
            var attack2ToIdle = attack2State.AddTransition(idleState);
            attack2ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack2ToIdle.hasExitTime = true;
            attack2ToIdle.exitTime = 0.95f;
            attack2ToIdle.duration = 0.05f;

            // Attack3 → Run
            var attack3ToRun = attack3State.AddTransition(runState);
            attack3ToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack3ToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            attack3ToRun.hasExitTime = true;
            attack3ToRun.exitTime = 0.95f;
            attack3ToRun.duration = 0.05f;

            // Attack3 → Idle
            var attack3ToIdle = attack3State.AddTransition(idleState);
            attack3ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attack3ToIdle.hasExitTime = true;
            attack3ToIdle.exitTime = 0.95f;
            attack3ToIdle.duration = 0.05f;

            // Any → Evade
            var anyToEvade = rootStateMachine.AddAnyStateTransition(evadeState);
            anyToEvade.AddCondition(AnimatorConditionMode.If, 0, "Evade");
            anyToEvade.hasExitTime = false;
            anyToEvade.duration = 0.1f;
            anyToEvade.canTransitionToSelf = false;

            // Evade → Run（闪避结束时如果在移动，直接切Run）
            var evadeToRun = evadeState.AddTransition(runState);
            evadeToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            evadeToRun.hasExitTime = true;
            evadeToRun.exitTime = 0.9f;
            evadeToRun.duration = 0.1f;

            // Evade → Idle
            var evadeToIdle = evadeState.AddTransition(idleState);
            evadeToIdle.hasExitTime = true;
            evadeToIdle.exitTime = 0.9f;
            evadeToIdle.duration = 0.15f;

            // Any → Hit
            var anyToHit = rootStateMachine.AddAnyStateTransition(hitState);
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.05f;
            anyToHit.canTransitionToSelf = false;

            // Hit → Run
            var hitToRun = hitState.AddTransition(runState);
            hitToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            hitToRun.hasExitTime = true;
            hitToRun.exitTime = 0.9f;
            hitToRun.duration = 0.1f;

            // Hit → Idle
            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.9f;
            hitToIdle.duration = 0.15f;

            // Any → Die
            var anyToDie = rootStateMachine.AddAnyStateTransition(dieState);
            anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;
            anyToDie.canTransitionToSelf = false;

            // Any → Skill
            var anyToSkill = rootStateMachine.AddAnyStateTransition(skillState);
            anyToSkill.AddCondition(AnimatorConditionMode.If, 0, "Skill");
            anyToSkill.hasExitTime = false;
            anyToSkill.duration = 0.1f;
            anyToSkill.canTransitionToSelf = false;

            // Skill → Run
            var skillToRun = skillState.AddTransition(runState);
            skillToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            skillToRun.hasExitTime = true;
            skillToRun.exitTime = 0.9f;
            skillToRun.duration = 0.1f;

            // Skill → Idle
            var skillToIdle = skillState.AddTransition(idleState);
            skillToIdle.hasExitTime = true;
            skillToIdle.exitTime = 0.9f;
            skillToIdle.duration = 0.15f;

            // ========== 添加动画事件 ==========
            AddAttackAnimationEvents(attack1Clip, 0);
            AddAttackAnimationEvents(attack2Clip, 1);
            AddAttackAnimationEvents(attack3Clip, 2);

            // 为闪避、受击、技能动画添加结束事件
            AddSimpleEndEvent(evadeClip, "OnEvadeEnd");
            AddSimpleEndEvent(hitClip, "OnHitEnd");
            AddSimpleEndEvent(skillClip, "OnSkillEnd");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>✅ Animator Controller 创建完成！</color>");
            Debug.Log($"<color=cyan>路径：{controllerPath}</color>");

            // 统计缺失的动画
            int missing = 0;
            if (idleClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Idle"); missing++; }
            if (runClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Run"); missing++; }
            if (attack1Clip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Attack1"); missing++; }
            if (attack2Clip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Attack2"); missing++; }
            if (attack3Clip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Attack3"); missing++; }
            if (evadeClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Evade"); missing++; }
            if (hitClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Hit"); missing++; }
            if (dieClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Die"); missing++; }
            if (skillClip == null) { Debug.LogWarning("  ⚠️ 缺失动画：Skill"); missing++; }

            if (missing == 0)
                Debug.Log("<color=green>  所有动画剪辑加载成功！</color>");
            else
                Debug.LogWarning($"  有 {missing} 个动画剪辑未找到，请检查 FBX 文件路径");

            Selection.activeObject = controller;

            // 自动修复场景中 Demo1Setup 的 Animator Controller 引用
            // （因为删除重建会导致旧引用丢失）
            var setup = Object.FindFirstObjectByType<Demo1Setup>();
            if (setup != null)
            {
                var so = new SerializedObject(setup);
                var animProp = so.FindProperty("animatorController");
                if (animProp != null)
                {
                    animProp.objectReferenceValue = controller;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(setup);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    Debug.Log("<color=green>  已自动修复场景中 Demo1Setup 的 Animator Controller 引用</color>");
                }
            }
        }

        [MenuItem("仙途梦境/④ 自动配置 Demo1 场景", false, 4)]
        public static void AutoConfigureDemo1Scene()
        {
            // 查找或创建 Demo1Setup
            var setup = Object.FindFirstObjectByType<Demo1Setup>();
            if (setup == null)
            {
                var go = new GameObject("Demo1Setup");
                setup = go.AddComponent<Demo1Setup>();
                Debug.Log("<color=green>已创建 Demo1Setup 对象</color>");
            }

            var so = new SerializedObject(setup);

            // 加载灵物数据
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/1Game/Data/Items" });
            var itemPoolProp = so.FindProperty("itemPool");
            if (itemPoolProp != null)
            {
                itemPoolProp.arraySize = itemGuids.Length;
                for (int i = 0; i < itemGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                    itemPoolProp.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                }
            }

            // 加载技能
            var skillQ = AssetDatabase.LoadAssetAtPath<SkillData>(SKILL_PATH + "落石术.asset");
            var skillQProp = so.FindProperty("testSkillQ");
            if (skillQProp != null && skillQ != null)
                skillQProp.objectReferenceValue = skillQ;

            // E/R槽位开局留空，让玩家自己探索获取
            var skillEProp = so.FindProperty("testSkillE");
            if (skillEProp != null)
                skillEProp.objectReferenceValue = null;

            var skillRProp = so.FindProperty("testSkillR");
            if (skillRProp != null)
                skillRProp.objectReferenceValue = null;

            var skillE = AssetDatabase.LoadAssetAtPath<SkillData>(SKILL_PATH + "金钟罩.asset");
            var skillR = AssetDatabase.LoadAssetAtPath<SkillData>(SKILL_PATH + "天雷引.asset");

            // 加载角色模型（Frank_Katana FBX）
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FRANK_MESH_PATH + "Frank_RPG_Katana_Unity_Y_top.FBX");
            var modelProp = so.FindProperty("playerModelPrefab");
            if (modelProp != null && modelPrefab != null)
                modelProp.objectReferenceValue = modelPrefab;

            // 加载 Animator Controller
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                CONTROLLER_PATH + "PlayerAnimatorController.controller");
            var animProp = so.FindProperty("animatorController");
            if (animProp != null && animController != null)
                animProp.objectReferenceValue = animController;

            // 加载刀光特效
            var slashVFX = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/1Game/ArtRes/Package/daoguang/Effect/Video_Prefab/刀光.prefab");
            var slashProp = so.FindProperty("slashVFXPrefab");
            if (slashProp != null && slashVFX != null)
                slashProp.objectReferenceValue = slashVFX;

            // 加载打击特效（选用 hit-red-1）
            var hitVFX = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/1Game/ArtRes/Package/Hit & Slashes Vol.3/Prefabs/hit-red-1.prefab");
            var hitProp = so.FindProperty("hitVFXPrefab");
            if (hitProp != null && hitVFX != null)
                hitProp.objectReferenceValue = hitVFX;

            so.ApplyModifiedProperties();

            // 标记场景为已修改
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("<color=green>✅ Demo1 场景配置完成！</color>");
            Debug.Log($"  灵物：{itemGuids.Length} 个");

            // 加载功法数据到skillPool
            string[] skillGuids = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/1Game/Data/Skills" });
            var skillPoolProp = so.FindProperty("skillPool");
            if (skillPoolProp != null)
            {
                skillPoolProp.arraySize = skillGuids.Length;
                for (int i = 0; i < skillGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
                    skillPoolProp.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
                }
            }
            Debug.Log($"  功法：{skillGuids.Length} 个");

            // 加载功法数据到skillPool（通过反射设置，因为Demo1Setup没有skillPool字段，
            // 功法池在运行时由Demo1Setup.CreateGameManager动态收集）
            Debug.Log($"  Q技能：{(skillQ != null ? "落石术 ✓" : "❌ 未找到")}");
            Debug.Log($"  E技能：留空（探索获取）");
            Debug.Log($"  R技能：留空（探索获取）");
            Debug.Log($"  模型：{(modelPrefab != null ? "Frank_Katana ✓" : "❌ 未找到（将使用胶囊体）")}");
            Debug.Log($"  动画控制器：{(animController != null ? "✓" : "❌ 请先执行步骤③")}");
            Debug.Log($"  刀光特效：{(slashVFX != null ? "✓" : "❌ 未找到")}");
            Debug.Log($"  打击特效：{(hitVFX != null ? "hit-red-1 ✓" : "❌ 未找到")}");

            Selection.activeGameObject = setup.gameObject;
        }

        [MenuItem("仙途梦境/⑤ 创建 Demo1 场景文件", false, 5)]
        public static void CreateDemo1Scene()
        {
            EnsureDirectory(SCENE_PATH);

            string scenePath = SCENE_PATH + "Demo1.unity";

            // 创建新场景
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            // 创建 Demo1Setup 对象
            var setupGo = new GameObject("Demo1Setup");
            setupGo.AddComponent<Demo1Setup>();

            // 保存场景
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"<color=green>✅ Demo1 场景已创建：{scenePath}</color>");
            Debug.Log("<color=yellow>请接着执行 ④ 自动配置 Demo1 场景</color>");
        }

        [MenuItem("仙途梦境/⑥ 创建怪物预制体配置", false, 6)]
        public static void CreateMonsterPrefabsConfig()
        {
            string configPath = "Assets/1Game/Resources/MonsterPrefabs.asset";

            // 加载各怪物Prefab
            var creeper = AssetDatabase.LoadAssetAtPath<GameObject>(
                MONSTER_PACK_PATH + "Creeper Cute Series/Prefabs/Creeper.prefab");
            var haunt = AssetDatabase.LoadAssetAtPath<GameObject>(
                MONSTER_PACK_PATH + "Haunt Cute Series/Prefabs/Haunt.prefab");
            var lurker = AssetDatabase.LoadAssetAtPath<GameObject>(
                MONSTER_PACK_PATH + "Lurker Cute Series/Prefabs/Lurker.prefab");
            var soulMage = AssetDatabase.LoadAssetAtPath<GameObject>(
                MONSTER_PACK_PATH + "Soul Mage Cute Series/Prefabs/Soul Mage.prefab");
            var dragonDarkness = AssetDatabase.LoadAssetAtPath<GameObject>(
                MONSTER_PACK_PATH + "Dragon Darkness Cute Series/Prefabs/Dragon Darkness.prefab");

            // 创建或更新配置
            var existing = AssetDatabase.LoadAssetAtPath<MonsterPrefabs>(configPath);
            MonsterPrefabs config;
            if (existing != null)
            {
                config = existing;
            }
            else
            {
                config = ScriptableObject.CreateInstance<MonsterPrefabs>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            config.普通小怪Prefab = creeper;
            config.远程敌人Prefab = haunt;
            config.冲锋敌人Prefab = lurker;
            config.法师敌人Prefab = soulMage;
            config.Boss敌人Prefab = dragonDarkness;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>✅ 怪物预制体配置创建完成！</color>");
            Debug.Log($"  普通小怪：{(creeper != null ? "Creeper ✓" : "❌ 未找到")}");
            Debug.Log($"  远程敌人：{(haunt != null ? "Haunt ✓" : "❌ 未找到")}");
            Debug.Log($"  冲锋敌人：{(lurker != null ? "Lurker ✓" : "❌ 未找到")}");
            Debug.Log($"  法师敌人：{(soulMage != null ? "Soul Mage ✓" : "❌ 未找到")}");
            Debug.Log($"  Boss敌人：{(dragonDarkness != null ? "Dragon Darkness ✓" : "❌ 未找到")}");

            Selection.activeObject = config;
        }

        // ==================== 动画工具方法 ====================

        /// <summary>从 Frank_Katana FBX 文件中加载动画剪辑</summary>
        private static AnimationClip LoadAnimClip(string clipName)
        {
            // 先尝试从 FBX 目录加载
            string fbxPath = FRANK_ANIM_FBX_PATH + clipName + ".FBX";
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in clips)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            // 尝试从 Animations 根目录加载
            string animPath = FRANK_ANIM_PATH + clipName + ".FBX";
            clips = AssetDatabase.LoadAllAssetsAtPath(animPath);
            foreach (var asset in clips)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            // 尝试搜索整个 Frank_Katana 目录
            string[] guids = AssetDatabase.FindAssets(clipName,
                new[] { "Assets/1Game/ArtRes/Package/Frank_Katana" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".FBX") || path.EndsWith(".fbx") || path.EndsWith(".anim"))
                {
                    clips = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var asset in clips)
                    {
                        if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                            return clip;
                    }
                }
            }

            Debug.LogWarning($"  未找到动画剪辑：{clipName}");
            return null;
        }

        /// <summary>为简单动画（闪避/受击/技能）添加结束事件</summary>
        private static void AddSimpleEndEvent(AnimationClip clip, string endEventName)
        {
            if (clip == null) return;

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath)) return;

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            var clipAnimations = importer.clipAnimations;
            if (clipAnimations.Length == 0)
                clipAnimations = importer.defaultClipAnimations;

            bool modified = false;
            for (int i = 0; i < clipAnimations.Length; i++)
            {
                var clipAnim = clipAnimations[i];

                // 强制所有clip锁定位移和旋转到原点（确保动画原地播放）
                clipAnim.keepOriginalPositionXZ = true;
                clipAnim.keepOriginalOrientation = true;
                clipAnim.keepOriginalPositionY = true;

// 只给目标clip添加事件（FBX中可能有多个clip，只给目标clip添加）
                if (clipAnim.name != clip.name)
                {
                    clipAnimations[i] = clipAnim;
                    modified = true;
                    continue;
                }

                float clipDuration = clip.length;
                float frameDuration = (clipAnim.lastFrame - clipAnim.firstFrame) / 60f;

                if (clipDuration > frameDuration * 1.5f && frameDuration > 0)
                {
                    Debug.LogWarning($"  [AddSimpleEndEvent] {clip.name}: clip.length={clipDuration:F3}s 远大于帧范围时长={frameDuration:F3}s，使用帧范围时长");
                    clipDuration = frameDuration;
                }

                float eventTime = clipDuration * 0.80f;
                if (eventTime < 0) eventTime = 0;

                Debug.Log($"  [AddSimpleEndEvent] {clip.name}: clipDuration={clipDuration:F3}s, " +
                          $"frameDuration={frameDuration:F3}s, clip.length={clip.length:F3}s, " +
                          $"eventTime={eventTime:F3}s");

                clipAnim.events = new AnimationEvent[]
                {
                    new AnimationEvent
                    {
                        functionName = endEventName,
                        time = eventTime
                    }
                };

                clipAnimations[i] = clipAnim;
                modified = true;
            }

            if (modified)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
                Debug.Log($"  已为 {clip.name} 添加结束事件：{endEventName}");
            }
        }

        /// <summary>为攻击动画添加动画事件（FBX 内嵌 clip）</summary>
        private static void AddAttackAnimationEvents(AnimationClip clip, int comboStep)
        {
            if (clip == null) return;

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath)) return;

            // FBX 内嵌 clip 的处理方式（通过 ModelImporter）
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            var clipAnimations = importer.clipAnimations;
            if (clipAnimations.Length == 0)
                clipAnimations = importer.defaultClipAnimations;

            bool modified = false;
            for (int i = 0; i < clipAnimations.Length; i++)
            {
                var clipAnim = clipAnimations[i];

                // 强制所有clip锁定位移和旋转到原点（确保动画原地播放）
                clipAnim.keepOriginalPositionXZ = true;
                clipAnim.keepOriginalOrientation = true;
                clipAnim.keepOriginalPositionY = true;

// 只给目标clip添加事件（FBX中可能有多个clip，只给目标clip添加）
                if (clipAnim.name != clip.name)
                {
                    clipAnimations[i] = clipAnim;
                    modified = true;
                    continue;
                }

                float frameDuration = (clipAnim.lastFrame - clipAnim.firstFrame) / 60f;
                float fbxClipDuration = clip.length;
                if (fbxClipDuration > frameDuration * 1.5f && frameDuration > 0)
                    fbxClipDuration = frameDuration;

                var events = new AnimationEvent[]
                {
                    new AnimationEvent { functionName = "OnHitWindowOpen", time = fbxClipDuration * 0.2f },
                    new AnimationEvent { functionName = "OnSlashVFX", time = fbxClipDuration * 0.25f },
                    new AnimationEvent { functionName = "OnHitWindowClose", time = fbxClipDuration * 0.5f },
                    new AnimationEvent { functionName = "OnComboWindowOpen", time = fbxClipDuration * 0.45f },
                    new AnimationEvent { functionName = "OnComboWindowClose", time = fbxClipDuration * 0.65f },
                    new AnimationEvent { functionName = "OnAttackEnd", time = fbxClipDuration * 0.70f }
                };

                clipAnim.events = events;
                clipAnimations[i] = clipAnim;
                modified = true;
            }

            if (modified)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
                Debug.Log($"  已为 {clip.name} 添加动画事件（连招段 {comboStep}）");
            }
        }

        // ==================== 灵物创建 ====================

        private static void CreateFireOrb()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "火灵珠";
            item.description = "蕴含火灵之力的珠子，攻击附带灼烧效果。\n叠加可增强灼烧伤害，集齐五颗可引发焚天冲击波。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Attack;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5, 8 };
            item.burnDamagePerSecond = 5f;
            item.attackBonus = 2f;
            SaveAsset(item, ITEM_PATH + "火灵珠.asset");
        }

        private static void CreateWindOrb()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "风灵珠";
            item.description = "凝聚风灵之力，持有者身轻如燕。\n叠加可进一步提升移速，集齐五颗闪避后留下风之残影。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Movement;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5 };
            item.moveSpeedBonusPercent = 0.1f;
            SaveAsset(item, ITEM_PATH + "风灵珠.asset");
        }

        private static void CreateJadePendant()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "玉佩";
            item.description = "温润的灵玉所制，可抵御部分伤害。\n叠加增强减伤效果，集齐五块可触发玉碎免死。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Defense;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5 };
            item.damageReductionBonus = 0.05f;
            item.maxHpBonus = 10f;
            SaveAsset(item, ITEM_PATH + "玉佩.asset");
        }

        private static void CreateRustySword()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "锈铁飞剑";
            item.description = "一柄锈迹斑斑的飞剑，此剑虽锈，剑意犹存。\n叠加增加攻击力和穿透，集齐五柄可召唤剑阵护体。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Attack;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5, 8 };
            item.attackBonus = 3f;
            item.pierceBonus = 1;
            item.attackSpeedBonusPercent = 0.05f;
            SaveAsset(item, ITEM_PATH + "锈铁飞剑.asset");
        }

        private static void CreateHealPill()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "回灵丹";
            item.description = "服用后灵力充盈，生命力大增。\n击杀敌人时可回复少量生命，集齐五颗可触发涅槃复活。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Pill;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5 };
            item.maxHpBonus = 20f;
            item.maxHpBonusPercent = 0.05f;
            item.healOnKill = 3f;
            SaveAsset(item, ITEM_PATH + "回灵丹.asset");
        }

        // ==================== 灵品灵物 ====================

        private static void CreateThunderBead()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "雷灵珠";
            item.description = "蕴含雷霆之力的灵珠，攻击时有概率触发雷击。\n叠加增强暴击率和暴击伤害。";
            item.rarity = ItemRarity.Ling;
            item.category = ItemCategory.Attack;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 4, 7 };
            item.attackBonus = 5f;
            item.critRateBonus = 0.08f;
            SaveAsset(item, ITEM_PATH + "雷灵珠.asset");
        }

        private static void CreateShadowCloak()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "影遁衣";
            item.description = "以暗影之力编织的灵衣，穿戴后身形飘忽。\n大幅提升移速和攻速，闪避后短暂隐身。";
            item.rarity = ItemRarity.Ling;
            item.category = ItemCategory.Movement;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 4 };
            item.moveSpeedBonusPercent = 0.15f;
            item.attackSpeedBonusPercent = 0.1f;
            SaveAsset(item, ITEM_PATH + "影遁衣.asset");
        }

        // ==================== 玄品灵物 ====================

        private static void CreateDragonScale()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "龙鳞甲";
            item.description = "取自上古真龙的鳞片，坚不可摧。\n大幅提升减伤和生命上限，受击时反弹伤害。";
            item.rarity = ItemRarity.Xuan;
            item.category = ItemCategory.Defense;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 3, 5 };
            item.damageReductionBonus = 0.12f;
            item.maxHpBonus = 40f;
            item.maxHpBonusPercent = 0.1f;
            SaveAsset(item, ITEM_PATH + "龙鳞甲.asset");
        }

        private static void CreatePhoenixFeather()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "凤凰羽";
            item.description = "凤凰涅槃后遗落的神羽，蕴含重生之力。\n大幅提升攻击力，击杀回复大量生命。";
            item.rarity = ItemRarity.Xuan;
            item.category = ItemCategory.Attack;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 3 };
            item.attackBonus = 8f;
            item.attackBonusPercent = 0.15f;
            item.healOnKill = 8f;
            SaveAsset(item, ITEM_PATH + "凤凰羽.asset");
        }

        // ==================== 地品灵物 ====================

        private static void CreateVoidStone()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "虚空石";
            item.description = "来自虚空裂隙的神秘矿石，扭曲时空之力。\n全属性大幅提升，攻击附带虚空侵蚀。";
            item.rarity = ItemRarity.Di;
            item.category = ItemCategory.Attack;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 2, 4 };
            item.attackBonus = 12f;
            item.attackBonusPercent = 0.1f;
            item.maxHpBonus = 30f;
            item.critRateBonus = 0.1f;
            item.burnDamagePerSecond = 8f;
            SaveAsset(item, ITEM_PATH + "虚空石.asset");
        }

        // ==================== 天品灵物 ====================

        // ==================== 新增灵物 ====================

        /// <summary>灵品·异变类：寒冰玉髓，攻击附带冻结</summary>
        private static void CreateIceJade()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "寒冰玉髓";
            item.description = "万年寒冰凝结的玉髓，触之刺骨。\n攻击有概率冻结敌人，叠加增强冻结概率。";
            item.rarity = ItemRarity.Ling;
            item.category = ItemCategory.Anomaly;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 4 };
            item.freezeChance = 0.1f;
            item.attackBonus = 3f;
            SaveAsset(item, ITEM_PATH + "寒冰玉髓.asset");
        }

        /// <summary>凡品·异变类：血珊瑚，击杀回血+灼烧</summary>
        private static void CreateBloodCoral()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "血珊瑚";
            item.description = "深海血色珊瑚，蕴含嗜血之力。\n击杀回复生命，攻击附带微弱灼烧。";
            item.rarity = ItemRarity.Fan;
            item.category = ItemCategory.Anomaly;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 5 };
            item.healOnKill = 4f;
            item.burnDamagePerSecond = 2f;
            SaveAsset(item, ITEM_PATH + "血珊瑚.asset");
        }

        /// <summary>玄品·异变类：破镜碎片，暴击率+暴击伤害</summary>
        private static void CreateMirrorShard()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "破镜碎片";
            item.description = "破碎的照妖镜残片，映射出敌人的弱点。\n大幅提升暴击率和暴击伤害。";
            item.rarity = ItemRarity.Xuan;
            item.category = ItemCategory.Anomaly;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 3 };
            item.critRateBonus = 0.12f;
            item.attackBonusPercent = 0.08f;
            SaveAsset(item, ITEM_PATH + "破镜碎片.asset");
        }

        /// <summary>灵品·丹药类：灵藤草，持续回复+移速</summary>
        private static void CreateSpiritVine()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "灵藤草";
            item.description = "灵山深处的藤蔓精华，服用后身轻体健。\n提升生命上限和移速，击杀回复少量生命。";
            item.rarity = ItemRarity.Ling;
            item.category = ItemCategory.Pill;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 4 };
            item.maxHpBonus = 25f;
            item.moveSpeedBonusPercent = 0.08f;
            item.healOnKill = 5f;
            SaveAsset(item, ITEM_PATH + "灵藤草.asset");
        }

        /// <summary>地品·身法类：星辰尘，极大提升移速和攻速</summary>
        private static void CreateStarDust()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "星辰尘";
            item.description = "坠落的星辰碎屑，蕴含时空加速之力。\n极大提升移速和攻速，闪避距离增加。";
            item.rarity = ItemRarity.Di;
            item.category = ItemCategory.Movement;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 2 };
            item.moveSpeedBonusPercent = 0.25f;
            item.attackSpeedBonusPercent = 0.2f;
            SaveAsset(item, ITEM_PATH + "星辰尘.asset");
        }

        /// <summary>地品·异变类：魂灯，全属性提升+特殊效果</summary>
        private static void CreateSoulLantern()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "引魂灯";
            item.description = "引渡亡魂的古灯，击杀敌人时吸收残魂。\n大幅提升暴击和击杀回复，攻击附带灼烧。";
            item.rarity = ItemRarity.Di;
            item.category = ItemCategory.Anomaly;
            item.stackable = true;
            item.qualitativeThresholds = new int[] { 2 };
            item.critRateBonus = 0.12f;
            item.healOnKill = 10f;
            item.burnDamagePerSecond = 6f;
            item.attackBonus = 8f;
            SaveAsset(item, ITEM_PATH + "引魂灯.asset");
        }

        /// <summary>天品·防御类：混沌核心，全属性极大提升</summary>
        private static void CreateChaosCore()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "混沌核心";
            item.description = "混沌初开时遗留的本源之力，持有者攻守兼备。\n全属性极大提升，受击反弹伤害。";
            item.rarity = ItemRarity.Tian;
            item.category = ItemCategory.Defense;
            item.stackable = false;
            item.damageReductionBonus = 0.2f;
            item.maxHpBonus = 80f;
            item.maxHpBonusPercent = 0.25f;
            item.attackBonus = 15f;
            item.attackBonusPercent = 0.15f;
            item.healOnKill = 8f;
            SaveAsset(item, ITEM_PATH + "混沌核心.asset");
        }

        private static void CreateCelestialOrb()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "天道珠";
            item.description = "蕴含天道法则的至宝，持有者如有神助。\n全属性极大提升，攻击附带天雷制裁。";
            item.rarity = ItemRarity.Tian;
            item.category = ItemCategory.Attack;
            item.stackable = false;
            item.attackBonus = 20f;
            item.attackBonusPercent = 0.25f;
            item.maxHpBonus = 50f;
            item.maxHpBonusPercent = 0.2f;
            item.critRateBonus = 0.15f;
            item.damageReductionBonus = 0.1f;
            item.burnDamagePerSecond = 15f;
            item.healOnKill = 10f;
            SaveAsset(item, ITEM_PATH + "天道珠.asset");
        }

        private static void CreateFallingRock()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "落石术";
            skill.description = "凝聚灵力于高空，召唤巨石砸落指定位置。\n对范围内敌人造成大量伤害。";
            skill.rarity = ItemRarity.Fan;
            skill.skillType = SkillType.AreaDamage;
            skill.baseDamage = 30f;
            skill.damageScaling = 0.5f;
            skill.cooldown = 8f;
            skill.aoeRadius = 3f;
            skill.vfxDuration = 1.5f;
            SaveAsset(skill, SKILL_PATH + "落石术.asset");
        }

        /// <summary>创建金钟罩技能（Buff类型）</summary>
        private static void CreateGoldenBell()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "金钟罩";
            skill.description = "凝聚灵力化为金色护罩，大幅提升减伤\n持续数秒后消散。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.Buff;
            skill.baseDamage = 0f;
            skill.damageScaling = 0f;
            skill.cooldown = 12f;
            skill.aoeRadius = 0f;
            skill.vfxDuration = 5f;
            SaveAsset(skill, SKILL_PATH + "金钟罩.asset");
        }

        /// <summary>创建天雷引技能（范围伤害，R槽位默认）</summary>
        private static void CreateThunderStrike()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "天雷引";
            skill.description = "引天雷轰击指定区域，造成大量雷电伤害。\n范围更大，伤害更高，但冷却也更长。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.AreaDamage;
            skill.baseDamage = 50f;
            skill.damageScaling = 0.8f;
            skill.cooldown = 15f;
            skill.aoeRadius = 4f;
            skill.vfxDuration = 2f;
            SaveAsset(skill, SKILL_PATH + "天雷引.asset");
        }

        /// <summary>创建御剑术技能（投射物类型）</summary>
        private static void CreateSwordProjectile()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "御剑术";
            skill.description = "御使灵剑向前方飞射，贯穿敌人。\n可同时发射三柄飞剑，扣形散射。";
            skill.rarity = ItemRarity.Fan;
            skill.skillType = SkillType.Projectile;
            skill.baseDamage = 20f;
            skill.damageScaling = 0.4f;
            skill.cooldown = 5f;
            skill.projectileSpeed = 18f;
            skill.projectileCount = 3;
            skill.spreadAngle = 30f;
            skill.vfxDuration = 1f;
            SaveAsset(skill, SKILL_PATH + "御剑术.asset");
        }

        /// <summary>创建土遁术技能（位移类型）</summary>
        private static void CreateEarthDash()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "土遁术";
            skill.description = "遁入地下瞬间移动至前方远处。\n路径上的敌人会被地刺伤害。";
            skill.rarity = ItemRarity.Fan;
            skill.skillType = SkillType.Dash;
            skill.baseDamage = 15f;
            skill.damageScaling = 0.3f;
            skill.cooldown = 6f;
            skill.dashDistance = 10f;
            skill.leaveTrail = true;
            skill.vfxDuration = 1f;
            SaveAsset(skill, SKILL_PATH + "土遁术.asset");
        }

        /// <summary>创建寒冰诀技能（范围伤害类型）</summary>
        private static void CreateFrostBlast()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "寒冰诀";
            skill.description = "以自身为中心释放冰霜冲击波。\n冻结周围敌人，造成冰霜伤害。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.AreaDamage;
            skill.baseDamage = 25f;
            skill.damageScaling = 0.6f;
            skill.cooldown = 10f;
            skill.aoeRadius = 5f;
            skill.vfxDuration = 1.5f;
            SaveAsset(skill, SKILL_PATH + "寒冰诀.asset");
        }

        /// <summary>创建回春术技能（治疗类型）</summary>
        private static void CreateHealSpring()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "回春术";
            skill.description = "运转灵力治疗自身伤势。\n恢复生命值，治疗量随攻击力提升。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.Heal;
            skill.baseDamage = 0f;
            skill.damageScaling = 0f;
            skill.cooldown = 18f;
            skill.healAmount = 40f;
            skill.healScaling = 0.5f;
            skill.vfxDuration = 1.5f;
            SaveAsset(skill, SKILL_PATH + "回春术.asset");
        }

        // ==================== 新增功法 ====================

        /// <summary>创建傀儡术技能（召唤类型）</summary>
        private static void CreatePuppetSummon()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "傀儡术";
            skill.description = "召唤一具灵力傀儡为你作战。\n傀儡会自动攻击附近敌人，持续一段时间后消散。";
            skill.rarity = ItemRarity.Xuan;
            skill.skillType = SkillType.Summon;
            skill.baseDamage = 0f;
            skill.damageScaling = 0f;
            skill.cooldown = 20f;
            skill.summonDuration = 10f;
            skill.summonDamage = 12f;
            skill.vfxDuration = 1f;
            SaveAsset(skill, SKILL_PATH + "傀儡术.asset");
        }

        /// <summary>创建烈焰掌技能（投射物类型，扇形火焰）</summary>
        private static void CreateFlameWave()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "烈焰掌";
            skill.description = "掌心喷射扇形火焰，灼烧前方敌人。\n多段火焰覆盖大范围，附带灼烧效果。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.Projectile;
            skill.baseDamage = 15f;
            skill.damageScaling = 0.35f;
            skill.cooldown = 7f;
            skill.projectileSpeed = 14f;
            skill.projectileCount = 5;
            skill.spreadAngle = 60f;
            skill.vfxDuration = 1f;
            SaveAsset(skill, SKILL_PATH + "烈焰掌.asset");
        }

        /// <summary>创建镜花水月技能（Buff类型，反伤护盾）</summary>
        private static void CreateMirrorShield()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "镜花水月";
            skill.description = "凝聚灵力化为镜面护盾，反弹受到的伤害。\n持续数秒，期间受到的伤害反弹给攻击者。";
            skill.rarity = ItemRarity.Xuan;
            skill.skillType = SkillType.Buff;
            skill.baseDamage = 0f;
            skill.damageScaling = 0f;
            skill.cooldown = 16f;
            skill.vfxDuration = 4f;
            SaveAsset(skill, SKILL_PATH + "镜花水月.asset");
        }

        /// <summary>创建雷锁链技能（范围伤害，链式闪电）</summary>
        private static void CreateThunderChain()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "雷锁链";
            skill.description = "释放雷电锁链连接多个敌人。\n闪电在敌人之间跳跃，每次跳跃伤害递减。";
            skill.rarity = ItemRarity.Xuan;
            skill.skillType = SkillType.AreaDamage;
            skill.baseDamage = 35f;
            skill.damageScaling = 0.6f;
            skill.cooldown = 12f;
            skill.aoeRadius = 6f;
            skill.vfxDuration = 1.5f;
            SaveAsset(skill, SKILL_PATH + "雷锁链.asset");
        }

        /// <summary>创建影步技能（位移类型，2层充能）</summary>
        private static void CreateShadowStep()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "影步";
            skill.description = "化为暗影瞬移至目标位置。\n拥有两层充能，可连续使用两次。";
            skill.rarity = ItemRarity.Ling;
            skill.skillType = SkillType.Dash;
            skill.baseDamage = 10f;
            skill.damageScaling = 0.2f;
            skill.cooldown = 4f;
            skill.maxCharges = 2;
            skill.chargeTime = 4f;
            skill.dashDistance = 7f;
            skill.leaveTrail = false;
            skill.vfxDuration = 0.5f;
            SaveAsset(skill, SKILL_PATH + "影步.asset");
        }

        // ==================== 工具方法 ====================

        private static void SaveAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"  创建：{path}");
        }

        private static void EnsureDirectory(string path)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, "..", path);
            if (!System.IO.Directory.Exists(fullPath))
                System.IO.Directory.CreateDirectory(fullPath);
        }

        private static void AddTag(string tagName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    Debug.Log($"  Tag '{tagName}' 已存在");
                    return;
                }
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"  已添加 Tag: {tagName}");
        }

        private static void AddLayer(string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    Debug.Log($"  Layer '{layerName}' 已存在（Layer {i}）");
                    return;
                }
            }

            for (int i = 8; i < 32; i++)
            {
                if (string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(i).stringValue))
                {
                    layersProp.GetArrayElementAtIndex(i).stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"  已添加 Layer: {layerName}（Layer {i}）");
                    return;
                }
            }

            Debug.LogWarning($"  无法添加 Layer '{layerName}'：没有空闲的 User Layer");
        }

        // ==================== 音效配置 ====================

        private static void CreateAudioConfig()
        {
            string configPath = "Assets/1Game/Resources/AudioConfig.asset";

            // 如果已存在则跳过
            if (AssetDatabase.LoadAssetAtPath<AudioConfig>(configPath) != null)
            {
                Debug.Log("  AudioConfig 已存在，跳过创建");
                return;
            }

            EnsureDirectory("Assets/1Game/Resources/");

            var config = ScriptableObject.CreateInstance<AudioConfig>();

            // 设置默认音量
            config.masterVolume = 1f;
            config.sfxVolume = 0.8f;
            config.bgmVolume = 0.5f;
            config.uiVolume = 0.7f;

            // 初始化数组（空槽位，后续在 Inspector 中拖入音频资源）
            config.meleeAttacks = new UnityEngine.AudioClip[3];
            config.meleeHits = new UnityEngine.AudioClip[3];
            config.playerHurt = new UnityEngine.AudioClip[3];
            config.enemyHurt = new UnityEngine.AudioClip[3];
            config.enemyDeath = new UnityEngine.AudioClip[2];
            config.itemPickup = new UnityEngine.AudioClip[5]; // 凡/灵/玄/地/天
            config.bgmBattle = new UnityEngine.AudioClip[3];  // 前/中/后期

            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log($"  ✅ 已创建音效配置：{configPath}");
            Debug.Log("  💡 提示：在 Inspector 中打开 AudioConfig，将音频文件拖入对应槽位即可");
        }
    }
}
