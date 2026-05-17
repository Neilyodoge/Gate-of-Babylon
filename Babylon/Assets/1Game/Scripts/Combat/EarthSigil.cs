using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 地脉烙印（土化身专属叠层标记 · v0.5 Week 7）
    ///
    /// - 普攻命中敌人 → +1 层（最多 5）
    /// - 4 秒不刷新 → 掉 1 层（玩家在扎根状态时延长到 6s）
    /// - 满层不会自动引爆 —— 引爆条件是"玩家技能命中带烙印的敌人"，由
    ///   <see cref="SpiritRootEarthController.OnSkillHit"/> 主动消费层数
    ///
    /// 视觉：敌人脚下 1 个旋转土黄圆盘 + N 个由外向内的细圈，整体随层数变化重建
    /// （CaveVfx.SpawnEarthSigil 提供）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EarthSigil : MonoBehaviour
    {
        public const int MaxStacks = 5;
        public const float LifetimeBase = 4f;
        public const float LifetimeRooted = 6f;

        public int CurrentStacks { get; private set; }
        private float _expireTimer;
        private float _lifetime;
        private GameObject _diskVfx;

        // ============================== 静态接口 ==============================

        public static EarthSigil AddStacks(GameObject target, int delta, bool rooted)
        {
            if (target == null || delta <= 0) return null;
            // 已死敌人不再挂
            var dmg = target.GetComponent<IDamageable>();
            if (dmg != null && dmg.Stats != null && !dmg.Stats.IsAlive) return null;

            var sigil = target.GetComponent<EarthSigil>();
            if (sigil == null) sigil = target.AddComponent<EarthSigil>();
            sigil._lifetime = rooted ? LifetimeRooted : LifetimeBase;
            sigil._expireTimer = sigil._lifetime;
            sigil.CurrentStacks = Mathf.Min(MaxStacks, sigil.CurrentStacks + delta);
            sigil.RebuildVfx();
            return sigil;
        }

        public static int GetStacks(GameObject target)
        {
            if (target == null) return 0;
            var s = target.GetComponent<EarthSigil>();
            return s != null ? s.CurrentStacks : 0;
        }

        public static void ClearStacks(GameObject target)
        {
            if (target == null) return;
            var s = target.GetComponent<EarthSigil>();
            if (s == null) return;
            s.CurrentStacks = 0;
            s.ClearVfx();
            Destroy(s);
        }

        // ============================== 生命周期 ==============================

        private void Update()
        {
            if (CurrentStacks <= 0)
            {
                ClearVfx();
                Destroy(this);
                return;
            }
            _expireTimer -= Time.deltaTime;
            if (_expireTimer <= 0f)
            {
                CurrentStacks = Mathf.Max(0, CurrentStacks - 1);
                _expireTimer = _lifetime;
                if (CurrentStacks <= 0)
                {
                    ClearVfx();
                    Destroy(this);
                }
                else RebuildVfx();
            }
        }

        private void OnDestroy() => ClearVfx();

        // ============================== 视觉 ==============================

        private void RebuildVfx()
        {
            ClearVfx();
            Color earthColor = FxFactory.ElementColor(ElementTag.Earth);
            // 圆盘半径稍微比 SigilDetonateRadius 小一点，避免视觉过大
            _diskVfx = CaveVfx.SpawnEarthSigil(transform, Vector3.zero, 1.2f,
                earthColor, CurrentStacks);
        }

        private void ClearVfx()
        {
            if (_diskVfx != null)
            {
                Destroy(_diskVfx);
                _diskVfx = null;
            }
        }
    }
}
