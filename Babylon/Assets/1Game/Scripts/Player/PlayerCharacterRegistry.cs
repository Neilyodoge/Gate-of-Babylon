using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 主角档案注册表 —— 从 Resources/CharacterProfiles/ 加载所有 <see cref="PlayerCharacterProfile"/>，
    /// 并持有当前选择（静态字段，跨场景重载存活，与化身选择同生命周期）。
    ///
    /// 默认选择为 sortOrder 最小者（一般是剑客）。玩家在村庄「问道使」处改选后写入 <see cref="Selected"/>。
    /// 若项目尚未生成任何档案资产，<see cref="All"/> 为空、<see cref="Selected"/> 为 null，
    /// 此时 Demo1Setup 退回到旧的 Frank_Katana 序列化字段（向后兼容）。
    /// </summary>
    public static class PlayerCharacterRegistry
    {
        private static List<PlayerCharacterProfile> _profiles;
        private static PlayerCharacterProfile _selected;

        public static IReadOnlyList<PlayerCharacterProfile> All
        {
            get
            {
                EnsureLoaded();
                return _profiles;
            }
        }

        /// <summary>当前选择的主角档案（可能为 null，表示沿用默认/序列化模型）</summary>
        public static PlayerCharacterProfile Selected
        {
            get
            {
                EnsureLoaded();
                if (_selected == null && _profiles.Count > 0)
                    _selected = _profiles[0];
                return _selected;
            }
            set => _selected = value;
        }

        private static void EnsureLoaded()
        {
            if (_profiles != null) return;
            _profiles = new List<PlayerCharacterProfile>();
            var loaded = Resources.LoadAll<PlayerCharacterProfile>("CharacterProfiles");
            if (loaded != null)
                _profiles.AddRange(loaded);
            _profiles.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        }

        public static PlayerCharacterProfile FindById(string id)
        {
            EnsureLoaded();
            foreach (var p in _profiles)
                if (p != null && p.characterId == id) return p;
            return null;
        }

        /// <summary>测试 / 编辑器刷新用：清空缓存，下次访问重新加载。</summary>
        public static void Invalidate()
        {
            _profiles = null;
        }
    }
}
