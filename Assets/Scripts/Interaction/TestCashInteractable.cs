using CheatOnYourDayOnes.Interaction;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.DebugTools
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class TestCashInteractable : NetworkBehaviour, IInteractable
    {
        [SerializeField, Min(1)] private int reward = 100;
        [SerializeField] private string interactionText = "Take test job (+$100)";

        private bool _cooldown;
        private float _cooldownUntil;

        public string GetInteractionText(PlayerAgent player) => interactionText;

        public bool CanInteract(PlayerAgent player)
        {
            if (!IsServer)
                return true;

            return !_cooldown || Time.time >= _cooldownUntil;
        }

        public void InteractServer(PlayerAgent player)
        {
            if (!IsServer || player == null)
                return;

            if (_cooldown && Time.time < _cooldownUntil)
                return;

            player.Wallet.AddCashServer(reward, "Phase 1 test interaction");
            player.Aura.AddAuraServer(5, "Worked for money");

            _cooldown = true;
            _cooldownUntil = Time.time + 1f;
        }
    }
}
