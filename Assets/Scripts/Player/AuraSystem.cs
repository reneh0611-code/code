using System;
using CheatOnYourDayOnes.Core;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class AuraSystem : NetworkBehaviour
    {
        public NetworkVariable<int> Aura = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action<int, int> AuraChanged;

        public override void OnNetworkSpawn()
        {
            Aura.OnValueChanged += HandleAuraChanged;
        }

        public override void OnNetworkDespawn()
        {
            Aura.OnValueChanged -= HandleAuraChanged;
        }

        public void AddAuraServer(int amount, string reason = "")
        {
            if (!IsServer || amount == 0)
                return;

            int oldValue = Aura.Value;
            Aura.Value = Mathf.Clamp(Aura.Value + amount, GameConstants.MinAura, GameConstants.MaxAura);

            Debug.Log($"[Aura] Client {OwnerClientId}: {oldValue} -> {Aura.Value}. Reason: {reason}");
        }

        private void HandleAuraChanged(int previous, int current)
        {
            AuraChanged?.Invoke(previous, current);
        }
    }
}
