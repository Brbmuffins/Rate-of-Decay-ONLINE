using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public Equipment equipment;
    public Health playerHealth;
    public Image[] slotImages;
    public Text[] stackCountTexts;
    public Sprite emptySlotSprite;
    public GameObject inventoryPanel; // drag InventoryPanel here
    public GameObject equipmentPanel; // drag EquipmentPanel here

    private CameraFollow camFollow;

    void Start()
    {
        inventoryPanel.SetActive(false);
        equipmentPanel.SetActive(false);

        for (int i = 0; i < slotImages.Length; i++)
        {
            InventorySlot slot = slotImages[i].gameObject.AddComponent<InventorySlot>();
            slot.inventory = inventory;
            slot.equipment = equipment;
            slot.playerHealth = playerHealth;
            slot.slotIndex = i;
        }
    }

    void Update()
    {
        if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<CameraFollow>();

        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            bool opening = !inventoryPanel.activeSelf;

            inventoryPanel.SetActive(opening);
            equipmentPanel.SetActive(opening);

            Cursor.lockState = opening ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = opening;
            if (camFollow != null) camFollow.inputLocked = opening;
        }

        if (inventoryPanel.activeSelf)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (i < inventory.items.Count)
            {
                InventoryEntry entry = inventory.items[i];
                slotImages[i].sprite = entry.item.icon;
                slotImages[i].color = Color.white;

                if (i < stackCountTexts.Length && stackCountTexts[i] != null)
                {
                    stackCountTexts[i].text = entry.count > 1 ? entry.count.ToString() : "";
                }
            }
            else
            {
                slotImages[i].sprite = emptySlotSprite;
                slotImages[i].color = new Color(1, 1, 1, 0.3f);

                if (i < stackCountTexts.Length && stackCountTexts[i] != null)
                {
                    stackCountTexts[i].text = "";
                }
            }
        }
    }
}