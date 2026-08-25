using UnityEngine;

namespace CheatOnYourDayOnes.CameraSystem
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new(0.42f, 1.52f, 0f);
        [SerializeField, Min(2f)] private float distance = 3.25f;
        [SerializeField] private float pitch = 10f;
        [SerializeField, Min(1f)] private float followSmooth = 16f;
        [SerializeField, Min(1f)] private float rotationSmooth = 14f;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.22f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float minimumDistance = 1.1f;

        private float _currentDistance;

        private void Awake()
        {
            _currentDistance = distance;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            Quaternion lookRotation = targetYaw * Quaternion.Euler(pitch, 0f, 0f);

            Vector3 pivot = target.position + targetYaw * pivotOffset;
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
                    desiredDistance = Mathf.Max(minimumDistance, hit.distance - 0.10f);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 18f * Time.deltaTime);
            Vector3 desiredPosition = pivot + backward * _currentDistance;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSmooth * Time.deltaTime);
        }
    }
}
