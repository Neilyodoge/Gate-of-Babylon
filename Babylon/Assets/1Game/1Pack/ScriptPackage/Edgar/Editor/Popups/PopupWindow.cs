using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Edgar.Unity.Editor
{
    public class PopupWindow : EditorWindow
    {
        private static PopupWindow LastWindow;

        private IPopup popup;
        private bool doNotShowAgain = false;

        private void OnGUI()
        {
            if (popup == null)
            {
                Close();
                return;
            }

            const int margin = 10;
            GUILayout.BeginVertical(new GUIStyle() {padding = new RectOffset(margin, margin, margin, margin)});
            GUILayout.Label(popup.Content, new GUIStyle(EditorStyles.label) {richText = true, wordWrap = true});
            GUILayout.FlexibleSpace();

            if (popup.Links != null && popup.Links.Count > 0)
            {
                GUILayout.Label("相关链接：");

                foreach (var link in popup.Links)
                {
                    if (GUILayout.Button(" - 链接：" + link.Text, GUI.skin.label))
                    {
                        Application.OpenURL(link.Url);
                    }

                    var lastRect = GUILayoutUtility.GetLastRect();
                    GUI.Label(lastRect, "   ___");
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("关闭"))
                Close();

            doNotShowAgain = GUILayout.Toggle(doNotShowAgain, "不再显示此弹窗。<size=8>（可通过“Edit/Edgar - 重新启用全部弹窗”恢复）</size>", new GUIStyle(EditorStyles.toggle) {richText = true});

            GUILayout.Space(10);

            GUILayout.Label("<b>！警告！</b>：<size=9>请勿直接在此示例场景中制作正式游戏。修改示例场景会增加后续升级插件的难度。可以把它当作试验场，但升级时通常建议删除整个插件目录，因此请预期这些修改可能丢失。</size>", new GUIStyle(EditorStyles.label) {richText = true, wordWrap = true});

            GUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            if (doNotShowAgain)
            {
                PopupManager.DisablePopup(popup);
            }
        }

        public static void Open(IPopup popup)
        {
            CloseLastPopup();

            var window = ScriptableObject.CreateInstance<PopupWindow>();
            var size = new Vector2(600, 330);
            window.minSize = size;
            window.maxSize = size;
            window.titleContent = new GUIContent($"Edgar - {popup.Title}");
            window.popup = popup;
            window.ShowUtility();

            LastWindow = window;
        }

        public static void CloseLastPopup()
        {
            if (LastWindow != null)
            {
                LastWindow.Close();
            }
        }
    }
}