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

        [Header("Driving")]
        [SerializeField] private float zeroToHundredSeconds = 6.8f;
        [SerializeField] private float maxSpeedKmh = 145f;
        [SerializeField] private float maxReverseKmh = 32f;
        [SerializeField] private float reverseAcceleration = 5.5f;
        [SerializeField] private float coastDeceleration = 1.8f;
        [SerializeField] private float brakeDeceleration = 14f;
        [SerializeField] private float steeringDegreesPerSecond = 82f;
        [SerializeField] private float highSpeedSteeringMultiplier = 0.28f;
        [SerializeField] private float lateralGrip = 8f;
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField] private float wheelGroundClearance = 0.02f;

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
        private bool _occupied;
        private float _throttle;
        private float _steer;
        private bool _brake;
        private bool _ignoreExitUntilEReleased;
        private float _debugTimer;
        private float _driveScale = 1f;
        private float _drivetrainSpeed;

        public bool IsOccupied => _occupied;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            DetectModelScaleAndWheels();
            ConfigureRigidbody();
            RebuildChassisCollider();
        }

        private void DetectModelScaleAndWheels()
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
                if (LooksLikeWheelName(n) || LooksLikeWheelName(p)) _wheelRenderers.Add(r);
            }
            float likelyLength = Mathf.Max(all.size.x, all.size.z);
            if (likelyLength > .01f) _driveScale = Mathf.Clamp(likelyLength / 4.5f, .25f, 25f);
            Debug.Log($"[CYDOY] Car model scale: visualLength={likelyLength:F2}, driveScale={_driveScale:F2}, wheelRenderers={_wheelRenderers.Count}", this);
        }

        private static bool LooksLikeWheelName(string n) =>
            n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("rad_") || n.Contains("reifen") || n.Contains("felge") || n.Contains("rim") || n.Contains("roue");

        private void ConfigureRigidbody()
        {
            _rb.mass = 1350f;
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.01f;
            _rb.angularDamping = 3f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 5f;
            _rb.centerOfMass = centerOfMass != null ? transform.InverseTransformPoint(centerOfMass.position) : new Vector3(0f, -.42f, 0f);
        }

        private void Update()
        {
            if (!_occupied || Keyboard.current == null) return;
            _throttle = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
            _steer = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            _brake = Keyboard.current.spaceKey.isPressed;
            if (_ignoreExitUntilEReleased) { if (!Keyboard.current.eKey.isPressed) _ignoreExitUntilEReleased = false; }
            else if (Keyboard.current.eKey.wasPressedThisFrame) Exit();
        }

        private void FixedUpdate()
        {
            if (!_occupied) return;

            float maxForward = maxSpeedKmh / 3.6f * _driveScale;
            float maxReverse = maxReverseKmh / 3.6f * _driveScale;
            float accel = (100f / 3.6f) / Mathf.Max(1f, zeroToHundredSeconds) * _driveScale;

            // Drivetrain speed is persistent. Do NOT rebuild acceleration from Rigidbody velocity each
            // physics tick: contact resolution can reduce Rigidbody velocity before the next tick.
            if (_brake) _drivetrainSpeed = Mathf.MoveTowards(_drivetrainSpeed, 0f, brakeDeceleration * _driveScale * Time.fixedDeltaTime);
            else if (_throttle > .01f) _drivetrainSpeed = Mathf.MoveTowards(_drivetrainSpeed, maxForward, accel * Time.fixedDeltaTime);
            else if (_throttle < -.01f) _drivetrainSpeed = Mathf.MoveTowards(_drivetrainSpeed, -maxReverse, reverseAcceleration * _driveScale * Time.fixedDeltaTime);
            else _drivetrainSpeed = Mathf.MoveTowards(_drivetrainSpeed, 0f, coastDeceleration * _driveScale * Time.fixedDeltaTime);

            Vector3 local = transform.InverseTransformDirection(_rb.linearVelocity);
            float lateral = Mathf.MoveTowards(local.x, 0f, lateralGrip * _driveScale * Time.fixedDeltaTime);
            Vector3 desired = transform.TransformDirection(new Vector3(lateral, local.y, _drivetrainSpeed));
            _rb.linearVelocity = desired;

            float speedAbs = Mathf.Abs(_drivetrainSpeed);
            float steerAuthority = Mathf.Clamp01(speedAbs / Mathf.Max(.5f, 1.2f * _driveScale));
            float steerFactor = Mathf.Lerp(1f, highSpeedSteeringMultiplier, Mathf.InverseLerp(0f, maxForward, speedAbs));
            float reverseSign = _drivetrainSpeed < -.05f ? -1f : 1f;
            float yaw = _steer * steeringDegreesPerSecond * steerFactor * steerAuthority * reverseSign * Time.fixedDeltaTime;
            if (Mathf.Abs(yaw) > .0001f) _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            Vector3 angular = _rb.angularVelocity;
            angular.x *= .82f; angular.z *= .82f; _rb.angularVelocity = angular;

            _debugTimer += Time.fixedDeltaTime;
            if (_debugTimer >= .75f && Mathf.Abs(_throttle) > .1f)
            {
                _debugTimer = 0f;
                float kmh = Mathf.Abs(_drivetrainSpeed) / _driveScale * 3.6f;
                Debug.Log($"[CYDOY] CAR DRIVE throttle={_throttle:F1} commanded={kmh:F1}km/h drivetrain={_drivetrainSpeed:F2}u/s actual={_rb.linearVelocity.magnitude:F2}u/s", this);
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
            for (int i = 0; i < _driverRenderers.Length; i++) { _driverRendererStates[i] = _driverRenderers[i].enabled; _driverRenderers[i].enabled = false; }
            _driverColliders = player.GetComponentsInChildren<Collider>(true);
            _driverColliderStates = new bool[_driverColliders.Length];
            for (int i = 0; i < _driverColliders.Length; i++) { _driverColliderStates[i] = _driverColliders[i].enabled; _driverColliders[i].enabled = false; }
            if (_driverController != null) _driverController.enabled = false;

            Transform seat = driverSeat != null ? driverSeat : transform;
            player.SetParent(seat, false); player.localPosition = Vector3.zero; player.localRotation = Quaternion.identity;
            _camera = Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
            if (_camera != null) _camera.EnterVehicleMode(transform);

            ConfigureRigidbody();
            DetectModelScaleAndWheels();
            RebuildChassisCollider();
            PlaceWheelsOnGround();
            _drivetrainSpeed = 0f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.WakeUp();
            _occupied = true;
            _ignoreExitUntilEReleased = true;
            Debug.Log($"[CYDOY] Vehicle active: {name} | scale={_driveScale:F2} | wheels={_wheelRenderers.Count} | chassis={_chassisCollider.size}", this);
            return true;
        }

        public void Exit()
        {
            if (!_occupied || _driver == null) return;
            Transform p = _driver; p.SetParent(null, true);
            p.position = exitPoint != null ? exitPoint.position : transform.position - transform.right * 1.8f + Vector3.up * .25f;
            p.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_camera != null) _camera.ExitVehicleMode(p);
            if (_driverRenderers != null) for (int i = 0; i < _driverRenderers.Length; i++) if (_driverRenderers[i] != null) _driverRenderers[i].enabled = _driverRendererStates[i];
            if (_driverColliders != null) for (int i = 0; i < _driverColliders.Length; i++) if (_driverColliders[i] != null) _driverColliders[i].enabled = _driverColliderStates[i];
            if (_driverController != null) _driverController.enabled = true;
            if (_networkController != null) _networkController.enabled = true;
            if (_interactor != null) _interactor.enabled = true;
            _driver = null; _driverController = null; _networkController = null; _interactor = null; _camera = null;
            _driverRenderers = null; _driverRendererStates = null; _driverColliders = null; _driverColliderStates = null;
            _occupied = false; _throttle = 0f; _steer = 0f; _brake = false; _drivetrainSpeed = 0f;
        }

        private void RebuildChassisCollider()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (c != null && c != _chassisCollider && !c.isTrigger) c.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds all = renderers[0].bounds; foreach (Renderer r in renderers) if (r != null) all.Encapsulate(r.bounds);
            Vector3 localMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in GetBoundsCorners(all)) { Vector3 l = transform.InverseTransformPoint(corner); localMin = Vector3.Min(localMin, l); localMax = Vector3.Max(localMax, l); }
            if (_chassisCollider == null) { _chassisCollider = GetComponent<BoxCollider>(); if (_chassisCollider == null) _chassisCollider = gameObject.AddComponent<BoxCollider>(); }
            Vector3 raw = localMax - localMin;
            float h = Mathf.Max(.25f * _driveScale, raw.y * .34f);
            _chassisCollider.size = new Vector3(Mathf.Max(.8f * _driveScale, raw.x * .84f), h, Mathf.Max(1.2f * _driveScale, raw.z * .86f));
            // Keep collision body around the cabin/chassis, clearly above the tyres' contact patch.
            _chassisCollider.center = new Vector3((localMin.x + localMax.x) * .5f, localMin.y + raw.y * .64f, (localMin.z + localMax.z) * .5f);
            _chassisCollider.enabled = true; _chassisCollider.isTrigger = false;
        }

        private void PlaceWheelsOnGround()
        {
            float wheelBottom = GetWheelVisualBottomY();
            Bounds cb = _chassisCollider.bounds;
            Vector3 origin = new(cb.center.x, cb.max.y + 3f * _driveScale, cb.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f * _driveScale, ~0, QueryTriggerInteraction.Ignore);
            bool found = false; float groundY = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform) || hit.normal.y < .55f) continue;
                if (!found || hit.point.y > groundY) { groundY = hit.point.y; found = true; }
            }
            if (!found) return;
            float delta = groundY + wheelGroundClearance * _driveScale - wheelBottom;
            transform.position += Vector3.up * delta; Physics.SyncTransforms();
            Debug.Log($"[CYDOY] Wheels grounded: wheelBottom={wheelBottom:F3} ground={groundY:F3} delta={delta:F3} wheelRenderers={_wheelRenderers.Count}", this);
        }

        private float GetWheelVisualBottomY()
        {
            float bottom = float.PositiveInfinity;
            if (_wheelRenderers.Count > 0) foreach (Renderer r in _wheelRenderers) if (r != null && r.enabled) bottom = Mathf.Min(bottom, r.bounds.min.y);
            if (!float.IsInfinity(bottom)) return bottom;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r != null && r.enabled) bottom = Mathf.Min(bottom, r.bounds.min.y);
            return float.IsInfinity(bottom) ? transform.position.y : bottom;
        }

        private static Vector3[] GetBoundsCorners(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            return new[] { new Vector3(min.x,min.y,min.z),new Vector3(max.x,min.y,min.z),new Vector3(min.x,max.y,min.z),new Vector3(max.x,max.y,min.z),new Vector3(min.x,min.y,max.z),new Vector3(max.x,min.y,max.z),new Vector3(min.x,max.y,max.z),new Vector3(max.x,max.y,max.z) };
        }
    }
}
