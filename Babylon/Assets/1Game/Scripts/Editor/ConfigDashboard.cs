using UnityEditor;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 游戏运行参数总控。这里只展示可直接修改并实际生效的配置。
    /// </summary>
    public sealed class ConfigDashboard : EditorWindow
    {
        private const string GameConfigPath = "Assets/1Game/Resources/GameConfig.asset";
        private const string BossAIPath = "Assets/1Game/Resources/EnemyAI/Boss_Default.asset";
        private const string EliteAIPath = "Assets/1Game/Resources/EnemyAI/Elite_Default.asset";

        private Vector2 _scrollPosition;

        [MenuItem("仙途秘境/🎮 Game 总控", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigDashboard>("🎮 Game 总控");
            window.minSize = new Vector2(480f, 560f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🎮 仙途秘境 · Game 总控", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                SaveAll();
            EditorGUILayout.EndHorizontal();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawEnemyAIControl();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawEnemyAIControl()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                EditorGUILayout.HelpBox("GameConfig.asset 不存在。", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("怪物攻击与行为", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "全局按怪物类型生效：不是逐关卡，也不是逐只怪物。修改后自动保存。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(config, "修改怪物 AI 总控");

            EditorGUILayout.LabelField("攻击频率（秒，越小越快）", EditorStyles.boldLabel);
            config.敌人攻击间隔 = EditorGUILayout.Slider("普通近战", config.敌人攻击间隔, 0.5f, 3f);
            config.精英近战攻击间隔 = EditorGUILayout.Slider(
                "精英近战",
                config.精英近战攻击间隔,
                0.3f,
                3f);
            config.Boss近战攻击间隔 = EditorGUILayout.Slider(
                "Boss 近战",
                config.Boss近战攻击间隔,
                0.3f,
                3f);
            config.远程敌人攻击间隔 = EditorGUILayout.Slider(
                "远程射击",
                config.远程敌人攻击间隔,
                0.5f,
                5f);
            config.法师敌人攻击间隔 = EditorGUILayout.Slider(
                "法师施法",
                config.法师敌人攻击间隔,
                0.5f,
                7f);
            config.冲锋敌人攻击间隔 = EditorGUILayout.Slider(
                "冲锋攻击",
                config.冲锋敌人攻击间隔,
                0.5f,
                7f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("近战压迫感", EditorStyles.boldLabel);
            config.同时近战攻击上限 = EditorGUILayout.IntSlider(
                "同时攻击上限",
                config.同时近战攻击上限,
                1,
                6);
            config.普通近战预警时间 = EditorGUILayout.Slider(
                "普通近战预警",
                config.普通近战预警时间,
                0.1f,
                1.2f);
            config.精英近战预警时间 = EditorGUILayout.Slider(
                "精英近战预警",
                config.精英近战预警时间,
                0.1f,
                1.2f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("追击与观察", EditorStyles.boldLabel);
            config.近战战术距离倍率 = EditorGUILayout.Slider(
                "进入观察的距离倍率",
                config.近战战术距离倍率,
                1.1f,
                4f);
            config.普通怪观察停顿概率 = EditorGUILayout.Slider(
                "普通怪停顿概率",
                config.普通怪观察停顿概率,
                0f,
                0.8f);
            config.精英观察停顿概率 = EditorGUILayout.Slider(
                "精英停顿概率",
                config.精英观察停顿概率,
                0f,
                0.8f);
            config.Boss观察停顿概率 = EditorGUILayout.Slider(
                "Boss 停顿概率",
                config.Boss观察停顿概率,
                0f,
                0.8f);
            Vector2 duration = EditorGUILayout.Vector2Field(
                "动作时长范围",
                config.战术动作持续时间);
            config.战术动作持续时间 = new Vector2(
                Mathf.Clamp(duration.x, 0.05f, 3f),
                Mathf.Clamp(duration.y, 0.05f, 3f));

            EditorGUILayout.Space(8f);
            DrawBossAbilityCooldowns();

            bool changed = EditorGUI.EndChangeCheck();
            EditorGUILayout.Space(10f);
            if (GUILayout.Button("恢复推荐值", GUILayout.Height(26f)))
            {
                Undo.RecordObject(config, "恢复怪物 AI 推荐值");
                ApplyRecommendedEnemyAI(config);
                changed = true;
            }

            if (!changed)
                return;

            SyncMeleeProfileCooldowns(config);
            EditorUtility.SetDirty(config);
            EnemyAbilityProfile bossProfile =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(BossAIPath);
            if (bossProfile != null)
                EditorUtility.SetDirty(bossProfile);
            EnemyAbilityProfile eliteProfile =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(EliteAIPath);
            if (eliteProfile != null)
                EditorUtility.SetDirty(eliteProfile);
            SaveAll();
        }

        private static void DrawBossAbilityCooldowns()
        {
            EnemyAbilityProfile profile =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(BossAIPath);
            if (profile == null)
                return;

            Undo.RecordObject(profile, "修改 Boss 技能频率");
            EditorGUILayout.LabelField("Boss 特殊技能冷却", EditorStyles.boldLabel);
            profile.DecisionInterval = EditorGUILayout.Slider(
                "决策检查间隔",
                profile.DecisionInterval,
                0.05f,
                0.5f);
            DrawAbilityCooldown(profile, "leap", "跳跃");
            DrawAbilityCooldown(profile, "charge", "冲锋");
            DrawAbilityCooldown(profile, "shockwave", "震荡波");
            DrawAbilityCooldown(profile, "area", "范围技");
            DrawAbilityCooldown(profile, "day_crown_sweep", "白昼·冠光裁决");
            DrawAbilityCooldown(profile, "night_prison_chains", "永夜·狱链封步");

            EnemyAbilityProfile elite =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(EliteAIPath);
            if (elite != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("精英核心技能冷却", EditorStyles.boldLabel);
                DrawAbilityCooldown(elite, "elite_counter_lunge", "闪避反击");
            }
        }

        private static void DrawAbilityCooldown(
            EnemyAbilityProfile profile,
            string abilityID,
            string label)
        {
            EnemyAbilityRule rule = profile.Abilities?.Find(item => item.ID == abilityID);
            if (rule != null)
                rule.Cooldown = EditorGUILayout.Slider(label, rule.Cooldown, 0.5f, 15f);
        }

        private static void SyncMeleeProfileCooldowns(GameConfig config)
        {
            EnemyAbilityProfile boss =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(BossAIPath);
            EnemyAbilityProfile elite =
                AssetDatabase.LoadAssetAtPath<EnemyAbilityProfile>(EliteAIPath);
            EnemyAbilityRule bossMelee = boss?.Abilities?.Find(item => item.ID == "melee");
            EnemyAbilityRule eliteMelee = elite?.Abilities?.Find(item => item.ID == "melee");

            if (bossMelee != null)
            {
                Undo.RecordObject(boss, "同步 Boss 近战冷却");
                bossMelee.Cooldown = config.Boss近战攻击间隔;
                EditorUtility.SetDirty(boss);
            }
            if (eliteMelee != null)
            {
                Undo.RecordObject(elite, "同步精英近战冷却");
                eliteMelee.Cooldown = config.精英近战攻击间隔;
                EditorUtility.SetDirty(elite);
            }
        }

        private static void ApplyRecommendedEnemyAI(GameConfig config)
        {
            config.敌人攻击间隔 = 1f;
            config.精英近战攻击间隔 = 0.9f;
            config.Boss近战攻击间隔 = 0.9f;
            config.远程敌人攻击间隔 = 2f;
            config.法师敌人攻击间隔 = 2.8f;
            config.冲锋敌人攻击间隔 = 3.2f;
            config.同时近战攻击上限 = 3;
            config.普通近战预警时间 = 0.35f;
            config.精英近战预警时间 = 0.32f;
            config.近战战术距离倍率 = 2.25f;
            config.普通怪观察停顿概率 = 0.1f;
            config.精英观察停顿概率 = 0.08f;
            config.Boss观察停顿概率 = 0.08f;
            config.战术动作持续时间 = new Vector2(0.25f, 0.6f);
        }

        private static void SaveAll()
        {
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }
    }
}
