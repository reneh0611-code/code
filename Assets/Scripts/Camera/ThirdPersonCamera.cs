using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Player Camera")]
        [SerializeField] private Vector3 pivotOffset = new(0.30f, 1.42f, 0f);
        [SerializeField, Min(1.5f)] private float distance = 2.35f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.14f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Vehicle Camera")]
        [SerializeField] private Vector3 vehiclePivotOffset = new(0f, 1.55f, 0f);
        [SerializeField, Min(2f)] private float vehicleDistance = 5.4f;
        [SerializeField] private float vehiclePitch = 10f;
        [SerializeField, Min(1f)] private float vehicleYawFollow = 10f;
        [SerializeField, Min(1f)] private float vehiclePositionFollow = 12f;

        [Header("Shared")]
        [SerializeField, Min(1f)] private float followSmooth = 18f;
        [SerializeField, Min(1f)] private float rotationSmooth = 16f;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.20f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minimumDistance = 0.85f;
        [SerializeField, Min(50f)] private float farClipDistance = 250f;

        private readonly RaycastHit[] _collisionHits = new RaycastHit[10];
        private float _currentDistance;
        private float _yaw;
        private float _pitch = 8f;
        private bool _initialized;
        private bool _vehicleMode;
        private Camera _camera;

        public float CurrentYaw => _yaw;
        public bool VehicleMode => _vehicleMode;

        private void Awake()
        {
            _currentDistance = distance;
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _camera.farClipPlane = Mathf.Min(farClipDistance, 220f);
                _camera.nearClipPlane = Mathf.Max(.05f, _camera.nearClipPlane);
                _camera.layerCullSpherical = true;
            }
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _initialized = false;
            InitializeAngles();
        }

        public void EnterVehicleMode(Transform vehicle)
        {
            target = vehicle;
            _vehicleMode = true;
            _yaw = vehicle != null ? vehicle.eulerAngles.y : _yaw;
            _pitch = vehiclePitch;
            _currentDistance = vehicleDistance;
            _initialized = true;
        }

        public void ExitVehicleMode(Transform player)
        {
            target = player;
            _vehicleMode = false;
            _yaw = player != null ? player.eulerAngles.y : _yaw;
            _pitch = 8f;
            _currentDistance = distance;
            _initialized = true;
        }

        private void Update()
        {
            if (target == null)
                return;

            InitializeAngles();

            if (_vehicleMode)
            {
                _yaw = Mathf.LerpAngle(_yaw, target.eulerAngles.y, 1f - Mathf.Exp(-vehicleYawFollow * Time.deltaTime));
                _pitch = vehiclePitch;
                return;
            }

            if (Mouse.current == null)
                return;

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

            Vector3 activePivotOffset = _vehicleMode ? vehiclePivotOffset : pivotOffset;
            float activeDistance = _vehicleMode ? vehicleDistance : distance;
            float activeFollow = _vehicleMode ? vehiclePositionFollow : followSmooth;

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Quaternion lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + yawRotation * activePivotOffset;
            Vector3 backward = -(lookRotation * Vector3.forward);

            float desiredDistance = activeDistance;
            int hitCount = Physics.SphereCastNonAlloc(pivot, collisionRadius, backward, _collisionHits, activeDistance, collisionMask, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _collisionHits[i];
                if (hit.collider == null || hit.transform == target || hit.transform.IsChildOf(target)) continue;
                if (hit.distance < nearestDistance) nearestDistance = hit.distance;
            }
            if (nearestDistance < float.MaxValue)
                desiredDistance = Mathf.Max(minimumDistance, nearestDistance - 0.08f);

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-20f * Time.deltaTime));
            Vector3 desiredPosition = pivot + backward * _currentDistance;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-activeFollow * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
        }
    }
}
