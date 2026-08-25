using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerData : NetworkBehaviour
    {
        public NetworkVariable<FixedString64Bytes> PlayerName = new(
            new FixedString64Bytes("Player"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> Reputation = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> Followers = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            if (PlayerName.Value.IsEmpty)
                PlayerName.Value = new FixedString64Bytes($"Player {OwnerClientId + 1}");
        }

        public void SetPlayerNameServer(string newName)
        {
            if (!IsServer)
                return;

            string safeName = string.IsNullOrWhiteSpace(newName) ? $"Player {OwnerClientId + 1}" : newName.Trim();
            if (safeName.Length > 32)
                safeName = safeName[..32];

            PlayerName.Value = new FixedString64Bytes(safeName);
        }

        public void AddReputationServer(int amount)
        {
            if (!IsServer)
                return;

            Reputation.Value = Mathf.Clamp(Reputation.Value + amount, -1000, 1000);
        }

        public void AddFollowersServer(int amount)
        {
            if (!IsServer)
                return;

            Followers.Value = Mathf.Max(0, Followers.Value + amount);
        }
    }
}
