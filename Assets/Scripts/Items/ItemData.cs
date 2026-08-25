using UnityEngine;

namespace CheatOnYourDayOnes.Items
{
    public enum ItemCategory
    {
        Food,
        Drink,
        Tool,
        Electronics,
        Clothes,
        BusinessItem,
        Misc
    }

    [CreateAssetMenu(fileName = "Item_", menuName = "Cheat On Your Day Ones/Items/Item Data")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId = "item.example";
        [SerializeField] private string displayName = "Example Item";
        [SerializeField] private ItemCategory category = ItemCategory.Misc;
        [SerializeField, Min(0f)] private float weight = 0.1f;
        [SerializeField, Min(0)] private int value = 1;
        [SerializeField, Min(1)] private int maxStack = 1;
        [SerializeField] private bool illegal;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemCategory Category => category;
        public float Weight => weight;
        public int Value => value;
        public int MaxStack => maxStack;
        public bool Illegal => illegal;

#if UNITY_EDITOR
        private void OnValidate()
        {
            itemId = itemId.Trim().ToLowerInvariant().Replace(" ", ".");
            if (string.IsNullOrWhiteSpace(itemId))
                itemId = name.ToLowerInvariant().Replace(" ", ".");
        }
#endif
    }
}
