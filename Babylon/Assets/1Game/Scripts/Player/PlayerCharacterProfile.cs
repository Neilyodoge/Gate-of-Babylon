using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 主角形象/职业档案 —— 把"用哪个模型 + 哪套动画控制器 + 普攻是近战还是远程"
    /// 打包成一个数据资产。与化身（五系，纯机制）正交：
    ///   - 角色档案决定 外观 + 普攻形态（剑客近战 / 法师远程）
    ///   - 化身决定 数值与专属机制
    ///
    /// 运行时由 <see cref="PlayerController.ApplyCharacterProfile"/> 热替换玩家模型，
    /// 资产放在 Resources/CharacterProfiles/ 下，由 <see cref="PlayerCharacterRegistry"/> 统一加载。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterProfile", menuName = "仙途秘境/主角档案")]
    public class PlayerCharacterProfile : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("唯一 ID（如 sword / mage），用于存档与排序")]
        public string characterId = "sword";
        public string displayName = "剑客";
        [Tooltip("角色定位标签，如 近战 / 远程·法系")]
        public string roleTag = "近战";
        [TextArea] public string description = "";
        public Color themeColor = new Color(0.8f, 0.7f, 0.3f);
        [Tooltip("选择面板排序，越小越靠前")]
        public int sortOrder = 0;

        [Header("模型 & 动画")]
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;
        [Tooltip("模型整体缩放（不同美术资源体型不一）")]
        public float modelScale = 1f;
        [Tooltip("模型 Avatar（Generic 骨架兜底用）。若模型 FBX 本身不带 Animator，运行时会补建 Animator 并绑定此 Avatar")]
        public Avatar modelAvatar;

        [Header("普攻形态")]
        [Tooltip("勾选则左键为远程：在挥击动画事件点发射投射物，而非近战扇形判定")]
        public bool rangedBasicAttack = false;
        [Tooltip("远程普攻使用的投射物 Prefab（需挂 Projectile 组件；为空则用程序化占位）")]
        public GameObject basicProjectilePrefab;
        public float basicProjectileSpeed = 18f;
        public ElementTag basicElement = ElementTag.None;
        [Tooltip("远程普攻伤害相对近战公式的倍率")]
        public float basicDamageMultiplier = 1f;

        [Header("挂点偏移（相对玩家根节点，沿瞄准方向）")]
        [Tooltip("攻击/发射原点偏移（x=侧向, y=高度, z=前向）")]
        public Vector3 attackOriginOffset = new Vector3(0f, 0.9f, 0.6f);
        [Tooltip("刀光/法术发射视觉点偏移")]
        public Vector3 slashVFXOffset = new Vector3(0f, 1.0f, 0.8f);

        [Header("特效覆盖（可选，留空则沿用 Demo1Setup 的全局设置）")]
        public GameObject slashVFXPrefab;
        public GameObject hitVFXPrefab;
    }
}
