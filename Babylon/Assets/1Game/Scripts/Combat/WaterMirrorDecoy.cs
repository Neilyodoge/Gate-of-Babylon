using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 水镜术 · 嘲讽分身。原地留下一个分身，存活期间所有敌人改为索敌/攻击它（吸引火力），到期消失。
    /// 敌人侧由 <see cref="EnemyBase"/> 读取 <see cref="ActiveTransform"/> 决定是否改打分身。
    /// </summary>
    public class WaterMirrorDecoy : MonoBehaviour
    {
        public static WaterMirrorDecoy Active { get; private set; }
        public static Transform ActiveTransform => Active != null ? Active.transform : null;

        private float _life;

        public static WaterMirrorDecoy Spawn(Vector3 pos, float duration)
        {
            if (Active != null) Destroy(Active.gameObject);

            var go = new GameObject("WaterMirrorDecoy");
            go.transform.position = pos;
            var d = go.AddComponent<WaterMirrorDecoy>();
            d._life = Mathf.Max(0.5f, duration);
            Active = d;

            // 视觉：半透明水蓝胶囊（无碰撞，仅作索敌点）
            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "DecoyVisual";
            vis.transform.SetParent(go.transform, false);
            var col = vis.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = vis.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.3f, 0.6f, 1f, 0.55f);
                rend.material = mat;
            }

            return d;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                if (Active == this) Active = null;
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
