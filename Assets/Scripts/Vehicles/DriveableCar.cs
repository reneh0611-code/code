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
        [SerializeField] private Transform[] visualWheels;

        [Header("Driving")]
        [SerializeField] private float motorForce = 9000f;
        [SerializeField] private float reverseForce = 5200f;
        [SerializeField] private float brakeForce = 13500f;
        [SerializeField] private float maxSpeedKmh = 165f;
        [SerializeField] private float maxReverseKmh = 35f;
        [SerializeField] private float steeringDegrees = 32f;
        [SerializeField] private float highSpeedSteeringDegrees = 8f;
        [SerializeField] private float lateralGrip = 7.5f;
        [SerializeField] private float downforce = 42f;
        [SerializeField] private float rollingResistance = 0.28f;
        [SerializeField] private float airDrag = 0.012f;
        [SerializeField] private float interactionDistance = 3.5f;

        private Rigidbody _rb;
        private Transform _driver;
        private CharacterController _driverController;
        private Behaviour[] _disabledDriverBehaviours;
        private bool _occupied;
        private float _throttle;
        private float _steer;
        private bool _brake;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = Mathf.Max(_rb.mass, 1250f);
            _rb.linearDamping = 0.02f;
            _rb.angularDamping = 1.4f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 7f;
            if (centerOfMass != null) _rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
            else _rb.centerOfMass = new Vector3(0f, -0.45f, 0.05f);
            EnsureBodyCollider();
        }

        private void Update()
        {
            if (!_occupied || Keyboard.current == null) return;
            float vertical = 0f;
            if (Keyboard.current.wKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            float horizontal = 0f;
            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            _throttle = Mathf.Clamp(vertical, -1f, 1f);
            _steer = Mathf.Clamp(horizontal, -1f, 1f);
            _brake = Keyboard.current.spaceKey.isPressed;
            if (Keyboard.current.eKey.wasPressedThisFrame) Exit();
        }

        private void FixedUpdate()
        {
            if (!_occupied) return;
            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
            float forwardKmh = localVelocity.z * 3.6f;

            float engine = 0f;
            if (_throttle > 0f && forwardKmh < maxSpeedKmh) engine = _throttle * motorForce;
            else if (_throttle < 0f && forwardKmh > -maxReverseKmh) engine = _throttle * reverseForce;
            _rb.AddForce(transform.forward * engine, ForceMode.Force);

            if (_brake)
            {
                Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, transform.up);
                if (planar.sqrMagnitude > 0.02f)
                    _rb.AddForce(-planar.normalized * brakeForce, ForceMode.Force);
            }

            float steerLimit = Mathf.Lerp(steeringDegrees, highSpeedSteeringDegrees, Mathf.InverseLerp(15f, maxSpeedKmh, speedKmh));
            float direction = Mathf.Abs(localVelocity.z) < 0.35f ? Mathf.Sign(_throttle == 0f ? 1f : _throttle) : Mathf.Sign(localVelocity.z);
            float steerAuthority = Mathf.Clamp01(Mathf.Abs(localVelocity.z) / 2.5f);
            float yaw = _steer * steerLimit * direction * steerAuthority * Time.fixedDeltaTime;
            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            Vector3 lateral = transform.right * localVelocity.x;
            _rb.AddForce(-lateral * lateralGrip * _rb.mass, ForceMode.Force);
            _rb.AddForce(-transform.up * downforce * speedKmh, ForceMode.Force);
            _rb.AddForce(-_rb.linearVelocity * rollingResistance, ForceMode.Force);
            _rb.AddForce(-_rb.linearVelocity * _rb.linearVelocity.magnitude * airDrag, ForceMode.Force);
        }

        public float DistanceFrom(Vector3 worldPoint)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            float best = float.MaxValue;
            foreach (Collider c in colliders)
            {
                if (c == null || !c.enabled) continue;
                Vector3 closest = c.ClosestPoint(worldPoint);
                float d = Vector3.Distance(worldPoint, closest);
                if (d < best) best = d;
            }
            if (best < float.MaxValue) return best;
            return Vector3.Distance(worldPoint, transform.position);
        }

        public bool TryEnter(Transform player)
        {
            if (_occupied || player == null || DistanceFrom(player.position) > interactionDistance) return false;
            _driver = player;
            _driverController = player.GetComponent<CharacterController>();
            if (_driverController != null) _driverController.enabled = false;
            var networkController = player.GetComponent<CheatOnYourDayOnes.Player.NetworkPlayerController>();
            _disabledDriverBehaviours = networkController != null ? new Behaviour[] { networkController } : System.Array.Empty<Behaviour>();
            foreach (Behaviour b in _disabledDriverBehaviours) if (b != null) b.enabled = false;
            Transform seat = driverSeat != null ? driverSeat : transform;
            player.SetParent(seat, true);
            player.position = seat.position;
            player.rotation = seat.rotation;
            _occupied = true;
            Debug.Log($"[CYDOY] Entered vehicle {name}.", this);
            return true;
        }

        public void Exit()
        {
            if (!_occupied || _driver == null) return;
            Transform p = _driver;
            p.SetParent(null, true);
            p.position = exitPoint != null ? exitPoint.position : transform.position - transform.right * 1.7f + Vector3.up * 0.3f;
            p.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_driverController != null) _driverController.enabled = true;
            foreach (Behaviour b in _disabledDriverBehaviours) if (b != null) b.enabled = true;
            _driver = null;
            _driverController = null;
            _disabledDriverBehaviours = null;
            _occupied = false;
            _throttle = 0f;
            _steer = 0f;
            _brake = false;
        }

        private void EnsureBodyCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 localSize = transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(localSize.x) * 0.92f, Mathf.Abs(localSize.y) * 0.78f, Mathf.Abs(localSize.z) * 0.94f);
        }
    }
}
