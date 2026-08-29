using System;
using CheatOnYourDayOnes.Core;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Economy
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerWallet : NetworkBehaviour
    {
        public NetworkVariable<long> Cash = new(
            GameConstants.StartingCash,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<long> Bank = new(
            GameConstants.StartingBank,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public event Action<long, long> WalletChanged;

        public override void OnNetworkSpawn()
        {
            Cash.OnValueChanged += HandleCashChanged;
            Bank.OnValueChanged += HandleBankChanged;

            if (IsServer && Cash.Value < 0)
                Cash.Value = GameConstants.StartingCash;
        }

        public override void OnNetworkDespawn()
        {
            Cash.OnValueChanged -= HandleCashChanged;
            Bank.OnValueChanged -= HandleBankChanged;
        }

        public bool CanAffordCash(long amount) => amount >= 0 && Cash.Value >= amount;
        public bool CanAffordBank(long amount) => amount >= 0 && Bank.Value >= amount;

        public bool TrySpendCashServer(long amount, string reason = "")
        {
            if (!IsServer || amount <= 0 || Cash.Value < amount)
                return false;

            Cash.Value -= amount;
            Debug.Log($"[Wallet] Client {OwnerClientId} spent ${amount}. Reason: {reason}");
            return true;
        }

        public bool TrySpendBankServer(long amount, string reason = "")
        {
            if (!IsServer || amount <= 0 || Bank.Value < amount)
                return false;

            Bank.Value -= amount;
            Debug.Log($"[Wallet] Client {OwnerClientId} spent ${amount} from bank. Reason: {reason}");
            return true;
        }

        public void AddCashServer(long amount, string reason = "")
        {
            if (!IsServer || amount <= 0)
                return;

            checked { Cash.Value += amount; }
            Debug.Log($"[Wallet] Client {OwnerClientId} received ${amount}. Reason: {reason}");
        }

        public void AddBankServer(long amount, string reason = "")
        {
            if (!IsServer || amount <= 0)
                return;

            checked { Bank.Value += amount; }
            Debug.Log($"[Wallet] Client {OwnerClientId} bank +${amount}. Reason: {reason}");
        }

        [Rpc(SendTo.Server)]
        public void RequestDepositRpc(long amount)
        {
            if (amount <= 0 || Cash.Value < amount)
                return;

            Cash.Value -= amount;
            Bank.Value += amount;
        }

        [Rpc(SendTo.Server)]
        public void RequestWithdrawRpc(long amount)
        {
            if (amount <= 0 || Bank.Value < amount)
                return;

            Bank.Value -= amount;
            Cash.Value += amount;
        }

        [Rpc(SendTo.Server)]
        public void RequestPoliceFineRpc(long amount)
        {
            if (amount <= 0 || Cash.Value < amount) return;
            Cash.Value -= amount;
            Debug.Log($"[Wallet] Client {OwnerClientId} paid a police fine of ${amount}.");
        }

        private void HandleCashChanged(long previous, long current) => WalletChanged?.Invoke(current, Bank.Value);
        private void HandleBankChanged(long previous, long current) => WalletChanged?.Invoke(Cash.Value, current);
    }
}
