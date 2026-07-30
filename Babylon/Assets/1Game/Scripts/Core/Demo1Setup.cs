using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Demo1 场景编排器（瘦 Bootstrap）。
    ///
    /// 结构约定（合规化，2026-07-30 重构）：
    /// - 美术相关对象（Main Camera + TopDownCamera / Directional Light / Global Volume 后处理）
    ///   已改为「场景预置」，挂在场景「Art」节点下，直接在 Inspector 调参，不再运行时实例化。
    /// - 需要运行时实例化的对象按类别拆分，由各自的 Builder 脚本构建、挂到对应类别根节点下：
    ///     · <see cref="SystemsBuilder"/> → "Systems"（对象池 / GameManager / 顿帧 / 过渡 / EventSystem / 音效）
    ///     · <see cref="GameplayBuilder"/> → "Gameplay"（临时地面 / 玩家）
    ///     · <see cref="HudBuilder"/>      → "UI"（GameCanvas + HUD）
    /// - 本脚本只负责「持有配置 + 按依赖顺序调度三个 Builder」。
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

        [Header("类别 Builder（留空则运行时自动创建对应根节点）")]
        [SerializeField] private SystemsBuilder systemsBuilder;
        [SerializeField] private GameplayBuilder gameplayBuilder;
        [SerializeField] private HudBuilder hudBuilder;

        private void Awake()
        {
            var systems = ResolveBuilder(ref systemsBuilder, "Systems");
            var gameplay = ResolveBuilder(ref gameplayBuilder, "Gameplay");
            var ui = ResolveBuilder(ref hudBuilder, "UI");

            // 顺序需保持：对象池 → 地面 → 玩家 → GameManager → HUD → 其余系统。
            systems.BuildObjectPool();
            gameplay.BuildGround();
            gameplay.BuildPlayer(playerModelPrefab, animatorController, slashVFXPrefab, hitVFXPrefab,
                testSkillQ, testSkillE, testSkillR);

            // 美术对象由场景预置；若场景缺失则兜底补齐相机 / 平行光（后处理走场景 Volume，不在此兜底）。
            EnsureArtObjectsFallback();

            systems.BuildGameManager(skillPool, testSkillQ, testSkillE, testSkillR, hitVFXPrefab);
            ui.BuildHud();
            systems.BuildHitStop();
            systems.BuildLevelTransition();
            systems.BuildEventSystem();
            systems.BuildAudioManager();
        }

        /// <summary>拿到对应类别 Builder：未在 Inspector 指定则查找场景，仍无则运行时新建一个类别根节点。</summary>
        private T ResolveBuilder<T>(ref T field, string rootName) where T : MonoBehaviour
        {
            if (field != null) return field;
            field = FindFirstObjectByType<T>();
            if (field != null) return field;

            var root = new GameObject(rootName);
            field = root.AddComponent<T>();
            return field;
        }

        /// <summary>
        /// 兜底：相机 / 平行光 正常由 Demo1 场景「Art」节点预置。
        /// 万一场景缺失（例如空场景直接挂本脚本调试），才运行时补齐一份默认对象。
        /// </summary>
        private void EnsureArtObjectsFallback()
        {
            if (Camera.main == null || Camera.main.GetComponent<TopDownCamera>() == null)
                SetupCamera();
            if (FindFirstObjectByType<Light>() == null)
                SetupLighting();
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

        private void SetupLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
    }
}
