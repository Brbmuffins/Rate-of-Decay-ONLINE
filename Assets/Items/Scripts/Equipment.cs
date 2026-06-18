using System.Collections.Generic;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    public Dictionary<EquipmentSlotType, ItemData> equippedItems = new Dictionary<EquipmentSlotType, ItemData>();

    public bool EquipItem(ItemData item, out ItemData previousItem)
    {
        previousItem = null;

        if (!item.equippable || item.equipSlot == EquipmentSlotType.None)
        {
            Debug.Log(item.itemName + " cannot be equipped.");
            return false;
        }

        equippedItems.TryGetValue(item.equipSlot, out previousItem);
        equippedItems[item.equipSlot] = item;
        Debug.Log("Equipped " + item.itemName + " to " + item.equipSlot);
        return true;
    }

    public ItemData UnequipItem(EquipmentSlotType slot)
    {
        if (equippedItems.TryGetValue(slot, out ItemData item))
        {
            equippedItems.Remove(slot);
            Debug.Log("Unequipped " + item.itemName + " from " + slot);
            return item;
        }

        return null;
    }

    public ItemData GetEquipped(EquipmentSlotType slot)
    {
        return equippedItems.TryGetValue(slot, out ItemData item) ? item : null;
    }
}
