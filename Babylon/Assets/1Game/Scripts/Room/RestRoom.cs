using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 休息房间 —— 恢复生命值
    /// </summary>
    public class RestRoom : MonoBehaviour
    {
        private int _roomIndex;
        private GameObject _roomVisuals;
        private bool _healed;

        public float RoomWidth => 18f;
        public float RoomDepth => 18f;

        public void Initialize(int roomIndex)
        {
            _roomIndex = roomIndex;
            BuildRoom();
        }

        private void BuildRoom()
        {
            _roomVisuals = RoomBuilder.Build(transform, 18f, 18f, _roomIndex);

            // 中央灵泉（圆柱体 + 发光）
            var spring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spring.name = "SpiritSpring";
            spring.transform.SetParent(transform);
            spring.transform.localPosition = new Vector3(0, 0.15f, 0);
            spring.transform.localScale = new Vector3(3f, 0.15f, 3f);

            var springCol = spring.GetComponent<Collider>();
            if (springCol != null) Destroy(springCol);

            var springRend = spring.GetComponent<Renderer>();
            if (springRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.2f, 0.7f, 1f, 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 1f) * 2f);
                springRend.material = mat;
            }

            // 触发器
            var triggerGo = new GameObject("SpringTrigger");
            triggerGo.transform.SetParent(spring.transform);
            triggerGo.transform.localPosition = Vector3.zero;
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 8f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<SpringTrigger>();
            trigger.Initialize(this);

            // 提示文字
            var hintCanvas = new GameObject("HintCanvas");
            hintCanvas.transform.SetParent(spring.transform);
            hintCanvas.transform.localPosition = new Vector3(0, 3f, 0);
            var c = hintCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 0.5f);
            hintCanvas.transform.localScale = Vector3.one * 0.02f;

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(hintCanvas.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "灵泉 · 靠近恢复生命";
            text.fontSize = 18;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = new Color(0.5f, 0.9f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 出口触发器（房间北侧）
            CreateExitTrigger();
        }

        /// <summary>在房间北侧创建出口触发器</summary>
        private void CreateExitTrigger()
        {
            var exitGo = new GameObject("ExitTrigger");
            exitGo.transform.SetParent(transform);
            exitGo.transform.localPosition = new Vector3(0, 0, RoomDepth / 2f - 2f);

            var sc = exitGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;
            var rb = exitGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var exitTrigger = exitGo.AddComponent<RoomExitTrigger>();
            exitTrigger.Initialize(() =>
            {
                GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
            });

            // 出口视觉标记（发光柱）
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "ExitPillar";
            pillar.transform.SetParent(exitGo.transform);
            pillar.transform.localPosition = new Vector3(0, 1.5f, 0);
            pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            var pillarCol = pillar.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);
            var pillarRend = pillar.GetComponent<Renderer>();
            if (pillarRend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.5f);
                pillarRend.material = mat;
            }
        }

        public void HealPlayer()
        {
            if (_healed) return;
            _healed = true;

            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                float healAmount = stats.maxHp * 0.5f; // 恢复50%最大生命
                stats.Heal(healAmount);

                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = stats.currentHp,
                    MaxHp = stats.maxHp
                });

                Debug.Log($"<color=cyan>灵泉恢复了 {healAmount:F0} 点生命</color>");
            }
        }

        private void OnDestroy()
        {
            if (_roomVisuals != null) Destroy(_roomVisuals);
        }
    }

    /// <summary>灵泉触发器</summary>
    public class SpringTrigger : MonoBehaviour
    {
        private RestRoom _room;

        public void Initialize(RestRoom room)
        {
            _room = room;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _room?.HealPlayer();
        }
    }
}
