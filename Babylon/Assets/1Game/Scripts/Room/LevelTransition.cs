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

        /// <summary>立即销毁当前传送门（用于"渡劫失利强制撤离"等场景）</summary>
        public void RemovePortal()
        {
            if (_portal != null)
            {
                Destroy(_portal);
                _portal = null;
            }
        }

        /// <summary>在指定位置创建传送门（大型发光门，非常醒目）</summary>
        public void SpawnPortal(Vector3 position, System.Action onEnter)
        {
            if (_portal != null) Destroy(_portal);

            _portal = new GameObject("Portal_Door");
            _portal.transform.position = position;

            // 使用非常醒目的颜色
            Color frameColor = new Color(0.6f, 0.5f, 0.8f);   // 亮紫色门框
            Color glowColor = new Color(0.2f, 0.8f, 1f);       // 青蓝色发光

            // ===== 左门柱 =====
            var leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "DoorFrame_Left";
            leftPillar.transform.SetParent(_portal.transform);
            leftPillar.transform.localPosition = new Vector3(-1.2f, 1.5f, 0);
            leftPillar.transform.localScale = new Vector3(0.35f, 3f, 0.35f);
            var leftCol = leftPillar.GetComponent<Collider>();
            if (leftCol != null) Destroy(leftCol);
            SetGlowMaterial(leftPillar, frameColor, frameColor * 1.5f);

            // ===== 右门柱 =====
            var rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "DoorFrame_Right";
            rightPillar.transform.SetParent(_portal.transform);
            rightPillar.transform.localPosition = new Vector3(1.2f, 1.5f, 0);
            rightPillar.transform.localScale = new Vector3(0.35f, 3f, 0.35f);
            var rightCol = rightPillar.GetComponent<Collider>();
            if (rightCol != null) Destroy(rightCol);
            SetGlowMaterial(rightPillar, frameColor, frameColor * 1.5f);

            // ===== 门楁（横梁） =====
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "DoorFrame_Top";
            lintel.transform.SetParent(_portal.transform);
            lintel.transform.localPosition = new Vector3(0, 3.15f, 0);
            lintel.transform.localScale = new Vector3(2.8f, 0.3f, 0.4f);            var lintelCol = lintel.GetComponent<Collider>();
            if (lintelCol != null) Destroy(lintelCol);
            SetGlowMaterial(lintel, frameColor, frameColor * 1.5f);

            // ===== 门内发光面板 =====
            var portalFace = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portalFace.name = "PortalFace";
            portalFace.transform.SetParent(_portal.transform);
            portalFace.transform.localPosition = new Vector3(0, 1.5f, 0);
            portalFace.transform.localScale = new Vector3(2f, 2.8f, 0.1f);
            var faceCol = portalFace.GetComponent<Collider>();
            if (faceCol != null) Destroy(faceCol);
            SetGlowMaterial(portalFace, glowColor, glowColor * 5f);

            // ===== 门顶发光球 =====
            var topOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            topOrb.name = "PortalOrb";
            topOrb.transform.SetParent(_portal.transform);
            topOrb.transform.localPosition = new Vector3(0, 3.8f, 0);
            topOrb.transform.localScale = Vector3.one * 0.7f;
            var orbCol = topOrb.GetComponent<Collider>();
            if (orbCol != null) Destroy(orbCol);
            SetGlowMaterial(topOrb, glowColor, glowColor * 8f);

            // ===== 左柱顶球 =====
            var leftOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftOrb.name = "LeftOrb";
            leftOrb.transform.SetParent(_portal.transform);
            leftOrb.transform.localPosition = new Vector3(-1.2f, 3.2f, 0);
            leftOrb.transform.localScale = Vector3.one * 0.4f;
            var loCol = leftOrb.GetComponent<Collider>();
            if (loCol != null) Destroy(loCol);
            SetGlowMaterial(leftOrb, glowColor, glowColor * 6f);

            // ===== 右柱顶球 =====
            var rightOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightOrb.name = "RightOrb";
            rightOrb.transform.SetParent(_portal.transform);
            rightOrb.transform.localPosition = new Vector3(1.2f, 3.2f, 0);
            rightOrb.transform.localScale = Vector3.one * 0.4f;
            var roCol = rightOrb.GetComponent<Collider>();
            if (roCol != null) Destroy(roCol);
            SetGlowMaterial(rightOrb, glowColor, glowColor * 6f);

            // ===== 地面发光引导条 =====
            for (int i = 0; i < 3; i++)
            {
                var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
                guide.name = $"GuideStrip_{i}";
                guide.transform.SetParent(_portal.transform);
                guide.transform.localPosition = new Vector3(0, 0.06f, -(i + 1) * 1.5f);
                guide.transform.localScale = new Vector3(0.8f - i * 0.1f, 0.06f, 0.8f);
                var gCol = guide.GetComponent<Collider>();
                if (gCol != null) Destroy(gCol);
                float intensity = 3f - i * 0.4f;
                SetGlowMaterial(guide, glowColor * 0.7f, glowColor * intensity);
            }

            // ===== 触发器（Box形状，覆盖门洞区域） =====
            var triggerGo = new GameObject("PortalTrigger");
            triggerGo.transform.SetParent(_portal.transform);
            triggerGo.transform.localPosition = new Vector3(0, 1.2f, -0.5f);
            var bc = triggerGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(3f, 3f, 3f);
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<PortalTrigger>();
            trigger.Initialize(() =>
            {
                if (!_isTransitioning)
                    StartCoroutine(TransitionCoroutine(onEnter));
            });

            // 门面板 + 发光球呼吸动画
            StartCoroutine(PortalGlowAnimation(portalFace, topOrb, glowColor));

            Debug.Log($"<color=cyan>★ 传送门已生成在 {position}，走入即可进入下一层 ★</color>");
        }

        /// <summary>设置自发光材质（不透明，靠 Emission 发光）</summary>
        private void SetGlowMaterial(GameObject go, Color baseColor, Color emissionColor)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = baseColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                rend.material = mat;
            }
        }

        /// <summary>门面板和发光球呼吸动画</summary>
        private IEnumerator PortalGlowAnimation(GameObject portalFace, GameObject topOrb, Color baseColor)
        {
            while (_portal != null && portalFace != null)
            {
                float pulse = (Mathf.Sin(Time.time * 2.5f) + 1f) * 0.5f; // 0~1
                float emissionIntensity = Mathf.Lerp(3f, 7f, pulse);

                // 门面板呼吸
                var faceRend = portalFace.GetComponent<Renderer>();
                if (faceRend != null)
                {
                    faceRend.material.SetColor("_EmissionColor", baseColor * emissionIntensity);
                }

                // 顶部球呼吸（更强烈）
                if (topOrb != null)
                {
                    var orbRend = topOrb.GetComponent<Renderer>();
                    if (orbRend != null)
                    {
                        orbRend.material.SetColor("_EmissionColor", baseColor * (emissionIntensity * 1.5f));
                    }
                    // 球体上下浮动
                    var pos = topOrb.transform.localPosition;
                    pos.y = 3.8f + Mathf.Sin(Time.time * 1.5f) * 0.2f;
                    topOrb.transform.localPosition = pos;
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
        private bool _triggered;

        public void Initialize(System.Action onEnter)
        {
            _onPlayerEnter = onEnter;
            _triggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTrigger(other);
        }

        // 备用：如果 OnTriggerEnter 没触发（CharacterController 有时只触发 Stay）
        private void OnTriggerStay(Collider other)
        {
            TryTrigger(other);
        }

        private void TryTrigger(Collider other)
        {
            if (_triggered) return;
            // 同时支持 tag 检测和组件检测
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                _triggered = true;
                Debug.Log("<color=green>★ 玩家进入传送门！★</color>");
                _onPlayerEnter?.Invoke();
            }
        }
    }
}
