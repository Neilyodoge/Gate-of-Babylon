using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Systems 类别构建器：对象池 / GameManager(+DebugConsole) / 顿帧 / 层间过渡 / EventSystem / 音效。
    /// 挂在场景「Systems」根节点上，由 <see cref="Demo1Setup"/> 按序调用；
    /// 生成的对象统一挂到本节点下。
    /// </summary>
    public class SystemsBuilder : MonoBehaviour
    {
        public void BuildObjectPool()
        {
            var poolGo = new GameObject("ObjectPool");
            poolGo.transform.SetParent(transform, false);
            poolGo.AddComponent<ObjectPool>();
        }

        public void BuildGameManager(SkillData[] skillPool, SkillData testSkillQ, SkillData testSkillE,
            SkillData testSkillR, GameObject hitVFXPrefab)
        {
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(transform, false);
            var gm = gmGo.AddComponent<GameManager>();

            // skillPool 为空或全 null 时，编辑器下自动加载
            bool skillPoolEmpty = skillPool == null || skillPool.Length == 0;
            if (!skillPoolEmpty)
            {
                bool allNull = true;
                foreach (var sk in skillPool)
                    if (sk != null) { allNull = false; break; }
                if (allNull)
                {
                    Debug.LogWarning($"[SystemsBuilder] skillPool 有 {skillPool.Length} 个槽位但全部为 null，重新自动加载...");
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
                    Debug.Log($"<color=green>[SystemsBuilder] 自动加载了 {skillPool.Length} 个技能数据</color>");
                }
#endif
            }

            // 设置技能池
            {
                var skillList = new System.Collections.Generic.List<SkillData>();
                if (skillPool != null && skillPool.Length > 0)
                {
                    foreach (var sk in skillPool)
                        if (sk != null && !skillList.Contains(sk))
                            skillList.Add(sk);
                }
                if (testSkillQ != null && !skillList.Contains(testSkillQ)) skillList.Add(testSkillQ);
                if (testSkillE != null && !skillList.Contains(testSkillE)) skillList.Add(testSkillE);
                if (testSkillR != null && !skillList.Contains(testSkillR)) skillList.Add(testSkillR);

                var skillPoolField = typeof(GameManager).GetField("skillPool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (skillPoolField != null && skillList.Count > 0)
                {
                    skillPoolField.SetValue(gm, skillList.ToArray());
                    Debug.Log($"<color=cyan>[SystemsBuilder] 技能池：{skillList.Count} 个技能</color>");
                }
                else if (skillList.Count == 0)
                {
                    Debug.Log("<color=yellow>[SystemsBuilder] 未找到技能数据，技能池为空</color>");
                }
            }

            // 打击特效传给 GameManager，用于生成的敌人
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
                    Debug.Log($"<color=#00ffcc>[SystemsBuilder] 模块池：{mods.Length} 个模块定义</color>");
                }
            }

            // Debug 控制台（F1 打开）
            gmGo.AddComponent<DebugConsole>();
        }

        public void BuildHitStop()
        {
            var go = new GameObject("HitStop");
            go.transform.SetParent(transform, false);
            go.AddComponent<HitStop>();
        }

        public void BuildLevelTransition()
        {
            var go = new GameObject("LevelTransition");
            go.transform.SetParent(transform, false);
            go.AddComponent<LevelTransition>();
        }

        public void BuildEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;

            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(transform, false);
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public void BuildAudioManager()
        {
            if (AudioManager.Instance != null) return;

            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(transform, false);
            audioGo.AddComponent<AudioManager>();
        }
    }
}
