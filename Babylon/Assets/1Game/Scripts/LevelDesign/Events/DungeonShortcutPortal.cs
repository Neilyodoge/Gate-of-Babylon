using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>事件生成的关卡内快捷通道；只改变当前位置，不推进房间或阶段。</summary>
    public sealed class DungeonShortcutPortal : MonoBehaviour
    {
        private Vector3 _targetPosition;
        private string _title;
        private string _ruleKey;
        private bool _playerInRange;
        private WorldPromptHandle _prompt;

        public string RuleKey => _ruleKey;

        public static DungeonShortcutPortal Create(
            Vector3 position,
            Vector3 targetPosition,
            string title,
            Transform parent,
            string ruleKey = null)
        {
            var root = new GameObject($"__DungeonShortcut_{title}");
            root.transform.SetParent(parent, true);
            root.transform.position = position;

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "升降井传送台_临时";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.12f;
            visual.transform.localScale = new Vector3(1.5f, 0.12f, 1.5f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (Application.isPlaying)
                Destroy(visualCollider);
            else
                DestroyImmediate(visualCollider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(MaterialHelper.GetUnlitShader())
                {
                    color = new Color(0.22f, 0.55f, 1f),
                };
                renderer.material = material;
            }

            var portal = root.AddComponent<DungeonShortcutPortal>();
            portal._targetPosition = targetPosition;
            portal._title = title;
            portal._ruleKey = ruleKey;
            return portal;
        }

        private void Update()
        {
            if (!_playerInRange
                || Keyboard.current == null
                || !Keyboard.current.fKey.wasPressedThisFrame)
                return;
            GameManager.Instance?.TeleportWithinCurrentMap(_targetPosition);
            HidePrompt();
            _playerInRange = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
                return;
            _playerInRange = true;
            ShowPrompt();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_playerInRange
                && other.GetComponentInParent<PlayerController>() != null)
            {
                _playerInRange = true;
                ShowPrompt();
            }
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
            if (_prompt != null)
                return;
            _prompt = WorldPromptPanel.Build(
                transform.position + Vector3.up * 1.8f,
                new PickupPromptData
                {
                    title = _title,
                    titleColor = new Color(0.45f, 0.78f, 1f),
                    subLine = "狱城升降井",
                    subColor = new Color(0.55f, 0.72f, 0.92f),
                    desc = "关卡内快捷通道",
                    promptHint = "按 [F] 使用",
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
    }
}
