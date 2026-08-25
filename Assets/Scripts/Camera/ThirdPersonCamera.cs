using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new(0f, 1.6f, 0f);
        [SerializeField] private float distance = 4f;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float positionSmooth = 18f;

        private float _yaw;
        private float _pitch = 15f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
                _yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * sensitivity;
                _pitch -= delta.y * sensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 desired = pivot - rotation * Vector3.forward * distance;

            transform.position = Vector3.Lerp(transform.position, desired, positionSmooth * Time.deltaTime);
            transform.rotation = rotation;
        }
    }
}
