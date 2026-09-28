using UnityEngine;

namespace Kamgam.SkyClouds
{
    /// <summary>
    /// Editor like camera controls for in-game camera.<br />
    /// Thanks to the group effort in the forum, see:
    /// https://forum.unity.com/threads/how-to-make-camera-move-in-a-way-similar-to-editor-scene.524645/#post-8302236
    /// </summary>
    using UnityEngine;
    public class CameraMovement : MonoBehaviour
    {
        [SerializeField] float navigationSpeed = 2f;
        [SerializeField] float shiftMultiplier = 2f;
        [SerializeField] float sensitivity = 0.15f;
        [SerializeField] float panSensitivity = 0.5f;
        [SerializeField] float mouseWheelZoomSpeed = 5f;
        [SerializeField] bool usePhysics = false;

        private Camera cam;
        private Vector3 anchorPoint;
        private Quaternion anchorRot;
        private bool isPanning;
        private Vector3 move;
        private Quaternion rotation;

        protected Rigidbody _rigidbody;
        public Rigidbody Rigidbody
        {
            get
            {
                if (_rigidbody == null)
                {
                    _rigidbody = this.GetComponent<Rigidbody>();
                }
                return _rigidbody;
            }
        }

        private bool shouldUsePhysics => usePhysics && Rigidbody != null;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            move = Vector3.zero;
            rotation = transform.rotation;
        }

        void Update()
        {
            move = Vector3.zero;
            rotation = transform.rotation;

            MousePanning();
            if (isPanning)
            { return; }

            if (Input.GetMouseButton(1))
            {
                float speed = navigationSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMultiplier : 1f) * 9.1f;
                if (Input.GetKey(KeyCode.W))
                    move += Vector3.forward * speed;
                if (Input.GetKey(KeyCode.S))
                    move -= Vector3.forward * speed;
                if (Input.GetKey(KeyCode.D))
                    move += Vector3.right * speed;
                if (Input.GetKey(KeyCode.A))
                    move -= Vector3.right * speed;
                if (Input.GetKey(KeyCode.E))
                    move += Vector3.up * speed;
                if (Input.GetKey(KeyCode.Q))
                    move -= Vector3.up * speed;

                if (!shouldUsePhysics)
                {
                    transform.Translate(move * Time.deltaTime);
                }
            }
            if (Input.GetMouseButtonDown(1))
            {
                anchorPoint = new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
                anchorRot = transform.rotation;
            }

            if (Input.GetMouseButton(1))
            {
                Quaternion rot = anchorRot;
                Vector3 dif = anchorPoint - new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
                rot.eulerAngles += dif * sensitivity;
                if (!shouldUsePhysics)
                    transform.rotation = rot;
                else
                    rotation = rot;
            }

            MouseWheeling();
        }

        private void FixedUpdate()
        {
            if (shouldUsePhysics)
            {
                Rigidbody.MovePosition(transform.position + transform.TransformVector(move * Time.fixedDeltaTime));
                Rigidbody.MoveRotation(rotation);
                Rigidbody.freezeRotation = true;
                Rigidbody.angularVelocity = Vector3.zero;
#if UNITY_6000_0_OR_NEWER
                Rigidbody.linearVelocity = Vector3.zero;
#else
                Rigidbody.velocity = Vector3.zero;
#endif
            }
        }

        //Zoom with mouse wheel
        void MouseWheeling()
        {
            float speed = 10 * (mouseWheelZoomSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMultiplier : 1f) * Time.deltaTime * 9.1f);

            Vector3 pos = transform.position;
            if (Input.GetAxis("Mouse ScrollWheel") < 0)
            {
                pos = pos - (transform.forward * speed);
                if (!shouldUsePhysics)
                    transform.position = pos;
                else
                    move = pos - transform.position;

            }
            if (Input.GetAxis("Mouse ScrollWheel") > 0)
            {
                pos = pos + (transform.forward * speed);
                if (!shouldUsePhysics)
                    transform.position = pos;
                else
                    move = pos - transform.position;
            }
        }


        private float pan_x;
        private float pan_y;
        private Vector3 panComplete;

        void MousePanning()
        {

            pan_x = -Input.GetAxis("Mouse X") * panSensitivity;
            pan_y = -Input.GetAxis("Mouse Y") * panSensitivity;
            panComplete = new Vector3(pan_x, pan_y, 0);

            if (Input.GetMouseButtonDown(2))
            {
                isPanning = true;
            }

            if (Input.GetMouseButtonUp(2))
            {
                isPanning = false;
            }

            if (isPanning)
            {
                if (!shouldUsePhysics)
                    transform.Translate(panComplete);
                else
                    move = panComplete;
            }
        }

    }

}