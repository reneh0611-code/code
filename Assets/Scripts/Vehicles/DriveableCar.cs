using CheatOnYourDayOnes.CameraSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DriveableCar : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private Transform driverSeat;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Transform centerOfMass;

        [Header("Driving")]
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float reverseAcceleration = 8f;
        [SerializeField] private float coastDeceleration = 2.2f;
        [SerializeField] private float brakeDeceleration = 20f;
        [SerializeField] private float maxSpeedKmh = 135f;
        [SerializeField] private float maxReverseKmh = 30f;
        [SerializeField] private float steeringDegreesPerSecond = 92f;
        [SerializeField] private float highSpeedSteeringMultiplier = 0.30f;
        [SerializeField] private float lateralGrip = 10f;
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField] private float chassisGroundClearance = 0.10f;

        private Rigidbody _rb;
        private Transform _driver;
        private CharacterController _driverController;
        private Behaviour _networkController;
        private VehicleInteractor _interactor;
        private Renderer[] _driverRenderers;
        private bool[] _driverRendererStates;
        private Collider[] _driverColliders;
        private bool[] _driverColliderStates;
        private ThirdPersonCamera _camera;
        private BoxCollider _chassisCollider;
        private bool _occupied;
        private float _throttle;
        private float _steer;
        private bool _brake;
        private bool _ignoreExitUntilEReleased;
        private float _debugTimer;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            RebuildChassisCollider();
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = 1350f;
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.035f;
            _rb.angularDamping = 2.8f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 5f;
            _rb.centerOfMass = centerOfMass != null
                ? transform.InverseTransformPoint(centerOfMass.position)
                : new Vector3(0f, -0.42f, 0f);
        }

        private void Update()
        {
            if (!_occupied || Keyboard.current == null) return;

            _throttle = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
            _steer = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            _brake = Keyboard.current.spaceKey.isPressed;

            if (_ignoreExitUntilEReleased)
            {
                if (!Keyboard.current.eKey.isPressed) _ignoreExitUntilEReleased = false;
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Exit();
            }
        }

        private void FixedUpdate()
        {
            if (!_occupied) return;

            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            float forwardSpeed = localVelocity.z;
            float maxForwardMs = maxSpeedKmh / 3.6f;
            float maxReverseMs = maxReverseKmh / 3.6f;

            float targetForwardSpeed;
            if (_brake)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * Time.fixedDeltaTime);
            else if (_throttle > 0f)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, maxForwardMs, acceleration * Time.fixedDeltaTime);
            else if (_throttle < 0f)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverseMs, reverseAcceleration * Time.fixedDeltaTime);
            else
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * Time.fixedDeltaTime);

            // Strong low-speed traction: W must produce obvious motion immediately.
            if (_throttle > 0.1f && Mathf.Abs(forwardSpeed) < 1.5f)
                targetForwardSpeed = Mathf.Max(targetForwardSpeed, Mathf.MoveTowards(forwardSpeed, 3.2f, 18f * Time.fixedDeltaTime));
            else if (_throttle < -0.1f && Mathf.Abs(forwardSpeed) < 1.2f)
                targetForwardSpeed = Mathf.Min(targetForwardSpeed, Mathf.MoveTowards(forwardSpeed, -2.2f, 12f * Time.fixedDeltaTime));

            float targetLateral = Mathf.MoveTowards(localVelocity.x, 0f, lateralGrip * Time.fixedDeltaTime * Mathf.Max(1f, Mathf.Abs(localVelocity.x)));
            Vector3 worldVelocity = transform.TransformDirection(new Vector3(targetLateral, localVelocity.y, targetForwardSpeed));
            _rb.linearVelocity = worldVelocity;

            float speedAbs = Mathf.Abs(targetForwardSpeed);
            float steerAuthority = Mathf.Clamp01(speedAbs / 1.1f);
            float steeringFactor = Mathf.Lerp(1f, highSpeedSteeringMultiplier, Mathf.InverseLerp(0f, maxForwardMs, speedAbs));
            float reverseSign = targetForwardSpeed < -0.05f ? -1f : 1f;
            float yaw = _steer * steeringDegreesPerSecond * steeringFactor * steerAuthority * reverseSign * Time.fixedDeltaTime;
            if (Mathf.Abs(yaw) > 0.0001f)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            Vector3 angular = _rb.angularVelocity;
            angular.x *= 0.82f;
            angular.z *= 0.82f;
            _rb.angularVelocity = angular;

            _debugTimer += Time.fixedDeltaTime;
            if (_debugTimer >= 0.75f && Mathf.Abs(_throttle) > 0.1f)
            {
                _debugTimer = 0f;
                Debug.Log($"[CYDOY] CAR DRIVE throttle={_throttle:F1} localForward={forwardSpeed:F2}m/s target={targetForwardSpeed:F2}m/s worldSpeed={_rb.linearVelocity.magnitude:F2}m/s sleeping={_rb.IsSleeping()}", this);
            }
        }

        public float DistanceFrom(Vector3 worldPoint)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            float best = float.MaxValue;
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                float d = Vector3.Distance(worldPoint, c.ClosestPoint(worldPoint));
                if (d < best) best = d;
            }
            return best < float.MaxValue ? best : Vector3.Distance(worldPoint, transform.position);
        }

        public bool TryEnter(Transform player)
        {
            if (_occupied || player == null || DistanceFrom(player.position) > interactionDistance) return false;

            _driver = player;
            _driverController = player.GetComponent<CharacterController>();
            _networkController = player.GetComponent<CheatOnYourDayOnes.Player.NetworkPlayerController>();
            _interactor = player.GetComponent<VehicleInteractor>();

            if (_networkController != null) _networkController.enabled = false;
            if (_interactor != null) _interactor.enabled = false;

            _driverRenderers = player.GetComponentsInChildren<Renderer>(true);
            _driverRendererStates = new bool[_driverRenderers.Length];
            for (int i = 0; i < _driverRenderers.Length; i++)
            {
                _driverRendererStates[i] = _driverRenderers[i].enabled;
                _driverRenderers[i].enabled = false;
            }

            _driverColliders = player.GetComponentsInChildren<Collider>(true);
            _driverColliderStates = new bool[_driverColliders.Length];
            for (int i = 0; i < _driverColliders.Length; i++)
            {
                _driverColliderStates[i] = _driverColliders[i].enabled;
                _driverColliders[i].enabled = false;
            }
            if (_driverController != null) _driverController.enabled = false;

            Transform seat = driverSeat != null ? driverSeat : transform;
            player.SetParent(seat, false);
            player.localPosition = Vector3.zero;
            player.localRotation = Quaternion.identity;

            _camera = Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
            if (_camera != null) _camera.EnterVehicleMode(transform);

            ConfigureRigidbody();
            RebuildChassisCollider();
            SnapChassisAboveGround();
            _rb.WakeUp();
            _occupied = true;
            _ignoreExitUntilEReleased = true;

            Debug.Log($"[CYDOY] Vehicle active: {name} | kinematic={_rb.isKinematic} | mass={_rb.mass} | chassis={_chassisCollider.size} | pos={transform.position}", this);
            return true;
        }

        public void Exit()
        {
            if (!_occupied || _driver == null) return;

            Transform p = _driver;
            p.SetParent(null, true);
            p.position = exitPoint != null ? exitPoint.position : transform.position - transform.right * 1.8f + Vector3.up * 0.25f;
            p.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_camera != null) _camera.ExitVehicleMode(p);

            if (_driverRenderers != null)
                for (int i = 0; i < _driverRenderers.Length; i++) if (_driverRenderers[i] != null) _driverRenderers[i].enabled = _driverRendererStates[i];
            if (_driverColliders != null)
                for (int i = 0; i < _driverColliders.Length; i++) if (_driverColliders[i] != null) _driverColliders[i].enabled = _driverColliderStates[i];
            if (_driverController != null) _driverController.enabled = true;
            if (_networkController != null) _networkController.enabled = true;
            if (_interactor != null) _interactor.enabled = true;

            _driver = null;
            _driverController = null;
            _networkController = null;
            _interactor = null;
            _driverRenderers = null;
            _driverRendererStates = null;
            _driverColliders = null;
            _driverColliderStates = null;
            _camera = null;
            _occupied = false;
            _throttle = 0f;
            _steer = 0f;
            _brake = false;
        }

        private void RebuildChassisCollider()
        {
            // Disable imported colliders; many downloaded car assets ship with a collider that is
            // oversized, concave, or embedded in the road and can completely block forward motion.
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c == _chassisCollider || c.isTrigger) continue;
                c.enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            bool found = false;
            Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                Vector3 min = b.min;
                Vector3 max = b.max;
                Vector3[] corners =
                {
                    new(min.x,min.y,min.z), new(max.x,min.y,min.z), new(min.x,max.y,min.z), new(max.x,max.y,min.z),
                    new(min.x,min.y,max.z), new(max.x,min.y,max.z), new(min.x,max.y,max.z), new(max.x,max.y,max.z)
                };
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = transform.InverseTransformPoint(corner);
                    localMin = Vector3.Min(localMin, local);
                    localMax = Vector3.Max(localMax, local);
                }
                found = true;
            }

            if (!found) return;

            if (_chassisCollider == null)
            {
                _chassisCollider = GetComponent<BoxCollider>();
                if (_chassisCollider == null) _chassisCollider = gameObject.AddComponent<BoxCollider>();
            }

            Vector3 rawSize = localMax - localMin;
            _chassisCollider.center = (localMin + localMax) * 0.5f + Vector3.up * rawSize.y * 0.05f;
            _chassisCollider.size = new Vector3(
                Mathf.Max(0.8f, rawSize.x * 0.88f),
                Mathf.Max(0.35f, rawSize.y * 0.58f),
                Mathf.Max(1.2f, rawSize.z * 0.90f));
            _chassisCollider.enabled = true;
            _chassisCollider.isTrigger = false;
        }

        private void SnapChassisAboveGround()
        {
            if (_chassisCollider == null) return;

            Bounds b = _chassisCollider.bounds;
            Vector3 origin = new Vector3(b.center.x, b.max.y + 2f, b.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float groundY = float.NegativeInfinity;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider == _chassisCollider || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < 0.55f) continue;
                if (!found || hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    found = true;
                }
            }

            if (!found) return;

            float desiredBottom = groundY + chassisGroundClearance;
            float delta = desiredBottom - b.min.y;
            if (Mathf.Abs(delta) > 0.005f)
            {
                transform.position += Vector3.up * delta;
                Physics.SyncTransforms();
                Debug.Log($"[CYDOY] Car chassis ground correction: deltaY={delta:F3}, ground={groundY:F3}", this);
            }
        }
    }
}
