using UnityEngine;

namespace XianTu
{
    /// <summary>救援完成后显示在回家路径终点的轻量世界引导。</summary>
    public sealed class StarterPrologueReturnGuide : MonoBehaviour
    {
        private LineRenderer _ring;
        private LineRenderer _beam;

        private void Start()
        {
            BuildVisuals();
            RefreshVisibility();
        }

        private void Update()
        {
            RefreshVisibility();
            if (!_ring.enabled)
                return;
            float pulse =
                0.42f + Mathf.PingPong(Time.time * 0.35f, 0.28f);
            Color color = new(0.55f, 0.88f, 1f, pulse);
            _ring.startColor = color;
            _ring.endColor = color;
            _beam.startColor = color;
            _beam.endColor =
                new Color(color.r, color.g, color.b, 0.05f);
        }

        private void RefreshVisibility()
        {
            if (_ring == null || _beam == null)
                return;
            bool show = StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.RescueCompleted;
            _ring.enabled = show;
            _beam.enabled = show;
        }

        private void BuildVisuals()
        {
            Material material =
                new(Shader.Find("Sprites/Default"));
            _ring = CreateLine("ReturnRing", material, 48);
            _ring.loop = true;
            _ring.startWidth = 0.07f;
            _ring.endWidth = 0.07f;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                _ring.SetPosition(
                    i,
                    transform.position +
                    new Vector3(
                        Mathf.Cos(angle) * 1.15f,
                        0.08f,
                        Mathf.Sin(angle) * 1.15f));
            }

            _beam = CreateLine("ReturnBeam", material, 2);
            _beam.startWidth = 0.12f;
            _beam.endWidth = 0.02f;
            _beam.SetPosition(
                0,
                transform.position + Vector3.up * 0.1f);
            _beam.SetPosition(
                1,
                transform.position + Vector3.up * 2.4f);
        }

        private LineRenderer CreateLine(
            string lineName,
            Material material,
            int count)
        {
            GameObject child = new(lineName);
            child.transform.SetParent(transform, true);
            LineRenderer line =
                child.AddComponent<LineRenderer>();
            line.material = material;
            line.positionCount = count;
            line.useWorldSpace = true;
            return line;
        }

        public static void EnsureAt(Transform marker)
        {
            if (marker == null ||
                marker.GetComponent<StarterPrologueReturnGuide>() != null)
            {
                return;
            }
            marker.gameObject.AddComponent<
                StarterPrologueReturnGuide>();
        }
    }
}
