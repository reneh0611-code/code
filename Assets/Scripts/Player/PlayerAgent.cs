using CheatOnYourDayOnes.Economy;
using CheatOnYourDayOnes.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Player
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerWallet))]
    [RequireComponent(typeof(AuraSystem))]
    [RequireComponent(typeof(NeedsSystem))]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerAgent : NetworkBehaviour
    {
        public PlayerData Data { get; private set; }
        public PlayerWallet Wallet { get; private set; }
        public AuraSystem Aura { get; private set; }
        public NeedsSystem Needs { get; private set; }
        public PlayerInventory Inventory { get; private set; }

        private void Awake()
        {
            Data = GetComponent<PlayerData>();
            Wallet = GetComponent<PlayerWallet>();
            Aura = GetComponent<AuraSystem>();
            Needs = GetComponent<NeedsSystem>();
            Inventory = GetComponent<PlayerInventory>();
        }
    }
}
