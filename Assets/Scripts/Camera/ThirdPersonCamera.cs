using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new(0f, 1.55f, 0f);
        [SerializeField, Min(1.5f)] private float distance = 4.6f;
        [SerializeField, Min(1f)] private float mouseSensitivity = 10f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 68f;
        [SerializeField, Min(1f)] private float positionSmooth = 22f;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minDistance = 1.35f;
        [SerializeField] private float maxDistance = 7f;
        [SerializeField] private float zoomSpeed = 0.65f;

        private float _yaw;
        private float _pitch = 12f;
        private float _currentDistance;
        private bool _cursorLocked;
        private bool _didInitialGameplayLock;

        public float Yaw => _yaw;

        private void Awake()
        {
            _currentDistance = distance;
        }

        private void OnEnable()
        {
            LockCursor(false);
            _didInitialGameplayLock = false;
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
                _yaw = target.eulerAngles.y;
        }

        private void Update()
        {
            bool networkRunning = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (!networkRunning)
            {
                if (_cursorLocked)
                    LockCursor(false);
                return;
            }

            if (!_didInitialGameplayLock)
            {
                _didInitialGameplayLock = true;
                LockCursor(true);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);

            if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                LockCursor(true);

            if (Mouse.current != null && _cursorLocked)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * mouseSensitivity * 0.02f;
                _pitch -= delta.y * mouseSensitivity * 0.02f;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomSpeed, minDistance, maxDistance);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 backward = -(rotation * Vector3.forward);

            float targetDistance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, backward, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                targetDistance = Mathf.Max(minDistance, hit.distance - 0.15f);

            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, 18f * Time.deltaTime);
            Vector3 desired = pivot + backward * _currentDistance;

            transform.position = Vector3.Lerp(transform.position, desired, positionSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 25f * Time.deltaTime);
        }

        private void LockCursor(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
