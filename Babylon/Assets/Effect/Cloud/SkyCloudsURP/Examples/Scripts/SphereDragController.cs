using UnityEngine;

namespace Kamgam.SkyClouds
{
    public class SphereDragController : MonoBehaviour
    {
        private Vector3 _initialPosition;
        private bool _isDragging = false;
        private Vector3 _offset;

        void Start()
        {
            _initialPosition = transform.position;
        }

        void Update()
        {
            // Start dragging
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray);

                foreach (RaycastHit hit in hits)
                {
                    // Check if collider is child of this.
                    if (hit.collider.transform == transform)
                    {
                        _isDragging = true;
                        _offset = transform.position - hit.point;
                        break;
                    }
                }
            }

            // Stop dragging
            if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            // Dragging
            if (_isDragging)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane dragPlane = new Plane(Vector3.up, _initialPosition);

                if (dragPlane.Raycast(ray, out float distance))
                {
                    Vector3 hitPoint = ray.GetPoint(distance) + _offset;
                    transform.position = new Vector3(hitPoint.x, _initialPosition.y, hitPoint.z);
                }
            }
        }
    }
}