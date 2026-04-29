using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 全局 UI 资源缓存。
    /// 主要解决 <c>Resources.GetBuiltinResource&lt;Font&gt;("LegacyRuntime.ttf")</c> 在
    /// 高频 ShowPrompt（拾取物提示、伤害飘字）调用栈里被反复拉取的问题。
    /// </summary>
    public static class UIBuiltins
    {
        private static Font _legacyFont;

        /// <summary>Unity 内置 LegacyRuntime 字体（全局单例引用）。</summary>
        public static Font LegacyFont
        {
            get
            {
                if (_legacyFont == null)
                    _legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _legacyFont;
            }
        }
    }
}
