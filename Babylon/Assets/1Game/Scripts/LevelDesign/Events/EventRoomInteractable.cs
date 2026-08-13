using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>事件房临时交互物；正式模型只需替换 Visual 子物体。</summary>
    public sealed class EventRoomInteractable : MonoBehaviour
    {
        private static Material _emissiveMaterial;

        private Action _onInteract;
        private Transform _visual;
        private WorldPromptHandle _prompt;
        private Vector3 _basePosition;
        private bool _playerInRange;
        private bool _interacting;
        private bool _completed;

        public static EventRoomInteractable Create(
            Vector3 position,
            Transform parent,
            Action onInteract)
        {
            var root = new GameObject("事件交互物_临时");
            root.transform.SetParent(parent, true);
            root.transform.position = position + Vector3.up * 1.1f;

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.5f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObject.name = "Visual_可替换";
            visualObject.transform.SetParent(root.transform, false);
            visualObject.transform.localScale = Vector3.one * 0.85f;
            var visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);
            var renderer = visualObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetEmissiveMaterial();

            var interactable = root.AddComponent<EventRoomInteractable>();
            interactable._onInteract = onInteract;
            interactable._visual = visualObject.transform;
            interactable._basePosition = root.transform.position;
            return interactable;
        }

        public void SetAvailable(bool available)
        {
            if (_completed)
                return;
            gameObject.SetActive(available);
        }

        public void Complete()
        {
            if (_completed)
                return;
            _completed = true;
            HidePrompt();
            Destroy(gameObject);
        }

        private void Update()
        {
            float phase = Time.time * 2f;
            transform.position = _basePosition + Vector3.up * (Mathf.Sin(phase) * 0.18f);
            if (_visual != null)
                _visual.Rotate(0f, 75f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);
            if (_prompt?.root != null)
                _prompt.root.transform.position = transform.position + Vector3.up * 1.7f;

            var keyboard = Keyboard.current;
            if (_playerInRange
                && !_interacting
                && !_completed
                && keyboard != null
                && keyboard.fKey.wasPressedThisFrame)
            {
                _interacting = true;
                HidePrompt();
                _onInteract?.Invoke();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;
            _playerInRange = true;
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;
            _playerInRange = false;
            HidePrompt();
        }

        private void ShowPrompt()
        {
            if (_prompt != null || _interacting || _completed)
                return;

            _prompt = WorldPromptPanel.Build(
                transform.position + Vector3.up * 1.7f,
                new PickupPromptData
                {
                    title = "事件交互物",
                    titleColor = new Color(0.2f, 0.95f, 1f),
                    subLine = "临时自发光占位",
                    subColor = new Color(0.55f, 0.9f, 1f),
                    desc = "调查后作出选择",
                    promptHint = "按 [F] 交互",
                });
        }

        private void HidePrompt()
        {
            if (_prompt?.root != null)
                Destroy(_prompt.root);
            _prompt = null;
        }

        private void OnDestroy()
        {
            HidePrompt();
        }

        private static Material GetEmissiveMaterial()
        {
            if (_emissiveMaterial != null)
                return _emissiveMaterial;

            Shader shader = Resources.Load<Shader>("Shaders/EventInteractableAlwaysVisible")
                ?? MaterialHelper.GetUnlitShader();
            Color debugColor = new Color(0.05f, 0.9f, 1f) * 2.5f;
            debugColor.a = 1f;

            _emissiveMaterial = new Material(shader)
            {
                name = "事件交互物_临时自发光_始终可见",
                color = debugColor,
                hideFlags = HideFlags.DontSave,
            };
            if (_emissiveMaterial.HasProperty("_BaseColor"))
                _emissiveMaterial.SetColor("_BaseColor", debugColor);
            return _emissiveMaterial;
        }
    }
}
