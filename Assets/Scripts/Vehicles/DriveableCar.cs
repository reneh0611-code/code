using CheatOnYourDayOnes.CameraSystem;
using System.Collections.Generic;
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

        [Header("Arcade Vehicle Physics")]
        [SerializeField] private float topSpeed = 20f;                 // Unity m/s
        [SerializeField] private float reverseTopSpeed = 6.5f;
        [SerializeField] private float forwardAcceleration = 7.2f;    // m/s²
        [SerializeField] private float reverseAcceleration = 4.8f;
        [SerializeField] private float engineBraking = 1.5f;
        [SerializeField] private float brakeAcceleration = 15f;
        [SerializeField] private float steeringRate = 72f;
        [SerializeField] private float highSpeedSteerFactor = 0.38f;
        [SerializeField] private float lateralGripLowSpeed = 10f;
        [SerializeField] private float lateralGripHighSpeed = 4.5f;
        [SerializeField] private float throttleResponse = 7f;
        [SerializeField] private float steeringResponse = 10f;
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField] private float tyreGroundClearance = 0.015f;

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

        private readonly List<Renderer> _wheelRenderers = new();
        private readonly List<SphereCollider> _wheelSupportColliders = new();

        private bool _occupied;
        private bool _ignoreExitUntilEReleased;
        private float _rawThrottle;
        private float _rawSteer;
        private float _throttle;
        private float _steer;
        private bool _brake;
        private float _driveSpeed;
        private float _debugTimer;
        private float _modelScale = 1f;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            DetectWheelsAndScale();
            ConfigureRigidbody();
            RebuildVehicleColliders();
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = 1350f;
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.02f;
            _rb.angularDamping = 3.2f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 4f;
            _rb.centerOfMass = centerOfMass != null
                ? transform.InverseTransformPoint(centerOfMass.position)
                : new Vector3(0f, -0.35f * _modelScale, 0.05f * _modelScale);
        }

        private void DetectWheelsAndScale()
        {
            _wheelRenderers.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds all = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                all.Encapsulate(r.bounds);
                string n = r.name.ToLowerInvariant();
                string p = r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : string.Empty;
                if (LooksLikeWheelName(n) || LooksLikeWheelName(p))
                    _wheelRenderers.Add(r);
            }

            float length = Mathf.Max(all.size.x, all.size.z);
            _modelScale = Mathf.Clamp(length / 4.5f, 0.5f, 3f);
            Debug.Log($"[CYDOY] Car setup: visualLength={length:F2}, modelScale={_modelScale:F2}, wheelRenderers={_wheelRenderers.Count}", this);
        }

        private static bool LooksLikeWheelName(string n) =>
            n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("reifen") ||
            n.Contains("felge") || n.Contains("rim") || n.Contains("roue") || n.Contains("rad_");

        private void Update()
        {
            if (!_occupied || Keyboard.current == null) return;

            _rawThrottle = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
            _rawSteer = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            _brake = Keyboard.current.spaceKey.isPressed;

            _throttle = Mathf.MoveTowards(_throttle, _rawThrottle, throttleResponse * Time.deltaTime);
            _steer = Mathf.MoveTowards(_steer, _rawSteer, steeringResponse * Time.deltaTime);

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

            if (_brake)
            {
                _driveSpeed = Mathf.MoveTowards(_driveSpeed, 0f, brakeAcceleration * Time.fixedDeltaTime);
            }
            else if (_throttle > 0.01f)
            {
                _driveSpeed = Mathf.MoveTowards(_driveSpeed, topSpeed, forwardAcceleration * _throttle * Time.fixedDeltaTime);
            }
            else if (_throttle < -0.01f)
            {
                _driveSpeed = Mathf.MoveTowards(_driveSpeed, -reverseTopSpeed, reverseAcceleration * -_throttle * Time.fixedDeltaTime);
            }
            else
            {
                _driveSpeed = Mathf.MoveTowards(_driveSpeed, 0f, engineBraking * Time.fixedDeltaTime);
            }

            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            float speed01 = Mathf.Clamp01(Mathf.Abs(_driveSpeed) / topSpeed);
            float grip = Mathf.Lerp(lateralGripLowSpeed, lateralGripHighSpeed, speed01);
            float lateral = Mathf.MoveTowards(localVelocity.x, 0f, grip * Time.fixedDeltaTime);

            // Preserve gravity/bumps on Y while commanding the longitudinal drivetrain velocity.
            Vector3 wantedWorldVelocity = transform.TransformDirection(new Vector3(lateral, 0f, _driveSpeed));
            wantedWorldVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = wantedWorldVelocity;

            // No steering while stationary. Steering builds naturally with road speed and becomes calmer at high speed.
            float steerAuthority = Mathf.Clamp01(Mathf.Abs(_driveSpeed) / 2.2f);
            float steerMultiplier = Mathf.Lerp(1f, highSpeedSteerFactor, speed01);
            float reverseSign = _driveSpeed < -0.05f ? -1f : 1f;
            float yaw = _steer * steeringRate * steerMultiplier * steerAuthority * reverseSign * Time.fixedDeltaTime;
            if (Mathf.Abs(yaw) > 0.0001f)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            // Keep pitch/roll stable without making the vehicle completely rigid.
            Vector3 angular = _rb.angularVelocity;
            angular.x *= 0.72f;
            angular.z *= 0.72f;
            _rb.angularVelocity = angular;

            _debugTimer += Time.fixedDeltaTime;
            if (_debugTimer >= 1f && Mathf.Abs(_rawThrottle) > 0.1f)
            {
                _debugTimer = 0f;
                Debug.Log($"[CYDOY] CAR DRIVE commanded={_driveSpeed:F2}m/s actual={transform.InverseTransformDirection(_rb.linearVelocity).z:F2}m/s steer={_steer:F2}", this);
            }
        }

        public float DistanceFrom(Vector3 worldPoint)
        {
            float best = float.MaxValue;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                best = Mathf.Min(best, Vector3.Distance(worldPoint, c.ClosestPoint(worldPoint)));
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

            DetectWheelsAndScale();
            ConfigureRigidbody();
            RebuildVehicleColliders();
            PutTyresOnGround();

            _driveSpeed = 0f;
            _throttle = 0f;
            _steer = 0f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.WakeUp();

            _occupied = true;
            _ignoreExitUntilEReleased = true;
            Debug.Log($"[CYDOY] Vehicle active. topSpeed={topSpeed:F1}m/s wheelSupports={_wheelSupportColliders.Count}", this);
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
            _rawThrottle = 0f;
            _rawSteer = 0f;
            _throttle = 0f;
            _steer = 0f;
            _brake = false;
            _driveSpeed = 0f;
        }

        private void RebuildVehicleColliders()
        {
            // Remove/disable imported collision geometry so it cannot drag under the road.
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c == _chassisCollider || _wheelSupportColliders.Contains(c as SphereCollider) || c.isTrigger) continue;
                c.enabled = false;
            }

            foreach (SphereCollider old in _wheelSupportColliders)
                if (old != null) Destroy(old.gameObject);
            _wheelSupportColliders.Clear();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds all = renderers[0].bounds;
            foreach (Renderer r in renderers) if (r != null) all.Encapsulate(r.bounds);

            Vector3 localMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in BoundsCorners(all))
            {
                Vector3 l = transform.InverseTransformPoint(corner);
                localMin = Vector3.Min(localMin, l);
                localMax = Vector3.Max(localMax, l);
            }

            if (_chassisCollider == null)
            {
                _chassisCollider = GetComponent<BoxCollider>();
                if (_chassisCollider == null) _chassisCollider = gameObject.AddComponent<BoxCollider>();
            }

            Vector3 raw = localMax - localMin;
            _chassisCollider.size = new Vector3(raw.x * 0.82f, raw.y * 0.32f, raw.z * 0.84f);
            _chassisCollider.center = new Vector3(
                (localMin.x + localMax.x) * 0.5f,
                localMin.y + raw.y * 0.66f,
                (localMin.z + localMax.z) * 0.5f);
            _chassisCollider.enabled = true;
            _chassisCollider.isTrigger = false;

            // Four physical support spheres at the actual rendered wheels. These, not the belly collider,
            // determine ride height and keep the visible tyres on the road.
            foreach (Renderer wheel in _wheelRenderers)
            {
                if (wheel == null || !wheel.enabled) continue;

                Bounds wb = wheel.bounds;
                float radiusWorld = Mathf.Clamp(wb.size.y * 0.47f, 0.16f * _modelScale, 0.48f * _modelScale);
                GameObject support = new GameObject("CYDOY_WheelSupport_" + wheel.name);
                support.transform.SetParent(transform, false);
                support.transform.position = wb.center;

                SphereCollider sphere = support.AddComponent<SphereCollider>();
                float parentScale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Max(Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z))));
                sphere.radius = radiusWorld / parentScale;
                sphere.center = Vector3.zero;
                _wheelSupportColliders.Add(sphere);
            }

            Debug.Log($"[CYDOY] Vehicle colliders rebuilt: wheelSupports={_wheelSupportColliders.Count}, chassisBottom={_chassisCollider.bounds.min.y:F3}", this);
        }

        private void PutTyresOnGround()
        {
            if (_wheelRenderers.Count == 0) return;

            float tyreBottom = float.PositiveInfinity;
            Vector3 averageCenter = Vector3.zero;
            int count = 0;
            foreach (Renderer wheel in _wheelRenderers)
            {
                if (wheel == null || !wheel.enabled) continue;
                tyreBottom = Mathf.Min(tyreBottom, wheel.bounds.min.y);
                averageCenter += wheel.bounds.center;
                count++;
            }
            if (count == 0 || float.IsInfinity(tyreBottom)) return;
            averageCenter /= count;

            Vector3 origin = new Vector3(averageCenter.x, averageCenter.y + 3f * _modelScale, averageCenter.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 10f * _modelScale, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float groundY = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform) || hit.normal.y < 0.65f) continue;
                if (!found || hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    found = true;
                }
            }
            if (!found) return;

            float delta = (groundY + tyreGroundClearance) - tyreBottom;
            transform.position += Vector3.up * delta;
            Physics.SyncTransforms();
            Debug.Log($"[CYDOY] TYRES ON GROUND: tyreBottom={tyreBottom:F3}, ground={groundY:F3}, delta={delta:F3}", this);
        }

        private static Vector3[] BoundsCorners(Bounds b)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;
            return new[]
            {
                new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
                new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
                new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
                new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
            };
        }
    }
}
