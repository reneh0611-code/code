using CheatOnYourDayOnes.CameraSystem;
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
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -26f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.35f;

        [Header("Sprint stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0.1f)] private float staminaDrainPerSecond = 22f;
        [SerializeField, Min(0.1f)] private float staminaRegenPerSecond = 18f;
        [SerializeField, Min(0f)] private float staminaRegenDelay = 0.8f;
        [SerializeField, Range(0f, 25f)] private float sprintResumeThreshold = 12f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private Vector2 _moveInput;
        private bool _sprintInput;
        private float _verticalVelocity;
        private Vector3 _serverPlanarVelocity;
        private float _lastSprintTime = -999f;
        private bool _sprintExhausted;

        private readonly NetworkVariable<Vector3> _serverPosition = new(default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> _serverRotation = new(Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _stamina = new(100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float Stamina => _stamina.Value;
        public float MaxStamina => maxStamina;
        public float Stamina01 => maxStamina <= 0f ? 0f : Mathf.Clamp01(_stamina.Value / maxStamina);
        public bool IsSprinting => _sprintInput && _moveInput.sqrMagnitude > 0.01f && !_sprintExhausted && _stamina.Value > 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (GetComponent<VehicleInteractor>() == null)
                gameObject.AddComponent<VehicleInteractor>();
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
            if (audioListener != null) audioListener.enabled = local;
            if (local) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (IsServer)
            {
                _serverPosition.Value = transform.position;
                _serverRotation.Value = transform.rotation;
                _stamina.Value = maxStamina;
            }
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
                ReadMovementInput();
                SendMovementInputRpc(_moveInput, _sprintInput, GetLookYaw());
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) RequestJumpRpc();
            }
            if (!IsServer)
            {
                transform.position = Vector3.Lerp(transform.position, _serverPosition.Value, 20f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, _serverRotation.Value, 20f * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;
            _serverPosition.Value = transform.position;
            _serverRotation.Value = transform.rotation;
        }

        private void ReadMovementInput()
        {
            if (Keyboard.current == null) { _moveInput = Vector2.zero; _sprintInput = false; return; }
            float x = 0f, y = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            _moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            _sprintInput = Keyboard.current.leftShiftKey.isPressed;
        }

        private float GetLookYaw()
        {
            if (_thirdPersonCamera != null) return _thirdPersonCamera.CurrentYaw;
            if (playerCamera != null) return playerCamera.transform.eulerAngles.y;
            return transform.eulerAngles.y;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendMovementInputRpc(Vector2 input, bool sprintRequested, float cameraYaw)
        {
            if (!IsServer || NetworkManager == null || !NetworkManager.IsListening) return;

            input = Vector2.ClampMagnitude(input, 1f);
            bool moving = input.sqrMagnitude > 0.01f;

            if (_sprintExhausted && _stamina.Value >= sprintResumeThreshold)
                _sprintExhausted = false;

            bool canSprint = sprintRequested && moving && !_sprintExhausted && _stamina.Value > 0.01f;

            if (canSprint)
            {
                _stamina.Value = Mathf.Max(0f, _stamina.Value - staminaDrainPerSecond * Time.deltaTime);
                _lastSprintTime = Time.time;
                if (_stamina.Value <= 0.01f)
                {
                    _stamina.Value = 0f;
                    _sprintExhausted = true;
                    canSprint = false;
                }
            }
            else if (Time.time - _lastSprintTime >= staminaRegenDelay)
            {
                _stamina.Value = Mathf.Min(maxStamina, _stamina.Value + staminaRegenPerSecond * Time.deltaTime);
            }

            Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            Vector3 desiredDirection = yawRotation * new Vector3(input.x, 0f, input.y);
            float maxSpeed = canSprint ? sprintSpeed : walkSpeed;
            Vector3 desiredVelocity = desiredDirection * maxSpeed;
            float rate = desiredVelocity.sqrMagnitude > 0.001f ? acceleration : deceleration;
            _serverPlanarVelocity = Vector3.MoveTowards(_serverPlanarVelocity, desiredVelocity, rate * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _serverPlanarVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, yawRotation, rotationSpeed * Time.deltaTime);
            _serverPosition.Value = transform.position;
            _serverRotation.Value = transform.rotation;
        }

        [Rpc(SendTo.Server)]
        private void RequestJumpRpc()
        {
            if (!IsServer || !_controller.isGrounded) return;
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
