using UnityEngine;
using TMPro;

namespace XianTu
{
    /// <summary>序章事故现场中照料员的倒地惊慌与恢复表现。</summary>
    public sealed class StarterPrologueCaretakerFear : MonoBehaviour
    {
        private const string VisualResourcePath =
            "Characters/StarterPrologueCaretaker_Druid";

        private Quaternion _baseRotation;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private Animator _animator;
        private TextMeshPro _fearIndicator;
        private bool _usesAnimatedVisual;

        private void Awake()
        {
            _baseRotation = transform.localRotation;
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;
            EnsureVisual();
            ApplyPose();
        }

        private void LateUpdate()
        {
            if (_fearIndicator == null ||
                !_fearIndicator.gameObject.activeSelf)
            {
                return;
            }
            if (Camera.main != null)
            {
                _fearIndicator.transform.rotation =
                    Camera.main.transform.rotation;
            }
            float pulse =
                1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.12f;
            _fearIndicator.transform.localScale =
                Vector3.one * (0.22f * pulse);
        }

        public void Calm()
        {
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            transform.localScale = _baseScale;
            if (_animator != null)
                _animator.Play("Calm", 0, 0f);
            if (_fearIndicator != null)
                _fearIndicator.gameObject.SetActive(false);
        }

        public void BeginExit()
        {
            Calm();
            if (_animator != null)
                _animator.Play("Run", 0, 0f);
        }

        private void ApplyPose()
        {
            transform.localPosition =
                _basePosition +
                Vector3.down * (_usesAnimatedVisual ? 0.18f : 0.48f);
            transform.localRotation =
                _baseRotation *
                (_usesAnimatedVisual
                    ? Quaternion.identity
                    : Quaternion.Euler(10f, 0f, 76f));
            transform.localScale = new Vector3(
                _baseScale.x,
                _baseScale.y *
                (_usesAnimatedVisual ? 1f : 0.88f),
                _baseScale.z);
        }

        private void EnsureVisual()
        {
            Transform existing = transform.Find("CaretakerVisual");
            GameObject visual = existing != null
                ? existing.gameObject
                : null;
            if (visual == null)
            {
                GameObject prefab =
                    Resources.Load<GameObject>(VisualResourcePath);
                if (prefab != null)
                {
                    visual = Instantiate(prefab, transform);
                    visual.name = "CaretakerVisual";
                    visual.transform.SetLocalPositionAndRotation(
                        new Vector3(0f, -1f, 0f),
                        Quaternion.identity);
                    visual.transform.localScale =
                        new Vector3(1.43f, 1f, 1.43f);
                }
            }

            if (visual == null)
                return;

            MeshRenderer placeholder =
                GetComponent<MeshRenderer>();
            if (placeholder != null)
                placeholder.enabled = false;
            Collider placeholderCollider = GetComponent<Collider>();
            if (placeholderCollider != null)
                placeholderCollider.enabled = false;

            _animator = visual.GetComponentInChildren<Animator>(true);
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.Play("Fear", 0, 0f);
                _usesAnimatedVisual = true;
            }
            DynamicCharacterRendering.Apply(visual);
            CreateFearIndicator();
        }

        private void CreateFearIndicator()
        {
            Transform existing =
                transform.Find("FearIndicator");
            if (existing != null)
            {
                _fearIndicator =
                    existing.GetComponent<TextMeshPro>();
                return;
            }

            GameObject indicator = new("FearIndicator");
            indicator.transform.SetParent(transform, false);
            indicator.transform.localPosition =
                new Vector3(0f, 1.45f, 0f);
            _fearIndicator = indicator.AddComponent<TextMeshPro>();
            if (UGuiKit.CjkFont != null)
                _fearIndicator.font = UGuiKit.CjkFont;
            _fearIndicator.text = "!";
            _fearIndicator.fontSize = 5.5f;
            _fearIndicator.fontStyle = FontStyles.Bold;
            _fearIndicator.alignment =
                TextAlignmentOptions.Center;
            _fearIndicator.color =
                new Color(1f, 0.36f, 0.12f, 1f);
            _fearIndicator.outlineColor =
                new Color32(40, 12, 4, 255);
            _fearIndicator.outlineWidth = 0.22f;
        }
    }
}
