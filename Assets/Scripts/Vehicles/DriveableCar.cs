using CheatOnYourDayOnes.CameraSystem;
using System;
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
        [SerializeField] private float reverseAcceleration = 8f;
        [SerializeField] private float coastDeceleration = 1.6f;
        [SerializeField] private float brakeDeceleration = 24f;
        [SerializeField] private float maxSpeedKmh = 145f;
        [SerializeField] private float maxReverseKmh = 32f;
        [SerializeField] private float steeringDegreesPerSecond = 82f;
        [SerializeField] private float highSpeedSteeringMultiplier = 0.28f;
        [SerializeField] private float lateralGrip = 9f;
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField] private float wheelGroundClearance = 0.025f;

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
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds all = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                all.Encapsulate(r.bounds);
                string n = r.name.ToLowerInvariant();
                string parent = r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : string.Empty;
                if (LooksLikeWheelName(n) || LooksLikeWheelName(parent)) _wheelRenderers.Add(r);
            }

            // Imported assets are often authored in centimetres or arbitrary units. Physics speed is
            // expressed in Unity world units, so adapt the requested real-car speeds to model scale.
            float likelyLength = Mathf.Max(all.size.x, all.size.z);
            if (likelyLength > 0.01f)
                _driveScale = Mathf.Clamp(likelyLength / 4.5f, 0.25f, 25f);

            Debug.Log($"[CYDOY] Car model scale: visualLength={likelyLength:F2}, driveScale={_driveScale:F2}, wheelRenderers={_wheelRenderers.Count}", this);
        }

        private static bool LooksLikeWheelName(string n)
        {
            return n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("rad_") ||
                   n.Contains("reifen") || n.Contains("felge") || n.Contains("rim") || n.Contains("roue");
        }

        private void ConfigureRigidbody()
        {
            _rb.mass = 1350f;
            _rb.useGravity = true;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.linearDamping = 0.02f;
            _rb.angularDamping = 2.8f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 5f;
            _rb.centerOfMass = centerOfMass != null ? transform.InverseTransformPoint(centerOfMass.position) : new Vector3(0f, -0.42f, 0f);
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
            else if (Keyboard.current.eKey.wasPressedThisFrame) Exit();
        }

        private void FixedUpdate()
        {
            if (!_occupied) return;

            Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
            float forwardSpeed = localVelocity.z;
            float maxForwardMs = (maxSpeedKmh / 3.6f) * _driveScale;
            float maxReverseMs = (maxReverseKmh / 3.6f) * _driveScale;
            float realCarAcceleration = ((100f / 3.6f) / Mathf.Max(1f, zeroToHundredSeconds)) * _driveScale;

            float targetForwardSpeed;
            if (_brake)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * _driveScale * Time.fixedDeltaTime);
            else if (_throttle > 0f)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, maxForwardMs, realCarAcceleration * Time.fixedDeltaTime);
            else if (_throttle < 0f)
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverseMs, reverseAcceleration * _driveScale * Time.fixedDeltaTime);
            else
                targetForwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * _driveScale * Time.fixedDeltaTime);

            float targetLateral = Mathf.MoveTowards(localVelocity.x, 0f, lateralGrip * _driveScale * Time.fixedDeltaTime * Mathf.Max(1f, Mathf.Abs(localVelocity.x)));
            _rb.linearVelocity = transform.TransformDirection(new Vector3(targetLateral, localVelocity.y, targetForwardSpeed));

            float speedAbs = Mathf.Abs(targetForwardSpeed);
            float steerAuthority = Mathf.Clamp01(speedAbs / Mathf.Max(0.4f, 1.5f * _driveScale));
            float steeringFactor = Mathf.Lerp(1f, highSpeedSteeringMultiplier, Mathf.InverseLerp(0f, maxForwardMs, speedAbs));
            float reverseSign = targetForwardSpeed < -0.05f ? -1f : 1f;
            float yaw = _steer * steeringDegreesPerSecond * steeringFactor * steerAuthority * reverseSign * Time.fixedDeltaTime;
            if (Mathf.Abs(yaw) > 0.0001f) _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            Vector3 angular = _rb.angularVelocity;
            angular.x *= 0.84f;
            angular.z *= 0.84f;
            _rb.angularVelocity = angular;

            _debugTimer += Time.fixedDeltaTime;
            if (_debugTimer >= 0.75f && Mathf.Abs(_throttle) > 0.1f)
            {
                _debugTimer = 0f;
                float displayedKmh = Mathf.Abs(forwardSpeed) / _driveScale * 3.6f;
                Debug.Log($"[CYDOY] CAR DRIVE throttle={_throttle:F1} speed={displayedKmh:F1}km/h world={forwardSpeed:F2}u/s target={targetForwardSpeed:F2}u/s scale={_driveScale:F2}", this);
            }
        }

        public float DistanceFrom(Vector3 worldPoint)
        {
            float best = float.MaxValue;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
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
            for (int i = 0; i < _driverRenderers.Length; i++) { _driverRendererStates[i] = _driverRenderers[i].enabled; _driverRenderers[i].enabled = false; }
            _driverColliders = player.GetComponentsInChildren<Collider>(true);
            _driverColliderStates = new bool[_driverColliders.Length];
            for (int i = 0; i < _driverColliders.Length; i++) { _driverColliderStates[i] = _driverColliders[i].enabled; _driverColliders[i].enabled = false; }
            if (_driverController != null) _driverController.enabled = false;

            Transform seat = driverSeat != null ? driverSeat : transform;
            player.SetParent(seat, false);
            player.localPosition = Vector3.zero;
            player.localRotation = Quaternion.identity;

            _camera = UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
            if (_camera != null) _camera.EnterVehicleMode(transform);

            ConfigureRigidbody();
            RebuildChassisCollider();
            PlaceWheelsOnGround();
            _rb.WakeUp();
            _occupied = true;
            _ignoreExitUntilEReleased = true;
            Debug.Log($"[CYDOY] Vehicle active: {name} | scale={_driveScale:F2} | wheels={_wheelRenderers.Count} | chassis={_chassisCollider.size}", this);
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
            if (_driverRenderers != null) for (int i = 0; i < _driverRenderers.Length; i++) if (_driverRenderers[i] != null) _driverRenderers[i].enabled = _driverRendererStates[i];
            if (_driverColliders != null) for (int i = 0; i < _driverColliders.Length; i++) if (_driverColliders[i] != null) _driverColliders[i].enabled = _driverColliderStates[i];
            if (_driverController != null) _driverController.enabled = true;
            if (_networkController != null) _networkController.enabled = true;
            if (_interactor != null) _interactor.enabled = true;
            _driver = null; _driverController = null; _networkController = null; _interactor = null;
            _driverRenderers = null; _driverRendererStates = null; _driverColliders = null; _driverColliderStates = null; _camera = null;
            _occupied = false; _throttle = 0f; _steer = 0f; _brake = false;
        }

        private void RebuildChassisCollider()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c == _chassisCollider || c.isTrigger) continue;
                c.enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds all = renderers[0].bounds;
            foreach (Renderer r in renderers) if (r != null) all.Encapsulate(r.bounds);

            Vector3[] corners = GetBoundsCorners(all);
            Vector3 localMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = transform.InverseTransformPoint(corner);
                localMin = Vector3.Min(localMin, local);
                localMax = Vector3.Max(localMax, local);
            }

            if (_chassisCollider == null)
            {
                _chassisCollider = GetComponent<BoxCollider>();
                if (_chassisCollider == null) _chassisCollider = gameObject.AddComponent<BoxCollider>();
            }

            Vector3 rawSize = localMax - localMin;
            // Chassis collider deliberately ends well above the visual wheel bottoms. Wheels, not the
            // belly of the car, define ride height.
            float chassisHeight = Mathf.Max(0.25f * _driveScale, rawSize.y * 0.42f);
            _chassisCollider.size = new Vector3(Mathf.Max(.8f * _driveScale, rawSize.x * .86f), chassisHeight, Mathf.Max(1.2f * _driveScale, rawSize.z * .88f));
            _chassisCollider.center = new Vector3((localMin.x + localMax.x) * .5f, localMin.y + rawSize.y * .57f, (localMin.z + localMax.z) * .5f);
            _chassisCollider.enabled = true;
            _chassisCollider.isTrigger = false;
        }

        private void PlaceWheelsOnGround()
        {
            float visualBottom = GetWheelVisualBottomY();
            Bounds chassisBounds = _chassisCollider.bounds;
            Vector3 origin = new(chassisBounds.center.x, Mathf.Max(chassisBounds.max.y, visualBottom) + 3f * _driveScale, chassisBounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f * _driveScale, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float groundY = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform) || hit.normal.y < .55f) continue;
                if (!found || hit.point.y > groundY) { groundY = hit.point.y; found = true; }
            }
            if (!found) return;

            float delta = (groundY + wheelGroundClearance * _driveScale) - visualBottom;
            transform.position += Vector3.up * delta;
            Physics.SyncTransforms();
            Debug.Log($"[CYDOY] Wheels grounded: wheelBottom={visualBottom:F3} ground={groundY:F3} delta={delta:F3} wheelRenderers={_wheelRenderers.Count}", this);
        }

        private float GetWheelVisualBottomY()
        {
            if (_wheelRenderers.Count > 0)
            {
                float bottom = float.PositiveInfinity;
                foreach (Renderer r in _wheelRenderers) if (r != null && r.enabled) bottom = Mathf.Min(bottom, r.bounds.min.y);
                if (!float.IsInfinity(bottom)) return bottom;
            }

            // Fallback for models whose wheel meshes have generic names: the visible model's lowest
            // point is normally the tyre contact patch, so use it rather than the chassis collider.
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            float fallback = float.PositiveInfinity;
            foreach (Renderer r in renderers) if (r != null && r.enabled) fallback = Mathf.Min(fallback, r.bounds.min.y);
            return float.IsInfinity(fallback) ? transform.position.y : fallback;
        }

        private static Vector3[] GetBoundsCorners(Bounds b)
        {
            Vector3 min = b.min, max = b.max;
            return new[]
            {
                new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z), new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
                new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z), new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
            };
        }
    }
}
