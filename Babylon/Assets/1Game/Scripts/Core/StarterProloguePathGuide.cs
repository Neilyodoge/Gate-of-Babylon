using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 出生点到救援空地的三点自然微光。不可交互，初契后自动消失。
    /// </summary>
    public sealed class StarterProloguePathGuide : MonoBehaviour
    {
        private readonly List<Transform> _lights = new();
        private readonly List<Vector3> _basePositions = new();

        public int LightCount => _lights.Count;

        private void Update()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.StarterChosen)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                Transform light = _lights[i];
                if (light == null)
                    continue;
                float phase = Time.time * 2.2f + i * 1.7f;
                light.position =
                    _basePositions[i] +
                    Vector3.up * (Mathf.Sin(phase) * 0.12f);
                float scale = 0.18f +
                              Mathf.Sin(phase * 1.25f) * 0.035f;
                light.localScale = Vector3.one * scale;
            }
        }

        private void Configure(Vector3 start, Vector3 destination)
        {
            Vector3[] positions =
                BuildGuidePoints(start, destination);
            Material material =
                new(MaterialHelper.GetLitShader());
            Color color = new(0.45f, 0.95f, 0.78f);
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.3f);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject orb = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                orb.name = $"PathFirefly_{i + 1}";
                orb.transform.SetParent(transform, true);
                orb.transform.position = positions[i];
                orb.transform.localScale = Vector3.one * 0.18f;
                Collider collider = orb.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                Renderer renderer = orb.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;
                _lights.Add(orb.transform);
                _basePositions.Add(positions[i]);
            }
        }

        public static Vector3[] BuildGuidePoints(
            Vector3 start,
            Vector3 destination)
        {
            Vector3 direction = destination - start;
            direction.y = 0f;
            Vector3 right = direction.sqrMagnitude > 0.01f
                ? Vector3.Cross(Vector3.up, direction.normalized)
                : Vector3.right;
            float[] progress = { 0.28f, 0.52f, 0.76f };
            float[] offsets = { -0.65f, 0.55f, -0.35f };
            var result = new Vector3[3];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Vector3.Lerp(
                    start,
                    destination,
                    progress[i]);
                result[i] += right * offsets[i] + Vector3.up * 0.65f;
            }
            return result;
        }

        public static StarterProloguePathGuide EnsureBetween(
            Transform start,
            Transform destination)
        {
            if (start == null || destination == null)
                return null;
            StarterProloguePathGuide existing =
                FindFirstObjectByType<StarterProloguePathGuide>();
            if (existing != null)
                return existing;
            StarterProloguePathGuide guide =
                new GameObject("StarterProloguePathGuide")
                    .AddComponent<StarterProloguePathGuide>();
            guide.Configure(start.position, destination.position);
            return guide;
        }
    }
}
