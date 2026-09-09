using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 初契确认后的白盒退场：照料员带两只未选幼宠从来路离开。
    /// </summary>
    public sealed class StarterPrologueCaretakerExit : MonoBehaviour
    {
        private const float Duration = 1.5f;
        private const float TravelDistance = 5f;

        private readonly List<Transform> _actors = new();
        private readonly List<Vector3> _startPositions = new();
        private readonly List<Vector3> _startScales = new();

        public int ActorCount => _actors.Count;
        public bool IsPlaying { get; private set; }

        private void Configure(StableConfigId chosenSpecies)
        {
            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null)
                AddActor(caretaker);

            StarterSpiritChoiceWorldEntity[] spirits =
                FindObjectsByType<StarterSpiritChoiceWorldEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (StarterSpiritChoiceWorldEntity spirit in spirits)
            {
                bool chosen = spirit.SpeciesId == chosenSpecies;
                spirit.gameObject.SetActive(!chosen);
                if (!chosen)
                    AddActor(spirit.gameObject);
            }
            StartCoroutine(PlayExit());
        }

        private void AddActor(GameObject actor)
        {
            actor.SetActive(true);
            Collider[] colliders =
                actor.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
            _actors.Add(actor.transform);
            _startPositions.Add(actor.transform.position);
            _startScales.Add(actor.transform.localScale);
        }

        private IEnumerator PlayExit()
        {
            IsPlaying = true;
            Vector3 direction =
                new Vector3(-0.2f, 0f, -1f).normalized;
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                float scaleT = Mathf.InverseLerp(0.68f, 1f, t);
                for (int i = 0; i < _actors.Count; i++)
                {
                    Transform actor = _actors[i];
                    if (actor == null)
                        continue;
                    actor.position =
                        _startPositions[i] +
                        direction * (TravelDistance * t);
                    actor.localScale = Vector3.Lerp(
                        _startScales[i],
                        Vector3.zero,
                        scaleT);
                    actor.rotation = Quaternion.Slerp(
                        actor.rotation,
                        Quaternion.LookRotation(direction),
                        10f * Time.deltaTime);
                }
                yield return null;
            }

            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i] != null)
                    _actors[i].gameObject.SetActive(false);
            }
            IsPlaying = false;
            Destroy(gameObject);
        }

        public static StarterPrologueCaretakerExit Play(
            StableConfigId chosenSpecies)
        {
            StarterPrologueCaretakerExit existing =
                FindFirstObjectByType<
                    StarterPrologueCaretakerExit>();
            if (existing != null)
                Destroy(existing.gameObject);
            StarterPrologueCaretakerExit exit =
                new GameObject("StarterPrologueCaretakerExit")
                    .AddComponent<
                        StarterPrologueCaretakerExit>();
            exit.Configure(chosenSpecies);
            return exit;
        }
    }
}
