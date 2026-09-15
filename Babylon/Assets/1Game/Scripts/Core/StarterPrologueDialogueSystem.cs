using System.Collections;
using UnityEngine;
using Yarn.Unity;

namespace XianTu
{
    /// <summary>
    /// 序章对Yarn Spinner的单一适配入口。
    /// 剧情脚本只依赖节点名，不直接持有第三方组件。
    /// </summary>
    public sealed class StarterPrologueDialogueSystem : MonoBehaviour
    {
        public const string ProjectResourcePath =
            "Dialogue/StarterPrologue";

        private static StarterPrologueDialogueSystem _instance;
        private DialogueRunner _runner;
        private YarnProject _project;

        public bool IsReady => _runner != null && _project != null;

        public static StarterPrologueDialogueSystem EnsureExists()
        {
            if (_instance != null)
                return _instance;
            StarterPrologueDialogueSystem existing =
                FindObjectOfType<StarterPrologueDialogueSystem>();
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }
            return new GameObject("StarterPrologueDialogueSystem")
                .AddComponent<StarterPrologueDialogueSystem>();
        }

        public static IEnumerator PlayNode(string nodeName)
        {
            StarterPrologueDialogueSystem system = EnsureExists();
            if (!system.IsReady)
            {
                Debug.LogError(
                    $"[序章对话] Yarn工程未就绪，跳过节点：{nodeName}");
                yield break;
            }
            yield return system.PlayNodeInternal(nodeName);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _project = Resources.Load<YarnProject>(
                ProjectResourcePath);
            if (_project == null)
            {
                Debug.LogError(
                    $"[序章对话] 未找到Resources/{ProjectResourcePath}.yarnproject");
                return;
            }

            StarterPrologueYarnPresenter presenter =
                gameObject.AddComponent<
                    StarterPrologueYarnPresenter>();
            _runner = gameObject.AddComponent<DialogueRunner>();
            _runner.autoStart = false;
            _runner.DialoguePresenters =
                new DialoguePresenterBase[] { presenter };
            _runner.SetProject(_project);
        }

        private IEnumerator PlayNodeInternal(string nodeName)
        {
            while (_runner.IsDialogueRunning)
                yield return null;
            yield return YarnTask.ToCoroutine(
                () => RunNodeAndWait(nodeName));
        }

        private async YarnTask RunNodeAndWait(string nodeName)
        {
            await _runner.StartDialogue(nodeName);
            await _runner.DialogueTask;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
