using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 序章中可点击的灵宠场景实体适配器。
    /// 模型、动画与离场演出由场景Prefab持有，本组件只提供身份和聚焦反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterSpiritChoiceWorldEntity : MonoBehaviour
    {
        [SerializeField] private string speciesConfigId;
        [SerializeField, Range(1f, 1.25f)]
        private float focusedScale = 1.08f;

        private Vector3 _baseScale;
        private bool _initialized;

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
    }
}
