using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 层间过渡动画 —— 传送门效果
    /// 通关后显示传送门，玩家靠近确认用途并按 F 后过渡。
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
        public void SpawnPortal(
            Vector3 position,
            System.Action onEnter,
            string title = "继续探索",
            string purpose = "进入下一房间")
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
            bc.size = new Vector3(4.5f, 3f, 4.5f);
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trigger = triggerGo.AddComponent<PortalTrigger>();
            trigger.Initialize(() =>
            {
                if (!_isTransitioning)
                    StartCoroutine(TransitionCoroutine(onEnter));
            }, title, purpose, glowColor);

            // 门面板 + 发光球呼吸动画
            StartCoroutine(PortalGlowAnimation(portalFace, topOrb, glowColor));

            Debug.Log($"<color=cyan>★ {title}已生成在 {position}，靠近查看用途并按 F 进入 ★</color>");
        }

        /// <summary>白昼 Boss 后生成两个明确出口：保留 Build 追入永夜，或结算后返回基地。</summary>
        public void SpawnPhaseChoicePortals(
            Vector3 position,
            System.Action onContinueNight,
            System.Action onReturnVillage)
        {
            if (_portal != null) Destroy(_portal);

            _portal = new GameObject("Portal_PhaseChoice");
            _portal.transform.position = position;
            BuildChoicePortal(
                _portal.transform,
                new Vector3(-2.4f, 0f, 0f),
                "追入永夜",
                "保留当前构筑，重新降落到永夜",
                new Color(0.2f, 0.85f, 1f),
                onContinueNight);
            BuildChoicePortal(
                _portal.transform,
                new Vector3(2.4f, 0f, 0f),
                "返回基地",
                "结算白昼阶段，下次从永夜继续",
                new Color(1f, 0.68f, 0.24f),
                onReturnVillage);
            Debug.Log("<color=#66ddff>[无暮王城] 白昼出口已生成：追入永夜 / 返回基地。</color>");
        }

        private void BuildChoicePortal(
            Transform parent,
            Vector3 localPosition,
            string label,
            string purpose,
            Color color,
            System.Action onEnter)
        {
            var root = new GameObject(label);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            CreateChoicePart(root.transform, "左门柱",
                new Vector3(-0.85f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 0.3f), color);
            CreateChoicePart(root.transform, "右门柱",
                new Vector3(0.85f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 0.3f), color);
            CreateChoicePart(root.transform, "门楣",
                new Vector3(0f, 2.75f, 0f), new Vector3(1.95f, 0.25f, 0.35f), color);
            CreateChoicePart(root.transform, "门面",
                new Vector3(0f, 1.35f, 0f), new Vector3(1.45f, 2.4f, 0.08f), color * 0.7f);

            var textGo = new GameObject("出口名称");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 3.35f, 0f);
            textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = textGo.AddComponent<TextMeshPro>();
            text.text = label;
            text.fontSize = 3f;
            if (UGuiKit.CjkFont != null) text.font = UGuiKit.CjkFont;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.rectTransform.sizeDelta = new Vector2(5f, 1f);

            var triggerGo = new GameObject("PortalTrigger");
            triggerGo.transform.SetParent(root.transform, false);
            triggerGo.transform.localPosition = new Vector3(0f, 1.2f, -0.4f);
            var collider = triggerGo.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(3.6f, 3f, 3.6f);
            var body = triggerGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            triggerGo.AddComponent<PortalTrigger>().Initialize(() =>
            {
                if (!_isTransitioning)
                    StartCoroutine(TransitionCoroutine(onEnter));
            }, label, purpose, color);
        }

        private void CreateChoicePart(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            SetGlowMaterial(part, color * 0.45f, color * 4f);
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
            _portal = null;
        }
    }

    /// <summary>传送门近距离确认：显示用途，按 F 后才执行过渡。</summary>
    public class PortalTrigger : MonoBehaviour, IInteractable
    {
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 10;
        public bool IsInteractionAvailable => _playerInRange && !_triggered;
        public bool IsRoutedActive { get; set; }

        private System.Action _onConfirm;
        private bool _triggered;
        private bool _playerInRange;
        private NpcHeadCard _headCard;

        public void Initialize(
            System.Action onConfirm,
            string title,
            string purpose,
            Color themeColor)
        {
            _onConfirm = onConfirm;
            _triggered = false;
            _headCard = NpcHeadCard.Attach(transform.parent, new NpcHeadCard.Config
            {
                displayName = title,
                icon = "◇",
                roleSub = purpose,
                hintText = "按 [F] 确认进入",
                themeColor = themeColor,
                yOffset = 4.1f,
                showLongRangeMarker = false,
            });
            _headCard.SetCardVisible(false);
            _headCard.SetHintVisible(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            SetPlayerInRange(other, true);
        }

        private void OnTriggerStay(Collider other)
        {
            SetPlayerInRange(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            SetPlayerInRange(other, false);
        }

        private void Update()
        {
            bool showPrompt = IsRoutedActive && !_triggered;
            _headCard?.SetCardVisible(showPrompt);
            _headCard?.SetHintVisible(showPrompt);
            if (!showPrompt)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                _triggered = true;
                _playerInRange = false;
                InteractionRouter.Unregister(this);
                _headCard?.SetCardVisible(false);
                _headCard?.SetHintVisible(false);
                Debug.Log("<color=green>★ 玩家确认进入传送门！★</color>");
                _onConfirm?.Invoke();
            }
        }

        private void SetPlayerInRange(Collider other, bool inRange)
        {
            if (other == null
                || (!other.CompareTag("Player")
                    && other.GetComponent<PlayerController>() == null))
                return;

            _playerInRange = inRange;
            if (inRange)
                InteractionRouter.Register(this);
            else
                InteractionRouter.Unregister(this);
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
        }
    }
}
