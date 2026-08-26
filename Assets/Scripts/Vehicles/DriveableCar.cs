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
        [SerializeField] private float acceleration = 8.5f;
        [SerializeField] private float reverseAcceleration = 5.5f;
        [SerializeField] private float coastDeceleration = 2.4f;
        [SerializeField] private float brakeDeceleration = 18f;
        [SerializeField] private float maxSpeedKmh = 135f;
        [SerializeField] private float maxReverseKmh = 30f;
        [SerializeField] private float steeringDegreesPerSecond = 78f;
        [SerializeField] private float highSpeedSteeringMultiplier = 0.28f;
        [SerializeField] private float lateralGrip = 8f;
        [SerializeField] private float interactionDistance = 3.5f;

        private Rigidbody _rb;
        private Transform _driver;
        private CharacterController _driverController;
        private Behaviour[] _disabledDriverBehaviours;
        private Renderer[] _driverRenderers;
        private bool[] _driverRendererStates;
        private Collider[] _driverColliders;
        private bool[] _driverColliderStates;
        private ThirdPersonCamera _camera;
        private bool _occupied;
        private float _throttle;
        private float _steer;
        private bool _brake;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            EnsureCollisionSetup();
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = Mathf.Max(_rb.mass, 1200f);
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.04f;
            _rb.angularDamping = 2.6f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 5f;
            _rb.centerOfMass = centerOfMass != null
                ? transform.InverseTransformPoint(centerOfMass.position)
                : new Vector3(0f, -0.50f, 0f);
        }

        private void Update()
        {
            if (!_occupied || Keyboard.current == null) return;

            _throttle = 0f;
            if (Keyboard.current.wKey.isPressed) _throttle += 1f;
            if (Keyboard.current.sKey.isPressed) _throttle -= 1f;

            _steer = 0f;
            if (Keyboard.current.aKey.isPressed) _steer -= 1f;
            if (Keyboard.current.dKey.isPressed) _steer += 1f;

            _brake = Keyboard.current.spaceKey.isPressed;
            if (Keyboard.current.eKey.wasPressedThisFrame) Exit();
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
            {
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * Time.fixedDeltaTime);
            }
            else if (_throttle > 0f)
            {
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, maxForwardMs, acceleration * Time.fixedDeltaTime);
            }
            else if (_throttle < 0f)
            {
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverseMs, reverseAcceleration * Time.fixedDeltaTime);
            }
            else
            {
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * Time.fixedDeltaTime);
            }

            float targetLateral = Mathf.MoveTowards(localVelocity.x, 0f, lateralGrip * Time.fixedDeltaTime * Mathf.Max(1f, Mathf.Abs(localVelocity.x)));
            Vector3 newLocalVelocity = new Vector3(targetLateral, localVelocity.y, targetForwardSpeed);
            _rb.linearVelocity = transform.TransformDirection(newLocalVelocity);

            float speedAbs = Mathf.Abs(targetForwardSpeed);
            float steerAuthority = Mathf.Clamp01(speedAbs / 0.7f);
            float speed01 = Mathf.InverseLerp(0f, maxForwardMs, speedAbs);
            float steeringFactor = Mathf.Lerp(1f, highSpeedSteeringMultiplier, speed01);
            float reverseSign = targetForwardSpeed < -0.05f ? -1f : 1f;
            float yaw = _steer * steeringDegreesPerSecond * steeringFactor * steerAuthority * reverseSign * Time.fixedDeltaTime;

            if (Mathf.Abs(yaw) > 0.0001f)
            {
                Quaternion targetRotation = _rb.rotation * Quaternion.Euler(0f, yaw, 0f);
                _rb.MoveRotation(targetRotation);
            }

            // Keep the car stable without locking normal road pitch/roll completely.
            Vector3 angular = _rb.angularVelocity;
            angular.x *= 0.88f;
            angular.z *= 0.88f;
            _rb.angularVelocity = angular;
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

            var networkController = player.GetComponent<CheatOnYourDayOnes.Player.NetworkPlayerController>();
            var interactor = player.GetComponent<VehicleInteractor>();
            _disabledDriverBehaviours = new Behaviour[] { networkController, interactor };
            foreach (Behaviour b in _disabledDriverBehaviours) if (b != null) b.enabled = false;

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
            _rb.WakeUp();
            _occupied = true;

            Debug.Log($"[CYDOY] Vehicle active: {name} | rb.isKinematic={_rb.isKinematic} | mass={_rb.mass} | colliders={GetComponentsInChildren<Collider>(true).Length}", this);
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
            if (_disabledDriverBehaviours != null)
                foreach (Behaviour b in _disabledDriverBehaviours) if (b != null) b.enabled = true;

            _driver = null;
            _driverController = null;
            _disabledDriverBehaviours = null;
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

        private void EnsureCollisionSetup()
        {
            MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in meshColliders)
            {
                if (mc != null && !mc.convex)
                {
                    mc.enabled = false;
                    Debug.LogWarning($"[CYDOY] Disabled non-convex MeshCollider '{mc.name}' on driveable car. Dynamic rigidbodies cannot use non-convex mesh colliders reliably.", mc);
                }
            }

            Collider[] existing = GetComponentsInChildren<Collider>(true);
            foreach (Collider c in existing)
            {
                if (c != null && c.enabled && !c.isTrigger && c is not MeshCollider)
                    return;
                if (c is MeshCollider mc && mc.enabled && mc.convex)
                    return;
            }

            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;

            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 localSize = transform.InverseTransformVector(b.size);
            box.size = new Vector3(
                Mathf.Max(0.5f, Mathf.Abs(localSize.x) * 0.90f),
                Mathf.Max(0.35f, Mathf.Abs(localSize.y) * 0.65f),
                Mathf.Max(0.8f, Mathf.Abs(localSize.z) * 0.92f));

            Debug.Log($"[CYDOY] Added fallback BoxCollider to {name}: center={box.center}, size={box.size}", box);
        }
    }
}
