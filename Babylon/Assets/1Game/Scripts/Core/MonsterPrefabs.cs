using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 怪物预制体配置 —— ScriptableObject
    /// 集中管理所有怪物模型的Prefab引用
    /// 菜单：Assets → Create → 仙途秘境 → 怪物预制体配置
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterPrefabs", menuName = "仙途秘境/怪物预制体配置")]
    public class MonsterPrefabs : ScriptableObject
    {
        // ========== 单例访问 ==========
        private static MonsterPrefabs _instance;
        public static MonsterPrefabs Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<MonsterPrefabs>("MonsterPrefabs");
                return _instance;
            }
        }

        [Header("═══ 白昼小怪 ═══")]
        [Tooltip("白昼普通近战敌人的模型 Prefab")]
        public GameObject 普通小怪Prefab;

        [Tooltip("白昼远程敌人的模型 Prefab")]
        public GameObject 远程敌人Prefab;

        [Tooltip("白昼冲锋敌人的模型 Prefab")]
        public GameObject 冲锋敌人Prefab;

        [Tooltip("白昼法师敌人的模型 Prefab")]
        public GameObject 法师敌人Prefab;

        [Header("═══ 永夜小怪 ═══")]
        public GameObject 永夜普通小怪Prefab;
        public GameObject 永夜远程敌人Prefab;
        public GameObject 永夜冲锋敌人Prefab;
        public GameObject 永夜法师敌人Prefab;

        [Header("═══ 昼夜精英 ═══")]
        public GameObject 白昼精英Prefab;
        public GameObject 永夜精英Prefab;

        [Header("═══ 昼夜 Boss ═══")]
        [Tooltip("白昼 Boss 模型 Prefab")]
        public GameObject Boss敌人Prefab;

        [Tooltip("永夜 Boss 模型 Prefab")]
        public GameObject Boss_Act2_Prefab;
        [Tooltip("兼容旧 BossID=3；当前默认复用永夜 Boss")]
        public GameObject Boss_Act3_Prefab;

        public GameObject GetEnemyPrefab(EnemyVisualRole role)
        {
            bool night = LevelAPhaseRuntime.IsNightMapActive;
            return role switch
            {
                EnemyVisualRole.Ranged => night ? 永夜远程敌人Prefab : 远程敌人Prefab,
                EnemyVisualRole.Charger => night ? 永夜冲锋敌人Prefab : 冲锋敌人Prefab,
                EnemyVisualRole.Mage => night ? 永夜法师敌人Prefab : 法师敌人Prefab,
                _ => night ? 永夜普通小怪Prefab : 普通小怪Prefab,
            };
        }

        public GameObject GetElitePrefab()
        {
            return LevelAPhaseRuntime.IsNightMapActive
                ? (永夜精英Prefab != null ? 永夜精英Prefab : 永夜普通小怪Prefab)
                : (白昼精英Prefab != null ? 白昼精英Prefab : 普通小怪Prefab);
        }

        /// <summary>按 bossID 返回对应 Prefab。1=默认Boss，2=Act2，3=Act3。</summary>
        public GameObject GetBossPrefab(int bossID)
        {
            return bossID switch
            {
                2 => Boss_Act2_Prefab != null ? Boss_Act2_Prefab : Boss敌人Prefab,
                3 => Boss_Act3_Prefab != null ? Boss_Act3_Prefab : Boss敌人Prefab,
                _ => Boss敌人Prefab,
            };
        }

        /// <summary>
        /// 实例化怪物模型，如果Prefab为空则回退到胶囊体
        /// </summary>
        public static GameObject InstantiateMonster(GameObject prefab, Vector3 position, string fallbackName)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, position, Quaternion.identity);
                go.name = fallbackName;
                foreach (var animator in go.GetComponentsInChildren<Animator>(true))
                    animator.applyRootMotion = false;
            }
            else
            {
                // 回退：创建胶囊体（兼容没有配置Prefab的情况）
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = fallbackName;
                go.transform.position = position;
            }
            return go;
        }
    }

    public enum EnemyVisualRole
    {
        Melee,
        Ranged,
        Charger,
        Mage,
    }
}
