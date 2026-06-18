using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Inventory inventory;
    public Equipment equipment;
    public Health playerHealth;
    public int slotIndex;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotIndex >= inventory.items.Count) return;

        ItemData item = inventory.items[slotIndex].item;

        if (item.equippable)
        {
            if (equipment.EquipItem(item, out ItemData previousItem))
            {
                inventory.RemoveItem(item);

                if (previousItem != null)
                {
                    inventory.AddItem(previousItem);
                }
            }
        }
        else if (item.itemType == ItemType.Consumable)
        {
            inventory.UseItem(slotIndex, playerHealth);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (slotIndex >= inventory.items.Count) return;

        ItemData item = inventory.items[slotIndex].item;
        TooltipUI.Instance.Show(item, eventData.position);
        transform.localScale = Vector3.one * 1.08f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipUI.Instance.Hide();
        transform.localScale = Vector3.one;
    }
}
