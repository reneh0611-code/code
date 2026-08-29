using CheatOnYourDayOnes.CameraSystem;
using CheatOnYourDayOnes.Core;
using CheatOnYourDayOnes.Vehicles;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.0f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.8f;
        [SerializeField, Min(0.1f)] private float acceleration = 18f;
        [SerializeField, Min(0.1f)] private float deceleration = 22f;
        [SerializeField, Min(0.1f)] private float airAcceleration = 7f;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -26f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.35f;

        [Header("Ground physics")]
        [SerializeField, Min(0.02f)] private float groundProbeDistance = 0.28f;
        [SerializeField, Min(0.1f)] private float groundStickSpeed = 4.5f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 42f;
        [SerializeField, Range(1f, 3f)] private float fallingGravityMultiplier = 1.35f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Sprint stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0.1f)] private float staminaDrainPerSecond = 22f;
        [SerializeField, Min(0.1f)] private float staminaRegenPerSecond = 18f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 0.8f;
        [SerializeField, Range(0f, 25f)] private float sprintResumeThreshold = 12f;

        [Header("Networking")]
        [SerializeField, Range(10f, 60f)] private float inputSendRate = 30f;
        [SerializeField, Min(1f)] private float remotePositionSharpness = 18f;
        [SerializeField, Min(1f)] private float remoteRotationSharpness = 20f;
        [SerializeField, Min(0.5f)] private float remoteSnapDistance = 4f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        private readonly RaycastHit[] _groundHits = new RaycastHit[10];
        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private Vector2 _moveInput;
        private bool _sprintInput;
        private float _verticalVelocity;
        private Vector3 _serverPlanarVelocity;
        private Vector2 _serverMoveInput;
        private bool _serverSprintRequested;
        private float _serverCameraYaw;
        private bool _serverJumpQueued;
        private float _lastSprintTime = -999f;
        private bool _sprintExhausted;
        private bool _combatMovementLocked;
        private bool _serverCombatMovementLocked;
        private bool _serverCombatFacingActive;
        private Vector3 _serverCombatFacingDirection;
        private bool _actionMovementActive;
        private bool _serverActionMovementActive;
        private bool _carryMovementActive;
        private bool _serverCarryMovementActive;
        private float _carrySpeedMultiplier = 1f;
        private float _serverCarrySpeedMultiplier = 1f;
        private float _actionMovementSpeed;
        private float _serverActionMovementSpeed;
        private bool _grounded;
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _remotePlanarVelocity;
        private float _nextInputSendTime;
        private float _nextTerrainSafetyCheck;
        private Vector2 _lastSentInput = new(float.PositiveInfinity, float.PositiveInfinity);
        private bool _lastSentSprint;
        private float _lastSentYaw = float.PositiveInfinity;

        private readonly NetworkVariable<Vector3> _serverPosition = new(default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> _serverRotation = new(Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _stamina = new(100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _serverGrounded = new(true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float Stamina => _stamina.Value;
        public float MaxStamina => maxStamina;
        public float Stamina01 => maxStamina <= 0f ? 0f : Mathf.Clamp01(_stamina.Value / maxStamina);
        public bool IsSprinting => !_combatMovementLocked && !_actionMovementActive && !_carryMovementActive && _sprintInput && _moveInput.sqrMagnitude > 0.01f && !_sprintExhausted && _stamina.Value > 0f;
        public bool IsCombatMovementLocked => _combatMovementLocked;
        public bool IsActionMovementActive => _actionMovementActive;
        public bool IsCarryMovementActive => _carryMovementActive;
        public bool IsGrounded => _grounded;
        public Vector3 PlanarVelocity
        {
            get
            {
                Vector3 velocity = IsServer && _controller != null ? _controller.velocity : _remotePlanarVelocity;
                velocity.y = 0f;
                return velocity;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (GetComponent<VehicleInteractor>() == null)
                gameObject.AddComponent<VehicleInteractor>();
        }

        private void OnEnable()
        {
            _verticalVelocity = Mathf.Min(_verticalVelocity, 0f);
            _serverPlanarVelocity = Vector3.zero;
            _moveInput = Vector2.zero;
            _sprintInput = false;
        }

        public override void OnNetworkSpawn()
        {
            bool local = IsOwner;
            VehicleInteractor vehicleInteractor = GetComponent<VehicleInteractor>();
            if (vehicleInteractor != null) vehicleInteractor.enabled = local;

            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(local);
                _thirdPersonCamera = playerCamera.GetComponent<ThirdPersonCamera>();
                if (_thirdPersonCamera == null) _thirdPersonCamera = playerCamera.GetComponentInParent<ThirdPersonCamera>();
                if (_thirdPersonCamera == null) _thirdPersonCamera = playerCamera.GetComponentInChildren<ThirdPersonCamera>(true);
                if (local && _thirdPersonCamera != null) _thirdPersonCamera.SetTarget(transform);
            }

            if (audioListener != null)
            {
                audioListener.enabled = local;
                if (local)
                {
                    foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
                    {
                        if (listener != null && listener != audioListener)
                            listener.enabled = false;
                    }
                    audioListener.enabled = true;
                }
            }
            if (local)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (IsServer)
            {
                _serverPosition.Value = transform.position;
                _serverRotation.Value = transform.rotation;
                _serverCameraYaw = transform.eulerAngles.y;
                _stamina.Value = maxStamina;
                _serverGrounded.Value = ProbeGround(out _groundNormal);
            }
        }

        public void SetCombatMovementLocked(bool locked)
        {
            if (!IsOwner) return;
            _combatMovementLocked = locked;
            if (locked)
            {
                _moveInput = Vector2.zero;
                _sprintInput = false;
            }

            if (IsServer)
                ApplyServerCombatLock(locked);
            else
                SetCombatMovementLockedRpc(locked);
        }

        [Rpc(SendTo.Server)]
        private void SetCombatMovementLockedRpc(bool locked) => ApplyServerCombatLock(locked);

        private void ApplyServerCombatLock(bool locked)
        {
            _serverCombatMovementLocked = locked;
            if (!locked)
            {
                _serverCombatFacingActive = false;
                return;
            }
            _serverMoveInput = Vector2.zero;
            _serverSprintRequested = false;
            _serverPlanarVelocity = Vector3.zero;
        }

        public void FaceCombatTarget(Vector3 worldDirection)
        {
            if (!IsOwner) return;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < .001f) return;
            worldDirection.Normalize();

            if (IsServer)
                ApplyServerCombatFacing(worldDirection);
            else
                FaceCombatTargetRpc(worldDirection);
        }

        [Rpc(SendTo.Server)]
        private void FaceCombatTargetRpc(Vector3 worldDirection) => ApplyServerCombatFacing(worldDirection);

        private void ApplyServerCombatFacing(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < .001f) return;
            _serverCombatFacingDirection = worldDirection.normalized;
            _serverCombatFacingActive = true;
        }

        public void SetActionMovement(bool active, float speed = 0f)
        {
            if (!IsOwner) return;
            _actionMovementActive = active;
            _actionMovementSpeed = active ? Mathf.Max(.1f, speed) : 0f;
            if (active) _sprintInput = false;

            if (IsServer)
                ApplyServerActionMovement(active, _actionMovementSpeed);
            else
                SetActionMovementRpc(active, _actionMovementSpeed);
        }

        [Rpc(SendTo.Server)]
        private void SetActionMovementRpc(bool active, float speed) => ApplyServerActionMovement(active, speed);

        private void ApplyServerActionMovement(bool active, float speed)
        {
            _serverActionMovementActive = active;
            _serverActionMovementSpeed = active ? Mathf.Max(.1f, speed) : 0f;
            if (active)
            {
                _serverSprintRequested = false;
                _serverPlanarVelocity = Vector3.ClampMagnitude(_serverPlanarVelocity, _serverActionMovementSpeed);
            }
        }

        public void SetCarryMovement(bool active, float speedMultiplier = .52f)
        {
            if (!IsOwner) return;
            _carryMovementActive = active;
            _carrySpeedMultiplier = active ? Mathf.Clamp(speedMultiplier, .2f, 1f) : 1f;
            if (active) _sprintInput = false;

            if (IsServer) ApplyServerCarryMovement(active, _carrySpeedMultiplier);
            else SetCarryMovementRpc(active, _carrySpeedMultiplier);
        }

        [Rpc(SendTo.Server)]
        private void SetCarryMovementRpc(bool active, float speedMultiplier) => ApplyServerCarryMovement(active, speedMultiplier);

        private void ApplyServerCarryMovement(bool active, float speedMultiplier)
        {
            _serverCarryMovementActive = active;
            _serverCarrySpeedMultiplier = active ? Mathf.Clamp(speedMultiplier, .2f, 1f) : 1f;
            if (active) _serverSprintRequested = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!IsOwner) return;
            Cursor.lockState = hasFocus ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !hasFocus;
        }

        private void Update()
        {
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;

            if (IsOwner)
            {
                if (_combatMovementLocked)
                {
                    _moveInput = Vector2.zero;
                    _sprintInput = false;
                }
                else
                {
                    ReadMovementInput();
                }

                SendInputIfNeeded();

                if (!_combatMovementLocked && !_actionMovementActive && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    if (IsServer) _serverJumpQueued = true;
                    else RequestJumpRpc();
                }
            }

            if (!IsServer)
                InterpolateRemoteTransform();
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening || !_controller.enabled)
                return;

            SimulateMovement(Time.fixedDeltaTime);
            RescueIfBelowTerrain();
            _serverPosition.Value = transform.position;
            _serverRotation.Value = transform.rotation;
            _serverGrounded.Value = _grounded;
        }

        private void ReadMovementInput()
        {
            if (Keyboard.current == null)
            {
                _moveInput = Vector2.zero;
                _sprintInput = false;
                return;
            }

            if (_carryMovementActive)
            {
                _moveInput = Keyboard.current.sKey.isPressed ? new Vector2(0f, -1f) : Vector2.zero;
                _sprintInput = false;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            _moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            _sprintInput = !_carryMovementActive && Keyboard.current.leftShiftKey.isPressed;
        }

        private float GetLookYaw()
        {
            if (_thirdPersonCamera != null) return _thirdPersonCamera.CurrentYaw;
            if (playerCamera != null) return playerCamera.transform.eulerAngles.y;
            return transform.eulerAngles.y;
        }

        private void SendInputIfNeeded()
        {
            float yaw = GetLookYaw();
            bool inputChanged = (_moveInput - _lastSentInput).sqrMagnitude > 0.0001f || _sprintInput != _lastSentSprint;
            bool yawChanged = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw)) > 1.5f;
            if (!inputChanged && !yawChanged && Time.unscaledTime < _nextInputSendTime) return;
            if (!inputChanged && Time.unscaledTime < _nextInputSendTime) return;

            _nextInputSendTime = Time.unscaledTime + 1f / Mathf.Max(10f, inputSendRate);
            _lastSentInput = _moveInput;
            _lastSentSprint = _sprintInput;
            _lastSentYaw = yaw;

            if (IsServer)
                StoreServerInput(_moveInput, _sprintInput, yaw);
            else
                SendMovementInputRpc(_moveInput, _sprintInput, yaw);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendMovementInputRpc(Vector2 input, bool sprintRequested, float cameraYaw)
        {
            StoreServerInput(input, sprintRequested, cameraYaw);
        }

        private void StoreServerInput(Vector2 input, bool sprintRequested, float cameraYaw)
        {
            if (!IsServer || NetworkManager == null || !NetworkManager.IsListening) return;
            _serverMoveInput = _serverCombatMovementLocked ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);
            _serverSprintRequested = !_serverCombatMovementLocked && !_serverCarryMovementActive && sprintRequested;
            _serverCameraYaw = cameraYaw;
        }

        private void SimulateMovement(float deltaTime)
        {
            bool groundedBeforeMove = ProbeGround(out Vector3 groundNormal);
            _groundNormal = groundedBeforeMove ? groundNormal : Vector3.up;

            Vector2 input = _serverCombatMovementLocked ? Vector2.zero : _serverMoveInput;
            bool moving = input.sqrMagnitude > 0.01f;
            bool actionMovement = _serverActionMovementActive && !_serverCombatMovementLocked;

            if (_sprintExhausted && _stamina.Value >= sprintResumeThreshold)
                _sprintExhausted = false;

            bool canSprint = !actionMovement && !_serverCarryMovementActive && _serverSprintRequested && moving && !_sprintExhausted && _stamina.Value > 0.01f;
            UpdateStamina(canSprint, deltaTime);
            if (_stamina.Value <= 0.01f) canSprint = false;

            Quaternion cameraYaw = Quaternion.Euler(0f, _serverCameraYaw, 0f);
            Vector3 desiredDirection = _serverCarryMovementActive
                ? -transform.forward * Mathf.Clamp01(-input.y)
                : cameraYaw * new Vector3(input.x, 0f, input.y);
            if (actionMovement && desiredDirection.sqrMagnitude <= 0.001f)
                desiredDirection = transform.forward;
            if (groundedBeforeMove && desiredDirection.sqrMagnitude > 0.001f)
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, _groundNormal).normalized;

            float targetSpeed = actionMovement ? _serverActionMovementSpeed : canSprint ? sprintSpeed : walkSpeed;
            if (_serverCarryMovementActive && !actionMovement) targetSpeed *= _serverCarrySpeedMultiplier;
            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            float moveRate = desiredVelocity.sqrMagnitude > 0.001f ? acceleration : deceleration;
            if (!groundedBeforeMove) moveRate = Mathf.Min(moveRate, airAcceleration);
            _serverPlanarVelocity = Vector3.MoveTowards(_serverPlanarVelocity, desiredVelocity, moveRate * deltaTime);

            if (_serverJumpQueued && groundedBeforeMove && !_serverCombatMovementLocked && !actionMovement && !_serverCarryMovementActive)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                groundedBeforeMove = false;
            }
            else if (groundedBeforeMove && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -groundStickSpeed;
            }
            else
            {
                float multiplier = _verticalVelocity < 0f ? fallingGravityMultiplier : 1f;
                _verticalVelocity = Mathf.Max(_verticalVelocity + gravity * multiplier * deltaTime, -maxFallSpeed);
            }
            _serverJumpQueued = false;

            Vector3 velocity = _serverPlanarVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags flags = _controller.Move(velocity * deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            _grounded = (flags & CollisionFlags.Below) != 0 || ProbeGround(out _groundNormal);
            if (_grounded && _verticalVelocity < -groundStickSpeed)
                _verticalVelocity = -groundStickSpeed;

            // Preserve the original control scheme: WASD is camera-relative while the character
            // itself follows the camera yaw. A/D therefore strafe instead of turning the player.
            Quaternion wantedRotation;
            if (_serverCarryMovementActive)
            {
                wantedRotation = Quaternion.Euler(0f, _serverCameraYaw - 180f, 0f);
            }
            else if (_serverCombatMovementLocked)
            {
                // PullStart turns only the camera. Unless a combat action explicitly supplied a
                // facing target, a locked player must retain the exact rotation they started with.
                wantedRotation = _serverCombatFacingActive
                    ? Quaternion.LookRotation(_serverCombatFacingDirection)
                    : transform.rotation;
            }
            else if (actionMovement && desiredDirection.sqrMagnitude > .001f)
            {
                wantedRotation = Quaternion.LookRotation(desiredDirection);
            }
            else
            {
                wantedRotation = cameraYaw;
            }
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                wantedRotation,
                1f - Mathf.Exp(-rotationSpeed * deltaTime));
        }

        private void UpdateStamina(bool sprinting, float deltaTime)
        {
            if (sprinting)
            {
                _stamina.Value = Mathf.Max(0f, _stamina.Value - staminaDrainPerSecond * deltaTime);
                _lastSprintTime = Time.time;
                if (_stamina.Value <= 0.01f)
                {
                    _stamina.Value = 0f;
                    _sprintExhausted = true;
                }
            }
            else if (Time.time - _lastSprintTime >= staminaRegenDelay)
            {
                _stamina.Value = Mathf.Min(maxStamina, _stamina.Value + staminaRegenPerSecond * deltaTime);
            }
        }

        public void TeleportServerAuthoritative(Vector3 position)
        {
            if (!IsServer || _controller == null) return;

            bool wasEnabled = _controller.enabled;
            if (wasEnabled) _controller.enabled = false;
            transform.position = position;
            if (wasEnabled) _controller.enabled = true;
            Physics.SyncTransforms();

            _verticalVelocity = -groundStickSpeed;
            _serverPlanarVelocity = Vector3.zero;
            _serverMoveInput = Vector2.zero;
            _serverSprintRequested = false;
            _serverJumpQueued = false;
            _grounded = true;
            _groundNormal = Vector3.up;
            _serverPosition.Value = position;
            _serverRotation.Value = transform.rotation;
            _serverGrounded.Value = true;
        }

        private void RescueIfBelowTerrain()
        {
            if (Time.time < _nextTerrainSafetyCheck || transform.parent != null || !_controller.enabled) return;
            _nextTerrainSafetyCheck = Time.time + .5f;

            if (!AutoLocalHost.TryFindSafeTerrainSpawn(transform.position, _controller, 2f, out Vector3 safePosition))
                return;

            bool belowSurface = transform.position.y < safePosition.y - 4f;
            bool catastrophicFall = transform.position.y < -200f;
            if (belowSurface || catastrophicFall)
            {
                Debug.LogWarning($"[CYDOY PHYSICS] Player fell below the terrain and was recovered at {safePosition}.", this);
                TeleportServerAuthoritative(safePosition);
            }
        }

        private bool ProbeGround(out Vector3 normal)
        {
            normal = Vector3.up;
            if (_controller == null || !_controller.enabled) return false;

            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float radius = Mathf.Max(0.05f, _controller.radius * scaleXZ * 0.92f);
            float halfHeight = Mathf.Max(_controller.height * scaleY * 0.5f, radius);
            Vector3 center = transform.TransformPoint(_controller.center);
            Vector3 bottomSphereCenter = center - Vector3.up * (halfHeight - radius);
            Vector3 origin = bottomSphereCenter + Vector3.up * 0.06f;
            float distance = groundProbeDistance + 0.06f;

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHits, distance, groundMask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            float minimumNormalY = Mathf.Cos((_controller.slopeLimit + 1f) * Mathf.Deg2Rad);
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < minimumNormalY || hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                normal = hit.normal;
                found = true;
            }

            return found;
        }

        private void InterpolateRemoteTransform()
        {
            Vector3 targetPosition = _serverPosition.Value;
            Vector3 previousPosition = transform.position;
            if ((transform.position - targetPosition).sqrMagnitude > remoteSnapDistance * remoteSnapDistance)
                transform.position = targetPosition;
            else
                transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-remotePositionSharpness * Time.deltaTime));

            transform.rotation = Quaternion.Slerp(transform.rotation, _serverRotation.Value, 1f - Mathf.Exp(-remoteRotationSharpness * Time.deltaTime));
            if (Time.deltaTime > .0001f)
            {
                Vector3 measured = (transform.position - previousPosition) / Time.deltaTime;
                measured.y = 0f;
                _remotePlanarVelocity = Vector3.Lerp(_remotePlanarVelocity, measured, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }
            _grounded = _serverGrounded.Value;
        }

        [Rpc(SendTo.Server)]
        private void RequestJumpRpc()
        {
            if (!IsServer || _serverCombatMovementLocked) return;
            _serverJumpQueued = true;
        }
    }
}
