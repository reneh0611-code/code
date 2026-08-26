using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new(0.30f, 1.42f, 0f);
        [SerializeField, Min(1.5f)] private float distance = 2.35f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField, Min(1f)] private float followSmooth = 18f;
        [SerializeField, Min(1f)] private float rotationSmooth = 16f;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.20f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minimumDistance = 0.85f;

        private float _currentDistance;
        private float _yaw;
        private float _pitch = 8f;
        private bool _initialized;

        public float CurrentYaw => _yaw;

        private void Awake()
        {
            _currentDistance = distance;
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            InitializeAngles();
        }

        private void Update()
        {
            if (target == null || Mouse.current == null)
                return;

            InitializeAngles();

            Vector2 delta = Mouse.current.delta.ReadValue();
            _yaw += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        private void InitializeAngles()
        {
            if (_initialized) return;
            _yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            InitializeAngles();

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Quaternion lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 pivot = target.position + yawRotation * pivotOffset;
            Vector3 backward = -(lookRotation * Vector3.forward);

            float desiredDistance = distance;
            if (Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    backward,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(target) && hit.transform != target)
                    desiredDistance = Mathf.Max(minimumDistance, hit.distance - 0.08f);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 20f * Time.deltaTime);
            Vector3 desiredPosition = pivot + backward * _currentDistance;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSmooth * Time.deltaTime);
        }
    }
}
