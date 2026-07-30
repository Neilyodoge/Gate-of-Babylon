using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Build 可用的模块目录。ModuleDef 资产位于 Data/Modules（不在 Resources）时，
    /// 由该 Resources 资产持有显式引用，避免编辑器 AssetDatabase 注入掩盖打包后空模块池。
    /// </summary>
    [CreateAssetMenu(fileName = "ModuleCatalog", menuName = "仙途秘境/模块/模块目录")]
    public class ModuleCatalog : ScriptableObject
    {
        public ModuleDef[] modules;
    }
}
