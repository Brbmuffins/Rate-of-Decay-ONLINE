using UnityEngine;
using UnityEngine.UI;

public class EquipmentUI : MonoBehaviour
{
    public Equipment equipment;
    public Image[] slotImages;
    public EquipmentSlotType[] slotTypes;
    public Sprite emptySlotSprite;
    public GameObject equipmentPanel;

    void Update()
    {
        if (equipmentPanel.activeSelf)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        for (int i = 0; i < slotImages.Length; i++)
        {
            ItemData item = equipment.GetEquipped(slotTypes[i]);
            if (item != null)
            {
                slotImages[i].sprite = item.icon;
                slotImages[i].color = Color.white;
            }
            else
            {
                slotImages[i].sprite = emptySlotSprite;
                slotImages[i].color = new Color(1, 1, 1, 0.3f);
            }
        }
    }
}
