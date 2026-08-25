using System;
using Unity.Collections;
using Unity.Netcode;

namespace CheatOnYourDayOnes.Inventory
{
    [Serializable]
    public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot>
    {
        public FixedString64Bytes ItemId;
        public int Quantity;

        public NetworkInventorySlot(FixedString64Bytes itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref Quantity);
        }

        public bool Equals(NetworkInventorySlot other)
        {
            return ItemId.Equals(other.ItemId) && Quantity == other.Quantity;
        }
    }
}
