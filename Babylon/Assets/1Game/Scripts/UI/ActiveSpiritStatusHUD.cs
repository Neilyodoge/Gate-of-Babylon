using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>左上角紧凑出战灵宠列，只显示头像与框体状态。</summary>
    public sealed class ActiveSpiritStatusHUD : MonoBehaviour
    {
        private RectTransform _root;

        public void Configure(RectTransform root)
        {
            _root = root;
        }

        private void Start()
        {
            GameEvents.Subscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnAttachmentChanged);
            Rebuild();
        }

        private void OnAttachmentChanged(
            GameEvents.StarterSpiritAttachmentChanged evt)
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (_root == null)
                return;
            for (int i = _root.childCount - 1; i >= 0; i--)
                Destroy(_root.GetChild(i).gameObject);

            ProjectRUITheme theme = ProjectRUITheme.Instance;
            SaveDataV1 save = SaveSystem.Instance.Data;
            if (theme == null ||
                save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null)
            {
                return;
            }

            var roster = new Dictionary<Guid, SpiritInstanceSave>();
            foreach (SpiritInstanceSave entry in save.spiritRoster)
            {
                if (entry != null &&
                    Guid.TryParse(entry.instanceGuid, out Guid id))
                {
                    roster[id] = entry;
                }
            }

            int visualIndex = 0;
            foreach (string guidText in save.activeSpiritInstanceGuids)
            {
                if (visualIndex >= 3 ||
                    !Guid.TryParse(guidText, out Guid id) ||
                    !roster.TryGetValue(id, out SpiritInstanceSave entry) ||
                    !SpiritSaveMapper.TryRestore(
                        entry,
                        out SpiritInstanceState spirit))
                {
                    continue;
                }

                CreateEntry(
                    visualIndex++,
                    theme,
                    spirit.Identity.InstanceId,
                    spirit.Identity.SpeciesConfigId);
            }
        }

        private void CreateEntry(
            int index,
            ProjectRUITheme theme,
            Guid spiritInstanceId,
            StableConfigId species)
        {
            var frameGo = new GameObject($"ActiveSpirit_{index}");
            frameGo.transform.SetParent(_root, false);
            var frameRect = frameGo.AddComponent<RectTransform>();
            frameRect.anchorMin = frameRect.anchorMax =
                new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition =
                new Vector2(0f, -index * 66f);
            frameRect.sizeDelta = new Vector2(60f, 64f);
            var frame = frameGo.AddComponent<Image>();
            frame.sprite = theme.SpiritFrame;
            frame.preserveAspect = true;
            frame.raycastTarget = true;
            var button = frameGo.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() =>
                SpiritTalentChoiceHUD.TryOpenTree(spiritInstanceId));

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(frameGo.transform, false);
            var portraitRect =
                portraitGo.AddComponent<RectTransform>();
            portraitRect.anchorMin = portraitRect.anchorMax =
                new Vector2(0.5f, 0.58f);
            portraitRect.sizeDelta = new Vector2(35f, 35f);
            var portrait = portraitGo.AddComponent<Image>();
            portrait.sprite = theme.SpiritPortrait(species);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnAttachmentChanged);
        }
    }
}
