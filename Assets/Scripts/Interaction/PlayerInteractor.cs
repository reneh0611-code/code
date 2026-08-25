using CheatOnYourDayOnes.Core;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatOnYourDayOnes.Interaction
{
    [RequireComponent(typeof(PlayerAgent))]
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.5f)] private float interactionDistance = GameConstants.DefaultInteractionDistance;
        [SerializeField] private LayerMask interactionMask = ~0;

        public string CurrentPrompt { get; private set; } = string.Empty;

        private PlayerAgent _player;
        private NetworkObject _currentTarget;

        private void Awake()
        {
            _player = GetComponent<PlayerAgent>();
        }

        private void Update()
        {
            if (!IsOwner || playerCamera == null)
                return;

            ScanForInteraction();

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && _currentTarget != null)
                RequestInteractRpc(_currentTarget.NetworkObjectId);
        }

        private void ScanForInteraction()
        {
            CurrentPrompt = string.Empty;
            _currentTarget = null;

            Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
                return;

            NetworkObject networkObject = hit.collider.GetComponentInParent<NetworkObject>();
            if (networkObject == null)
                return;

            IInteractable interactable = FindInteractable(networkObject.gameObject);
            if (interactable == null || !interactable.CanInteract(_player))
                return;

            _currentTarget = networkObject;
            CurrentPrompt = $"[E] {interactable.GetInteractionText(_player)}";
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractRpc(ulong targetNetworkObjectId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject target))
                return;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > interactionDistance + 0.75f)
            {
                Debug.LogWarning($"[Interaction] Rejected interaction from {OwnerClientId}: target too far ({distance:F2}m).");
                return;
            }

            IInteractable interactable = FindInteractable(target.gameObject);
            if (interactable == null || !interactable.CanInteract(_player))
                return;

            interactable.InteractServer(_player);
        }

        private static IInteractable FindInteractable(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                    return interactable;
            }

            return null;
        }
    }
}
