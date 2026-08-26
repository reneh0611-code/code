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
        [SerializeField] private float acceleration = 11.5f;
        [SerializeField] private float reverseAcceleration = 7f;
        [SerializeField] private float brakeAcceleration = 22f;
        [SerializeField] private float maxSpeedKmh = 145f;
        [SerializeField] private float maxReverseKmh = 32f;
        [SerializeField] private float steeringDegreesPerSecond = 72f;
        [SerializeField] private float highSpeedSteeringMultiplier = 0.32f;
        [SerializeField] private float lateralGrip = 6.5f;
        [SerializeField] private float downforce = 18f;
        [SerializeField] private float interactionDistance = 3.5f;

        private Rigidbody _rb;
        private Transform _driver;
        private CharacterController _driverController;
        private Behaviour[] _disabledDriverBehaviours;
        private Renderer[] _driverRenderers;
        private bool[] _driverRendererStates;
        private Collider[] _driverColliders;
        private bool[] _driverColliderStates;
        private bool _occupied;
        private float _throttle;
        private float _steer;
        private bool _brake;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = Mathf.Max(_rb.mass, 1200f);
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.06f;
            _rb.angularDamping = 2.2f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 5f;
            _rb.centerOfMass = centerOfMass != null
                ? transform.InverseTransformPoint(centerOfMass.position)
                : new Vector3(0f, -0.55f, 0f);
            EnsureBodyCollider();
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
            float forwardKmh = localVelocity.z * 3.6f;
            float speedKmh = Mathf.Abs(forwardKmh);

            if (_brake)
            {
                Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);
                _rb.AddForce(-planar * brakeAcceleration, ForceMode.Acceleration);
            }
            else if (_throttle > 0f && forwardKmh < maxSpeedKmh)
            {
                _rb.AddForce(transform.forward * acceleration * _throttle, ForceMode.Acceleration);
            }
            else if (_throttle < 0f && forwardKmh > -maxReverseKmh)
            {
                _rb.AddForce(transform.forward * reverseAcceleration * _throttle, ForceMode.Acceleration);
            }

            float moving = Mathf.Clamp01(Mathf.Abs(localVelocity.z) / 0.8f);
            float highSpeedFactor = Mathf.Lerp(1f, highSpeedSteeringMultiplier, Mathf.InverseLerp(20f, maxSpeedKmh, speedKmh));
            float direction = localVelocity.z < -0.15f ? -1f : 1f;
            float yawDelta = _steer * steeringDegreesPerSecond * highSpeedFactor * moving * direction * Time.fixedDeltaTime;
            if (Mathf.Abs(yawDelta) > 0.0001f)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yawDelta, 0f));

            Vector3 lateralVelocity = transform.right * localVelocity.x;
            _rb.AddForce(-lateralVelocity * lateralGrip, ForceMode.Acceleration);
            _rb.AddForce(-transform.up * downforce * _rb.linearVelocity.sqrMagnitude * 0.02f, ForceMode.Acceleration);
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

            // Keep the player object attached to the car for now, but hide the standing avatar.
            // A dedicated seated driving animation can replace this later without changing the vehicle physics.
            Transform seat = driverSeat != null ? driverSeat : transform;
            player.SetParent(seat, false);
            player.localPosition = Vector3.zero;
            player.localRotation = Quaternion.identity;

            _rb.WakeUp();
            _occupied = true;
            Debug.Log($"[CYDOY] Entered vehicle {name}. Driving enabled: WASD, Space brake, E exit.", this);
            return true;
        }

        public void Exit()
        {
            if (!_occupied || _driver == null) return;

            Transform p = _driver;
            p.SetParent(null, true);
            p.position = exitPoint != null ? exitPoint.position : transform.position - transform.right * 1.8f + Vector3.up * 0.25f;
            p.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

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
            _occupied = false;
            _throttle = 0f;
            _steer = 0f;
            _brake = false;
        }

        private void EnsureBodyCollider()
        {
            Collider[] existing = GetComponentsInChildren<Collider>(true);
            foreach (Collider c in existing)
                if (c != null && !c.isTrigger) return;

            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 localSize = transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(localSize.x) * 0.92f, Mathf.Abs(localSize.y) * 0.72f, Mathf.Abs(localSize.z) * 0.94f);
        }
    }
}
