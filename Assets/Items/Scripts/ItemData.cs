using UnityEngine;

public enum EquipmentSlotType
{
    None,
    Head,
    Chest,
    Legs,
    Weapon
}

public enum ItemType
{
    Generic,
    Consumable,
    Equipment,
    QuestItem
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea]
    public string description;
    public bool stackable = true;
    public int maxStackSize = 99;

    public ItemType itemType = ItemType.Generic;
    public ItemRarity rarity = ItemRarity.Common;

    public bool equippable = false;
    public EquipmentSlotType equipSlot = EquipmentSlotType.None;

    public float healAmount = 0f;

    public Color RarityColor
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.3f, 0.9f, 0.3f);
                case ItemRarity.Rare: return new Color(0.3f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.7f, 0.3f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.6f, 0.1f);
                default: return Color.white;
            }
        }
    }
}