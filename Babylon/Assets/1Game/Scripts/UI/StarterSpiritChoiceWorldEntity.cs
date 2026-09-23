using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章中可点击的灵宠场景实体适配器。
    /// 三只候选直接显示场景模型，本组件提供身份、聚焦与离场反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterSpiritChoiceWorldEntity : MonoBehaviour
    {
        [SerializeField] private string speciesConfigId;
        [SerializeField, Range(1f, 1.25f)]
        private float focusedScale = 1.08f;
        [SerializeField] private GameObject containerVisual;
        [SerializeField] private GameObject spiritVisual;

        private Vector3 _baseScale;
        private bool _initialized;
        private Renderer[] _presentationRenderers =
            Array.Empty<Renderer>();
        private Renderer[] _spiritRenderers =
            Array.Empty<Renderer>();
        private Collider[] _interactionColliders =
            Array.Empty<Collider>();
        private Vector3 _storyPosition;
        private bool _trialPresentation;

        public static event Action<StarterSpiritChoiceWorldEntity>
            HoverEntered;
        public static event Action<StarterSpiritChoiceWorldEntity>
            HoverExited;
        public static event Action<StarterSpiritChoiceWorldEntity>
            Clicked;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(speciesConfigId);

        public StableConfigId SpeciesId =>
            IsConfigured
                ? new StableConfigId(speciesConfigId)
                : default;

        private void Awake()
        {
            CaptureBaseScale();
            EnsurePresentation();
            _interactionColliders = GetComponents<Collider>();
            HideForStory();
        }

        private void OnDisable()
        {
            SetFocused(false);
        }

        public void Configure(StableConfigId species)
        {
            if (!StarterSpiritChoice.IsOption(species))
                throw new ArgumentException(
                    "World entity must use a starter spirit species.",
                    nameof(species));
            speciesConfigId = species.Value;
            CaptureBaseScale();
        }

        public void ConfigurePresentation(
            GameObject container,
            GameObject spirit)
        {
            containerVisual = container;
            spiritVisual = spirit;
            CapturePresentationRenderers();
            HideForStory();
        }

        public void ShowContained()
        {
            EnsurePresentation();
            if (containerVisual != null)
                containerVisual.SetActive(false);
            SetRenderers(_spiritRenderers, true);
            SetColliders(true);
            transform.localScale = _baseScale;
        }

        public void HideForStory()
        {
            EnsurePresentation();
            if (containerVisual != null)
                containerVisual.SetActive(false);
            SetRenderers(_presentationRenderers, false);
            SetColliders(false);
            transform.localScale = _baseScale;
        }

        public void RevealForChoice(float delay)
        {
            gameObject.SetActive(true);
            StartCoroutine(RevealAfterDelay(delay));
        }

        public void HideImmediately()
        {
            SetRenderers(_presentationRenderers, false);
            SetColliders(false);
            gameObject.SetActive(false);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            EnsurePresentation();
            SetColliders(enabled);
        }

        public void RevealChosenAndHide()
        {
            if (containerVisual != null)
                containerVisual.SetActive(false);
            SetRenderers(_spiritRenderers, true);
            SetColliders(false);
            StartCoroutine(HideAfterReveal());
        }

        public void EnterTrialPresentation(Vector3 sharedPosition)
        {
            EnsurePresentation();
            _storyPosition = transform.position;
            transform.position = new Vector3(
                sharedPosition.x,
                _storyPosition.y,
                sharedPosition.z);
            _trialPresentation = true;
            SetRenderers(_presentationRenderers, false);
            SetColliders(false);
            transform.localScale = _baseScale;
        }

        public void SetTrialSelected(bool selected)
        {
            if (!_trialPresentation)
            {
                SetFocused(selected);
                return;
            }
            SetRenderers(_spiritRenderers, selected);
            SetColliders(false);
            SetFocused(selected);
        }

        public void ExitTrialPresentation()
        {
            if (!_trialPresentation)
                return;
            transform.position = _storyPosition;
            transform.localScale = _baseScale;
            _trialPresentation = false;
        }

        public void SetFocused(bool focused)
        {
            CaptureBaseScale();
            transform.localScale = focused
                ? _baseScale * focusedScale
                : _baseScale;
        }

        public void NotifyHoverEntered()
        {
            if (IsConfigured)
                HoverEntered?.Invoke(this);
        }

        public void NotifyHoverExited()
        {
            if (IsConfigured)
                HoverExited?.Invoke(this);
        }

        public void NotifyClicked()
        {
            if (IsConfigured)
                Clicked?.Invoke(this);
        }

        private void OnMouseEnter() => NotifyHoverEntered();
        private void OnMouseExit() => NotifyHoverExited();
        private void OnMouseDown() => NotifyClicked();

        private void CaptureBaseScale()
        {
            if (_initialized)
                return;
            _baseScale = transform.localScale;
            _initialized = true;
        }

        private void EnsurePresentation()
        {
            if (_presentationRenderers.Length == 0)
                CapturePresentationRenderers();
            if (_interactionColliders.Length == 0)
                _interactionColliders = GetComponents<Collider>();
        }

        private void CapturePresentationRenderers()
        {
            Renderer[] all =
                GetComponentsInChildren<Renderer>(true);
            var presentation = new List<Renderer>(all.Length);
            var childVisuals = new List<Renderer>(all.Length);
            foreach (Renderer renderer in all)
            {
                if (renderer == null ||
                    containerVisual != null &&
                    renderer.transform.IsChildOf(
                        containerVisual.transform))
                {
                    continue;
                }
                presentation.Add(renderer);
                if (spiritVisual != null)
                {
                    if (renderer.transform.IsChildOf(
                            spiritVisual.transform))
                    {
                        childVisuals.Add(renderer);
                    }
                }
                else if (renderer.transform != transform)
                {
                    childVisuals.Add(renderer);
                }
            }
            _presentationRenderers = presentation.ToArray();
            _spiritRenderers =
                childVisuals.Count > 0
                    ? childVisuals.ToArray()
                    : _presentationRenderers;
        }

        private GameObject CreateContainerVisual()
        {
            GameObject root = new("ContainerVisual");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.down * 0.4f;

            Color accent = ContainerColorFor(SpeciesId);
            GameObject shell = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            shell.name = "Shell";
            shell.transform.SetParent(root.transform, false);
            shell.transform.localScale = Vector3.one * 0.72f;
            SetContainerMaterial(shell, accent * 0.72f);
            RemoveCollider(shell);

            GameObject band = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            band.name = "SealBand";
            band.transform.SetParent(root.transform, false);
            band.transform.localScale =
                new Vector3(0.39f, 0.055f, 0.39f);
            SetContainerMaterial(
                band,
                new Color(0.08f, 0.1f, 0.12f));
            RemoveCollider(band);

            GameObject core = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            core.name = "ResponseCore";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition =
                new Vector3(0f, 0.02f, -0.37f);
            core.transform.localScale = Vector3.one * 0.13f;
            SetContainerMaterial(core, accent, true);
            RemoveCollider(core);
            return root;
        }

        private static Color ContainerColorFor(
            StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                return new Color(1f, 0.42f, 0.08f);
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return new Color(0.28f, 0.68f, 1f);
            }
            return new Color(0.38f, 0.92f, 0.62f);
        }

        private static void SetContainerMaterial(
            GameObject target,
            Color color,
            bool emission = false)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;
            Material material =
                new(MaterialHelper.GetLitShader());
            material.color = color;
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            renderer.material = material;
        }

        private static void SetRenderers(
            IEnumerable<Renderer> renderers,
            bool enabled)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        private void SetColliders(bool enabled)
        {
            foreach (Collider collider in _interactionColliders)
            {
                if (collider != null)
                    collider.enabled = enabled;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private IEnumerator HideAfterReveal()
        {
            yield return new WaitForSecondsRealtime(1.05f);
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            const float duration = 0.35f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private IEnumerator RevealAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            SetRenderers(_spiritRenderers, true);
            SetColliders(true);
            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(
                    0.2f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                transform.localScale = _baseScale * t;
                yield return null;
            }
            transform.localScale = _baseScale;
        }
    }
}
