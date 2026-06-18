using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventoryEntry
{
    public ItemData item;
    public int count;

    public InventoryEntry(ItemData item, int count)
    {
        this.item = item;
        this.count = count;
    }
}

public class Inventory : MonoBehaviour
{
    public int slotCount = 10;
    public List<InventoryEntry> items = new List<InventoryEntry>();

    public bool AddItem(ItemData item)
    {
        if (item.stackable)
        {
            foreach (InventoryEntry entry in items)
            {
                if (entry.item == item && entry.count < item.maxStackSize)
                {
                    entry.count++;
                    Debug.Log("Added " + item.itemName + " to inventory (stack: " + entry.count + ").");
                    return true;
                }
            }
        }

        if (items.Count >= slotCount)
        {
            Debug.Log("Inventory full!");
            return false;
        }

        items.Add(new InventoryEntry(item, 1));
        Debug.Log("Added " + item.itemName + " to inventory.");
        return true;
    }

    public void RemoveItem(ItemData item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].item == item)
            {
                items[i].count--;
                Debug.Log("Removed " + item.itemName + " from inventory.");

                if (items[i].count <= 0)
                {
                    items.RemoveAt(i);
                }
                return;
            }
        }
    }

    public bool HasItem(ItemData item)
    {
        foreach (InventoryEntry entry in items)
        {
            if (entry.item == item && entry.count > 0) return true;
        }

        return false;
    }

    public bool UseItem(int index, Health health)
    {
        if (index < 0 || index >= items.Count) return false;

        ItemData item = items[index].item;
        if (item.itemType != ItemType.Consumable) return false;

        if (health != null && item.healAmount > 0f)
        {
            health.Heal(item.healAmount);
        }

        RemoveItem(item);
        return true;
    }
}
