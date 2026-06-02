using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵脉道具拾取物（v0.5.5 · GDD 9.1.9 "灵脉道具"）—— 秘境专属掉落，给灵脉第二条获取路。
    ///
    /// 灵脉碎片 / 聚灵珠 / 地脉精华 / 洞天残核：玩家走近自动汲取（地脉之气被洞府灵脉牵引），
    /// 直接转为灵脉经验（<see cref="SpiritVeinSystem.InjectExp"/>），不进战斗背包、不需撤离带回。
    ///
    /// 与"历练值注入灵脉"互补：历练值是主动分配的纵向投资，灵脉道具是秘境里"捞到就赚"的即时收益。
    /// 仅在洞府 meta 启用（<see cref="FeatureFlags.EnableCaveMeta"/>）时生成。
    /// </summary>
    public class SpiritVeinPickup : MonoBehaviour
    {
        private int _amount;
        private string _displayName;
        private bool _collected;
        private const float PickupRadius = 1.6f;
        private float _spinT;

        /// <summary>生成一个灵脉道具拾取物。meta 关闭时不生成。</summary>
        public static SpiritVeinPickup Spawn(string displayName, int amount, Vector3 position)
        {
            if (!FeatureFlags.EnableCaveMeta) return null;
            if (amount <= 0) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"SpiritVeinPickup_{displayName}";
            Vector3 pos = position;
            pos.y = Mathf.Max(pos.y, 0f) + 0.6f;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.45f;
            go.layer = LayerMask.NameToLayer("Default");

            var col = go.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Color veinColor = new Color(0.4f, 0.9f, 0.7f);
                MaterialHelper.ApplyEmissiveColor(rend, veinColor, veinColor * 1.4f);
            }

            var pickup = go.AddComponent<SpiritVeinPickup>();
            pickup._amount = amount;
            pickup._displayName = displayName;
            return pickup;
        }

        private void Update()
        {
            if (_collected) return;

            // 缓慢自转 + 上下浮动，提示"可拾取的灵物"
            _spinT += Time.deltaTime;
            transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
            var p = PlayerController.Instance;
            if (p == null) return;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist <= PickupRadius)
                Absorb();
        }

        private void Absorb()
        {
            _collected = true;
            SpiritVeinSystem.Instance.InjectExp(_amount, _displayName);  // 内部已发 SpiritVeinGained 事件供 HUD 提示
            FxFactory.SpawnElementBurst(transform.position, ElementTag.Wood, 1.0f, 0.5f);
            Destroy(gameObject);
        }
    }
}
