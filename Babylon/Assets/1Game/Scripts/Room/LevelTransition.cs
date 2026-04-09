using UnityEngine;
using System.Collections;

namespace XianTu
{
    /// <summary>
    /// 层间过渡动画 —— 传送门效果
    /// 通关后显示传送门，玩家走入后过渡到下一层
    /// </summary>
    public class LevelTransition : MonoBehaviour
    {
        public static LevelTransition Instance { get; private set; }

        private GameObject _portal;
        private GameObject _fadeOverlay;
        private Canvas _fadeCanvas;
        private UnityEngine.UI.Image _fadeImage;
        private bool _isTransitioning;

        private void Awake()
        {
            Instance = this;
            CreateFadeOverlay();
        }

        private void CreateFadeOverlay()
        {
            // 全屏淡入淡出遮罩
            _fadeOverlay = new GameObject("FadeOverlay");
            _fadeOverlay.transform.SetParent(transform);
            _fadeCanvas = _fadeOverlay.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 999;

            var imgGo = new GameObject("FadeImage");
            imgGo.transform.SetParent(_fadeOverlay.transform, false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _fadeImage = imgGo.AddComponent<UnityEngine.UI.Image>();
            _fadeImage.color = new Color(0, 0, 0, 0);
            _fadeImage.raycastTarget = false;
            _fadeOverlay.SetActive(false);
        }

        /// <summary>在指定位置创建传送门</summary>
        public void SpawnPortal(Vector3 position, System.Action onEnter)
        {
            if (_portal != null) Destroy(_portal);

            _portal = new GameObject("Portal");
            _portal.transform.position = position;

            // 传送门视觉：旋转的环形（用多个Cube组成）
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"PortalPiece_{i}";
                piece.transform.SetParent(_portal.transform);
                float rad = angle * Mathf.Deg2Rad;
                piece.transform.localPosition = new Vector3(Mathf.Cos(rad) * 1.5f, 1f + Mathf.Sin(rad * 2f) * 0.3f, Mathf.Sin(rad) * 1.5f);
                piece.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
                piece.transform.localRotation = Quaternion.Euler(0, angle, 0);

                var col = piece.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = piece.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.3f, 0.6f, 1f, 0.8f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 3f);
                    rend.material = mat;
                }
            }

            // 中心光柱
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "PortalPillar";
            pillar.transform.SetParent(_portal.transform);
            pillar.transform.localPosition = new Vector3(0, 2f, 0);
            pillar.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            var pillarCol = pillar.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);
            var pillarRend = pillar.GetComponent<Renderer>();
            if (pillarRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(0.4f, 0.7f, 1f, 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 1f) * 2f);
                pillarRend.material = mat;
            }

            // 触发器
            var triggerGo = new GameObject("PortalTrigger");
            triggerGo.transform.SetParent(_portal.transform);
            triggerGo.transform.localPosition = Vector3.zero;
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<PortalTrigger>();
            trigger.Initialize(() =>
            {
                if (!_isTransitioning)
                    StartCoroutine(TransitionCoroutine(onEnter));
            });

            // 旋转动画
            StartCoroutine(PortalRotation());
        }

        private IEnumerator PortalRotation()
        {
            while (_portal != null)
            {
                _portal.transform.Rotate(Vector3.up * 30f * Time.deltaTime, Space.World);
                // 上下浮动
                foreach (Transform child in _portal.transform)
                {
                    if (child.name.StartsWith("PortalPiece"))
                    {
                        var pos = child.localPosition;
                        pos.y = 1f + Mathf.Sin(Time.time * 2f + child.GetSiblingIndex()) * 0.2f;
                        child.localPosition = pos;
                    }
                }
                yield return null;
            }
        }

        private IEnumerator TransitionCoroutine(System.Action onEnter)
        {
            _isTransitioning = true;
            _fadeOverlay.SetActive(true);

            // 淡出（变黑）
            float fadeTime = 0.8f;
            float timer = 0;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = timer / fadeTime;
                _fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }
            _fadeImage.color = new Color(0, 0, 0, 1);

            // 销毁传送门
            if (_portal != null) Destroy(_portal);

            // 执行回调（切换房间）
            onEnter?.Invoke();

            yield return new WaitForSeconds(0.3f);

            // 淡入（变透明）
            timer = 0;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = 1f - (timer / fadeTime);
                _fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }
            _fadeImage.color = new Color(0, 0, 0, 0);
            _fadeOverlay.SetActive(false);
            _isTransitioning = false;
        }

        public void DestroyPortal()
        {
            if (_portal != null) Destroy(_portal);
        }
    }

    /// <summary>传送门触发器</summary>
    public class PortalTrigger : MonoBehaviour
    {
        private System.Action _onPlayerEnter;

        public void Initialize(System.Action onEnter)
        {
            _onPlayerEnter = onEnter;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _onPlayerEnter?.Invoke();
        }
    }
}
