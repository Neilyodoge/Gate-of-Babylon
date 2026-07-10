using UnityEngine;

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

        [Header("═══ 普通近战小怪 ═══")]
        [Tooltip("普通近战敌人的模型Prefab（如Creeper）")]
        public GameObject 普通小怪Prefab;

        [Header("═══ 远程弓箭手 ═══")]
        [Tooltip("远程弓箭手敌人的模型Prefab（如Haunt）")]
        public GameObject 远程敌人Prefab;

        [Header("═══ 冲锋型敌人 ═══")]
        [Tooltip("冲锋型敌人的模型Prefab（如Lurker）")]
        public GameObject 冲锋敌人Prefab;

        [Header("═══ AOE法师 ═══")]
        [Tooltip("AOE法师敌人的模型Prefab（如Soul Mage）")]
        public GameObject 法师敌人Prefab;

        [Header("═══ Boss ═══")]
        [Tooltip("Boss敌人的模型Prefab（如Dragon Darkness）")]
        public GameObject Boss敌人Prefab;

        [Header("═══ Boss 多形态 ═══")]
        [Tooltip("Act2 Boss（幽冥谷守灵 / Dragon Nightfall）")]
        public GameObject Boss_Act2_Prefab;
        [Tooltip("Act3 Boss（炼狱峰龙魂 / Dragon Dusk）")]
        public GameObject Boss_Act3_Prefab;

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
}
