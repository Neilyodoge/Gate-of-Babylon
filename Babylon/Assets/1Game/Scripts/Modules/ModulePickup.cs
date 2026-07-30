using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 模块拾取物——继承 PickupBase 的交互模式。
    /// F 轻按拾取入背包，F 长按分解（MVP 阶段分解直接销毁）。
    /// </summary>
    public class ModulePickup : PickupBase
    {
        public override int InteractionPriority => 22;

        private ModuleDef _moduleDef;
        private PlayerController _targetPlayer;
        private ModuleInventory _targetInventory;

        public ModuleDef ModuleDef => _moduleDef;

        public static ModulePickup Spawn(ModuleDef module, Vector3 position)
        {
            if (module == null) return null;
            if (!GameManager.EnableWorldDrops) return null; // #2：世界掉落总开关

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"[ModulePickup] {module.displayName}";
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            var oldCol = go.GetComponent<Collider>();
            if (oldCol != null) Destroy(oldCol);

            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.5f;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = CategoryColor(module.category);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", CategoryColor(module.category) * 0.5f);
                rend.material = mat;
            }

            var pickup = go.AddComponent<ModulePickup>();
            pickup._moduleDef = module;
            return pickup;
        }

        protected override void SetupVisual() { }

        protected override PickupPromptData BuildPromptData()
        {
            string catName = _moduleDef != null ? _moduleDef.category switch
            {
                ModuleCategory.Trigger => "触发器",
                ModuleCategory.Effect => "效果器",
                ModuleCategory.Modifier => "改造件",
                ModuleCategory.Universal => "万能件",
                _ => "???"
            } : "???";

            string modeName = _moduleDef != null
                ? (_moduleDef.executionMode == ExecutionMode.Active ? " ● 主动" : " ○ 被动")
                : "";

            string desc = _moduleDef != null
                ? (!string.IsNullOrEmpty(_moduleDef.uiDescription) ? _moduleDef.uiDescription : _moduleDef.description)
                : "";

            return new PickupPromptData
            {
                title = _moduleDef != null ? _moduleDef.displayName : "???",
                titleColor = CategoryColor(_moduleDef != null ? _moduleDef.category : ModuleCategory.Trigger),
                subLine = $"[{catName}]{modeName}",
                desc = desc,
                promptHint = "[F] 拾取  [长按F] 分解"
            };
        }

        protected override void OnPrimaryAction()
        {
            if (_moduleDef == null || _targetInventory == null) return;
            _targetInventory.Add(_moduleDef);
            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        protected override void OnDecomposeAction()
        {
            Debug.Log($"<color=grey>模块分解：{(_moduleDef != null ? _moduleDef.displayName : "???")}</color>");
            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        protected override bool AcquireTarget(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc == null) return false;
            _targetPlayer = pc;
            _targetInventory = pc.GetComponent<ModuleInventory>();
            return _targetInventory != null;
        }

        protected override bool HasTarget => _targetPlayer != null && _targetInventory != null;

        protected override void ReleaseTarget()
        {
            _targetPlayer = null;
            _targetInventory = null;
        }

        private static Color CategoryColor(ModuleCategory cat) => cat switch
        {
            ModuleCategory.Trigger   => new Color(0.2f, 0.7f, 1f),
            ModuleCategory.Effect    => new Color(1f, 0.4f, 0.2f),
            ModuleCategory.Modifier  => new Color(0.4f, 1f, 0.4f),
            ModuleCategory.Universal => new Color(1f, 0.8f, 0.2f),
            _ => Color.white
        };
    }
}
