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
        [SerializeField, Min(0.1f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.8f;
        [SerializeField, Min(0.1f)] private float acceleration = 18f;
        [SerializeField, Min(0.1f)] private float deceleration = 22f;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 18f;
        [SerializeField] private float gravity = -26f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        private CharacterController _controller;
        private Vector2 _moveInput;
        private bool _sprintInput;
        private float _verticalVelocity;
        private Vector3 _serverPlanarVelocity;

        private readonly NetworkVariable<Vector3> _serverPosition = new(default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> _serverRotation = new(Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake() => _controller = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            bool local = IsOwner;
            if (playerCamera != null) playerCamera.gameObject.SetActive(local);
            if (audioListener != null) audioListener.enabled = local;

            if (IsServer)
            {
                _serverPosition.Value = transform.position;
                _serverRotation.Value = transform.rotation;
            }
        }

        private void Update()
        {
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening)
                return;

            if (IsOwner)
            {
                ReadInput();
                SendMovementInputRpc(_moveInput, _sprintInput, GetLookYaw());
            }

            if (!IsServer)
            {
                transform.position = Vector3.Lerp(transform.position, _serverPosition.Value, 20f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, _serverRotation.Value, 20f * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening)
                return;

            _serverPosition.Value = transform.position;
            _serverRotation.Value = transform.rotation;
        }

        private void ReadInput()
        {
            if (Keyboard.current == null)
            {
                _moveInput = Vector2.zero;
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
            _sprintInput = Keyboard.current.leftShiftKey.isPressed;
        }

        private float GetLookYaw() => playerCamera != null ? playerCamera.transform.eulerAngles.y : transform.eulerAngles.y;

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendMovementInputRpc(Vector2 input, bool sprint, float cameraYaw)
        {
            if (!IsServer || NetworkManager == null || !NetworkManager.IsListening)
                return;

            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            Vector3 desiredDirection = yawRotation * new Vector3(input.x, 0f, input.y);
            float maxSpeed = sprint ? sprintSpeed : walkSpeed;
            Vector3 desiredVelocity = desiredDirection * maxSpeed;

            float rate = desiredVelocity.sqrMagnitude > 0.001f ? acceleration : deceleration;
            _serverPlanarVelocity = Vector3.MoveTowards(_serverPlanarVelocity, desiredVelocity, rate * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _serverPlanarVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            Vector3 flatMove = new(_serverPlanarVelocity.x, 0f, _serverPlanarVelocity.z);
            if (flatMove.sqrMagnitude > 0.12f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatMove.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            _serverPosition.Value = transform.position;
            _serverRotation.Value = transform.rotation;
        }
    }
}
