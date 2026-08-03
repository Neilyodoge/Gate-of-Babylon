using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 局内模块背包——持有当前局内拾取的所有模块（未装配到链上的也在此）。
    /// 挂在 PlayerController 同一 GameObject 上。
    /// </summary>
    public class ModuleInventory : MonoBehaviour
    {
        private readonly List<ModuleDef> _modules = new();

        public IReadOnlyList<ModuleDef> Modules => _modules;

        public void Add(ModuleDef module)
        {
            if (module == null) return;
            _modules.Add(module);
            SaveSystem.Instance.UnlockModule(module);
            GameEvents.Publish(new GameEvents.ModulePickedUp { Module = module });
            Debug.Log($"<color=#00ffcc>模块入包：{module.displayName}（{module.category}）</color>");
        }

        public bool Remove(ModuleDef module)
        {
            return _modules.Remove(module);
        }

        /// <summary>获取可放入指定槽位的模块列表</summary>
        public List<ModuleDef> GetForSlot(int slotPosition)
        {
            var result = new List<ModuleDef>();
            foreach (var m in _modules)
            {
                if (m.CanFitSlot(slotPosition)) result.Add(m);
            }
            return result;
        }

        public List<ModuleDef> GetByCategory(ModuleCategory cat)
        {
            var result = new List<ModuleDef>();
            foreach (var m in _modules)
            {
                if (m.category == cat) result.Add(m);
            }
            return result;
        }

        public void Clear()
        {
            _modules.Clear();
        }
    }
}
