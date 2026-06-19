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

    [Header("Gear Stats (attunement system)")]
    [Tooltip("Innate stat bonuses granted while this gear is equipped.")]
    public StatModifier[] baseModifiers;

    [Tooltip("How many attunements can be socketed into this gear.")]
    public int attunementSlots = 0;

    [Tooltip("Attunements currently socketed. Should not exceed attunementSlots.")]
    public Attunement[] installedAttunements;

    // Every active modifier: innate gear stats + all socketed attunements.
    public System.Collections.Generic.IEnumerable<StatModifier> AllModifiers()
    {
        if (baseModifiers != null)
            foreach (var m in baseModifiers) yield return m;

        if (installedAttunements != null)
            foreach (var att in installedAttunements)
                if (att != null && att.modifiers != null)
                    foreach (var m in att.modifiers) yield return m;
    }

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