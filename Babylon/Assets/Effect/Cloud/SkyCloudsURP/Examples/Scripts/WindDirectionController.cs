using UnityEngine;

namespace Kamgam.SkyClouds
{
    public class WindDirectionController : MonoBehaviour
    {
        public Vector3 Direction => transform.TransformDirection(Vector3.forward);
        public float Sensitivity = 0.1f;
        public Material[] Materials;

        private Vector3 _lastMousePosition;
        private bool _isDragging = false;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.gameObject == gameObject)
                    {
                        _isDragging = true;
                        _lastMousePosition = Input.mousePosition;
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector3 deltaMousePosition = Input.mousePosition - _lastMousePosition;
                float rotationX = deltaMousePosition.y * Sensitivity; // Adjust sensitivity as needed
                float rotationY = -deltaMousePosition.x * Sensitivity; // Adjust sensitivity as needed

                Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
                transform.rotation = rotation * transform.rotation;

                _lastMousePosition = Input.mousePosition;
            }

            foreach (var material in Materials)
            {
                if (material == null)
                    continue;

                material.SetVector("_WindDirection", Direction);
            }
        }
    }
}
