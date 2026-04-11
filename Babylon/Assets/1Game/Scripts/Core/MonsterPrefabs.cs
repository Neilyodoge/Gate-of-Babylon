using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 怪物预制体配置 —— ScriptableObject
    /// 集中管理所有怪物模型的Prefab引用
    /// 菜单：Assets → Create → 仙途梦境 → 怪物预制体配置
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterPrefabs", menuName = "仙途梦境/怪物预制体配置")]
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
