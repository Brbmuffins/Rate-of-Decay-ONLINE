using UnityEngine;
using UnityEngine.EventSystems;

public class EquipmentSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Equipment equipment;
    public Inventory inventory;
    public EquipmentSlotType slotType;

    public void OnPointerClick(PointerEventData eventData)
    {
        ItemData item = equipment.GetEquipped(slotType);
        if (item == null) return;

        if (inventory.AddItem(item))
        {
            equipment.UnequipItem(slotType);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemData item = equipment.GetEquipped(slotType);
        if (item == null) return;

        TooltipUI.Instance.Show(item, eventData.position, "Click to unequip");
        transform.localScale = Vector3.one * 1.08f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipUI.Instance.Hide();
        transform.localScale = Vector3.one;
    }
}
