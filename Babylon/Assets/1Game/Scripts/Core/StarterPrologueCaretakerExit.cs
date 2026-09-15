using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 战后对话完成后，照料员独自沿来路离开。
    /// 三只初契候选已在结契时退场，不在这里重新激活。
    /// </summary>
    public sealed class StarterPrologueCaretakerExit : MonoBehaviour
    {
        private const float Duration = 1.5f;
        private const float TravelDistance = 5f;

        private readonly List<Transform> _actors = new();
        private readonly List<Vector3> _startPositions = new();
        private readonly List<Vector3> _startScales = new();
        private Vector3 _travelDirection =
            new Vector3(1f, 0f, 0.4f).normalized;

        public int ActorCount => _actors.Count;
        public bool IsPlaying { get; private set; }

        private void Configure()
        {
            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null)
            {
                _travelDirection =
                    ResolveHomeDirection(caretaker.transform.position);
                caretaker.GetComponent<
                    StarterPrologueCaretakerFear>()?.BeginExit();
                AddActor(caretaker);
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
            Vector3 direction = _travelDirection;
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

        private static Vector3 ResolveHomeDirection(Vector3 origin)
        {
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind !=
                    StarterPrologueMarkerKind.HomeReturn)
                {
                    continue;
                }
                Vector3 direction =
                    marker.transform.position - origin;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                    return direction.normalized;
            }
            return new Vector3(1f, 0f, 0.4f).normalized;
        }

        public static StarterPrologueCaretakerExit Play()
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
            exit.Configure();
            return exit;
        }
    }
}
