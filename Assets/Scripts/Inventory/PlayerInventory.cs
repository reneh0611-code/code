using System;
using CheatOnYourDayOnes.Core;
using CheatOnYourDayOnes.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Inventory
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerInventory : NetworkBehaviour
    {
        [SerializeField, Min(1)] private int maxSlots = GameConstants.DefaultInventorySlots;

        public NetworkList<NetworkInventorySlot> Slots { get; private set; }

        public event Action InventoryChanged;

        private void Awake()
        {
            Slots = new NetworkList<NetworkInventorySlot>(
                null,
                NetworkVariableReadPermission.Owner,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            Slots.OnListChanged += HandleListChanged;
        }

        public override void OnNetworkDespawn()
        {
            Slots.OnListChanged -= HandleListChanged;
        }

        public bool Contains(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
                return false;

            int total = 0;
            var fixedId = new FixedString64Bytes(itemId);

            foreach (var slot in Slots)
            {
                if (slot.ItemId.Equals(fixedId))
                    total += slot.Quantity;
            }

            return total >= quantity;
        }

        public bool TryAddItemServer(ItemData item, int quantity)
        {
            if (!IsServer || item == null || quantity <= 0)
                return false;

            return TryAddItemServer(item.ItemId, item.MaxStack, quantity);
        }

        public bool TryAddItemServer(string itemId, int maxStack, int quantity)
        {
            if (!IsServer || string.IsNullOrWhiteSpace(itemId) || maxStack <= 0 || quantity <= 0)
                return false;

            var fixedId = new FixedString64Bytes(itemId);
            int remaining = quantity;

            for (int i = 0; i < Slots.Count && remaining > 0; i++)
            {
                var slot = Slots[i];
                if (!slot.ItemId.Equals(fixedId) || slot.Quantity >= maxStack)
                    continue;

                int add = Mathf.Min(maxStack - slot.Quantity, remaining);
                slot.Quantity += add;
                remaining -= add;
                Slots[i] = slot;
            }

            while (remaining > 0 && Slots.Count < maxSlots)
            {
                int add = Mathf.Min(maxStack, remaining);
                Slots.Add(new NetworkInventorySlot(fixedId, add));
                remaining -= add;
            }

            return remaining == 0;
        }

        public bool TryRemoveItemServer(string itemId, int quantity)
        {
            if (!IsServer || string.IsNullOrWhiteSpace(itemId) || quantity <= 0 || !Contains(itemId, quantity))
                return false;

            var fixedId = new FixedString64Bytes(itemId);
            int remaining = quantity;

            for (int i = Slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = Slots[i];
                if (!slot.ItemId.Equals(fixedId))
                    continue;

                int remove = Mathf.Min(slot.Quantity, remaining);
                slot.Quantity -= remove;
                remaining -= remove;

                if (slot.Quantity <= 0)
                    Slots.RemoveAt(i);
                else
                    Slots[i] = slot;
            }

            return remaining == 0;
        }

        public bool ContainsPoliceContraband()
        {
            foreach (NetworkInventorySlot slot in Slots)
                if (IsPoliceContraband(slot.ItemId.ToString())) return true;
            return false;
        }

        public bool ConfiscatePoliceContrabandServer()
        {
            if (!IsServer) return false;
            bool removed = false;
            for (int i = Slots.Count - 1; i >= 0; i--)
            {
                if (!IsPoliceContraband(Slots[i].ItemId.ToString())) continue;
                Slots.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        [Rpc(SendTo.Server)]
        public void RequestPoliceConfiscationRpc() => ConfiscatePoliceContrabandServer();

        private static bool IsPoliceContraband(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            string normalized = itemId.ToLowerInvariant();
            return normalized.Contains("weapon") || normalized.Contains("gun") ||
                   normalized.Contains("pistol") || normalized.Contains("rifle") ||
                   normalized.Contains("shotgun") || normalized.Contains("taser") ||
                   normalized.Contains("knife");
        }

        private void HandleListChanged(NetworkListEvent<NetworkInventorySlot> changeEvent)
        {
            InventoryChanged?.Invoke();
        }
    }
}
