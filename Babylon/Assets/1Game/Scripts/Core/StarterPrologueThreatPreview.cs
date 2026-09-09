using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 初契前远景中的失控宿主剪影。不可交互，只预告能量异常。
    /// </summary>
    public sealed class StarterPrologueThreatPreview : MonoBehaviour
    {
        private Material _energyMaterial;

        private void Update()
        {
            if (_energyMaterial == null)
                return;
            float pulse =
                1.4f + Mathf.PingPong(Time.time * 2.1f, 1.4f);
            _energyMaterial.SetColor(
                "_EmissionColor",
                new Color(0.18f, 0.58f, 1f) * pulse);
            transform.localRotation = Quaternion.Euler(
                0f,
                Mathf.Sin(Time.time * 1.4f) * 3f,
                0f);
        }

        public static StarterPrologueThreatPreview CreateAt(
            Vector3 position)
        {
            StarterPrologueThreatPreview existing =
                FindFirstObjectByType<
                    StarterPrologueThreatPreview>();
            if (existing != null)
                return existing;

            GameObject root =
                new("StarterPrologueThreatPreview");
            root.transform.position = position;

            GameObject body = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            body.name = "PreviewHostBody";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.95f;
            body.transform.localScale =
                new Vector3(0.72f, 0.95f, 0.72f);
            RemoveCollider(body);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material =
                    new Material(MaterialHelper.GetLitShader());
                bodyRenderer.material.color =
                    new Color(0.18f, 0.22f, 0.32f);
            }

            GameObject weapon = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            weapon.name = "PreviewFlowingEnergyWeapon";
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition =
                new Vector3(0.62f, 1f, 0.05f);
            weapon.transform.localRotation =
                Quaternion.Euler(0f, 0f, -18f);
            weapon.transform.localScale =
                new Vector3(0.12f, 1.15f, 0.12f);
            RemoveCollider(weapon);

            StarterPrologueThreatPreview preview =
                root.AddComponent<StarterPrologueThreatPreview>();
            Renderer weaponRenderer =
                weapon.GetComponent<Renderer>();
            if (weaponRenderer != null)
            {
                Color color = new(0.18f, 0.58f, 1f);
                preview._energyMaterial =
                    new Material(MaterialHelper.GetLitShader());
                preview._energyMaterial.color = color;
                preview._energyMaterial.EnableKeyword("_EMISSION");
                preview._energyMaterial.SetColor(
                    "_EmissionColor",
                    color * 2f);
                weaponRenderer.material =
                    preview._energyMaterial;
            }
            return preview;
        }

        public static void Remove()
        {
            StarterPrologueThreatPreview preview =
                FindFirstObjectByType<
                    StarterPrologueThreatPreview>();
            if (preview != null)
                Destroy(preview.gameObject);
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
    }
}
